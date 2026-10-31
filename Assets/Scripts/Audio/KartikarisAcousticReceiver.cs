using UnityEngine;
using Kartikaris.Combat;

namespace Kartikaris.Audio
{
    public enum KartikarisAcousticReceiverKind
    {
        Player,
        SoundProbe
    }

    public enum KartikarisAcousticFilterPolicy
    {
        All,
        HostileOnly,
        NonFriendly
    }

    public readonly struct KartikarisAcousticDetection
    {
        public long EventId { get; }
        public int ReceiverId { get; }
        public KartikarisAcousticReceiverKind ReceiverKind { get; }
        public Vector3 ReceiverPosition { get; }
        public float ArrivalTime { get; }
        public float SoundSpeed { get; }
        public KartikarisAffiliation EmitterAffiliation { get; }

        public KartikarisAcousticDetection(long eventId, int receiverId,
            KartikarisAcousticReceiverKind kind, Vector3 position, float arrivalTime,
            float soundSpeed, KartikarisAffiliation emitterAffiliation)
        {
            EventId = eventId;
            ReceiverId = receiverId;
            ReceiverKind = kind;
            ReceiverPosition = position;
            ArrivalTime = arrivalTime;
            SoundSpeed = soundSpeed;
            EmitterAffiliation = emitterAffiliation;
        }

        public KartikarisAcousticDetection(long eventId, int receiverId,
            KartikarisAcousticReceiverKind kind, Vector3 position, float arrivalTime,
            float soundSpeed)
            : this(eventId, receiverId, kind, position, arrivalTime, soundSpeed,
                KartikarisAffiliation.Neutral)
        {
        }
    }

    [DisallowMultipleComponent]
    public sealed class KartikarisAcousticReceiver : MonoBehaviour
    {
        [SerializeField] private KartikarisSoundPropagation propagation;
        [SerializeField] private KartikarisAcousticReceiverKind receiverKind = KartikarisAcousticReceiverKind.SoundProbe;
        [SerializeField] private int receiverId;
        [Header("Affiliation Filtering")]
        [SerializeField] private KartikarisAffiliation ownerAffiliation = KartikarisAffiliation.Neutral;
        [SerializeField] private KartikarisAcousticFilterPolicy filterPolicy =
            KartikarisAcousticFilterPolicy.HostileOnly;
        [SerializeField] private bool acceptNeutralEvents;

        public int ReceiverId => receiverId;
        public KartikarisAcousticReceiverKind ReceiverKind => receiverKind;
        public KartikarisAffiliation OwnerAffiliation => ownerAffiliation;
        public KartikarisAcousticFilterPolicy FilterPolicy => filterPolicy;

        private void Awake()
        {
            if (receiverId == 0)
                receiverId = GetInstanceID();
        }

        private void OnEnable()
        {
            if (propagation == null)
                propagation = FindFirstObjectByType<KartikarisSoundPropagation>();
            propagation?.RegisterReceiver(this);
        }

        private void OnDisable() => propagation?.UnregisterReceiver(this);

        public void Configure(KartikarisSoundPropagation service, int stableReceiverId)
        {
            propagation?.UnregisterReceiver(this);
            propagation = service;
            receiverId = stableReceiverId != 0 ? stableReceiverId : GetInstanceID();
            if (isActiveAndEnabled)
                propagation?.RegisterReceiver(this);
        }

        public void ConfigureHostileProbe(
            KartikarisSoundPropagation service,
            int stableReceiverId,
            KartikarisAffiliation affiliation,
            bool acceptNeutral = false)
        {
            ownerAffiliation = affiliation;
            filterPolicy = KartikarisAcousticFilterPolicy.HostileOnly;
            acceptNeutralEvents = acceptNeutral;
            Configure(service, stableReceiverId);
        }

        public bool Accepts(KartikarisAffiliation emitterAffiliation)
        {
            if (filterPolicy == KartikarisAcousticFilterPolicy.All)
                return true;
            if (emitterAffiliation == KartikarisAffiliation.Neutral)
                return acceptNeutralEvents;

            bool hostile = KartikarisCombatAffiliation.AreHostile(
                ownerAffiliation,
                emitterAffiliation);
            if (filterPolicy == KartikarisAcousticFilterPolicy.HostileOnly)
                return hostile;

            return hostile || ownerAffiliation == KartikarisAffiliation.Neutral;
        }
    }
}
