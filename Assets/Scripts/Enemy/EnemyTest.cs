using System;
using Kartikaris.Audio;
using Kartikaris.Combat;
using Kartikaris.Player;
using UnityEngine;
using UnityEngine.Serialization;

namespace Kartikaris.Enemy
{
    /// <summary>
    /// Autonomous artillery prototype using the shared cannon/projectile path.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController), typeof(MechTreeCrusher))]
    public sealed class EnemyTest : MonoBehaviour
    {
        public enum PrototypeState
        {
            Disabled,
            TransitioningToFire,
            Preparing,
            PostFireVulnerability,
            TransitioningToTravel,
            Relocating,
            Settling
        }

        private static readonly int CannonParameter = Animator.StringToHash("cannon");
        private const float MinimumDuration = 0.01f;

        [Header("Scene References")]
        [SerializeField] private Transform player;
        [SerializeField] private MechFirstPersonController playerMovement;
        [SerializeField] private Terrain targetTerrain;
        [SerializeField] private Animator enemyAnimator;
        [SerializeField] private KartikarisSoundPropagation soundPropagation;
        [SerializeField] private KartikarisCannonWeapon cannonWeapon;

        [Header("Initial Placement")]
        [FormerlySerializedAs("maximumSpawnDistance")]
        [SerializeField, Min(0f)] private float minimumSpawnDistance = 300f;
        [SerializeField, Min(0f)] private float spawnMaximumDistance = 1000f;
        [SerializeField, Range(1, 128)] private int spawnPlacementAttempts = 64;
        [SerializeField] private float groundOffset;

        [Header("Artillery Fire")]
        [SerializeField, Min(MinimumDuration)] private float modeTransitionDuration = 2f;
        [SerializeField, Min(MinimumDuration)] private float prepareDuration = 2f;
        [SerializeField, Min(MinimumDuration)] private float maximumAimDuration = 10f;
        [SerializeField, Range(0.1f, 10f)] private float firingAlignmentToleranceDegrees = 1.5f;
        [SerializeField] private KartikarisTrajectoryPreference trajectoryPreference = KartikarisTrajectoryPreference.HighAngle;
        [SerializeField, Min(0f)] private float postFireVulnerabilityDuration = 5f;

        [Header("Relocation")]
        [SerializeField, Min(0f)] private float maximumRelocationDistance = 100f;
        [SerializeField, Min(0.05f)] private float arrivalTolerance = 1f;
        [SerializeField, Range(0f, 90f)] private float movementAlignmentDegrees = 15f;
        [SerializeField, Min(0f)] private float settleDuration = 1f;
        [SerializeField, Range(0f, 90f)] private float maximumRelocationSlope = 35f;
        [SerializeField, Range(1, 32)] private int destinationAttempts = 12;

        [Header("Runtime Diagnostics")]
        [SerializeField] private PrototypeState currentState = PrototypeState.Disabled;
        [SerializeField] private Vector3 currentDestination;
        [SerializeField] private Vector3 currentNotionalFireTarget;
        [SerializeField] private Vector3 lastFireSourcePosition;
        [SerializeField] private float timeUntilNextState;
        [SerializeField] private float longitudinalSpeed;
        [SerializeField] private float verticalVelocity;
        [SerializeField, Range(-1f, 1f)] private float leftTrackCommand;
        [SerializeField, Range(-1f, 1f)] private float rightTrackCommand;
        [SerializeField] private bool isPivotTurning;
        [SerializeField] private int realFireCount;
        [SerializeField] private int completedRelocationCount;
        [SerializeField] private bool hasRelocationDestination;
        [SerializeField] private bool hasBallisticSolution;
        [SerializeField] private Vector3 solvedLaunchDirection;
        [SerializeField] private float solvedFlightTime;
        [SerializeField] private float aimElapsed;
        [SerializeField] private bool postFireVulnerabilityActive;
        [SerializeField] private float postFireVulnerabilityRemaining;

