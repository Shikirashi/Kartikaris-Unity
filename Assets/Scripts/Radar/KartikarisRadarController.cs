using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Kartikaris.Radar
{
    /// <summary>
    /// Player-centred, north-up cockpit radar presentation. This component owns
    /// presentation only: spotting and sound propagation report into its APIs.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KartikarisRadarController : MonoBehaviour
    {
        private const float LegacyDefaultRadarRange = 500f;

        private sealed class ContactView
        {
            public Image Image;
            public float FadeElapsed = float.PositiveInfinity;
        }

        [Header("Scene References")]
        [SerializeField] private Transform player;
        [SerializeField] private RectTransform radarRoot;
        [SerializeField] private RectTransform sweep;
        [SerializeField] private RectTransform contactsRoot;
        [SerializeField] private Image playerCenter;
        [SerializeField] private Image soundOriginHint;

        [Header("Sprites")]
        [SerializeField] private Sprite contactSprite;
        [Tooltip("Optional. No sound wedge is drawn until a sprite is assigned.")]
        [SerializeField] private Sprite soundOriginHintSprite;

        [Header("Radar Range / Zoom")]
        [FormerlySerializedAs("radarRange")]
        [SerializeField, Min(1f)] private float defaultRadarRange = 3000f;
        [SerializeField, Min(1f)] private float minimumRadarRange = 250f;
        [SerializeField, Min(1f)] private float maximumRadarRange = 3000f;
        [Tooltip("Exponential zoom response per mouse-scroll unit. Unity mice commonly report 120 units per wheel notch.")]
        [SerializeField, Min(0.00001f)] private float zoomSensitivity = 0.002f;
        [SerializeField, HideInInspector] private bool zoomConfigurationInitialized;

        [Header("Radar Presentation")]
        [Range(0.1f, 1f)] [SerializeField] private float mapToRadarScale = 0.87f;
        [Min(0f)] [SerializeField] private float sweepRotationsPerSecond = 0.8f;
        [Min(0.01f)] [SerializeField] private float contactFadeDuration = 0.5f;
        [Min(1f)] [SerializeField] private float contactDiameter = 18f;
        [SerializeField] private Color friendlyColor = new(0.18f, 0.62f, 1f, 1f);
        [SerializeField] private Color enemyColor = new(1f, 0.16f, 0.1f, 1f);

        [Header("Sound Origin Hint")]
        [Range(0f, 180f)] [SerializeField] private float soundMaxInaccuracyDegrees = 15f;
        [Min(0f)] [SerializeField] private float soundHintVisibleDuration = 2f;
        [Min(0.01f)] [SerializeField] private float soundHintFadeDuration = 2f;
        [Tooltip("Use this only to compensate for the authored orientation of the assigned wedge sprite.")]
        [SerializeField] private float soundHintNorthRotationOffset;

        private readonly Dictionary<RadarContact, ContactView> contactViews = new();
        private readonly List<RadarContact> staleContacts = new();
        private System.Random soundRandom;
        private float sweepBearingDegrees;
        private bool soundHintActive;
        private float soundHintAge;
        private float currentSoundTrueBearing;
        private float currentSoundDisplayedBearing;
        private float currentSoundError;

        [Header("Runtime Diagnostics")]
        [SerializeField] private float currentRadarRange;

        public event Action<float> RadarRangeChanged;

        public float RadarRange => currentRadarRange;
        public float CurrentRadarRange => currentRadarRange;
        public float DefaultRadarRange => defaultRadarRange;
        public float MinimumRadarRange => minimumRadarRange;
        public float MaximumRadarRange => maximumRadarRange;
        public float ZoomSensitivity => zoomSensitivity;
        public Transform Player => player;
        public float CurrentSweepBearing => sweepBearingDegrees;
        public bool HasActiveSoundHint => soundHintActive;
        public float CurrentSoundTrueBearing => currentSoundTrueBearing;
        public float CurrentSoundDisplayedBearing => currentSoundDisplayedBearing;
        public float CurrentSoundError => currentSoundError;
        public Color FriendlyColor => friendlyColor;
        public Color EnemyColor => enemyColor;

        public float EffectiveRadarRadius
        {
            get
            {
                if (radarRoot == null)
                    return 0f;

                Rect rect = radarRoot.rect;
                return Mathf.Min(rect.width, rect.height) * 0.5f * mapToRadarScale;
            }
        }

        private void Awake()
        {
            ResolveMissingReferences();
            SanitizeRadarRangeConfiguration();
            SetCurrentRadarRange(defaultRadarRange, false);
            soundRandom = new System.Random(Environment.TickCount ^ GetInstanceID());

            if (soundOriginHint != null)
            {
                soundOriginHint.sprite = soundOriginHintSprite;
                soundOriginHint.raycastTarget = false;
                soundOriginHint.gameObject.SetActive(false);
            }
        }

        private void OnEnable()
        {
            if (!Application.isPlaying)
                return;

            RadarContact.ContactAdded += AddContact;
            RadarContact.ContactRemoved += RemoveContact;

            foreach (RadarContact contact in RadarContact.ActiveContacts)
                AddContact(contact);
        }

        private void OnDisable()
        {
            if (!Application.isPlaying)
                return;

            RadarContact.ContactAdded -= AddContact;
            RadarContact.ContactRemoved -= RemoveContact;
            DestroyContactViews();
        }

        private void LateUpdate()
        {
            UpdateZoomInput();
            AdvancePresentation(Time.unscaledDeltaTime);
        }

        private void OnValidate()
        {
            SanitizeRadarRangeConfiguration();
            if (!Application.isPlaying)
                currentRadarRange = defaultRadarRange;
            mapToRadarScale = Mathf.Clamp(mapToRadarScale, 0.1f, 1f);
            sweepRotationsPerSecond = Mathf.Max(0f, sweepRotationsPerSecond);
            contactFadeDuration = Mathf.Max(0.01f, contactFadeDuration);
            contactDiameter = Mathf.Max(1f, contactDiameter);
            soundMaxInaccuracyDegrees = Mathf.Clamp(soundMaxInaccuracyDegrees, 0f, 180f);
            soundHintVisibleDuration = Mathf.Max(0f, soundHintVisibleDuration);
            soundHintFadeDuration = Mathf.Max(0.01f, soundHintFadeDuration);

            if (playerCenter != null)
                playerCenter.raycastTarget = false;

            if (soundOriginHint != null)
                soundOriginHint.raycastTarget = false;
        }

        public void Configure(
            Transform newPlayer,
            RectTransform newRadarRoot,
            RectTransform newSweep,
            RectTransform newContactsRoot,
            Image newPlayerCenter,
            Image newSoundOriginHint,
            Sprite newContactSprite,
            Sprite newSoundOriginHintSprite,
            float newRadarRange,
            float newMapToRadarScale,
            float newSweepRotationsPerSecond,
            float newContactFadeDuration)
        {
            player = newPlayer;
            radarRoot = newRadarRoot;
            sweep = newSweep;
            contactsRoot = newContactsRoot;
            playerCenter = newPlayerCenter;
            soundOriginHint = newSoundOriginHint;
            contactSprite = newContactSprite;
            soundOriginHintSprite = newSoundOriginHintSprite;
            defaultRadarRange = Mathf.Clamp(
                Mathf.Max(1f, newRadarRange),
                minimumRadarRange,
                maximumRadarRange);
            SetCurrentRadarRange(defaultRadarRange, Application.isPlaying);
            mapToRadarScale = Mathf.Clamp(newMapToRadarScale, 0.1f, 1f);
            sweepRotationsPerSecond = Mathf.Max(0f, newSweepRotationsPerSecond);
            contactFadeDuration = Mathf.Max(0.01f, newContactFadeDuration);

            if (soundOriginHint != null)
            {
                soundOriginHint.sprite = soundOriginHintSprite;
                soundOriginHint.raycastTarget = false;
                soundOriginHint.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Maps a world position onto the north-up radar. +Z is always up and
        /// +X is always right, regardless of the player's current rotation.
        /// </summary>
        public Vector2 WorldPositionToRadar(Vector3 worldPosition)
        {
            if (player == null)
                return Vector2.zero;

            return WorldOffsetToRadar(worldPosition - player.position);
        }

        public Vector2 WorldOffsetToRadar(Vector3 worldOffset)
        {
            return CalculateNorthUpRadarPosition(
                worldOffset,
                currentRadarRange,
                EffectiveRadarRadius);
        }

        public bool IsWorldPositionWithinRange(Vector3 worldPosition)
        {
            if (player == null)
                return false;

            Vector3 delta = worldPosition - player.position;
            delta.y = 0f;
            return delta.sqrMagnitude <= currentRadarRange * currentRadarRange;
        }

        public bool IsContactVisibleOnRadar(RadarContact contact)
        {
            if (player == null || contact == null || !contact.isActiveAndEnabled ||
                !contact.gameObject.activeInHierarchy || !contact.IsRadarEligible)
            {
                return false;
            }

            return IsWorldPositionWithinRange(contact.WorldPosition);
        }

        public Color GetContactColor(RadarContact contact)
        {
            return contact != null && contact.Affiliation == RadarAffiliation.Friendly
                ? friendlyColor
                : enemyColor;
        }

        /// <summary>
        /// Reports one completed sound-arrival event. The bearing error is sampled
        /// exactly once here; subsequent frames only age and fade this event.
        /// </summary>
        public void ReportSoundArrival(Vector3 sourceWorldPosition)
        {
            if (player == null)
            {
                Debug.LogWarning("Radar cannot report a sound arrival without a player transform.", this);
                return;
            }

            Vector3 direction = sourceWorldPosition - player.position;
            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.0001f)
            {
                soundHintActive = false;
                if (soundOriginHint != null)
                    soundOriginHint.gameObject.SetActive(false);
                return;
            }

            if (soundRandom == null)
                soundRandom = new System.Random(Environment.TickCount ^ GetInstanceID());

            currentSoundTrueBearing = CalculateNorthUpBearing(direction);
            currentSoundError = Mathf.Lerp(
                -soundMaxInaccuracyDegrees,
                soundMaxInaccuracyDegrees,
                (float)soundRandom.NextDouble());
            currentSoundDisplayedBearing = Mathf.Repeat(currentSoundTrueBearing + currentSoundError, 360f);
            soundHintAge = 0f;
            soundHintActive = true;

            if (soundOriginHint == null)
                return;

            soundOriginHint.sprite = soundOriginHintSprite;
            if (soundOriginHintSprite == null)
            {
                soundOriginHint.gameObject.SetActive(false);
                return;
            }

            soundOriginHint.rectTransform.localRotation = Quaternion.Euler(
                0f,
                0f,
                BearingToUiRotation(currentSoundDisplayedBearing) + soundHintNorthRotationOffset);
            SetImageAlpha(soundOriginHint, 1f);
            soundOriginHint.gameObject.SetActive(true);
        }

        public static Vector2 CalculateNorthUpRadarPosition(
            Vector3 worldOffset,
            float worldRange,
            float radarRadius)
        {
            if (worldRange <= 0f || radarRadius <= 0f)
                return Vector2.zero;

            return new Vector2(worldOffset.x, worldOffset.z) * (radarRadius / worldRange);
        }

        public static float CalculateNorthUpBearing(Vector3 worldDirection)
        {
            return Mathf.Repeat(
                Mathf.Atan2(worldDirection.x, worldDirection.z) * Mathf.Rad2Deg,
                360f);
        }

        public static float BearingToUiRotation(float bearingDegrees)
        {
            return -Mathf.Repeat(bearingDegrees, 360f);
        }

        public static bool DidClockwiseSweepCross(
            float previousBearing,
            float currentBearing,
            float targetBearing,
            float sweptDegrees)
        {
            if (sweptDegrees >= 360f)
                return true;

            previousBearing = Mathf.Repeat(previousBearing, 360f);
            currentBearing = Mathf.Repeat(currentBearing, 360f);
            targetBearing = Mathf.Repeat(targetBearing, 360f);

            if (currentBearing >= previousBearing)
                return targetBearing > previousBearing && targetBearing <= currentBearing;

            return targetBearing > previousBearing || targetBearing <= currentBearing;
        }

        private void AdvancePresentation(float unscaledDeltaTime)
        {
            float deltaTime = Mathf.Max(0f, unscaledDeltaTime);
            float previousBearing = sweepBearingDegrees;
            float sweptDegrees = sweepRotationsPerSecond * 360f * deltaTime;
            sweepBearingDegrees = Mathf.Repeat(sweepBearingDegrees + sweptDegrees, 360f);

            if (sweep != null)
                sweep.localRotation = Quaternion.Euler(0f, 0f, -sweepBearingDegrees);

            UpdateContactViews(previousBearing, sweptDegrees, deltaTime);
            UpdateSoundHint(deltaTime);
        }

        private void UpdateZoomInput()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null)
                return;

            float scrollY = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scrollY) <= 0.0001f)
                return;

            // Positive wheel delta zooms in. Exponential scaling gives each
            // wheel movement the same proportional effect at every range.
            float exponent = Mathf.Clamp(-scrollY * zoomSensitivity, -20f, 20f);
            SetCurrentRadarRange(currentRadarRange * Mathf.Exp(exponent), true);
        }

        private void SanitizeRadarRangeConfiguration()
        {
            if (!zoomConfigurationInitialized)
            {
                // The saved scene used the old component default of 500 m. Only
                // that legacy default is promoted; deliberate non-default range
                // values remain intact when the field name migrates.
                if (Mathf.Approximately(defaultRadarRange, LegacyDefaultRadarRange))
                    defaultRadarRange = 3000f;
                zoomConfigurationInitialized = true;
            }

            minimumRadarRange = Mathf.Max(1f, minimumRadarRange);
            maximumRadarRange = Mathf.Max(minimumRadarRange, maximumRadarRange);
            defaultRadarRange = Mathf.Clamp(
                Mathf.Max(1f, defaultRadarRange),
                minimumRadarRange,
                maximumRadarRange);
            zoomSensitivity = Mathf.Max(0.00001f, zoomSensitivity);
        }

        private void SetCurrentRadarRange(float range, bool notifyListeners)
        {
            float clampedRange = Mathf.Clamp(range, minimumRadarRange, maximumRadarRange);
            if (Mathf.Approximately(currentRadarRange, clampedRange))
                return;

            currentRadarRange = clampedRange;
            if (notifyListeners)
                RadarRangeChanged?.Invoke(currentRadarRange);
        }

        private void AddContact(RadarContact contact)
        {
            if (contact == null || contactViews.ContainsKey(contact) || contactsRoot == null)
                return;

            GameObject viewObject = new($"Contact_{contact.name}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            viewObject.layer = contactsRoot.gameObject.layer;
            viewObject.transform.SetParent(contactsRoot, false);

            RectTransform rect = (RectTransform)viewObject.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = Vector2.one * contactDiameter;

            Image image = viewObject.GetComponent<Image>();
            image.sprite = contactSprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.color = GetContactColor(contact);
            SetImageAlpha(image, 0f);
            viewObject.SetActive(false);

            contactViews.Add(contact, new ContactView { Image = image });
        }

        private void RemoveContact(RadarContact contact)
        {
            if (contact == null || !contactViews.TryGetValue(contact, out ContactView view))
                return;

            if (view.Image != null)
                Destroy(view.Image.gameObject);

            contactViews.Remove(contact);
        }

        private void UpdateContactViews(float previousBearing, float sweptDegrees, float deltaTime)
        {
            staleContacts.Clear();

            foreach (KeyValuePair<RadarContact, ContactView> pair in contactViews)
            {
                RadarContact contact = pair.Key;
                ContactView view = pair.Value;
                if (contact == null || view.Image == null)
                {
                    staleContacts.Add(contact);
                    continue;
                }

                bool visible = IsContactVisibleOnRadar(contact);
                view.Image.gameObject.SetActive(visible);
                if (!visible)
                {
                    view.FadeElapsed = float.PositiveInfinity;
                    continue;
                }

                view.Image.rectTransform.anchoredPosition = WorldPositionToRadar(contact.WorldPosition);
                view.Image.color = GetContactColor(contact);

                Vector3 direction = contact.WorldPosition - player.position;
                direction.y = 0f;
                float bearing = CalculateNorthUpBearing(direction);
                if (DidClockwiseSweepCross(
                        previousBearing,
                        sweepBearingDegrees,
                        bearing,
                        sweptDegrees))
                {
                    view.FadeElapsed = 0f;
                }
                else if (!float.IsPositiveInfinity(view.FadeElapsed))
                {
                    view.FadeElapsed += deltaTime;
                }

                float alpha = float.IsPositiveInfinity(view.FadeElapsed)
                    ? 0f
                    : 1f - Mathf.Clamp01(view.FadeElapsed / contactFadeDuration);
                SetImageAlpha(view.Image, alpha);
            }

            for (int index = 0; index < staleContacts.Count; index++)
                RemoveContact(staleContacts[index]);
        }

        private void UpdateSoundHint(float deltaTime)
        {
            if (!soundHintActive)
                return;

            soundHintAge += deltaTime;
            float fadeStart = soundHintVisibleDuration;
            float endTime = fadeStart + soundHintFadeDuration;
            if (soundHintAge >= endTime)
            {
                soundHintActive = false;
                if (soundOriginHint != null)
                    soundOriginHint.gameObject.SetActive(false);
                return;
            }

            if (soundOriginHint == null || soundOriginHintSprite == null)
                return;

            float alpha = soundHintAge <= fadeStart
                ? 1f
                : 1f - Mathf.Clamp01((soundHintAge - fadeStart) / soundHintFadeDuration);
            SetImageAlpha(soundOriginHint, alpha);
        }

        private void ResolveMissingReferences()
        {
            if (radarRoot == null)
                radarRoot = transform as RectTransform;

            if (sweep == null)
                sweep = transform.Find("Sweep") as RectTransform;

            if (contactsRoot == null)
                contactsRoot = transform.Find("Contacts") as RectTransform;

            if (playerCenter == null)
            {
                Transform center = transform.Find("PlayerCenter");
                if (center != null)
                    playerCenter = center.GetComponent<Image>();
            }

            if (soundOriginHint == null)
            {
                Transform hint = transform.Find("SoundOriginHint");
                if (hint != null)
                    soundOriginHint = hint.GetComponent<Image>();
            }
        }

        private void DestroyContactViews()
        {
            foreach (ContactView view in contactViews.Values)
            {
                if (view.Image != null)
                    Destroy(view.Image.gameObject);
            }

            contactViews.Clear();
        }

        private static void SetImageAlpha(Image image, float alpha)
        {
            Color color = image.color;
            color.a = Mathf.Clamp01(alpha);
            image.color = color;
        }

        [ContextMenu("Radar/Test Sound From North")]
        private void TestSoundFromNorth()
        {
            ReportTestSound(Vector3.forward);
        }

        [ContextMenu("Radar/Test Sound From East")]
        private void TestSoundFromEast()
        {
            ReportTestSound(Vector3.right);
        }

        [ContextMenu("Radar/Test Sound From South")]
        private void TestSoundFromSouth()
        {
            ReportTestSound(Vector3.back);
        }

        [ContextMenu("Radar/Test Sound From West")]
        private void TestSoundFromWest()
        {
            ReportTestSound(Vector3.left);
        }

        private void ReportTestSound(Vector3 direction)
        {
            if (player != null)
                ReportSoundArrival(player.position + direction * (currentRadarRange * 0.5f));
        }
    }
}
