using System.Collections.Generic;
using UnityEngine;

namespace Kartikaris.Modules
{
    /// <summary>Mounted, faction-agnostic bounded EMP pulse.</summary>
    public sealed class KartikarisEmpModuleAction : KartikarisModuleAction
    {
        [Header("EMP")]
        [SerializeField] private Transform pulseOrigin;
        [SerializeField, Min(0f)] private float effectiveRadius = 30f;
        [SerializeField, Min(0f)] private float cooldown = 60f;
        [SerializeField] private LayerMask deployableLayers = ~0;
        [SerializeField] private GameObject empVfxPrefab;
        [SerializeField, Min(0f)] private float empVfxLifetime;
        [SerializeField, Range(8, 256)] private int overlapCapacity = 64;

        [Header("Runtime Diagnostics")]
        [SerializeField] private float nextReadyTime;
        [SerializeField] private float cooldownRemaining;
        [SerializeField] private bool ready = true;
        [SerializeField] private int lastDestroyedCount;
        [SerializeField] private int pulseCount;

        private Collider[] overlapBuffer;
        private readonly HashSet<IKartikarisDeployable> resolvedDeployables = new();

        public float EffectiveRadius => effectiveRadius;
        public float Cooldown => cooldown;
        public float CooldownRemaining => Mathf.Max(0f, nextReadyTime - Time.time);
        public bool IsReady => CooldownRemaining <= 0f;
        public int LastDestroyedCount => lastDestroyedCount;

        private void OnValidate()
        {
            effectiveRadius = Mathf.Max(0f, effectiveRadius);
            cooldown = Mathf.Max(0f, cooldown);
            empVfxLifetime = Mathf.Max(0f, empVfxLifetime);
            overlapCapacity = Mathf.Clamp(overlapCapacity, 8, 256);
        }

        protected override void OnModuleInitialized()
        {
            EnsureBuffer();
        }

        private void Update()
        {
            cooldownRemaining = Mathf.Max(0f, nextReadyTime - Time.time);
            ready = cooldownRemaining <= 0f;
        }

        public override bool HandleModuleButtonPressed()
        {
            if (!IsInitialized || !IsReady)
                return false;

            EnsureBuffer();
            Transform originTransform = pulseOrigin != null
                ? pulseOrigin
                : Context.Mount != null ? Context.Mount : Context.Owner;
            if (originTransform == null)
                return false;

            Vector3 origin = originTransform.position;
            int overlapCount = Physics.OverlapSphereNonAlloc(
                origin,
                effectiveRadius,
                overlapBuffer,
                deployableLayers,
                QueryTriggerInteraction.Collide);
            resolvedDeployables.Clear();
            for (int index = 0; index < overlapCount; index++)
            {
                Collider overlap = overlapBuffer[index];
                IKartikarisDeployable deployable = overlap != null
                    ? overlap.GetComponentInParent<IKartikarisDeployable>()
                    : null;
                if (deployable != null && !deployable.IsDestroyed)
                    resolvedDeployables.Add(deployable);
            }

            lastDestroyedCount = 0;
            foreach (IKartikarisDeployable deployable in resolvedDeployables)
            {
                if (deployable.DestroyDeployable(KartikarisDeployableDestroyReason.Emp))
                    lastDestroyedCount++;
            }

            if (empVfxPrefab != null)
            {
                GameObject effect = Instantiate(empVfxPrefab, origin, originTransform.rotation);
                effect.transform.SetParent(null, true);
                if (empVfxLifetime > 0f)
                    Destroy(effect, empVfxLifetime);
            }

            pulseCount++;
            nextReadyTime = Time.time + cooldown;
            return true;
        }

        private void EnsureBuffer()
        {
            if (overlapBuffer == null || overlapBuffer.Length != overlapCapacity)
                overlapBuffer = new Collider[overlapCapacity];
        }
    }
}