        private System.Random random;
        private bool canControlCannon;
        private CharacterController characterController;
        private MechTreeCrusher treeCrusher;

        public Transform Player => player;
        public Terrain TargetTerrain => targetTerrain;
        public Animator EnemyAnimator => enemyAnimator;
        public KartikarisSoundPropagation SoundPropagation => soundPropagation;
        public PrototypeState CurrentState => currentState;
        public Vector3 CurrentDestination => currentDestination;
        public Vector3 CurrentNotionalFireTarget => currentNotionalFireTarget;
        public Vector3 LastFireSourcePosition => lastFireSourcePosition;
        public float TimeUntilNextState => timeUntilNextState;
        public float MinimumSpawnDistance => minimumSpawnDistance;
        public float MaximumSpawnDistance => spawnMaximumDistance;
        public float MaximumRelocationDistance => maximumRelocationDistance;
        public float ModeTransitionDuration => modeTransitionDuration;
        public float CurrentLongitudinalSpeed => longitudinalSpeed;
        public float ForwardSpeed => playerMovement != null ? playerMovement.ForwardSpeed : 0f;
        public float Acceleration => playerMovement != null ? playerMovement.Acceleration : 0f;
        public float Deceleration => playerMovement != null ? playerMovement.Deceleration : 0f;
        public float TurnSpeedDegreesPerSecond =>
            playerMovement != null ? playerMovement.PivotTurnSpeedDegreesPerSecond : 0f;
        public float PivotTurnSpeedDegreesPerSecond =>
            playerMovement != null ? playerMovement.PivotTurnSpeedDegreesPerSecond : 0f;
        public float MovingTurnSpeedDegreesPerSecond =>
            playerMovement != null ? playerMovement.MovingTurnSpeedDegreesPerSecond : 0f;
        public float LeftTrackCommand => leftTrackCommand;
        public float RightTrackCommand => rightTrackCommand;
        public bool IsPivotTurning => isPivotTurning;
        public int RealFireCount => realFireCount;
        public int CompletedRelocationCount => completedRelocationCount;
        public float PostFireVulnerabilityDuration => postFireVulnerabilityDuration;
        public bool IsPostFireVulnerabilityActive => postFireVulnerabilityActive;
        public float PostFireVulnerabilityRemaining => postFireVulnerabilityRemaining;

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
            treeCrusher = GetComponent<MechTreeCrusher>();
            ResolveReferences();
        }

        private void Start()
        {
            ResolveReferences();
            random = new System.Random(Environment.TickCount ^ GetInstanceID());
            PlaceRandomlyNearPlayer();
            InitializeAnimator();

            if (targetTerrain == null || targetTerrain.terrainData == null)
            {
                currentState = PrototypeState.Disabled;
                Debug.LogError("Enemy prototype requires a Terrain with valid TerrainData.", this);
                return;
            }

            BeginTransitionToFire();
        }

        private void Update()
        {
            AdvanceStateMachine(Time.deltaTime);
        }

        private void Reset()
        {
            ResolveReferences();
        }

        private void OnValidate()
        {
            minimumSpawnDistance = Mathf.Max(0f, minimumSpawnDistance);
            spawnMaximumDistance = Mathf.Max(minimumSpawnDistance, spawnMaximumDistance);
            spawnPlacementAttempts = Mathf.Clamp(spawnPlacementAttempts, 1, 128);
            modeTransitionDuration = Mathf.Max(MinimumDuration, modeTransitionDuration);
            prepareDuration = Mathf.Max(MinimumDuration, prepareDuration);
            maximumAimDuration = Mathf.Max(MinimumDuration, maximumAimDuration);
            firingAlignmentToleranceDegrees = Mathf.Clamp(firingAlignmentToleranceDegrees, 0.1f, 10f);
            postFireVulnerabilityDuration = Mathf.Max(0f, postFireVulnerabilityDuration);
            postFireVulnerabilityRemaining = Mathf.Max(0f, postFireVulnerabilityRemaining);
            postFireVulnerabilityActive = postFireVulnerabilityRemaining > 0f;
            maximumRelocationDistance = Mathf.Max(0f, maximumRelocationDistance);
            arrivalTolerance = Mathf.Max(0.05f, arrivalTolerance);
            movementAlignmentDegrees = Mathf.Clamp(movementAlignmentDegrees, 0f, 90f);
            settleDuration = Mathf.Max(0f, settleDuration);
            maximumRelocationSlope = Mathf.Clamp(maximumRelocationSlope, 0f, 90f);
            destinationAttempts = Mathf.Clamp(destinationAttempts, 1, 32);
        }

