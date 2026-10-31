using System.Collections.Generic;
using Kartikaris.WorldInteraction;
using Kartikaris.Modules;
using UnityEngine;

namespace Kartikaris.Combat
{
    /// <summary>
    /// Reusable radial gameplay explosion. Visual effects remain the caller's
    /// responsibility so damage and presentation can be configured separately.
    /// </summary>
    public static class KartikarisExplosion
    {
        public readonly struct Result
        {
            public int DamagedMechCount { get; }
            public float TotalDamageApplied { get; }
            public int CrushedTreeCount { get; }
            public int DamagedDeployableCount { get; }

            public Result(int damagedMechCount, float totalDamageApplied, int crushedTreeCount,
                int damagedDeployableCount = 0)
            {
                DamagedMechCount = damagedMechCount;
                TotalDamageApplied = totalDamageApplied;
                CrushedTreeCount = crushedTreeCount;
                DamagedDeployableCount = damagedDeployableCount;
            }
        }

        /// <summary>
        /// Full damage applies through innerRadius, then falls linearly to zero
        /// at outerRadius. One health component is damaged at most once even if
        /// several of its child colliders overlap the blast.
        /// </summary>
        public static Result Detonate(
            Vector3 origin,
            float outerRadius,
            float innerRadius,
            float maximumDamage,
            LayerMask damageLayers,
            GameObject damageSource,
            KartikarisTreeInteractionRuntime treeInteraction,
            float treeEffectRadius)
        {
            float safeOuterRadius = Mathf.Max(0f, outerRadius);
            float safeInnerRadius = Mathf.Clamp(innerRadius, 0f, safeOuterRadius);
            float safeMaximumDamage = Mathf.Max(0f, maximumDamage);
            int damagedMechCount = 0;
            float totalDamageApplied = 0f;
            int damagedDeployableCount = 0;

            if (safeOuterRadius > 0f && safeMaximumDamage > 0f)
            {
                Collider[] overlaps = Physics.OverlapSphere(
                    origin,
                    safeOuterRadius,
                    damageLayers,
                    QueryTriggerInteraction.Collide);
                HashSet<KartikarisMechHealth> resolvedHealth = new HashSet<KartikarisMechHealth>();
                HashSet<KartikarisDeployableHealth> resolvedDeployableHealth =
                    new HashSet<KartikarisDeployableHealth>();

                for (int index = 0; index < overlaps.Length; index++)
                {
                    Collider overlap = overlaps[index];
                    if (overlap == null)
                        continue;

                    KartikarisMechHealth health = overlap.GetComponentInParent<KartikarisMechHealth>();
                    if (health != null && resolvedHealth.Add(health))
                    {
                        float distance = Vector3.Distance(origin, health.transform.position);
                        float damage = CalculateDamage(
                            distance,
                            safeInnerRadius,
                            safeOuterRadius,
                            safeMaximumDamage);
                        float applied = health.ApplyDamage(damage, damageSource, origin);
                        if (applied > 0f)
                        {
                            damagedMechCount++;
                            totalDamageApplied += applied;
                        }
                    }

                    KartikarisDeployableHealth deployableHealth =
                        overlap.GetComponentInParent<KartikarisDeployableHealth>();
                    if (deployableHealth != null &&
                        resolvedDeployableHealth.Add(deployableHealth))
                    {
                        float distance = Vector3.Distance(
                            origin,
                            deployableHealth.transform.position);
                        float damage = CalculateDamage(
                            distance,
                            safeInnerRadius,
                            safeOuterRadius,
                            safeMaximumDamage);
                        float applied = deployableHealth.ApplyDamage(
                            damage,
                            damageSource,
                            origin);
                        if (applied > 0f)
                        {
                            damagedDeployableCount++;
                            totalDamageApplied += applied;
                        }
                    }
                }
            }

            int crushedTreeCount = treeInteraction != null
                ? treeInteraction.CrushTreesInRadius(origin, Mathf.Max(0f, treeEffectRadius))
                : 0;
            return new Result(damagedMechCount, totalDamageApplied, crushedTreeCount,
                damagedDeployableCount);
        }

