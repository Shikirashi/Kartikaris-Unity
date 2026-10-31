using Kartikaris.Combat;
using Kartikaris.Radar;
using UnityEngine;

namespace Kartikaris.Modules
{
    public enum KartikarisTrackingDroneFlightState
    {
        Outbound,
        Tracking,
        Returning
    }

    /// <summary>Terrain-following outbound/search, track, and return state machine.</summary>
    [DisallowMultipleComponent]
    public sealed class KartikarisTrackingDrone : KartikarisDeployable
    {
        [Header("Ground Following")]
        [SerializeField] private Terrain terrain;
        [SerializeField] private LayerMask groundLayers = ~0;
        [SerializeField, Min(0f)] private float groundClearance = 15f;
        [SerializeField, Min(1f)] private float fallbackGroundRayDistance = 10000f;

        [Header("Flight")]
        [SerializeField, Min(0f)] private float outboundTravelDistance = 500f;
        [SerializeField, Min(0.01f)] private float maximumMovementSpeed = 7f;
        [SerializeField, Min(0.01f)] private float acceleration = 7f;
        [SerializeField, Min(0.01f)] private float returnArrivalTolerance = 5f;

        [Header("Detection")]
        [SerializeField] private LayerMask combatTargetLayers = ~0;
        [SerializeField, Min(0f)] private float detectionRange = 100f;
        [SerializeField, Range(0.1f, 179f)] private float detectionConeFullAngle = 30f;
        [SerializeField, Range(0f, 89f)] private float detectionConeDownwardPitch = 45f;
        [SerializeField, Min(0.02f)] private float detectionInterval = 0.2f;
        [SerializeField, Range(8, 256)] private int detectionCapacity = 64;

        [Header("Tracking")]
        [Tooltip("Horizontal offset in the target's local space. Altitude still follows local ground clearance.")]
        [SerializeField] private Vector3 trackingOffset = new Vector3(0f, 0f, -20f);

        [Header("Battery")]
        [SerializeField, Min(0.1f)] private float maximumBatteryLifetime = 300f;

        [Header("Runtime Diagnostics")]
        [SerializeField] private KartikarisTrackingDroneFlightState state;
        [SerializeField] private Vector3 deploymentPosition;
        [SerializeField] private Vector3 deploymentDirection;
        [SerializeField] private float batteryRemaining;
        [SerializeField] private float distanceTravelledOutbound;
        [SerializeField] private float currentSpeed;
        [SerializeField] private KartikarisMechHealth trackedTarget;
        [SerializeField] private RadarContact trackedRadarContact;

        private Collider[] detectionBuffer;
        private float nextDetectionTime;
        private int spotSourceId;

        public KartikarisTrackingDroneFlightState State => state;
        public Vector3 DeploymentPosition => deploymentPosition;
        public Vector3 DeploymentDirection => deploymentDirection;
        public float BatteryRemaining => batteryRemaining;
        public float DistanceTravelledOutbound => distanceTravelledOutbound;
        public float CurrentSpeed => currentSpeed;
        public KartikarisMechHealth TrackedTarget => trackedTarget;
        public float MaximumMovementSpeed => maximumMovementSpeed;

        private void OnValidate()
        {
            groundClearance = Mathf.Max(0f, groundClearance);
            fallbackGroundRayDistance = Mathf.Max(1f, fallbackGroundRayDistance);
            outboundTravelDistance = Mathf.Max(0f, outboundTravelDistance);
            maximumMovementSpeed = Mathf.Max(0.01f, maximumMovementSpeed);
            acceleration = Mathf.Max(0.01f, acceleration);
            returnArrivalTolerance = Mathf.Max(0.01f, returnArrivalTolerance);
            detectionRange = Mathf.Max(0f, detectionRange);
            detectionConeFullAngle = Mathf.Clamp(detectionConeFullAngle, 0.1f, 179f);
            detectionConeDownwardPitch = Mathf.Clamp(detectionConeDownwardPitch, 0f, 89f);
            detectionInterval = Mathf.Max(0.02f, detectionInterval);
            detectionCapacity = Mathf.Clamp(detectionCapacity, 8, 256);
            maximumBatteryLifetime = Mathf.Max(0.1f, maximumBatteryLifetime);
        }

        protected override void OnDeployableInitialized()
        {
            deploymentPosition = transform.position;
            deploymentDirection = OwningMech != null
                ? Vector3.ProjectOnPlane(OwningMech.forward, Vector3.up)
                : Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            if (deploymentDirection.sqrMagnitude <= 0.000001f)
                deploymentDirection = Vector3.forward;
            deploymentDirection.Normalize();
            transform.rotation = Quaternion.LookRotation(deploymentDirection, Vector3.up);
            state = KartikarisTrackingDroneFlightState.Outbound;
            batteryRemaining = maximumBatteryLifetime;
            distanceTravelledOutbound = 0f;
            currentSpeed = 0f;
            trackedTarget = null;
            trackedRadarContact = null;
            spotSourceId = GetInstanceID();
            nextDetectionTime = Time.time;
            EnsureDetectionBuffer();
            transform.SetParent(null, true);
        }