        [ContextMenu("Enemy Prototype/Place Randomly In Player Annulus")]
        public void PlaceRandomlyNearPlayer()
        {
            ResolveReferences();
            EnsureRandom();
            if (player == null)
            {
                Debug.LogError("Enemy test placement requires a Player reference.", this);
                return;
            }

            if (targetTerrain == null || targetTerrain.terrainData == null)
            {
                Debug.LogError("Enemy test placement requires a Terrain with valid TerrainData.", this);
                return;
            }

            if (!TryChooseInitialSpawnPoint(out Vector3 placement))
            {
                Debug.LogError(
                    $"Enemy test could not find a valid Terrain point between " +
                    $"{minimumSpawnDistance:0.0} m and {spawnMaximumDistance:0.0} m from the Player. " +
                    "The Enemy was not moved.",
                    this);
                return;
            }

            transform.position = placement;

            float horizontalDistance = HorizontalDistance(player.position, placement);
            Debug.Log(
                $"Placed test enemy {horizontalDistance:0.0} m from the player at {placement}.",
                this);
        }

        private void AdvanceStateMachine(float deltaTime)
        {
            float safeDeltaTime = Mathf.Max(0f, deltaTime);
            if (currentState != PrototypeState.Relocating)
                ApplyTankMovement(0f, 0f, safeDeltaTime);

            switch (currentState)
            {
                case PrototypeState.TransitioningToFire:
                    timeUntilNextState -= safeDeltaTime;
                    if (timeUntilNextState <= 0f)
                        BeginPreparing();
                    break;

                case PrototypeState.Preparing:
                    UpdateArtilleryAim(safeDeltaTime);
                    break;

                case PrototypeState.PostFireVulnerability:
                    AdvancePostFireVulnerability(safeDeltaTime);
                    break;

                case PrototypeState.TransitioningToTravel:
                    timeUntilNextState -= safeDeltaTime;
                    if (timeUntilNextState <= 0f)
                        BeginRelocatingOrSettling();
                    break;

                case PrototypeState.Relocating:
                    MoveTowardDestination(safeDeltaTime);
                    break;

                case PrototypeState.Settling:
                    timeUntilNextState -= safeDeltaTime;
                    if (timeUntilNextState <= 0f)
                        BeginTransitionToFire();
                    break;
            }
        }

        private void BeginTransitionToFire()
        {
            cannonWeapon?.PrepareForFireTransition();
            SetCannon(true);
            currentState = PrototypeState.TransitioningToFire;
            timeUntilNextState = Mathf.Max(MinimumDuration, modeTransitionDuration);
        }

        private void BeginPreparing()
        {
            cannonWeapon?.EnterFireMode();
            currentNotionalFireTarget = player != null ? player.position : transform.position;
            currentState = PrototypeState.Preparing;
            timeUntilNextState = Mathf.Max(MinimumDuration, maximumAimDuration);
            aimElapsed = 0f;
            hasBallisticSolution = TryPrepareBallisticSolution();
            if (!hasBallisticSolution)
                AbortFireAndRelocate();
        }

        private bool TryPrepareBallisticSolution()
        {
            if (cannonWeapon == null || cannonWeapon.FirePoint == null || player == null)
                return false;
            currentNotionalFireTarget = player.position;
            if (TrySolveAndApplyAim(trajectoryPreference))
                return true;
            return trajectoryPreference == KartikarisTrajectoryPreference.HighAngle &&
                TrySolveAndApplyAim(KartikarisTrajectoryPreference.LowAngle);
        }

