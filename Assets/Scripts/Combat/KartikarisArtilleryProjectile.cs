using Kartikaris.WorldInteraction;
using UnityEngine;

namespace Kartikaris.Combat
{
    /// <summary>
    /// Reusable gravity-driven shell. A Rigidbody with continuous collision
    /// detection provides swept high-speed collision against terrain and other
    /// non-trigger colliders.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class KartikarisArtilleryProjectile : MonoBehaviour
    {
        private const float MinimumLinearVelocityLimit = 1000f;

        [Header("Lifetime and Impact")]
        [SerializeField, Min(0.1f)] private float maximumLifetime = 30f;
        [SerializeField] private LayerMask validImpactLayers = ~0;
        [Tooltip("Normally off. Enable only when trigger volumes should count as shell impacts.")]
        [SerializeField] private bool destroyOnTriggerImpact;

        [Header("Flight Presentation")]
        [SerializeField, Min(0f)] private float minimumOrientationSpeed = 0.05f;

        [Header("Impact VFX")]
        [SerializeField] private GameObject impactVfxPrefab;
        [Tooltip("Zero lets a self-managed impact VFX destroy itself.")]
        [SerializeField, Min(0f)] private float impactVfxLifetime;

        [Header("Explosion Gameplay")]
        [SerializeField, Min(0f)] private float explosionRadius = 100f;
        [SerializeField, Min(0f)] private float fullDamageRadius = 30f;
        [SerializeField, Min(0f)] private float maximumMechDamage = 100f;
        [SerializeField] private LayerMask mechDamageLayers = ~0;
        [SerializeField, Min(0f)] private float treeEffectRadius = 30f;

        [Header("Optional Guided Missile Presentation")]
        [SerializeField] private Transform cameraMount;
        [SerializeField] private Camera missileCamera;

        [Header("Runtime Diagnostics")]
        [SerializeField] private bool isLaunched;
        [SerializeField] private bool impactResolved;
        [SerializeField] private Vector3 baseInitialVelocity;
        [SerializeField] private Vector3 initialVelocity;
        [SerializeField] private float ballisticTravelSpeedMultiplier = 1f;
        [SerializeField] private Vector3 effectiveGravity;
        [SerializeField] private float effectiveLaunchSpeed;
        [SerializeField] private float effectiveGravityMagnitude;
        [SerializeField] private GameObject sourceOwner;
        [SerializeField] private int sourceTeamId = -1;
        [SerializeField] private KartikarisShellType selectedShellType = KartikarisShellType.StandardHE;
        [SerializeField] private KartikarisMechHealth lockedHomingTarget;
        [SerializeField] private int lastDamagedMechCount;
        [SerializeField] private float lastTotalDamageApplied;
        [SerializeField] private int lastCrushedTreeCount;

        private Rigidbody body;
        private Collider[] projectileColliders;
        private float launchTime;
        private KartikarisTreeInteractionRuntime treeInteraction;
        private KartikarisShellDefinition shellDefinition;
        private KartikarisCombatAffiliation sourceAffiliation;
        private KartikarisGuidanceAntenna guidanceAntenna;
        private Vector2 guidanceInput;
        private bool airburstArmed;

        public float MaximumLifetime => maximumLifetime;
        public bool IsLaunched => isLaunched;
        public Vector3 BaseInitialVelocity => baseInitialVelocity;
        public Vector3 InitialVelocity => initialVelocity;
        public Vector3 CurrentVelocity => body != null ? body.linearVelocity : Vector3.zero;
        public float BallisticTravelSpeedMultiplier => ballisticTravelSpeedMultiplier;
        public Vector3 EffectiveGravity => effectiveGravity;
        public float EffectiveLaunchSpeed => effectiveLaunchSpeed;
        public float EffectiveGravityMagnitude => effectiveGravityMagnitude;
        public GameObject SourceOwner => sourceOwner;
        public int SourceTeamId => sourceTeamId;
        public KartikarisShellType SelectedShellType => selectedShellType;
        public KartikarisShellDefinition ShellDefinition => shellDefinition;
        public KartikarisCombatAffiliation SourceAffiliation => sourceAffiliation;
        public KartikarisMechHealth LockedHomingTarget => lockedHomingTarget;
        public Transform CameraMount => cameraMount;
        public Camera MissileCamera => missileCamera;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            projectileColliders = GetComponentsInChildren<Collider>(true);
            ConfigureBody();
        }

