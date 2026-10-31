using System.Collections.Generic;
using Kartikaris.Player;
using Kartikaris.Radar;
using Kartikaris.Combat;
using UnityEngine;

namespace Kartikaris.Audio
{
    /// <summary>
    /// Small prototype-only sound travel scheduler. It records the emission
    /// position and delays player-facing audio/radar delivery by distance / c.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KartikarisSoundPropagation : MonoBehaviour
    {
        private const float MinimumSpeedOfSound = 1f;

        private sealed class PendingSound
        {
            public long EventId;
            public int ReceiverId;
            public KartikarisAcousticReceiverKind ReceiverKind;
            public Transform ReceiverTransform;
            public Vector3 ReceiverPosition;
            public Vector3 SourcePosition;
            public float EmissionTime;
            public float ArrivalTime;
            public bool DeliverPlayerPresentation;
            public KartikarisAffiliation EmitterAffiliation;
        }

        private sealed class SpatialEmitter
        {
            public AudioSource Source;
            public float AvailableTime;
        }

        [Header("Scene References")]
        [SerializeField] private Transform player;
        [SerializeField] private KartikarisRadarController radar;

        [Header("Propagation")]
        [SerializeField, Min(MinimumSpeedOfSound)] private float speedOfSound = 343f;
        [Tooltip("Independent of probe faction filtering. Disable only to mute cockpit cannon-arrival presentation.")]
        [SerializeField] private bool deliverPlayerCannonPresentation = true;

        [Header("Prototype Audio")]
        [SerializeField, Range(0f, 1f)] private float cannonVolume = 0.9f;
        [SerializeField, Range(0f, 1f)] private float treeBreakVolume = 0.6f;
        [SerializeField, Range(0f, 1f)] private float treeImpactVolume = 0.55f;
        [SerializeField] private bool playRadarNotificationBeep = true;
        [SerializeField, Range(0f, 1f)] private float radarBeepVolume = 0.14f;
        [SerializeField, Min(1f)] private float spatialMinDistance = 12f;
        [SerializeField, Min(1f)] private float spatialMaxDistance = 1000f;
        [SerializeField, Range(1, 16)] private int maximumSpatialEmitters = 8;

        [Header("Runtime Diagnostics")]
        [SerializeField] private Vector3 lastSourcePosition;
        [SerializeField] private float lastSourceDistance;
        [SerializeField] private float lastPropagationDelay;
        [SerializeField] private int deliveredArrivalCount;

        private readonly List<PendingSound> pendingSounds = new List<PendingSound>();
        private readonly List<PendingSound> dueSounds = new List<PendingSound>();
        private readonly List<KartikarisAcousticReceiver> receivers = new List<KartikarisAcousticReceiver>();
        private readonly List<SpatialEmitter> spatialEmitters = new List<SpatialEmitter>();
        private AudioSource radarBeepSource;
        private AudioClip cannonBoomClip;
        private AudioClip radarBeepClip;
        private AudioClip treeBreakClip;
        private AudioClip treeImpactClip;
        private long nextEventId = 1;

        public event System.Action<KartikarisAcousticDetection> DetectionArrived;

        public Transform Player => player;
        public KartikarisRadarController Radar => radar;
        public float SpeedOfSound => speedOfSound;
        public Vector3 LastSourcePosition => lastSourcePosition;
        public float LastSourceDistance => lastSourceDistance;
        public float LastPropagationDelay => lastPropagationDelay;
        public int PendingEventCount => pendingSounds.Count;
        public int DeliveredArrivalCount => deliveredArrivalCount;

        private void Awake()
        {
            ResolveReferences();
            CreatePrototypeClips();
            EnsureRadarBeepSource();
        }

        private void Update()
        {
            DeliverDueSounds(Time.time);
        }

        private void OnDestroy()
        {
            DestroyRuntimeClip(cannonBoomClip);
            DestroyRuntimeClip(radarBeepClip);
            DestroyRuntimeClip(treeBreakClip);
            DestroyRuntimeClip(treeImpactClip);
        }

        private void Reset()
        {
            ResolveReferences();
        }

        private void OnValidate()
        {
            speedOfSound = Mathf.Max(MinimumSpeedOfSound, speedOfSound);
            cannonVolume = Mathf.Clamp01(cannonVolume);
            treeBreakVolume = Mathf.Clamp01(treeBreakVolume);
            treeImpactVolume = Mathf.Clamp01(treeImpactVolume);
            radarBeepVolume = Mathf.Clamp01(radarBeepVolume);
            spatialMinDistance = Mathf.Max(1f, spatialMinDistance);
            spatialMaxDistance = Mathf.Max(spatialMinDistance, spatialMaxDistance);
            maximumSpatialEmitters = Mathf.Clamp(maximumSpatialEmitters, 1, 16);
        }

        public void Configure(Transform newPlayer, KartikarisRadarController newRadar)
        {
            player = newPlayer;
            radar = newRadar;
        }

        /// <summary>
        /// Records a cannon emission. Nothing is played or shown to the player
        /// until the calculated arrival time.
        /// </summary>
        public float EmitCannonDryFire(Vector3 sourceWorldPosition)
        {
            EmitCannonEvent(sourceWorldPosition, KartikarisAffiliation.Neutral);
            return lastPropagationDelay;
        }

        public float EmitCannonDryFire(
            Vector3 sourceWorldPosition,
            KartikarisCombatAffiliation emitterAffiliation)
        {
            EmitCannonEvent(
                sourceWorldPosition,
                emitterAffiliation != null
                    ? emitterAffiliation.Affiliation
                    : KartikarisAffiliation.Neutral);
            return lastPropagationDelay;
        }

        public long EmitCannonEvent(Vector3 sourceWorldPosition)
        {
            return EmitCannonEvent(sourceWorldPosition, KartikarisAffiliation.Neutral);
        }

        public long EmitCannonEvent(
            Vector3 sourceWorldPosition,
            KartikarisCombatAffiliation emitterAffiliation)
        {
            return EmitCannonEvent(
                sourceWorldPosition,
                emitterAffiliation != null
                    ? emitterAffiliation.Affiliation
                    : KartikarisAffiliation.Neutral);
        }

        public long EmitCannonEvent(
            Vector3 sourceWorldPosition,
            KartikarisAffiliation emitterAffiliation)
        {
            ResolveReferences();
            if (player == null)
            {
                Debug.LogWarning("Sound propagation cannot schedule a cannon without a Player transform.", this);
                return 0;
            }

            long eventId = nextEventId++;
            float emissionTime = Time.time;
            lastSourcePosition = sourceWorldPosition;
            lastSourceDistance = Vector3.Distance(sourceWorldPosition, player.position);
            lastPropagationDelay = CalculatePropagationDelay(lastSourceDistance, speedOfSound);
            pendingSounds.Add(new PendingSound
            {
                EventId = eventId,
                ReceiverId = 0,
                ReceiverKind = KartikarisAcousticReceiverKind.Player,
                ReceiverTransform = player,
                ReceiverPosition = player.position,
                SourcePosition = sourceWorldPosition,
                EmissionTime = emissionTime,
                ArrivalTime = emissionTime + lastPropagationDelay,
                DeliverPlayerPresentation = deliverPlayerCannonPresentation,
                EmitterAffiliation = emitterAffiliation
            });

            for (int index = receivers.Count - 1; index >= 0; index--)
            {
                KartikarisAcousticReceiver receiver = receivers[index];
                if (receiver == null)
                {
                    receivers.RemoveAt(index);
                    continue;
                }
                if (!receiver.Accepts(emitterAffiliation))
                    continue;
                Vector3 receiverPosition = receiver.transform.position;
                pendingSounds.Add(new PendingSound
                {
                    EventId = eventId,
                    ReceiverId = receiver.ReceiverId,
                    ReceiverKind = receiver.ReceiverKind,
                    ReceiverTransform = receiver.transform,
                    ReceiverPosition = receiverPosition,
                    SourcePosition = sourceWorldPosition,
                    EmissionTime = emissionTime,
                    ArrivalTime = emissionTime + CalculatePropagationDelay(
                        Vector3.Distance(sourceWorldPosition, receiverPosition), speedOfSound),
                    EmitterAffiliation = emitterAffiliation
                });
            }
            return eventId;
        }

        public void RegisterReceiver(KartikarisAcousticReceiver receiver)
        {
            if (receiver != null && !receivers.Contains(receiver))
                receivers.Add(receiver);
        }

        public void UnregisterReceiver(KartikarisAcousticReceiver receiver)
        {
            if (receiver == null)
                return;

            receivers.Remove(receiver);
            for (int index = pendingSounds.Count - 1; index >= 0; index--)
            {
                PendingSound pending = pendingSounds[index];
                if (!pending.DeliverPlayerPresentation &&
                    pending.ReceiverId == receiver.ReceiverId)
                {
                    pendingSounds.RemoveAt(index);
                }
            }
        }

        public void PlayTreeBreak(Vector3 worldPosition)
        {
            PlaySpatial(treeBreakClip, worldPosition, treeBreakVolume, Time.time);
        }

        public void PlayTreeImpact(Vector3 worldPosition)
        {
            PlaySpatial(treeImpactClip, worldPosition, treeImpactVolume, Time.time);
        }

        public static float CalculatePropagationDelay(float distance, float propagationSpeed)
        {
            return Mathf.Max(0f, distance) / Mathf.Max(MinimumSpeedOfSound, propagationSpeed);
        }

        private void DeliverDueSounds(float currentTime)
        {
            dueSounds.Clear();
            for (int index = pendingSounds.Count - 1; index >= 0; index--)
            {
                PendingSound pending = pendingSounds[index];
                if (currentTime < pending.ArrivalTime)
                    continue;

                pendingSounds.RemoveAt(index);
                dueSounds.Add(pending);
            }

            dueSounds.Sort((first, second) => first.ArrivalTime.CompareTo(second.ArrivalTime));
            for (int index = 0; index < dueSounds.Count; index++)
            {
                PendingSound pending = dueSounds[index];
                Vector3 detectedReceiverPosition = pending.ReceiverTransform != null
                    ? pending.ReceiverTransform.position
                    : pending.ReceiverPosition;
                DetectionArrived?.Invoke(new KartikarisAcousticDetection(
                    pending.EventId, pending.ReceiverId, pending.ReceiverKind,
                    detectedReceiverPosition, pending.ArrivalTime, speedOfSound,
                    pending.EmitterAffiliation));
                if (pending.DeliverPlayerPresentation)
                {
                    PlaySpatial(cannonBoomClip, pending.SourcePosition, cannonVolume, currentTime);
                    radar?.ReportSoundArrival(pending.SourcePosition);
                    if (playRadarNotificationBeep && radarBeepSource != null && radarBeepClip != null)
                        radarBeepSource.PlayOneShot(radarBeepClip, radarBeepVolume);
                    deliveredArrivalCount++;
                }
            }
        }

        private void PlaySpatial(AudioClip clip, Vector3 worldPosition, float volume, float currentTime)
        {
            if (clip == null || volume <= 0f)
                return;

            SpatialEmitter emitter = AcquireSpatialEmitter(currentTime);
            emitter.Source.transform.position = worldPosition;
            emitter.Source.Stop();
            emitter.Source.PlayOneShot(clip, Mathf.Clamp01(volume));
            emitter.AvailableTime = currentTime + clip.length;
        }

        private SpatialEmitter AcquireSpatialEmitter(float currentTime)
        {
            SpatialEmitter oldest = null;
            for (int index = 0; index < spatialEmitters.Count; index++)
            {
                SpatialEmitter emitter = spatialEmitters[index];
                if (emitter.AvailableTime <= currentTime)
                    return emitter;

                if (oldest == null || emitter.AvailableTime < oldest.AvailableTime)
                    oldest = emitter;
            }

            if (spatialEmitters.Count < maximumSpatialEmitters)
            {
                GameObject emitterObject = new GameObject($"Prototype Spatial Audio {spatialEmitters.Count + 1}");
                emitterObject.transform.SetParent(transform, false);
                AudioSource source = emitterObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = false;
                source.spatialBlend = 1f;
                source.rolloffMode = AudioRolloffMode.Logarithmic;
                source.minDistance = spatialMinDistance;
                source.maxDistance = spatialMaxDistance;
                SpatialEmitter created = new SpatialEmitter { Source = source };
                spatialEmitters.Add(created);
                return created;
            }

            return oldest;
        }

        private void CreatePrototypeClips()
        {
            cannonBoomClip = PrototypeAudioSynthesis.CreateCannonBoom("Kartikaris Prototype Cannon Boom");
            radarBeepClip = PrototypeAudioSynthesis.CreateRadarBeep("Kartikaris Radar Sound Hint Beep");
            treeBreakClip = PrototypeAudioSynthesis.CreateTreeBreak("Kartikaris Tree Break");
            treeImpactClip = PrototypeAudioSynthesis.CreateTreeImpact("Kartikaris Tree Impact");
        }

        private void EnsureRadarBeepSource()
        {
            radarBeepSource = GetComponent<AudioSource>();
            if (radarBeepSource == null)
                radarBeepSource = gameObject.AddComponent<AudioSource>();

            radarBeepSource.playOnAwake = false;
            radarBeepSource.loop = false;
            radarBeepSource.spatialBlend = 0f;
        }

        private void ResolveReferences()
        {
            if (player == null)
            {
                MechFirstPersonController playerController = FindFirstObjectByType<MechFirstPersonController>();
                player = playerController != null ? playerController.transform : null;
            }

            if (radar == null)
                radar = FindFirstObjectByType<KartikarisRadarController>();
        }

        private static void DestroyRuntimeClip(AudioClip clip)
        {
            if (clip == null)
                return;

            if (Application.isPlaying)
                Destroy(clip);
            else
                DestroyImmediate(clip);
        }
    }
}