        private bool TrySolveAndApplyAim(KartikarisTrajectoryPreference preference)
        {
            if (!KartikarisBallisticSolver.TrySolveTrajectory(cannonWeapon.FirePoint.position,
                    currentNotionalFireTarget, cannonWeapon.EffectiveMuzzleVelocity, Physics.gravity,
                    cannonWeapon.BallisticTravelSpeedMultiplier, preference,
                    out Vector3 launchDirection, out float flightTime))
                return false;

            float elevation = Mathf.Asin(Mathf.Clamp(
                Vector3.Dot(launchDirection, transform.up), -1f, 1f)) * Mathf.Rad2Deg;
            if (elevation < cannonWeapon.MinimumElevationDegrees - 0.001f ||
                elevation > cannonWeapon.MaximumElevationDegrees + 0.001f)
                return false;
            Vector3 horizontal = Vector3.ProjectOnPlane(launchDirection, transform.up);
            if (horizontal.sqrMagnitude <= 0.000001f)
                horizontal = transform.forward;
            float heading = Vector3.SignedAngle(transform.forward, horizontal.normalized, transform.up);
            solvedLaunchDirection = launchDirection;
            solvedFlightTime = flightTime;
            cannonWeapon.SetAimDegrees(heading, elevation);
            return true;
        }

        private void UpdateArtilleryAim(float deltaTime)
        {
            aimElapsed += deltaTime;
            timeUntilNextState -= deltaTime;
            if (!hasBallisticSolution || cannonWeapon == null || cannonWeapon.FirePoint == null)
            {
                AbortFireAndRelocate();
                return;
            }
            if (aimElapsed >= prepareDuration &&
                Vector3.Angle(cannonWeapon.FirePoint.forward, solvedLaunchDirection) <=
                    firingAlignmentToleranceDegrees && cannonWeapon.TryFire())
            {
                lastFireSourcePosition = cannonWeapon.FirePoint.position;
                realFireCount++;
                StartPostFireVulnerability();
                return;
            }
            if (timeUntilNextState <= 0f)
                AbortFireAndRelocate();
        }

        private void AbortFireAndRelocate()
        {
            ClearPostFireVulnerability();
            BeginRelocationChoiceAndTravel();
        }

        private void StartPostFireVulnerability()
        {
            hasBallisticSolution = false;
            postFireVulnerabilityRemaining = Mathf.Max(0f, postFireVulnerabilityDuration);
            postFireVulnerabilityActive = postFireVulnerabilityRemaining > 0f;
            if (!postFireVulnerabilityActive)
            {
                BeginRelocationChoiceAndTravel();
                return;
            }

            currentState = PrototypeState.PostFireVulnerability;
            timeUntilNextState = postFireVulnerabilityRemaining;
        }

        private void AdvancePostFireVulnerability(float deltaTime)
        {
            postFireVulnerabilityRemaining = Mathf.Max(
                0f,
                postFireVulnerabilityRemaining - Mathf.Max(0f, deltaTime));
            postFireVulnerabilityActive = postFireVulnerabilityRemaining > 0f;
            timeUntilNextState = postFireVulnerabilityRemaining;
            if (!postFireVulnerabilityActive)
                BeginRelocationChoiceAndTravel();
        }

        private void ClearPostFireVulnerability()
        {
            postFireVulnerabilityActive = false;
            postFireVulnerabilityRemaining = 0f;
        }

        private void BeginRelocationChoiceAndTravel()
        {
            hasBallisticSolution = false;

            hasRelocationDestination = TryChooseTerrainPoint(
                transform.position,
                maximumRelocationDistance,
                maximumRelocationSlope,
                destinationAttempts,
                out currentDestination);
            BeginTransitionToTravel();
        }

