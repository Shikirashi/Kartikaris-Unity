using System.Collections.Generic;
using UnityEngine;

namespace Kartikaris.WorldGeneration
{
    /// <summary>
    /// Writes deterministic, gently rolling heights into an existing Unity TerrainData asset.
    /// Tree prefabs are shared with the separate forest-generation stage.
    /// </summary>
    public sealed class KartikarisTerrainGenerator : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] private Terrain targetTerrain;
        [SerializeField] private bool generateOnStart;

        [Header("Terrain Shape")]
        [SerializeField] private int seed = 12345;
        [SerializeField, Min(0f)] private float baseElevation = 30f;
        [SerializeField, Min(0f)] private float terrainAmplitude = 24f;
        [SerializeField, Min(1f)] private float primaryNoiseScale = 300f;
        [SerializeField, Range(1, 8)] private int octaveCount = 4;
        [SerializeField, Range(0f, 1f)] private float persistence = 0.4f;
        [SerializeField, Min(1f)] private float lacunarity = 2f;

        [Header("Tree Prototypes")]
        [Tooltip("Tree prefabs used by the separate Kartikaris forest generator.")]
        [SerializeField] private List<GameObject> treePrefabs = new List<GameObject>();

        public Terrain TargetTerrain => targetTerrain;
        public IReadOnlyList<GameObject> TreePrefabs => treePrefabs;

        private void Start()
        {
            if (generateOnStart)
            {
                GenerateTerrain();
            }
        }

        [ContextMenu("Generate Terrain")]
        public void GenerateTerrain()
        {
            if (!TryGetTerrainData(out TerrainData terrainData))
            {
                return;
            }

            WarnAboutUnusualTerrainData(terrainData);

            int resolution = terrainData.heightmapResolution;
            Vector3 terrainSize = terrainData.size;
            float[,] heights = new float[resolution, resolution];
            float safeScale = Mathf.Max(1f, primaryNoiseScale);
            int safeOctaves = Mathf.Clamp(octaveCount, 1, 8);
            float safePersistence = Mathf.Clamp01(persistence);
            float safeLacunarity = Mathf.Max(1f, lacunarity);
            float safeVerticalSize = Mathf.Max(0.0001f, terrainSize.y);

            Vector2[] octaveOffsets = BuildOctaveOffsets(seed, safeOctaves);
            float maximumNoiseAmplitude = CalculateMaximumNoiseAmplitude(safeOctaves, safePersistence);

            for (int z = 0; z < resolution; z++)
            {
                float worldZ = z / (resolution - 1f) * terrainSize.z;

                for (int x = 0; x < resolution; x++)
                {
                    float worldX = x / (resolution - 1f) * terrainSize.x;
                    float frequency = 1f;
                    float amplitude = 1f;
                    float noiseSum = 0f;

                    for (int octave = 0; octave < safeOctaves; octave++)
                    {
                        Vector2 offset = octaveOffsets[octave];
                        float sampleX = worldX / safeScale * frequency + offset.x;
                        float sampleZ = worldZ / safeScale * frequency + offset.y;
                        float centeredNoise = Mathf.PerlinNoise(sampleX, sampleZ) * 2f - 1f;

                        noiseSum += centeredNoise * amplitude;
                        amplitude *= safePersistence;
                        frequency *= safeLacunarity;
                    }

                    float normalizedNoise = noiseSum / maximumNoiseAmplitude;
                    float heightInMetres = baseElevation + normalizedNoise * terrainAmplitude;
                    heights[z, x] = Mathf.Clamp01(heightInMetres / safeVerticalSize);
                }
            }

            RecordTerrainDataUndo(terrainData, "Generate Kartikaris Terrain");
            terrainData.SetHeights(0, 0, heights);
            targetTerrain.Flush();

            Debug.Log(
                $"Generated deterministic terrain with seed {seed} at {resolution}x{resolution} " +
                $"using the existing TerrainData size {terrainSize}.",
                this);
        }

        [ContextMenu("Regenerate Current Seed")]
        private void RegenerateCurrentSeed()
        {
            GenerateTerrain();
        }

        [ContextMenu("Flatten Terrain")]
        public void FlattenTerrain()
        {
            if (!TryGetTerrainData(out TerrainData terrainData))
            {
                return;
            }

            int resolution = terrainData.heightmapResolution;
            RecordTerrainDataUndo(terrainData, "Flatten Kartikaris Terrain");
            terrainData.SetHeights(0, 0, new float[resolution, resolution]);
            targetTerrain.Flush();

            Debug.Log($"Flattened terrain at {resolution}x{resolution} without changing its configuration.", this);
        }

        private bool TryGetTerrainData(out TerrainData terrainData)
        {
            terrainData = null;

            if (targetTerrain == null)
            {
                Debug.LogError("Kartikaris terrain generation requires a target Terrain assignment.", this);
                return false;
            }

            terrainData = targetTerrain.terrainData;
            if (terrainData == null)
            {
                Debug.LogError("The assigned Terrain has no TerrainData.", targetTerrain);
                return false;
            }

            if (terrainData.heightmapResolution < 2 || terrainData.size.y <= 0f)
            {
                Debug.LogError("The assigned TerrainData has an invalid heightmap resolution or vertical size.", targetTerrain);
                terrainData = null;
                return false;
            }

            return true;
        }

        private static Vector2[] BuildOctaveOffsets(int rootSeed, int count)
        {
            Vector2[] offsets = new Vector2[count];

            for (int octave = 0; octave < count; octave++)
            {
                offsets[octave] = new Vector2(
                    HashToOffset(rootSeed, octave, 0),
                    HashToOffset(rootSeed, octave, 1));
            }

            return offsets;
        }

        private static float HashToOffset(int rootSeed, int octave, int axis)
        {
            unchecked
            {
                uint value = (uint)rootSeed;
                value ^= (uint)(octave + 1) * 0x9E3779B9u;
                value ^= (uint)(axis + 1) * 0x85EBCA6Bu;
                value ^= value >> 16;
                value *= 0x7FEB352Du;
                value ^= value >> 15;
                value *= 0x846CA68Bu;
                value ^= value >> 16;

                return value / (float)uint.MaxValue * 20000f - 10000f;
            }
        }

        private static float CalculateMaximumNoiseAmplitude(int count, float persistenceValue)
        {
            float total = 0f;
            float amplitude = 1f;

            for (int octave = 0; octave < count; octave++)
            {
                total += amplitude;
                amplitude *= persistenceValue;
            }

            return Mathf.Max(total, 0.0001f);
        }

        private void WarnAboutUnusualTerrainData(TerrainData terrainData)
        {
            Vector3 size = terrainData.size;
            if (size.x < 500f || size.z < 500f || size.x > 4096f || size.z > 4096f)
            {
                Debug.LogWarning(
                    $"Terrain size is {size.x:0.#} x {size.z:0.#} metres. Defaults are tuned for roughly 1 km square, " +
                    "but the existing TerrainData configuration will be preserved.",
                    this);
            }

            float requestedMinimum = baseElevation - terrainAmplitude;
            float requestedMaximum = baseElevation + terrainAmplitude;
            if (requestedMinimum < 0f || requestedMaximum > size.y)
            {
                Debug.LogWarning(
                    $"Requested height range ({requestedMinimum:0.#} to {requestedMaximum:0.#} m) exceeds the " +
                    $"TerrainData vertical range (0 to {size.y:0.#} m); generated heights will be clamped.",
                    this);
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
    }
}