        private void Update()
        {
            if (!IsDeployed || IsDestroyed)
                return;

            float deltaTime = Mathf.Max(0f, Time.deltaTime);
            batteryRemaining = Mathf.Max(0f, batteryRemaining - deltaTime);
            if (batteryRemaining <= 0f)
            {
                DestroyDeployable(KartikarisDeployableDestroyReason.BatteryExpired);
                return;
            }
            if (OwningMech == null)
            {
                DestroyDeployable(KartikarisDeployableDestroyReason.OwnerLost);
                return;
            }

            switch (state)
            {
                case KartikarisTrackingDroneFlightState.Outbound:
                    UpdateOutbound(deltaTime);
                    break;
                case KartikarisTrackingDroneFlightState.Tracking:
                    UpdateTracking(deltaTime);
                    break;
                case KartikarisTrackingDroneFlightState.Returning:
                    UpdateReturning(deltaTime);
                    break;
            }
        }

        public bool Recall()
        {
            if (!IsDeployed || IsDestroyed || batteryRemaining <= 0f ||
                state == KartikarisTrackingDroneFlightState.Returning)
            {
                return false;
            }

            ClearSpotContribution();
            state = KartikarisTrackingDroneFlightState.Returning;
            return true;
        }

        private void UpdateOutbound(float deltaTime)
        {
            Vector3 horizontalOffset = Vector3.ProjectOnPlane(
                transform.position - deploymentPosition,
                Vector3.up);
            distanceTravelledOutbound = Mathf.Max(
                distanceTravelledOutbound,
                Vector3.Dot(horizontalOffset, deploymentDirection));
            if (distanceTravelledOutbound >= outboundTravelDistance)
            {
                state = KartikarisTrackingDroneFlightState.Returning;
                return;
            }

            Vector3 destination = deploymentPosition +
                deploymentDirection * outboundTravelDistance;
            MoveToward(destination, deltaTime, deploymentDirection);

            if (Time.time >= nextDetectionTime)
            {
                nextDetectionTime = Time.time + detectionInterval;
                TryAcquireTarget();
            }
        }

        private void UpdateTracking(float deltaTime)
        {
            if (!IsTrackedTargetValid())
            {
                ClearSpotContribution();
                state = KartikarisTrackingDroneFlightState.Returning;
                return;
            }

            Vector3 offset = trackedTarget.transform.TransformVector(trackingOffset);
            offset.y = 0f;
            Vector3 destination = trackedTarget.transform.position + offset;
            Vector3 facing = Vector3.ProjectOnPlane(
                trackedTarget.transform.position - transform.position,
                Vector3.up);
            MoveToward(destination, deltaTime, facing);
        }

        private void UpdateReturning(float deltaTime)
        {
            Vector3 ownerPosition = OwningMech.position;
            Vector3 horizontalDelta = Vector3.ProjectOnPlane(
                ownerPosition - transform.position,
                Vector3.up);
            if (horizontalDelta.magnitude <= returnArrivalTolerance)
            {
                DestroyDeployable(KartikarisDeployableDestroyReason.Recovered);
                return;
            }

            Vector3 destination = ownerPosition;
            MoveToward(destination, deltaTime, horizontalDelta);
        }

        private void MoveToward(
            Vector3 destination,
            float deltaTime,
            Vector3 preferredFacing)
        {
            Vector3 previous = transform.position;
            currentSpeed = Mathf.MoveTowards(
                currentSpeed,
                maximumMovementSpeed,
                acceleration * deltaTime);
            float maximumStep = currentSpeed * deltaTime;
            Vector3 horizontalRemaining = Vector3.ProjectOnPlane(
                destination - previous,
                Vector3.up);
            Vector3 localWaypoint = previous;
            if (horizontalRemaining.sqrMagnitude > 0.000001f)
            {
                localWaypoint += horizontalRemaining.normalized *
                    Mathf.Min(maximumStep, horizontalRemaining.magnitude);
            }
            localWaypoint.y = ResolveFlightHeight(localWaypoint, previous.y);
            transform.position = Vector3.MoveTowards(previous, localWaypoint, maximumStep);

            Vector3 displacement = transform.position - previous;
            currentSpeed = deltaTime > Mathf.Epsilon
                ? Mathf.Min(maximumMovementSpeed, displacement.magnitude / deltaTime)
                : 0f;
            Vector3 horizontalFacing = Vector3.ProjectOnPlane(preferredFacing, Vector3.up);
            if (horizontalFacing.sqrMagnitude > 0.000001f)
                transform.rotation = Quaternion.LookRotation(horizontalFacing.normalized, Vector3.up);
        }

