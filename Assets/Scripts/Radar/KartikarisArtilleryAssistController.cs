using Kartikaris.Combat;
using Kartikaris.Player;
using TMPro;
using UnityEngine;

namespace Kartikaris.Radar
{
    /// <summary>
    /// Closing IN-style two-image artillery assist. The heading parent rotates
    /// on the north-up radar and its child slides outward to the predicted point.
    /// No UI hierarchy or endpoint contact is created at runtime.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KartikarisArtilleryAssistController : MonoBehaviour
    {
        [Header("Explicit References")]
        [SerializeField] private MechFirstPersonController playerController;
        [SerializeField] private KartikarisFireControlPredictor predictor;
        [SerializeField] private KartikarisRadarController radar;
        [SerializeField] private RectTransform headingAssist;
        [Tooltip("Assign the imported elevation-assist Image RectTransform as a child of Heading Assist.")]
        [SerializeField] private RectTransform elevationAssist;
        [Tooltip("Optional. Direct child named ImpactEstimate is resolved automatically when this is empty.")]
        [SerializeField] private TMP_Text impactEstimate;

        [Header("Presentation Tuning")]
        [Tooltip("Optional legacy behavior. Leave disabled to show valid Standard HE/Airburst assistance in every Player operating state.")]
        [SerializeField] private bool requireFireMode;
        [SerializeField] private float headingRotationOffset;
        [SerializeField] private float elevationLocalXOffset;

        [Header("Runtime Diagnostics")]
        [SerializeField] private bool assistVisible;
        [SerializeField] private Vector3 predictedWorldPosition;
        [SerializeField] private Vector2 predictedRadarPosition;
        [SerializeField] private float displayedBearing;
        [SerializeField] private float displayedRadius;
        [SerializeField] private bool impactCountdownActive;
        [SerializeField] private float impactEstimateSeconds;
        [SerializeField] private KartikarisArtilleryProjectile trackedProjectile;

        private KartikarisCannonWeapon subscribedWeapon;
        private float estimatedImpactTime;

        public bool IsVisible => assistVisible;
        public Vector3 PredictedWorldPosition => predictedWorldPosition;
        public Vector2 PredictedRadarPosition => predictedRadarPosition;
        public bool IsImpactCountdownActive => impactCountdownActive;
        public float ImpactEstimateSeconds => impactEstimateSeconds;

        private void Awake()
        {
            if (playerController == null)
                playerController = GetComponentInParent<MechFirstPersonController>();
            ResolveImpactEstimateReference();
            SetVisible(!requireFireMode);
            SetImpactEstimateVisible(false);
        }

        private void OnEnable()
        {
            SubscribeToWeapon();
        }

        private void Start()
        {
            SubscribeToWeapon();
        }

        private void LateUpdate()
        {
            RefreshPresentation();
            UpdateImpactCountdown();
        }

        private void OnDisable()
        {
            UnsubscribeFromWeapon();
            SetVisible(false);
            ClearImpactCountdown();
        }

        public void RefreshPresentation()
        {
            if (playerController == null ||
                (requireFireMode && !playerController.IsFireMode) ||
                predictor == null || radar == null ||
                headingAssist == null || elevationAssist == null ||
                !predictor.TryPredict(out KartikarisFireControlPredictor.Prediction prediction))
            {
                SetVisible(false);
                return;
            }

            predictedWorldPosition = prediction.Position;
            if (!radar.IsWorldPositionWithinRange(predictedWorldPosition))
            {
                SetVisible(false);
                return;
            }

            predictedRadarPosition = radar.WorldPositionToRadar(prediction.Position);
            float radius = predictedRadarPosition.magnitude;

            displayedBearing = KartikarisRadarController.CalculateNorthUpBearing(
                predictor.PredictionHeadingDirection);
            displayedRadius = radius;

            headingAssist.localRotation = Quaternion.Euler(
                0f,
                0f,
                KartikarisRadarController.BearingToUiRotation(displayedBearing) +
                    headingRotationOffset);
            elevationAssist.anchoredPosition = new Vector2(
                elevationLocalXOffset,
                displayedRadius);
            SetVisible(true);
        }

        private void SetVisible(bool visible)
        {
            assistVisible = visible;
            if (headingAssist != null && headingAssist.gameObject.activeSelf != visible)
                headingAssist.gameObject.SetActive(visible);
            if (elevationAssist != null && elevationAssist.gameObject.activeSelf != visible)
                elevationAssist.gameObject.SetActive(visible);
        }

        private void ResolveImpactEstimateReference()
        {
            if (impactEstimate != null)
            {
                impactEstimate.raycastTarget = false;
                return;
            }

            Transform child = transform.Find("ImpactEstimate");
            if (child != null)
                impactEstimate = child.GetComponent<TMP_Text>();
            if (impactEstimate != null)
                impactEstimate.raycastTarget = false;
        }

        private void SubscribeToWeapon()
        {
            ResolveImpactEstimateReference();
            KartikarisCannonWeapon weapon = predictor != null
                ? predictor.Weapon
                : playerController != null
                    ? playerController.CannonWeapon
                    : null;
            if (subscribedWeapon == weapon)
                return;

            UnsubscribeFromWeapon();
            subscribedWeapon = weapon;
            if (subscribedWeapon != null)
                subscribedWeapon.Fired += OnWeaponFired;
        }

        private void UnsubscribeFromWeapon()
        {
            if (subscribedWeapon != null)
                subscribedWeapon.Fired -= OnWeaponFired;
            subscribedWeapon = null;
        }

        private void OnWeaponFired(KartikarisArtilleryProjectile projectile)
        {
            trackedProjectile = projectile;
            if (trackedProjectile == null || predictor == null ||
                !predictor.TryPredict(out KartikarisFireControlPredictor.Prediction prediction))
            {
                ClearImpactCountdown();
                return;
            }

            impactEstimateSeconds = Mathf.Max(0f, prediction.FlightTime);
            estimatedImpactTime = Time.time + impactEstimateSeconds;
            impactCountdownActive = true;
            RefreshImpactEstimateText();
        }

        private void UpdateImpactCountdown()
        {
            if (!impactCountdownActive)
            {
                SetImpactEstimateVisible(false);
                return;
            }

            if (trackedProjectile == null)
            {
                ClearImpactCountdown();
                return;
            }

            impactEstimateSeconds = Mathf.Max(0f, estimatedImpactTime - Time.time);
            RefreshImpactEstimateText();
        }

        private void RefreshImpactEstimateText()
        {
            if (impactEstimate == null)
                return;

            impactEstimate.SetText("Impact: {0:2}s", impactEstimateSeconds);
            SetImpactEstimateVisible(true);
        }

        private void ClearImpactCountdown()
        {
            impactCountdownActive = false;
            impactEstimateSeconds = 0f;
            estimatedImpactTime = 0f;
            trackedProjectile = null;
            SetImpactEstimateVisible(false);
        }

        private void SetImpactEstimateVisible(bool visible)
        {
            if (impactEstimate != null && impactEstimate.gameObject.activeSelf != visible)
                impactEstimate.gameObject.SetActive(visible);
        }
    }
}
