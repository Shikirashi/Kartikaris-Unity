using Kartikaris.Audio;
using UnityEngine;

namespace Kartikaris.WorldInteraction
{
    /// <summary>
    /// Runtime-only, non-physical fall from the authored upright pose to a
    /// terrain-tangent settled pose.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FallingTreeVisual : MonoBehaviour
    {
        private Quaternion startRotation;
        private Quaternion settledRotation;
        private float fallDuration;
        private float elapsed;
        private bool initialized;
        private bool settled;
        private KartikarisSoundPropagation soundPropagation;

        public bool IsSettled => settled;
        public float NormalizedProgress => fallDuration <= 0f ? 1f : Mathf.Clamp01(elapsed / fallDuration);

        public void Initialize(
            Quaternion uprightRotation,
            Quaternion finalRotation,
            float duration,
            KartikarisSoundPropagation propagation)
        {
            startRotation = uprightRotation;
            settledRotation = finalRotation;
            fallDuration = Mathf.Max(0.01f, duration);
            soundPropagation = propagation;
            elapsed = 0f;
            settled = false;
            initialized = true;
            transform.rotation = startRotation;
        }

        private void Update()
        {
            if (!initialized || settled)
                return;

            elapsed += Time.deltaTime;
            float normalized = Mathf.Clamp01(elapsed / fallDuration);
            float eased = normalized * normalized * (3f - 2f * normalized);
            transform.rotation = Quaternion.Slerp(startRotation, settledRotation, eased);

            if (normalized < 1f)
                return;

            settled = true;
            transform.rotation = settledRotation;
            soundPropagation?.PlayTreeImpact(transform.position);
        }
    }
}
