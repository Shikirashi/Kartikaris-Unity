using UnityEngine;

namespace Kartikaris.Modules
{
    public enum KartikarisTrackingDroneModuleState
    {
        Available,
        Outbound,
        Tracking,
        Returning,
        Cooldown,
        Lost
    }

    public sealed class KartikarisTrackingDroneModuleAction : KartikarisModuleAction
    {
        [Header("Replacement Cooldowns")]
        [SerializeField, Min(0f)] private float successfulReturnCooldown = 120f;
        [SerializeField, Min(0f)] private float lossCooldown = 120f;

        [Header("Runtime Diagnostics")]
        [SerializeField] private KartikarisTrackingDrone activeDrone;
        [SerializeField] private KartikarisTrackingDroneModuleState state =
            KartikarisTrackingDroneModuleState.Available;
        [SerializeField] private float nextAvailableTime;
        [SerializeField] private float cooldownRemaining;

        public KartikarisTrackingDrone ActiveDrone => activeDrone;
        public override bool HasActiveDeployable =>
            activeDrone != null && !activeDrone.IsDestroyed;
        public KartikarisTrackingDroneModuleState State => state;
        public float BatteryRemaining => activeDrone != null
            ? activeDrone.BatteryRemaining
            : 0f;
        public float CooldownRemaining => Mathf.Max(0f, nextAvailableTime - Time.time);
        public float DistanceTravelledOutbound => activeDrone != null
            ? activeDrone.DistanceTravelledOutbound
            : 0f;
        public Kartikaris.Combat.KartikarisMechHealth CurrentTrackedTarget =>
            activeDrone != null ? activeDrone.TrackedTarget : null;
        public float CurrentSpeed => activeDrone != null ? activeDrone.CurrentSpeed : 0f;

        private void OnValidate()
        {
            successfulReturnCooldown = Mathf.Max(0f, successfulReturnCooldown);
            lossCooldown = Mathf.Max(0f, lossCooldown);
        }

        private void Update()
        {
            cooldownRemaining = CooldownRemaining;
            if (activeDrone != null)
            {
                state = activeDrone.State switch
                {
                    KartikarisTrackingDroneFlightState.Outbound =>
                        KartikarisTrackingDroneModuleState.Outbound,
                    KartikarisTrackingDroneFlightState.Tracking =>
                        KartikarisTrackingDroneModuleState.Tracking,
                    _ => KartikarisTrackingDroneModuleState.Returning
                };
                return;
            }

            if ((state == KartikarisTrackingDroneModuleState.Cooldown ||
                    state == KartikarisTrackingDroneModuleState.Lost) &&
                Time.time >= nextAvailableTime)
            {
                state = KartikarisTrackingDroneModuleState.Available;
            }
        }

        public override bool HandleModuleButtonPressed()
        {
            if (activeDrone != null)
                return activeDrone.Recall();
            if (state != KartikarisTrackingDroneModuleState.Available)
                return false;
            return DeployDrone();
        }

        private bool DeployDrone()
        {
            GameObject instance = InstantiateDefinitionPrefab();
            if (instance == null)
                return false;

            activeDrone = instance.GetComponent<KartikarisTrackingDrone>();
            if (activeDrone == null)
            {
                Debug.LogError(
                    "A Tracking Drone module prefab requires KartikarisTrackingDrone.",
                    instance);
                return false;
            }

            activeDrone.Destroyed += OnDroneDestroyed;
            activeDrone.InitializeDeployable(Context.Owner, Context.OwnerAffiliation);
            state = KartikarisTrackingDroneModuleState.Outbound;
            return true;
        }

        private void OnDroneDestroyed(
            KartikarisDeployable deployable,
            KartikarisDeployableDestroyReason reason)
        {
            deployable.Destroyed -= OnDroneDestroyed;
            activeDrone = null;
            bool recovered = reason == KartikarisDeployableDestroyReason.Recovered;
            state = recovered
                ? KartikarisTrackingDroneModuleState.Cooldown
                : KartikarisTrackingDroneModuleState.Lost;
            nextAvailableTime = Time.time +
                (recovered ? successfulReturnCooldown : lossCooldown);
        }

        private void OnDestroy()
        {
            if (activeDrone != null)
                activeDrone.DestroyDeployable(KartikarisDeployableDestroyReason.OwnerLost);
        }
    }
}
