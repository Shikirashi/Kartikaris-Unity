using Kartikaris.Audio;
using Kartikaris.Combat;
using UnityEngine;

namespace Kartikaris.Modules
{
    public readonly struct KartikarisModuleContext
    {
        public int SlotIndex { get; }
        public Transform Owner { get; }
        public KartikarisAffiliation OwnerAffiliation { get; }
        public Transform Mount { get; }
        public KartikarisModuleDefinition Definition { get; }
        public KartikarisSoundPropagation SoundPropagation { get; }

        public KartikarisModuleContext(
            int slotIndex,
            Transform owner,
            KartikarisAffiliation ownerAffiliation,
            Transform mount,
            KartikarisModuleDefinition definition,
            KartikarisSoundPropagation soundPropagation)
        {
            SlotIndex = slotIndex;
            Owner = owner;
            OwnerAffiliation = ownerAffiliation;
            Mount = mount;
            Definition = definition;
            SoundPropagation = soundPropagation;
        }
    }

    /// <summary>Per-slot runtime behavior. The controller only forwards presses.</summary>
    public abstract class KartikarisModuleAction : MonoBehaviour
    {
        protected KartikarisModuleContext Context { get; private set; }
        public bool IsInitialized { get; private set; }
        public virtual bool HasActiveDeployable => false;

        public void InitializeModule(KartikarisModuleContext context)
        {
            Context = context;
            IsInitialized = true;
            OnModuleInitialized();
        }

        public abstract bool HandleModuleButtonPressed();

        protected virtual void OnModuleInitialized()
        {
        }

        protected GameObject InstantiateDefinitionPrefab()
        {
            if (!IsInitialized || Context.Mount == null ||
                Context.Definition == null || Context.Definition.DeployablePrefab == null)
            {
                return null;
            }

            GameObject instance = Instantiate(
                Context.Definition.DeployablePrefab,
                Context.Mount.position,
                Context.Mount.rotation);
            instance.transform.SetParent(null, true);
            return instance;
        }
    }

    /// <summary>
    /// Generic compatibility behavior for one-shot deployables such as the
    /// existing Sound Probe. No module-type switch is required.
    /// </summary>
    public sealed class KartikarisDeployOnceModuleAction : KartikarisModuleAction
    {
        [SerializeField] private KartikarisDeployable deployedInstance;
        [SerializeField] private bool deploymentAttempted;

        public KartikarisDeployable DeployedInstance => deployedInstance;
        public override bool HasActiveDeployable =>
            deployedInstance != null && !deployedInstance.IsDestroyed;

        public override bool HandleModuleButtonPressed()
        {
            if (deploymentAttempted || deployedInstance != null)
                return false;

            deploymentAttempted = true;
            GameObject instance = InstantiateDefinitionPrefab();
            if (instance == null)
                return false;

            deployedInstance = instance.GetComponent<KartikarisDeployable>();
            if (deployedInstance == null)
            {
                Debug.LogError(
                    "A deployable module prefab requires a KartikarisDeployable component.",
                    instance);
                return false;
            }

            deployedInstance.Destroyed += OnDeployableDestroyed;
            deployedInstance.InitializeDeployable(Context.Owner, Context.OwnerAffiliation);
            if (deployedInstance is KartikarisSoundProbe probe)
            {
                probe.ConfigureProbe(
                    Context.SlotIndex,
                    Context.SoundPropagation);
            }
            return true;
        }

        private void OnDeployableDestroyed(
            KartikarisDeployable deployable,
            KartikarisDeployableDestroyReason reason)
        {
            deployable.Destroyed -= OnDeployableDestroyed;
            deployedInstance = null;
        }

        private void OnDestroy()
        {
            if (deployedInstance != null)
                deployedInstance.DestroyDeployable(KartikarisDeployableDestroyReason.OwnerLost);
        }
    }
}
