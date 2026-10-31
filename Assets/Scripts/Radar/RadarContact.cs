using System;
using System.Collections.Generic;
using UnityEngine;

namespace Kartikaris.Radar
{
    public enum RadarAffiliation
    {
        Friendly,
        Enemy
    }

    public enum RadarContactCategory
    {
        Unspecified,
        Mech,
        Camera,
        Drone
    }

    /// <summary>
    /// Declares an object as a potential radar contact. Friendly contacts are
    /// always eligible; enemy contacts only become eligible after being spotted.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RadarContact : MonoBehaviour
    {
        private static readonly HashSet<RadarContact> ActiveContactSet = new();

        [SerializeField] private Transform positionSource;
        [SerializeField] private RadarAffiliation affiliation = RadarAffiliation.Enemy;
        [SerializeField] private RadarContactCategory category = RadarContactCategory.Unspecified;
        [SerializeField] private bool spotted;
        [SerializeField] private bool contactActive = true;

        private readonly HashSet<int> spotSourceIds = new();

        internal static IEnumerable<RadarContact> ActiveContacts => ActiveContactSet;
        internal static event Action<RadarContact> ContactAdded;
        internal static event Action<RadarContact> ContactRemoved;

        public event Action<RadarContact> StateChanged;

        public Transform PositionSource => positionSource != null ? positionSource : transform;
        public Vector3 WorldPosition => PositionSource.position;
        public RadarAffiliation Affiliation => affiliation;
        public RadarContactCategory Category => category;
        public bool IsSpotted => spotted || spotSourceIds.Count > 0;
        public int ActiveSpotSourceCount => spotSourceIds.Count;
        public bool IsContactActive => contactActive;
        public bool IsRadarEligible => contactActive &&
            (affiliation == RadarAffiliation.Friendly || IsSpotted);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRegistry()
        {
            ActiveContactSet.Clear();
            ContactAdded = null;
            ContactRemoved = null;
        }

        private void OnEnable()
        {
            if (ActiveContactSet.Add(this))
                ContactAdded?.Invoke(this);
        }

        private void OnDisable()
        {
            if (ActiveContactSet.Remove(this))
                ContactRemoved?.Invoke(this);
            if (spotSourceIds.Count > 0)
            {
                spotSourceIds.Clear();
                StateChanged?.Invoke(this);
            }
        }

        public void Configure(
            Transform newPositionSource,
            RadarAffiliation newAffiliation,
            RadarContactCategory newCategory,
            bool newSpotted,
            bool newContactActive = true)
        {
            positionSource = newPositionSource;
            affiliation = newAffiliation;
            category = newCategory;
            spotted = newSpotted;
            contactActive = newContactActive;
            StateChanged?.Invoke(this);
        }

        public void SetSpotted(bool value)
        {
            if (spotted == value)
                return;

            spotted = value;
            StateChanged?.Invoke(this);
        }

        public bool AddSpotSource(int sourceId)
        {
            if (sourceId == 0 || !spotSourceIds.Add(sourceId))
                return false;

            StateChanged?.Invoke(this);
            return true;
        }

        public bool RemoveSpotSource(int sourceId)
        {
            if (sourceId == 0 || !spotSourceIds.Remove(sourceId))
                return false;

            StateChanged?.Invoke(this);
            return true;
        }

        public bool HasSpotSource(int sourceId)
        {
            return sourceId != 0 && spotSourceIds.Contains(sourceId);
        }

        public void SetContactActive(bool value)
        {
            if (contactActive == value)
                return;

            contactActive = value;
            StateChanged?.Invoke(this);
        }

        public void SetAffiliation(RadarAffiliation value)
        {
            if (affiliation == value)
                return;

            affiliation = value;
            StateChanged?.Invoke(this);
        }

        public void SetCategory(RadarContactCategory value)
        {
            if (category == value)
                return;

            category = value;
            StateChanged?.Invoke(this);
        }

        [ContextMenu("Radar/Mark Spotted")]
        private void MarkSpotted()
        {
            SetSpotted(true);
        }

        [ContextMenu("Radar/Clear Spotted")]
        private void ClearSpotted()
        {
            SetSpotted(false);
        }
    }
}
