using System;
using Kartikaris.Audio;
using Kartikaris.UI;
using Kartikaris.WorldInteraction;
using UnityEngine;

namespace Kartikaris.Combat
{
    /// <summary>
    /// Shared cannon articulation and firing API. Player input and Enemy AI are
    /// deliberately kept outside this component.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KartikarisCannonWeapon : MonoBehaviour
    {
        private const float VerticalElevationDegrees = 90f;

        public enum LocalRotationAxis
        {
            X,
            Y,
            Z
        }

        [Header("Explicit Articulation References")]
        [SerializeField] private Transform headingPivot;
        [SerializeField] private LocalRotationAxis headingAxis = LocalRotationAxis.Y;
        [SerializeField] private Transform elevationPivot;
        [SerializeField] private LocalRotationAxis elevationAxis = LocalRotationAxis.X;
        [SerializeField] private Transform firePoint;

        [Header("Elevation Limits")]
        [Tooltip("Lowest Player-facing physical muzzle elevation. Vertical is always fixed at 90 degrees.")]
        [SerializeField, Range(0f, VerticalElevationDegrees)] private float minimumElevationDegrees;
        [Tooltip("Player-facing vertical limit. Runtime code keeps this at 90 degrees.")]
        [SerializeField] private float maximumElevationDegrees = VerticalElevationDegrees;

        [Header("Aim Smoothing")]
        [Tooltip("Exponential visual interpolation strength. Higher values follow logical aim more quickly.")]
        [SerializeField, Min(0.01f)] private float aimLerpSharpness = 12f;

        [Header("Firing")]
        [SerializeField] private KartikarisArtilleryProjectile artilleryShellPrefab;
        [Tooltip("Optional fixed pre-match shell selection. Locked when this weapon awakens.")]
        [SerializeField] private KartikarisShellLoadout shellLoadout;
        [SerializeField, Min(0f)] private float muzzleVelocity = 300f;
        [SerializeField, Min(0.01f)] private float firingCooldown = 5f;
        [SerializeField] private KartikarisCombatAffiliation sourceAffiliation;
        [SerializeField] private KartikarisGuidanceAntenna guidanceAntenna;
        [SerializeField] private KartikarisGuidedMissileController guidedMissileController;

        [Header("Shot VFX")]
        [SerializeField] private GameObject muzzleFireVfxPrefab;
        [Tooltip("Local-space position offset from the fire point for the muzzle VFX only.")]
        [SerializeField] private Vector3 muzzleVfxLocalPositionOffset;
        [Tooltip("Zero lets a self-managed muzzle VFX destroy itself.")]
        [SerializeField, Min(0f)] private float muzzleVfxLifetime;
        [SerializeField] private GameObject lingeringSmokePrefab;
        [Tooltip("Zero lets a self-managed smoke prefab destroy itself.")]
        [SerializeField, Min(0f)] private float lingeringSmokeLifetime = 30f;

        [Header("Existing System Integration")]
        [Tooltip("Optional. Receives measured muzzle elevation relative to the Player/reference XZ plane.")]
        [SerializeField] private KartikarisHeadingElevationHUD headingElevationHud;
        [Tooltip("Optional HUD plane reference. Leave empty when this component is on the Player root.")]
        [SerializeField] private Transform elevationHudReferenceFrame;
        [Tooltip("Optional. Assign the existing finite-speed sound propagation component.")]
        [SerializeField] private KartikarisSoundPropagation soundPropagation;
        [Tooltip("Explicit runtime used by launched projectiles for spatial tree blast queries.")]
        [SerializeField] private KartikarisTreeInteractionRuntime treeInteraction;
        [Tooltip("Future-facing source identifier. No faction rules are applied yet.")]
        [SerializeField] private int sourceTeamId = -1;

        [Header("Runtime Diagnostics")]
        [SerializeField] private float currentHeadingDegrees;
        [SerializeField] private float currentElevationDegrees;
        [SerializeField] private float visualHeadingDegrees;
        [SerializeField] private float visualElevationDegrees;
        [SerializeField] private float measuredMuzzleElevationDegrees;
        [SerializeField] private float nextAllowedFireTime;
        [SerializeField] private bool travelResetActive;
        [SerializeField] private bool proceduralElevationSuppressed = true;
        [SerializeField] private float travelResetElapsed;
        [SerializeField] private float travelResetDuration;
        [SerializeField] private float baseBallisticVelocity;
        [SerializeField] private float ballisticTravelSpeedMultiplier = 1f;
        [SerializeField] private float effectiveLaunchSpeed;
        [SerializeField] private float effectiveGravityMagnitude;

        private const float PoseComparisonToleranceDegrees = 0.0001f;

        private Transform trackedHeadingPivot;
        private Transform trackedElevationPivot;
        private Quaternion headingAnimationBaseLocalRotation;
        private Quaternion elevationAnimationBaseLocalRotation;
        private Quaternion lastAppliedHeadingLocalRotation;
        private Quaternion lastAppliedElevationLocalRotation;
        private bool headingPoseInitialized;
        private bool elevationPoseInitialized;
        private bool visualAimInitialized;
        private int lastAimSmoothingFrame = -1;
        private float travelResetStartHeadingDegrees;
        private float travelResetStartElevationDegrees;

        public Transform HeadingPivot => headingPivot;
        public Transform ElevationPivot => elevationPivot;
        public Transform FirePoint => firePoint;
        public Transform ElevationHudReferenceFrame => elevationHudReferenceFrame;
        public LocalRotationAxis HeadingAxis => headingAxis;
        public LocalRotationAxis ElevationAxis => elevationAxis;
        public float CurrentHeadingDegrees => currentHeadingDegrees;
        public float CurrentElevationDegrees => currentElevationDegrees;
        public float VisualHeadingDegrees => visualHeadingDegrees;
        public float VisualElevationDegrees => visualElevationDegrees;
        public float MinimumElevationDegrees => minimumElevationDegrees;
        public float MaximumElevationDegrees => maximumElevationDegrees;
        public float ProceduralElevationOffsetDegrees => proceduralElevationSuppressed
            ? 0f
            : ElevationOffsetFromPhysicalDegrees(visualElevationDegrees);
        public float MeasuredMuzzleElevationDegrees => measuredMuzzleElevationDegrees;
        public float AimLerpSharpness => aimLerpSharpness;
        /// <summary>Weapon-configured base velocity before any shell override or time scaling.</summary>
        public float MuzzleVelocity => muzzleVelocity;
        /// <summary>Selected shell's base ballistic velocity before time scaling.</summary>
        public float EffectiveMuzzleVelocity
        {
            get
            {
                float shellVelocity = CurrentShellDefinition != null &&
                    CurrentShellDefinition.MuzzleVelocityOverride > 0f
                        ? CurrentShellDefinition.MuzzleVelocityOverride
                        : muzzleVelocity;
                return shellLoadout != null
                    ? shellLoadout.ApplyChargeMuzzleVelocity(shellVelocity)
                    : shellVelocity;
            }
        }
        public float BallisticTravelSpeedMultiplier => CurrentShellDefinition != null
            ? CurrentShellDefinition.BallisticTravelSpeedMultiplier
            : 1f;
        public float EffectiveLaunchSpeed => EffectiveMuzzleVelocity * BallisticTravelSpeedMultiplier;
        public float EffectiveGravityMagnitude =>
            Physics.gravity.magnitude * BallisticTravelSpeedMultiplier * BallisticTravelSpeedMultiplier;
        public KartikarisShellDefinition CurrentShellDefinition => shellLoadout != null
            ? shellLoadout.SelectedShell
            : null;
        public KartikarisShellType CurrentShellType => CurrentShellDefinition != null
            ? CurrentShellDefinition.ShellType
            : KartikarisShellType.StandardHE;
        public float FiringCooldown => firingCooldown;
        public bool IsTravelResetActive => travelResetActive;
        public float TravelResetProgress => travelResetDuration <= 0f
            ? (travelResetActive ? 1f : 0f)
            : Mathf.Clamp01(travelResetElapsed / travelResetDuration);
        public bool IsProceduralElevationSuppressed => proceduralElevationSuppressed;
        public float CooldownRemaining => Mathf.Max(0f, nextAllowedFireTime - Time.time);
        public bool CanFire => Time.time >= nextAllowedFireTime;

        public event Action<KartikarisArtilleryProjectile> Fired;

        private void Awake()
        {
            if (shellLoadout == null)
                shellLoadout = GetComponent<KartikarisShellLoadout>();
            shellLoadout?.LockSelection();
            if (sourceAffiliation == null)
                sourceAffiliation = GetComponentInParent<KartikarisCombatAffiliation>();
            SanitizeElevationLimits();
            proceduralElevationSuppressed = true;
            SetAimDegrees(currentHeadingDegrees, currentElevationDegrees);
            currentElevationDegrees = VerticalElevationDegrees;
            visualHeadingDegrees = currentHeadingDegrees;
            visualElevationDegrees = VerticalElevationDegrees;
            visualAimInitialized = true;
            UpdateBallisticDiagnostics();
            ApplyProceduralPoseAfterAnimation();
        }

        private void LateUpdate()
        {
            UpdateBallisticDiagnostics();
            ApplyProceduralPoseAfterAnimation();
        }

        private void OnDisable()
        {
            headingElevationHud?.ClearRuntimeElevationOverride();
        }

        private void OnValidate()
        {
            SanitizeElevationLimits();

            currentElevationDegrees = Mathf.Clamp(
                currentElevationDegrees,
                minimumElevationDegrees,
                maximumElevationDegrees);
            aimLerpSharpness = Mathf.Max(0.01f, aimLerpSharpness);
            muzzleVelocity = Mathf.Max(0f, muzzleVelocity);
            firingCooldown = Mathf.Max(0.01f, firingCooldown);
            muzzleVfxLifetime = Mathf.Max(0f, muzzleVfxLifetime);
            lingeringSmokeLifetime = Mathf.Max(0f, lingeringSmokeLifetime);
            UpdateBallisticDiagnostics();
        }

        private void UpdateBallisticDiagnostics()
        {
            baseBallisticVelocity = EffectiveMuzzleVelocity;
            ballisticTravelSpeedMultiplier = BallisticTravelSpeedMultiplier;
            effectiveLaunchSpeed = EffectiveLaunchSpeed;
            effectiveGravityMagnitude = EffectiveGravityMagnitude;
        }

        public void StepHeading(float deltaDegrees)
        {
            SetHeadingDegrees(currentHeadingDegrees + deltaDegrees);
        }

        public void StepElevation(float deltaDegrees)
        {
            SetElevationDegrees(currentElevationDegrees + deltaDegrees);
        }

        public void SetHeadingDegrees(float degrees)
        {
            currentHeadingDegrees = degrees;
        }

        public void SetElevationDegrees(float degrees)
        {
            currentElevationDegrees = Mathf.Clamp(
                degrees,
                minimumElevationDegrees,
                maximumElevationDegrees);
        }

        public void SetAimDegrees(float headingDegrees, float elevationDegrees)
        {
            SetHeadingDegrees(headingDegrees);
            SetElevationDegrees(elevationDegrees);
        }

        public void ResetHeadingToForward()
        {
            SetHeadingDegrees(0f);
        }

        public void PrepareForFireTransition()
        {
            travelResetActive = false;
            currentElevationDegrees = VerticalElevationDegrees;
            visualElevationDegrees = VerticalElevationDegrees;
            proceduralElevationSuppressed = true;
        }

        /// <summary>
        /// Enables Player elevation only after the authored deployment has
        /// completed. Vertical is the neutral pose, so the initial offset is zero.
        /// </summary>
        public void EnterFireMode()
        {
            travelResetActive = false;
            currentElevationDegrees = VerticalElevationDegrees;
            visualElevationDegrees = VerticalElevationDegrees;
            proceduralElevationSuppressed = false;
        }

        public void BeginTravelReset(float duration)
        {
            travelResetStartHeadingDegrees = visualHeadingDegrees;
            travelResetStartElevationDegrees = visualElevationDegrees;
            travelResetDuration = Mathf.Max(0.01f, duration);
            travelResetElapsed = 0f;
            travelResetActive = true;
            proceduralElevationSuppressed = false;
            SetHeadingDegrees(0f);
            currentElevationDegrees = VerticalElevationDegrees;
        }

        public void CompleteTravelReset()
        {
            RestoreElevationAnimatorBasePose();
            travelResetActive = false;
            travelResetElapsed = travelResetDuration;
            currentHeadingDegrees = 0f;
            visualHeadingDegrees = 0f;
            currentElevationDegrees = VerticalElevationDegrees;
            visualElevationDegrees = VerticalElevationDegrees;
            proceduralElevationSuppressed = true;
        }

        /// <summary>
        /// Spawns and launches one shell if the weapon is configured and its
        /// cooldown has elapsed. Operating-mode permission remains the caller's
        /// responsibility so this API is equally usable by Player and Enemy code.
        /// </summary>
        public bool TryFire()
        {
            return TryFire(out _);
        }

        public bool TryFire(out KartikarisArtilleryProjectile projectile)
        {
            projectile = null;
            // Charge rejection precedes every side effect, including cooldown
            // checks, so each zero-charge trigger attempt emits one warning hook.
            if (shellLoadout != null && !shellLoadout.TryAuthorizeFire())
                return false;
            if (!CanFire)
                return false;

            KartikarisShellDefinition selectedShell = CurrentShellDefinition;
            KartikarisArtilleryProjectile selectedPrefab = selectedShell != null &&
                selectedShell.ProjectilePrefab != null
                    ? selectedShell.ProjectilePrefab
                    : artilleryShellPrefab;
            if (firePoint == null || selectedPrefab == null)
            {
                Debug.LogWarning(
                    "Cannon fire requires both an explicit firePoint and an artillery shell prefab.",
                    this);
                return false;
            }

            ApplyProceduralPoseAfterAnimation();

            Quaternion shellRotation = Quaternion.LookRotation(
                firePoint.forward,
                firePoint.up);
            projectile = Instantiate(
                selectedPrefab,
                firePoint.position,
                shellRotation);

            // Keep the configured muzzle velocity as the base ballistic value.
            // The projectile applies velocity x k and gravity x k^2 locally.
            Vector3 baseLaunchVelocity = firePoint.forward * EffectiveMuzzleVelocity;
            if (!projectile.Launch(
                    baseLaunchVelocity,
                    transform.root,
                    sourceTeamId,
                    treeInteraction,
                    selectedShell,
                    sourceAffiliation,
                    guidanceAntenna,
                    BallisticTravelSpeedMultiplier))
            {
                Destroy(projectile.gameObject);
                projectile = null;
                return false;
            }

            nextAllowedFireTime = Time.time + firingCooldown;
            guidedMissileController?.RegisterMissile(projectile);
            SpawnShotVfx();
            soundPropagation?.EmitCannonDryFire(firePoint.position, sourceAffiliation);
            Fired?.Invoke(projectile);
            return true;
        }

        /// <summary>
        /// Composes logical aim after Animator evaluation. It remembers the last
        /// procedural result so an unanimated pivot keeps a stable base pose,
        /// while an Animator-written pose becomes the new base each frame.
        /// Heading is applied first so a nested elevation pivot inherits it.
        /// </summary>
        public void ApplyProceduralPoseAfterAnimation()
        {
            UpdateSmoothedAim();
            ApplyPivotOffset(
                headingPivot,
                headingAxis,
                visualHeadingDegrees,
                ref trackedHeadingPivot,
                ref headingAnimationBaseLocalRotation,
                ref lastAppliedHeadingLocalRotation,
                ref headingPoseInitialized);
            if (proceduralElevationSuppressed)
            {
                ObserveAnimatorPose(
                    elevationPivot,
                    ref trackedElevationPivot,
                    ref elevationAnimationBaseLocalRotation,
                    ref lastAppliedElevationLocalRotation,
                    ref elevationPoseInitialized);
            }
            else
            {
                ApplyPivotOffset(
                    elevationPivot,
                    elevationAxis,
                    ElevationOffsetFromPhysicalDegrees(visualElevationDegrees),
                    ref trackedElevationPivot,
                    ref elevationAnimationBaseLocalRotation,
                    ref lastAppliedElevationLocalRotation,
                    ref elevationPoseInitialized);
            }
            UpdateHudMuzzleElevation();
        }

        private void SpawnShotVfx()
        {
            Quaternion muzzleVfxRotation = Quaternion.LookRotation(
                -firePoint.right,
                firePoint.up);
            SpawnDetachedVfx(
                muzzleFireVfxPrefab,
                firePoint.TransformPoint(muzzleVfxLocalPositionOffset),
                muzzleVfxRotation,
                muzzleVfxLifetime);
            SpawnDetachedVfx(
                lingeringSmokePrefab,
                firePoint.position,
                firePoint.rotation,
                lingeringSmokeLifetime);
        }

        private static void SpawnDetachedVfx(
            GameObject prefab,
            Vector3 position,
            Quaternion rotation,
            float lifetime)
        {
            if (prefab == null)
                return;

            GameObject instance = Instantiate(prefab, position, rotation);
            instance.transform.SetParent(null, true);
            if (lifetime > 0f)
                Destroy(instance, lifetime);
        }

        private void UpdateHudMuzzleElevation()
        {
            if (firePoint == null)
                return;

            Transform referenceFrame = elevationHudReferenceFrame != null
                ? elevationHudReferenceFrame
                : transform;
            float signedMuzzleElevation = Mathf.Asin(Mathf.Clamp(
                Vector3.Dot(firePoint.forward.normalized, referenceFrame.up.normalized),
                -1f,
                1f)) * Mathf.Rad2Deg;
            measuredMuzzleElevationDegrees = Mathf.Clamp(
                signedMuzzleElevation,
                0f,
                VerticalElevationDegrees);
            headingElevationHud?.SetRuntimeElevationDegrees(measuredMuzzleElevationDegrees);
        }

        private void UpdateSmoothedAim()
        {
            if (!visualAimInitialized)
            {
                visualHeadingDegrees = currentHeadingDegrees;
                visualElevationDegrees = currentElevationDegrees;
                visualAimInitialized = true;
            }

            if (lastAimSmoothingFrame == Time.frameCount)
                return;

            lastAimSmoothingFrame = Time.frameCount;
            if (travelResetActive)
            {
                travelResetElapsed = Mathf.Min(
                    travelResetDuration,
                    travelResetElapsed + Mathf.Max(0f, Time.deltaTime));
                float resetProgress = travelResetDuration <= 0f
                    ? 1f
                    : Mathf.Clamp01(travelResetElapsed / travelResetDuration);
                visualHeadingDegrees = Mathf.LerpAngle(
                    travelResetStartHeadingDegrees,
                    0f,
                    resetProgress);
                visualElevationDegrees = Mathf.Lerp(
                    travelResetStartElevationDegrees,
                    VerticalElevationDegrees,
                    resetProgress);

                if (resetProgress >= 1f)
                {
                    visualHeadingDegrees = 0f;
                    visualElevationDegrees = VerticalElevationDegrees;
                }
                return;
            }

            float blend = 1f - Mathf.Exp(-aimLerpSharpness * Mathf.Max(0f, Time.deltaTime));
            visualHeadingDegrees = Mathf.LerpAngle(
                visualHeadingDegrees,
                currentHeadingDegrees,
                blend);
            visualElevationDegrees = Mathf.Lerp(
                visualElevationDegrees,
                currentElevationDegrees,
                blend);

            if (Mathf.Abs(Mathf.DeltaAngle(visualHeadingDegrees, currentHeadingDegrees)) < 0.0001f)
                visualHeadingDegrees = currentHeadingDegrees;
            if (Mathf.Abs(visualElevationDegrees - currentElevationDegrees) < 0.0001f)
                visualElevationDegrees = currentElevationDegrees;
        }

        private static void ApplyPivotOffset(
            Transform pivot,
            LocalRotationAxis axis,
            float offsetDegrees,
            ref Transform trackedPivot,
            ref Quaternion animationBaseLocalRotation,
            ref Quaternion lastAppliedLocalRotation,
            ref bool poseInitialized)
        {
            if (pivot == null)
            {
                trackedPivot = null;
                poseInitialized = false;
                return;
            }

            if (trackedPivot != pivot)
            {
                trackedPivot = pivot;
                poseInitialized = false;
            }

            Quaternion observedLocalRotation = pivot.localRotation;
            if (!poseInitialized ||
                Quaternion.Angle(observedLocalRotation, lastAppliedLocalRotation) >
                PoseComparisonToleranceDegrees)
            {
                animationBaseLocalRotation = observedLocalRotation;
                poseInitialized = true;
            }

            Quaternion proceduralOffset = Quaternion.AngleAxis(offsetDegrees, AxisVector(axis));
            Quaternion finalLocalRotation = animationBaseLocalRotation * proceduralOffset;
            pivot.localRotation = finalLocalRotation;
            lastAppliedLocalRotation = finalLocalRotation;
        }

        private void RestoreElevationAnimatorBasePose()
        {
            ApplyPivotOffset(
                elevationPivot,
                elevationAxis,
                0f,
                ref trackedElevationPivot,
                ref elevationAnimationBaseLocalRotation,
                ref lastAppliedElevationLocalRotation,
                ref elevationPoseInitialized);
        }

        private static void ObserveAnimatorPose(
            Transform pivot,
            ref Transform trackedPivot,
            ref Quaternion animationBaseLocalRotation,
            ref Quaternion lastAppliedLocalRotation,
            ref bool poseInitialized)
        {
            if (pivot == null)
            {
                trackedPivot = null;
                poseInitialized = false;
                return;
            }

            trackedPivot = pivot;
            animationBaseLocalRotation = pivot.localRotation;
            lastAppliedLocalRotation = pivot.localRotation;
            poseInitialized = true;
        }

        private void SanitizeElevationLimits()
        {
            minimumElevationDegrees = Mathf.Clamp(
                minimumElevationDegrees,
                0f,
                VerticalElevationDegrees);
            maximumElevationDegrees = VerticalElevationDegrees;
        }

        private static float ElevationOffsetFromPhysicalDegrees(float physicalElevationDegrees)
        {
            return VerticalElevationDegrees - physicalElevationDegrees;
        }

        private static Vector3 AxisVector(LocalRotationAxis axis)
        {
            switch (axis)
            {
                case LocalRotationAxis.X:
                    return Vector3.right;
                case LocalRotationAxis.Y:
                    return Vector3.up;
                default:
                    return Vector3.forward;
            }
        }
    }
}
