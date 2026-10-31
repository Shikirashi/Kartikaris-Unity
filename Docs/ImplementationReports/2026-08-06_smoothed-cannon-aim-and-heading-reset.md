# Smoothed Cannon Aim and Heading Reset

## Summary

Added frame-rate-independent visual interpolation for cannon heading/elevation, removed cannon-heading limits, and changed leaving Fire mode to smoothly target Player-forward rather than snap. The discrete input resolution, elevation limits, post-Animator pose layering, HUD semantics, and all user-owned Unity assignments remain unchanged.

## Files Created

- `Docs/ImplementationReports/2026-08-06_smoothed-cannon-aim-and-heading-reset.md`

## Files Modified

- `Assets/Scripts/Combat/KartikarisCannonWeapon.cs`
- `Assets/Scripts/Player/MechFirstPersonController.cs`
- `Docs/PROJECT_STATE.md`

## Scene / Prefab / Inspector Changes

None. No scene, prefab, hierarchy, component assignment, serialized reference, tag/layer, or other Unity asset wiring was changed.

## Implementation Details

- Logical `CurrentHeadingDegrees` is now unbounded, allowing A/D to accumulate continuous rotation through any number of revolutions. `CurrentElevationDegrees` retains its configurable clamp. Existing 1° and Left-Alt 0.1° stepping writes both targets exactly as before.
- Separate runtime `VisualHeadingDegrees` and `VisualElevationDegrees` approach the logical targets through exponential interpolation: heading uses `Mathf.LerpAngle`, elevation uses `Mathf.Lerp`, and the blend is `1 - exp(-sharpness * deltaTime)`.
- The serialized `aimLerpSharpness` defaults to `12`. Higher values follow the logical target faster while preserving frame-rate-independent response.
- Smoothing is allowed only once per `Time.frameCount`. This prevents a double interpolation step when both the weapon and Player request a LateUpdate pose refresh.
- Near-target values snap within a very small tolerance so the interpolation does not leave an indefinite numerical tail.
- The smoothed values feed the existing selected-local-axis quaternion composition over the current Animator base pose. Nested heading/elevation and no-drift behavior are unchanged.
- Shell spawning continues to refresh and read the current visual `firePoint` pose, so a shot leaves along the barrel's actually smoothed direction rather than jumping to the logical target.
- `BeginTransitionToTravel()` calls `KartikarisCannonWeapon.ResetHeadingToForward()`, which changes only the logical heading target to `0°`. It does not write the pivot or visual heading directly. Transition state disables further aim input while the same lerp eases the visual heading back toward the Player-forward authored base. Elevation is deliberately retained.

## Decisions and Rationale

- Logical stepping and visual movement are separate so smoothing cannot alter input resolution, elevation clamps, repeat timing, or HUD logical elevation.
- Exponential interpolation was chosen over a fixed per-frame lerp factor so response does not change with frame rate.
- Reset occurs at transition entry rather than at Travel completion, giving the existing transition time for visible recentering.
- `0°` is the weapon's local heading offset relative to its Animator/authored base and therefore represents Player-forward without world-space rotation or transform-name assumptions.

## Validation Performed

- Built the full `Assembly-CSharp.csproj --no-restore --nologo` against Unity `6000.3.9f1` references: zero errors and zero warnings.
- Static inspection confirmed `Mathf.LerpAngle` for heading, `Mathf.Lerp` for elevation, exponential delta-time weighting, once-per-frame guarding, smoothed values passed to both pivot offsets, no minimum/maximum heading fields or heading clamp, and a target-only heading reset at the start of TransitioningToTravel.
- Confirmed the elevation clamp, 1°/0.1° input defaults, post-animation base-pose handling, nested-pivot ordering, HUD elevation call, fire-point launch, mode restrictions, Player Animator mapping, cockpit look, projectile, cooldown, sound, tracked movement, tree interaction, Enemy, and radar code still compile.
- Unity/Play Mode was not launched. Interpolation feel, axis direction, visible recentering, and transition-time fit remain manual checks.
- Git status/diff was unavailable because repository metadata remains non-functional; intentional changes were accounted for through source/filesystem inspection.

## Known Issues / Limitations

- The default sharpness of `12` requires an interactive feel pass.
- Exponential interpolation approaches the target asymptotically and then uses a tiny snap tolerance. A very short transition or very low sharpness may leave visible recentering continuing into Travel.
- Heading recentres to logical local `0°`; the authored/Animator base pose and selected heading axis must be correct for that to visually face Player-forward.
- Elevation intentionally does not reset when leaving Fire.
- Very long sessions of uninterrupted rotation can eventually reduce floating-point precision because logical heading is intentionally continuous and unbounded; ordinary prototype use is not expected to approach that scale.

## Manual Follow-up

1. On `KartikarisCannonWeapon`, confirm `Aim Lerp Sharpness = 12` initially and adjust only after testing.
2. Enter Fire and tap/hold W/S/A/D. Confirm logical 1°/0.1° stepping remains but both visual axes move smoothly.
3. Hold A/D through multiple full rotations and confirm heading never stops at a limit.
4. Leave Fire with a clearly non-zero heading. Confirm heading eases back to Player-forward during TransitioningToTravel without snapping and does not accept aim input during the transition.
5. Confirm elevation retains its previous logical target when leaving Fire.
6. Confirm the Animator deployment/travel base pose still runs beneath smoothing, nested elevation/firePoint follow heading, idle pivots do not drift, and firing uses the current smoothed muzzle direction.

## Suggested Next Step

Perform the focused Play Mode feel pass and tune only `Aim Lerp Sharpness` if the response is too soft or too abrupt relative to the configured mode-transition duration.
