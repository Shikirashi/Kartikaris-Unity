# Fresh Project Recovery

## Summary

Reconciled the fresh `/home/shiki/Documents/github/Kartikaris` Unity 6000.3.9f1 project with the read-only `/home/shiki/Documents/github/Katarsis` prototype and restored functional parity at the dense-forest milestone immediately before tree crushing.

The fresh project already contained byte-identical copies of the terrain generator, forest generator, and first-person controller source, but its scene did not reference them. Its valid fresh TerrainData was still at Unity's default-style dimensions, had no tree prototypes or instances, was positioned at the positive quadrant, and had tree collisions enabled. The recovery preserved the fresh package/settings/assets, configured that same TerrainData through Unity APIs, rebuilt scene hookups, assigned the ten fresh Synty prefabs, generated deterministic terrain and 25,000 trees, and restored documentation.

## Comparison Before Recovery

### Present in the donor but missing in the fresh project

- Repository `AGENTS.md` documentation workflow.
- `Docs/PROJECT_STATE.md`, implementation-report workflow, dense-forest report, and Ritual Division audit.
- Functional scene hookups for `KartikarisTerrainGenerator`, `KartikarisForestGenerator`, and `MechFirstPersonController`.
- `MapGenerator` scene root and its serialized settings/references.
- Generated rolling terrain and configured Terrain tree prototypes/instances.
- Terrain centered around world origin and visual-only tree collision setting.

### Present in both but different

- The three in-scope gameplay `.cs` files were byte-identical, but the fresh project had independently generated script `.meta` GUIDs. Fresh GUIDs were preserved and the scene was wired through Unity APIs.
- The scene asset shared the same scene GUID but the fresh scene retained its new-project lighting/volume/mech import and had no gameplay hookups.
- The fresh TerrainData was valid and referenced by both Terrain components, but measured `1000 × 600 × 1000 m`, detail resolution `1024/32`, zero prototypes/trees, and terrain position `(0,0,0)`. It was configured in place to the verified prototype settings instead of copying the donor binary.
- The fresh player already had a 4 m × 1 m `CharacterController`, but its step offset was `0.3`, its Y position was `0`, and its camera pivot was offset to `(0.692, 3.31, -0.583)`. The recovered values are step offset `0.5`, player Y `60`, and pivot `(0,3,0)`; the fresh X/Z spawn was preserved.
- Fresh package versions differ from the donor for Input System, AI Navigation, Timeline, Visual Scripting, Collaboration, and Rider integration.

### Present only in the fresh project and preserved

- `Assets/New Terrain.asset` and its GUID.
- Freshly imported `Assets/Synty/` content; all ten donor prefab paths/GUIDs resolved directly in the fresh import.
- Fresh mech FBX/import metadata and existing mech scene instance.
- Fresh URP settings/assets, volume profile, input actions asset, package manifest/lock, and project settings.

### Obsolete or out-of-scope donor state not restored

- Donor `Library/`, `Temp/`, `Logs/`, `UserSettings/`, package caches, IDE outputs, packages, and project settings.
- Donor `Assets/Terrain/Kartikaris_TerrainData.asset` binary and its downgrade-migration history.
- Donor `_TerrainAutoUpgrade` content and migration/repair editor tooling.
- Donor Unity 6000.5/downgrade serialization debris and URP repair artifacts.
- Runtime tree crushing, runtime TerrainData cloning/mutation, and fallen-tree spawning.

## Files Copied From Katarsis

- `AGENTS.md`
- `Docs/ImplementationReports/README.md`
- `Docs/ImplementationReports/2026-08-05_dense-forest-prototype.md`
- `Docs/RitualDivision_MapGeneration_Port_Audit.md`

No Unity asset or script `.meta` file was copied. No third-party asset was copied.

## Files Created

- `Docs/PROJECT_STATE.md`
- `Docs/ImplementationReports/2026-08-05_fresh-project-recovery.md`

A temporary `Assets/Editor/KartikarisFreshRecovery.cs` plus generated `.meta` was used for Unity-API inspection/recovery/validation and removed after use.

## Files Modified

- `Assets/Scenes/Kartikaris_MapTest.unity`
- `Assets/New Terrain.asset`

## Files Removed

The fresh target already contained later-milestone source that is beyond the requested pre-tree-crushing state. It was not attached to the fresh scene and was removed:

- `Assets/Scripts/Player/MechTreeCrusher.cs`
- `Assets/Scripts/Player/MechTreeCrusher.cs.meta`
- `Assets/Scripts/WorldInteraction/KartikarisTreeInteractionRuntime.cs`
- `Assets/Scripts/WorldInteraction/KartikarisTreeInteractionRuntime.cs.meta`
- `Assets/Scripts/WorldInteraction.meta`

## Scene / Prefab / Inspector Changes

- Added a `MapGenerator` root at the origin.
- Attached/configured `KartikarisTerrainGenerator` and `KartikarisForestGenerator`.
- Assigned the existing scene Terrain to the terrain generator.
- Assigned ten prefabs from the fresh Synty imports. Paths and GUIDs matched the donor references, so no remapping or `.meta` replacement was necessary.
- Configured terrain defaults: seed `12345`, base `30`, amplitude `24`, scale `300`, octaves `4`, persistence `0.4`, lacunarity `2.0`.
- Configured forest defaults: seed `67890`, target `25,000`, spacing `3`, max slope `35`, scale `0.85–1.2`, edge `15`, density scale `180`, strength `0.25`, attempts `12`.
- Attached `MechFirstPersonController` to `Player` and assigned `CameraPivot`.
- Set `CameraPivot` local position to `(0,3,0)`.
- Set CharacterController height/radius/center/slope/step to `4/1/(0,2,0)/45/0.5`.
- Raised the player to Y `60` while preserving fresh X/Z placement.
- Centered Terrain at `(-512,0,-512)`.
- Disabled TerrainCollider tree collisions.
- Kept the fresh Terrain, Directional Light, Global Volume, camera, and mech visual.

