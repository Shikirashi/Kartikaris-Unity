using UnityEngine;
using UnityEngine.InputSystem;

namespace Kartikaris.Cockpit
{
    [DisallowMultipleComponent]
    public sealed class KartikarisCockpitInteractionController : MonoBehaviour
    {
        [Header("Gaze Ray")]
        [SerializeField] private Camera interactionCamera;
        [SerializeField, Min(0.1f)] private float interactionDistance = 2f;
        [SerializeField] private LayerMask interactionLayers = 1 << 8;
        [SerializeField] private QueryTriggerInteraction triggerInteraction =
            QueryTriggerInteraction.Collide;

        [Header("Input")]
        [Tooltip("Optional New Input System action. Keyboard E is the fallback when unassigned.")]
        [SerializeField] private InputActionReference interactAction;

        [Header("Runtime Diagnostics")]
        [SerializeField] private KartikarisCockpitInteractable focusedInteractable;
        [SerializeField] private string focusedObjectName;
        [SerializeField] private float focusedDistance;

        private readonly RaycastHit[] hits = new RaycastHit[12];
        private bool enabledAssignedAction;

        public Camera InteractionCamera => interactionCamera;
        public float InteractionDistance => interactionDistance;
        public LayerMask InteractionLayers => interactionLayers;
        public KartikarisCockpitInteractable FocusedInteractable => focusedInteractable;

        private void Awake()
        {
            if (interactionCamera == null)
                interactionCamera = GetComponent<Camera>();
        }

        private void OnEnable()
        {
            if (interactAction != null && interactAction.action != null &&
                !interactAction.action.enabled)
            {
                interactAction.action.Enable();
                enabledAssignedAction = true;
            }
        }

        private void OnDisable()
        {
            SetFocus(null, 0f);
            if (enabledAssignedAction && interactAction != null && interactAction.action != null)
                interactAction.action.Disable();
            enabledAssignedAction = false;
        }

        private void OnValidate()
        {
            interactionDistance = Mathf.Max(0.1f, interactionDistance);
        }

        private void Update()
        {
            RefreshFocus();
            if (WasInteractPressed())
                focusedInteractable?.TryInteract();
        }

        private void RefreshFocus()
        {
            if (interactionCamera == null)
            {
                SetFocus(null, 0f);
                return;
            }

            Ray ray = new Ray(interactionCamera.transform.position,
                interactionCamera.transform.forward);
            int count = Physics.RaycastNonAlloc(ray, hits, interactionDistance,
                interactionLayers, triggerInteraction);
            KartikarisCockpitInteractable nearest = null;
            float nearestDistance = float.PositiveInfinity;
            for (int index = 0; index < count; index++)
            {
                RaycastHit hit = hits[index];
                if (hit.collider == null || hit.distance >= nearestDistance)
                    continue;

                KartikarisCockpitInteractable candidate =
                    hit.collider.GetComponentInParent<KartikarisCockpitInteractable>();
                if (candidate == null || !candidate.InteractionEnabled)
                    continue;

                nearest = candidate;
                nearestDistance = hit.distance;
            }

            SetFocus(nearest, nearest != null ? nearestDistance : 0f);
        }

        private bool WasInteractPressed()
        {
            if (interactAction != null && interactAction.action != null)
                return interactAction.action.WasPressedThisFrame();
            return Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
        }

        private void SetFocus(KartikarisCockpitInteractable next, float distance)
        {
            if (focusedInteractable != next)
            {
                focusedInteractable?.SetFocused(false);
                focusedInteractable = next;
                focusedInteractable?.SetFocused(true);
            }

            focusedDistance = distance;
            focusedObjectName = focusedInteractable != null
                ? focusedInteractable.name
                : string.Empty;
        }
    }
}
