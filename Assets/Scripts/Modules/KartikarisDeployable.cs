using System;
using Kartikaris.Combat;
using UnityEngine;

namespace Kartikaris.Modules
{
    public interface IKartikarisDeployable
    {
        Transform OwningMech { get; }
        KartikarisAffiliation OwnerAffiliation { get; }
        bool IsDeployed { get; }
        bool IsDestroyed { get; }
        bool DestroyDeployable(KartikarisDeployableDestroyReason reason);
    }

    public enum KartikarisDeployableDestroyReason
    {
        Damage,
        Emp,
        BatteryExpired,
        Recovered,
        OwnerLost,
        External
    }

    /// <summary>Shared identity and one-shot cleanup contract for world modules.</summary>
    [DisallowMultipleComponent]
    public abstract class KartikarisDeployable : MonoBehaviour, IKartikarisDeployable
    {
        [Header("Runtime Identity")]
        [SerializeField] private Transform owningMech;
        [SerializeField] private KartikarisAffiliation ownerAffiliation = KartikarisAffiliation.Neutral;
        [SerializeField] private bool deployed;
        [SerializeField] private bool destroyed;
        [SerializeField] private KartikarisDeployableDestroyReason destroyReason;

        private bool cleanupCompleted;

        public Transform OwningMech => owningMech;
        public GameObject OwningGameObject => owningMech != null ? owningMech.gameObject : null;
        public KartikarisAffiliation OwnerAffiliation => ownerAffiliation;
        public bool IsDeployed => deployed;
        public bool IsDestroyed => destroyed;
        public KartikarisDeployableDestroyReason DestroyReason => destroyReason;

        public event Action<KartikarisDeployable, KartikarisDeployableDestroyReason> Destroyed;

        public virtual void InitializeDeployable(
            Transform owner,
            KartikarisAffiliation affiliation)
        {
            owningMech = owner;
            ownerAffiliation = affiliation;
            deployed = true;
            destroyed = false;
            cleanupCompleted = false;
            OnDeployableInitialized();
        }

        public bool DestroyDeployable(KartikarisDeployableDestroyReason reason)
        {
            if (destroyed)
                return false;

            destroyed = true;
            destroyReason = reason;
            CompleteCleanup();
            if (gameObject.activeSelf)
                gameObject.SetActive(false);
            Destroy(gameObject);
            return true;
        }

        protected virtual void OnDeployableInitialized()
        {
        }

        protected virtual void OnDeployableCleanup(
            KartikarisDeployableDestroyReason reason)
        {
        }

        protected virtual void OnDestroy()
        {
            if (!destroyed)
            {
                destroyed = true;
                destroyReason = KartikarisDeployableDestroyReason.External;
            }
            CompleteCleanup();
        }

        private void CompleteCleanup()
        {
            if (cleanupCompleted)
                return;

            cleanupCompleted = true;
            deployed = false;
            OnDeployableCleanup(destroyReason);
            Destroyed?.Invoke(this, destroyReason);
            Destroyed = null;
        }
    }
}
