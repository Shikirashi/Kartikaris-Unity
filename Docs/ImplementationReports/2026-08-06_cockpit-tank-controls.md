# Cockpit-Centric Tank Controls

## Summary

Reworked the existing `MechFirstPersonController` from conventional FPS movement/look into the first cockpit-centric tank-control pass:

```text
W / S   signed forward and reverse throttle
A / D   physical mech hull turn left and right
Mouse   pilot head yaw and pitch inside the isolated cockpit
Escape  release cursor
Click   recapture cursor
```

The physical mech still uses its existing `CharacterController`, gravity, acceleration/deceleration, and tree-crushing integration. There is no strafing, mouse-driven hull yaw, mouse-driven exterior-camera aiming, jump, sprint, crouch, Rigidbody track simulation, cannon control, or cockpit interaction.

## Observed Existing Behavior

Before this change, `MechFirstPersonController` created one normalized 2D WASD action and converted both axes into a local movement vector. W/S moved forward/reverse, while A/D produced lateral strafe velocity. The movement vector was transformed by Player orientation and accelerated as a full horizontal velocity vector.

Mouse X directly rotated the physical Player root. Mouse Y rotated the serialized `cameraPivot`. In the actual saved scene that reference pointed to `Player/ExteriorCameraMount`, so mouse pitch aimed `FrontFeedCamera`; horizontal mouse movement steered the mech itself. `CockpitCamera` was on a separate static `cockpit/CameraPivot` and had no look owner.

The existing cursor behavior already locked on startup, released with Escape, recaptured with left click, and ignored look input while unlocked. That behavior was preserved.

The scene contains two objects named `cockpit`: a scene-stage root and an imported model child. Scene hookup therefore resolved the stage by enumerating scene roots rather than relying on an ambiguous name-only lookup.

## Files Created

- `Docs/ImplementationReports/2026-08-06_cockpit-tank-controls.md`

## Files Modified

- `Assets/Scripts/Player/MechFirstPersonController.cs`
- `Assets/Scenes/Kartikaris_MapTest.unity`
- `Assets/FrontVision.renderTexture` — Unity refreshed the source serialization timestamp while exercising the live render target; its inspected `1280×720` settings and scene GUID reference remain intact.
- `Docs/PROJECT_STATE.md`

No package, ProjectSettings, prefab, TerrainData, tree, enemy, radar, monitor material, imported cockpit model, or Synty asset was intentionally modified.

## Scene / Prefab / Inspector Changes

The former cockpit `CameraPivot` was reused as the yaw pivot and renamed. One pitch pivot was inserted, producing:

```text
cockpit (scene stage)
└── LookYaw
    └── LookPitch
        └── CockpitCamera
```

Both pivots start at identity rotation. `CockpitCamera` remains at local position/rotation zero beneath `LookPitch`. The physical Player controller now references these two cockpit-stage transforms even though they are outside the Player hierarchy.

Configured values are:

- mouse sensitivity: `0.12`;
- cockpit yaw limits: `-130°` to `+130°`;
- cockpit pitch limits: `-70°` to `+70°`;
- forward speed: `6 m/s`;
- reverse speed: `5 m/s`;
- acceleration: `12 m/s²`;
- deceleration: `16 m/s²`;
- hull turn speed: `45°/s`;
- gravity: `-25 m/s²`;
- grounded vertical velocity: `-2 m/s`.

`Player/ExteriorCameraMount/FrontFeedCamera` remains unchanged structurally, with identity local rotations and the existing `FrontVision.renderTexture`. Since the mount remains a Player child, it follows A/D hull rotation naturally but receives no mouse rotation.

The existing layer-9 VisionHUD Canvas was changed from Screen Space Overlay to Screen Space Camera and explicitly assigned to `CockpitCamera` at plane distance `1`. `CockpitCamera` includes layer 9; `FrontFeedCamera` excludes it, so the HUD remains pilot/head-mounted and is not embedded in the monitor RenderTexture.

