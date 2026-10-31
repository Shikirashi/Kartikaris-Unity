using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Kartikaris.UI
{
    /// <summary>
    /// Drives the cockpit heading and elevation tapes without depending on the
    /// pilot's look cameras. Both tapes use the existing scene notch objects as
    /// visual templates and only pool enough notches to cover their masks.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KartikarisHeadingElevationHUD : MonoBehaviour
    {
        public enum LocalElevationAxis
        {
            X,
            Y,
            Z
        }

        private const float NotchDegrees = 10f;
        private const float TextTenthsPerDegree = 10f;
        private const float MinimumDisplayedElevationDegrees = 0f;
        private const float MaximumDisplayedElevationDegrees = 90f;
        private const int MinimumPoolBuffer = 2;
        private const int MaximumPoolNotches = 2049;

        [Header("Sources")]
        [SerializeField] private Transform headingSource;
        [Tooltip("Optional. Leave empty to use the manual elevation value.")]
        [SerializeField] private Transform elevationSource;
        [SerializeField] private LocalElevationAxis elevationAxis = LocalElevationAxis.X;
        [SerializeField] private float elevationAngleOffset;

        [Header("Existing Vision HUD References")]
        [SerializeField] private RectTransform headingContainer;
        [SerializeField] private RectTransform headingNotchTemplate;
        [SerializeField] private TMP_Text headingText;
        [SerializeField] private RectTransform elevationContainer;
        [SerializeField] private RectTransform elevationNotchTemplate;
        [SerializeField] private TMP_Text elevationText;

        [Header("Tape Scale")]
        [Tooltip("Horizontal distance in pixels between adjacent 10-degree heading notches.")]
        [Min(0.1f)]
        [SerializeField] private float headingNotchSpacingPixels = 40f;
        [Tooltip("Vertical distance in pixels between adjacent 10-degree elevation notches.")]
        [Min(0.1f)]
        [SerializeField] private float elevationNotchSpacingPixels = 40f;
        [Min(MinimumPoolBuffer)]
        [SerializeField] private int poolBufferNotches = 4;

        [Header("Elevation")]
        [Range(MinimumDisplayedElevationDegrees, MaximumDisplayedElevationDegrees)]
        [SerializeField] private float manualElevationDegrees;
        [SerializeField] private float minimumElevationDegrees = MinimumDisplayedElevationDegrees;
        [SerializeField] private float maximumElevationDegrees = MaximumDisplayedElevationDegrees;

        private readonly List<RectTransform> headingNotches = new List<RectTransform>();
        private readonly List<RectTransform> elevationNotches = new List<RectTransform>();

        private RectTransform headingPoolRoot;
        private RectTransform elevationPoolRoot;
        private bool poolLayoutDirty = true;
        private bool headingInitialized;
        private float previousNormalizedHeading;
        private float continuousHeading;
        private int activeHeadingNotchCount;
        private int activeElevationNotchCount;
        private int previousElevationBaseTick = int.MinValue;
        private int previousHeadingTextTick = int.MinValue;
        private int previousElevationTextTick = int.MinValue;
        private bool hasRuntimeElevationOverride;
        private float runtimeElevationDegrees;

        public float CurrentHeadingDegrees { get; private set; }
        public float CurrentElevationDegrees { get; private set; }
        public int HeadingPooledNotchCount => activeHeadingNotchCount;
        public int ElevationPooledNotchCount => activeElevationNotchCount;
        public float DegreesPerNotch => NotchDegrees;
        public float HeadingNotchSpacingPixels => headingNotchSpacingPixels;
        public float ElevationNotchSpacingPixels => elevationNotchSpacingPixels;
        public float HeadingPixelsPerDegree => headingNotchSpacingPixels / NotchDegrees;
        public float ElevationPixelsPerDegree => elevationNotchSpacingPixels / NotchDegrees;
        public bool UsesManualElevation => elevationSource == null;
        public bool HasRuntimeElevationOverride => hasRuntimeElevationOverride;

        private void Awake()
        {
            ClampSerializedSettings();
        }

        private void Start()
        {
            Canvas.ForceUpdateCanvases();
            EnsurePools();
            RefreshNow();
        }

        private void LateUpdate()
        {
            EnsurePools();
            RefreshNow();
        }

        private void OnValidate()
        {
            ClampSerializedSettings();
            poolLayoutDirty = true;
        }

        private void OnRectTransformDimensionsChange()
        {
            poolLayoutDirty = true;
        }

        /// <summary>
        /// Sets the elevation used when no elevation transform is assigned.
        /// </summary>
        public void SetElevationDegrees(float degrees)
        {
            manualElevationDegrees = Mathf.Clamp(
                degrees,
                MinimumDisplayedElevationDegrees,
                MaximumDisplayedElevationDegrees);

            if (isActiveAndEnabled)
            {
                RefreshNow();
            }
        }

        /// <summary>
        /// Overrides transform/manual elevation with a measured runtime value.
        /// This lets the weapon display muzzle elevation without changing its
        /// articulation or requiring the HUD's serialized source to be rewired.
        /// </summary>
        public void SetRuntimeElevationDegrees(float degrees)
        {
            runtimeElevationDegrees = Mathf.Clamp(
                degrees,
                MinimumDisplayedElevationDegrees,
                MaximumDisplayedElevationDegrees);
            hasRuntimeElevationOverride = true;

            if (isActiveAndEnabled)
                RefreshNow();
        }

        public void ClearRuntimeElevationOverride()
        {
            hasRuntimeElevationOverride = false;
        }

        /// <summary>
        /// Immediately samples the sources and updates both tapes.
        /// </summary>
        public void RefreshNow()
        {
            if (headingSource == null)
            {
                return;
            }

            float normalizedHeading = NormalizeHeading(headingSource.eulerAngles.y);
            if (!headingInitialized)
            {
                continuousHeading = normalizedHeading;
                previousNormalizedHeading = normalizedHeading;
                headingInitialized = true;
            }
            else
            {
                continuousHeading += Mathf.DeltaAngle(previousNormalizedHeading, normalizedHeading);
                previousNormalizedHeading = normalizedHeading;
            }

            CurrentHeadingDegrees = normalizedHeading;
            CurrentElevationDegrees = ReadElevationDegrees();

            UpdateHeadingTape();
            UpdateElevationTape();
            UpdateHeadingText();
            UpdateElevationText();
        }

        /// <summary>
        /// Used by the scene setup utility to bind the existing VisionHUD hierarchy.
        /// </summary>
        public void Configure(
            Transform newHeadingSource,
            RectTransform newHeadingContainer,
            RectTransform newHeadingTemplate,
            TMP_Text newHeadingText,
            RectTransform newElevationContainer,
            RectTransform newElevationTemplate,
            TMP_Text newElevationText)
        {
            headingSource = newHeadingSource;
            headingContainer = newHeadingContainer;
            headingNotchTemplate = newHeadingTemplate;
            headingText = newHeadingText;
            elevationContainer = newElevationContainer;
            elevationNotchTemplate = newElevationTemplate;
            elevationText = newElevationText;
            elevationSource = null;
            poolLayoutDirty = true;
            headingInitialized = false;
            previousHeadingTextTick = int.MinValue;
            previousElevationTextTick = int.MinValue;
        }

        [ContextMenu("HUD/Manual Elevation/0 Degrees")]
        private void SetManualElevationZero()
        {
            SetManualElevationFromContext(0f);
        }

        [ContextMenu("HUD/Manual Elevation/45 Degrees")]
        private void SetManualElevationFortyFive()
        {
            SetManualElevationFromContext(45f);
        }

        [ContextMenu("HUD/Manual Elevation/90 Degrees")]
        private void SetManualElevationNinety()
        {
            SetManualElevationFromContext(90f);
        }

        private void SetManualElevationFromContext(float degrees)
        {
            if (elevationSource != null)
            {
                Debug.LogWarning(
                    "Manual elevation context commands are disabled while an elevation source is assigned.",
                    this);
                return;
            }

            SetElevationDegrees(degrees);
        }

        private void EnsurePools()
        {
            if (!poolLayoutDirty)
            {
                return;
            }

            if (headingContainer == null || headingNotchTemplate == null ||
                elevationContainer == null || elevationNotchTemplate == null)
            {
                return;
            }

            headingPoolRoot = EnsurePoolRoot(
                headingPoolRoot,
                headingContainer,
                "Heading Notch Pool",
                headingNotchTemplate.gameObject.layer);
            elevationPoolRoot = EnsurePoolRoot(
                elevationPoolRoot,
                elevationContainer,
                "Elevation Notch Pool",
                elevationNotchTemplate.gameObject.layer);

            int desiredHeadingCount = CalculatePoolCount(
                headingContainer.rect.width,
                HeadingNotchSpacingPixels);
            int desiredElevationCount = CalculatePoolCount(
                elevationContainer.rect.height,
                ElevationNotchSpacingPixels);

            ResizePool(
                headingNotches,
                desiredHeadingCount,
                headingNotchTemplate,
                headingPoolRoot,
                HeadingNotchSpacingPixels,
                true);
            ResizePool(
                elevationNotches,
                desiredElevationCount,
                elevationNotchTemplate,
                elevationPoolRoot,
                ElevationNotchSpacingPixels,
                false);

            activeHeadingNotchCount = desiredHeadingCount;
            activeElevationNotchCount = desiredElevationCount;
            headingNotchTemplate.gameObject.SetActive(false);
            elevationNotchTemplate.gameObject.SetActive(false);
            previousElevationBaseTick = int.MinValue;
            poolLayoutDirty = false;
        }

        private RectTransform EnsurePoolRoot(
            RectTransform existingRoot,
            RectTransform parent,
            string rootName,
            int layer)
        {
            if (existingRoot != null)
            {
                return existingRoot;
            }

            GameObject rootObject = new GameObject(rootName, typeof(RectTransform));
            rootObject.hideFlags = HideFlags.DontSave;
            rootObject.layer = layer;
            RectTransform root = rootObject.GetComponent<RectTransform>();
            root.SetParent(parent, false);
            root.anchorMin = new Vector2(0.5f, 0.5f);
            root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.sizeDelta = Vector2.zero;
            root.anchoredPosition = Vector2.zero;
            return root;
        }

        private int CalculatePoolCount(float visibleExtent, float notchSpacing)
        {
            int count = Mathf.CeilToInt(Mathf.Max(0f, visibleExtent) / notchSpacing);
            count += Mathf.Max(MinimumPoolBuffer, poolBufferNotches) * 2 + 3;
            count = Mathf.Clamp(count, 3, MaximumPoolNotches);
            if ((count & 1) == 0)
            {
                count = Mathf.Min(count + 1, MaximumPoolNotches);
            }

            return count;
        }

        private static void ResizePool(
            List<RectTransform> pool,
            int desiredCount,
            RectTransform template,
            RectTransform poolRoot,
            float spacing,
            bool isHeading)
        {
            while (pool.Count < desiredCount)
            {
                RectTransform notch = Instantiate(template, poolRoot, false);
                notch.name = isHeading
                    ? $"Heading Notch {pool.Count:000}"
                    : $"Elevation Notch {pool.Count:000}";
                notch.gameObject.hideFlags = HideFlags.DontSave;
                Graphic graphic = notch.GetComponent<Graphic>();
                if (graphic != null)
                {
                    graphic.raycastTarget = false;
                }

                pool.Add(notch);
            }

            int halfCount = desiredCount / 2;
            Vector2 templatePosition = template.anchoredPosition;

            for (int i = 0; i < pool.Count; i++)
            {
                bool shouldBeActive = i < desiredCount;
                RectTransform notch = pool[i];
                notch.gameObject.SetActive(shouldBeActive);
                if (!shouldBeActive)
                {
                    continue;
                }

                notch.anchorMin = new Vector2(0.5f, 0.5f);
                notch.anchorMax = new Vector2(0.5f, 0.5f);
                notch.pivot = new Vector2(0.5f, 0.5f);
                int offset = i - halfCount;
                notch.anchoredPosition = isHeading
                    ? new Vector2(offset * spacing, templatePosition.y)
                    : new Vector2(templatePosition.x, offset * spacing);
            }
        }

        private void UpdateHeadingTape()
        {
            if (headingPoolRoot == null)
            {
                return;
            }

            float fractionalNotch = Mathf.Repeat(continuousHeading / NotchDegrees, 1f);
            float fractionalDegrees = fractionalNotch * NotchDegrees;
            Vector2 position = headingPoolRoot.anchoredPosition;
            position.x = -fractionalDegrees * HeadingPixelsPerDegree;
            position.y = 0f;
            headingPoolRoot.anchoredPosition = position;
        }

        private void UpdateElevationTape()
        {
            if (elevationPoolRoot == null)
            {
                return;
            }

            float tickCoordinate = SnapNearInteger(CurrentElevationDegrees / NotchDegrees);
            int baseTick = Mathf.FloorToInt(tickCoordinate);
            float fractionalTick = tickCoordinate - baseTick;
            float fractionalDegrees = fractionalTick * NotchDegrees;

            Vector2 position = elevationPoolRoot.anchoredPosition;
            position.x = 0f;
            position.y = -fractionalDegrees * ElevationPixelsPerDegree;
            elevationPoolRoot.anchoredPosition = position;

            if (baseTick == previousElevationBaseTick)
            {
                return;
            }

            previousElevationBaseTick = baseTick;
            int halfCount = activeElevationNotchCount / 2;
            int minimumTick = Mathf.CeilToInt(minimumElevationDegrees / NotchDegrees);
            int maximumTick = Mathf.FloorToInt(maximumElevationDegrees / NotchDegrees);

            for (int i = 0; i < activeElevationNotchCount; i++)
            {
                int representedTick = baseTick + i - halfCount;
                bool isInsideRange = representedTick >= minimumTick && representedTick <= maximumTick;
                if (elevationNotches[i].gameObject.activeSelf != isInsideRange)
                {
                    elevationNotches[i].gameObject.SetActive(isInsideRange);
                }
            }
        }

        private void UpdateHeadingText()
        {
            if (headingText == null)
            {
                return;
            }

            int tenthDegrees = Mathf.RoundToInt(CurrentHeadingDegrees * TextTenthsPerDegree);
            if (tenthDegrees >= 3600)
            {
                tenthDegrees = 0;
            }

            if (tenthDegrees == previousHeadingTextTick)
            {
                return;
            }

            previousHeadingTextTick = tenthDegrees;
            float displayDegrees = tenthDegrees / TextTenthsPerDegree;
            headingText.text = displayDegrees.ToString("000.0", CultureInfo.InvariantCulture) + "\u00b0";
        }

        private void UpdateElevationText()
        {
            if (elevationText == null)
            {
                return;
            }

            int tenthDegrees = Mathf.Clamp(
                Mathf.RoundToInt(CurrentElevationDegrees * TextTenthsPerDegree),
                0,
                Mathf.RoundToInt(MaximumDisplayedElevationDegrees * TextTenthsPerDegree));
            if (tenthDegrees == previousElevationTextTick)
            {
                return;
            }

            previousElevationTextTick = tenthDegrees;
            float displayDegrees = tenthDegrees / TextTenthsPerDegree;
            elevationText.text = displayDegrees.ToString("0.0", CultureInfo.InvariantCulture) + "\u00b0";
        }

        private float ReadElevationDegrees()
        {
            if (hasRuntimeElevationOverride)
            {
                return Mathf.Clamp(
                    runtimeElevationDegrees,
                    MinimumDisplayedElevationDegrees,
                    MaximumDisplayedElevationDegrees);
            }

            float degrees = manualElevationDegrees;
            if (elevationSource != null)
            {
                Vector3 localEulerAngles = elevationSource.localEulerAngles;
                switch (elevationAxis)
                {
                    case LocalElevationAxis.Y:
                        degrees = Mathf.DeltaAngle(0f, localEulerAngles.y);
                        break;
                    case LocalElevationAxis.Z:
                        degrees = Mathf.DeltaAngle(0f, localEulerAngles.z);
                        break;
                    default:
                        degrees = Mathf.DeltaAngle(0f, localEulerAngles.x);
                        break;
                }

                degrees += elevationAngleOffset;
            }

            return Mathf.Clamp(
                degrees,
                MinimumDisplayedElevationDegrees,
                MaximumDisplayedElevationDegrees);
        }

        private void ClampSerializedSettings()
        {
            headingNotchSpacingPixels = Mathf.Max(0.1f, headingNotchSpacingPixels);
            elevationNotchSpacingPixels = Mathf.Max(0.1f, elevationNotchSpacingPixels);
            poolBufferNotches = Mathf.Max(MinimumPoolBuffer, poolBufferNotches);

            minimumElevationDegrees = MinimumDisplayedElevationDegrees;
            maximumElevationDegrees = MaximumDisplayedElevationDegrees;

            manualElevationDegrees = Mathf.Clamp(
                manualElevationDegrees,
                minimumElevationDegrees,
                maximumElevationDegrees);
        }

        private static float NormalizeHeading(float degrees)
        {
            return Mathf.Repeat(degrees, 360f);
        }

        private static float SnapNearInteger(float value)
        {
            float rounded = Mathf.Round(value);
            return Mathf.Abs(value - rounded) < 0.0001f ? rounded : value;
        }
    }
}
