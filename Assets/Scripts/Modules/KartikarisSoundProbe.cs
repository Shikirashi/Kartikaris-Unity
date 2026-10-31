using Kartikaris.Audio;
using UnityEngine;

namespace Kartikaris.Modules
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(KartikarisAcousticReceiver))]
    public sealed class KartikarisSoundProbe : KartikarisDeployable
    {
        [SerializeField] private KartikarisAcousticReceiver receiver;
        [SerializeField] private int deployedSlotIndex = -1;

        public int DeployedSlotIndex => deployedSlotIndex;
        public KartikarisAcousticReceiver Receiver => receiver;

        private void Awake()
        {
            if (receiver == null)
                receiver = GetComponent<KartikarisAcousticReceiver>();
        }

        public void Initialize(int slotIndex, Transform owner, KartikarisSoundPropagation propagation)
        {
            Kartikaris.Combat.KartikarisCombatAffiliation affiliation = owner != null
                ? owner.GetComponentInParent<Kartikaris.Combat.KartikarisCombatAffiliation>()
                : null;
            InitializeDeployable(
                owner,
                affiliation != null
                    ? affiliation.Affiliation
                    : Kartikaris.Combat.KartikarisAffiliation.Neutral);
            ConfigureProbe(slotIndex, propagation);
        }

        public void ConfigureProbe(int slotIndex, KartikarisSoundPropagation propagation)
        {
            deployedSlotIndex = slotIndex;
            if (receiver == null)
                receiver = GetComponent<KartikarisAcousticReceiver>();
            int stableId = OwningMech != null
                ? unchecked(OwningMech.GetInstanceID() * 397 ^ (slotIndex + 1))
                : GetInstanceID();
            receiver?.ConfigureHostileProbe(
                propagation,
                stableId,
                OwnerAffiliation);
            transform.SetParent(null, true);
        }
    }
}