        private void BeginTransitionToTravel()
        {
            cannonWeapon?.BeginTravelReset(modeTransitionDuration);
            SetCannon(false);
            currentState = PrototypeState.TransitioningToTravel;
            timeUntilNextState = Mathf.Max(MinimumDuration, modeTransitionDuration);
        }

        private void BeginRelocatingOrSettling()
        {
            cannonWeapon?.CompleteTravelReset();
            if (!hasRelocationDestination)
            {
                BeginSettling();
                return;
            }

            currentState = PrototypeState.Relocating;
            timeUntilNextState = 0f;
        }

        private void MoveTowardDestination(float deltaTime)
        {
            Vector3 currentPosition = transform.position;
            Vector3 horizontalDelta = currentDestination - currentPosition;
            horizontalDelta.y = 0f;
            float distance = horizontalDelta.magnitude;
            float safeDeceleration = Mathf.Max(Mathf.Epsilon, Deceleration);
            float stoppingDistance = longitudinalSpeed * longitudinalSpeed / (2f * safeDeceleration);
            if (distance <= arrivalTolerance && longitudinalSpeed <= 0.05f)
            {
                longitudinalSpeed = 0f;
                leftTrackCommand = 0f;
                rightTrackCommand = 0f;
                isPivotTurning = false;
                completedRelocationCount++;
                BeginSettling();
                return;
            }

            if (distance <= Mathf.Epsilon)
            {
                ApplyTankMovement(0f, 0f, deltaTime);
                return;
            }

            Vector3 direction = horizontalDelta / distance;
            float signedAngle = Vector3.SignedAngle(transform.forward, direction, Vector3.up);
            bool isAligned = Mathf.Abs(signedAngle) <= movementAlignmentDegrees;
            float safeAlignment = Mathf.Max(0.1f, movementAlignmentDegrees);
            float turnInput = Mathf.Clamp(signedAngle / safeAlignment, -1f, 1f);
            bool shouldBrake = distance <= stoppingDistance + arrivalTolerance;
            float throttleInput = isAligned && !shouldBrake ? 1f : 0f;
            ApplyTankMovement(throttleInput, turnInput, deltaTime, Mathf.Abs(signedAngle));
        }

        private void ApplyTankMovement(
            float throttleInput,
            float turnInput,
            float deltaTime,
            float remainingTurnDegrees = float.PositiveInfinity)
        {
            if (characterController == null)
                characterController = GetComponent<CharacterController>();
            if (treeCrusher == null)
                treeCrusher = GetComponent<MechTreeCrusher>();
            if (characterController == null || playerMovement == null)
                return;

            float safeDeltaTime = Mathf.Max(0f, deltaTime);
            TrackedDriveModel.Output drive = TrackedDriveModel.Evaluate(
                throttleInput,
                turnInput,
                longitudinalSpeed,
                playerMovement.ForwardSpeed,
                playerMovement.ReverseSpeed,
                playerMovement.StationarySpeedThreshold,
                playerMovement.PivotTurnSpeedDegreesPerSecond,
                playerMovement.MovingTurnSpeedDegreesPerSecond,
                playerMovement.MovingTurnSpeedMultiplier);
            leftTrackCommand = drive.LeftTrackCommand;
            rightTrackCommand = drive.RightTrackCommand;
            isPivotTurning = drive.IsPivotTurn;

            float turnDegrees = drive.YawRateDegreesPerSecond * safeDeltaTime;
            if (!float.IsPositiveInfinity(remainingTurnDegrees))
                turnDegrees = Mathf.Sign(turnDegrees) * Mathf.Min(Mathf.Abs(turnDegrees), remainingTurnDegrees);
            transform.Rotate(0f, turnDegrees, 0f, Space.Self);

            float targetSpeed = Mathf.Clamp01(throttleInput) * ForwardSpeed * drive.LongitudinalSpeedMultiplier;
            float changeRate = targetSpeed > longitudinalSpeed ? Acceleration : Deceleration;
            longitudinalSpeed = Mathf.MoveTowards(
                longitudinalSpeed,
                targetSpeed,
                changeRate * safeDeltaTime);

            if (characterController.isGrounded && verticalVelocity < 0f)
                verticalVelocity = playerMovement.GroundedVerticalVelocity;

            verticalVelocity += playerMovement.Gravity * safeDeltaTime;
            Vector3 horizontalDisplacement = transform.forward * longitudinalSpeed * safeDeltaTime;
            if (treeCrusher != null &&
                treeCrusher.ResolveHorizontalMovement(horizontalDisplacement, out Vector3 resolvedDisplacement))
            {
                horizontalDisplacement = resolvedDisplacement;
                longitudinalSpeed = 0f;
            }

            Vector3 verticalDisplacement = Vector3.up * verticalVelocity * safeDeltaTime;
            characterController.Move(horizontalDisplacement + verticalDisplacement);
        }

