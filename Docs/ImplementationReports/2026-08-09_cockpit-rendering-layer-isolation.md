# Cockpit Rendering Layer Isolation

## Summary

Configured the saved cockpit presentation stage as a separate URP lighting space. A new `Cockpit` Rendering Layer isolates all physical cockpit and monitor renderers from the exterior Directional Light while allowing the existing red `Warning Light` Point Light to target those renderers.

This was a targeted serialized edit performed while Unity was closed. No Unity Editor, batch-mode, Play Mode, or visual render process was started.

## Files Created

- `Docs/ImplementationReports/2026-08-09_cockpit-rendering-layer-isolation.md`

## Files Modified

- `ProjectSettings/TagManager.asset`
- `Assets/Scenes/Kartikaris_MapTest.unity`
- `Docs/PROJECT_STATE.md`

## Scene / Prefab / Inspector Changes

- Appended Rendering Layer `Cockpit` at index `8`, corresponding to bit `0x00000100` and decimal mask `256`.
- Added scene prefab-instance overrides assigning mask `256` to 28 physical renderer components beneath the cockpit stage:
  - ten `MeshRenderer` components in the imported `kartikaris monitor` instance;
  - one `MeshRenderer` and seventeen `SkinnedMeshRenderer` components in the imported `cockpit` instance.
- Assigned the red `Warning Light` Point Light to Cockpit-only mask `256` in both its built-in `Light.m_RenderingLayerMask` and URP `UniversalAdditionalLightData.m_RenderingLayersMask` fields.
- Verified the main scene `Directional Light` already has Default-only mask `1`. Because bit `1` and Cockpit bit `256` do not overlap, it already excludes Cockpit and no Directional Light numeric field required modification.

No prefab asset, imported FBX, material, Canvas/UI component, camera, Terrain, exterior renderer, ordinary GameObject layer, light transform, light color/intensity/range, shadow setting, or physical geometry was changed. The existing `Warning Light` GameObject remains inactive.

## Implementation Details

Unity 6000.3.9f1 serializes Rendering Layer names in `ProjectSettings/TagManager.asset` under `m_RenderingLayers`. Since indices `0` through `7` were already named, `Cockpit` was appended at index `8`; no existing name was renamed or repurposed.

The installed URP 17.3 source identifies `m_SupportsLightLayers` as the serialized `Use Rendering Layers` pipeline setting, `m_RenderingLayersMask` as the current URP additional-light mask, and `m_RenderingLayerMask` as the renderer/built-in Light mask. The active `Assets/Settings/PC_RPAsset.asset` already serializes `m_SupportsLightLayers: 1`, so no render-pipeline or Renderer Data asset change was necessary.

The cockpit geometry is supplied by two imported model instances rather than scene-authored renderer documents. Unity 6000.3.9f1's standalone `binary2text` tool was used read-only on their existing cached imported artifacts to enumerate exact renderer component file IDs. Those IDs were then used for narrow scene prefab overrides; no property name or target ID was inferred.

The disabled `radar panel` renderer still receives the Cockpit override so its lighting assignment remains correct if its existing activation/enable state is changed later. World Space radar UI and VisionHUD UI were deliberately excluded.

## Decisions and Rationale

- Preserved the Directional Light's existing Default-only mask instead of broadening it to every bit except Cockpit. That is the smallest change and retains all existing light-layer intent.
- Restricted the red Point Light to Cockpit only, preventing exterior spill while satisfying the required cockpit-light overlap.
- Preserved ordinary Lit materials and used renderer/light filtering only. No Unlit conversion, emissive fake lighting, roof, wall, cube, or invisible blocker was introduced.
- Preserved the red light's inactive saved state because activation was outside the requested rendering-layer semantic change.

## Validation Performed

- Confirmed `GraphicsSettings.asset` and the active PC quality level reference `PC_RPAsset`.
- Confirmed `PC_RPAsset.asset` already has `m_SupportsLightLayers: 1` and references `PC_Renderer.asset`.
- Confirmed `TagManager.asset` contains `Cockpit` exactly once at index `8`, after all eight preserved existing names.
- Extracted and mapped the imported artifacts for `Assets/3D/cockpit.fbx` and `Assets/3D/kartikaris monitor.fbx` with Unity 6000.3.9f1 `binary2text`.
- Confirmed all 18 cockpit-model and all 10 monitor-model renderer component IDs have one scene `m_RenderingLayerMask` override with value `256`.
- Confirmed `Warning Light` is a red Point Light and both current light-mask fields are `256`.
- Confirmed the main `Directional Light` retains mask `1`, excluding Cockpit bit `256` while matching Terrain and other exterior renderers on Default bit `1`.
- Confirmed camera culling-mask fields and ordinary `m_Layer` assignments were not edited.
- Confirmed no material, imported model, prefab, script, package, Renderer Data, or pipeline asset was edited.
- Confirmed no scene GameObject or geometry document was added or removed.
- Ran Unity 6000.3.9f1 `UnityYAMLMerge diff` against the resulting scene itself; it parsed successfully and exited `0` with no differences.
- Audited workspace hashes because Git metadata is unavailable in this environment; only the intended scene, TagManager, project-state document, and this report differ from the pre-task snapshot.

Unity Editor and runtime behavior were not validated, as explicitly required.

## Known Issues / Limitations

- The saved `Warning Light` GameObject was already inactive and remains inactive. Its mask is ready for Cockpit-only lighting whenever intended logic or a manual action enables it.
- Static serialization proves mask overlap/exclusion but cannot prove final pixels, shader behavior, or perceived red-light intensity.
- The inactive/disabled imported `radar panel` physical renderer is counted among the 28 assigned renderers because it remains cockpit geometry and may be re-enabled later.

## Manual Follow-up

1. Open the project only in Unity `6000.3.9f1` and inspect Rendering Layers on the cockpit/monitor renderers, `Directional Light`, and `Warning Light`.
2. In Game/Play view, confirm the cockpit does not receive sunlight while Terrain and exterior mechs do.
3. Temporarily enable the currently inactive `Warning Light`; confirm red Lit shading affects the cockpit but not the exterior.
4. Confirm there is no blocker geometry and camera/UI rendering remains unchanged.

## Suggested Next Step

Run the short visual verification above, then decide whether `Warning Light` should start active or be controlled by an explicit cockpit/gameplay state in a separate task.
