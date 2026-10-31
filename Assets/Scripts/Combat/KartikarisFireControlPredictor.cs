using Kartikaris.Player;
using UnityEngine;

namespace Kartikaris.Combat
{
    /// <summary>
    /// Predicts the current unguided Player shell path without spawning a
    /// projectile. The spatial equation deliberately uses the base velocity and
    /// base gravity: velocity x k and gravity x k^2 trace the same parabola.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KartikarisFireControlPredictor : MonoBehaviour
    {
        public enum PredictionKind
        {
            WorldImpact,
            AirburstDetonation
        }

        public readonly struct Prediction
        {
            public Vector3 Position { get; }
            public Vector3 Normal { get; }
            public float FlightTime { get; }
            public PredictionKind Kind { get; }
            public Collider HitCollider { get; }

            public Prediction(Vector3 position, Vector3 normal, float flightTime,
                PredictionKind kind, Collider hitCollider)
            {
                Position = position;
                Normal = normal;
                FlightTime = flightTime;
                Kind = kind;
                HitCollider = hitCollider;
            }
        }

        [Header("Explicit References")]
        [SerializeField] private MechFirstPersonController playerController;
        [SerializeField] private KartikarisCannonWeapon weapon;
        [Tooltip("Optional preferred Terrain. Terrain.activeTerrain is used only when this is empty.")]
        [SerializeField] private Terrain terrain;

        [Header("Trajectory Sampling")]
        [SerializeField] private LayerMask predictionLayers = ~0;
        [SerializeField, Min(0.01f)] private float baseTimeStep = 0.05f;
        [SerializeField, Min(0.1f)] private float maximumBaseFlightTime = 60f;
        [SerializeField, Min(0f)] private float sweepRadius = 0.05f;
        [SerializeField, Range(4, 64)] private int maximumHitsPerSegment = 16;
        [SerializeField, Min(1f)] private float fallbackGroundRayDistance = 10000f;

        [Header("Runtime Diagnostics")]
        [SerializeField] private bool hasPrediction;
        [SerializeField] private PredictionKind predictedKind;
        [SerializeField] private Vector3 predictedPosition;
        [SerializeField] private float predictedFlightTime;
        [SerializeField] private int sampledSegments;
        [SerializeField] private Vector3 predictionHeadingDirection;
        [SerializeField] private Vector3 predictionLaunchDirection;
        [SerializeField, Range(0f, 90f)] private float clampedPredictionElevationDegrees;

        private RaycastHit[] castHits;

        public MechFirstPersonController PlayerController => playerController;
        public KartikarisCannonWeapon Weapon => weapon;
        public LayerMask PredictionLayers => predictionLayers;
        public bool HasPrediction => hasPrediction;
        public Vector3 PredictedPosition => predictedPosition;
        public float PredictedFlightTime => predictedFlightTime;
        public int SampledSegments => sampledSegments;
        public Vector3 PredictionHeadingDirection => predictionHeadingDirection;
        public Vector3 PredictionLaunchDirection => predictionLaunchDirection;
        public float ClampedPredictionElevationDegrees => clampedPredictionElevationDegrees;

        private void Awake()
        {
            if (playerController == null)
                playerController = GetComponentInParent<MechFirstPersonController>();
            if (weapon == null && playerController != null)
                weapon = playerController.CannonWeapon;
            EnsureHitBuffer();
        }

        private void OnValidate()
        {
            baseTimeStep = Mathf.Max(0.01f, baseTimeStep);
            maximumBaseFlightTime = Mathf.Max(0.1f, maximumBaseFlightTime);
            sweepRadius = Mathf.Max(0f, sweepRadius);
            maximumHitsPerSegment = Mathf.Clamp(maximumHitsPerSegment, 4, 64);
            fallbackGroundRayDistance = Mathf.Max(1f, fallbackGroundRayDistance);
        }

        public bool TryPredict(out Prediction prediction)
        {
            prediction = default;
            hasPrediction = false;
            sampledSegments = 0;

            if (weapon == null || weapon.FirePoint == null ||
                weapon.CurrentShellDefinition == null)
            {
                return false;
            }

            KartikarisShellDefinition shell = weapon.CurrentShellDefinition;
            if (shell.ShellType == KartikarisShellType.Homing ||
                shell.ShellType == KartikarisShellType.GuidedMissile)
            {
                return false;
            }

            // Animator evaluation can restore the elevation pivot to its authored
            // base before this component's LateUpdate runs. Apply the same final
            // procedural pose that TryFire uses so prediction never depends on
            // script execution order and samples the actual launch direction.
            weapon.ApplyProceduralPoseAfterAnimation();
            Transform firePoint = weapon.FirePoint;
            Vector3 start = firePoint.position;
            Transform headingPivot = weapon.HeadingPivot;
            Vector3 referenceUp = playerController != null
                ? playerController.transform.up
                : weapon.transform.root.up;
            Vector3 torsoForward = headingPivot != null
                ? headingPivot.forward
                : (playerController != null
                    ? playerController.transform.forward
                    : weapon.transform.root.forward);
            predictionLaunchDirection = CalculateForwardHemisphereLaunchDirection(
                firePoint.forward,
                torsoForward,
                referenceUp,
                out predictionHeadingDirection,
                out clampedPredictionElevationDegrees);
            Vector3 baseVelocity = predictionLaunchDirection * weapon.EffectiveMuzzleVelocity;
            if (baseVelocity.sqrMagnitude <= 0.000001f)
                return false;

            float multiplier = Mathf.Max(0.01f, weapon.BallisticTravelSpeedMultiplier);
            bool airburst = shell.ShellType == KartikarisShellType.Airburst;
            bool airburstArmed = false;
            bool hasPreviousAltitude = TryGetGroundHeight(start, shell, out float previousGround);
            float previousAltitude = hasPreviousAltitude ? start.y - previousGround : 0f;
            if (hasPreviousAltitude &&
                previousAltitude >= shell.AirburstHeight + shell.AirburstArmingMargin)
            {
                airburstArmed = true;
            }

            Vector3 previous = start;
            float previousTime = 0f;
            int steps = Mathf.CeilToInt(maximumBaseFlightTime / baseTimeStep);
            for (int index = 1; index <= steps; index++)
            {
                float currentTime = Mathf.Min(index * baseTimeStep, maximumBaseFlightTime);
                Vector3 current = CalculatePosition(
                    start,
                    baseVelocity,
                    Physics.gravity,
                    currentTime);
                sampledSegments++;

                bool hasWorldHit = TryFindFirstValidHit(
                    previous,
                    current,
                    out RaycastHit worldHit,
                    out float worldHitFraction);

                if (airburst && TryGetGroundHeight(current, shell, out float currentGround))
                {
                    float currentAltitude = current.y - currentGround;
                    if (!airburstArmed &&
                        currentAltitude >= shell.AirburstHeight + shell.AirburstArmingMargin)
                    {
                        airburstArmed = true;
                    }

                    if (airburstArmed && hasPreviousAltitude &&
                        previousAltitude > shell.AirburstHeight &&
                        currentAltitude <= shell.AirburstHeight)
                    {
                        float span = previousAltitude - currentAltitude;
                        float fraction = span > Mathf.Epsilon
                            ? Mathf.Clamp01((previousAltitude - shell.AirburstHeight) / span)
                            : 1f;
                        if (!hasWorldHit || fraction <= worldHitFraction)
                        {
                            Vector3 detonation = Vector3.Lerp(previous, current, fraction);
                            if (TryGetGroundHeight(detonation, shell, out float crossingGround))
                                detonation.y = crossingGround + shell.AirburstHeight;
                            float baseFlightTime = Mathf.Lerp(previousTime, currentTime, fraction);
                            prediction = new Prediction(
                                detonation,
                                Vector3.up,
                                baseFlightTime / multiplier,
                                PredictionKind.AirburstDetonation,
                                null);
                            RecordPrediction(prediction);
                            return true;
                        }
                    }

                    previousAltitude = currentAltitude;
                    hasPreviousAltitude = true;
                }

                if (hasWorldHit)
                {
                    float baseFlightTime = Mathf.Lerp(previousTime, currentTime, worldHitFraction);
                    prediction = new Prediction(
                        worldHit.point,
                        worldHit.normal,
                        baseFlightTime / multiplier,
                        PredictionKind.WorldImpact,
                        worldHit.collider);
                    RecordPrediction(prediction);
                    return true;
                }

                previous = current;
                previousTime = currentTime;
            }

            return false;
        }

        public static Vector3 CalculatePosition(
            Vector3 start,
            Vector3 baseVelocity,
            Vector3 baseGravity,
            float baseTime)
        {
            float safeTime = Mathf.Max(0f, baseTime);
            return start + baseVelocity * safeTime +
                0.5f * baseGravity * safeTime * safeTime;
        }

        /// <summary>
        /// Reconstructs the assist-only firing direction from the torso heading
        /// and the muzzle's vertical angle. This keeps the always-visible assist
        /// in the torso-forward hemisphere while deployment/folding animation
        /// temporarily points the authored barrel pose backward or downward.
        /// Actual projectile launches continue to use the physical fire point.
        /// </summary>
        public static Vector3 CalculateForwardHemisphereLaunchDirection(
            Vector3 barrelForward,
            Vector3 torsoForward,
            Vector3 referenceUp,
            out Vector3 horizontalHeading,
            out float clampedElevationDegrees)
        {
            Vector3 up = referenceUp.sqrMagnitude > 0.000001f
                ? referenceUp.normalized
                : Vector3.up;
            horizontalHeading = Vector3.ProjectOnPlane(torsoForward, up);
            if (horizontalHeading.sqrMagnitude <= 0.000001f)
                horizontalHeading = Vector3.ProjectOnPlane(barrelForward, up);
            if (horizontalHeading.sqrMagnitude <= 0.000001f)
                horizontalHeading = Vector3.ProjectOnPlane(Vector3.forward, up);
            if (horizontalHeading.sqrMagnitude <= 0.000001f)
                horizontalHeading = Vector3.Cross(up, Vector3.right);
            horizontalHeading.Normalize();

            Vector3 normalizedBarrel = barrelForward.sqrMagnitude > 0.000001f
                ? barrelForward.normalized
                : horizontalHeading;
            float signedElevation = Mathf.Asin(Mathf.Clamp(
                Vector3.Dot(normalizedBarrel, up), -1f, 1f)) * Mathf.Rad2Deg;
            clampedElevationDegrees = Mathf.Clamp(signedElevation, 0f, 90f);
            float elevationRadians = clampedElevationDegrees * Mathf.Deg2Rad;
            return (horizontalHeading * Mathf.Cos(elevationRadians) +
                up * Mathf.Sin(elevationRadians)).normalized;
        }

        private bool TryFindFirstValidHit(
            Vector3 segmentStart,
            Vector3 segmentEnd,
            out RaycastHit nearestHit,
            out float segmentFraction)
        {
            nearestHit = default;
            segmentFraction = 1f;
            Vector3 segment = segmentEnd - segmentStart;
            float distance = segment.magnitude;
            if (distance <= Mathf.Epsilon)
                return false;

            EnsureHitBuffer();
            Vector3 direction = segment / distance;
            int hitCount = sweepRadius > 0f
                ? Physics.SphereCastNonAlloc(
                    segmentStart,
                    sweepRadius,
                    direction,
                    castHits,
                    distance,
                    predictionLayers,
                    QueryTriggerInteraction.Ignore)
                : Physics.RaycastNonAlloc(
                    segmentStart,
                    direction,
                    castHits,
                    distance,
                    predictionLayers,
                    QueryTriggerInteraction.Ignore);

            float nearestDistance = float.PositiveInfinity;
            Transform ownerRoot = playerController != null
                ? playerController.transform.root
                : weapon.transform.root;
            for (int index = 0; index < hitCount; index++)
            {
                RaycastHit candidate = castHits[index];
                Collider candidateCollider = candidate.collider;
                if (candidateCollider == null ||
                    (ownerRoot != null && candidateCollider.transform.IsChildOf(ownerRoot)) ||
                    candidateCollider.GetComponentInParent<KartikarisArtilleryProjectile>() != null ||
                    candidate.distance >= nearestDistance)
                {
                    continue;
                }

                nearestDistance = candidate.distance;
                nearestHit = candidate;
            }

            if (float.IsPositiveInfinity(nearestDistance))
                return false;

            segmentFraction = Mathf.Clamp01(nearestDistance / distance);
            return true;
        }

        private bool TryGetGroundHeight(
            Vector3 position,
            KartikarisShellDefinition shell,
            out float groundHeight)
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
            if (Physics.Raycast(
                    origin,
                    Vector3.down,
                    out RaycastHit hit,
                    fallbackGroundRayDistance,
                    shell.GroundLayers,
                    QueryTriggerInteraction.Ignore))
            {
                groundHeight = hit.point.y;
                return true;
            }

            groundHeight = 0f;
            return false;
        }

        private void EnsureHitBuffer()
        {
            if (castHits == null || castHits.Length != maximumHitsPerSegment)
                castHits = new RaycastHit[maximumHitsPerSegment];
        }

        private void RecordPrediction(Prediction prediction)
        {
            hasPrediction = true;
            predictedKind = prediction.Kind;
            predictedPosition = prediction.Position;
            predictedFlightTime = prediction.FlightTime;
        }
    }
}
