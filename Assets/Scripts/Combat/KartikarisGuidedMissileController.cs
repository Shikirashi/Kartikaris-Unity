using UnityEngine;
using UnityEngine.InputSystem;

namespace Kartikaris.Combat
{
    [DisallowMultipleComponent]
    public sealed class KartikarisGuidedMissileController : MonoBehaviour
    {
        [SerializeField] private KartikarisGuidanceAntenna guidanceAntenna;
        [Tooltip("Optional manually configured Vector2 action. Leave empty and drive SetSteeringInput from another UI/controller.")]
        [SerializeField] private InputActionReference steeringAction;
        [SerializeField] private KartikarisArtilleryProjectile activeMissile;

        private Vector2 externalSteeringInput;

        public KartikarisArtilleryProjectile ActiveMissile => activeMissile;
        public bool HasGuidanceSignal => guidanceAntenna != null && guidanceAntenna.HasValidSignal;
        public Camera ActiveMissileCamera => activeMissile != null ? activeMissile.MissileCamera : null;

        private void OnEnable() => steeringAction?.action.Enable();
        private void OnDisable() => steeringAction?.action.Disable();

        private void Update()
        {
            if (activeMissile == null)
                return;
            Vector2 input = steeringAction != null
                ? steeringAction.action.ReadValue<Vector2>()
                : externalSteeringInput;
            if (HasGuidanceSignal)
                activeMissile.SetGuidanceInput(input);
        }

        public void RegisterMissile(KartikarisArtilleryProjectile missile)
        {
            if (missile != null && missile.SelectedShellType == KartikarisShellType.GuidedMissile)
                activeMissile = missile;
        }

        public void SetSteeringInput(Vector2 input) => externalSteeringInput = Vector2.ClampMagnitude(input, 1f);
    }
}
