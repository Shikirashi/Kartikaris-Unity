using System;
using UnityEngine;

namespace Kartikaris.Combat
{
    [DisallowMultipleComponent]
    public sealed class KartikarisShellLoadout : MonoBehaviour
    {
        public const int RuntimeSlotCount = 6;

        public enum SelectionDirection
        {
            Previous = -1,
            Next = 1
        }

        [Serializable]
        public sealed class ShellSlot
        {
            [SerializeField] private KartikarisShellDefinition definition;
            [SerializeField] private string displayCode = "----";
            [SerializeField, Range(1, RuntimeSlotCount)] private int physicalSlotIndex = 1;

            public KartikarisShellDefinition Definition => definition;
            public string DisplayCode => displayCode;
            public int PhysicalSlotIndex => physicalSlotIndex;

            internal void SetPhysicalSlotIndex(int value)
            {
                physicalSlotIndex = Mathf.Clamp(value, 1, RuntimeSlotCount);
            }
        }

        public readonly struct ShellSelectionChange
        {
            public int PreviousPhysicalSlotIndex { get; }
            public int NewPhysicalSlotIndex { get; }
            public SelectionDirection Direction { get; }

            public ShellSelectionChange(int previousPhysicalSlotIndex,
                int newPhysicalSlotIndex, SelectionDirection direction)
            {
                PreviousPhysicalSlotIndex = previousPhysicalSlotIndex;
                NewPhysicalSlotIndex = newPhysicalSlotIndex;
                Direction = direction;
            }
        }

        [Header("Fixed Loadout (Enemy / Legacy)")]
        [SerializeField] private KartikarisShellDefinition selectedShell;
        [SerializeField] private bool lockOnAwake = true;
        [SerializeField] private bool isLocked;

        [Header("Runtime Player Selection")]
        [SerializeField] private bool runtimeSelectionEnabled;
        [SerializeField] private ShellSlot[] runtimeSlots = new ShellSlot[RuntimeSlotCount];
        [SerializeField, Range(0, RuntimeSlotCount - 1)] private int selectedRuntimeSlot;

        [Header("Propellant Charges")]
        [SerializeField, Range(0, 3)] private int chargeCount = 1;

        private KartikarisShellDefinition lockedShell;

        public KartikarisShellDefinition SelectedShell => runtimeSelectionEnabled
            ? GetRuntimeSlot(selectedRuntimeSlot)?.Definition
            : isLocked ? lockedShell : selectedShell;
        public KartikarisShellType? SelectedShellType => SelectedShell != null ? SelectedShell.ShellType : null;
        public bool IsLocked => isLocked;
        public bool RuntimeSelectionEnabled => runtimeSelectionEnabled;
        public int SelectedPhysicalSlotIndex => runtimeSelectionEnabled &&
            GetRuntimeSlot(selectedRuntimeSlot) != null
                ? GetRuntimeSlot(selectedRuntimeSlot).PhysicalSlotIndex
                : 0;
        public string SelectedDisplayCode => FormatDisplayCode(
            runtimeSelectionEnabled ? GetRuntimeSlot(selectedRuntimeSlot)?.DisplayCode : "----");
        public int ChargeCount => chargeCount;
        public bool HasUsableCharge => !runtimeSelectionEnabled || chargeCount > 0;

        public event Action<ShellSelectionChange> ShellSelectionChanged;
        public event Action<int, int> ChargeCountChanged;
        public event Action ZeroChargeFireRejected;

        private void Awake()
        {
            EnsureRuntimeSlots();
            if (runtimeSelectionEnabled)
                SelectInitialAvailableSlot();
            if (lockOnAwake)
                LockSelection();
        }

        private void OnValidate()
        {
            EnsureRuntimeSlots();
            selectedRuntimeSlot = Mathf.Clamp(selectedRuntimeSlot, 0, RuntimeSlotCount - 1);
            chargeCount = Mathf.Clamp(chargeCount, 0, 3);
        }

        public void LockSelection()
        {
            // Runtime Player selection intentionally supersedes the legacy lock.
            if (runtimeSelectionEnabled)
                return;
            if (isLocked)
                return;

            lockedShell = selectedShell;
            isLocked = true;
        }

