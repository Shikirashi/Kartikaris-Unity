using UnityEngine;

namespace Kartikaris.Modules
{
    public sealed class KartikarisSoundDecoyModuleAction : KartikarisModuleAction
    {
        [Header("Runtime Diagnostics")]
        [SerializeField] private KartikarisSoundDecoy deployedDecoy;
        [SerializeField] private bool deploymentAttempted;
        [SerializeField] private bool lost;
        [SerializeField] private bool deployed;
        [SerializeField] private bool alive;
        [SerializeField] private float cooldownRemaining;
        [SerializeField] private bool canActivate;

        public bool IsDeployed => deployedDecoy != null && !deployedDecoy.IsDestroyed;
        public override bool HasActiveDeployable => IsDeployed;
        public bool IsAlive => IsDeployed;
        public bool IsLost => lost;
        public float CooldownRemaining => deployedDecoy != null
            ? deployedDecoy.CooldownRemaining
            : 0f;
        public bool CanActivate => deployedDecoy != null && deployedDecoy.CanActivate;

        private void Update()
        {
            deployed = IsDeployed;
            alive = IsAlive;
            cooldownRemaining = CooldownRemaining;
            canActivate = CanActivate;
        }

        public override bool HandleModuleButtonPressed()
        {
            if (!deploymentAttempted)
                return Deploy();
            return deployedDecoy != null && deployedDecoy.TryActivate();
        }

        private bool Deploy()
        {
            deploymentAttempted = true;
            GameObject instance = InstantiateDefinitionPrefab();
            if (instance == null)
                return false;

            deployedDecoy = instance.GetComponent<KartikarisSoundDecoy>();
            if (deployedDecoy == null)
            {
                Debug.LogError(
                    "A Sound Decoy module prefab requires KartikarisSoundDecoy.",
                    instance);
                return false;
            }

            deployedDecoy.Destroyed += OnDecoyDestroyed;
            deployedDecoy.InitializeDeployable(Context.Owner, Context.OwnerAffiliation);
            deployedDecoy.ConfigureSoundPropagation(Context.SoundPropagation);
            return true;
        }

        private void OnDecoyDestroyed(
            KartikarisDeployable deployable,
            KartikarisDeployableDestroyReason reason)
        {
            deployable.Destroyed -= OnDecoyDestroyed;
            deployedDecoy = null;
            lost = true;
        }

        private void OnDestroy()
        {
            if (deployedDecoy != null)
                deployedDecoy.DestroyDeployable(KartikarisDeployableDestroyReason.OwnerLost);
        }
    }
}
