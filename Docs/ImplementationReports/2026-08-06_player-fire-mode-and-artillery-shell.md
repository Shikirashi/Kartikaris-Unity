# Player Fire Mode and Artillery Shell

## Summary

Implemented the code-only Player Travel/Fire mode milestone, reusable cannon articulation/firing, a gravity-driven high-speed shell, and matching transition timing in the existing Enemy dry-fire loop. The follow-up fix added explicit Player `cannon` Animator control and post-animation weapon articulation so animated pivots retain their deployment pose beneath procedural aim. No Unity scene, prefab, hierarchy, Inspector reference, tag/layer, package, or ProjectSettings asset was edited.

The saved scene is intentionally not ready to fire until the manual assignments below are completed.

## Files Created

- `Assets/Scripts/Combat/KartikarisCannonWeapon.cs`
- `Assets/Scripts/Combat/KartikarisCannonWeapon.cs.meta`
- `Assets/Scripts/Combat/KartikarisArtilleryProjectile.cs`
- `Assets/Scripts/Combat/KartikarisArtilleryProjectile.cs.meta`
- `Assets/Scripts/Combat.meta`
- `Docs/ImplementationReports/2026-08-06_player-fire-mode-and-artillery-shell.md`

## Files Modified

- `Assets/Scripts/Player/MechFirstPersonController.cs`
- `Assets/Scripts/Enemy/EnemyTest.cs`
- `Docs/PROJECT_STATE.md`

## Scene / Prefab / Inspector Changes

None. The user requested code-only changes and will perform scene/prefab wiring manually.

## Implementation Details

### Player operating modes

- Player defaults to `Travel` and exposes `Travel`, `TransitioningToFire`, `Fire`, and `TransitioningToTravel` runtime states.
- T starts a gameplay-code timer with a serialized `2 s` default. Animation length/speed is not consulted.
- Existing tracked input, acceleration, turning, CharacterController movement, gravity, and tree crushing remain on the original path in Travel.
- Both transitions pass zero drive input so the mech brakes without accepting locomotion, aim, or fire. Entering Fire clears residual longitudinal speed and track diagnostics.
- Fire continues applying zero drive input and reuses WASD only for weapon steps. Cockpit mouse look remains an unconditional, independent update in every mode.
- W/S step elevation; A/D step heading. Defaults are `1°` normally and `0.1°` with Left Alt. A new direction steps immediately, then uses serialized repeat delay/interval timing with bounded frame-independent catch-up.
- Firing requires fully entered Fire, a locked cursor, an intentional new LMB press, an assigned weapon, and weapon cooldown. A recapture click latches fire suppression until release.
- Player now exposes an explicit `playerAnimator` reference and never searches for it. Its exact bool parameter `cannon` is synchronized from gameplay state every frame: false in Travel/TransitioningToTravel and true in TransitioningToFire/Fire. Beginning either transition writes the requested value immediately. The gameplay timer remains independent of Animator clips, speed, normalized time, and events.

### Shared cannon

- `KartikarisCannonWeapon` owns explicit heading pivot, elevation pivot, fire point, local-axis selections, logical limits, cooldown, projectile spawning, and sound/HUD integration.
- No transform name or hierarchy path is searched.
- Logical aim is stored independently from the visual pose. In `LateUpdate`, each pivot's current Animator-produced local rotation becomes that frame's base, then code writes `animatorBaseLocalRotation * Quaternion.AngleAxis(value, selectedAxis)`. The previous procedural result is tracked: if an unanimated pivot still contains exactly that result next frame, the stored base is reused instead of treating the procedural result as a new base. This supports animated and static pivots without cumulative drift.
- Heading is composed before elevation. A descendant elevation pivot therefore inherits its heading ancestor naturally while receiving its own local-axis offset, and descendants such as `firePoint` follow the combined pose.
- Defaults are heading `-180°..+180°`, elevation `-30°..+90°`, muzzle velocity `300 m/s`, and cooldown `5 s`.
- `TryFire` is input/mode agnostic so future Enemy/combat code can use the same API. A successful launch reports the muzzle position through `KartikarisSoundPropagation.EmitCannonDryFire`, retaining finite-speed delivery.
- Optional HUD integration forwards real logical elevation through the existing `SetElevationDegrees` API. Hull heading remains the heading-tape source.

### Ballistic shell

- `KartikarisArtilleryProjectile` requires a Rigidbody and validates at least one non-trigger Collider before launch.
- Runtime configuration uses normal Unity gravity, non-kinematic motion, `ContinuousDynamic` collision detection, and interpolation.
- The Rigidbody velocity cap is raised as needed before assigning the initial world velocity, preventing Unity's cap from reducing the requested 300 m/s prototype speed.
- Projectile colliders ignore colliders beneath the firing weapon's owner root to avoid immediate self-impact.
- Valid collision layers are configurable. Valid collisions destroy the shell; trigger impacts are optional/off by default; lifetime defaults to 30 seconds.
- No damage, explosion, penetration, fragmentation, drag, wind, terrain deformation, tree effect, or trajectory solving was added.

### Enemy transition parity

- `EnemyTest` now follows `TransitioningToFire → Preparing → TransitioningToTravel → Relocating → Settling`.
- Both transition states use a serialized `modeTransitionDuration` with the same `2 s` default as Player and apply zero tracked input.
- The existing Animator `cannon` bool, dry-fire event, delayed sound, destination selection, Player-matched locomotion, tree collision, braking, and settling behavior remain.
- Enemy deliberately remains a dry-fire prototype with no automatic aim or projectile spawning. The shared weapon API is structurally available for a later caller.

## Decisions and Rationale