The radar World Space Canvas, its camera/layer isolation, and all radar references were preserved.

## Implementation Details

### Tank locomotion

The existing 2D Input System action remains, but uses digital/non-normalized composite mode so simultaneous throttle and turning retain full independent axis values. Its semantic mapping is:

```text
input.y  throttle: W = +1, S = -1
input.x  hull turn: A = -1, D = +1
```

A/D apply local-Y rotation directly to the physical Player at `turnSpeedDegreesPerSecond`; turning is independent of throttle and works from rest.

W/S target one signed longitudinal speed. Positive throttle scales `forwardSpeed`; negative throttle scales `reverseSpeed`. The current longitudinal speed approaches that target using the existing acceleration value and returns toward zero using the existing deceleration value. Horizontal CharacterController velocity is always:

```text
Player.transform.forward * longitudinalSpeed
```

There is no right-vector term, camera transform, or retained lateral velocity. Gravity and grounded downward bias remain part of the CharacterController move.

### Cockpit look

Mouse X accumulates and clamps local yaw on `LookYaw`. Mouse Y subtracts from and clamps local pitch on `LookPitch`, preserving the prior non-inverted convention. These are the only transforms touched by mouse-look logic.

Player, the mech visual, `ExteriorCameraMount`, and `FrontFeedCamera` are never referenced by the look update. Conversely, hull turning never reads either cockpit look transform, so pilot view direction cannot influence driving direction.

The ±130° yaw range exposes the front monitor wall, left radar wall, and right cockpit wall without permitting unrestricted rearward rotation through problematic cockpit geometry. The ±70° pitch range is deliberately a little tighter than the prior exterior-camera ±80° pitch.

### Combined behavior

When the pilot looks left, `LookYaw` holds approximately `-90°` while Player remains unchanged. Pressing D then rotates Player and its exterior feed to the right; the isolated cockpit look yaw remains locally left. The radar controller still computes positions from world X/Z only, so neither hull rotation nor pilot look rotates its north-up coordinate system.

## Decisions and Rationale

- Adapted `MechFirstPersonController` rather than replacing the component, preserving scene identity, Input System usage, cursor behavior, gravity, CharacterController movement, and tree-crushing integration.
- Kept forward and reverse speeds separate with a modest `6` versus `5 m/s` difference rather than introducing a dramatic reverse penalty.
- Used direct prototype hull rotation instead of Rigidbody or differential-track physics, as required for this milestone.
- Reused the existing cockpit pivot and added only the missing pitch pivot.
- Kept the cockpit stage stationary and independent of world travel; only the pilot look rig rotates.
- Made VisionHUD camera ownership explicit without implementing heading/elevation behavior.
- Left radar, monitor, enemy, terrain, forest, and tree interaction systems unchanged.

## Validation Performed

All Unity validation used `/home/shiki/Unity/Hub/Editor/6000.3.9f1/Editor/Unity`.

### Compilation

- The modified production controller compiled successfully.
- Scene hookup was performed and saved through Unity APIs.
- After removing temporary editor/test assets, a production-only batch compile exited `0` with no C# errors or warnings.

### Saved-scene validation

A temporary Unity editor validator passed 22 checks:

- enabled CharacterController and exactly one control owner;
- assigned isolated `LookYaw` and `LookPitch` references;
- correct `LookYaw/LookPitch/CockpitCamera` hierarchy;
- look rig outside Player and identity starting rotations;
- FrontFeedCamera parented through ExteriorCameraMount with fixed local orientation;
- retained monitor RenderTexture and direct CockpitCamera output;
- VisionHUD explicitly assigned to CockpitCamera and excluded from FrontFeedCamera;
- `6` forward, `5` reverse, and `45°/s` hull-turn tuning;
- `±130°` yaw and `±70°` pitch limits;
- intact Player radar reference and initially unspotted Enemy contact;
- north-up radar mapping unchanged by hull yaw;
- retained EnemyTest, terrain generator, forest generator, tree runtime, and MechTreeCrusher;
- zero missing scripts.

