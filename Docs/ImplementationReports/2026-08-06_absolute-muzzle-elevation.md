# Absolute Muzzle Elevation

## Summary

Changed cannon elevation semantics from a raw local-bone offset to an absolute barrel angle relative to the Player's horizontal XZ plane. Zero degrees now means `firePoint.forward` is horizontal, positive 90 degrees means it points along Player up, and the calculation remains layered over the current Animator pose and visual smoothing.

## Files Created

- `Docs/ImplementationReports/2026-08-06_absolute-muzzle-elevation.md`

## Files Modified

- `Assets/Scripts/Combat/KartikarisCannonWeapon.cs`
- `Docs/PROJECT_STATE.md`

## Scene / Prefab / Inspector Changes

None. Existing heading/elevation pivots, axes, fire point, Animator, weapon, projectile, HUD, and sound assignments were not modified.

## Implementation Details

- Added an optional explicit `elevationReferenceFrame`. Its local XZ plane defines horizontal and its up direction defines positive elevation. When unassigned, the weapon component's Transform is used; the current weapon is on Player, so no new assignment is required unless the component moves.
- Logical and smoothed visual elevation remain `-30°..+90°` by default. Input stepping and interpolation are unchanged.
- After heading is applied, the elevation pivot is restored to its detected current Animator base local rotation before measuring the muzzle. This prevents the prior procedural result from contaminating the next solve.
- Code projects the base `firePoint.forward` onto the reference XZ plane to retain current heading/azimuth, constructs a target direction from horizontal cosine plus reference-up sine at the requested elevation, converts the selected local elevation axis to world space, and uses `Vector3.SignedAngle` to calculate the required local-axis offset.
- The calculated offset is composed onto the Animator base quaternion exactly once. A nested elevation pivot therefore inherits smoothed heading while satisfying its own absolute barrel-elevation target.
- Added read-only/runtime `MeasuredMuzzleElevationDegrees`, calculated as the arcsine of the final muzzle-forward dot reference-up. This provides a direct Play Mode check that logical 0°/90° correspond to physical horizontal/vertical results.
- When `firePoint` is missing, the component safely falls back to treating visual elevation as the raw selected-axis offset; firing already requires a fire point.

## Decisions and Rationale

- Elevation is defined from the final firing direction rather than imported bone Euler values because Animator rest/deployment poses can contain arbitrary local rotation.
- Player-local up/XZ is used rather than hardcoded world up so the meaning remains attached to the Player reference frame if future movement introduces tilt.
- The solution stays local-axis based at the pivot; it does not convert the hierarchy to world rotations or bypass nested heading.
- HUD input remains the logical requested elevation, while the new measured value is a diagnostic for rig/axis correctness.

## Validation Performed

- Built `Assembly-CSharp.csproj --no-restore --nologo` against Unity `6000.3.9f1`: zero errors and zero warnings.
- Static inspection confirmed reference-frame fallback, base-pose restoration, horizontal projection, absolute target construction, selected-axis world conversion, signed-angle solving, local quaternion composition, and final muzzle-angle measurement.
- Confirmed heading is still applied before elevation; smoothing is still once per frame; heading remains unbounded; elevation clamps and 1°/0.1° steps remain; and HUD, fire-point launch, Player states/Animator, projectile, cooldown, sound, movement, trees, Enemy, and radar compile unchanged.
- Unity/Play Mode was not launched. Actual imported axis direction, fire-point ancestry, and measured horizontal/vertical outcomes require manual verification.
- Git status/diff was unavailable because functional repository metadata is absent; intentional changes were accounted for through source/filesystem inspection.

## Known Issues / Limitations

- `firePoint.forward` must accurately represent barrel direction and firePoint must inherit the elevation pivot.
- The selected elevation axis must be approximately perpendicular to the barrel's elevation plane. A misconfigured axis cannot physically reach the requested direction with one degree of freedom.
- If the base muzzle is exactly vertical, horizontal azimuth is undefined; the solver falls back to the reference frame's projected forward direction.
- HUD still shows logical requested elevation, not the smoothed measured diagnostic.

## Manual Follow-up

1. Keep `Elevation Reference Frame` empty while `KartikarisCannonWeapon` remains on Player; otherwise explicitly assign Player.
2. Confirm `firePoint` is a descendant of the elevation pivot and its local-forward/blue axis points out of the barrel.
3. Confirm the selected local elevation axis is correct.
4. In Fire mode, target `0°` and wait for smoothing. Confirm the barrel is parallel to Player's XZ plane and `Measured Muzzle Elevation Degrees` settles near zero.
5. Target `+90°` and confirm the barrel points along Player up and the measured value settles near 90.
6. Rotate heading and repeat representative elevations to confirm heading carries the nested elevation/fire point without changing elevation semantics.

## Suggested Next Step

Run the focused 0°/90° Play Mode verification. If the measured value moves in the wrong direction or cannot reach the target, correct the explicit elevation-axis selection or fire-point orientation rather than adding a model-specific sign/name workaround.
