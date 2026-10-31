using UnityEngine;
using UnityEngine.UI;

namespace Kartikaris.Radar
{
    /// <summary>
    /// Crisp UI-mesh radar grid whose geometry shares the radar controller's
    /// current world-distance scale. Sweep and heading overlays remain images.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KartikarisRadarGridGraphic : MaskableGraphic
    {
        private const int MinimumCircleSegments = 32;
        private const int MaximumCircleSegments = 512;
        private const int MaximumGeneratedGridSteps = 128;

        [Header("Radar Reference")]
        [SerializeField] private KartikarisRadarController radar;

        [Header("World Grid")]
        [SerializeField, Min(1f)] private float gridSpacingMeters = 500f;
        [SerializeField, Range(4, 32)] private int radialSubdivisionCount = 4;
        [SerializeField, Range(MinimumCircleSegments, MaximumCircleSegments)]
        private int circleSegments = 128;

        [Header("Style")]
        [SerializeField] private Color faceColor = new Color(0f, 0f, 0f, 1f);
        [SerializeField] private Color gridColor = new Color(0f, 1f, 0.08f, 0.82f);
        [SerializeField, Min(0.25f)] private float lineThickness = 2f;
        [SerializeField, Min(0.25f)] private float majorAxisThickness = 3f;
        [SerializeField, Min(0.25f)] private float boundaryThickness = 4f;
        [SerializeField, Min(0f)] private float centerPointDiameter = 6f;
        [SerializeField] private bool drawCartesianSubdivisions = true;
        [SerializeField] private bool drawRadialSubdivisions = true;

        [Header("Runtime Diagnostics")]
        [SerializeField] private float renderedRadarRange;
        [SerializeField] private float renderedRadarRadius;
        [SerializeField] private int renderedRingCount;
        [SerializeField] private int renderedGridLineCount;
        [SerializeField] private int geometryRebuildCount;

        private KartikarisRadarController subscribedRadar;

        public KartikarisRadarController Radar => radar;
        public float GridSpacingMeters => gridSpacingMeters;
        public int RenderedRingCount => renderedRingCount;
        public int RenderedGridLineCount => renderedGridLineCount;
        public int GeometryRebuildCount => geometryRebuildCount;

        protected override void Awake()
        {
            base.Awake();
            raycastTarget = false;
            ResolveRadarReference();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            raycastTarget = false;
            ResolveRadarReference();
            SubscribeToRadar();
            SetVerticesDirty();
        }

        protected override void OnDisable()
        {
            UnsubscribeFromRadar();
            base.OnDisable();
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            gridSpacingMeters = Mathf.Max(1f, gridSpacingMeters);
            radialSubdivisionCount = Mathf.Clamp(radialSubdivisionCount, 4, 32);
            circleSegments = Mathf.Clamp(
                circleSegments,
                MinimumCircleSegments,
                MaximumCircleSegments);
            lineThickness = Mathf.Max(0.25f, lineThickness);
            majorAxisThickness = Mathf.Max(0.25f, majorAxisThickness);
            boundaryThickness = Mathf.Max(0.25f, boundaryThickness);
            centerPointDiameter = Mathf.Max(0f, centerPointDiameter);
            raycastTarget = false;
            SetVerticesDirty();
        }

        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();
            renderedRingCount = 0;
            renderedGridLineCount = 0;
            renderedRadarRange = radar != null ? radar.CurrentRadarRange : 0f;
            renderedRadarRadius = ResolveRadarRadius();
            geometryRebuildCount++;

            if (renderedRadarRange <= 0f || renderedRadarRadius <= 0f)
                return;

            Rect rect = rectTransform.rect;
            Vector2 center = rect.center;
            Color32 vertexColor = gridColor;
            int safeCircleSegments = Mathf.Clamp(
                circleSegments,
                MinimumCircleSegments,
                MaximumCircleSegments);

            if (faceColor.a > 0f)
            {
                AddFilledCircle(
                    vertexHelper,
                    center,
                    renderedRadarRadius,
                    safeCircleSegments,
                    faceColor);
            }

            int completeGridSteps = Mathf.Min(
                Mathf.FloorToInt(renderedRadarRange / gridSpacingMeters),
                MaximumGeneratedGridSteps);
            for (int stepIndex = 1; stepIndex <= completeGridSteps; stepIndex++)
            {
                float worldDistance = stepIndex * gridSpacingMeters;
                float ringRadius = worldDistance / renderedRadarRange * renderedRadarRadius;
                bool coincidesWithBoundary = Mathf.Abs(
                    worldDistance - renderedRadarRange) <= 0.001f;
                if (!coincidesWithBoundary)
                {
                    AddRing(
                        vertexHelper,
                        center,
                        ringRadius,
                        lineThickness,
                        safeCircleSegments,
                        vertexColor);
                    renderedRingCount++;
                }

                if (drawCartesianSubdivisions && !coincidesWithBoundary)
                {
                    AddCartesianGridPair(
                        vertexHelper,
                        center,
                        ringRadius,
                        renderedRadarRadius,
                        lineThickness,
                        vertexColor);
                    renderedGridLineCount += 4;
                }
            }

            AddRing(
                vertexHelper,
                center,
                renderedRadarRadius,
                boundaryThickness,
                safeCircleSegments,
                vertexColor);
            renderedRingCount++;

            AddLine(vertexHelper,
                center + Vector2.left * renderedRadarRadius,
                center + Vector2.right * renderedRadarRadius,
                majorAxisThickness,
                vertexColor);
            AddLine(vertexHelper,
                center + Vector2.down * renderedRadarRadius,
                center + Vector2.up * renderedRadarRadius,
                majorAxisThickness,
                vertexColor);
            renderedGridLineCount += 2;

            if (drawRadialSubdivisions)
            {
                int safeRadialCount = Mathf.Clamp(radialSubdivisionCount, 4, 32);
                for (int index = 0; index < safeRadialCount; index++)
                {
                    float angle = index * Mathf.PI * 2f / safeRadialCount;
                    Vector2 direction = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle));
                    if (Mathf.Abs(direction.x) <= 0.0001f ||
                        Mathf.Abs(direction.y) <= 0.0001f)
                    {
                        continue;
                    }

                    AddLine(
                        vertexHelper,
                        center,
                        center + direction * renderedRadarRadius,
                        lineThickness,
                        vertexColor);
                    renderedGridLineCount++;
                }
            }

            if (centerPointDiameter > 0f)
                AddFilledSquare(vertexHelper, center, centerPointDiameter, vertexColor);
        }

        private void ResolveRadarReference()
        {
            if (radar == null)
                radar = GetComponentInParent<KartikarisRadarController>();
        }

        private void SubscribeToRadar()
        {
            if (subscribedRadar == radar)
                return;

            UnsubscribeFromRadar();
            subscribedRadar = radar;
            if (subscribedRadar != null)
                subscribedRadar.RadarRangeChanged += OnRadarRangeChanged;
        }

        private void UnsubscribeFromRadar()
        {
            if (subscribedRadar != null)
                subscribedRadar.RadarRangeChanged -= OnRadarRangeChanged;
            subscribedRadar = null;
        }

        private void OnRadarRangeChanged(float unusedRange)
        {
            SetVerticesDirty();
        }

        private float ResolveRadarRadius()
        {
            if (radar == null)
                return 0f;

            Rect rect = rectTransform.rect;
            float localLimit = Mathf.Min(rect.width, rect.height) * 0.5f;
            return Mathf.Min(Mathf.Max(0f, radar.EffectiveRadarRadius), localLimit);
        }

        private static void AddCartesianGridPair(
            VertexHelper vertexHelper,
            Vector2 center,
            float offset,
            float boundaryRadius,
            float thickness,
            Color32 color)
        {
            float halfSpan = Mathf.Sqrt(Mathf.Max(
                0f,
                boundaryRadius * boundaryRadius - offset * offset));
            AddLine(vertexHelper,
                center + new Vector2(-halfSpan, offset),
                center + new Vector2(halfSpan, offset),
                thickness,
                color);
            AddLine(vertexHelper,
                center + new Vector2(-halfSpan, -offset),
                center + new Vector2(halfSpan, -offset),
                thickness,
                color);
            AddLine(vertexHelper,
                center + new Vector2(offset, -halfSpan),
                center + new Vector2(offset, halfSpan),
                thickness,
                color);
            AddLine(vertexHelper,
                center + new Vector2(-offset, -halfSpan),
                center + new Vector2(-offset, halfSpan),
                thickness,
                color);
        }

        private static void AddRing(
            VertexHelper vertexHelper,
            Vector2 center,
            float radius,
            float thickness,
            int segments,
            Color32 color)
        {
            float halfThickness = thickness * 0.5f;
            float innerRadius = Mathf.Max(0f, radius - halfThickness);
            float outerRadius = radius + halfThickness;
            for (int index = 0; index < segments; index++)
            {
                float firstAngle = index * Mathf.PI * 2f / segments;
                float secondAngle = (index + 1) * Mathf.PI * 2f / segments;
                Vector2 firstDirection = new Vector2(
                    Mathf.Sin(firstAngle),
                    Mathf.Cos(firstAngle));
                Vector2 secondDirection = new Vector2(
                    Mathf.Sin(secondAngle),
                    Mathf.Cos(secondAngle));
                AddQuad(
                    vertexHelper,
                    center + firstDirection * innerRadius,
                    center + firstDirection * outerRadius,
                    center + secondDirection * outerRadius,
                    center + secondDirection * innerRadius,
                    color);
            }
        }

        private static void AddLine(
            VertexHelper vertexHelper,
            Vector2 first,
            Vector2 second,
            float thickness,
            Color32 color)
        {
            Vector2 direction = second - first;
            if (direction.sqrMagnitude <= 0.000001f)
                return;

            Vector2 normal = new Vector2(-direction.y, direction.x).normalized *
                (thickness * 0.5f);
            AddQuad(
                vertexHelper,
                first - normal,
                first + normal,
                second + normal,
                second - normal,
                color);
        }

        private static void AddFilledSquare(
            VertexHelper vertexHelper,
            Vector2 center,
            float diameter,
            Color32 color)
        {
            float halfSize = diameter * 0.5f;
            Vector2 offset = new Vector2(halfSize, halfSize);
            AddQuad(
                vertexHelper,
                center - offset,
                center + new Vector2(-halfSize, halfSize),
                center + offset,
                center + new Vector2(halfSize, -halfSize),
                color);
        }

        private static void AddFilledCircle(
            VertexHelper vertexHelper,
            Vector2 center,
            float radius,
            int segments,
            Color32 color)
        {
            int start = vertexHelper.currentVertCount;
            UIVertex vertex = UIVertex.simpleVert;
            vertex.color = color;
            vertex.position = center;
            vertexHelper.AddVert(vertex);

            for (int index = 0; index <= segments; index++)
            {
                float angle = index * Mathf.PI * 2f / segments;
                vertex.position = center + new Vector2(
                    Mathf.Sin(angle),
                    Mathf.Cos(angle)) * radius;
                vertexHelper.AddVert(vertex);
            }

            for (int index = 0; index < segments; index++)
                vertexHelper.AddTriangle(start, start + index + 1, start + index + 2);
        }

        private static void AddQuad(
            VertexHelper vertexHelper,
            Vector2 first,
            Vector2 second,
            Vector2 third,
            Vector2 fourth,
            Color32 color)
        {
            int start = vertexHelper.currentVertCount;
            UIVertex vertex = UIVertex.simpleVert;
            vertex.color = color;
            vertex.position = first;
            vertexHelper.AddVert(vertex);
            vertex.position = second;
            vertexHelper.AddVert(vertex);
            vertex.position = third;
            vertexHelper.AddVert(vertex);
            vertex.position = fourth;
            vertexHelper.AddVert(vertex);
            vertexHelper.AddTriangle(start, start + 1, start + 2);
            vertexHelper.AddTriangle(start, start + 2, start + 3);
        }
    }
}
