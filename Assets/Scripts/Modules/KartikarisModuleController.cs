using System;
using Kartikaris.Audio;
using Kartikaris.Combat;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Kartikaris.Modules
{
    [DisallowMultipleComponent]
    public sealed class KartikarisModuleController : MonoBehaviour
    {
        [Serializable]
        private sealed class ModuleSlot
        {
            [SerializeField] private Transform mount;
            [SerializeField] private KartikarisModuleDefinition equippedModule;
            [Tooltip("Optional specialized behavior. Empty uses the generic one-shot deploy action.")]
            [SerializeField] private KartikarisModuleAction moduleAction;
            [NonSerialized] private KartikarisModuleAction runtimeAction;

            public Transform Mount => mount;
            public KartikarisModuleDefinition EquippedModule => equippedModule;
            public KartikarisModuleAction ModuleAction => moduleAction;
            public KartikarisModuleAction RuntimeAction
            {
                get => runtimeAction;
                set => runtimeAction = value;
            }
        }

        [SerializeField] private ModuleSlot slot1 = new ModuleSlot();
        [SerializeField] private ModuleSlot slot2 = new ModuleSlot();
        [SerializeField] private KartikarisSoundPropagation soundPropagation;
        [SerializeField] private KartikarisCombatAffiliation ownerAffiliation;

        private InputAction deploySlot1Action;
        private InputAction deploySlot2Action;

        public KartikarisModuleAction GetModuleAction(int slotIndex) => GetSlot(slotIndex)?.RuntimeAction;
        public bool IsSlotDeployed(int slotIndex) =>
            GetSlot(slotIndex)?.RuntimeAction != null &&
            GetSlot(slotIndex).RuntimeAction.HasActiveDeployable;

        private void Awake()
        {
            deploySlot1Action = new InputAction("Deploy Module Slot 1", InputActionType.Button, "<Keyboard>/1");
            deploySlot2Action = new InputAction("Deploy Module Slot 2", InputActionType.Button, "<Keyboard>/2");
            if (soundPropagation == null)
                soundPropagation = FindFirstObjectByType<KartikarisSoundPropagation>();
            if (ownerAffiliation == null)
                ownerAffiliation = GetComponentInParent<KartikarisCombatAffiliation>();
            InitializeSlot(slot1, 0);
            InitializeSlot(slot2, 1);
        }

        private void OnEnable()
        {
            deploySlot1Action.Enable();
            deploySlot2Action.Enable();
        }

        private void OnDisable()
        {
            deploySlot1Action.Disable();
            deploySlot2Action.Disable();
        }

        private void OnDestroy()
        {
            deploySlot1Action?.Dispose();
            deploySlot2Action?.Dispose();
        }

        private void Update()
        {
            if (deploySlot1Action.WasPressedThisFrame())
                HandleModuleButtonPressed(0);
            if (deploySlot2Action.WasPressedThisFrame())
                HandleModuleButtonPressed(1);
        }

        public bool TryDeploy(int slotIndex)
        {
            return HandleModuleButtonPressed(slotIndex);
        }

        public bool HandleModuleButtonPressed(int slotIndex)
        {
            ModuleSlot slot = GetSlot(slotIndex);
            return slot?.RuntimeAction != null &&
                slot.RuntimeAction.HandleModuleButtonPressed();
        }

        private void InitializeSlot(ModuleSlot slot, int slotIndex)
        {
            if (slot == null || slot.Mount == null || slot.EquippedModule == null)
                return;

            KartikarisModuleAction action = slot.ModuleAction;
            if (action == null)
                action = gameObject.AddComponent<KartikarisDeployOnceModuleAction>();

            slot.RuntimeAction = action;
            action.InitializeModule(new KartikarisModuleContext(
                slotIndex,
                transform.root,
                ownerAffiliation != null
                    ? ownerAffiliation.Affiliation
                    : KartikarisAffiliation.Neutral,
                slot.Mount,
                slot.EquippedModule,
                soundPropagation));
        }

        private ModuleSlot GetSlot(int slotIndex)
        {
            return slotIndex == 0 ? slot1 : slotIndex == 1 ? slot2 : null;
        }
    }
}
