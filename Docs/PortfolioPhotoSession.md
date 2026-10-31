# KART-UNITY-PHOTO-001 — Mech and forest portfolio photo session

**Status:** TWO FINAL GALLERY SHOTS APPROVED — `kartikaris_alt2_folded_02.png` (travel/folded) and `kartikaris_alt2_unfolded_30deg.png` (deployed, aimed 30° above horizontal). Earlier upright `kartikaris_alt2_unfolded_02.png` retained only as a backup. Images have NOT been published to the website.
**Source repository:** `Shikirashi/Kartikaris-Unity`, `main`.
**Primary scene:** `Assets/Scenes/Kartikaris_MapTest.unity`.
**Unity editor:** `6000.3.9f1` (Unity 6.3 LTS), from `ProjectSettings/ProjectVersion.txt`.
**Relevant project state:** `Docs/PROJECT_STATE.md`, `AGENTS.md`.

## Photo-session operating rule (updated 1 November 2026)

**Everything needed for the photograph must be arranged STATIC and NON-HOSTILE before capture.** Codex must prepare the scene and **take the first 2–3 screenshots**, not ask the user to time actions. Disable enemy behavior, firing, artillery, tree destruction and live actor movement for the isolated photo fixture. Freeze mech pose/camera/forest and any non-interactive visual effects so the user can inspect and reposition the camera for as long as desired. Existing mech/enemy visuals may serve as stationary props, but not active opponents. Do not globally pause a rendering pipeline that hides forest, VFX or cameras; make an explicit opt-in stage isolated from play. If no graphical Unity session is available, report capture BLOCKED and provide manual static staging steps. Preserve gameplay and original scene. Only use real project assets; no fabricated effects.

## Objective
Produce a truthful, striking portfolio hero screenshot showing the **actual detailed Kartikaris mech** within the **actual procedural rolling landscape and dense Synty forest**. The mech should be the visual subject, not hidden inside the first-person cockpit, while the forest clearly communicates the scale and setting.

This is a **photo session**, not authorization to redesign the game's normal camera, mech controls, terrain/forest generation, AI artillery, weapons, UI, simulation, performance settings, or visuals.

## Existing scene — verified references
- `Kartikaris_MapTest.unity` contains `MapGenerator`, Terrain, the directional light, the Player and its `Kartikaris Mech` child, an Enemy with a duplicate mech visual, `ExteriorCameraMount/FrontFeedCamera`, and a separate cockpit/CockpitCamera and HUD setup.
- `KartikarisForestGenerator` procedurally places approximately **25,000 tree instances** using the existing forest prefabs. Do not replace forest with imaginary assets or one-off generated promotional art.
- The normal game is cockpit/first-person-focused. A **separate, opt-in external screenshot camera** or saved capture scene is preferable to changing the gameplay camera.
- Do not open or save this project using Unity 6000.5; honor the canonical 6000.3.9f1 version.

## Pass 1 — Codex static stage setup and first screenshots
1. Verify `git status` and current branch; read `AGENTS.md`, `Docs/PROJECT_STATE.md`, and this task. Preserve all user-local and scene/prefab/Inspector edits; `git pull --ff-only` only when safe.
2. Confirm the actual scene/prefabs and available Unity editor (prefer 6000.3.9f1). If graphical Unity isn't available, inspect assets and prepare a **script-only optional capture helper plus exact manual steps**; label screenshots NOT CAPTURED. Do not fabricate images or edit a Unity scene blindly.
3. Use existing Player mech model (or Enemy mech if it composes better), place an external camera at an attractive **3/4 perspective** with visible full mech silhouette, and keep recognizably forested terrain extending behind it. Prefer foreground/model framing around one-third to one-half of image with convincing depth, shadows and sky.
4. Offer **2–3 distinct candidate shots** using real project materials/models:
   - **A. Hero:** mech prominent foreground, dense forest and rolling hills background.
   - **B. Scale:** slightly wider scenic angle showing terrain scale and tree density.
   - **C. Static silhouette:** mech safely posed with a readable cannon and forest background, with all simulation and hostile AI disabled in the isolated capture setup.
5. No Inspector/scene changes in the authoritative scene without user approval. If an opt-in camera prefab, editor menu, runtime capture script or screenshot staging scene is implemented, keep it clearly isolated and disabled by default. Do not inadvertently capture the cockpit-only UI, radar Canvas, debug gizmos or editor toolbars over the exterior shot.

