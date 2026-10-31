using UnityEngine;

namespace Kartikaris.Combat
{
    public enum KartikarisShellType
    {
        StandardHE,
        Airburst,
        Homing,
        GuidedMissile
    }

    [CreateAssetMenu(menuName = "Kartikaris/Combat/Shell Definition", fileName = "ShellDefinition")]
    public sealed class KartikarisShellDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private KartikarisShellType shellType = KartikarisShellType.StandardHE;
        [SerializeField] private KartikarisArtilleryProjectile projectilePrefab;
        [Tooltip("Zero uses the cannon weapon's muzzle velocity.")]
        [SerializeField, Min(0f)] private float muzzleVelocityOverride;
        [Tooltip("Scales real-time ballistic traversal while preserving the base spatial arc. " +
            "Runtime velocity uses x k and gravity uses x k squared.")]
        [SerializeField, Min(0.01f)] private float ballisticTravelSpeedMultiplier = 1f;

        [Header("Radial / Standard HE")]
        [SerializeField, Min(0f)] private float maximumExplosionDamage = 1000f;
        [SerializeField, Min(0f)] private float fullDamageRadius = 30f;
        [SerializeField, Min(0f)] private float explosionRadius = 100f;
        [SerializeField, Min(0f)] private float treeEffectRadius = 30f;

        [Header("Airburst")]
        [SerializeField, Min(0f)] private float airburstHeight = 50f;
        [SerializeField, Min(0f)] private float airburstArmingMargin = 5f;
        [SerializeField, Min(0f)] private float airburstDamage = 200f;
        [SerializeField, Min(0f)] private float airburstRadius = 200f;
        [SerializeField, Range(0.1f, 179f)] private float airburstConeFullAngle = 30f;
        [SerializeField] private LayerMask groundLayers = ~0;

        [Header("Homing")]
        [SerializeField, Min(0f)] private float homingDetectionRadius = 300f;
        [SerializeField, Range(0.1f, 179f)] private float homingDetectionConeFullAngle = 30f;
        [SerializeField, Min(0f)] private float homingTurnRateDegreesPerSecond = 45f;
        [SerializeField, Min(0f)] private float homingDirectHitDamage = 100f;

        [Header("Guided Missile")]
        [SerializeField, Min(0f)] private float guidedTurnRateDegreesPerSecond = 35f;
        [SerializeField, Min(0f)] private float guidedDirectHitDamage = 800f;

        [Header("Future Presentation Overrides")]
        [SerializeField] private GameObject impactVfxOverride;
        [SerializeField] private AudioClip firingAudioOverride;
        [SerializeField] private AudioClip impactAudioOverride;

        public KartikarisShellType ShellType => shellType;
        public KartikarisArtilleryProjectile ProjectilePrefab => projectilePrefab;
        public float MuzzleVelocityOverride => muzzleVelocityOverride;
        public float BallisticTravelSpeedMultiplier => Mathf.Max(0.01f, ballisticTravelSpeedMultiplier);
        public float DirectHitDamage => shellType == KartikarisShellType.Homing
            ? homingDirectHitDamage
            : shellType == KartikarisShellType.GuidedMissile ? guidedDirectHitDamage : 0f;
        public float MaximumExplosionDamage => maximumExplosionDamage;
        public float FullDamageRadius => fullDamageRadius;
        public float ExplosionRadius => explosionRadius;
        public float TreeEffectRadius => treeEffectRadius;
        public float AirburstHeight => airburstHeight;
        public float AirburstArmingMargin => airburstArmingMargin;
        public float AirburstDamage => airburstDamage;
        public float AirburstRadius => airburstRadius;
        public float AirburstConeFullAngle => airburstConeFullAngle;
        public LayerMask GroundLayers => groundLayers;
        public float HomingDetectionRadius => homingDetectionRadius;
        public float HomingDetectionConeFullAngle => homingDetectionConeFullAngle;
        public float HomingTurnRateDegreesPerSecond => homingTurnRateDegreesPerSecond;
        public float GuidedTurnRateDegreesPerSecond => guidedTurnRateDegreesPerSecond;
        public GameObject ImpactVfxOverride => impactVfxOverride;
        public AudioClip FiringAudioOverride => firingAudioOverride;
        public AudioClip ImpactAudioOverride => impactAudioOverride;

        private void OnValidate()
        {
            muzzleVelocityOverride = Mathf.Max(0f, muzzleVelocityOverride);
            ballisticTravelSpeedMultiplier = Mathf.Max(0.01f, ballisticTravelSpeedMultiplier);
            maximumExplosionDamage = Mathf.Max(0f, maximumExplosionDamage);
            explosionRadius = Mathf.Max(0f, explosionRadius);
            fullDamageRadius = Mathf.Clamp(fullDamageRadius, 0f, explosionRadius);
            treeEffectRadius = Mathf.Max(0f, treeEffectRadius);
            airburstHeight = Mathf.Max(0f, airburstHeight);
            airburstArmingMargin = Mathf.Max(0f, airburstArmingMargin);
            airburstDamage = Mathf.Max(0f, airburstDamage);
            airburstRadius = Mathf.Max(0f, airburstRadius);
            airburstConeFullAngle = Mathf.Clamp(airburstConeFullAngle, 0.1f, 179f);
            homingDetectionRadius = Mathf.Max(0f, homingDetectionRadius);
            homingDetectionConeFullAngle = Mathf.Clamp(homingDetectionConeFullAngle, 0.1f, 179f);
            homingTurnRateDegreesPerSecond = Mathf.Max(0f, homingTurnRateDegreesPerSecond);
            homingDirectHitDamage = Mathf.Max(0f, homingDirectHitDamage);
            guidedTurnRateDegreesPerSecond = Mathf.Max(0f, guidedTurnRateDegreesPerSecond);
            guidedDirectHitDamage = Mathf.Max(0f, guidedDirectHitDamage);
        }
    }
}