        private void Update()
        {
            if (isLaunched && Time.time - launchTime >= maximumLifetime)
                Destroy(gameObject);
        }

        private void FixedUpdate()
        {
            if (!isLaunched || impactResolved || body == null)
                return;

            // Rigidbody gravity is disabled so every projectile can own its time-scaled
            // acceleration without changing global Physics.gravity.
            body.AddForce(effectiveGravity, ForceMode.Acceleration);

            if (shellDefinition == null)
                return;

            switch (selectedShellType)
            {
                case KartikarisShellType.Airburst:
                    UpdateAirburst();
                    break;
                case KartikarisShellType.Homing:
                    UpdateHoming();
                    break;
                case KartikarisShellType.GuidedMissile:
                    UpdateGuidance();
                    break;
            }
        }

        private void LateUpdate()
        {
            if (!isLaunched || impactResolved || body == null)
                return;

            Vector3 velocity = body.linearVelocity;
            if (velocity.sqrMagnitude < minimumOrientationSpeed * minimumOrientationSpeed)
                return;

            Vector3 forward = velocity.normalized;
            Vector3 up = transform.up;
            if (Vector3.Cross(forward, up).sqrMagnitude <= 0.000001f)
                up = Mathf.Abs(Vector3.Dot(forward, Vector3.up)) < 0.999f
                    ? Vector3.up
                    : Vector3.right;

            transform.rotation = Quaternion.LookRotation(forward, up);
        }

        private void OnValidate()
        {
            maximumLifetime = Mathf.Max(0.1f, maximumLifetime);
            minimumOrientationSpeed = Mathf.Max(0f, minimumOrientationSpeed);
            impactVfxLifetime = Mathf.Max(0f, impactVfxLifetime);
            explosionRadius = Mathf.Max(0f, explosionRadius);
            fullDamageRadius = Mathf.Clamp(fullDamageRadius, 0f, explosionRadius);
            maximumMechDamage = Mathf.Max(0f, maximumMechDamage);
            treeEffectRadius = Mathf.Max(0f, treeEffectRadius);
        }

        /// <summary>
        /// Starts this projectile from a base world-space ballistic velocity.
        /// Runtime velocity is baseVelocity x k and per-projectile gravity is
        /// Physics.gravity x k^2, preserving the spatial parabola while scaling time.
        /// Returns false if the prefab has no usable Collider.
        /// </summary>
        public bool Launch(
            Vector3 worldVelocity,
            Transform ownerRoot = null,
            int ownerTeamId = -1,
            KartikarisTreeInteractionRuntime runtimeTreeInteraction = null,
            KartikarisShellDefinition selectedShell = null,
            KartikarisCombatAffiliation ownerAffiliation = null,
            KartikarisGuidanceAntenna ownerGuidanceAntenna = null,
            float travelSpeedMultiplier = 1f)
        {
            if (body == null)
                body = GetComponent<Rigidbody>();
            if (projectileColliders == null || projectileColliders.Length == 0)
                projectileColliders = GetComponentsInChildren<Collider>(true);

            if (body == null || !HasNonTriggerCollider())
            {
                Debug.LogError(
                    "Artillery projectile requires a Rigidbody and at least one non-trigger Collider.",
                    this);
                return false;
            }

            ConfigureBody();
            IgnoreOwnerCollisions(ownerRoot);

            baseInitialVelocity = worldVelocity;
            ballisticTravelSpeedMultiplier = Mathf.Max(0.01f, travelSpeedMultiplier);
            initialVelocity = baseInitialVelocity * ballisticTravelSpeedMultiplier;
            effectiveGravity = Physics.gravity *
                ballisticTravelSpeedMultiplier * ballisticTravelSpeedMultiplier;
            effectiveLaunchSpeed = initialVelocity.magnitude;
            effectiveGravityMagnitude = effectiveGravity.magnitude;
            sourceOwner = ownerRoot != null ? ownerRoot.gameObject : null;
            sourceTeamId = ownerTeamId;
            treeInteraction = runtimeTreeInteraction;
            shellDefinition = selectedShell;
            sourceAffiliation = ownerAffiliation;
            guidanceAntenna = ownerGuidanceAntenna;
            selectedShellType = selectedShell != null ? selectedShell.ShellType : KartikarisShellType.StandardHE;
            lockedHomingTarget = null;
            guidanceInput = Vector2.zero;
            airburstArmed = false;
            impactResolved = false;
            lastDamagedMechCount = 0;
            lastTotalDamageApplied = 0f;
            lastCrushedTreeCount = 0;
            body.maxLinearVelocity = Mathf.Max(
                body.maxLinearVelocity,
                Mathf.Max(MinimumLinearVelocityLimit, initialVelocity.magnitude));
            body.linearVelocity = initialVelocity;
            launchTime = Time.time;
            isLaunched = true;
            return true;
        }

