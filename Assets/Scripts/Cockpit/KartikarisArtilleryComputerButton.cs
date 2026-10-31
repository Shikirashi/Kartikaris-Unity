using System;
using UnityEngine;
using UnityEngine.Events;

namespace Kartikaris.Cockpit
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(KartikarisCockpitInteractable))]
    public sealed class KartikarisArtilleryComputerButton : MonoBehaviour
    {
        public enum ButtonAction
        {
            PreviousShell,
            NextShell,
            ChargeLess,
            ChargeMore
        }

        [SerializeField] private ButtonAction action;
        [SerializeField] private KartikarisArtilleryComputer artilleryComputer;
        [Tooltip("Explicit hook for a future press animation; this pass never moves the bone.")]
        [SerializeField] private Transform buttonBone;
        [Tooltip("Optional Animator hook. Leave the trigger empty to perform no animation.")]
        [SerializeField] private Animator animator;
        [SerializeField] private string successfulPressTrigger;
        [SerializeField] private UnityEvent successfulPress;

        private KartikarisCockpitInteractable interactable;

        public ButtonAction Action => action;
        public Transform ButtonBone => buttonBone;

        public event Action<KartikarisArtilleryComputerButton> SuccessfullyPressed;

        private void Awake()
        {
            interactable = GetComponent<KartikarisCockpitInteractable>();
        }

        private void OnEnable()
        {
            if (interactable == null)
                interactable = GetComponent<KartikarisCockpitInteractable>();
            if (interactable != null)
                interactable.Interacted += OnInteracted;
        }

        private void OnDisable()
        {
            if (interactable != null)
                interactable.Interacted -= OnInteracted;
        }

        private void OnInteracted()
        {
            if (artilleryComputer == null)
                return;

            switch (action)
            {
                case ButtonAction.PreviousShell:
                    artilleryComputer.SelectPreviousShell();
                    break;
                case ButtonAction.NextShell:
                    artilleryComputer.SelectNextShell();
                    break;
                case ButtonAction.ChargeLess:
                    artilleryComputer.DecreaseCharge();
                    break;
                case ButtonAction.ChargeMore:
                    artilleryComputer.IncreaseCharge();
                    break;
            }

            // The interaction itself was accepted even when the selected value
            // is already at a boundary. Animation remains an external hook.
            if (animator != null && !string.IsNullOrEmpty(successfulPressTrigger))
                animator.SetTrigger(successfulPressTrigger);
            artilleryComputer.NotifyButtonPressed(action, buttonBone);
            SuccessfullyPressed?.Invoke(this);
            successfulPress?.Invoke();
        }
    }
}