## Prepared opt-in capture workflow

`Assets/Editor/KartikarisPortfolioPhotoSession.cs` is the prepared editor-only helper. It never saves the authoritative `Kartikaris_MapTest.unity` scene. Its creation command copies that scene to `Assets/Scenes/Kartikaris_PortfolioPhotoSession.unity`, makes the copy static/non-hostile, and saves three disabled external cameras with their transforms and fields of view. The draft capture command renders real project assets to `PortfolioCaptures/PhotoSession/` at native `1600×900`:

- `kartikaris_hero_01.png` — camera A, prominent three-quarter mech composition.
- `kartikaris_alt_01.png` — camera B, wider forest/terrain scale composition.
- `kartikaris_alt_02.png` — camera C, alternate angle emphasizing the deployed cannon silhouette.

After activating a valid Unity Editor license, perform the initial setup with exact Unity `6000.3.9f1`:

1. Open this project in Unity `6000.3.9f1`; decline any upgrade prompt from another version.
2. Choose **Kartikaris > Portfolio Photo Session > Create or Refresh Static Scene**. This creates/recreates only the isolated photo scene.
3. Open `Assets/Scenes/Kartikaris_PortfolioPhotoSession.unity` and verify the `PhotoSession_STATIC_NON_HOSTILE` root, inactive Enemy/cockpit/Canvas, disabled gameplay components, stationary Player mech, Terrain/forest, Directional Light, and the three saved photo cameras.
4. Select each camera and use its Camera Preview to adjust framing if desired. Save the isolated photo scene; do not run Play Mode and do not modify `Kartikaris_MapTest.unity`.
5. Choose **Kartikaris > Portfolio Photo Session > Capture 1600x900 Drafts**. Review the three PNGs in `PortfolioCaptures/PhotoSession/`.

For a fully automated initial build and capture after licensing, close other Unity instances and run:

```bash
/home/shiki/Unity/Hub/Editor/6000.3.9f1/Editor/Unity \
  -batchmode \
  -projectPath /home/shiki/Documents/GitHub/Kartikaris-Unity \
  -executeMethod Kartikaris.Editor.KartikarisPortfolioPhotoSession.BuildAndCaptureDrafts \
  -force-glcore \
  -logFile /tmp/kartikaris-photo-session.log
```

The automated entry point recreates the isolated scene, so use the menu-only capture command after making manual camera adjustments.

## Initial draft result — 1 November 2026

The licensed Unity `6000.3.9f1` CLI created `Assets/Scenes/Kartikaris_PortfolioPhotoSession.unity` and rendered all three candidates at native `1600×900`. Draft PNGs remain local and ignored under `PortfolioCaptures/PhotoSession/`; none were published or committed.

The real Player mech is saved at world position `(-32.695, 40.058, -96.707)` and rotation `(0, 28, 0)`. The copied fixture uses a private duplicate of the generated TerrainData with 24,968 of the original 25,000 trees. Thirty-two trees were removed only from local camera sight corridors in that duplicate after visual inspection showed foreground canopies obscuring the mech; the source TerrainData and production scene are unchanged.

- **A — Hero:** position `(-34.466, 45.358, -112.079)`, rotation `(3.146, 6.750, 0)`, FOV `38°`.
- **B — Environment:** position `(-13.696, 47.404, -90.591)`, rotation `(6.840, 252.193, 0)`, FOV `47°`.
- **C — Cannon:** position `(-32.490, 46.210, -76.946)`, rotation `(3.462, 180.447, 0)`, FOV `34°`.

The isolated Directional Light is saved at rotation `(42, -28, 0)`, intensity `1.25`, warm color `(1, 0.94, 0.84)`, soft shadows at `0.9` strength, and ambient intensity `1.05`. The static Animator evaluation leaves the cannon deployed; all photo cameras remain disabled until explicitly previewed or rendered.

## Human review — 1 November 2026 (KART-UNITY-PHOTO-001 continuation)

**Confirmed push:** `04b93baba4d27199e02501dd2ede058ff8cf05ab` (initial real 1600×900 captures). The screenshots are intentionally **local, ignored**, not missing because the source commit failed. The user supplied all three screenshots for visual review.

