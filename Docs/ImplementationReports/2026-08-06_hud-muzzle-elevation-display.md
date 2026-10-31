# HUD Muzzle Elevation Display Correction

## Summary

Removed the absolute barrel-elevation articulation change and restored the previously working selected-axis aiming. Player-horizontal-plane elevation is now calculated only for the HUD display from the final muzzle direction.

## Files Created

- `Docs/ImplementationReports/2026-08-06_hud-muzzle-elevation-display.md`

## Files Modified

- `Assets/Scripts/Combat/KartikarisCannonWeapon.cs`
- `Assets/Scripts/UI/KartikarisHeadingElevationHUD.cs`
- `Docs/PROJECT_STATE.md`

## Scene / Prefab / Inspector Changes

None. Existing scene references—including the HUD's serialized elevation source—were preserved.

## Implementation Details

- Restored elevation articulation to the existing post-Animator `ApplyPivotOffset` path using smoothed logical elevation directly on the selected local axis.
- Removed horizontal projection, target-direction construction, `Vector3.SignedAngle` solving, and the elevation reference from articulation.
- After heading/elevation visual pose composition, the weapon measures muzzle elevation as `asin(dot(normalized firePoint.forward, normalized referenceUp))`.
- The optional `elevationHudReferenceFrame` supplies reference up; when empty it uses the weapon component Transform, currently Player. Thus `0°` means the barrel is parallel to Player's XZ plane, `+90°` points along Player up, and negative values point below the plane.
- Added a runtime elevation override API to `KartikarisHeadingElevationHUD`. While active, it takes precedence over the HUD's serialized elevation Transform and manual value, allowing the correct measured number without rewiring the scene.
- The weapon updates that override after the final smoothed/post-animation pose and clears it when disabled, restoring the HUD's existing transform/manual fallback behavior.
- `MeasuredMuzzleElevationDegrees` remains available on the weapon as a diagnostic and now has display-only semantics.

## Decisions and Rationale

- Aiming and presentation are separated: logical elevation continues controlling the rig exactly as before, while HUD elevation describes the physical final barrel direction.
- Measuring `firePoint.forward` automatically includes the Animator base pose, procedural heading/elevation, smoothing, and nested hierarchy without feeding any correction back into those systems.
- A dedicated runtime override avoids changing or clearing the user's existing serialized HUD elevation-source assignment.

## Validation Performed

- Built `Assembly-CSharp.csproj --no-restore --nologo` against Unity `6000.3.9f1`: zero errors and zero warnings.
- Static inspection confirmed the absolute-elevation articulation solver and its projection/signed-angle code are absent.
- Confirmed elevation again uses the same generic selected-axis quaternion path as heading, preserving smoothing, Animator base detection, and no-drift behavior.
- Confirmed measured muzzle elevation is calculated only after final pose composition and only sent to the HUD runtime override.
- Confirmed the runtime override wins over serialized transform/manual elevation and can be cleared to restore the old source path.
- Heading, transition reset, input steps, elevation clamps, firing, projectile, cooldown, sound, movement, trees, Enemy, radar, and hull-heading HUD compile unchanged.
- Unity/Play Mode was not launched. Displayed 0°/90° values and unchanged visual aiming remain manual checks.
- Git status/diff was unavailable because functional repository metadata is absent; intentional changes were accounted for through source/filesystem inspection.

## Known Issues / Limitations

- `firePoint.forward` must point along the barrel for the displayed number to be correct.
- The HUD remains clamped to its configured elevation display range, currently `-30°..+90°`.
- If the weapon component moves off Player, `Elevation Hud Reference Frame` should be explicitly assigned to Player.

## Manual Follow-up

1. Leave `Elevation Hud Reference Frame` empty while the weapon remains directly on Player; otherwise assign Player.
2. Confirm `firePoint` local-forward/blue points out of the barrel.
3. Enter Fire and verify W/S aiming moves exactly as it did before the absolute-articulation experiment.
4. Place the barrel physically horizontal and confirm the HUD and `Measured Muzzle Elevation Degrees` show approximately `0°`.
5. Place it vertically along Player up and confirm both show approximately `+90°`.
6. Change heading and repeat to verify the elevation display is independent of azimuth.

## Suggested Next Step

Run the focused Play Mode display check. If the value has the wrong sign, verify `firePoint.forward` and the Player/reference up assignment rather than changing elevation articulation.
