# Muzzle VFX Offset and Fire-Point Axis Alignment

## Summary

Added a simple adjustable local-space position offset for the muzzle-flash VFX and made the requested shell/muzzle axis relationships explicit.

The artillery shell's local Z axis and launch velocity follow `firePoint.forward`. The muzzle VFX's local Y axis follows that same fire-point Z/forward direction, while muzzle local Z uses `firePoint.up` as a stable roll reference.

## Files Created

- `Docs/ImplementationReports/2026-08-06_muzzle-vfx-offset-and-axis.md`

## Files Modified

- `Assets/Scripts/Combat/KartikarisCannonWeapon.cs`
- `Docs/PROJECT_STATE.md`

## Scene / Prefab / Inspector Changes

None. Existing scene assignments and prefab assets were not edited.

## Implementation Details

- Added serialized `muzzleVfxLocalPositionOffset` under `Shot VFX`.
- Muzzle position is calculated with `firePoint.TransformPoint(muzzleVfxLocalPositionOffset)`, making X/Y/Z values relative to the fire point. Positive local Z moves outward along the barrel direction.
- Shell spawn rotation uses `Quaternion.LookRotation(firePoint.forward, firePoint.up)`, explicitly mapping shell local Z to fire-point local Z.
- Launch velocity remains `firePoint.forward * muzzleVelocity`, matching the shell's initial visual forward axis.
- Muzzle rotation uses `Quaternion.LookRotation(firePoint.up, firePoint.forward)`, mapping muzzle local Y to fire-point local Z and muzzle local Z to fire-point local Y.
- Lingering smoke remains at `firePoint.position` with `firePoint.rotation`; it does not inherit the muzzle-only position offset.

## Decisions and Rationale

- Local-space offset is more useful than a world-space offset because it follows cannon heading and elevation automatically.
- `Quaternion.LookRotation` makes the axis mapping explicit and supplies a stable secondary direction instead of relying on a model-specific Euler correction.
- Only muzzle presentation is offset. Projectile physics, fire-point launch origin, smoke evidence location, cooldown, and sound propagation remain unchanged.

## Validation Performed

- Ran `dotnet build Assembly-CSharp.csproj` after regenerating the missing local NuGet restore metadata.
- Build succeeded with zero errors and one unrelated existing warning in `Assets/Mirza Beig/Cinematic Explosions FREE/Scripts/DemoController.cs` about its `camera` member hiding `Component.camera`.
- Static inspection confirmed the shell local-Z/velocity mapping, muzzle local-Y mapping, local offset transform, and unchanged smoke position.
- Unity Editor and Play Mode were not run.

## Known Issues / Limitations

- The assigned muzzle prefab must be authored to emit along its local Y axis for the requested mapping to look correct.
- Non-uniform scale in the fire-point hierarchy also scales the position offset because `TransformPoint` intentionally uses the full local-to-world transform.
- Final offset values and effect scale require visual tuning in Play Mode.

## Manual Follow-up

1. On `KartikarisCannonWeapon`, adjust `Muzzle Vfx Local Position Offset`; use positive Z to move the effect farther down the barrel direction.
2. Confirm the assigned muzzle VFX is authored along local Y.
3. Fire at several heading/elevation angles and confirm the shell travels along fire-point Z, the muzzle effect points along the same axis, and smoke remains at the unoffset firing position.

## Suggested Next Step

Tune the offset with the final converted URP muzzle prefab in Play Mode, then leave the fire point itself at the true projectile origin.