### Shot decision
- **Preferred composition:** `kartikaris_alt_02.png` (camera `PhotoCamera_C_Cannon`, labeled alt 2). Preserve its balanced forest/background, subject size and overall framing as the baseline.
- `kartikaris_alt_01.png` works as a wider environmental backup, but the mech is small for gallery cards.
- `kartikaris_hero_01.png` hides too much of the mech behind the tall cannon; not selected.
- **Main defect:** current helper's `PoseCannon()` unconditionally sets Animator bool `cannon=true` and advances 240 frames, freezing an extremely tall vertical cannon. Without context this resembles a pole and overwhelms the robot, particularly at thumbnail size.

### Authorized follow-up — static cannon-pose variants
1. Inspect existing actual mech Animator, animation clips and articulation/pivot hierarchy. Determine **genuine travel/folded and deployed/unfolded poses** and how they are safely selected. Do not assume that blindly toggling one bool gives correct articulation; evaluate/verify both actual states visually. Never invent or redesign mechanical geometry or claim a pose is an implemented gameplay ability if it is photo-only.
2. Preserve **camera C / alt 2** as the base view. Do not use `Create or Refresh Static Scene` or automated scene recreation in a way that overwrites manually adjusted original fixture/cameras. Preserve the current scene and current three raw drafts; create **separate named capture states or saved photo fixtures** if that is safer.
3. In an opt-in, STATIC, NON-HOSTILE photo setup, make at least these genuine in-engine candidates:
   - `kartikaris_alt2_folded_01.png`: travel/folded cannon with the mech fully readable. This is the **preferred potential gallery hero**.
   - `kartikaris_alt2_deployed_01.png`: deployed/unfolded cannon in its legitimate animation pose, perhaps vertical if that's what the rig actually does.
   - `kartikaris_alt2_deployed_angle_01.png`: alternative lower or rotated cannon elevation **only if supported by the actual rig and transform constraints**; otherwise report unavailable rather than posing physically impossible joints.
   - Optional alternate slightly three-quarter camera if the folded silhouette improves.
4. Any temporary projectile, gun or actor effect must stay frozen; no live enemy AI or moving simulation. No fake motion/action.
5. Codex captures and actually inspects **first candidate screenshots** with Unity `6000.3.9f1`, native `1600×900` where available, saves camera and cannon transform/animator parameters, and reports what articulation is possible. Do not claim a screenshot exists without inspecting a real rendered file.
6. The user selects the final hero from the new variants. **Do not publish to the website yet.**
7. If code/scene/editor tooling changes, update `Docs/PROJECT_STATE.md` and add a factual report under `Docs/ImplementationReports/` per `AGENTS.md`; confirm original `Assets/Scenes/Kartikaris_MapTest.unity` and `Assets/New Terrain.asset` are unchanged. Keep captures outside Git until final approved public image is transferred to portfolio repository.

**Execution:** This is the next review-correction pass of the same task, not a new gameplay feature. Continue and stop after screenshots + documentation + scoped commit/push for another human review.

## Alt 2 cannon-variant pass — 1 November 2026

Use **Kartikaris > Portfolio Photo Session > Capture Alt 2 Cannon Pose Variants** to render the current six-file set without saving the photo scene. The command renders deployed candidates first, then reopens the isolated scene before attempting the folded pair. This split is intentional: Unity's disabled edit-mode Animator did not reliably transition from an already-rendered deployed pose back to folded in the same scene session.

All outputs are native `1600×900` PNGs under ignored local directory `PortfolioCaptures/PhotoSession/`:

| File | Intended pose | Camera position | Camera rotation | FOV | Pose offset |
| --- | --- | --- | --- | --- | --- |
| `kartikaris_alt2_folded_01.png` | Folded anchor | `(-32.490, 46.210, -76.946)` | `(3.462, 180.447, 0)` | `34°` | Animator `cannon=false`, evaluated in a freshly opened scene before its first render |
| `kartikaris_alt2_folded_02.png` | Folded alternate | `(-34.890, 46.560, -76.928)` | `(11.120, 173.512, 0)` | `35°` | Same folded attempt; minor camera shift/look-at only |
| `kartikaris_alt2_unfolded_01.png` | Deployed anchor | `(-32.490, 46.210, -76.946)` | `(3.462, 180.447, 0)` | `34°` | Saved static Alt 2 pose; no offset |
| `kartikaris_alt2_unfolded_02.png` | Deployed alternate | `(-30.290, 46.710, -76.964)` | `(7.308, 186.809, 0)` | `35°` | Saved static pose; minor camera shift/look-at only |
| `kartikaris_alt2_unfolded_elev78_az12.png` | Deployed orientation candidate | `(-32.490, 46.210, -76.946)` | `(3.462, 180.447, 0)` | `34°` | Heading `+12°`, elevation-pivot offset `+12°` |
| `kartikaris_alt2_unfolded_elev84_az-12.png` | Deployed orientation candidate | `(-32.490, 46.210, -76.946)` | `(3.462, 180.447, 0)` | `34°` | Heading `-12°`, elevation-pivot offset `+6°` |

