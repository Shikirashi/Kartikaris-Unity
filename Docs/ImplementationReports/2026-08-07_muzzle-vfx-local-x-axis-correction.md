# Muzzle VFX Local-X Axis Correction

## Summary

Corrected the muzzle-flash orientation so the prefab's local X axis, rather than local Y, aligns with `firePoint.forward`.

## Files Created

- `Docs/ImplementationReports/2026-08-07_muzzle-vfx-local-x-axis-correction.md`

## Files Modified

- `Assets/Scripts/Combat/KartikarisCannonWeapon.cs`
- `Docs/PROJECT_STATE.md`

## Scene / Prefab / Inspector Changes

None.

## Implementation Details

- Muzzle rotation now uses `Quaternion.LookRotation(-firePoint.right, firePoint.up)`.
- This maps muzzle local Z to `-firePoint.right`, local Y to `firePoint.up`, and therefore local X to `firePoint.forward`.
- Shell local Z and launch velocity remain aligned with `firePoint.forward`.
- The adjustable muzzle local-position offset, lingering smoke pose, cooldown, and sound behavior are unchanged.

## Decisions and Rationale

Keeping muzzle local Y aligned with `firePoint.up` supplies an unambiguous roll reference while satisfying the requested local-X emission direction.

## Validation Performed

- `Assembly-CSharp.csproj` built with zero errors.
- The build retained one unrelated existing warning from Mirza Beig's `DemoController.camera` member hiding `Component.camera`.
- Static axis-basis inspection confirmed local X maps to fire-point forward.
- Unity Editor and Play Mode were not run.

## Known Issues / Limitations

- Final appearance depends on the muzzle prefab actually being authored to emit along local X.

## Manual Follow-up

Fire at multiple heading/elevation angles and confirm the muzzle effect emits along the barrel while retaining stable roll.

## Suggested Next Step

Tune only `Muzzle Vfx Local Position Offset` after confirming the corrected prefab axis visually.
