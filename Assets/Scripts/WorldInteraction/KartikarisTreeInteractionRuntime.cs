using System;
using System.Collections.Generic;
using Kartikaris.Audio;
using Kartikaris.WorldGeneration;
using UnityEngine;

namespace Kartikaris.WorldInteraction
{
    /// <summary>
    /// Owns runtime-only Terrain tree mutation, virtual trunk collision queries,
    /// spatial lookup, and non-physical falling-tree visuals.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KartikarisTreeInteractionRuntime : MonoBehaviour
    {
        [Serializable]
        public sealed class TreeTrunkProfile
        {
            [SerializeField] private string prototypeName;
            [SerializeField, Min(0.01f)] private float radius = 0.3f;
            [SerializeField, Min(0.1f)] private float height = 7f;
            [SerializeField] private float verticalOffset;
            [SerializeField] private bool derivedFromSourceCapsule;

            public string PrototypeName => prototypeName;
            public float Radius => radius;
            public float Height => height;
            public float VerticalOffset => verticalOffset;
            public bool DerivedFromSourceCapsule => derivedFromSourceCapsule;

            public TreeTrunkProfile(
                string name,
                float newRadius,
                float newHeight,
                float newVerticalOffset,
                bool fromSourceCapsule)
            {
                prototypeName = name;
                radius = Mathf.Max(0.01f, newRadius);
                height = Mathf.Max(0.1f, newHeight);
                verticalOffset = newVerticalOffset;
                derivedFromSourceCapsule = fromSourceCapsule;
            }

            public void Sanitize()
            {
                radius = Mathf.Max(0.01f, radius);
                height = Mathf.Max(0.1f, height);
            }
        }

        private const float MinimumCellSize = 0.5f;
        private const float MinimumDirectionSqrMagnitude = 0.000001f;

        [Header("Source")]
        [SerializeField] private KartikarisTerrainGenerator terrainGenerator;
        [SerializeField] private KartikarisSoundPropagation soundPropagation;

        [Header("Spatial Index")]
        [SerializeField, Min(MinimumCellSize)] private float spatialCellSize = 8f;

        [Header("Virtual Trunk Collision")]
        [Tooltip("Editable per-prototype trunk volumes. Source CapsuleColliders seed defaults; mesh-only trees use conservative named defaults.")]
        [SerializeField] private List<TreeTrunkProfile> trunkProfiles = new List<TreeTrunkProfile>();

        [Header("Fallen Trees")]
        [SerializeField] private float fallenTreeVerticalOffset = 0.05f;
        [SerializeField, Range(0.4f, 1f)] private float fallDuration = 0.7f;

        [Header("Runtime Diagnostics")]
        [SerializeField] private int activeTreeCount;
        [SerializeField] private int destroyedTreeCount;
        [SerializeField] private int candidatesTestedLastQuery;
        [SerializeField] private int treesCrushedLastQuery;
        [SerializeField] private int lastCrushedTreeIndex = -1;
        [SerializeField] private float lastImpactFraction = -1f;

        private readonly Dictionary<long, List<int>> spatialCells = new Dictionary<long, List<int>>();
        private Terrain targetTerrain;
        private TerrainCollider targetTerrainCollider;
        private TerrainData sourceTerrainData;
        private TerrainData runtimeTerrainData;
        private Vector2[] treeWorldPositions;
        private float[] treeWorldRadii;
        private float[] treeWorldBottoms;
        private float[] treeWorldTops;
        private bool[] activeTrees;
        private GameObject[] prototypePrefabs;
        private TreeTrunkProfile[] runtimeProfiles;
        private float maximumWorldTrunkRadius;
        private Transform fallenTreesRoot;
        private bool initialized;
        private bool terrainFlushPending;

        public bool IsInitialized => initialized;
        public int ActiveTreeCount => activeTreeCount;
        public int DestroyedTreeCount => destroyedTreeCount;
        public int CandidatesTestedLastQuery => candidatesTestedLastQuery;
        public int TreesCrushedLastQuery => treesCrushedLastQuery;
        public int LastCrushedTreeIndex => lastCrushedTreeIndex;
        public float LastImpactFraction => lastImpactFraction;
        public int StandingTreeCapacity => activeTrees?.Length ?? 0;
        public TerrainData SourceTerrainData => sourceTerrainData;
        public TerrainData RuntimeTerrainData => runtimeTerrainData;
        public Transform FallenTreesRoot => fallenTreesRoot;
        public IReadOnlyList<TreeTrunkProfile> TrunkProfiles => trunkProfiles;
        public float FallDuration => fallDuration;

        private void Awake()
        {
            InitializeRuntime();
        }

        private void LateUpdate()
        {
            FlushPendingTerrainChanges();
        }

        private void OnDestroy()
        {
            ShutdownRuntime();
        }

        private void Reset()
        {
            ResolveReferences();
            SynchronizePrototypeProfiles();
        }

        private void OnValidate()
        {
            ResolveReferences();
            spatialCellSize = Mathf.Max(MinimumCellSize, spatialCellSize);
            fallDuration = Mathf.Clamp(fallDuration, 0.4f, 1f);
            for (int index = 0; index < trunkProfiles.Count; index++)
                trunkProfiles[index]?.Sanitize();
        }

        [ContextMenu("Tree Interaction/Synchronize Trunk Profiles")]
        public void SynchronizePrototypeProfiles()
        {
            if (terrainGenerator == null)
                terrainGenerator = GetComponent<KartikarisTerrainGenerator>();

            if (terrainGenerator == null)
                return;

            Dictionary<string, TreeTrunkProfile> existing = new Dictionary<string, TreeTrunkProfile>();
            for (int index = 0; index < trunkProfiles.Count; index++)
            {
                TreeTrunkProfile profile = trunkProfiles[index];
                if (profile != null && !string.IsNullOrEmpty(profile.PrototypeName))
                    existing[profile.PrototypeName] = profile;
            }

            List<TreeTrunkProfile> synchronized = new List<TreeTrunkProfile>();
            IReadOnlyList<GameObject> prefabs = terrainGenerator.TreePrefabs;
            for (int index = 0; index < prefabs.Count; index++)
            {
                GameObject prefab = prefabs[index];
                if (prefab == null)
                    continue;

                if (existing.TryGetValue(prefab.name, out TreeTrunkProfile profile))
                {
                    profile.Sanitize();
                    synchronized.Add(profile);
                }
                else
                {
                    synchronized.Add(CreateDefaultProfile(prefab));
                }
            }

            trunkProfiles = synchronized;
        }

        public bool InitializeRuntime()
        {
            if (initialized)
                return true;

            ResolveReferences();
            targetTerrain = terrainGenerator != null ? terrainGenerator.TargetTerrain : null;
            if (targetTerrain == null || targetTerrain.terrainData == null)
            {
                Debug.LogError("Runtime tree interaction requires a terrain generator with valid TerrainData.", this);
                return false;
            }

            targetTerrainCollider = targetTerrain.GetComponent<TerrainCollider>();
            if (targetTerrainCollider == null)
            {
                Debug.LogError("Runtime tree interaction requires the Terrain to have a TerrainCollider.", targetTerrain);
                return false;
            }

            sourceTerrainData = targetTerrain.terrainData;
            runtimeTerrainData = Instantiate(sourceTerrainData);
            runtimeTerrainData.name = $"{sourceTerrainData.name} (Runtime Clone)";
            runtimeTerrainData.hideFlags = HideFlags.DontSave;
            targetTerrain.terrainData = runtimeTerrainData;
            targetTerrainCollider.terrainData = runtimeTerrainData;
            targetTerrain.Flush();

            CachePrototypeData();
            BuildSpatialIndex();
            initialized = true;

            Debug.Log(
                $"Runtime tree interaction indexed {activeTreeCount:N0} virtual trunk volumes " +
                $"across {spatialCells.Count:N0} cells with Terrain tree colliders disabled.",
                this);
            return true;
        }

        /// <summary>
        /// Finds the earliest active trunk hit by a moving horizontal circle,
        /// destroys it immediately, and returns an endpoint just before impact.
        /// </summary>
        public bool TryCrushFirstBlockingTree(
            Vector3 segmentStart,
            Vector3 segmentEnd,
            float moverRadius,
            float moverBottomY,
            float moverTopY,
            Vector3 fallDirection,
            float impactSkin,
            out Vector3 allowedEnd,
            out int crushedTreeIndex)
        {
            allowedEnd = segmentEnd;
            crushedTreeIndex = -1;
            candidatesTestedLastQuery = 0;
            treesCrushedLastQuery = 0;
            lastCrushedTreeIndex = -1;
            lastImpactFraction = -1f;

            if (!initialized || runtimeTerrainData == null || activeTrees == null)
                return false;

            Vector2 start = new Vector2(segmentStart.x, segmentStart.z);
            Vector2 end = new Vector2(segmentEnd.x, segmentEnd.z);
            Vector2 segment = end - start;
            float segmentLength = segment.magnitude;
            if (segmentLength <= Mathf.Epsilon)
                return false;

            float queryRadius = Mathf.Max(0f, moverRadius) + maximumWorldTrunkRadius;
            float cellSize = Mathf.Max(MinimumCellSize, spatialCellSize);
            int minimumCellX = Mathf.FloorToInt((Mathf.Min(start.x, end.x) - queryRadius) / cellSize);
            int maximumCellX = Mathf.FloorToInt((Mathf.Max(start.x, end.x) + queryRadius) / cellSize);
            int minimumCellZ = Mathf.FloorToInt((Mathf.Min(start.y, end.y) - queryRadius) / cellSize);
            int maximumCellZ = Mathf.FloorToInt((Mathf.Max(start.y, end.y) + queryRadius) / cellSize);

            float earliestFraction = float.PositiveInfinity;
            int earliestTree = -1;

            for (int cellZ = minimumCellZ; cellZ <= maximumCellZ; cellZ++)
            {
                for (int cellX = minimumCellX; cellX <= maximumCellX; cellX++)
                {
                    if (!spatialCells.TryGetValue(BuildCellKey(cellX, cellZ), out List<int> treeIndices))
                        continue;

                    for (int indexInCell = 0; indexInCell < treeIndices.Count; indexInCell++)
                    {
                        int treeIndex = treeIndices[indexInCell];
                        if (!activeTrees[treeIndex] ||
                            moverTopY < treeWorldBottoms[treeIndex] ||
                            moverBottomY > treeWorldTops[treeIndex])
                        {
                            continue;
                        }

                        candidatesTestedLastQuery++;
                        float combinedRadius = Mathf.Max(0f, moverRadius) + treeWorldRadii[treeIndex];
                        if (!TrySegmentCircleIntersection(
                                start,
                                segment,
                                treeWorldPositions[treeIndex],
                                combinedRadius,
                                out float impactFraction) ||
                            impactFraction >= earliestFraction)
                        {
                            continue;
                        }

                        earliestFraction = impactFraction;
                        earliestTree = treeIndex;
                    }
                }
            }

            if (earliestTree < 0)
                return false;

            Vector3 horizontalFallDirection = NormalizeHorizontalDirection(fallDirection, segment);
            CrushTree(earliestTree, horizontalFallDirection);
            RequestTerrainFlush();

            float allowedDistance = Mathf.Max(0f, earliestFraction * segmentLength - Mathf.Max(0f, impactSkin));
            Vector2 allowedHorizontal = segment.normalized * allowedDistance;
            allowedEnd = new Vector3(
                segmentStart.x + allowedHorizontal.x,
                segmentEnd.y,
                segmentStart.z + allowedHorizontal.y);
            crushedTreeIndex = earliestTree;
            treesCrushedLastQuery = 1;
            lastCrushedTreeIndex = earliestTree;
            lastImpactFraction = earliestFraction;
            return true;
        }

        /// <summary>
        /// Compatibility helper for non-blocking sweep callers. It still uses
        /// virtual trunk radii and never relies on prefab Collider geometry.
        /// </summary>
        public int CrushTreesAlongSegment(
            Vector3 segmentStart,
            Vector3 segmentEnd,
            float crushRadius,
            Vector3 fallDirection)
        {
            candidatesTestedLastQuery = 0;
            treesCrushedLastQuery = 0;
            if (!initialized || activeTrees == null)
                return 0;

            Vector2 start = new Vector2(segmentStart.x, segmentStart.z);
            Vector2 end = new Vector2(segmentEnd.x, segmentEnd.z);
            Vector2 segment = end - start;
            if (segment.sqrMagnitude <= MinimumDirectionSqrMagnitude)
                return 0;

            float queryRadius = Mathf.Max(0f, crushRadius) + maximumWorldTrunkRadius;
            float cellSize = Mathf.Max(MinimumCellSize, spatialCellSize);
            int minimumCellX = Mathf.FloorToInt((Mathf.Min(start.x, end.x) - queryRadius) / cellSize);
            int maximumCellX = Mathf.FloorToInt((Mathf.Max(start.x, end.x) + queryRadius) / cellSize);
            int minimumCellZ = Mathf.FloorToInt((Mathf.Min(start.y, end.y) - queryRadius) / cellSize);
            int maximumCellZ = Mathf.FloorToInt((Mathf.Max(start.y, end.y) + queryRadius) / cellSize);
            Vector3 horizontalFallDirection = NormalizeHorizontalDirection(fallDirection, segment);

            for (int cellZ = minimumCellZ; cellZ <= maximumCellZ; cellZ++)
            {
                for (int cellX = minimumCellX; cellX <= maximumCellX; cellX++)
                {
                    if (!spatialCells.TryGetValue(BuildCellKey(cellX, cellZ), out List<int> treeIndices))
                        continue;

                    for (int indexInCell = 0; indexInCell < treeIndices.Count; indexInCell++)
                    {
                        int treeIndex = treeIndices[indexInCell];
                        if (!activeTrees[treeIndex])
                            continue;

                        candidatesTestedLastQuery++;
                        float combinedRadius = Mathf.Max(0f, crushRadius) + treeWorldRadii[treeIndex];
                        if (SqrDistanceToSegment(treeWorldPositions[treeIndex], start, end) > combinedRadius * combinedRadius)
                            continue;

                        CrushTree(treeIndex, horizontalFallDirection);
                        treesCrushedLastQuery++;
                    }
                }
            }

            if (treesCrushedLastQuery > 0)
                RequestTerrainFlush();

            return treesCrushedLastQuery;
        }

        /// <summary>
        /// Deactivates active trees within a horizontal world-space radius by
        /// visiting only spatial cells overlapped by the blast. Falling visuals
        /// are directed away from the blast origin.
        /// </summary>
        public int CrushTreesInRadius(Vector3 worldCenter, float radius)
        {
            candidatesTestedLastQuery = 0;
            treesCrushedLastQuery = 0;
            lastCrushedTreeIndex = -1;
            lastImpactFraction = -1f;

            float safeRadius = Mathf.Max(0f, radius);
            if (!initialized || activeTrees == null || safeRadius <= 0f)
                return 0;

            Vector2 center = new Vector2(worldCenter.x, worldCenter.z);
            float cellSize = Mathf.Max(MinimumCellSize, spatialCellSize);
            int minimumCellX = Mathf.FloorToInt((center.x - safeRadius) / cellSize);
            int maximumCellX = Mathf.FloorToInt((center.x + safeRadius) / cellSize);
            int minimumCellZ = Mathf.FloorToInt((center.y - safeRadius) / cellSize);
            int maximumCellZ = Mathf.FloorToInt((center.y + safeRadius) / cellSize);
            float radiusSquared = safeRadius * safeRadius;

            for (int cellZ = minimumCellZ; cellZ <= maximumCellZ; cellZ++)
            {
                for (int cellX = minimumCellX; cellX <= maximumCellX; cellX++)
                {
                    if (!spatialCells.TryGetValue(BuildCellKey(cellX, cellZ), out List<int> treeIndices))
                        continue;

                    for (int indexInCell = 0; indexInCell < treeIndices.Count; indexInCell++)
                    {
                        int treeIndex = treeIndices[indexInCell];
                        if (!activeTrees[treeIndex])
                            continue;

                        candidatesTestedLastQuery++;
                        Vector2 awayFromBlast = treeWorldPositions[treeIndex] - center;
                        if (awayFromBlast.sqrMagnitude > radiusSquared)
                            continue;

                        Vector3 fallDirection = new Vector3(awayFromBlast.x, 0f, awayFromBlast.y);
                        if (fallDirection.sqrMagnitude <= MinimumDirectionSqrMagnitude)
                            fallDirection = Vector3.forward;

                        CrushTree(treeIndex, fallDirection.normalized);
                        treesCrushedLastQuery++;
                        lastCrushedTreeIndex = treeIndex;
                    }
                }
            }

            if (treesCrushedLastQuery > 0)
                RequestTerrainFlush();

            return treesCrushedLastQuery;
        }

        public bool IsTreeActive(int treeIndex)
        {
            return activeTrees != null && treeIndex >= 0 && treeIndex < activeTrees.Length && activeTrees[treeIndex];
        }

        private void CachePrototypeData()
        {
            SynchronizePrototypeProfiles();
            TreePrototype[] prototypes = runtimeTerrainData.treePrototypes;
            prototypePrefabs = new GameObject[prototypes.Length];
            runtimeProfiles = new TreeTrunkProfile[prototypes.Length];

            Dictionary<string, TreeTrunkProfile> byName = new Dictionary<string, TreeTrunkProfile>();
            for (int index = 0; index < trunkProfiles.Count; index++)
            {
                TreeTrunkProfile profile = trunkProfiles[index];
                if (profile != null && !string.IsNullOrEmpty(profile.PrototypeName))
                    byName[profile.PrototypeName] = profile;
            }

            for (int prototypeIndex = 0; prototypeIndex < prototypes.Length; prototypeIndex++)
            {
                GameObject prefab = prototypes[prototypeIndex].prefab;
                prototypePrefabs[prototypeIndex] = prefab;
                if (prefab != null && byName.TryGetValue(prefab.name, out TreeTrunkProfile profile))
                    runtimeProfiles[prototypeIndex] = profile;
                else
                    runtimeProfiles[prototypeIndex] = CreateDefaultProfile(prefab);
            }
        }

        private void BuildSpatialIndex()
        {
            spatialCells.Clear();
            int treeCount = runtimeTerrainData.treeInstanceCount;
            treeWorldPositions = new Vector2[treeCount];
            treeWorldRadii = new float[treeCount];
            treeWorldBottoms = new float[treeCount];
            treeWorldTops = new float[treeCount];
            activeTrees = new bool[treeCount];
            activeTreeCount = 0;
            destroyedTreeCount = 0;
            maximumWorldTrunkRadius = 0f;
            float cellSize = Mathf.Max(MinimumCellSize, spatialCellSize);
            Vector3 terrainPosition = targetTerrain.transform.position;
            Vector3 terrainSize = runtimeTerrainData.size;

            for (int treeIndex = 0; treeIndex < treeCount; treeIndex++)
            {
                TreeInstance instance = runtimeTerrainData.GetTreeInstance(treeIndex);
                Vector2 worldPosition = new Vector2(
                    terrainPosition.x + instance.position.x * terrainSize.x,
                    terrainPosition.z + instance.position.z * terrainSize.z);
                treeWorldPositions[treeIndex] = worldPosition;

                TreeTrunkProfile profile = GetRuntimeProfile(instance.prototypeIndex);
                float baseHeight = terrainPosition.y + instance.position.y * terrainSize.y;
                float radius = profile.Radius * Mathf.Max(0.01f, instance.widthScale);
                float bottom = baseHeight + profile.VerticalOffset * instance.heightScale;
                float top = bottom + profile.Height * Mathf.Max(0.01f, instance.heightScale);
                treeWorldRadii[treeIndex] = radius;
                treeWorldBottoms[treeIndex] = Mathf.Min(bottom, top);
                treeWorldTops[treeIndex] = Mathf.Max(bottom, top);
                maximumWorldTrunkRadius = Mathf.Max(maximumWorldTrunkRadius, radius);

                bool isActive = instance.widthScale > 0f && instance.heightScale > 0f;
                activeTrees[treeIndex] = isActive;
                if (!isActive)
                    continue;

                activeTreeCount++;
                int cellX = Mathf.FloorToInt(worldPosition.x / cellSize);
                int cellZ = Mathf.FloorToInt(worldPosition.y / cellSize);
                long key = BuildCellKey(cellX, cellZ);
                if (!spatialCells.TryGetValue(key, out List<int> treeIndices))
                {
                    treeIndices = new List<int>();
                    spatialCells.Add(key, treeIndices);
                }
                treeIndices.Add(treeIndex);
            }
        }

        private void CrushTree(int treeIndex, Vector3 horizontalFallDirection)
        {
            if (!IsTreeActive(treeIndex))
                return;

            TreeInstance standingInstance = runtimeTerrainData.GetTreeInstance(treeIndex);
            activeTrees[treeIndex] = false;
            activeTreeCount--;
            destroyedTreeCount++;

            TreeInstance hiddenInstance = standingInstance;
            hiddenInstance.widthScale = 0f;
            hiddenInstance.heightScale = 0f;
            runtimeTerrainData.SetTreeInstance(treeIndex, hiddenInstance);
            SpawnFallingTree(standingInstance, horizontalFallDirection);
        }

        private void SpawnFallingTree(TreeInstance standingInstance, Vector3 horizontalFallDirection)
        {
            int prototypeIndex = standingInstance.prototypeIndex;
            if (prototypeIndex < 0 || prototypeIndex >= prototypePrefabs.Length || prototypePrefabs[prototypeIndex] == null)
            {
                Debug.LogWarning($"Tree prototype {prototypeIndex} has no prefab for a fallen-tree visual.", this);
                return;
            }

            EnsureFallenTreesRoot();
            GameObject fallenTree = Instantiate(prototypePrefabs[prototypeIndex], fallenTreesRoot);
            fallenTree.name = $"Fallen_{prototypePrefabs[prototypeIndex].name}";

            Vector3 terrainSize = runtimeTerrainData.size;
            Vector3 terrainPosition = targetTerrain.transform.position;
            Vector3 worldPosition = new Vector3(
                terrainPosition.x + standingInstance.position.x * terrainSize.x,
                0f,
                terrainPosition.z + standingInstance.position.z * terrainSize.z);
            worldPosition.y = targetTerrain.SampleHeight(worldPosition) + terrainPosition.y + fallenTreeVerticalOffset;

            Vector3 terrainNormal = targetTerrain.transform.TransformDirection(
                runtimeTerrainData.GetInterpolatedNormal(
                    Mathf.Clamp01(standingInstance.position.x),
                    Mathf.Clamp01(standingInstance.position.z))).normalized;
            Vector3 tangentFallDirection = Vector3.ProjectOnPlane(horizontalFallDirection, terrainNormal);
            if (tangentFallDirection.sqrMagnitude <= MinimumDirectionSqrMagnitude)
                tangentFallDirection = Vector3.ProjectOnPlane(Vector3.forward, terrainNormal);
            tangentFallDirection.Normalize();

            Quaternion uprightRotation = Quaternion.AngleAxis(standingInstance.rotation * Mathf.Rad2Deg, Vector3.up);
            Quaternion settledRotation = Quaternion.FromToRotation(Vector3.up, tangentFallDirection) * uprightRotation;
            fallenTree.transform.SetPositionAndRotation(worldPosition, uprightRotation);

            Vector3 authoredScale = fallenTree.transform.localScale;
            fallenTree.transform.localScale = Vector3.Scale(
                authoredScale,
                new Vector3(standingInstance.widthScale, standingInstance.heightScale, standingInstance.widthScale));

            DisableCopiedPhysics(fallenTree);
            FallingTreeVisual animation = fallenTree.AddComponent<FallingTreeVisual>();
            animation.Initialize(uprightRotation, settledRotation, fallDuration, soundPropagation);
            soundPropagation?.PlayTreeBreak(worldPosition);
        }

        private static void DisableCopiedPhysics(GameObject fallenTree)
        {
            Collider[] colliders = fallenTree.GetComponentsInChildren<Collider>(true);
            for (int colliderIndex = 0; colliderIndex < colliders.Length; colliderIndex++)
                colliders[colliderIndex].enabled = false;

            Rigidbody[] rigidbodies = fallenTree.GetComponentsInChildren<Rigidbody>(true);
            for (int rigidbodyIndex = 0; rigidbodyIndex < rigidbodies.Length; rigidbodyIndex++)
            {
                rigidbodies[rigidbodyIndex].detectCollisions = false;
                rigidbodies[rigidbodyIndex].isKinematic = true;
                DestroyRuntimeObject(rigidbodies[rigidbodyIndex]);
            }
        }

        private void EnsureFallenTreesRoot()
        {
            if (fallenTreesRoot != null)
                return;

            GameObject root = new GameObject("FallenTrees");
            root.hideFlags = HideFlags.DontSave;
            fallenTreesRoot = root.transform;
        }

        public void ShutdownRuntime()
        {
            terrainFlushPending = false;
            if (targetTerrain != null && sourceTerrainData != null && targetTerrain.terrainData == runtimeTerrainData)
            {
                targetTerrain.terrainData = sourceTerrainData;
                if (targetTerrainCollider != null)
                {
                    targetTerrainCollider.terrainData = sourceTerrainData;
                }
                targetTerrain.Flush();
            }

            if (fallenTreesRoot != null)
                DestroyRuntimeObject(fallenTreesRoot.gameObject);
            if (runtimeTerrainData != null)
                DestroyRuntimeObject(runtimeTerrainData);

            spatialCells.Clear();
            treeWorldPositions = null;
            treeWorldRadii = null;
            treeWorldBottoms = null;
            treeWorldTops = null;
            activeTrees = null;
            prototypePrefabs = null;
            runtimeProfiles = null;
            runtimeTerrainData = null;
            sourceTerrainData = null;
            fallenTreesRoot = null;
            initialized = false;
        }

        private void RequestTerrainFlush()
        {
            if (targetTerrain != null)
                terrainFlushPending = true;
        }

        private void FlushPendingTerrainChanges()
        {
            if (!terrainFlushPending)
                return;

            terrainFlushPending = false;
            if (targetTerrain != null)
                targetTerrain.Flush();
        }

        private void ResolveReferences()
        {
            if (terrainGenerator == null)
                terrainGenerator = GetComponent<KartikarisTerrainGenerator>();
            if (soundPropagation == null)
                soundPropagation = FindFirstObjectByType<KartikarisSoundPropagation>();
        }

        private TreeTrunkProfile GetRuntimeProfile(int prototypeIndex)
        {
            if (runtimeProfiles != null && prototypeIndex >= 0 && prototypeIndex < runtimeProfiles.Length &&
                runtimeProfiles[prototypeIndex] != null)
            {
                return runtimeProfiles[prototypeIndex];
            }

            return new TreeTrunkProfile("Fallback", 0.35f, 8f, 0f, false);
        }

        private static TreeTrunkProfile CreateDefaultProfile(GameObject prefab)
        {
            string prototypeName = prefab != null ? prefab.name : "Missing Prototype";
            if (prefab != null)
            {
                CapsuleCollider[] capsules = prefab.GetComponentsInChildren<CapsuleCollider>(true);
                CapsuleCollider selected = null;
                for (int index = 0; index < capsules.Length; index++)
                {
                    if (capsules[index].direction == 1 &&
                        (selected == null || capsules[index].radius < selected.radius))
                    {
                        selected = capsules[index];
                    }
                }

                if (selected != null)
                {
                    Vector3 scale = selected.transform.lossyScale;
                    float horizontalScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
                    float verticalScale = Mathf.Abs(scale.y);
                    float radius = selected.radius * horizontalScale;
                    float height = selected.height * verticalScale;
                    float verticalOffset = Mathf.Max(0f, (selected.center.y - selected.height * 0.5f) * verticalScale);
                    return new TreeTrunkProfile(prototypeName, radius, height, verticalOffset, true);
                }
            }

            if (prototypeName.Contains("Tree_Big_01", StringComparison.Ordinal))
                return new TreeTrunkProfile(prototypeName, 0.62f, 11.5f, 0f, false);
            if (prototypeName.Contains("Tree_Big_02", StringComparison.Ordinal))
                return new TreeTrunkProfile(prototypeName, 0.56f, 10.5f, 0f, false);
            if (prototypeName.EndsWith("Tree_01", StringComparison.Ordinal))
                return new TreeTrunkProfile(prototypeName, 0.38f, 7.5f, 0f, false);
            if (prototypeName.EndsWith("Tree_02", StringComparison.Ordinal))
                return new TreeTrunkProfile(prototypeName, 0.42f, 8.5f, 0f, false);

            return new TreeTrunkProfile(prototypeName, 0.35f, 8f, 0f, false);
        }

        private static bool TrySegmentCircleIntersection(
            Vector2 segmentStart,
            Vector2 segment,
            Vector2 circleCenter,
            float radius,
            out float impactFraction)
        {
            Vector2 fromCenter = segmentStart - circleCenter;
            float radiusSquared = radius * radius;
            float c = Vector2.Dot(fromCenter, fromCenter) - radiusSquared;
            if (c <= 0f)
            {
                impactFraction = 0f;
                return true;
            }

            float a = Vector2.Dot(segment, segment);
            float b = Vector2.Dot(fromCenter, segment);
            float discriminant = b * b - a * c;
            if (a <= MinimumDirectionSqrMagnitude || b >= 0f || discriminant < 0f)
            {
                impactFraction = 0f;
                return false;
            }

            float candidate = (-b - Mathf.Sqrt(discriminant)) / a;
            impactFraction = candidate;
            return candidate >= 0f && candidate <= 1f;
        }

        private static Vector3 NormalizeHorizontalDirection(Vector3 direction, Vector2 fallbackSegment)
        {
            Vector3 horizontal = new Vector3(direction.x, 0f, direction.z);
            if (horizontal.sqrMagnitude <= MinimumDirectionSqrMagnitude)
                horizontal = new Vector3(fallbackSegment.x, 0f, fallbackSegment.y);
            return horizontal.normalized;
        }

        private static float SqrDistanceToSegment(Vector2 point, Vector2 start, Vector2 end)
        {
            Vector2 segment = end - start;
            float segmentLengthSquared = segment.sqrMagnitude;
            if (segmentLengthSquared <= MinimumDirectionSqrMagnitude)
                return (point - start).sqrMagnitude;

            float interpolation = Mathf.Clamp01(Vector2.Dot(point - start, segment) / segmentLengthSquared);
            Vector2 closestPoint = start + segment * interpolation;
            return (point - closestPoint).sqrMagnitude;
        }

        private static long BuildCellKey(int x, int z)
        {
            return ((long)x << 32) ^ (uint)z;
        }

        private static void DestroyRuntimeObject(UnityEngine.Object target)
        {
            if (target == null)
                return;

            if (Application.isPlaying)
                Destroy(target);
            else
                DestroyImmediate(target);
        }
    }
}
