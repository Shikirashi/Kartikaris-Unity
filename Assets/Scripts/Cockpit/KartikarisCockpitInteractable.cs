using System;
using UnityEngine;
using UnityEngine.Events;

namespace Kartikaris.Cockpit
{
    [DisallowMultipleComponent]
    public sealed class KartikarisCockpitInteractable : MonoBehaviour
    {
        [SerializeField] private bool interactionEnabled = true;
        [SerializeField] private KartikarisCockpitOutline outlineTarget;
        [SerializeField] private UnityEvent focusEntered;
        [SerializeField] private UnityEvent focusExited;
        [SerializeField] private UnityEvent interacted;

        private bool isFocused;

        public bool InteractionEnabled => interactionEnabled && isActiveAndEnabled;
        public bool IsFocused => isFocused;

        public event Action FocusEntered;
        public event Action FocusExited;
        public event Action Interacted;

        private void Awake()
        {
            if (outlineTarget == null)
                outlineTarget = GetComponent<KartikarisCockpitOutline>();
            outlineTarget?.SetOutlined(false);
        }

        private void OnDisable()
        {
            SetFocused(false);
        }

        public void SetInteractionEnabled(bool value)
        {
            interactionEnabled = value;
            if (!value)
                SetFocused(false);
        }

        public bool TryInteract()
        {
            if (!InteractionEnabled || !isFocused)
                return false;

            Interacted?.Invoke();
            interacted?.Invoke();
            return true;
        }

        internal void SetFocused(bool value)
        {
            bool next = value && InteractionEnabled;
            if (next == isFocused)
                return;

            isFocused = next;
            outlineTarget?.SetOutlined(isFocused);
            if (isFocused)
            {
                FocusEntered?.Invoke();
                focusEntered?.Invoke();
            }
            else
            {
                FocusExited?.Invoke();
                focusExited?.Invoke();
            }
        }
    }
}
