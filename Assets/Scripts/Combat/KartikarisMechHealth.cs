using System;
using UnityEngine;

namespace Kartikaris.Combat
{
    /// <summary>
    /// Lightweight reusable mech health. Death is reported but deliberately
    /// does not destroy or disable the owning GameObject.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KartikarisMechHealth : MonoBehaviour
    {
        [Header("Health")]
        [SerializeField, Min(0.01f)] private float maximumHealth = 1000f;

        [Header("Runtime Diagnostics")]
        [SerializeField] private float currentHealth;
        [SerializeField] private bool isDead;
        [SerializeField] private GameObject lastDamageSource;
        [SerializeField] private Vector3 lastDamageOrigin;

        public float MaximumHealth => maximumHealth;
        public float CurrentHealth => currentHealth;
        public bool IsDead => isDead;
        public bool IsAlive => !isDead;
        public GameObject LastDamageSource => lastDamageSource;
        public Vector3 LastDamageOrigin => lastDamageOrigin;

        public event Action<KartikarisMechHealth, float> Damaged;
        public event Action<KartikarisMechHealth> Killed;

        private void Awake()
        {
            ResetHealth();
        }

        private void OnValidate()
        {
            maximumHealth = Mathf.Max(0.01f, maximumHealth);
            if (Application.isPlaying)
                currentHealth = Mathf.Clamp(currentHealth, 0f, maximumHealth);
        }

        /// <summary>
        /// Applies positive damage once and returns the health actually removed.
        /// Dead targets ignore subsequent damage.
        /// </summary>
        public float ApplyDamage(float damage, GameObject source = null, Vector3 damageOrigin = default)
        {
            float safeDamage = Mathf.Max(0f, damage);
            if (isDead || safeDamage <= 0f)
                return 0f;

            lastDamageSource = source;
            lastDamageOrigin = damageOrigin;
            float previousHealth = currentHealth;
            currentHealth = Mathf.Max(0f, currentHealth - safeDamage);
            float damageApplied = previousHealth - currentHealth;
            Damaged?.Invoke(this, damageApplied);

            if (currentHealth <= 0f)
            {
                isDead = true;
                Killed?.Invoke(this);
            }

            return damageApplied;
        }

        public void ResetHealth()
        {
            currentHealth = Mathf.Max(0.01f, maximumHealth);
            isDead = false;
            lastDamageSource = null;
            lastDamageOrigin = default;
        }
    }
}