        public static Result DetonateCone(
            Vector3 origin,
            Vector3 forward,
            float radius,
            float coneFullAngleDegrees,
            float maximumDamage,
            LayerMask damageLayers,
            GameObject damageSource,
            KartikarisTreeInteractionRuntime treeInteraction,
            float treeEffectRadius)
        {
            float safeRadius = Mathf.Max(0f, radius);
            float safeDamage = Mathf.Max(0f, maximumDamage);
            Vector3 safeForward = forward.sqrMagnitude > 0.000001f ? forward.normalized : Vector3.forward;
            float minimumDot = Mathf.Cos(Mathf.Clamp(coneFullAngleDegrees, 0.1f, 179f) * 0.5f * Mathf.Deg2Rad);
            int damagedCount = 0;
            float totalDamage = 0f;
            HashSet<KartikarisMechHealth> resolved = new HashSet<KartikarisMechHealth>();
            HashSet<KartikarisDeployableHealth> resolvedDeployables =
                new HashSet<KartikarisDeployableHealth>();
            int damagedDeployables = 0;
            Collider[] overlaps = Physics.OverlapSphere(origin, safeRadius, damageLayers, QueryTriggerInteraction.Collide);
            for (int index = 0; index < overlaps.Length; index++)
            {
                KartikarisMechHealth health = overlaps[index] != null
                    ? overlaps[index].GetComponentInParent<KartikarisMechHealth>()
                    : null;
                if (health == null || !health.IsAlive || !resolved.Add(health))
                    continue;

                Vector3 offset = health.transform.position - origin;
                float distance = offset.magnitude;
                if (distance <= Mathf.Epsilon || distance > safeRadius ||
                    Vector3.Dot(safeForward, offset / distance) < minimumDot)
                    continue;

                float applied = health.ApplyDamage(
                    safeDamage * (1f - Mathf.Clamp01(distance / safeRadius)),
                    damageSource,
                    origin);
                if (applied <= 0f)
                    continue;
                damagedCount++;
                totalDamage += applied;
            }

            for (int index = 0; index < overlaps.Length; index++)
            {
                KartikarisDeployableHealth health = overlaps[index] != null
                    ? overlaps[index].GetComponentInParent<KartikarisDeployableHealth>()
                    : null;
                if (health == null || !health.IsAlive || !resolvedDeployables.Add(health))
                    continue;

                Vector3 offset = health.transform.position - origin;
                float distance = offset.magnitude;
                if (distance <= Mathf.Epsilon || distance > safeRadius ||
                    Vector3.Dot(safeForward, offset / distance) < minimumDot)
                    continue;

                float applied = health.ApplyDamage(
                    safeDamage * (1f - Mathf.Clamp01(distance / safeRadius)),
                    damageSource,
                    origin);
                if (applied <= 0f)
                    continue;
                damagedDeployables++;
                totalDamage += applied;
            }

            int crushedTrees = treeInteraction != null
                ? treeInteraction.CrushTreesInRadius(origin, Mathf.Max(0f, treeEffectRadius))
                : 0;
            return new Result(damagedCount, totalDamage, crushedTrees, damagedDeployables);
        }

        public static float CalculateDamage(
            float distance,
            float innerRadius,
            float outerRadius,
            float maximumDamage)
        {
            float safeOuterRadius = Mathf.Max(0f, outerRadius);
            float safeInnerRadius = Mathf.Clamp(innerRadius, 0f, safeOuterRadius);
            float safeMaximumDamage = Mathf.Max(0f, maximumDamage);
            float safeDistance = Mathf.Max(0f, distance);

            if (safeMaximumDamage <= 0f || safeDistance >= safeOuterRadius)
                return 0f;
            if (safeDistance <= safeInnerRadius || safeOuterRadius <= safeInnerRadius)
                return safeMaximumDamage;

            float falloff = 1f - (safeDistance - safeInnerRadius) /
                (safeOuterRadius - safeInnerRadius);
            return safeMaximumDamage * Mathf.Clamp01(falloff);
        }
    }
}