        private void BeginSettling()
        {
            hasRelocationDestination = false;
            currentState = PrototypeState.Settling;
            timeUntilNextState = Mathf.Max(0f, settleDuration);
        }

        private bool TryChooseTerrainPoint(
            Vector3 origin,
            float radius,
            float maximumSlope,
            int attempts,
            out Vector3 chosenPoint)
        {
            chosenPoint = GroundPoint(origin);
            if (targetTerrain == null || targetTerrain.terrainData == null)
                return false;

            EnsureRandom();
            int safeAttempts = Mathf.Max(1, attempts);
            for (int attempt = 0; attempt < safeAttempts; attempt++)
            {
                Vector2 offset = NextInsideUnitCircle() * Mathf.Max(0f, radius);
                Vector3 candidate = ClampToTerrainBounds(new Vector3(
                    origin.x + offset.x,
                    0f,
                    origin.z + offset.y));
                candidate = GroundPoint(candidate);

                if (IsWithinPlayerDistanceBand(candidate) &&
                    GetTerrainSlope(candidate) <= maximumSlope)
                {
                    chosenPoint = candidate;
                    return true;
                }
            }

            return false;
        }

        private bool TryChooseInitialSpawnPoint(out Vector3 chosenPoint)
        {
            chosenPoint = transform.position;
            if (targetTerrain == null || targetTerrain.terrainData == null || player == null)
                return false;

            EnsureRandom();
            Vector3 terrainPosition = targetTerrain.GetPosition();
            Vector3 terrainSize = targetTerrain.terrainData.size;
            const float edgeInset = 0.1f;
            float minimumX = terrainPosition.x + edgeInset;
            float maximumX = terrainPosition.x + terrainSize.x - edgeInset;
            float minimumZ = terrainPosition.z + edgeInset;
            float maximumZ = terrainPosition.z + terrainSize.z - edgeInset;

            int safeAttempts = Mathf.Max(1, spawnPlacementAttempts);
            for (int attempt = 0; attempt < safeAttempts; attempt++)
            {
                Vector3 candidate = new Vector3(
                    Mathf.Lerp(minimumX, maximumX, (float)random.NextDouble()),
                    0f,
                    Mathf.Lerp(minimumZ, maximumZ, (float)random.NextDouble()));
                if (!IsWithinPlayerDistanceBand(candidate))
                    continue;

                candidate = GroundPoint(candidate);
                chosenPoint = candidate;
                return true;
            }

            return false;
        }

        private bool IsWithinPlayerDistanceBand(Vector3 candidate)
        {
            if (player == null)
                return true;

            float distance = HorizontalDistance(player.position, candidate);
            return distance >= minimumSpawnDistance && distance <= spawnMaximumDistance;
        }

        private Vector3 GroundPoint(Vector3 worldPosition)
        {
            if (targetTerrain == null || targetTerrain.terrainData == null)
                return worldPosition;

            Vector3 grounded = ClampToTerrainBounds(worldPosition);
            grounded.y = targetTerrain.SampleHeight(grounded) + targetTerrain.GetPosition().y + groundOffset;
            return grounded;
        }

