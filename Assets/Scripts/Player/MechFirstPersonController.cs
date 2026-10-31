using Kartikaris.Combat;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

namespace Kartikaris.Player
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class MechFirstPersonController : MonoBehaviour
    {
        private static readonly int CannonParameter = Animator.StringToHash("cannon");

        public enum OperatingState
        {
            Travel,
            TransitioningToFire,
            Fire,
            TransitioningToTravel
        }

        [Header("Cockpit View")]
        [Tooltip("Local yaw pivot for the pilot's head. It must not be parented to the physical Player.")]
        [SerializeField] private Transform cockpitLookYaw;
        [Tooltip("Local pitch pivot beneath Cockpit Look Yaw. CockpitCamera should be its child.")]
        [FormerlySerializedAs("cameraPivot")]
        [SerializeField] private Transform cockpitLookPitch;
        [SerializeField, Min(0f)] private float mouseSensitivity = 0.12f;
        [SerializeField] private float minimumYaw = -130f;
        [SerializeField] private float maximumYaw = 130f;
        [SerializeField] private float minimumPitch = -70f;
        [SerializeField] private float maximumPitch = 70f;

        [Header("Tracked Movement")]
        [FormerlySerializedAs("movementSpeed")]
        [SerializeField, Min(0f)] private float forwardSpeed = 6f;
        [SerializeField, Min(0f)] private float reverseSpeed = 5f;
        [SerializeField, Min(0f)] private float acceleration = 12f;
        [SerializeField, Min(0f)] private float deceleration = 16f;
        [FormerlySerializedAs("turnSpeedDegreesPerSecond")]
        [SerializeField, Min(0f)] private float pivotTurnSpeedDegreesPerSecond = 45f;
        [SerializeField, Min(0f)] private float movingTurnSpeedDegreesPerSecond = 18f;
        [SerializeField, Min(0f)] private float stationarySpeedThreshold = 0.1f;
        [SerializeField, Range(0f, 1f)] private float movingTurnSpeedMultiplier = 0.8f;
        [SerializeField] private float gravity = -25f;
        [SerializeField] private float groundedVerticalVelocity = -2f;

        [Header("Operating Mode")]
        [SerializeField] private KartikarisCannonWeapon cannonWeapon;
        [Tooltip("Explicit Player mech Animator. No hierarchy search is performed.")]
        [SerializeField] private Animator playerAnimator;
        [SerializeField, Min(0.01f)] private float modeTransitionDuration = 2f;
        [SerializeField, Min(0.01f)] private float normalAimStepDegrees = 1f;
        [SerializeField, Min(0.01f)] private float precisionAimStepDegrees = 0.1f;
        [SerializeField, Min(0f)] private float aimRepeatDelay = 0.35f;
        [SerializeField, Min(0.01f)] private float aimRepeatInterval = 0.1f;

        [Header("Post-Fire Commitment")]
        [SerializeField, Min(0f)] private float postFireVulnerabilityDuration = 5f;

        [Header("Tracked Drive Runtime")]
        [SerializeField, Range(-1f, 1f)] private float leftTrackCommand;
        [SerializeField, Range(-1f, 1f)] private float rightTrackCommand;
        [SerializeField] private bool isPivotTurning;

        [Header("Operating Mode Runtime")]
        [SerializeField] private OperatingState currentOperatingState = OperatingState.Travel;
        [SerializeField] private float transitionTimeRemaining;
        [SerializeField] private bool postFireVulnerabilityActive;
        [SerializeField] private float postFireVulnerabilityRemaining;

        private CharacterController characterController;
        private MechTreeCrusher treeCrusher;
        private InputAction moveAction;
        private InputAction lookAction;
        private InputAction releaseCursorAction;
        private InputAction captureCursorAction;
        private InputAction toggleOperatingModeAction;
        private InputAction fireAction;
        private float longitudinalSpeed;
        private float verticalVelocity;
        private float yaw;
        private float pitch;
        private int heldHeadingDirection;
        private int heldElevationDirection;
        private float nextHeadingRepeatTime;
        private float nextElevationRepeatTime;
        private bool suppressFireUntilMouseRelease;
        private bool canControlCannonAnimation;

        public Transform CockpitLookYaw => cockpitLookYaw;
        public Transform CockpitLookPitch => cockpitLookPitch;
        public float ForwardSpeed => forwardSpeed;
        public float ReverseSpeed => reverseSpeed;
        public float Acceleration => acceleration;
        public float Deceleration => deceleration;
        public float TurnSpeedDegreesPerSecond => pivotTurnSpeedDegreesPerSecond;
        public float PivotTurnSpeedDegreesPerSecond => pivotTurnSpeedDegreesPerSecond;
        public float MovingTurnSpeedDegreesPerSecond => movingTurnSpeedDegreesPerSecond;
        public float StationarySpeedThreshold => stationarySpeedThreshold;
        public float MovingTurnSpeedMultiplier => movingTurnSpeedMultiplier;
        public float Gravity => gravity;
        public float GroundedVerticalVelocity => groundedVerticalVelocity;
        public float MinimumCockpitYaw => minimumYaw;
        public float MaximumCockpitYaw => maximumYaw;
        public float MinimumCockpitPitch => minimumPitch;
        public float MaximumCockpitPitch => maximumPitch;
        public float CurrentCockpitYaw => yaw;
        public float CurrentCockpitPitch => pitch;
        public float CurrentLongitudinalSpeed => longitudinalSpeed;
        public float LeftTrackCommand => leftTrackCommand;
        public float RightTrackCommand => rightTrackCommand;
        public bool IsPivotTurning => isPivotTurning;
        public MechTreeCrusher TreeCrusher => treeCrusher;
        public KartikarisCannonWeapon CannonWeapon => cannonWeapon;
        public Animator PlayerAnimator => playerAnimator;
        public OperatingState CurrentOperatingState => currentOperatingState;
        public float ModeTransitionDuration => modeTransitionDuration;
        public float TransitionTimeRemaining => transitionTimeRemaining;
        public float OperatingModeProgress01
        {
            get
            {
                float duration = Mathf.Max(0.0001f, modeTransitionDuration);
                switch (currentOperatingState)
                {
                    case OperatingState.TransitioningToFire:
                        return 1f - Mathf.Clamp01(transitionTimeRemaining / duration);
                    case OperatingState.Fire:
                        return 1f;
                    case OperatingState.TransitioningToTravel:
                        return Mathf.Clamp01(transitionTimeRemaining / duration);
                    default:
                        return 0f;
                }
            }
        }
        public bool IsModeTransitioning =>
            currentOperatingState == OperatingState.TransitioningToFire ||
            currentOperatingState == OperatingState.TransitioningToTravel;
        public bool IsTravelMode => currentOperatingState == OperatingState.Travel;
        public bool IsFireMode => currentOperatingState == OperatingState.Fire;
        public float NormalAimStepDegrees => normalAimStepDegrees;
        public float PrecisionAimStepDegrees => precisionAimStepDegrees;
        public float PostFireVulnerabilityDuration => postFireVulnerabilityDuration;
        public bool IsPostFireVulnerabilityActive => postFireVulnerabilityActive;
        public float PostFireVulnerabilityRemaining => postFireVulnerabilityRemaining;
        public bool IsCannonAnimationRequested =>
            currentOperatingState == OperatingState.TransitioningToFire ||
            currentOperatingState == OperatingState.Fire;

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
            treeCrusher = GetComponent<MechTreeCrusher>();

            moveAction = new InputAction("Mech Move", InputActionType.Value);
            // Digital mode keeps throttle and turning independent when keys are
            // combined, so W+D still gives full forward and full right-turn input.
            moveAction.AddCompositeBinding("2DVector(mode=1)")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");

            lookAction = new InputAction("Mech Look", InputActionType.Value, "<Mouse>/delta");
            releaseCursorAction = new InputAction("Release Cursor", InputActionType.Button, "<Keyboard>/escape");
            captureCursorAction = new InputAction("Capture Cursor", InputActionType.Button, "<Mouse>/leftButton");
            toggleOperatingModeAction = new InputAction(
                "Toggle Operating Mode",
                InputActionType.Button,
                "<Keyboard>/t");
            fireAction = new InputAction("Fire Cannon", InputActionType.Button, "<Mouse>/leftButton");

            InitializeLookAngles();
            InitializeCannonAnimator();
        }

        private void OnEnable()
        {
            moveAction.Enable();
            lookAction.Enable();
            releaseCursorAction.performed += OnReleaseCursor;
            captureCursorAction.performed += OnCaptureCursor;
            releaseCursorAction.Enable();
            captureCursorAction.Enable();
            toggleOperatingModeAction.Enable();
            fireAction.Enable();
            SynchronizeCannonAnimator();
        }

        private void Start()
        {
            if (currentOperatingState == OperatingState.Travel)
                cannonWeapon?.CompleteTravelReset();

            CaptureCursor();
        }

        private void Update()
        {
            UpdateLook();
            AdvancePostFireVulnerability(Time.deltaTime);
            UpdateOperatingModeToggle();
            UpdateOperatingMode(Time.deltaTime);
            SynchronizeCannonAnimator();
        }

        private void LateUpdate()
        {
            cannonWeapon?.ApplyProceduralPoseAfterAnimation();
            UpdateFiring();
        }

        private void OnDisable()
        {
            releaseCursorAction.performed -= OnReleaseCursor;
            captureCursorAction.performed -= OnCaptureCursor;
            moveAction.Disable();
            lookAction.Disable();
            releaseCursorAction.Disable();
            captureCursorAction.Disable();
            toggleOperatingModeAction.Disable();
            fireAction.Disable();
        }

        private void OnDestroy()
        {
            moveAction?.Dispose();
            lookAction?.Dispose();
            releaseCursorAction?.Dispose();
            captureCursorAction?.Dispose();
            toggleOperatingModeAction?.Dispose();
            fireAction?.Dispose();
        }

        private void OnValidate()
        {
            maximumYaw = Mathf.Clamp(maximumYaw, -180f, 180f);
            minimumYaw = Mathf.Clamp(minimumYaw, -180f, maximumYaw);
            maximumPitch = Mathf.Clamp(maximumPitch, -89f, 89f);
            minimumPitch = Mathf.Clamp(minimumPitch, -89f, maximumPitch);
            forwardSpeed = Mathf.Max(0f, forwardSpeed);
            reverseSpeed = Mathf.Max(0f, reverseSpeed);
            acceleration = Mathf.Max(0f, acceleration);
            deceleration = Mathf.Max(0f, deceleration);
            pivotTurnSpeedDegreesPerSecond = Mathf.Max(0f, pivotTurnSpeedDegreesPerSecond);
            movingTurnSpeedDegreesPerSecond = Mathf.Max(0f, movingTurnSpeedDegreesPerSecond);
            stationarySpeedThreshold = Mathf.Max(0f, stationarySpeedThreshold);
            movingTurnSpeedMultiplier = Mathf.Clamp01(movingTurnSpeedMultiplier);
            gravity = Mathf.Min(gravity, 0f);
            groundedVerticalVelocity = Mathf.Min(groundedVerticalVelocity, 0f);
            modeTransitionDuration = Mathf.Max(0.01f, modeTransitionDuration);
            normalAimStepDegrees = Mathf.Max(0.01f, normalAimStepDegrees);
            precisionAimStepDegrees = Mathf.Max(0.01f, precisionAimStepDegrees);
            aimRepeatDelay = Mathf.Max(0f, aimRepeatDelay);
            aimRepeatInterval = Mathf.Max(0.01f, aimRepeatInterval);
            postFireVulnerabilityDuration = Mathf.Max(0f, postFireVulnerabilityDuration);
            postFireVulnerabilityRemaining = Mathf.Max(0f, postFireVulnerabilityRemaining);
            postFireVulnerabilityActive = postFireVulnerabilityRemaining > 0f;
        }

        private void UpdateLook()
        {
            if (Cursor.lockState != CursorLockMode.Locked ||
                cockpitLookYaw == null || cockpitLookPitch == null)
            {
                return;
            }

            ApplyCockpitLookDelta(lookAction.ReadValue<Vector2>());
        }

        private void UpdateOperatingModeToggle()
        {
            if (!toggleOperatingModeAction.WasPressedThisFrame())
                return;

            if (currentOperatingState == OperatingState.Travel)
                BeginTransitionToFire();
            else if (currentOperatingState == OperatingState.Fire)
                BeginTransitionToTravel();
        }

        private void UpdateOperatingMode(float deltaTime)
        {
            float safeDeltaTime = Mathf.Max(0f, deltaTime);
            switch (currentOperatingState)
            {
                case OperatingState.Travel:
                    ResetAimRepeatState();
                    ApplyTankControls(moveAction.ReadValue<Vector2>(), safeDeltaTime);
                    break;

                case OperatingState.TransitioningToFire:
                    ResetAimRepeatState();
                    ApplyTankControls(Vector2.zero, safeDeltaTime);
                    AdvanceModeTransition(safeDeltaTime, OperatingState.Fire);
                    break;

                case OperatingState.Fire:
                    ApplyTankControls(Vector2.zero, safeDeltaTime);
                    UpdateWeaponAiming(moveAction.ReadValue<Vector2>());
                    break;

                case OperatingState.TransitioningToTravel:
                    ResetAimRepeatState();
                    ApplyTankControls(Vector2.zero, safeDeltaTime);
                    AdvanceModeTransition(safeDeltaTime, OperatingState.Travel);
                    break;
            }
        }

        private void AdvanceModeTransition(float deltaTime, OperatingState completedState)
        {
            transitionTimeRemaining = Mathf.Max(0f, transitionTimeRemaining - deltaTime);
            if (transitionTimeRemaining > 0f)
                return;

            currentOperatingState = completedState;
            if (completedState == OperatingState.Fire)
            {
                longitudinalSpeed = 0f;
                leftTrackCommand = 0f;
                rightTrackCommand = 0f;
                isPivotTurning = false;
                cannonWeapon?.EnterFireMode();
            }
            else if (completedState == OperatingState.Travel)
            {
                cannonWeapon?.CompleteTravelReset();
            }
        }

        public void BeginTransitionToFire()
        {
            if (currentOperatingState != OperatingState.Travel)
                return;

            currentOperatingState = OperatingState.TransitioningToFire;
            transitionTimeRemaining = modeTransitionDuration;
            ResetAimRepeatState();
            cannonWeapon?.PrepareForFireTransition();
            SetCannonAnimation(true);
        }

        public void BeginTransitionToTravel()
        {
            if (currentOperatingState != OperatingState.Fire || postFireVulnerabilityActive)
                return;

            currentOperatingState = OperatingState.TransitioningToTravel;
            transitionTimeRemaining = modeTransitionDuration;
            ResetAimRepeatState();
            cannonWeapon?.BeginTravelReset(modeTransitionDuration);
            SetCannonAnimation(false);
        }

        private void UpdateWeaponAiming(Vector2 input)
        {
            if (cannonWeapon == null)
            {
                ResetAimRepeatState();
                return;
            }

            int headingDirection = DirectionFromInput(input.x);
            int elevationDirection = DirectionFromInput(input.y);
            bool precisionHeld = Keyboard.current != null && Keyboard.current.leftAltKey.isPressed;
            float stepDegrees = precisionHeld ? precisionAimStepDegrees : normalAimStepDegrees;
            float currentTime = Time.time;

            ProcessAimRepeat(
                headingDirection,
                ref heldHeadingDirection,
                ref nextHeadingRepeatTime,
                currentTime,
                stepDegrees,
                cannonWeapon.StepHeading);
            ProcessAimRepeat(
                elevationDirection,
                ref heldElevationDirection,
                ref nextElevationRepeatTime,
                currentTime,
                stepDegrees,
                cannonWeapon.StepElevation);
        }

        private void ProcessAimRepeat(
            int direction,
            ref int heldDirection,
            ref float nextRepeatTime,
            float currentTime,
            float stepDegrees,
            System.Action<float> applyStep)
        {
            if (direction == 0)
            {
                heldDirection = 0;
                nextRepeatTime = 0f;
                return;
            }

            if (direction != heldDirection)
            {
                heldDirection = direction;
                applyStep(direction * stepDegrees);
                nextRepeatTime = currentTime + aimRepeatDelay;
                return;
            }

            const int maximumCatchUpStepsPerFrame = 16;
            int catchUpSteps = 0;
            while (currentTime >= nextRepeatTime && catchUpSteps < maximumCatchUpStepsPerFrame)
            {
                applyStep(direction * stepDegrees);
                nextRepeatTime += aimRepeatInterval;
                catchUpSteps++;
            }
        }

        private void UpdateFiring()
        {
            if (!fireAction.IsPressed())
                suppressFireUntilMouseRelease = false;

            if (suppressFireUntilMouseRelease ||
                !fireAction.WasPressedThisFrame() ||
                Cursor.lockState != CursorLockMode.Locked ||
                currentOperatingState != OperatingState.Fire ||
                postFireVulnerabilityActive ||
                cannonWeapon == null)
            {
                return;
            }

            if (cannonWeapon.TryFire())
                StartPostFireVulnerability();
        }

        private void StartPostFireVulnerability()
        {
            postFireVulnerabilityRemaining = Mathf.Max(0f, postFireVulnerabilityDuration);
            postFireVulnerabilityActive = postFireVulnerabilityRemaining > 0f;
        }

        private void AdvancePostFireVulnerability(float deltaTime)
        {
            if (!postFireVulnerabilityActive)
                return;

            postFireVulnerabilityRemaining = Mathf.Max(
                0f,
                postFireVulnerabilityRemaining - Mathf.Max(0f, deltaTime));
            postFireVulnerabilityActive = postFireVulnerabilityRemaining > 0f;
        }

        private void ResetAimRepeatState()
        {
            heldHeadingDirection = 0;
            heldElevationDirection = 0;
            nextHeadingRepeatTime = 0f;
            nextElevationRepeatTime = 0f;
        }

        private static int DirectionFromInput(float value)
        {
            if (value > 0.5f)
                return 1;
            if (value < -0.5f)
                return -1;
            return 0;
        }

        private void InitializeCannonAnimator()
        {
            canControlCannonAnimation =
                playerAnimator != null &&
                HasBoolParameter(playerAnimator, CannonParameter);

            if (playerAnimator == null)
            {
                Debug.LogWarning(
                    "Player cannon animation requires an explicitly assigned Animator.",
                    this);
            }
            else if (!canControlCannonAnimation)
            {
                Debug.LogWarning(
                    "Assigned Player Animator has no bool parameter named 'cannon'.",
                    this);
            }

            SynchronizeCannonAnimator();
        }

        private void SynchronizeCannonAnimator()
        {
            SetCannonAnimation(IsCannonAnimationRequested);
        }

        private void SetCannonAnimation(bool enabled)
        {
            if (canControlCannonAnimation)
                playerAnimator.SetBool(CannonParameter, enabled);
        }

        private static bool HasBoolParameter(Animator animator, int parameterHash)
        {
            AnimatorControllerParameter[] parameters = animator.parameters;
            for (int index = 0; index < parameters.Length; index++)
            {
                AnimatorControllerParameter parameter = parameters[index];
                if (parameter.nameHash == parameterHash &&
                    parameter.type == AnimatorControllerParameterType.Bool)
                {
                    return true;
                }
            }

            return false;
        }

        private void ApplyCockpitLookDelta(Vector2 rawLookDelta)
        {
            Vector2 lookDelta = rawLookDelta * mouseSensitivity;
            yaw = Mathf.Clamp(yaw + lookDelta.x, minimumYaw, maximumYaw);
            pitch = Mathf.Clamp(pitch - lookDelta.y, minimumPitch, maximumPitch);

            cockpitLookYaw.localRotation = Quaternion.Euler(0f, yaw, 0f);
            cockpitLookPitch.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        private void ApplyTankControls(Vector2 input, float deltaTime)
        {
            float safeDeltaTime = Mathf.Max(0f, deltaTime);
            float throttleInput = Mathf.Clamp(input.y, -1f, 1f);
            float turnInput = Mathf.Clamp(input.x, -1f, 1f);

            TrackedDriveModel.Output drive = TrackedDriveModel.Evaluate(
                throttleInput,
                turnInput,
                longitudinalSpeed,
                forwardSpeed,
                reverseSpeed,
                stationarySpeedThreshold,
                pivotTurnSpeedDegreesPerSecond,
                movingTurnSpeedDegreesPerSecond,
                movingTurnSpeedMultiplier);
            leftTrackCommand = drive.LeftTrackCommand;
            rightTrackCommand = drive.RightTrackCommand;
            isPivotTurning = drive.IsPivotTurn;

            transform.Rotate(0f, drive.YawRateDegreesPerSecond * safeDeltaTime, 0f, Space.Self);

            float targetSpeed = throttleInput >= 0f
                ? throttleInput * forwardSpeed
                : throttleInput * reverseSpeed;
            targetSpeed *= drive.LongitudinalSpeedMultiplier;
            float changeRate = Mathf.Abs(throttleInput) > Mathf.Epsilon
                ? acceleration
                : deceleration;
            longitudinalSpeed = Mathf.MoveTowards(
                longitudinalSpeed,
                targetSpeed,
                changeRate * safeDeltaTime);

            if (characterController.isGrounded && verticalVelocity < 0f)
            {
                verticalVelocity = groundedVerticalVelocity;
            }

            verticalVelocity += gravity * safeDeltaTime;
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

        public void ConfigureCockpitLookRig(Transform lookYaw, Transform lookPitch)
        {
            cockpitLookYaw = lookYaw;
            cockpitLookPitch = lookPitch;
            InitializeLookAngles();
        }

        private void OnReleaseCursor(InputAction.CallbackContext context)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void OnCaptureCursor(InputAction.CallbackContext context)
        {
            if (Cursor.lockState != CursorLockMode.Locked)
            {
                suppressFireUntilMouseRelease = true;
                CaptureCursor();
            }
        }

        private static void CaptureCursor()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private static float NormalizeAngle(float angle)
        {
            return angle > 180f ? angle - 360f : angle;
        }

        private void InitializeLookAngles()
        {
            if (cockpitLookYaw != null)
            {
                yaw = Mathf.Clamp(
                    NormalizeAngle(cockpitLookYaw.localEulerAngles.y),
                    minimumYaw,
                    maximumYaw);
            }

            if (cockpitLookPitch != null)
            {
                pitch = Mathf.Clamp(
                    NormalizeAngle(cockpitLookPitch.localEulerAngles.x),
                    minimumPitch,
                    maximumPitch);
            }
        }
    }
}
