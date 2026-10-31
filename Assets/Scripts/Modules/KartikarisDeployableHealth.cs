using System;
using UnityEngine;

namespace Kartikaris.Modules
{
    /// <summary>Optional normal-damage health for destructible deployables.</summary>
    [DisallowMultipleComponent]
    public sealed class KartikarisDeployableHealth : MonoBehaviour
    {
        [SerializeField, Min(0.01f)] private float maximumHealth = 100f;
        [SerializeField] private KartikarisDeployable deployable;

        [Header("Runtime Diagnostics")]
        [SerializeField] private float currentHealth;
        [SerializeField] private bool isDead;

        public float MaximumHealth => maximumHealth;
        public float CurrentHealth => currentHealth;
        public bool IsAlive => !isDead;

        public event Action<KartikarisDeployableHealth, float> Damaged;
        public event Action<KartikarisDeployableHealth> Killed;

        private void Awake()
        {
            if (deployable == null)
                deployable = GetComponentInParent<KartikarisDeployable>();
            ResetHealth();
        }

        private void OnValidate()
        {
            maximumHealth = Mathf.Max(0.01f, maximumHealth);
            if (Application.isPlaying)
                currentHealth = Mathf.Clamp(currentHealth, 0f, maximumHealth);
        }

        public float ApplyDamage(
            float damage,
            GameObject source = null,
            Vector3 damageOrigin = default)
        {
            float safeDamage = Mathf.Max(0f, damage);
            if (isDead || safeDamage <= 0f)
                return 0f;

            float previous = currentHealth;
            currentHealth = Mathf.Max(0f, currentHealth - safeDamage);
            float applied = previous - currentHealth;
            Damaged?.Invoke(this, applied);
            if (currentHealth <= 0f)
            {
                isDead = true;
                Killed?.Invoke(this);
                deployable?.DestroyDeployable(KartikarisDeployableDestroyReason.Damage);
            }
            return applied;
        }

        public void ResetHealth()
        {
            currentHealth = maximumHealth;
            isDead = false;
        }
    }
}
