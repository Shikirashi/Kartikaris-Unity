using UnityEngine;

namespace Kartikaris.Modules
{
    public enum KartikarisModuleType
    {
        SoundProbe,
        SoundDecoy,
        EmpBlaster,
        TrackingDrone
    }

    [CreateAssetMenu(menuName = "Kartikaris/Modules/Module Definition", fileName = "ModuleDefinition")]
    public sealed class KartikarisModuleDefinition : ScriptableObject
    {
        [SerializeField] private KartikarisModuleType moduleType = KartikarisModuleType.SoundProbe;
        [SerializeField] private GameObject deployablePrefab;

        public KartikarisModuleType ModuleType => moduleType;
        public GameObject DeployablePrefab => deployablePrefab;
    }
}
