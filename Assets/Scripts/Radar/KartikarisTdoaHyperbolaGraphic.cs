using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Kartikaris.Radar
{
    [DisallowMultipleComponent]
    public sealed class KartikarisTdoaHyperbolaGraphic : MaskableGraphic
    {
        [SerializeField] private KartikarisRadarController radar;
        [SerializeField] private Color hyperbolaColor = new Color(1f, 0.08f, 0.08f, 0.9f);
        [SerializeField, Min(0.5f)] private float hyperbolaThickness = 3f;
        [SerializeField, Range(16, 512)] private int hyperbolaResolution = 128;
        [SerializeField] private int renderedSampleCount;

        private readonly List<Vector2> points = new List<Vector2>(256);

        public int RenderedSampleCount => renderedSampleCount;
        public float HyperbolaThickness => hyperbolaThickness;
        public int HyperbolaResolution => hyperbolaResolution;
        public KartikarisRadarController Radar => radar;

        protected override void Awake()
        {
            base.Awake();
            raycastTarget = false;
            color = hyperbolaColor;
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            hyperbolaThickness = Mathf.Max(0.5f, hyperbolaThickness);
            hyperbolaResolution = Mathf.Clamp(hyperbolaResolution, 16, 512);
            color = hyperbolaColor;
            SetVerticesDirty();
        }

        public bool RenderMeasurement(Vector3 playerPosition, Vector3 probePosition, float deltaDistance)
        {
            // Sign convention: deltaDistance = (probe arrival - player arrival) * c,
            // and rendered points satisfy distance(P, probe) - distance(P, player) = deltaDistance.
            points.Clear();
            renderedSampleCount = 0;
            if (radar == null)
                return false;

            Vector2 baselineVector = new Vector2(
                probePosition.x - playerPosition.x,
                probePosition.z - playerPosition.z);
            float baseline = baselineVector.magnitude;
            float tolerance = Mathf.Max(0.001f, baseline * 0.0001f);
            if (baseline <= tolerance || Mathf.Abs(deltaDistance) > baseline + tolerance)
            {
                SetVerticesDirty();
                return false;
            }

            deltaDistance = Mathf.Clamp(deltaDistance, -baseline, baseline);
            Vector2 axis = baselineVector / baseline;
            Vector2 perpendicular = new Vector2(-axis.y, axis.x);
            Vector2 midpoint = baselineVector * 0.5f;
            float range = radar.RadarRange;
            int resolution = Mathf.Clamp(hyperbolaResolution, 16, 512);
            float a = Mathf.Abs(deltaDistance) * 0.5f;
            float c = baseline * 0.5f;

            for (int index = 0; index < resolution; index++)
            {
                float y = Mathf.Lerp(-range, range, index / (float)(resolution - 1));
                float x;
                if (a <= tolerance)
                {
                    x = 0f;
                }
                else if (c - a <= tolerance)
                {
                    float radial = Mathf.Abs(y);
                    x = -Mathf.Sign(deltaDistance) * Mathf.Max(c, radial);
                }
                else
                {
                    float b = Mathf.Sqrt(Mathf.Max(0f, c * c - a * a));
                    x = -Mathf.Sign(deltaDistance) * a *
                        Mathf.Sqrt(1f + y * y / Mathf.Max(0.000001f, b * b));
                }

                Vector2 worldOffset = midpoint + axis * x + perpendicular * y;
                if (worldOffset.sqrMagnitude > range * range)
                    continue;
                points.Add(radar.WorldOffsetToRadar(new Vector3(worldOffset.x, 0f, worldOffset.y)));
            }

            renderedSampleCount = points.Count;
            SetVerticesDirty();
            return points.Count >= 2;
        }

        public void Clear()
        {
            points.Clear();
            renderedSampleCount = 0;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();
            if (points.Count < 2)
                return;
            UIVertex vertex = UIVertex.simpleVert;
            vertex.color = color;
            float halfThickness = hyperbolaThickness * 0.5f;
            for (int index = 0; index < points.Count - 1; index++)
            {
                Vector2 first = points[index];
                Vector2 second = points[index + 1];
                Vector2 direction = second - first;
                if (direction.sqrMagnitude <= 0.000001f)
                    continue;
                Vector2 normal = new Vector2(-direction.y, direction.x).normalized * halfThickness;
                int start = vertexHelper.currentVertCount;
                vertex.position = first - normal; vertexHelper.AddVert(vertex);
                vertex.position = first + normal; vertexHelper.AddVert(vertex);
                vertex.position = second + normal; vertexHelper.AddVert(vertex);
                vertex.position = second - normal; vertexHelper.AddVert(vertex);
                vertexHelper.AddTriangle(start, start + 1, start + 2);
                vertexHelper.AddTriangle(start, start + 2, start + 3);
            }
        }
    }
}