        private void TryAcquireTarget()
        {
            EnsureDetectionBuffer();
            int count = Physics.OverlapSphereNonAlloc(
                transform.position,
                detectionRange,
                detectionBuffer,
                combatTargetLayers,
                QueryTriggerInteraction.Collide);
            Vector3 horizontalForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            if (horizontalForward.sqrMagnitude <= 0.000001f)
                horizontalForward = deploymentDirection;
            horizontalForward.Normalize();
            Vector3 coneCenter = Quaternion.AngleAxis(
                detectionConeDownwardPitch,
                Vector3.Cross(Vector3.up, horizontalForward).normalized) * horizontalForward;
            float minimumDot = Mathf.Cos(
                detectionConeFullAngle * 0.5f * Mathf.Deg2Rad);

            KartikarisMechHealth nearestHealth = null;
            RadarContact nearestContact = null;
            float nearestDistanceSquared = float.PositiveInfinity;
            for (int index = 0; index < count; index++)
            {
                Collider candidateCollider = detectionBuffer[index];
                KartikarisMechHealth health = candidateCollider != null
                    ? candidateCollider.GetComponentInParent<KartikarisMechHealth>()
                    : null;
                if (health == null || !health.IsAlive || health == nearestHealth)
                    continue;

                KartikarisCombatAffiliation affiliation =
                    health.GetComponentInParent<KartikarisCombatAffiliation>();
                if (affiliation == null || !KartikarisCombatAffiliation.AreHostile(
                        OwnerAffiliation,
                        affiliation.Affiliation))
                {
                    continue;
                }

                Vector3 offset = health.transform.position - transform.position;
                float distanceSquared = offset.sqrMagnitude;
                if (distanceSquared <= 0.000001f ||
                    distanceSquared > detectionRange * detectionRange ||
                    Vector3.Dot(coneCenter, offset.normalized) < minimumDot)
                {
                    continue;
                }

                RadarContact radarContact = health.GetComponent<RadarContact>();
                if (radarContact == null)
                    radarContact = health.GetComponentInChildren<RadarContact>();
                if (radarContact == null || distanceSquared >= nearestDistanceSquared)
                    continue;

                nearestHealth = health;
                nearestContact = radarContact;
                nearestDistanceSquared = distanceSquared;
            }

            if (nearestHealth == null || nearestContact == null)
                return;

            trackedTarget = nearestHealth;
            trackedRadarContact = nearestContact;
            trackedRadarContact.AddSpotSource(spotSourceId);
            state = KartikarisTrackingDroneFlightState.Tracking;
        }

        private bool IsTrackedTargetValid()
        {
            if (trackedTarget == null || !trackedTarget.IsAlive ||
                trackedRadarContact == null ||
                !trackedRadarContact.isActiveAndEnabled ||
                !trackedRadarContact.gameObject.activeInHierarchy)
            {
                return false;
            }

            KartikarisCombatAffiliation affiliation =
                trackedTarget.GetComponentInParent<KartikarisCombatAffiliation>();
            return affiliation != null && KartikarisCombatAffiliation.AreHostile(
                OwnerAffiliation,
                affiliation.Affiliation);
        }

        private float ResolveFlightHeight(Vector3 position, float fallbackHeight)
        {
            if (TryGetGroundHeight(position, out float groundHeight))
                return groundHeight + groundClearance;
            return fallbackHeight;
        }

        private bool TryGetGroundHeight(Vector3 position, out float groundHeight)
        {
            Terrain selectedTerrain = terrain != null ? terrain : Terrain.activeTerrain;
            if (selectedTerrain != null && selectedTerrain.terrainData != null)
            {
                Vector3 terrainPosition = selectedTerrain.GetPosition();
                Vector3 size = selectedTerrain.terrainData.size;
                if (position.x >= terrainPosition.x && position.x <= terrainPosition.x + size.x &&
                    position.z >= terrainPosition.z && position.z <= terrainPosition.z + size.z)
                {
                    groundHeight = selectedTerrain.SampleHeight(position) + terrainPosition.y;
                    return true;
                }
            }

            Vector3 origin = position + Vector3.up * (fallbackGroundRayDistance * 0.5f);
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit,
                    fallbackGroundRayDistance, groundLayers, QueryTriggerInteraction.Ignore))
            {
                groundHeight = hit.point.y;
                return true;
            }

            groundHeight = 0f;
            return false;
        }

        protected override void OnDeployableCleanup(
            KartikarisDeployableDestroyReason reason)
        {
            ClearSpotContribution();
            trackedTarget = null;
            currentSpeed = 0f;
        }

        private void ClearSpotContribution()
        {
            if (trackedRadarContact != null)
                trackedRadarContact.RemoveSpotSource(spotSourceId);
            trackedRadarContact = null;
        }

        private void EnsureDetectionBuffer()
        {
            if (detectionBuffer == null || detectionBuffer.Length != detectionCapacity)
                detectionBuffer = new Collider[detectionCapacity];
        }
    }
}
