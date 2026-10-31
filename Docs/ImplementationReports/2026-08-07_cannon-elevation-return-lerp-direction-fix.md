# Cannon Elevation Return Lerp and Direction Fix

## Summary

Replaced the immediate elevation reset when leaving Fire with an exact-duration linear return, and reversed the procedural elevation offset so player-controlled lowering moves toward the mech's forward side on the current rig.

## Files Created

- `Docs/ImplementationReports/2026-08-07_cannon-elevation-return-lerp-direction-fix.md`

## Files Modified

- `Assets/Scripts/Combat/KartikarisCannonWeapon.cs`
- `Docs/PROJECT_STATE.md`

## Scene / Prefab / Inspector Changes

None. This was a code-only change and preserves all existing assignments.

## Implementation Details

- Physical Fire elevation remains `0°..90°`, with the Animator-authored vertical pose as `90°` and zero procedural offset.
- The procedural selected-axis offset now uses `90° - physicalElevation`, reversing the previous direction without changing the assigned pivot or axis.
- `BeginTravelReset()` captures both the current visual heading and elevation instead of immediately restoring the elevation base.
- During `TransitioningToTravel`, heading follows its existing shortest-route interpolation and elevation linearly returns from the captured aim to `90°` over the same configured transition duration.
- The diminishing elevation offset continues to compose over the Animator's changing folding base without accumulating into that base.
- At Travel completion, a final zero offset is resolved against the currently observed Animator/base pose before procedural elevation is suppressed.
- Deployment behavior, Fire entry reset, W/S semantics, normal and precision steps, repeat timing, firing, projectiles, VFX, sound, explosions, tree interaction, and HUD formatting are unchanged.

## Decisions and Rationale

Using the already configured mode-transition duration makes the return predictable and removes the one-frame jump without adding another tuning field. Reversing the offset calculation changes only the procedural rig direction; Player-facing elevation and HUD semantics remain horizontal `0°`, vertical `90°`.

## Validation Performed

- `Assembly-CSharp.csproj` built with zero errors.
- The build retained one unrelated existing warning from Mirza Beig's `DemoController.camera` member hiding `Component.camera`.
- Static inspection confirmed Travel reset captures the current visual elevation, interpolates it to vertical using the same normalized transition progress as heading, and does not immediately restore the elevation base. Completion resolves zero offset through the drift-safe pose path before suppression.
- Static inspection confirmed both the diagnostic offset and applied quaternion use the reversed `90° - physicalElevation` calculation.
- Unity Editor, batch mode, and Play Mode were not run.

## Known Issues / Limitations

- Forward lowering depends on the current selected local axis matching the reported rig orientation; this needs visual confirmation.
- The return offset and folding animation now run concurrently. Their combined visual path depends on the authored clip and must be checked in Play Mode.
- Repository git metadata remains unavailable, so a normal git diff/status inspection could not be performed.

## Manual Follow-up

- Enter Fire, press S, and confirm the barrel lowers toward the mech's forward side; W should return it toward vertical.
- Leave Fire from several elevations and confirm the barrel blends smoothly with no immediate snap.
- Confirm both heading and elevation finish their resets exactly when `modeTransitionDuration` ends.
- Repeat several deploy/aim/fold cycles and confirm the final folded pose does not drift.

## Suggested Next Step

Run the focused Play Mode checks above. If lowering is still reversed, report the selected elevation axis and whether the pivot uses a mirrored transform before changing any Inspector setup.