        private void ConfigureBody()
        {
            if (body == null)
                return;

            body.isKinematic = false;
            body.useGravity = false;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.interpolation = RigidbodyInterpolation.Interpolate;
        }

        private void IgnoreOwnerCollisions(Transform ownerRoot)
        {
            if (ownerRoot == null)
                return;

            Collider[] ownerColliders = ownerRoot.GetComponentsInChildren<Collider>(true);
            for (int projectileIndex = 0; projectileIndex < projectileColliders.Length; projectileIndex++)
            {
                Collider projectileCollider = projectileColliders[projectileIndex];
                if (projectileCollider == null)
                    continue;

                for (int ownerIndex = 0; ownerIndex < ownerColliders.Length; ownerIndex++)
                {
                    Collider ownerCollider = ownerColliders[ownerIndex];
                    if (ownerCollider != null && ownerCollider != projectileCollider)
                        Physics.IgnoreCollision(projectileCollider, ownerCollider, true);
                }
            }
        }

        private bool HasNonTriggerCollider()
        {
            for (int index = 0; index < projectileColliders.Length; index++)
            {
                Collider projectileCollider = projectileColliders[index];
                if (projectileCollider != null && !projectileCollider.isTrigger)
                    return true;
            }

            return false;
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!isLaunched || impactResolved || !IsValidImpactLayer(collision.gameObject.layer))
                return;

            Vector3 impactPosition = transform.position;
            Vector3 impactNormal = FallbackImpactNormal();
            if (collision.contactCount > 0)
            {
                ContactPoint firstContact = collision.GetContact(0);
                impactPosition = firstContact.point;
                impactNormal = firstContact.normal;
            }

