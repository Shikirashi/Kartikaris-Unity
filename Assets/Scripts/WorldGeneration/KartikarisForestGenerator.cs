using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Kartikaris.WorldGeneration
{
    /// <summary>
    /// Generates deterministic visual-only forests using Unity Terrain tree instances.
    /// The resulting TerrainData tree instances remain the authoritative standing-tree data.
    /// </summary>
    [RequireComponent(typeof(KartikarisTerrainGenerator))]
    public sealed class KartikarisForestGenerator : MonoBehaviour
    {
        private const int MaximumSupportedTreeCount = 250000;
        private const float TwoPi = Mathf.PI * 2f;

        [Header("Source")]
        [SerializeField] private KartikarisTerrainGenerator terrainGenerator;

        [Header("Forest Placement")]
        [SerializeField] private int forestSeed = 67890;
        [SerializeField, Min(0)] private int targetTreeCount = 25000;
        [SerializeField, Min(0f)] private float minimumSpacing = 3f;
        [SerializeField, Range(0f, 90f)] private float maximumTerrainSlope = 35f;
        [SerializeField, Min(0.01f)] private float minimumScale = 0.85f;
        [SerializeField, Min(0.01f)] private float maximumScale = 1.2f;
        [SerializeField, Min(0f)] private float terrainEdgeMargin = 15f;

        [Header("Density Variation")]
        [SerializeField, Min(1f)] private float densityVariationScale = 180f;
        [SerializeField, Range(0f, 1f)] private float densityVariationStrength = 0.25f;
        [SerializeField, Range(1, 50)] private int maxPlacementAttemptsPerTree = 12;

        private void Reset()
        {
            terrainGenerator = GetComponent<KartikarisTerrainGenerator>();
        }

        private void OnValidate()
        {
            if (terrainGenerator == null)
            {
                terrainGenerator = GetComponent<KartikarisTerrainGenerator>();
            }

            targetTreeCount = Mathf.Max(0, targetTreeCount);
            minimumSpacing = Mathf.Max(0f, minimumSpacing);
            maximumTerrainSlope = Mathf.Clamp(maximumTerrainSlope, 0f, 90f);
            minimumScale = Mathf.Max(0.01f, minimumScale);
            maximumScale = Mathf.Max(minimumScale, maximumScale);
            terrainEdgeMargin = Mathf.Max(0f, terrainEdgeMargin);
            densityVariationScale = Mathf.Max(1f, densityVariationScale);
            densityVariationStrength = Mathf.Clamp01(densityVariationStrength);
            maxPlacementAttemptsPerTree = Mathf.Clamp(maxPlacementAttemptsPerTree, 1, 50);
        }

        [ContextMenu("Generate / Regenerate Forest")]
        public void GenerateForest()
        {
            if (!TryGetTerrain(out Terrain terrain, out TerrainData terrainData))
            {
                return;
            }

            List<TreePrototype> prototypes = BuildTreePrototypes(terrain);
            if (prototypes.Count == 0)
            {
                Debug.LogWarning(
                    "Forest generation skipped because no valid tree prefabs are assigned. " +
                    "Zero assigned prefabs is supported and leaves the current forest unchanged.",
                    this);
                return;
            }

            Vector3 terrainSize = terrainData.size;
            float clampedMargin = Mathf.Clamp(terrainEdgeMargin, 0f, Mathf.Min(terrainSize.x, terrainSize.z) * 0.5f);
            float usableWidth = terrainSize.x - clampedMargin * 2f;
            float usableDepth = terrainSize.z - clampedMargin * 2f;
            if (usableWidth <= 0f || usableDepth <= 0f)
            {
                Debug.LogError("The terrain-edge margin leaves no usable forest placement area.", this);
                return;
            }

            int requestedCount = Mathf.Clamp(targetTreeCount, 0, MaximumSupportedTreeCount);
            if (requestedCount != targetTreeCount)
            {
                Debug.LogWarning(
                    $"Requested {targetTreeCount:N0} trees; clamped to the safety limit of " +
                    $"{MaximumSupportedTreeCount:N0} trees.",
                    this);
            }

            Stopwatch stopwatch = Stopwatch.StartNew();
            DeterministicRandom random = new DeterministicRandom(forestSeed);
            List<TreeInstance> instances = new List<TreeInstance>(requestedCount);
            List<Vector2> acceptedPositions = minimumSpacing > 0f
                ? new List<Vector2>(requestedCount)
                : null;
            SpatialGrid spatialGrid = minimumSpacing > 0f
                ? new SpatialGrid(minimumSpacing, requestedCount)
                : null;

            int maximumAttempts = CalculateMaximumAttempts(requestedCount, maxPlacementAttemptsPerTree);
            int densityRejections = 0;
            int slopeRejections = 0;
            int spacingRejections = 0;
            Vector2 densityOffset = BuildDensityOffset(forestSeed);
            float minimumNormalizedX = clampedMargin / terrainSize.x;
            float maximumNormalizedX = 1f - minimumNormalizedX;
            float minimumNormalizedZ = clampedMargin / terrainSize.z;
            float maximumNormalizedZ = 1f - minimumNormalizedZ;
            float minimumNormalY = Mathf.Cos(maximumTerrainSlope * Mathf.Deg2Rad);

            for (int attempt = 0; attempt < maximumAttempts && instances.Count < requestedCount; attempt++)
            {
                float normalizedX = Mathf.Lerp(minimumNormalizedX, maximumNormalizedX, random.NextFloat01());
                float normalizedZ = Mathf.Lerp(minimumNormalizedZ, maximumNormalizedZ, random.NextFloat01());
                float localX = normalizedX * terrainSize.x;
                float localZ = normalizedZ * terrainSize.z;

                if (!PassesDensityVariation(localX, localZ, densityOffset, ref random))
                {
                    densityRejections++;
                    continue;
                }

                if (terrainData.GetInterpolatedNormal(normalizedX, normalizedZ).y < minimumNormalY)
                {
                    slopeRejections++;
                    continue;
                }

                Vector2 localPosition = new Vector2(localX, localZ);
                if (spatialGrid != null &&
                    !spatialGrid.CanPlace(localPosition, acceptedPositions, minimumSpacing))
                {
                    spacingRejections++;
                    continue;
                }

                float scale = Mathf.Lerp(minimumScale, maximumScale, random.NextFloat01());
                float normalizedHeight = terrainData.GetInterpolatedHeight(normalizedX, normalizedZ) / terrainSize.y;
                TreeInstance instance = new TreeInstance
                {
                    position = new Vector3(normalizedX, Mathf.Clamp01(normalizedHeight), normalizedZ),
                    prototypeIndex = random.NextInt(prototypes.Count),
                    rotation = random.NextFloat01() * TwoPi,
                    widthScale = scale,
                    heightScale = scale,
                    color = (Color32)Color.white,
                    lightmapColor = (Color32)Color.white
                };

                instances.Add(instance);
                if (spatialGrid != null)
                {
                    int acceptedIndex = acceptedPositions.Count;
                    acceptedPositions.Add(localPosition);
                    spatialGrid.Add(localPosition, acceptedIndex);
                }
            }

            RecordTerrainDataUndo(terrainData, "Generate Kartikaris Forest");
            terrainData.treePrototypes = prototypes.ToArray();
            terrainData.SetTreeInstances(instances.ToArray(), false);
            terrain.Flush();
            stopwatch.Stop();

            int rejectedCount = densityRejections + slopeRejections + spacingRejections;
            Debug.Log(
                $"Forest generation requested {requestedCount:N0} trees and placed {instances.Count:N0} in " +
                $"{stopwatch.Elapsed.TotalSeconds:0.000} s. Rejected {rejectedCount:N0} candidates " +
                $"(density {densityRejections:N0}, slope {slopeRejections:N0}, spacing {spacingRejections:N0}) " +
                $"using forest seed {forestSeed} and {prototypes.Count} prototypes.",
                this);

            if (instances.Count < requestedCount)
            {
                Debug.LogWarning(
                    $"Placed {instances.Count:N0} of {requestedCount:N0} requested trees after the bounded " +
                    $"{maximumAttempts:N0} placement attempts. Reduce minimum spacing or slope/density restrictions.",
                    this);
            }
        }

        [ContextMenu("Clear Forest")]
        public void ClearForest()
        {
            if (!TryGetTerrain(out Terrain terrain, out TerrainData terrainData))
            {
                return;
            }

            int removedCount = terrainData.treeInstanceCount;
            RecordTerrainDataUndo(terrainData, "Clear Kartikaris Forest");
            terrainData.SetTreeInstances(Array.Empty<TreeInstance>(), false);
            terrain.Flush();

            Debug.Log($"Cleared {removedCount:N0} Terrain tree instances without changing terrain heights.", this);
        }

        private bool TryGetTerrain(out Terrain terrain, out TerrainData terrainData)
        {
            terrain = null;
            terrainData = null;

            if (terrainGenerator == null)
            {
                terrainGenerator = GetComponent<KartikarisTerrainGenerator>();
            }

            if (terrainGenerator == null)
            {
                Debug.LogError("Forest generation requires a KartikarisTerrainGenerator reference.", this);
                return false;
            }

            terrain = terrainGenerator.TargetTerrain;
            if (terrain == null)
            {
                Debug.LogError("The referenced terrain generator has no target Terrain assignment.", this);
                return false;
            }

            terrainData = terrain.terrainData;
            if (terrainData == null || terrainData.size.x <= 0f || terrainData.size.y <= 0f || terrainData.size.z <= 0f)
            {
                Debug.LogError("The target Terrain has no valid TerrainData.", terrain);
                terrainData = null;
                return false;
            }

            return true;
        }

        private List<TreePrototype> BuildTreePrototypes(Terrain terrain)
        {
            IReadOnlyList<GameObject> configuredPrefabs = terrainGenerator.TreePrefabs;
            List<TreePrototype> prototypes = new List<TreePrototype>(configuredPrefabs.Count);
            bool foundCollider = false;
            bool foundMissingLod = false;
            bool foundNonInstancedMaterial = false;

            for (int index = 0; index < configuredPrefabs.Count; index++)
            {
                GameObject prefab = configuredPrefabs[index];
                if (prefab == null)
                {
                    Debug.LogWarning($"Tree prefab slot {index} is empty and will be skipped.", this);
                    continue;
                }

                Renderer[] renderers = prefab.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length == 0)
                {
                    Debug.LogWarning(
                        $"Tree prefab '{prefab.name}' has no Renderer and is unsuitable for Terrain trees; it was skipped.",
                        prefab);
                    continue;
                }

                foundCollider |= prefab.GetComponentInChildren<Collider>(true) != null;
                foundMissingLod |= prefab.GetComponentInChildren<LODGroup>(true) == null;
                foundNonInstancedMaterial |= HasNonInstancedMaterial(renderers);
                prototypes.Add(new TreePrototype { prefab = prefab, bendFactor = 0f });
            }

            if (foundCollider)
            {
                Debug.LogWarning(
                    "One or more assigned tree prefabs contains colliders. Source prefabs were not modified. " +
                    "Disable Enable Tree Colliders on the TerrainCollider for this visual-only milestone.",
                    terrain);
            }

            if (foundMissingLod)
            {
                Debug.LogWarning(
                    "One or more assigned tree prefabs has no LODGroup. Regular mesh prefabs may remain full meshes " +
                    "rather than providing stable billboard/LOD transitions; verify this in Play Mode.",
                    terrain);
            }

            if (foundNonInstancedMaterial)
            {
                Debug.LogWarning(
                    "One or more assigned tree materials has GPU instancing disabled. No third-party material was " +
                    "modified; measure the rendering cost before relying on these prefabs for final density.",
                    terrain);
            }

            WarnAboutTerrainRenderingSettings(terrain);
            return prototypes;
        }

        private bool PassesDensityVariation(
            float localX,
            float localZ,
            Vector2 offset,
            ref DeterministicRandom random)
        {
            if (densityVariationStrength <= 0f)
            {
                return true;
            }

            float scale = Mathf.Max(1f, densityVariationScale);
            float noise = Mathf.PerlinNoise(localX / scale + offset.x, localZ / scale + offset.y);
            float acceptanceProbability = Mathf.Lerp(1f, noise, densityVariationStrength);
            return random.NextFloat01() <= acceptanceProbability;
        }

        private static bool HasNonInstancedMaterial(Renderer[] renderers)
        {
            for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
            {
                Material[] materials = renderers[rendererIndex].sharedMaterials;
                for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                {
                    Material material = materials[materialIndex];
                    if (material != null && !material.enableInstancing)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private void WarnAboutTerrainRenderingSettings(Terrain terrain)
        {
            float terrainDiagonal = new Vector2(terrain.terrainData.size.x, terrain.terrainData.size.z).magnitude;
            if (terrain.treeDistance > terrainDiagonal)
            {
                Debug.LogWarning(
                    $"Terrain tree distance is {terrain.treeDistance:0.#} m, beyond the " +
                    $"{terrainDiagonal:0.#} m terrain diagonal. This can increase dense-forest rendering cost.",
                    terrain);
            }

            if (terrain.treeMaximumFullLODCount < Mathf.Min(targetTreeCount, 1000))
            {
                Debug.LogWarning(
                    $"Terrain Maximum Full LOD Trees is {terrain.treeMaximumFullLODCount:N0} for a requested " +
                    $"{targetTreeCount:N0}-tree forest. Inspect transition stability before changing it.",
                    terrain);
            }
        }

        private static int CalculateMaximumAttempts(int requestedCount, int attemptsPerTree)
        {
            long attempts = (long)requestedCount * Mathf.Clamp(attemptsPerTree, 1, 50);
            return (int)Math.Min(attempts, int.MaxValue);
        }

        private static Vector2 BuildDensityOffset(int seed)
        {
            return new Vector2(HashToOffset(seed, 0xA341316Cu), HashToOffset(seed, 0xC8013EA4u));
        }

        private static float HashToOffset(int seed, uint salt)
        {
            unchecked
            {
                uint value = (uint)seed ^ salt;
                value ^= value >> 16;
                value *= 0x7FEB352Du;
                value ^= value >> 15;
                value *= 0x846CA68Bu;
                value ^= value >> 16;
                return value / (float)uint.MaxValue * 20000f - 10000f;
            }
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private static void RecordTerrainDataUndo(TerrainData terrainData, string operationName)
        {
#if UNITY_EDITOR
            UnityEditor.Undo.RegisterCompleteObjectUndo(terrainData, operationName);
            UnityEditor.EditorUtility.SetDirty(terrainData);
#endif
        }

        private sealed class SpatialGrid
        {
            private readonly float cellSize;
            private readonly Dictionary<long, int> acceptedIndices;

            public SpatialGrid(float spacing, int expectedCount)
            {
                cellSize = Mathf.Max(0.001f, spacing / Mathf.Sqrt(2f));
                acceptedIndices = new Dictionary<long, int>(Mathf.Max(0, expectedCount));
            }

            public bool CanPlace(Vector2 candidate, List<Vector2> positions, float minimumDistance)
            {
                int candidateX = Mathf.Max(0, (int)(candidate.x / cellSize));
                int candidateZ = Mathf.Max(0, (int)(candidate.y / cellSize));
                int searchRadius = Mathf.CeilToInt(minimumDistance / cellSize);
                float minimumDistanceSquared = minimumDistance * minimumDistance;
                int minimumX = Mathf.Max(0, candidateX - searchRadius);
                int maximumX = candidateX + searchRadius;
                int minimumZ = Mathf.Max(0, candidateZ - searchRadius);
                int maximumZ = candidateZ + searchRadius;

                for (int z = minimumZ; z <= maximumZ; z++)
                {
                    for (int x = minimumX; x <= maximumX; x++)
                    {
                        if (acceptedIndices.TryGetValue(BuildKey(x, z), out int acceptedIndex) &&
                            (positions[acceptedIndex] - candidate).sqrMagnitude <= minimumDistanceSquared)
                        {
                            return false;
                        }
                    }
                }

                return true;
            }

            public void Add(Vector2 position, int acceptedIndex)
            {
                int x = Mathf.Max(0, (int)(position.x / cellSize));
                int z = Mathf.Max(0, (int)(position.y / cellSize));
                acceptedIndices[BuildKey(x, z)] = acceptedIndex;
            }

            private static long BuildKey(int x, int z)
            {
                return ((long)x << 32) | (uint)z;
            }
        }

        private struct DeterministicRandom
        {
            private uint state;

            public DeterministicRandom(int seed)
            {
                state = (uint)seed;
                if (state == 0u)
                {
                    state = 0x6E624EB7u;
                }
            }

            public float NextFloat01()
            {
                return (NextUInt() >> 8) * (1f / 16777216f);
            }

            public int NextInt(int exclusiveMaximum)
            {
                return exclusiveMaximum <= 1
                    ? 0
                    : (int)(NextUInt() % (uint)exclusiveMaximum);
            }

            private uint NextUInt()
            {
                uint value = state;
                value ^= value << 13;
                value ^= value >> 17;
                value ^= value << 5;
                state = value;
                return value;
            }
        }
    }
}
