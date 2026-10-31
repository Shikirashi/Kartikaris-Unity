# KART-UNITY-PHOTO-001 Static Capture Preparation

## Summary

Prepared an editor-only, opt-in workflow that creates a static and non-hostile portfolio photo fixture from the real Kartikaris gameplay scene and renders three 1600×900 draft compositions. The workflow preserves the authoritative `Assets/Scenes/Kartikaris_MapTest.unity`, the actual Player `Kartikaris Mech`, the authored procedural TerrainData and forest, and the existing lighting/rendering pipeline.

An actual GPU-backed screenshot attempt was made with exact Unity `6000.3.9f1`, but the Editor exited before importing the helper or rendering because no valid Unity Editor license was available. No screenshot or generated staging scene is claimed.

## Files Created

- `Assets/Editor/KartikarisPortfolioPhotoSession.cs`
- `Assets/Editor/KartikarisPortfolioPhotoSession.cs.meta`
- `Docs/ImplementationReports/2026-11-01_kart-unity-photo-001-static-capture-preparation.md`

## Files Modified

- `Docs/PROJECT_STATE.md`
- `Docs/PortfolioPhotoSession.md`

## Scene / Prefab / Inspector Changes

None. The production scene was not saved or modified. The optional photo-session scene could not be generated because Unity licensing failed before project import.

When run in a licensed Editor, the helper creates or refreshes only `Assets/Scenes/Kartikaris_PortfolioPhotoSession.unity`. It serializes the staged mech, isolated lighting adjustment, and three external camera transforms/FOVs in that copy.

## Implementation Details

- Adds menu commands under `Kartikaris/Portfolio Photo Session` to create the isolated fixture and capture three native 1600×900 PNG drafts.
- Copies the authoritative scene instead of editing it in place.
- Disables Player and MapGenerator gameplay MonoBehaviours, CharacterControllers, audio, Rigidbody motion/collision, live particles, original cameras, Enemy, cockpit, Canvas, and EventSystem in the copy.
- Preserves Terrain rendering, the existing TerrainData/tree instances, real mech renderers/materials, sky, Global Volume, Directional Light, soft shadows, and URP rendering.
- Searches the existing Terrain for a relatively flat nearby clearing, grounds the real Player mech, and evaluates the existing `cannon` Animator bool into a frozen deployed pose when that parameter is available.
- Creates saved Hero, Environment, and Cannon cameras, all disabled by default and excluding cockpit/UI layers 7–9.
- Writes local drafts to ignored `Temp/PortfolioPhotoSession/` so unapproved images are not committed or published.

## Decisions and Rationale

The workflow is editor-only and explicit because portfolio photography must not alter normal gameplay, camera ownership, AI, artillery, controls, procedural generation, or UI. The fixture is rebuilt from the authoritative scene so it continues to use genuine project assets while remaining isolated. Manual scene-YAML construction was rejected after the Unity license failure because it would create an unverified scene blindly.

## Validation Performed

- Verified Git repository `Shikirashi/Kartikaris-Unity`, branch `main`, clean initial status, and `origin/main`; `git pull --ff-only origin main` reported already up to date.
- Confirmed `ProjectSettings/ProjectVersion.txt` specifies `6000.3.9f1 (7a9955a4f2fa)` and that exact editor version is installed.
- Confirmed X11 display `:0`, direct accelerated rendering, and Radeon RX 6700 XT availability.
- Built the editor helper through the generated `Assembly-CSharp-Editor.csproj` against installed Unity `6000.3.9f1` assemblies: zero errors and one unrelated pre-existing Mirza Beig `DemoController.camera` warning. The temporary generated-project inclusion was removed afterward.
- Launched exact Unity `6000.3.9f1` in batch mode with the real GPU/OpenGL path and the helper execute method. Unity exited before project import with `No valid Unity Editor license found`; no render occurred.
- Confirmed the authoritative scene SHA-256 remained `0fe661f1e541cdbc3419ceb00dc46c794121fa0c09f9a33e8f90d23363d5dbb4` and no photo-session scene or PNG was produced.

## Known Issues / Limitations

- Initial screenshots and the generated static photo-session scene are blocked until a valid Unity Editor license is activated for the installed `6000.3.9f1` editor.
- The three computed compositions have compiled but have not been visually inspected in Unity because rendering never began.
- The static deployed cannon pose, clearing selection, camera occlusion, material appearance, post-processing, and exact forest framing require review after the first licensed render.

## Manual Follow-up

Activate a valid Unity Editor license, then use **Kartikaris > Portfolio Photo Session > Create or Refresh Static Scene** followed by **Capture 1600x900 Drafts**. Review the generated scene and the three images before selecting or manually adjusting a composition. Exact steps and the optional batch command are recorded in `Docs/PortfolioPhotoSession.md`.

## Suggested Next Step

After license activation, generate and visually inspect candidates A/B/C, make only isolated camera/mech/light adjustments in `Kartikaris_PortfolioPhotoSession.unity`, and return the real screenshots for human selection without publishing them to the portfolio repository.