- Mode policy remains in Player input code; artillery simulation does not know about Player state.
- A Rigidbody shell with `ContinuousDynamic` was chosen as the smallest robust Unity-native prototype solution for 300 m/s terrain collision. The manual setup recommends a primitive non-trigger Collider because that is the appropriate CCD shape.
- Transition timing is explicit gameplay state rather than animation-derived timing, leaving animation as a future presentation hook.
- Player and Enemy use the same existing `cannon` bool convention, but each retains its own explicit state flow. Player's assigned Animator is presentation only and cannot complete or extend a gameplay transition.
- Procedural articulation is deliberately a post-animation layer rather than a startup-rest-pose replacement. Logical input changes occur in Update; visual quaternion composition and Player firing occur in LateUpdate after Animator evaluation.
- Real shots reuse the existing sound emission method despite its historical `DryFire` name to avoid introducing competing propagation infrastructure.
- The current heading HUD continues to describe hull/world heading; only weapon elevation is safely forwarded through its existing manual API.

## Validation Performed

- Confirmed no Unity Editor, shader compiler, or licensing process was running before validation.
- Temporarily included both new scripts in the generated `Assembly-CSharp.csproj`, ran `dotnet build Assembly-CSharp.csproj --no-restore --nologo` against Unity `6000.3.9f1` references, and received zero errors and zero warnings. The temporary project-file entries were removed afterward.
- Statically inspected the resulting source for the four Player states and Enemy transition states; Travel-only drive input; Fire-only aim/fire gates; unconditional cockpit look; exact default 1°/0.1° steps; repeat timing; aim clamps; selected-axis quaternion composition; 300 m/s fire-point launch; cooldown; sound call; continuous Rigidbody collision; gravity; impact/lifetime destruction; and cursor-recapture suppression.
- Follow-up compilation again passed with zero errors and zero warnings. Static inspection confirmed the Player `cannon` mapping for all four states; immediate writes at both transition entry points; no animation-duration reads; post-animation local composition; previous-result detection preventing accumulation; heading-before-elevation ordering for nested pivots; unchanged limits/step defaults/HUD call/fire-point launch; and unchanged locomotion, look, firing, projectile, sound, Enemy, tree, radar, and HUD code paths.
- No Unity Editor or Play Mode process was launched. No live input, animation, articulation, projectile trajectory, terrain CCD, audio propagation, or HUD behavior was claimed as tested.
- Final filesystem audit found that `Kartikaris_MapTest.unity` had received a concurrent/external save during this task (mtime `2026-08-06 20:11:47 +0700`). Codex did not write, revert, or otherwise alter that scene and preserved the external change.
- Git status/diff was unavailable because repository git metadata is not functional in this workspace. A filesystem/source audit was used to account for intentional files instead.

## Known Issues / Limitations

- The new code is not wired into the saved scene or shell prefab.
- Actual imported cannon pivot axes, Animator-authored deployment poses, and fire-point forward direction require visual verification.
- Rigidbody CCD quality still depends on an appropriate primitive Collider, Physics collision matrix, fixed timestep, and world collider setup.
- The optional HUD reference updates elevation only while the HUD uses its manual/API source; a non-null elevation Transform takes precedence in the HUD.
- Enemy does not reference or invoke the weapon yet and intentionally remains dry-fire only.
- Player now drives the existing `cannon` bool, but its Animator reference still requires manual assignment and no reload-specific animation parameter was added.
- Cooldown, transition/repeat timing, limits, and collider sizing require an interactive feel pass.

## Manual Follow-up

### Player

1. Add `KartikarisCannonWeapon` to Player or its dedicated cannon controller object.
2. Assign it to `MechFirstPersonController.cannonWeapon`.
3. Assign the Player mech Animator to `MechFirstPersonController.playerAnimator` and confirm its exact bool parameter is `cannon`.
4. Keep the torso assigned as the heading pivot if desired and select its actual local X/Y/Z axis.
5. Keep `cannon.1` assigned as the elevation pivot and select its actual local X/Y/Z axis. A descendant relationship beneath the heading pivot is supported; use a distinct Transform from the heading pivot.
6. Assign the existing muzzle `firePoint` and verify its blue/local-Z forward axis points out of the barrel.
7. Assign the configured artillery-shell prefab component.
8. Confirm `300 m/s`, desired cooldown (`5 s` default), heading/elevation limits, and Player transition duration (`2 s` default).
9. Assign `KartikarisSoundPropagation`. Optionally assign `KartikarisHeadingElevationHUD`; leave that HUD's elevation source empty if logical weapon elevation should drive it.
10. In Play Mode, confirm T immediately changes `cannon`, the deployment/travel animation continues normally, and Fire-mode W/S elevation remains visibly layered over the animated `cannon.1` pose without frame-to-frame drift.

### Enemy

1. Set `EnemyTest.modeTransitionDuration` to match Player (`2 s` by default).
2. No weapon reference is required for the retained dry-fire loop.

### Projectile prefab

1. Add `KartikarisArtilleryProjectile` to `Assets/Prefabs/artillery shell.prefab`.
2. Add/retain one non-kinematic Rigidbody and at least one enabled non-trigger primitive Collider; a small SphereCollider is recommended.
3. Assign the prefab's projectile component to `KartikarisCannonWeapon.artilleryShellPrefab`.
4. Ensure its layer collides with Terrain and desired world layers in the Physics matrix, and that those layers are included in `validImpactLayers`.
5. Play-test recapture suppression, mode gates, aim directions/limits, fire-point orientation, 300 m/s launch, gravity arc, terrain CCD, impact destruction, lifetime, cooldown, sound arrival, and Enemy transition timing.

## Suggested Next Step

Complete the updated manual wiring and interactive Play Mode acceptance pass. Verify the Animator-driven deployment base plus procedural torso/elevation offsets visually before tuning axes, limits, transition duration, or animation content.
