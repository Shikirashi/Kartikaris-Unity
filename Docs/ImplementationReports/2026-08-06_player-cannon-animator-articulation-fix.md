# Player Cannon Animator and Post-Animation Articulation Fix

## Summary

Added explicit Player control of the existing Animator bool `cannon` and changed cannon articulation from a startup-captured rest pose to a drift-free procedural layer over the current Animator pose. The change is code-only and preserves all user-owned scene, prefab, hierarchy, and Inspector assignments.

## Files Created

- `Docs/ImplementationReports/2026-08-06_player-cannon-animator-articulation-fix.md`

## Files Modified

- `Assets/Scripts/Player/MechFirstPersonController.cs`
- `Assets/Scripts/Combat/KartikarisCannonWeapon.cs`
- `Docs/PROJECT_STATE.md`
- `Docs/ImplementationReports/2026-08-06_player-fire-mode-and-artillery-shell.md`

## Scene / Prefab / Inspector Changes

None. The new Player Animator reference was intentionally left unassigned for manual setup. Existing torso heading, `cannon.1` elevation, fire-point, weapon, projectile, sound, and HUD assignments were not changed.

## Implementation Details

### Player Animator state

- `MechFirstPersonController` now exposes an explicit serialized `playerAnimator`; it never searches the hierarchy or infers an Animator.
- It validates that the assigned controller contains a bool named exactly `cannon`.
- `cannon` is false in Travel and TransitioningToTravel, and true in TransitioningToFire and Fire.
- Both public transition-entry methods write the new animation request immediately. Per-frame state synchronization keeps the parameter coherent throughout stable states.
- The existing mode timer is unchanged. No Animator state, clip length, playback speed, normalized time, transition duration, or animation event is read by gameplay code.
- Cockpit look remains in the unconditional Update path. Locomotion, aim, and fire gates are unchanged.

### Post-animation articulation

- Logical heading/elevation and HUD updates remain separate from Transform writes.
- `KartikarisCannonWeapon.LateUpdate` applies visual articulation after Animator evaluation. Player firing was moved to `MechFirstPersonController.LateUpdate`, which explicitly refreshes the weapon pose before reading `firePoint`.
- For each pivot, the code observes its current local rotation. If Animator wrote a different rotation after the previous frame, that observed pose becomes the fresh base. The logical offset is composed as `baseLocalRotation * Quaternion.AngleAxis(logicalDegrees, selectedLocalAxis)`.
- The last procedural result is stored. If an unanimated pivot still contains that same result next frame, the stored base is reused instead of treating the offset result as a new base. This prevents frame-to-frame accumulation without requiring an animated/static Inspector switch.
- Heading is applied first. A descendant elevation pivot then keeps its own local animated base and receives elevation, while naturally inheriting heading in world space. `firePoint` descendants follow the combined hierarchy pose.
- No world-space rotation, transform-name lookup, Animator disabling, clip modification, or incremental Euler edit was introduced.

## Decisions and Rationale

- Animator owns the changing deployment/travel base pose; the weapon owns only logical selected-axis aim offsets.
- Previous-result detection supports both animated and unanimated pivots. Blindly multiplying onto the observed transform would accumulate on static pivots, while blindly removing the prior offset would cancel aim on Animator-reset pivots.
- Player performs its intentional fire check in LateUpdate so the shell uses the post-animation, current logical `firePoint` pose. Input press state remains valid for that frame.
- Enemy behavior was not changed because its existing `cannon` animation flow already works and this fix is Player-specific.

## Validation Performed

- Confirmed no Unity Editor, shader compiler, or licensing process was running during the final check.
- Built `Assembly-CSharp.csproj --no-restore --nologo` against the Unity `6000.3.9f1` references: zero errors and zero warnings.
- Static source inspection confirmed exact `cannon` state mapping, immediate transition-entry writes, ongoing stable-state synchronization, and complete absence of Animator timing reads.
- Static inspection confirmed LateUpdate visual application, animated-base refresh, previous-result/no-drift handling, selected X/Y/Z axis use, local quaternion multiplication, heading-before-elevation ordering, and a final pose refresh before fire-point spawning.
- Existing clamps, 1°/0.1° stepping, HUD elevation call, Travel/Fire input gates, cockpit look, projectile launch/cooldown/sound, tracked movement, tree interaction, Enemy, radar, and projectile code still compile.
- Unity and Play Mode were not launched. Visible animation layering, actual imported local-axis choices, nested-pivot motion, and fire-point trajectory remain manual checks.
- Git status/diff remains unavailable because functional repository metadata is absent; intentional files were accounted for by source/filesystem inspection.

## Known Issues / Limitations

- `playerAnimator` must be manually assigned before Player animation state can change.
- The imported torso and `cannon.1` axis selections are user-owned and not visually verified by this code-only pass.
- Procedural articulation assumes other late-running scripts do not intentionally overwrite the same pivots after this weapon's final LateUpdate refresh.
- No animation event, reload animation, automatic aiming, or Enemy ballistic firing was added.

## Manual Follow-up

1. Assign the Player mech Animator to `MechFirstPersonController.playerAnimator` and confirm the exact bool parameter `cannon` exists.
2. Keep torso assigned as heading pivot if desired and keep `cannon.1` assigned as the nested elevation pivot.
3. Confirm the selected local axes for both pivots and the fire-point local-forward direction.
4. Enter Play Mode and confirm Travel sets `cannon` false; starting TransitioningToFire immediately sets it true; Fire keeps it true; starting TransitioningToTravel immediately sets it false.
5. Confirm the configured gameplay transition duration is unchanged when Animator speed or clip duration differs.
6. During/after deployment, aim heading and elevation. Confirm the animated base pose remains active, elevation is visibly layered over `cannon.1`, nested heading carries the elevation/fire point, and idle frames do not drift.
7. Confirm one Fire-mode shot leaves from the combined fire-point pose and that HUD elevation, cockpit look, movement gates, cooldown, sound, and projectile behavior remain correct.

## Suggested Next Step

Run the focused Play Mode checklist and adjust only the explicit local-axis selections if the imported rig's authored axes differ from the current manual choices.