No tree-crushing/runtime-interaction component was added. No third-party prefab, shader, material, or collider was modified.

## TerrainData Changes

The valid fresh `Assets/New Terrain.asset` was retained. Through Unity 6000.3 APIs it was configured to:

- size `1024 × 120 × 1024 m`;
- heightmap resolution `513`;
- detail resolution `512`, `32` per patch;
- control texture resolution `512`;
- base texture resolution `1024`.

The deterministic terrain was generated into this asset, followed by 25,000 Terrain tree instances. The donor TerrainData binary was not copied.

## Packages / Project Settings

No package or ProjectSettings file changed. The authoritative fresh baseline remains:

- Unity `6000.3.9f1`;
- URP/Shader Graph `17.3.0`;
- Input System `1.18.0`;
- AI Navigation `2.0.10` (installed but unused).

The donor's Input System `1.20.0`, AI Navigation `2.0.14`, and other newer package patch versions were intentionally not transplanted. The fresh project already had new Input System handling enabled and a valid URP assignment.

## Unity-Version Compatibility Adjustments

The copied gameplay implementation required no source adaptation; the in-scope fresh copies were byte-identical to donor source and compiled under Unity 6000.3.9f1.

The temporary recovery helper accessed TerrainCollider's tree-collision flag through serialized property `m_EnableTreeColliders`, because Unity 6000.3 does not expose it as a public C# property. The helper was removed after the scene was saved and validated.

## Validation Performed

All Unity work used `/home/shiki/Unity/Hub/Editor/6000.3.9f1/Editor/Unity` (`6000.3.9f1`).

- Confirmed the fresh TerrainData was readable through Unity APIs before modification.
- Unity batch compile/import completed successfully after final cleanup.
- Confirmed the map-test scene contains Terrain/TerrainCollider with their shared fresh TerrainData, MapGenerator with both generators, Player with CharacterController/controller, CameraPivot, and Main Camera.
- Confirmed no missing scripts on the recovered MapGenerator or Player.
- Generated deterministic terrain at `513 × 513` with seed `12345`.
- Repeated terrain generation and obtained identical height hash `1DA8AD9EDB9DFB7E`.
- Generated all 25,000 requested Terrain trees from ten prototypes. The recovery run completed placement in `0.476 s`; validation regenerations completed in `0.123–0.138 s`. These are generation times, not rendering benchmarks.
- Repeated forest generation and obtained identical ordered-tree hash `C7ED14CE827BB23B`.
- Cleared all trees, confirmed heights were unchanged, and regenerated the identical 25,000-tree forest.
- Confirmed forest generation did not create per-tree scene roots.
- Confirmed tree collision is disabled and Terrain remains centered with the expected configuration.

Interactive Play Mode controls and rendered performance were not claimed as automated validation.

## Final Functional Comparison

| Feature | Old Katarsis | Fresh Kartikaris after recovery | Status |
| --- | --- | --- | --- |
| Terrain generation | Present | Present, deterministic on fresh TerrainData | Restored |
| Forest generation | Present | Present, deterministic at 25,000 trees | Restored |
| First-person mech controller | Present | Present and referenced | Restored |
| Tree prefab references | Ten Synty prefabs | Same ten fresh-import prefabs | Reconnected without copying |
| TerrainData | Donor migration-rebuilt asset | Fresh Unity-6000.3 asset configured in place | Intentionally different |
| Documentation workflow | Present | Present | Restored |
| Tree crushing | Later donor milestone | Not present | Intentionally excluded |
| Packages/project settings | Donor patch versions/migration history | Fresh Unity-6000.3 baseline | Intentionally preserved |

## Known Issues / Limitations

- Synty trees still use their functional URP materials. The known Terrain warning about non-`Nature/Soft Occlusion` shaders was not addressed because changing third-party materials/shaders is out of scope and could break URP rendering.
- No interactive Play Mode traversal, billboard-quality inspection, or sustained FPS/GPU/CPU profiling was performed.
- Unity printed a licensing token refresh message during one batch run, but licensing initialized and all requested batch runs completed successfully.
- Git status/diff was attempted before and after recovery, but the target's workspace `.git` directory is nonfunctional/empty and the donor has no `.git` directory. Changes were accounted for through direct file/content inspection instead.

## Donor Safety

The donor was treated read-only. Its non-generated content digest was `84508bd8beab25bd8d455049a877ad557ba666e7c022b3ec796eed063ae8ad4d` both before and after recovery. No Unity process was launched against the donor, and no migration or write was performed there.

## Manual Follow-up

1. Open the project and map-test scene in Unity `6000.3.9f1`.
2. Enter Play Mode and verify WASD, mouse look, grounding, Escape release, click recapture, camera placement, and mech-visual alignment.
3. Walk through trees to confirm visual-only collision behavior.
4. Inspect tree billboard/lighting transitions and profile the 25,000-tree scene on the intended hardware.

## Suggested Next Step

Complete the interactive traversal/visual/performance pass. Do not begin tree crushing until explicitly requested as the next milestone.
