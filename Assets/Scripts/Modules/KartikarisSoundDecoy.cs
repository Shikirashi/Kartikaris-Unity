using Kartikaris.Audio;
using UnityEngine;

namespace Kartikaris.Modules
{
    /// <summary>Cannon-like acoustic deception with no projectile or impact.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(KartikarisDeployableHealth))]
    public sealed class KartikarisSoundDecoy : KartikarisDeployable
    {
        [Header("Explicit References")]
        [SerializeField] private KartikarisSoundPropagation soundPropagation;
        [SerializeField] private Transform activationPoint;
        [SerializeField] private GameObject muzzleFlashPrefab;
        [SerializeField] private GameObject lingeringSmokePrefab;

        [Header("Activation")]
        [SerializeField, Min(0f)] private float activationCooldown = 15f;
        [SerializeField, Min(0f)] private float muzzleFlashLifetime;
        [SerializeField, Min(0f)] private float lingeringSmokeLifetime = 30f;

        [Header("Runtime Diagnostics")]
        [SerializeField] private float nextActivationTime;
        [SerializeField] private long lastAcousticEventId;
        [SerializeField] private int successfulActivationCount;
        [SerializeField] private float cooldownRemaining;
        [SerializeField] private bool canActivate;

        public float ActivationCooldown => activationCooldown;
        public float CooldownRemaining => Mathf.Max(0f, nextActivationTime - Time.time);
        public bool CanActivate => IsDeployed && !IsDestroyed && CooldownRemaining <= 0f;
        public long LastAcousticEventId => lastAcousticEventId;
        public int SuccessfulActivationCount => successfulActivationCount;

        private void OnValidate()
        {
            activationCooldown = Mathf.Max(0f, activationCooldown);
            muzzleFlashLifetime = Mathf.Max(0f, muzzleFlashLifetime);
            lingeringSmokeLifetime = Mathf.Max(0f, lingeringSmokeLifetime);
        }

        private void Update()
        {
            cooldownRemaining = CooldownRemaining;
            canActivate = CanActivate;
        }

        public void ConfigureSoundPropagation(KartikarisSoundPropagation propagation)
        {
            soundPropagation = propagation;
        }

        public bool TryActivate()
        {
            if (!CanActivate || soundPropagation == null)
                return false;

            Transform point = activationPoint != null ? activationPoint : transform;
            lastAcousticEventId = soundPropagation.EmitCannonEvent(
                point.position,
                OwnerAffiliation);
            if (lastAcousticEventId == 0)
                return false;

            SpawnDetached(
                muzzleFlashPrefab,
                point.position,
                Quaternion.LookRotation(-point.right, point.up),
                muzzleFlashLifetime);
            SpawnDetached(
                lingeringSmokePrefab,
                point.position,
                point.rotation,
                lingeringSmokeLifetime);
            successfulActivationCount++;
            nextActivationTime = Time.time + activationCooldown;
            return true;
        }

        private static void SpawnDetached(
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
    }
}