            ResolveImpact(impactPosition, impactNormal, collision.collider);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!isLaunched || impactResolved || !destroyOnTriggerImpact ||
                !IsValidImpactLayer(other.gameObject.layer))
            {
                return;
            }

            Vector3 impactPosition = other.ClosestPoint(transform.position);
            Vector3 impactNormal = transform.position - impactPosition;
            if (impactNormal.sqrMagnitude <= 0.000001f)
                impactNormal = FallbackImpactNormal();
            ResolveImpact(impactPosition, impactNormal.normalized, other);
        }

        public void SetGuidanceInput(Vector2 input)
        {
            if (selectedShellType == KartikarisShellType.GuidedMissile &&
                guidanceAntenna != null && guidanceAntenna.HasValidSignal)
                guidanceInput = Vector2.ClampMagnitude(input, 1f);
        }

        private void ResolveImpact(Vector3 impactPosition, Vector3 impactNormal, Collider directCollider = null)
        {
            impactResolved = true;
            Vector3 safeNormal = impactNormal.sqrMagnitude > 0.000001f
                ? impactNormal.normalized
                : Vector3.up;

            SpawnImpactVfx(impactPosition, safeNormal);
            KartikarisExplosion.Result result;
            if (shellDefinition == null)
            {
                result = KartikarisExplosion.Detonate(impactPosition, explosionRadius, fullDamageRadius,
                    maximumMechDamage, mechDamageLayers, sourceOwner, treeInteraction, treeEffectRadius);
            }
            else if (selectedShellType == KartikarisShellType.StandardHE)
            {
                result = KartikarisExplosion.Detonate(impactPosition, shellDefinition.ExplosionRadius,
                    shellDefinition.FullDamageRadius, shellDefinition.MaximumExplosionDamage,
                    mechDamageLayers, sourceOwner, treeInteraction, shellDefinition.TreeEffectRadius);
            }
            else if (selectedShellType == KartikarisShellType.Airburst)
            {
                result = KartikarisExplosion.DetonateCone(impactPosition, CurrentForwardDirection(),
                    shellDefinition.AirburstRadius, shellDefinition.AirburstConeFullAngle,
                    shellDefinition.AirburstDamage, mechDamageLayers, sourceOwner, treeInteraction,
                    shellDefinition.TreeEffectRadius);
            }
            else
            {
                float applied = ApplyDirectDamage(directCollider, shellDefinition.DirectHitDamage);
                result = new KartikarisExplosion.Result(applied > 0f ? 1 : 0, applied, 0);
            }
            lastDamagedMechCount = result.DamagedMechCount;
            lastTotalDamageApplied = result.TotalDamageApplied;
            lastCrushedTreeCount = result.CrushedTreeCount;
            Destroy(gameObject);
        }

        private void SpawnImpactVfx(Vector3 impactPosition, Vector3 impactNormal)
        {
            GameObject prefab = shellDefinition != null && shellDefinition.ImpactVfxOverride != null
                ? shellDefinition.ImpactVfxOverride
                : impactVfxPrefab;
            if (prefab == null)
                return;

            Quaternion rotation = Quaternion.FromToRotation(Vector3.up, impactNormal);
            GameObject instance = Instantiate(prefab, impactPosition, rotation);
            instance.transform.SetParent(null, true);
            if (impactVfxLifetime > 0f)
                Destroy(instance, impactVfxLifetime);
        }

        private Vector3 FallbackImpactNormal()
        {
            Vector3 velocity = body != null ? body.linearVelocity : Vector3.zero;
            return velocity.sqrMagnitude > 0.000001f
                ? -velocity.normalized
                : Vector3.up;
        }

        private bool IsValidImpactLayer(int layer)
        {
            return (validImpactLayers.value & (1 << layer)) != 0;
        }

        private void UpdateAirburst()
        {
            Vector3 currentPosition = body != null ? body.position : transform.position;
            if (!TryGetGroundHeight(currentPosition, out float groundHeight))
                return;

            float targetAltitude = shellDefinition.AirburstHeight;
            float altitude = currentPosition.y - groundHeight;
            if (altitude >= targetAltitude + shellDefinition.AirburstArmingMargin)
                airburstArmed = true;
            if (!airburstArmed)
                return;

            if (altitude <= targetAltitude)
            {
                currentPosition.y = groundHeight + targetAltitude;
                ResolveImpact(currentPosition, Vector3.up);
                return;
            }

            // Predict the next physics sample so high multipliers cannot step past
            // the configured world-space detonation altitude unnoticed.
            float step = Time.fixedDeltaTime;
            Vector3 predictedPosition = currentPosition +
                (body.linearVelocity + effectiveGravity * step) * step;
            if (!TryGetGroundHeight(predictedPosition, out float predictedGroundHeight))
                return;

            float predictedAltitude = predictedPosition.y - predictedGroundHeight;
            if (predictedAltitude > targetAltitude)
                return;

            float altitudeSpan = altitude - predictedAltitude;
            float crossingFraction = altitudeSpan > Mathf.Epsilon
                ? Mathf.Clamp01((altitude - targetAltitude) / altitudeSpan)
                : 1f;
            Vector3 detonationPosition = Vector3.Lerp(
                currentPosition,
                predictedPosition,
                crossingFraction);
            if (TryGetGroundHeight(detonationPosition, out float crossingGroundHeight))
                detonationPosition.y = crossingGroundHeight + targetAltitude;
            ResolveImpact(detonationPosition, Vector3.up);
        }

        private bool TryGetGroundHeight(Vector3 position, out float groundHeight)
        {
            Terrain terrain = Terrain.activeTerrain;
            if (terrain != null && terrain.terrainData != null)
            {
                Vector3 terrainPosition = terrain.GetPosition();
                Vector3 size = terrain.terrainData.size;
                if (position.x >= terrainPosition.x && position.x <= terrainPosition.x + size.x &&
                    position.z >= terrainPosition.z && position.z <= terrainPosition.z + size.z)
                {
                    groundHeight = terrain.SampleHeight(position) + terrainPosition.y;
                    return true;
                }
            }

            if (Physics.Raycast(position, Vector3.down, out RaycastHit hit, 10000f,
                    shellDefinition.GroundLayers, QueryTriggerInteraction.Ignore))
            {
                groundHeight = hit.point.y;
                return true;
            }
            groundHeight = 0f;
            return false;
        }

        private void UpdateHoming()
        {
            if (lockedHomingTarget == null || !lockedHomingTarget.IsAlive)
                lockedHomingTarget = FindFirstHostileTarget();
            if (lockedHomingTarget == null)
                return;
            SteerVelocityToward(lockedHomingTarget.transform.position - transform.position,
                shellDefinition.HomingTurnRateDegreesPerSecond);
        }

        private KartikarisMechHealth FindFirstHostileTarget()
        {
            Vector3 forward = CurrentForwardDirection();
            float minimumDot = Mathf.Cos(shellDefinition.HomingDetectionConeFullAngle * 0.5f * Mathf.Deg2Rad);
            Collider[] candidates = Physics.OverlapSphere(transform.position,
                shellDefinition.HomingDetectionRadius, mechDamageLayers, QueryTriggerInteraction.Collide);
            for (int index = 0; index < candidates.Length; index++)
            {
                KartikarisMechHealth health = candidates[index] != null
                    ? candidates[index].GetComponentInParent<KartikarisMechHealth>()
                    : null;
                if (!IsValidHostile(health))
                    continue;
                Vector3 offset = health.transform.position - transform.position;
                if (offset.sqrMagnitude > 0.000001f && Vector3.Dot(forward, offset.normalized) >= minimumDot)
                    return health;
            }
            return null;
        }

        private void UpdateGuidance()
        {
            if (guidanceAntenna == null || !guidanceAntenna.HasValidSignal)
            {
                guidanceInput = Vector2.zero;
                return;
            }
            if (guidanceInput.sqrMagnitude <= 0.000001f)
                return;
            Vector3 direction = CurrentForwardDirection();
            float step = shellDefinition.GuidedTurnRateDegreesPerSecond * Time.fixedDeltaTime;
            direction = Quaternion.AngleAxis(guidanceInput.x * step, Vector3.up) * direction;
            direction = Quaternion.AngleAxis(-guidanceInput.y * step, transform.right) * direction;
            SetVelocityDirection(direction);
        }

        private void SteerVelocityToward(Vector3 desiredDirection, float turnRateDegrees)
        {
            if (desiredDirection.sqrMagnitude <= 0.000001f)
                return;
            Vector3 direction = Vector3.RotateTowards(CurrentForwardDirection(), desiredDirection.normalized,
                turnRateDegrees * Mathf.Deg2Rad * Time.fixedDeltaTime, 0f);
            SetVelocityDirection(direction);
        }

        private void SetVelocityDirection(Vector3 direction)
        {
            float speed = body.linearVelocity.magnitude;
            if (speed > Mathf.Epsilon && direction.sqrMagnitude > 0.000001f)
                body.linearVelocity = direction.normalized * speed;
        }

        private Vector3 CurrentForwardDirection()
        {
            return body != null && body.linearVelocity.sqrMagnitude > 0.000001f
                ? body.linearVelocity.normalized
                : transform.forward;
        }

        private float ApplyDirectDamage(Collider directCollider, float damage)
        {
            KartikarisMechHealth health = directCollider != null
                ? directCollider.GetComponentInParent<KartikarisMechHealth>()
                : null;
            return IsValidHostile(health) ? health.ApplyDamage(damage, sourceOwner, transform.position) : 0f;
        }

        private bool IsValidHostile(KartikarisMechHealth health)
        {
            if (health == null || !health.IsAlive || (sourceOwner != null && health.transform.root.gameObject == sourceOwner))
                return false;
            if (sourceAffiliation == null)
                return true;
            KartikarisCombatAffiliation targetAffiliation = health.GetComponentInParent<KartikarisCombatAffiliation>();
            return sourceAffiliation.IsHostileTo(targetAffiliation);
        }
    }
}
