using System;
using System.Collections.Generic;
using Kartikaris.Audio;
using UnityEngine;

namespace Kartikaris.Radar
{
    [DisallowMultipleComponent]
    public sealed class KartikarisAcousticLocalization : MonoBehaviour
    {
        [Serializable]
        public sealed class PairDiagnostic
        {
            public long EventId;
            public int ProbeReceiverId;
            public Vector3 PlayerReceiverPosition;
            public Vector3 ProbeReceiverPosition;
            public float PlayerArrivalTime;
            public float ProbeArrivalTime;
            public float DeltaTime;
            public float DeltaDistance;
            public float ReceiverBaseline;
            public int RenderedSamples;
            public bool IsValid;
        }

        private sealed class EventRecord
        {
            public bool HasPlayer;
            public KartikarisAcousticDetection Player;
            public readonly Dictionary<int, KartikarisAcousticDetection> Probes = new();
        }

        [SerializeField] private KartikarisSoundPropagation propagation;
        [SerializeField] private KartikarisTdoaHyperbolaGraphic hyperbolaA;
        [SerializeField] private KartikarisTdoaHyperbolaGraphic hyperbolaB;
        [SerializeField, Min(0f)] private float visualizationLifetime = 8f;
        [SerializeField] private PairDiagnostic pairA = new PairDiagnostic();
        [SerializeField] private PairDiagnostic pairB = new PairDiagnostic();

        private readonly Dictionary<long, EventRecord> eventsById = new();
        private long newestEventId;
        private float newestVisualizationTime;
        private KartikarisRadarController subscribedRadar;

        public PairDiagnostic PairA => pairA;
        public PairDiagnostic PairB => pairB;

        private void OnEnable()
        {
            if (propagation == null)
                propagation = FindFirstObjectByType<KartikarisSoundPropagation>();
            if (propagation != null)
                propagation.DetectionArrived += OnDetectionArrived;
            SubscribeToRadarZoom();
        }

        private void OnDisable()
        {
            if (propagation != null)
                propagation.DetectionArrived -= OnDetectionArrived;
            if (subscribedRadar != null)
                subscribedRadar.RadarRangeChanged -= OnRadarRangeChanged;
            subscribedRadar = null;
        }

        private void Update()
        {
            if (visualizationLifetime > 0f && newestEventId != 0 &&
                Time.time - newestVisualizationTime >= visualizationLifetime)
            {
                hyperbolaA?.Clear();
                hyperbolaB?.Clear();
                newestEventId = 0;
            }
        }

        private void OnValidate() => visualizationLifetime = Mathf.Max(0f, visualizationLifetime);

        private void OnDetectionArrived(KartikarisAcousticDetection detection)
        {
            if (!eventsById.TryGetValue(detection.EventId, out EventRecord record))
            {
                record = new EventRecord();
                eventsById[detection.EventId] = record;
            }
            if (detection.ReceiverKind == KartikarisAcousticReceiverKind.Player)
            {
                record.Player = detection;
                record.HasPlayer = true;
            }
            else
            {
                record.Probes[detection.ReceiverId] = detection;
            }

            if (detection.EventId < newestEventId)
                return;
            if (detection.EventId > newestEventId)
            {
                newestEventId = detection.EventId;
                hyperbolaA?.Clear();
                hyperbolaB?.Clear();
                pairA = new PairDiagnostic();
                pairB = new PairDiagnostic();
            }
            RebuildNewest(record);
        }

        private void RebuildNewest(EventRecord record)
        {
            if (!record.HasPlayer)
                return;
            List<int> receiverIds = new List<int>(record.Probes.Keys);
            receiverIds.Sort();
            BuildPair(record.Player, receiverIds.Count > 0 ? record.Probes[receiverIds[0]] : default,
                receiverIds.Count > 0, hyperbolaA, ref pairA);
            BuildPair(record.Player, receiverIds.Count > 1 ? record.Probes[receiverIds[1]] : default,
                receiverIds.Count > 1, hyperbolaB, ref pairB);
            newestVisualizationTime = Time.time;
        }

        private void SubscribeToRadarZoom()
        {
            subscribedRadar = hyperbolaA != null && hyperbolaA.Radar != null
                ? hyperbolaA.Radar
                : hyperbolaB != null
                    ? hyperbolaB.Radar
                    : FindFirstObjectByType<KartikarisRadarController>();
            if (subscribedRadar != null)
                subscribedRadar.RadarRangeChanged += OnRadarRangeChanged;
        }

        private void OnRadarRangeChanged(float unusedRange)
        {
            if (newestEventId == 0)
                return;

            ReprojectPair(hyperbolaA, ref pairA);
            ReprojectPair(hyperbolaB, ref pairB);
        }

        private static void ReprojectPair(
            KartikarisTdoaHyperbolaGraphic renderer,
            ref PairDiagnostic diagnostic)
        {
            renderer?.Clear();
            if (renderer == null || diagnostic == null || !diagnostic.IsValid)
                return;

            renderer.RenderMeasurement(
                diagnostic.PlayerReceiverPosition,
                diagnostic.ProbeReceiverPosition,
                diagnostic.DeltaDistance);
            diagnostic.RenderedSamples = renderer.RenderedSampleCount;
        }

        private static void BuildPair(KartikarisAcousticDetection player,
            KartikarisAcousticDetection probe, bool hasProbe,
            KartikarisTdoaHyperbolaGraphic renderer, ref PairDiagnostic diagnostic)
        {
            renderer?.Clear();
            diagnostic = new PairDiagnostic { EventId = player.EventId };
            if (!hasProbe)
                return;
            diagnostic.ProbeReceiverId = probe.ReceiverId;
            diagnostic.PlayerReceiverPosition = player.ReceiverPosition;
            diagnostic.ProbeReceiverPosition = probe.ReceiverPosition;
            diagnostic.PlayerArrivalTime = player.ArrivalTime;
            diagnostic.ProbeArrivalTime = probe.ArrivalTime;
            diagnostic.DeltaTime = probe.ArrivalTime - player.ArrivalTime;
            diagnostic.DeltaDistance = diagnostic.DeltaTime * player.SoundSpeed;
            diagnostic.ReceiverBaseline = Vector2.Distance(
                new Vector2(player.ReceiverPosition.x, player.ReceiverPosition.z),
                new Vector2(probe.ReceiverPosition.x, probe.ReceiverPosition.z));
            float tolerance = Mathf.Max(0.001f, diagnostic.ReceiverBaseline * 0.0001f);
            diagnostic.IsValid = diagnostic.ReceiverBaseline > tolerance &&
                Mathf.Abs(diagnostic.DeltaDistance) <= diagnostic.ReceiverBaseline + tolerance;
            if (diagnostic.IsValid && renderer != null)
            {
                diagnostic.DeltaDistance = Mathf.Clamp(diagnostic.DeltaDistance,
                    -diagnostic.ReceiverBaseline, diagnostic.ReceiverBaseline);
                renderer.RenderMeasurement(player.ReceiverPosition,
                    probe.ReceiverPosition, diagnostic.DeltaDistance);
                diagnostic.RenderedSamples = renderer.RenderedSampleCount;
            }
        }
    }
}
