using Kartikaris.WorldInteraction;
using UnityEngine;

namespace Kartikaris.Player
{
    /// <summary>
    /// Resolves intended mech movement against virtual tree trunks before the
    /// CharacterController moves, preventing its normal tangential slide.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public sealed class MechTreeCrusher : MonoBehaviour
    {
        [Header("Tree Interaction")]
        [SerializeField] private KartikarisTreeInteractionRuntime treeInteraction;
        [SerializeField, Min(0f)] private float crushRadius = 1.25f;
        [SerializeField, Min(0f)] private float impactSkin = 0.03f;

        [Header("Runtime Diagnostics")]
        [SerializeField] private bool impactedTreeLastMove;
        [SerializeField] private int lastCrushedTreeIndex = -1;

        [Header("Debug")]
        [SerializeField] private bool drawCrushGizmos = true;

        private CharacterController characterController;

        public KartikarisTreeInteractionRuntime TreeInteraction => treeInteraction;
        public float CrushRadius => crushRadius;
        public bool ImpactedTreeLastMove => impactedTreeLastMove;
        public int LastCrushedTreeIndex => lastCrushedTreeIndex;

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
            ResolveTreeInteraction();
        }

        private void OnValidate()
        {
            crushRadius = Mathf.Max(0f, crushRadius);
            impactSkin = Mathf.Max(0f, impactSkin);
        }

        private void Reset()
        {
            characterController = GetComponent<CharacterController>();
            ResolveTreeInteraction();
        }

        public bool ResolveHorizontalMovement(Vector3 intendedDisplacement, out Vector3 resolvedDisplacement)
        {
            intendedDisplacement.y = 0f;
            resolvedDisplacement = intendedDisplacement;
            impactedTreeLastMove = false;
            lastCrushedTreeIndex = -1;

            if (intendedDisplacement.sqrMagnitude <= Mathf.Epsilon)
                return false;

            if (treeInteraction == null)
                ResolveTreeInteraction();
            if (treeInteraction == null)
                return false;
            if (!treeInteraction.IsInitialized && !treeInteraction.InitializeRuntime())
                return false;

            if (characterController == null)
                characterController = GetComponent<CharacterController>();

            Vector3 controllerCenter = transform.TransformPoint(characterController.center);
            float scaledHeight = characterController.height * Mathf.Abs(transform.lossyScale.y);
            float moverBottom = controllerCenter.y - scaledHeight * 0.5f;
            float moverTop = controllerCenter.y + scaledHeight * 0.5f;
            Vector3 start = transform.position;
            Vector3 intendedEnd = start + intendedDisplacement;

            if (!treeInteraction.TryCrushFirstBlockingTree(
                    start,
                    intendedEnd,
                    crushRadius,
                    moverBottom,
                    moverTop,
                    intendedDisplacement,
                    impactSkin,
                    out Vector3 allowedEnd,
                    out int treeIndex))
            {
                return false;
            }

            resolvedDisplacement = allowedEnd - start;
            resolvedDisplacement.y = 0f;
            impactedTreeLastMove = true;
            lastCrushedTreeIndex = treeIndex;
            return true;
        }

        private void ResolveTreeInteraction()
        {
            if (treeInteraction == null)
                treeInteraction = FindFirstObjectByType<KartikarisTreeInteractionRuntime>();
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawCrushGizmos)
                return;

            Gizmos.color = new Color(1f, 0.45f, 0.1f, 0.8f);
            Gizmos.DrawWireSphere(transform.position, crushRadius);
        }
    }
}
