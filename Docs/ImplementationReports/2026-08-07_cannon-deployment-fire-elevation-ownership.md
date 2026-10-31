# Cannon Deployment and Fire-Elevation Ownership Fix

## Summary

Removed procedural elevation from Travel and both mode transitions. The Animator now exclusively owns deployment and folding, while Fire mode layers a fresh physical `0°..90°` aim offset over the authored deployed vertical pose.

## Files Created

- `Docs/ImplementationReports/2026-08-07_cannon-deployment-fire-elevation-ownership.md`

## Files Modified

- `Assets/Scripts/Combat/KartikarisCannonWeapon.cs`
- `Assets/Scripts/Player/MechFirstPersonController.cs`
- `Assets/Scripts/UI/KartikarisHeadingElevationHUD.cs`
- `Docs/PROJECT_STATE.md`

## Scene / Prefab / Inspector Changes

None. This was a code-only change.

## Implementation Details

- `KartikarisCannonWeapon` begins with procedural elevation suppressed and only observes the Animator-written elevation pose outside Fire.
- `PrepareForFireTransition()` clears the prior firing session to logical/visual `90°` but remains suppressed throughout deployment.
- `EnterFireMode()` is called only after the Player's transition timer completes. It enables a zero procedural offset over the authored vertical Animator base.
- Fire elevation is Player-facing `0°..90°`: W raises toward the hard vertical cap, S lowers toward the configurable non-negative minimum, and physical elevation maps to the selected-axis offset `physicalElevation - 90°`.
- `BeginTravelReset()` immediately restores the cached deployed Animator base before the Player sets `cannon = false`, then leaves elevation suppressed throughout folding.
- The existing shortest-route heading reset still spans exactly the Player transition duration and remains independent of normal aim smoothing.
- Final muzzle elevation is still measured from `firePoint.forward` against the configured reference XZ plane. The HUD clamps it to `0°..90°` and formats it without a leading plus or negative zero.
- Existing aim step size, Left Alt precision, held-repeat timing, firing gates, projectiles, VFX, sound, explosions, damage, and tree interaction were left unchanged.

## Decisions and Rationale

The authored animation already defines the complete folded-to-vertical and vertical-to-folded motion. Treating that deployed vertical pose as Fire's neutral base prevents the old additional `+90°` rotation, while restoring the base before folding prevents Fire offsets from contaminating the return animation. Separating logical physical elevation from the procedural quaternion also makes `0°` horizontal and `90°` vertical unambiguous to input and HUD code.

## Validation Performed

- `Assembly-CSharp.csproj` built with zero errors.
- The build retained one unrelated existing warning from Mirza Beig's `DemoController.camera` member hiding `Component.camera`.
- Static inspection confirmed no elevation interpolation or pivot write remains in Travel or either transition state.
- Static inspection confirmed Fire entry resets to `90°` with zero offset, Travel entry restores the Animator base before changing the Animator bool, and heading reset timing remains unchanged.
- Static inspection confirmed HUD elevation ranges and formatting are consistently unsigned `0.0°..90.0°`.
- Unity Editor, batch mode, and Play Mode were not run.

## Known Issues / Limitations

- The Animator's completed deployment pose must actually place the barrel vertically.
- The selected local elevation axis must be oriented so a negative offset lowers the barrel from vertical toward horizontal.
- Existing serialized negative minimum-elevation values are sanitized to `0°` at runtime without editing the scene asset.
- Transition-boundary continuity and long-run drift require visual Play Mode verification against the assigned Animator and pivots.

## Manual Follow-up

- Deploy from Travel and confirm the animation reaches vertical without an extra rotation or a jump when Fire begins.
- Lower the barrel, return to Travel, and confirm the Fire offset disappears before folding starts.
- Repeat several deploy/aim/fold cycles from different elevations and confirm every Fire session starts vertical with no drift.
- Confirm W stops at `90.0°`, S lowers toward the configured minimum, and the HUD never displays a plus sign or `-0.0°`.
- Confirm an off-centre heading returns to Player-forward exactly when the transition timer finishes.

## Suggested Next Step

Run the focused Play Mode checks above and adjust only the selected elevation-axis direction or non-negative minimum if the visual barrel direction does not match the intended controls.
