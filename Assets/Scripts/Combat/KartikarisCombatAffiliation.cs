using UnityEngine;

namespace Kartikaris.Combat
{
    public enum KartikarisAffiliation
    {
        Neutral,
        Player,
        Friendly,
        Hostile
    }

    [DisallowMultipleComponent]
    public sealed class KartikarisCombatAffiliation : MonoBehaviour
    {
        [SerializeField] private KartikarisAffiliation affiliation = KartikarisAffiliation.Neutral;

        public KartikarisAffiliation Affiliation => affiliation;

        public bool IsHostileTo(KartikarisCombatAffiliation other)
        {
            return other != null && AreHostile(affiliation, other.affiliation);
        }

        public static bool AreHostile(
            KartikarisAffiliation first,
            KartikarisAffiliation second)
        {
            if (first == KartikarisAffiliation.Neutral ||
                second == KartikarisAffiliation.Neutral)
            {
                return false;
            }

            bool firstHostile = first == KartikarisAffiliation.Hostile;
            bool secondHostile = second == KartikarisAffiliation.Hostile;
            return firstHostile != secondHostile;
        }
    }
}
