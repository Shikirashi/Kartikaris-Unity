# Dense Forest Prototype

## Summary

Implemented a deterministic dense-forest test stage using Unity's built-in Terrain tree system. It reuses the Terrain and ten tree-prefab slots already configured on `KartikarisTerrainGenerator`, generates lightweight `TreeInstance` records rather than GameObjects, enforces optional spacing/slope/edge constraints, and provides bounded generation plus clearing and timing diagnostics.

This milestone provides the data and controls needed to test whether built-in Terrain trees can conceal mechs at 10,000–75,000-tree densities. It does not implement tree collision, crushing, falling trees, or a custom renderer.

## Files Created

- `Assets/Scripts/WorldGeneration/KartikarisForestGenerator.cs`
- `Assets/Scripts/WorldGeneration/KartikarisForestGenerator.cs.meta`
- `Docs/ImplementationReports/2026-08-05_dense-forest-prototype.md`

## Files Modified

- `Assets/Scripts/WorldGeneration/KartikarisTerrainGenerator.cs`
- `Docs/PROJECT_STATE.md`

## Scene / Prefab / Inspector Changes

No Unity scene or prefab was modified during this task. The open map-test scene was inspected and already contained the Milestone 1 terrain/controller hookups, player Y=60, and ten assigned Synty tree prefabs.

Manual hookup is required: add `KartikarisForestGenerator` to the existing `MapGenerator`. `RequireComponent`, `Reset`, and runtime fallback resolve the colocated `KartikarisTerrainGenerator` reference. For this visual-only milestone, disable **Enable Tree Colliders** on the scene TerrainCollider before testing.

No third-party prefab, collider, material, shader, or package was changed.

## Implementation Details

`KartikarisTerrainGenerator` now exposes read-only access to its target Terrain and existing tree-prefab list. `KartikarisForestGenerator` consumes that configuration and replaces the TerrainData tree prototypes/instances only when generation succeeds with at least one valid renderer-bearing prefab.

Default forest parameters:

- Forest seed: `67890`
- Target trees: `25,000`
- Minimum spacing: `3 m`
- Maximum terrain slope: `35°`
- Uniform scale range: `0.85–1.2`
- Terrain-edge margin: `15 m`
- Density variation scale: `180 m`
- Density variation strength: `0.25`
- Maximum attempts per requested tree: `12`
- Hard safety ceiling: `250,000` trees

A local xorshift32 stream supplies candidate positions, prototype selection, yaw, scale, and density acceptance. A forest-seed-derived Perlin offset supplies broad density variation without touching global Unity random state. Trees use interpolated Terrain height and normal data, upright Y rotation, and matching width/height scale.

When minimum spacing is positive, a dictionary-backed spatial grid with cells sized to `spacing / sqrt(2)` checks nearby cells instead of performing all-pairs comparisons. Zero spacing bypasses the grid. Generation ends at `target count × attempts per tree`, reports shortfalls, and clamps extreme requested counts to its safety ceiling.

Generated standing-tree data remains recoverable from `TerrainData.treeInstances`: normalized position, prototype index, rotation, and scale. Clear Forest removes tree instances without changing Terrain heights. No standing-tree GameObjects, MonoBehaviours, Rigidbodies, or explicit gameplay colliders are created.

Generation logs requested and placed counts, density/slope/spacing rejection totals, forest seed, prototype count, and elapsed wall-clock time.

## Decisions and Rationale

- Used `TreePrototype`, `TreeInstance`, and `TerrainData` to evaluate Unity's built-in Terrain tree path before investing in a bespoke renderer.
- Kept forest generation in a separate component while reusing the Milestone 1 prefab configuration, preserving explicit independent terrain and forest stages without duplicate prefab lists.
- Used a local PRNG so forest changes cannot alter terrain randomness or depend on unrelated random-call ordering.
- Used bounded rejection sampling and a spatial hash to support tens of thousands of candidates without an unbounded loop or naive all-pairs spacing cost.
- Left source prefabs and rendering settings untouched so the prototype measures their actual suitability.

## Validation Performed

- `dotnet build Assembly-CSharp.csproj`: succeeded with zero errors and zero warnings after explicitly including the new source for the check; the generated project file was restored afterward.
- Unity `6000.5.6f1` batch validation in an isolated temporary project: passed using real `TerrainData`, `TreePrototype`, and `TreeInstance` APIs.
- The Unity validation confirmed:
  - one prefab generated the requested 500 trees;
  - repeated seed/settings produced identical ordered instance data;
  - changing forest seed changed instances without changing heights;
  - multiple prototypes were configured and used;
  - zero prefabs returned safely;
  - zero spacing generated correctly;
  - Clear Forest removed instances without changing heights;
  - generation did not create GameObjects per tree;
  - impossible 1,000 m spacing stopped at the bounded attempt count and reported a shortfall.
- A second isolated 1 km × 1 km TerrainData stress check placed all 50,000 requested trees in 1.308 seconds. It rejected 208,403 bounded candidates and preserved the heightmap. This is a generation-time result, not a rendering benchmark.
- Static inspection confirmed no `UnityEngine.Random`, `Random.InitState`, `SetHeights`, per-tree `Instantiate`, unbounded `while` loop, or custom renderer in the forest source.
- Inspected all ten scene-assigned Synty tree prefabs and the relevant Terrain/URP rendering settings.
- Git diff/status was attempted but unavailable because repository metadata is not exposed in the workspace `.git` directory; intentional files were accounted for by direct inspection.

No main-scene Play Mode, FPS, CPU/GPU rendering Profiler, Frame Debugger, memory, or dense-forest rendering measurement was performed.

## Known Issues / Limitations

- The forest component is not attached to the map-test scene yet.
- All ten assigned Synty prefabs contain colliders. Terrain tree collisions may be enabled by the scene TerrainCollider unless **Enable Tree Colliders** is disabled.
- The assigned prefabs are regular mesh prefabs with no `LODGroup`; they are not SpeedTree assets. Stable billboard transitions are therefore not established.
- Their custom Synty Shader Graph materials have GPU instancing disabled, and their MeshRenderers cast/receive shadows. Rendering cost and Terrain-tree shader behavior are unmeasured.
- Current Terrain settings are tree distance 5,000 m, billboard distance 50 m, cross-fade 5 m, and maximum 50 full-LOD trees. These values are potential popping/performance hazards and require visual profiling rather than speculative changes.
- Generate Forest replaces all TerrainData tree prototypes and instances; it does not preserve separately hand-painted trees.
- Clear Forest removes all TerrainData tree instances because no separate ownership metadata exists yet.
- The 250,000-tree ceiling is a development safety bound, not a recommended density.

## Manual Follow-up

1. Add `KartikarisForestGenerator` to `MapGenerator`.
2. Disable TerrainCollider **Enable Tree Colliders**.
3. Generate 10,000 trees and inspect placement/scale.
4. Test 25,000, 50,000, and optionally 75,000 trees in Play Mode.
5. Use the Unity Profiler and Frame Debugger to record CPU, GPU, memory, draw-call, shadow, culling, and transition behavior.
6. Record whether the forest actually conceals the large mech at practical traversal distances.

## Suggested Next Step

Run and document the staged main-scene density benchmark, then decide whether built-in Terrain trees are sufficient before designing tree crushing or a custom standing-tree renderer.