The final result was:

```text
KARTIKARIS_CONTROLS_VALIDATION_PASS checks=22 missingScripts=0
```

### Visual validation

Two GPU-backed Unity renders were captured to `/tmp` and visually inspected:

- cockpit yaw `0°` faces the forward monitor wall and shows the CockpitCamera-owned HUD;
- cockpit yaw `-90°` cleanly exposes the complete left-wall radar while the HUD remains head-mounted.

### Play Mode acceptance test

A temporary production-scene Play Mode test passed one of one tests. It directly exercised the controller's runtime movement/look steps and confirmed:

- W semantics move along Player forward;
- S semantics move opposite Player forward;
- A decreases yaw and D increases yaw;
- A/D rotate from rest without horizontal translation or strafing;
- cockpit yaw changes without changing Player yaw or ExteriorCameraMount local rotation;
- pitch clamps at `-70°` and `+70°`;
- the pilot can remain at `-90°` left yaw while D turns the hull to `+45°`;
- ExteriorCameraMount follows that hull turn through parenting;
- radar mapping remains unchanged after hull rotation.

The first headless `-nographics` test attempt was invalid because the existing FrontFeed RenderTexture could not initialize without a graphics device. It was rerun with the available GPU. The first GPU assertion also exposed that the scene still carried its old serialized ±80° pitch values despite new code defaults; scene setup then explicitly saved the intended ±70° values, after which the complete test passed. These were validation/setup issues rather than runtime control failures.

Temporary setup scripts, tests, asmdefs, directory metadata, and preview artifacts were not retained under `Assets`.

### Workspace audit

Target `git status --short --untracked-files=all` remains unavailable because the workspace has no functional `.git` worktree metadata. The concrete changes attributable to this implementation are the files listed above. A timestamp audit also showed `Assets/3D/Monitor.mat` and `Assets/Images/sound_origin_hint.png` changing during the surrounding workspace session; this implementation neither referenced nor edited them, and preserved them as user-owned/unrelated state. No temporary `Editor` or `Tests` directory remains under `Assets`.

Unity continues to log the existing Synty Terrain-tree warning about the legacy `Nature/Soft Occlusion` shader. No tree or material change was made.

## Known Issues / Limitations

- Control speed, turn rate, mouse sensitivity, and yaw/pitch limits have automated correctness coverage but still require subjective interactive tuning.
- The forward monitor RenderTexture appears black in an edit-time single-camera preview because its exterior camera is not advanced as a live Play Mode frame; its existing runtime architecture and target reference are intact.
- When the cursor is unlocked, pilot look pauses. Physical controls retain the previous behavior and may continue if movement keys are held.
- There is no track physics, speed-dependent turn rate, camera smoothing, head bob, recenter command, cockpit interaction, cannon aiming, deployment, or firing.
- Existing forest/tree, enemy animation, radar spotted-state, and sound-wedge limitations are unchanged.

## Manual Follow-up

1. Enter Play Mode in `Kartikaris_MapTest` and confirm the cursor locks automatically.
2. Drive with W/S and turn in place with A/D; verify the values feel appropriate for the mech scale.
3. Move the mouse through the forward monitor, left radar, and right wall and assess sensitivity and clamp comfort.
4. Look left at the radar, hold D, and confirm the front monitor feed turns with the hull while the pilot continues looking left.
5. Release the cursor with Escape, confirm cockpit look stops, then click to recapture it.
6. Drive through representative trees and confirm swept crushing still follows physical Player travel.
7. Use `Enemy`'s `Radar/Mark Spotted` context command and verify the north-up radar still behaves while both hull and head rotate independently.

## Suggested Next Step

Perform the interactive feel pass and tune only forward/reverse speed, hull turn rate, mouse sensitivity, or look limits if necessary. Keep cockpit buttons, handwheels, deployment, cannon aiming, and firing out of that tuning milestone.
