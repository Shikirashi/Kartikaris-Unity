# KART-UNITY-PHOTO-001 — Mech and forest portfolio photo session

**Status:** QUEUED — photo session planned and user-approved, but **no screenshot captured yet**.
**Source repository:** `Shikirashi/Kartikaris-Unity`, `main`.
**Primary scene:** `Assets/Scenes/Kartikaris_MapTest.unity`.
**Unity editor:** `6000.3.9f1` (Unity 6.3 LTS), from `ProjectSettings/ProjectVersion.txt`.
**Relevant project state:** `Docs/PROJECT_STATE.md`, `AGENTS.md`.

## Objective
Produce a truthful, striking portfolio hero screenshot showing the **actual detailed Kartikaris mech** within the **actual procedural rolling landscape and dense Synty forest**. The mech should be the visual subject, not hidden inside the first-person cockpit, while the forest clearly communicates the scale and setting.

This is a **photo session**, not authorization to redesign the game's normal camera, mech controls, terrain/forest generation, AI artillery, weapons, UI, simulation, performance settings, or visuals.

## Existing scene — verified references
- `Kartikaris_MapTest.unity` contains `MapGenerator`, Terrain, the directional light, the Player and its `Kartikaris Mech` child, an Enemy with a duplicate mech visual, `ExteriorCameraMount/FrontFeedCamera`, and a separate cockpit/CockpitCamera and HUD setup.
- `KartikarisForestGenerator` procedurally places approximately **25,000 tree instances** using the existing forest prefabs. Do not replace forest with imaginary assets or one-off generated promotional art.
- The normal game is cockpit/first-person-focused. A **separate, opt-in external screenshot camera** or saved capture scene is preferable to changing the gameplay camera.
- Do not open or save this project using Unity 6000.5; honor the canonical 6000.3.9f1 version.

## Pass 1 — Codex setup and draft frames
1. Verify `git status` and current branch; read `AGENTS.md`, `Docs/PROJECT_STATE.md`, and this task. Preserve all user-local and scene/prefab/Inspector edits; `git pull --ff-only` only when safe.
2. Confirm the actual scene/prefabs and available Unity editor (prefer 6000.3.9f1). If graphical Unity isn't available, inspect assets and prepare a **script-only optional capture helper plus exact manual steps**; label screenshots NOT CAPTURED. Do not fabricate images or edit a Unity scene blindly.
3. Use existing Player mech model (or Enemy mech if it composes better), place an external camera at an attractive **3/4 perspective** with visible full mech silhouette, and keep recognizably forested terrain extending behind it. Prefer foreground/model framing around one-third to one-half of image with convincing depth, shadows and sky.
4. Offer **2–3 distinct candidate shots** using real project materials/models:
   - **A. Hero:** mech prominent foreground, dense forest and rolling hills background.
   - **B. Scale:** slightly wider scenic angle showing terrain scale and tree density.
   - **C. Action-ready:** mech posed as if preparing artillery with readable cannon silhouette (only if an actual safe pose exists; do not invent firing or effects).
5. No Inspector/scene changes in the authoritative scene without user approval. If an opt-in camera prefab, editor menu, runtime capture script or screenshot staging scene is implemented, keep it clearly isolated and disabled by default. Do not inadvertently capture the cockpit-only UI, radar Canvas, debug gizmos or editor toolbars over the exterior shot.

## Pass 2 — Human visual direction
- User selects A/B/C or opens Unity and takes a low-resolution manual screenshot showing the desired camera angle/model placement.
- Save camera transform, projection/FOV, target/look-at, player/enemy mech transform/pose, terrain regeneration seed if relevant, lighting, exposure, viewport aspect ratio and anything else necessary to reproduce the user-approved composition.
- **A manual 720p shot is a valid final if sharp enough; don't chase 4K.** Preserve composition and proportions during reshooting.

## Pass 3 — High-quality capture
- Preferred native output **1600×900** or **1920×1080**. **1280×720 native is acceptable**, especially if clear at website card size. Do not upsample a small bitmap and claim it's a genuine higher-resolution render.
- Capture using Unity Game view or a narrowly-scoped isolated camera/RenderTexture/ReadPixels pipeline that avoids modifying the main scene and preserves correct model/lighting/terrain appearance.
- Deliver one user-approved hero and two alternative actual screenshots; suggested names `kartikaris_hero_01.png`, `kartikaris_alt_01.png`, `kartikaris_alt_02.png`.
- Prefer keep raw/draft captures local until the human selects one; only optimized final public image transfers to portfolio repo under `assets/img/gallery/`.

## Validation and handoff
- Confirm the capture is *from real Unity mech + forest*, not from newer `Kartikaris-Godot` placeholder geometry or from AI-generated concept art.
- Verify the original `Kartikaris_MapTest.unity` still boots and unchanged default game mechanics stay intact, to the degree possible in the actual environment. Do not claim playtest/render success from static checks.
- If source implementation or scene/configuration is modified to support capture, follow `AGENTS.md`: **update relevant `Docs/PROJECT_STATE.md` sections** and create a dated `Docs/ImplementationReports/YYYY-MM-DD_<task>.md` with actual validation. Documentation-only planning does not require an implementation report.
- Report actual source commit, screenshots and dimensions, chosen scene, exact capture steps, what the user must verify, and any blockers. Do not publish gallery or change other repositories from this task.

## Exact Codex prompt (use when executing this task)

```text
Work in Shikirashi/Kartikaris-Unity on main. This is the real legacy Unity mech+forest project, not Kartikaris-Godot. Read AGENTS.md, Docs/PROJECT_STATE.md and Docs/PortfolioPhotoSession.md. Use exact Unity 6000.3.9f1 and preserve Assets/Scenes/Kartikaris_MapTest.unity, the actual Kartikaris Mech model and the procedurally generated forest (~25k trees). Prepare 2–3 real in-engine portfolio screenshot candidates: mech foreground at a clear cinematic three-quarter angle, actual forest hills behind, optional real cannon pose. Prefer a separate opt-in exterior capture camera or staging scene that cannot disrupt the cockpit and game. The user will approve or manually adjust framing before final capture. Native 1280×720 minimum is fine; 1600×900/1920×1080 optional. If Unity GUI/rendering isn't available, provide exact manual composition/capture instructions and clearly report no screenshots taken. Follow AGENTS.md's project-state/report requirements for any implementation edits, verify realistically, commit only scoped work and stop for human photo review. No gameplay redesign, Unity upgrade, or fabricated screenshot.
```
