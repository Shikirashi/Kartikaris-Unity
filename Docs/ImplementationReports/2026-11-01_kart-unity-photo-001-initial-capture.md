# KART-UNITY-PHOTO-001 Initial Capture

## Summary

Activated the prepared photo workflow with licensed Unity `6000.3.9f1`, created a stable isolated photo-session scene, and captured three actual GPU-rendered 1600×900 candidates of the real Kartikaris mech in its generated forest. The final drafts are local and awaiting human review; nothing was published to the portfolio website.

## Files Created

- `Assets/Scenes/Kartikaris_PortfolioPhotoSession.unity`
- `Assets/Scenes/Kartikaris_PortfolioPhotoSession.unity.meta`
- `Assets/PortfolioPhotoSession.meta`
- `Assets/PortfolioPhotoSession/Kartikaris_PhotoTerrainData.asset`
- `Assets/PortfolioPhotoSession/Kartikaris_PhotoTerrainData.asset.meta`
- `Docs/ImplementationReports/2026-11-01_kart-unity-photo-001-initial-capture.md`

Local ignored outputs:

- `PortfolioCaptures/PhotoSession/kartikaris_hero_01.png`
- `PortfolioCaptures/PhotoSession/kartikaris_alt_01.png`
- `PortfolioCaptures/PhotoSession/kartikaris_alt_02.png`

## Files Modified

- `.gitignore`
- `Assets/Editor/KartikarisPortfolioPhotoSession.cs`
- `Docs/PROJECT_STATE.md`
- `Docs/PortfolioPhotoSession.md`

The prior static-preparation report remains historical; one path line was restored to describe its original pre-capture implementation accurately.

## Scene / Prefab / Inspector Changes

Created only the isolated `Kartikaris_PortfolioPhotoSession.unity` scene. The Player mech is saved at `(-32.695, 40.058, -96.707)` with Y rotation `28°`, grounded on the copied Terrain and frozen with its existing cannon Animator bool evaluated into the deployed pose. Enemy, cockpit, Canvas, EventSystem, source cameras, gameplay MonoBehaviours, CharacterControllers, audio, Rigidbody motion/collision, and live particles are disabled in the fixture.

The photo scene saves three disabled cameras:

- Hero: `(-34.466, 45.358, -112.079)`, Euler `(3.146, 6.750, 0)`, FOV `38°`.
- Environment: `(-13.696, 47.404, -90.591)`, Euler `(6.840, 252.193, 0)`, FOV `47°`.
- Cannon: `(-32.490, 46.210, -76.946)`, Euler `(3.462, 180.447, 0)`, FOV `34°`.

The isolated Directional Light is saved at Euler `(42, -28, 0)`, intensity `1.25`, warm color `(1, 0.94, 0.84)`, soft shadows, `0.9` shadow strength, and ambient intensity `1.05`.

## Implementation Details

- Changed local draft output from Unity-cleaned `Temp/` to ignored `PortfolioCaptures/PhotoSession/`, allowing PNGs to survive Editor shutdown.
- Removed all capture-time `QualitySettings` writes after the first licensed run caused Unity to reserialize that project asset; the file was restored exactly and remains outside the final diff.
- Duplicates `Assets/New Terrain.asset` into a photo-only TerrainData asset before changing tree instances.
- Retains 24,968 of 25,000 generated trees. Thirty-two copied instances are removed from a 13 m subject clearing and narrow 10 m camera sight corridors only; the original TerrainData is untouched.
- Tightened camera distances after visual inspection of the first pass and widened only the copied sight corridors after a second pass showed an overhead canopy obstructing the hero silhouette.
- Keeps all three raw drafts ignored and local pending human selection.

## Decisions and Rationale

The first render pass was genuinely captured and inspected rather than treated as successful based on process exit alone. It showed severe foreground canopy occlusion. A private TerrainData duplicate permits small photo-stage sightline changes without altering the procedural forest used by gameplay. Retaining more than 99.8% of the generated trees preserves the real forest density and terrain context while producing readable compositions.

## Validation Performed

- Unity CLI license status reported active Unity Personal and signed-in OAuth state.
- Used exact Unity Editor `6000.3.9f1` with X11, OpenGL, and the Radeon RX 6700 XT renderer.
- Three Unity CLI capture passes exited successfully; final logs report `staticNonHostile=true`, source tree count 25,000, retained count 24,968, and three completed PNG captures.
- A separate non-mutating Unity validation pass reported `failures=0`, three disabled photo cameras, 24,968 copied trees, and zero missing scripts; it also confirmed Player, Enemy and MapGenerator gameplay MonoBehaviours and all CharacterControllers are disabled, cockpit/UI layers are excluded, the private TerrainData is assigned, and all three local PNGs exist at 1600×900.
- Verified all final PNGs are native 1600×900, 8-bit RGB, non-interlaced images and visually inspected each at original resolution.
- Confirmed the production `Assets/Scenes/Kartikaris_MapTest.unity` SHA-256 remains `0fe661f1e541cdbc3419ceb00dc46c794121fa0c09f9a33e8f90d23363d5dbb4`.
- Confirmed `ProjectSettings/QualitySettings.asset` and `Assets/New Terrain.asset` have no repository diff after capture.
- Confirmed the final images contain the real staged Player mech, copied real TerrainData, dense existing Synty forest, sky, lighting and shadows, without cockpit UI, radar, gizmos, or editor chrome.
- Play Mode was not used because the photo fixture is deliberately static and is rendered directly by saved external cameras.

## Known Issues / Limitations

- These are initial draft compositions, not user-approved final portfolio images.
- The deployed cannon is extremely tall in the authored pose and dominates the silhouette; the user may prefer a lower authored pose or a Travel-state hero after review.
- The real mech material is subdued gray against the bright low-poly forest. Additional isolated light/framing adjustment may improve separation after the user selects a candidate.
- The local PNGs are intentionally ignored and therefore are not recoverable from Git alone.

## Manual Follow-up

Review candidates A/B/C. To adjust one, open `Kartikaris_PortfolioPhotoSession.unity` in Unity `6000.3.9f1`, select the corresponding saved camera, adjust only the isolated fixture, save it, and run **Kartikaris > Portfolio Photo Session > Capture 1600x900 Drafts**. Do not run the refresh command after manual adjustments because it intentionally rebuilds the fixture.

## Suggested Next Step

Choose A, B, or C and specify whether the deployed cannon silhouette should remain vertical. Then perform one scoped composition/light revision in the isolated scene and capture the user-approved final without publishing it yet.