        public bool TrySetBeforeLock(KartikarisShellDefinition definition)
        {
            if (isLocked)
                return false;

            selectedShell = definition;
            return true;
        }

        public bool SelectPreviousAvailable()
        {
            return SelectAvailable(SelectionDirection.Previous);
        }

        public bool SelectNextAvailable()
        {
            return SelectAvailable(SelectionDirection.Next);
        }

        public bool SetChargeCount(int value)
        {
            int clamped = Mathf.Clamp(value, 0, 3);
            if (!runtimeSelectionEnabled || clamped == chargeCount)
                return false;

            int previous = chargeCount;
            chargeCount = clamped;
            ChargeCountChanged?.Invoke(previous, chargeCount);
            return true;
        }

        public bool DecreaseCharge()
        {
            return SetChargeCount(chargeCount - 1);
        }

        public bool IncreaseCharge()
        {
            return SetChargeCount(chargeCount + 1);
        }

        /// <summary>
        /// Called at the very start of a fire attempt. Charges currently gate
        /// firing but deliberately do not alter velocity or consume propellant.
        /// </summary>
        public bool TryAuthorizeFire()
        {
            if (HasUsableCharge)
                return true;

            ZeroChargeFireRejected?.Invoke();
            return false;
        }

        /// <summary>Future charge-ballistics seam; first pass is intentionally 1:1.</summary>
        public float ApplyChargeMuzzleVelocity(float baseVelocity)
        {
            return baseVelocity;
        }

        public static string FormatDisplayCode(string value)
        {
            string safe = string.IsNullOrEmpty(value) ? "----" : value.ToUpperInvariant();
            return safe.Length >= 4 ? safe.Substring(0, 4) : safe.PadRight(4);
        }

        private bool SelectAvailable(SelectionDirection direction)
        {
            if (!runtimeSelectionEnabled)
                return false;

            EnsureRuntimeSlots();
            int step = (int)direction;
            int previousIndex = selectedRuntimeSlot;
            for (int offset = 1; offset <= RuntimeSlotCount; offset++)
            {
                int candidate = (selectedRuntimeSlot + step * offset + RuntimeSlotCount * 2) %
                    RuntimeSlotCount;
                ShellSlot slot = runtimeSlots[candidate];
                if (slot == null || slot.Definition == null)
                    continue;
                if (candidate == previousIndex)
                    return false;

                selectedRuntimeSlot = candidate;
                ShellSelectionChanged?.Invoke(new ShellSelectionChange(
                    runtimeSlots[previousIndex]?.PhysicalSlotIndex ?? previousIndex + 1,
                    slot.PhysicalSlotIndex,
                    direction));
                return true;
            }

            return false;
        }

        private void SelectInitialAvailableSlot()
        {
            if (GetRuntimeSlot(selectedRuntimeSlot)?.Definition != null)
                return;

            for (int index = 0; index < RuntimeSlotCount; index++)
            {
                if (runtimeSlots[index]?.Definition != null)
                {
                    selectedRuntimeSlot = index;
                    return;
                }
            }
        }

        private ShellSlot GetRuntimeSlot(int index)
        {
            return runtimeSlots != null && index >= 0 && index < runtimeSlots.Length
                ? runtimeSlots[index]
                : null;
        }

        private void EnsureRuntimeSlots()
        {
            if (runtimeSlots == null || runtimeSlots.Length != RuntimeSlotCount)
            {
                ShellSlot[] resized = new ShellSlot[RuntimeSlotCount];
                if (runtimeSlots != null)
                    Array.Copy(runtimeSlots, resized, Mathf.Min(runtimeSlots.Length, resized.Length));
                runtimeSlots = resized;
            }

            for (int index = 0; index < runtimeSlots.Length; index++)
            {
                if (runtimeSlots[index] == null)
                    runtimeSlots[index] = new ShellSlot();
                runtimeSlots[index].SetPhysicalSlotIndex(index + 1);
            }
        }
    }
}
