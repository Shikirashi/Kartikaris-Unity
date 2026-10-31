using System;
using System.Text;
using Kartikaris.Combat;
using Kartikaris.Player;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace Kartikaris.Cockpit
{
    [DisallowMultipleComponent]
    public sealed class KartikarisArtilleryComputer : MonoBehaviour
    {
        [Header("Gameplay Sources")]
        [SerializeField] private KartikarisShellLoadout shellLoadout;
        [SerializeField] private KartikarisCannonWeapon weapon;
        [SerializeField] private KartikarisFireControlPredictor predictor;
        [SerializeField] private MechFirstPersonController playerController;

        [Header("Presentation")]
        [SerializeField] private TMP_Text monitorText;
        [SerializeField] private Renderer[] chargeRenderers = new Renderer[3];
        [SerializeField, Min(0.02f)] private float monitorRefreshInterval = 0.05f;
        [SerializeField] private string filledChargeGlyph = "#";
        [SerializeField] private string emptyChargeGlyph = "   ";
        [SerializeField] private string filledModeGlyph = "#";
        [SerializeField] private string emptyModeGlyph = ".";

        [Header("Future Loading Animation References (Not Animated Yet)")]
        [SerializeField] private Transform ramrodBone;
        [SerializeField] private Transform chargesBone;
        [SerializeField] private Transform coverpanelBone;
        [SerializeField] private Transform revolverBone;
        [SerializeField] private Transform[] shellBones = new Transform[6];

        [Header("Inspector Hooks")]
        [SerializeField] private UnityEvent shellSelectionChanged;
        [SerializeField] private UnityEvent chargeCountChanged;
        [SerializeField] private UnityEvent validShotFired;
        [SerializeField] private UnityEvent zeroChargeFireRejected;

        private readonly StringBuilder monitorBuilder = new StringBuilder(160);
        private float nextMonitorRefreshTime;
        private string lastMonitorText;
        private bool monitorDirty = true;

        public KartikarisShellLoadout ShellLoadout => shellLoadout;
        public int ChargeCount => shellLoadout != null ? shellLoadout.ChargeCount : 0;
        public Transform RamrodBone => ramrodBone;
        public Transform ChargesBone => chargesBone;
        public Transform CoverpanelBone => coverpanelBone;
        public Transform RevolverBone => revolverBone;
        public Transform[] ShellBones => shellBones;

        public event Action<KartikarisShellLoadout.ShellSelectionChange> ShellSelectionChanged;
        public event Action<int, int> ChargeCountChanged;
        public event Action<KartikarisArtilleryProjectile> ValidShotFired;
        public event Action ZeroChargeFireRejected;
        public event Action<KartikarisArtilleryComputerButton.ButtonAction, Transform> ButtonPressed;

        private void Awake()
        {
            if (monitorText != null)
                monitorText.richText = false;
            ApplyChargeVisibility();
            RefreshMonitor();
        }

        private void OnEnable()
        {
            Subscribe();
            monitorDirty = true;
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void OnValidate()
        {
            monitorRefreshInterval = Mathf.Max(0.02f, monitorRefreshInterval);
            EnsureReferenceArraySizes();
            if (monitorText != null)
                monitorText.richText = false;
        }

        private void LateUpdate()
        {
            if (!monitorDirty && Time.unscaledTime < nextMonitorRefreshTime)
                return;
            RefreshMonitor();
        }

        public bool SelectPreviousShell()
        {
            return shellLoadout != null && shellLoadout.SelectPreviousAvailable();
        }

        public bool SelectNextShell()
        {
            return shellLoadout != null && shellLoadout.SelectNextAvailable();
        }

        public bool DecreaseCharge()
        {
            return shellLoadout != null && shellLoadout.DecreaseCharge();
        }

        public bool IncreaseCharge()
        {
            return shellLoadout != null && shellLoadout.IncreaseCharge();
        }

        internal void NotifyButtonPressed(
            KartikarisArtilleryComputerButton.ButtonAction action,
            Transform buttonBone)
        {
            ButtonPressed?.Invoke(action, buttonBone);
        }

        private void Subscribe()
        {
            if (shellLoadout != null)
            {
                shellLoadout.ShellSelectionChanged += OnShellSelectionChanged;
                shellLoadout.ChargeCountChanged += OnChargeCountChanged;
                shellLoadout.ZeroChargeFireRejected += OnZeroChargeFireRejected;
            }
            if (weapon != null)
                weapon.Fired += OnWeaponFired;
        }

        private void Unsubscribe()
        {
            if (shellLoadout != null)
            {
                shellLoadout.ShellSelectionChanged -= OnShellSelectionChanged;
                shellLoadout.ChargeCountChanged -= OnChargeCountChanged;
                shellLoadout.ZeroChargeFireRejected -= OnZeroChargeFireRejected;
            }
            if (weapon != null)
                weapon.Fired -= OnWeaponFired;
        }

        private void OnShellSelectionChanged(KartikarisShellLoadout.ShellSelectionChange change)
        {
            monitorDirty = true;
            ShellSelectionChanged?.Invoke(change);
            shellSelectionChanged?.Invoke();
        }

        private void OnChargeCountChanged(int previous, int current)
        {
            ApplyChargeVisibility();
            monitorDirty = true;
            ChargeCountChanged?.Invoke(previous, current);
            chargeCountChanged?.Invoke();
        }

        private void OnWeaponFired(KartikarisArtilleryProjectile projectile)
        {
            ValidShotFired?.Invoke(projectile);
            validShotFired?.Invoke();
        }

        private void OnZeroChargeFireRejected()
        {
            ZeroChargeFireRejected?.Invoke();
            zeroChargeFireRejected?.Invoke();
        }

        private void ApplyChargeVisibility()
        {
            int count = ChargeCount;
            if (chargeRenderers == null)
                return;
            for (int index = 0; index < chargeRenderers.Length; index++)
            {
                if (chargeRenderers[index] != null)
                    chargeRenderers[index].enabled = index < count;
            }
        }

        private void RefreshMonitor()
        {
            nextMonitorRefreshTime = Time.unscaledTime + monitorRefreshInterval;
            monitorDirty = false;
            if (monitorText == null)
                return;

            monitorBuilder.Clear();
            monitorBuilder.Append("SHEL: [");
            monitorBuilder.Append(shellLoadout != null
                ? shellLoadout.SelectedDisplayCode
                : "----");
            monitorBuilder.Append("]\nCHRG: ");
            int charge = ChargeCount;
            for (int index = 0; index < 3; index++)
            {
                if (index > 0)
                    monitorBuilder.Append(' ');
                monitorBuilder.Append('[');
                monitorBuilder.Append(index < charge ? filledChargeGlyph : emptyChargeGlyph);
                monitorBuilder.Append(']');
            }

            KartikarisFireControlPredictor.Prediction prediction = default;
            bool hasPrediction = charge > 0 && predictor != null &&
                predictor.TryPredict(out prediction);
            monitorBuilder.Append("\nDIST: ");
            if (hasPrediction)
            {
                Vector3 referencePosition = playerController != null
                    ? playerController.transform.position
                    : weapon != null && weapon.FirePoint != null
                        ? weapon.FirePoint.position
                        : transform.position;
                Vector2 horizontalDelta = new Vector2(
                    prediction.Position.x - referencePosition.x,
                    prediction.Position.z - referencePosition.z);
                monitorBuilder.Append(Mathf.RoundToInt(horizontalDelta.magnitude));
                monitorBuilder.Append('m');
            }
            else
            {
                monitorBuilder.Append("----m");
            }

            monitorBuilder.Append("\nFLGT: ");
            if (hasPrediction)
            {
                int totalMilliseconds = Mathf.Max(0,
                    Mathf.RoundToInt(prediction.FlightTime * 1000f));
                int seconds = totalMilliseconds / 1000;
                int milliseconds = totalMilliseconds % 1000;
                monitorBuilder.Append(seconds);
                monitorBuilder.Append(':');
                if (milliseconds < 100)
                    monitorBuilder.Append('0');
                if (milliseconds < 10)
                    monitorBuilder.Append('0');
                monitorBuilder.Append(milliseconds);
            }
            else
            {
                monitorBuilder.Append("--:---");
            }

            int filledCells = playerController != null
                ? Mathf.Clamp(Mathf.RoundToInt(playerController.OperatingModeProgress01 * 10f), 0, 10)
                : 0;
            monitorBuilder.Append('\n');
            for (int index = 0; index < 10; index++)
            {
                if (index > 0)
                    monitorBuilder.Append(' ');
                monitorBuilder.Append(index < filledCells ? filledModeGlyph : emptyModeGlyph);
            }

            string next = monitorBuilder.ToString();
            if (!string.Equals(lastMonitorText, next, StringComparison.Ordinal))
            {
                lastMonitorText = next;
                monitorText.text = next;
            }
        }

        private void EnsureReferenceArraySizes()
        {
            if (chargeRenderers == null || chargeRenderers.Length != 3)
                Array.Resize(ref chargeRenderers, 3);
            if (shellBones == null || shellBones.Length != 6)
                Array.Resize(ref shellBones, 6);
        }
    }
}
