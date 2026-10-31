# Simulated Tracked / Roller-Foot Locomotion

## Summary

Player and Enemy now behave like kinematic tracked vehicles with powered roller-foot sides rather than applying one car-like yaw rate during every turn. A small shared `TrackedDriveModel` produces virtual left/right roller commands, pivot/moving mode, target-speed multiplier, and yaw rate. CharacterController movement, acceleration/deceleration, gravity, and virtual-tree crushing remain authoritative.

The previous behavior applied the same `45°/s` yaw rate whether stationary or translating. The new behavior uses `45°/s` only for near-stationary pivots and a gentler `18°/s` for moving course correction.

## Files Created

- `Assets/Scripts/Player/TrackedDriveModel.cs`
- `Assets/Scripts/Player/TrackedDriveModel.cs.meta`
- `Docs/ImplementationReports/2026-08-06_tracked-roller-locomotion.md`

## Files Modified

- `Assets/Scripts/Player/MechFirstPersonController.cs`
- `Assets/Scripts/Enemy/EnemyTest.cs`
- `Assets/Scenes/Kartikaris_MapTest.unity`
- `Docs/PROJECT_STATE.md`

## Scene / Prefab / Inspector Changes

The existing Player component now serializes:

- pivot turn speed: `45°/s`
- moving turn speed: `18°/s`
- stationary threshold: `0.1 m/s`
- full-moving-steer speed multiplier: `0.8`
- runtime left/right track commands and pivot-state diagnostics

The former serialized `turnSpeedDegreesPerSecond` value migrated into `pivotTurnSpeedDegreesPerSecond` through `FormerlySerializedAs`. No scene hierarchy, CharacterController geometry, camera, Canvas, radar, Terrain, forest, or tree-collision reference changed. Enemy continues to source locomotion tuning from the Player component rather than serializing duplicate rates.

## Implementation Details

### Shared tracked-drive rules

`TrackedDriveModel.Evaluate` accepts analog-friendly throttle/steering values in `[-1, +1]`, actual longitudinal speed, and the small set of movement tunings. It returns:

- `LeftTrackCommand`
- `RightTrackCommand`
- `LongitudinalSpeedMultiplier`
- `YawRateDegreesPerSecond`
- `IsPivotTurn`

Command semantics for future roller animation are:

- value greater than zero: that side's rollers should animate forward;
- value less than zero: that side's rollers should animate backward;
- zero: that side's rollers should stop.

The Player and Enemy both expose read-only `LeftTrackCommand`, `RightTrackCommand`, and `IsPivotTurning` properties. No current roller transform name is assumed and no roller animation was added.

### Straight drive and stationary pivot

```text
W:  left +1, right +1
S:  left -1, right -1

stationary A: left -1, right +1, yaw left at 45°/s
stationary D: left +1, right -1, yaw right at 45°/s
```

Pivot mode requires no longitudinal throttle and actual speed at or below `0.1 m/s`. Releasing W while still coasting therefore does not immediately switch A/D into a pivot.

### Moving tracked steering

At full keyboard steering:

```text
W+A: left  0, right +1, yaw left  at 18°/s
W+D: left +1, right  0, yaw right at 18°/s
S+A: left -1, right  0, yaw left  at 18°/s
S+D: left  0, right -1, yaw right at 18°/s
```

Intermediate analog steering progressively reduces only the inside side. Moving steering never reverses the inside side. Translation remains exclusively along `transform.forward` with signed longitudinal speed; the track outputs do not introduce lateral velocity.

Full moving steer multiplies the target forward/reverse speed by `0.8`. This provides noticeable speed loss without the frustrating 50% loss of textbook differential-drive averaging. Pivot and moving yaw are direct arcade rates; angular acceleration was deliberately not added.

### Player integration

`MechFirstPersonController` still reads the same W/S/A/D Input System composite. It evaluates the shared model before updating yaw and longitudinal target speed. Existing `6 m/s` forward, `5 m/s` reverse, `12 m/s²` acceleration, `16 m/s²` deceleration, gravity, grounded velocity, and CharacterController movement remain intact.

Mouse look remains isolated to `LookYaw`/`LookPitch`. Hull heading continues to drive the existing heading HUD naturally.

### Enemy integration

Enemy retains random startup placement, cannon preparation/dry-fire, bounded destination selection, one-metre arrival tolerance, braking, settling, CharacterController, and MechTreeCrusher.

For a large destination error while stopped, Enemy supplies steering with zero throttle to the shared model and receives an equal/opposite pivot. Once within the existing `15°` alignment window, it applies forward throttle. Residual heading error becomes proportional analog steering, so its inside side slows rather than reverses and moving yaw stays under the `18°/s` limit. Enemy reads all tracked tunings live from Player.