The CLI reported successful rendering and the dimensions above. At the user's request, Codex stopped visually interpreting the images; a separate ChatGPT image review will determine whether each filename's intended cannon state is actually visible and provide concrete follow-up instructions. In particular, treat the two `folded` filenames as **unconfirmed**, not approved, until that review. No image has been published.

## ChatGPT push review — 1 November 2026 (e978e90)

- Confirmed commit `e978e9037bbbf0c85d0a1c6d33b47591fda8eb37` exists on `main` and contains scoped changes to the capture helper, task/project-state docs and new implementation report.
- Reviewed source: the helper captures deployed candidates from the anchor scene; the folded attempt reopens the isolated photo scene and uses `PoseCannon(mechVisual, false)` (Animator `cannon=false`, `Animator.Update` then disabled). This is not conclusive visual proof of folded articulation; scene-session/animation cache uncertainty remains.
- Codex documentation reports exact Unity 6000.3.9f1 exit code 0 and six local ignored 1600×900 PNG files. These are **reported results, not independent execution or pixel inspection** by ChatGPT; ignored files do not travel with the Git push.
- **Next gate:** user uploads all six local PNGs or a sufficiently clear labeled contact sheet for image review. Especially compare the two `folded` files against `unfolded_01`. If folded pose is unchanged, task remains open for a focused animation-state correction or manual posing. No portfolio publication until visual approval.

## Approved two-image gallery selection + optional 30° deployed image — 1 November 2026

The user explicitly approved **both of the images most recently shared** as portfolio images:
- `kartikaris_alt2_unfolded_02.png` — **deployed/upright cannon**, alternative camera, first screenshot in the user's pair. This is NOT the folded shot.
- `kartikaris_alt2_folded_02.png` — **folded/travel cannon**, alternate camera, second screenshot in the user's pair.

Preserve both in the portfolio gallery. They are complementary, not a hidden backup. Do not overwrite either original. Gallery should show two visible inline photographs with meaningful captions, no hover or click required. Source image files are currently **local and Git-ignored**; this documentation update alone does not publish the images.

### Additional requested static photo (captured; awaiting human review)
- Create **one** new screenshot `kartikaris_alt2_unfolded_30deg.png` from the **same camera framing as `kartikaris_alt2_unfolded_02.png`** (its alternative camera transform/FOV), with the deployed cannon at **physical elevation 30° above the horizontal**, not 30° from vertical.
- Actual articulation **is procedural**, once the deployment animation is complete. Verified in `Assets/Scripts/Combat/KartikarisCannonWeapon.cs`: `ElevationOffsetFromPhysicalDegrees(physicalElevationDegrees)` is `90f - physicalElevationDegrees`, so 30° elevation corresponds to **+60° pivot offset from the deployed vertical animation base pose** on `weapon.ElevationPivot` using `weapon.ElevationAxis`. Existing `+12°` and `+6°` offsets correspond to approximately 78° and 84° physical elevation, respectively.
- Preserve the deployed animation pose; **do not** attempt to play the fold animation to lower the cannon. Do not alter runtime `KartikarisCannonWeapon` gameplay code or aim limits just for the picture.
- For the **30° image**, first restore known deployed pivot rotations, then apply the calculated +60° procedural offset **once**, rather than accumulating on earlier variants. Read the existing helper `ApplyStaticCannonOffset`. Verify actual muzzle elevation in world coordinates by inspecting `FirePoint.forward` and using the horizontal/reference up normal, since local pivot orientation or nested hierarchy could change the sign. Validate and report whether measured angle is close to 30°, adjust the photo-only stage if needed. Avoid unnatural intersections between cannon/mech.
- The image must remain completely **static, non-hostile**, with the mech and existing forest, no firing, AI, or timed state changes. Use Unity `6000.3.9f1`, genuine native 1600×900 rendering, and **capture/inspect the image first** before reporting visual success.
- Preserve already-selected images, original capture filenames and photo scene, original `Kartikaris_MapTest.unity`, and `Assets/New Terrain.asset`. Changes must be limited to isolated editor photo capture tooling and the project task/report documentation. Stop for human visual review of this third candidate; no publication without the site's Gallery task.