        private Vector3 ClampToTerrainBounds(Vector3 worldPosition)
        {
            Vector3 terrainPosition = targetTerrain.GetPosition();
            Vector3 terrainSize = targetTerrain.terrainData.size;
            const float edgeInset = 0.1f;
            worldPosition.x = Mathf.Clamp(
                worldPosition.x,
                terrainPosition.x + edgeInset,
                terrainPosition.x + terrainSize.x - edgeInset);
            worldPosition.z = Mathf.Clamp(
                worldPosition.z,
                terrainPosition.z + edgeInset,
                terrainPosition.z + terrainSize.z - edgeInset);
            return worldPosition;
        }

        private float GetTerrainSlope(Vector3 worldPosition)
        {
            Vector3 terrainPosition = targetTerrain.GetPosition();
            Vector3 terrainSize = targetTerrain.terrainData.size;
            float normalizedX = Mathf.InverseLerp(terrainPosition.x, terrainPosition.x + terrainSize.x, worldPosition.x);
            float normalizedZ = Mathf.InverseLerp(terrainPosition.z, terrainPosition.z + terrainSize.z, worldPosition.z);
            Vector3 normal = targetTerrain.terrainData.GetInterpolatedNormal(normalizedX, normalizedZ);
            return Vector3.Angle(normal, Vector3.up);
        }

        private Vector2 NextInsideUnitCircle()
        {
            float angle = (float)(random.NextDouble() * Mathf.PI * 2f);
            float radius = Mathf.Sqrt((float)random.NextDouble());
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        }

        private void InitializeAnimator()
        {
            canControlCannon = enemyAnimator != null && HasBoolParameter(enemyAnimator, CannonParameter);
            if (!canControlCannon)
            {
                Debug.LogWarning("Enemy Animator has no bool parameter named 'cannon'; behavior continues without animation control.", this);
                return;
            }

            enemyAnimator.SetBool(CannonParameter, false);
        }

        private void SetCannon(bool enabled)
        {
            if (canControlCannon)
                enemyAnimator.SetBool(CannonParameter, enabled);
        }

        private void ResolveReferences()
        {
            if (player == null)
            {
                MechFirstPersonController playerController = FindFirstObjectByType<MechFirstPersonController>();
                player = playerController != null ? playerController.transform : null;
                playerMovement = playerController;
            }

            if (playerMovement == null && player != null)
                playerMovement = player.GetComponent<MechFirstPersonController>();

            if (targetTerrain == null)
            {
                targetTerrain = Terrain.activeTerrain;
                if (targetTerrain == null)
                    targetTerrain = FindFirstObjectByType<Terrain>();
            }

            if (enemyAnimator == null)
                enemyAnimator = GetComponentInChildren<Animator>(true);
            if (soundPropagation == null)
                soundPropagation = FindFirstObjectByType<KartikarisSoundPropagation>();
        }

        private void EnsureRandom()
        {
            if (random == null)
                random = new System.Random(Environment.TickCount ^ GetInstanceID());
        }

        private static float HorizontalDistance(Vector3 first, Vector3 second)
        {
            return Vector2.Distance(
                new Vector2(first.x, first.z),
                new Vector2(second.x, second.z));
        }

        private static bool HasBoolParameter(Animator animator, int parameterHash)
        {
            AnimatorControllerParameter[] parameters = animator.parameters;
            for (int index = 0; index < parameters.Length; index++)
            {
                AnimatorControllerParameter parameter = parameters[index];
                if (parameter.nameHash == parameterHash && parameter.type == AnimatorControllerParameterType.Bool)
                    return true;
            }

            return false;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.25f, 0.1f, 0.9f);
            Gizmos.DrawWireSphere(currentNotionalFireTarget, 2f);
            Gizmos.DrawLine(transform.position, currentNotionalFireTarget);

            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.9f);
            Gizmos.DrawWireSphere(currentDestination, arrivalTolerance);
            Gizmos.DrawLine(transform.position, currentDestination);
        }
    }
}