### Tree-collision integration

Both movement components still build only forward-axis horizontal displacement and pass it through `MechTreeCrusher.ResolveHorizontalMovement` before `CharacterController.Move`. On impact, the earliest-hit clamp prevents side sliding, the stable tree becomes inactive/falls once, longitudinal speed is set to zero, and held throttle can accelerate again on the next frame. Terrain tree colliders remain disabled.

## Decisions and Rationale

- Used one pure static helper because Player and Enemy require identical nontrivial mapping but do not need a vehicle framework.
- Used actual longitudinal speed—not only current W/S input—to select pivot versus moving steering.
- Preserved `45°/s` as the familiar pivot baseline and selected `18°/s` for visibly gentler moving correction.
- Selected `0.1 m/s` as a small stable pivot threshold within the requested range.
- Selected `0.8` target-speed retention at full moving steer to preserve course-correction usability.
- Kept yaw kinematic and directly tuned; no Rigidbody, torque, suspension, wheel collider, track force, or angular-acceleration system was introduced.
- Kept track commands independent enough for later animation while leaving CharacterController motion authoritative.

## Validation Performed

All validation used Unity `6000.3.9f1`, with process checks before and after every invocation and only one project-owning Unity process at a time. No Unity-generated directory or lock was removed during this milestone.

Saved-scene and pure-model validation passed:

- W/S equal same-sign commands and zero yaw;
- stationary A/D opposite-sign commands and correct Unity yaw signs;
- forward/reverse A/D mappings exactly as documented above;
- no moving-steer roller reversal;
- analog partial-inside-side reduction;
- coasting above `0.1 m/s` remains moving steering;
- below-threshold steering enters pivot mode;
- `45°/s > 18°/s` and full-steer speed multiplier `0.8`;
- Player/Enemy shared tuning and retained CharacterController/tree components;
- FrontFeed RenderTexture, CockpitCamera, radar, sound propagation, heading/elevation HUD, and zero missing scripts.

Result:

```text
KARTIKARIS_TRACKED_DRIVE_SETUP_OK
```

Live production-scene Play Mode acceptance then verified:

- W-only forward translation and S-only reverse translation with no yaw;
- stationary D produced `+9°` in `0.2 s`; stationary A produced `-9°` in `0.2 s`, both exactly `45°/s` with negligible horizontal translation;
- W+A produced `-1.8°` in `0.1 s` and S+D produced `+1.8°` in `0.1 s`, exactly `18°/s`, with correct non-reversing commands;
- coasting D stayed in moving steering until actual speed fell below `0.1 m/s`, then entered pivot mode without a translation jump;
- controlled Player and Enemy tree impacts each clamped without more than `0.02 m` lateral offset, zeroed longitudinal speed, and deactivated one active tree;
- Enemy completed a real dry-fire, pivoted for a forced 25 m lateral destination, transitioned to positive-only moving correction, retained Player speed bounds, and remained within the existing 100 m relocation limit.

```text
KARTIKARIS_TRACKED_DRIVE_PLAYMODE_OK pivot=45.0 moving=18.0 threshold=0.10 turnMultiplier=0.80 enemyDryFires=1
```

Temporary editor validation code and metadata were removed afterward. The headless `-nographics` run emitted the existing render-pass warnings but no tracked-drive exception or failed assertion.

## Known Issues / Limitations

- Track commands are not animated because verified left/right roller transforms do not yet exist.
- Enemy still has no path planning, avoidance steering, or reverse recovery; it crushes encountered virtual trunks.
- Pivot/moving rates and full-steer speed retention are prototype defaults pending subjective controller/hardware feel testing.
- Direct yaw-rate changes have no angular acceleration or rotational inertia.
- CharacterController motion approximates tracked behavior and intentionally is not physically exact differential drive.

## Manual Follow-up

1. Test W/S straight travel and inspect both runtime track commands.
2. Compare stationary A/D pivots with W/S+A/D moving corrections; the latter should feel clearly gentler.
3. Release W while holding a steering key and inspect the `0.1 m/s` transition into pivot mode.
4. Test full and brief steering taps in forward and reverse for comfortable `0.8` speed retention.
5. Drive both straight and turning impacts into representative trees and confirm no side-slide regression.
6. Observe multiple Enemy relocations for pivot-to-moving visual consistency.

## Suggested Next Step

Once the redesigned model contains verified left/right roller transforms, add a small visual-only animator that converts each side's exposed command into roller angular velocity. Keep it independent of CharacterController authority and do not infer transform names before the model is ready.
