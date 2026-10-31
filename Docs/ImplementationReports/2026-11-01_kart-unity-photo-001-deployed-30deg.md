# KART-UNITY-PHOTO-001 Deployed 30-Degree Candidate

## Summary

Added one editor-only capture command for a static, fully deployed Kartikaris cannon at a verified physical muzzle elevation of approximately 30 degrees. The command reuses the approved `kartikaris_alt2_unfolded_02` composition and writes a new local `1600×900` PNG without overwriting prior captures or saving any scene.

## Files Created

- `Docs/ImplementationReports/2026-11-01_kart-unity-photo-001-deployed-30deg.md`
- Local ignored output `PortfolioCaptures/PhotoSession/kartikaris_alt2_unfolded_30deg.png`.

## Files Modified

- `Assets/Editor/KartikarisPortfolioPhotoSession.cs`
- `Docs/PROJECT_STATE.md`
- `Docs/PortfolioPhotoSession.md`

## Scene / Prefab / Inspector Changes

None. The helper opens `Assets/Scenes/Kartikaris_PortfolioPhotoSession.unity` for an opt-in render and restores its in-memory camera/elevation-pivot values without saving. Production scenes, prefabs, gameplay code, TerrainData, and Inspector assignments were not changed.

## Implementation Details

- Added menu command **Kartikaris > Portfolio Photo Session > Capture Alt 2 Deployed 30 Degrees** and CLI entry point `CaptureAlt2Unfolded30DegreesAndExit`.
- Reconstructs the existing `alt2_unfolded_02` camera from the saved Cannon anchor and the same offset/look-at calculation: final position `(-30.290, 46.710, -76.964)`, rotation `(7.308, 186.809, 0)`, FOV `35°`.
- Restores the known deployed elevation-pivot rotation, then applies one `+60°` local rotation using `weapon.ElevationAxis` (`X` in the fixture). This mirrors `ElevationOffsetFromPhysicalDegrees(30) = 90 - 30 = 60` without modifying runtime weapon code.
- Measures physical muzzle elevation with the gameplay convention: `asin(dot(FirePoint.forward, referenceUp))` in degrees, using `ElevationHudReferenceFrame` when assigned or the weapon transform otherwise.
- Throws before capture when the measured result differs from 30 degrees by more than 0.5 degrees.
- Restores camera and elevation-pivot state in `finally`; does not enable AI, firing, animation playback, physics, or other gameplay progression.

## Decisions and Rationale

The procedural pivot is the actual post-deployment aiming mechanism, so the new image does not alter or replay the deployment animation. World-space measurement of `FirePoint.forward` verifies the physical result independently of local-axis sign, nesting, or visual assumptions.

## Validation Performed

- Exact Unity `6000.3.9f1` batch capture exited with code `0`.
- Unity logged configured elevation axis `X`, procedural offset `+60.0°`, and measured muzzle elevation `30.048°`.
- Unity logged `sceneSaved=false` and camera position/rotation/FOV matching `alt2_unfolded_02`.
- ImageMagick identified the output as native `1600×900`.
- Direct image inspection confirmed a real deployed mech and forest, visibly lowered cannon, intact composition/lighting, and no obvious cannon/mech intersection, UI, firing, motion, or render defect.
- Approved files `kartikaris_alt2_unfolded_02.png` and `kartikaris_alt2_folded_02.png` retained timestamps from before the new capture.
- `Assets/Scenes/Kartikaris_MapTest.unity` SHA-256 remained `0fe661f1e541cdbc3419ceb00dc46c794121fa0c09f9a33e8f90d23363d5dbb4`.
- `Assets/New Terrain.asset` SHA-256 remained `c31f583e709108de0ab2e7c399cabe08b7a8eb2f64fc274fb9ba830ecb03323f`.

## Known Issues / Limitations

- The ignored screenshot exists only in this workspace until a later, explicitly authorized portfolio publication task copies an approved image.
- Final aesthetic approval remains with the user.

## Manual Follow-up

Review `kartikaris_alt2_unfolded_30deg.png` alongside the two already approved Alt 2 images. Do not publish it yet.

## Suggested Next Step

Accept or reject the new measured 30-degree candidate for the eventual three-image gallery, then handle publication as a separate authorized task.
