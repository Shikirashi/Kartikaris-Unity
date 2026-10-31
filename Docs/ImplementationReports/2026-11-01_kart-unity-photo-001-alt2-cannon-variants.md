# KART-UNITY-PHOTO-001 Alt 2 Cannon Variants

## Summary

Extended the isolated portfolio photo-session helper with a CLI/menu capture pass anchored to `PhotoCamera_C_Cannon`. The pass writes six static `1600×900` candidates covering intended folded/deployed states, two minor camera alternatives, and two subtle cannon orientation offsets. No capture session is saved and no production gameplay scene or asset is modified.

## Files Created

- `Docs/ImplementationReports/2026-11-01_kart-unity-photo-001-alt2-cannon-variants.md`
- Six ignored local PNG outputs under `PortfolioCaptures/PhotoSession/`; these are review artifacts, not repository assets.

## Files Modified

- `Assets/Editor/KartikarisPortfolioPhotoSession.cs`
- `Docs/PROJECT_STATE.md`
- `Docs/PortfolioPhotoSession.md`

## Scene / Prefab / Inspector Changes

None. The helper opens `Assets/Scenes/Kartikaris_PortfolioPhotoSession.unity` for rendering but does not save it. `Assets/Scenes/Kartikaris_MapTest.unity` remains byte-identical.

## Implementation Details

- Added menu command **Kartikaris > Portfolio Photo Session > Capture Alt 2 Cannon Pose Variants** and batch entry point `CaptureAlt2CannonPoseVariantsAndExit`.
- Reuses the saved Alt 2 anchor camera at position `(-32.490, 46.210, -76.946)`, rotation `(3.462, 180.447, 0)`, and FOV `34°`.
- Renders deployed candidates before any temporary pose work, preserving the saved Alt 2 composition.
- Applies optional heading/elevation offsets directly to `KartikarisCannonWeapon` pivots with no firing, AI, physics, animation playback, or timed input.
- Reopens a fresh isolated scene before the folded attempt so the disabled Animator is evaluated before that session's first render.
- Restores camera and weapon-pivot rotations in memory and never saves the capture scene.

## Decisions and Rationale

The deployed pose is already serialized into the stable photo fixture, so it is the authoritative anchor. During iteration, Unity's disabled edit-mode Animator did not reliably transition from an already-rendered deployed state back to folded in the same scene session. Splitting the folded attempt into a freshly opened scene reduces pose-cache coupling while keeping the process opt-in and non-destructive.

Visual interpretation was stopped at the user's request. The helper and logs record intended states, but the `folded` filenames remain visually unconfirmed pending the user's separate ChatGPT image analysis.

## Validation Performed

- Exact Unity `6000.3.9f1` batch capture completed with exit code `0`.
- Capture log recorded all six `PORTFOLIO_ALT2_VARIANT_CAPTURED` entries and `PORTFOLIO_ALT2_VARIANTS_COMPLETE` with `sceneSaved=false`.
- ImageMagick `identify` reported `1600×900` for all six PNGs.
- `Assets/Scenes/Kartikaris_MapTest.unity` SHA-256 remained `0fe661f1e541cdbc3419ceb00dc46c794121fa0c09f9a33e8f90d23363d5dbb4`.
- `git diff --check` passed.

## Known Issues / Limitations

- The two intended folded outputs are not visually approved. Unity edit-mode Animator caching was inconsistent during repeated capture iterations; external visual review must confirm whether the cannon is actually folded.
- The ignored PNGs are local to this workspace and are not committed or published.
- A transient Unity/Mono file-descriptor assertion occurred during one repeated batch launch; a clean retry succeeded.

## Manual Follow-up

Review all six PNGs with the user's separate ChatGPT image-analysis workflow. If the folded files still show the deployed pose, provide concrete manual Unity instructions rather than continuing blind image iteration. The menu command can be rerun after any scoped helper correction.

## Suggested Next Step

Select the strongest deployed composition and confirm the folded-state result before making any further pose or camera changes. Do not publish to the portfolio yet.
