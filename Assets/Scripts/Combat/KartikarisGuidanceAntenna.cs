using UnityEngine;

namespace Kartikaris.Combat
{
    [DisallowMultipleComponent]
    public sealed class KartikarisGuidanceAntenna : MonoBehaviour
    {
        [SerializeField] private bool isRaised;
        [SerializeField] private bool signalEnabled = true;

        public bool IsRaised => isRaised;
        public bool HasValidSignal => isRaised && signalEnabled && isActiveAndEnabled;

        public void SetRaised(bool raised) => isRaised = raised;
        public void SetSignalEnabled(bool enabled) => signalEnabled = enabled;
    }
}
