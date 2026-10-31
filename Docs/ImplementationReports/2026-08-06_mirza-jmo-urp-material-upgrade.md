# Mirza, Mirza Beig, and JMO URP Material Upgrade

## Summary

Audited every material and texture under `Assets/Mirza`, `Assets/Mirza Beig`, and `Assets/JMO Assets`, then upgraded every legacy material reference to a supported Universal Render Pipeline shader.

The final material set contains 130 supported materials: 16 on official URP shaders, seven on the existing Mirza URP distortion shader, and 97 on a new Kartikaris URP WarFX compatibility shader. No scene, prefab, hierarchy, component reference, source texture, or texture importer setting was changed.

## Files Created

- `Assets/JMO Assets/WarFX/Shaders/WFX_S Universal URP.shader`
- `Assets/JMO Assets/WarFX/Shaders/WFX_S Universal URP.shader.meta`
- `Assets/Editor/KartikarisVfxUrpUpgrade.cs`
- `Assets/Editor/KartikarisVfxUrpUpgrade.cs.meta`
- `Docs/ImplementationReports/2026-08-06_mirza-jmo-urp-material-upgrade.md`

## Files Modified

- 107 material assets:
  - one Mirza Beig legacy particle material;
  - ten JMO Standard demo materials;
  - 96 JMO legacy/custom effect materials.
- `Docs/PROJECT_STATE.md`

The remaining 23 audited materials were already URP compatible and were not rewritten. Individual converted material paths are reported by the Unity upgrade log and all reside beneath the three requested asset roots.

## Scene / Prefab / Inspector Changes

None. No scenes or prefabs were opened or saved, and no serialized component references or hierarchy objects were changed.

## Implementation Details

### Audit

- Audited 130 materials and 105 texture assets through Unity's `AssetDatabase`.
- Pre-upgrade material shader distribution included:
  - 23 already-URP materials;
  - ten JMO `Standard` materials;
  - fourteen JMO legacy particle materials;
  - 82 JMO custom built-in-render-pipeline WFX materials;
  - one Mirza Beig `Particles/Standard Unlit` material.
- Texture importer distribution was 42 Default, eight NormalMap, nine Sprite, and 46 SingleChannel.
- The normal-map textures were already imported as NormalMap with linear color treatment where appropriate. Single-channel WarFX alpha textures were also already correctly classified.

### URP WarFX compatibility shader

`Kartikaris/URP/WarFX Universal` uses URP Core, depth-texture, and lighting libraries and declares the `UniversalPipeline` tag. One shader preserves the effect-specific material behavior through an internal mode selected by the converter:

- alpha blend;
- alpha-channel additive;
- soft additive;
- alpha-channel multiply;
- scrolling additive;
- scrolling alpha;
- soft multiply;
- scrolling soft multiply;
- scrolling smoke;
- transparent diffuse;
- transparent specular;
- conventional legacy additive.

The shader preserves vertex color, tint, main texture transform, soft-particle fade distance, scroll speed, blend factors, fog treatment, and the lighted decal variants' basic diffuse/specular response. Soft particles use the URP scene-depth texture and degrade safely to ordinary transparent rendering when their material mode does not request depth fading.

### Converter

The idempotent `KartikarisVfxUrpUpgrade` editor utility is available at `Kartikaris/VFX/Upgrade Imported VFX Materials To URP` and as a batch `-executeMethod` target.

- `Standard` materials migrate to `Universal Render Pipeline/Lit` while preserving base texture/color, metallic, smoothness, normal map/scale, emission, and base texture scale/offset.
- Recognized legacy particle and WFX shaders migrate to the compatibility shader while preserving texture, tint, inverse fade, scroll speed, shininess, specular color, and effect-specific blend behavior.
- Existing official URP and Mirza URP distortion materials are left untouched.
- Unknown shaders are skipped with an explicit warning rather than silently receiving an unsafe fallback.

## Decisions and Rationale

- Textures themselves are render-pipeline neutral. Since the audit showed correct Default, NormalMap, Sprite, and SingleChannel classifications, rewriting import metadata would have added churn without improving URP compatibility.
- Reassigning custom WFX materials directly to generic URP Particle Unlit would have lost authored multiply blends, scrolling, alpha-channel sampling, smoke behavior, and lit decal variants. A compatibility shader retains those features while using URP includes and pipeline tags.
- Original vendor shader sources remain unchanged for provenance and future comparison. All currently audited materials point away from those built-in-render-pipeline shaders.
- The converter remains in the project so later vendor reimports or new materials can be upgraded consistently.

## Validation Performed

- Checked that no Unity Editor process was active before every invocation.
- Ran only Unity `6000.3.9f1`; each batch process fully exited before the next began.
- The first sandboxed audit attempt could not start Unity Package Manager and exited without completing. It was rerun with approved access and completed successfully; no Unity-generated directory was deleted or cleaned.
- Conversion result: `standard=10`, `effects=97`, `alreadyUrp=23`, `skipped=0`.
- Post-conversion Unity audit result:
  - `Universal Render Pipeline/Lit`: 16;
  - `Universal Render Pipeline/Particles/Simple Lit`: one;
  - `Universal Render Pipeline/Particles/Lit`: five;
  - `Universal Render Pipeline/Particles/Unlit`: four;
  - `Mirza Beig/Distortion Shockwave Particle`: seven;
  - `Kartikaris/URP/WarFX Universal`: 97.
- All 130 material shaders reported `supported=True`; none were missing.
- A final import-only Unity batch pass after cleanup and the final shader adjustment exited successfully with no shader or C# compilation errors in its log.
- Static YAML inspection confirmed all 97 compatibility materials received an explicit effect mode and the expected blend configuration.
- Unity Play Mode and visual side-by-side comparison were not run.

## Known Issues / Limitations

- Exact effect appearance still needs a visual pass, especially additive intensity, soft-particle intersections, multiply smoke, scrolling, fog, and transparent lit decals.
- Soft-particle fading requires scene depth from the active URP renderer/camera.
- The compatibility shader prioritizes preserving the package's prototype look and blend modes; it does not reproduce every historical fixed-function fallback or obsolete hardware path.
- Original JMO legacy shaders remain in the vendor directory but are no longer referenced by the audited materials.
- Git status/diff is unavailable because functional repository metadata is not exposed in this workspace; final assets were accounted for through direct file inspection and Unity audit counts.

## Manual Follow-up

1. In Unity Game View, preview representative Mirza distortion shockwaves and Mirza Beig explosion/smoke prefabs.
2. Preview JMO desktop and mobile muzzle flashes, impacts, smoke, fire, scrolling, multiply, soft-particle, and lighted decal variants.
3. Confirm no material renders magenta and compare intensity, tint, animation speed, depth intersection, and fog behavior with the vendor previews.
4. If soft intersections are hard-edged, enable depth texture generation on the active URP renderer/camera before changing material fade values.
5. Use `Kartikaris/VFX/Upgrade Imported VFX Materials To URP` after a future vendor reimport.

## Suggested Next Step

Assign a few representative converted prefabs to the weapon's muzzle, lingering-smoke, and projectile-impact slots, then perform a Play Mode visual tuning pass before choosing final production effects.