**Codex instruction:** Work in `Shikirashi/Kartikaris-Unity` on `main`. Read `AGENTS.md`, `Docs/PROJECT_STATE.md`, and this photo task. Extend the existing opt-in editor capture helper to render a single deployed cannon at exactly 30° physical elevation from horizontal using procedural pivot articulation. Reuse the `alt2_unfolded_02` camera; do not overwrite the six old PNGs; output `kartikaris_alt2_unfolded_30deg.png`, report measured muzzle angle and capture evidence, follow project-state/implementation-report workflow, commit and push only scoped code/docs, and stop for human review.

### 30° capture result — 1 November 2026

- Output: `PortfolioCaptures/PhotoSession/kartikaris_alt2_unfolded_30deg.png`, native `1600×900`, local and Git-ignored.
- Camera: position `(-30.290, 46.710, -76.964)`, rotation `(7.308, 186.809, 0)`, FOV `35°`; this matches `kartikaris_alt2_unfolded_02`.
- Pose: saved deployed animation base plus one `+60°` procedural offset on the serialized X-axis elevation pivot. No heading offset was applied.
- Verification: the helper measured `asin(dot(FirePoint.forward, referenceUp)) = 30.048°` immediately before capture and rejects values outside `30° ± 0.5°`.
- Visual inspection: real deployed Kartikaris mech and forest are present; the cannon is visibly lowered; composition and lighting remain intact; no obvious cannon/mech intersection, firing, motion, UI, or render defect is visible.
- Preservation: the approved `kartikaris_alt2_unfolded_02.png` and `kartikaris_alt2_folded_02.png` retained their earlier timestamps and were not overwritten. No scene was saved and no image was published.

## Final image selection — 1 November 2026 (supersedes earlier upright selection)

The user supplied and explicitly approved the new `kartikaris_alt2_unfolded_30deg.png` image as the **deployed-cannon photo** for the portfolio. Retain prior approval of `kartikaris_alt2_folded_02.png` as the **folded/travel-cannon photo**. This **replaces** the earlier selection of the fully upright `kartikaris_alt2_unfolded_02.png` for the live gallery; that file remains an unselected backup.

Publish exactly these two **static, immediately visible** photographs side by side on desktop (stacked on mobile), with captions "Cannon deployed — 30° elevation" and "Cannon folded — travel configuration". Do not insert the vertical-cannon backup instead. Both were genuinely captured in Unity `6000.3.9f1` at native 1600×900. Codex's `a88c528` report measured the new shot at `30.048°` using the actual muzzle direction. Preserve original names as source references; optimized derivatives for the Gallery may use descriptive filenames.

**Important publication gate:** Raw files are local under Git-ignored `PortfolioCaptures/PhotoSession/`; this documentation update does not upload images or publish the Gallery. Confirm both binary assets exist in the portfolio build before deployment. No more Kartikaris photo iteration required unless requested.

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
Work in Shikirashi/Kartikaris-Unity on main. This is the real legacy Unity mech+forest project, not Kartikaris-Godot. Read AGENTS.md, Docs/PROJECT_STATE.md and Docs/PortfolioPhotoSession.md. Use exact Unity 6000.3.9f1 and preserve Assets/Scenes/Kartikaris_MapTest.unity, the actual Kartikaris Mech model and the procedurally generated forest (~25k trees). Prepare a STATIC, non-hostile opt-in scene with a stationary mech, static forest and no active AI/fire/simulation; take 2–3 real in-engine portfolio screenshot candidates FIRST: mech foreground at a clear cinematic three-quarter angle, actual forest hills behind, optional stationary cannon pose. Prefer a separate opt-in exterior capture camera or staging scene that cannot disrupt the cockpit and game. The user will approve or manually adjust framing before final capture. Native 1280×720 minimum is fine; 1600×900/1920×1080 optional. If Unity GUI/rendering isn't available, provide exact manual composition/capture instructions and clearly report no screenshots taken. Follow AGENTS.md's project-state/report requirements for any implementation edits, verify realistically, commit only scoped work and stop for human photo review. No gameplay redesign, Unity upgrade, or fabricated screenshot.
```
