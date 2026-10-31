# Weapon Reset, VFX, Explosion, and Damage Polish

## Summary

Implemented a code-only cannon reset path tied exactly to Player mode-transition time, reliable authored Travel-fold restoration, velocity-following shell orientation, three separate optional VFX hooks, reusable radial mech damage, lightweight mech health, projectile ownership metadata, and spatially bounded virtual-tree blast interaction.

Enemy dry-fire timing and behavior were not changed. No Unity scene, prefab, hierarchy, Inspector value, serialized assignment, animation clip, or project setting was edited.

## Files Created

- `Assets/Scripts/Combat/KartikarisExplosion.cs`
- `Assets/Scripts/Combat/KartikarisExplosion.cs.meta`
- `Assets/Scripts/Combat/KartikarisMechHealth.cs`
- `Assets/Scripts/Combat/KartikarisMechHealth.cs.meta`
- `Docs/ImplementationReports/2026-08-06_weapon-vfx-explosion-damage-polish.md`

## Files Modified

- `Assets/Scripts/Combat/KartikarisCannonWeapon.cs`
- `Assets/Scripts/Combat/KartikarisArtilleryProjectile.cs`
- `Assets/Scripts/Player/MechFirstPersonController.cs`
- `Assets/Scripts/WorldInteraction/KartikarisTreeInteractionRuntime.cs`
- `Docs/PROJECT_STATE.md`

## Scene / Prefab / Inspector Changes

None. All new references and components require manual setup.

## Implementation Details

### Transition reset

- Entering `TransitioningToTravel` calls `BeginTravelReset(modeTransitionDuration)`.
- Reset progress is linear over the supplied duration and independent of generic aim smoothing and Animator timing.
- Heading uses `Mathf.LerpAngle` from its current visual value to `0°`, taking the shortest angular route and being forced exactly to zero at completion.
- Elevation moves from its current visual value to configurable `+90°` during the same interval.
- On Travel completion, procedural elevation is suppressed. The final pivot therefore receives zero procedural offset over the Animator-authored Travel base.
- Transitioning back to Fire re-enables procedural elevation. Starting in Travel also suppresses it, preventing an initial offset.

### Shot and impact presentation

- A successful weapon launch can create one optional detached muzzle-fire VFX and one optional detached lingering-smoke VFX at the exact fire-point pose.
- Rejected launches and cooldown rejection spawn no VFX.
- Each VFX has an optional lifetime; zero leaves destruction to the prefab. Smoke defaults to 30 seconds and stays in world space after the shooter moves.
- Shell `LateUpdate` rotates its forward axis to current Rigidbody velocity above a small configurable speed threshold. Rotation does not write velocity.
- A valid impact records its first collision contact when available, spawns one optional detached impact VFX aligned to the contact normal, resolves gameplay once, then destroys the projectile.

### Explosion and health

- `KartikarisExplosion` separates radial gameplay from VFX spawning.
- Default damage is full through 30 m and decreases linearly to zero at the 100 m outer radius.
- A per-blast `HashSet` ensures one `KartikarisMechHealth` receives damage once even when it owns several overlapping colliders.
- `KartikarisMechHealth` exposes maximum/current health, alive/dead state, damage and reset APIs, last source/origin diagnostics, and `Damaged`/`Killed` C# events.
- Health death does not automatically destroy or disable a Player, Enemy, controller, or root object.

### Ownership and trees

- Launch stores the firing root and a future-facing integer team identifier.
- Projectile colliders still ignore owner colliders immediately after launch. Explosion queries do not exclude the owner, so unsafe close fire can cause self-damage.
- Tree blasts call `CrushTreesInRadius`, which visits only spatial-hash cells intersecting the horizontal radius. Active tree indices inside the circle are hidden once and reuse the existing falling visual plus crack/thud path, directed away from the blast.

## Decisions and Rationale

- Travel reset has a dedicated interpolation clock because normal exponential smoothing reaches most of its target early and cannot guarantee exact transition-duration arrival.
- Procedural elevation is suppressed only after Travel completion so Animator-authored folding can remain the pose authority without bone names or animation edits.
- VFX fields remain on their natural owners: muzzle and smoke on the weapon; impact on the projectile. Damage does not depend on any VFX assignment.
- Shooter immunity is limited to physical launch collision-ignore. Permanent blast immunity would hide dangerous close-fire behavior and was not requested.
- Tree blast distance uses the existing horizontal virtual-tree positions and spatial cells, preserving the 25,000-tree architecture without collider creation or a global scan.

## Validation Performed

- Temporarily included the two new source files in the generated `Assembly-CSharp.csproj`, then ran `dotnet build Assembly-CSharp.csproj --no-restore` against the installed Unity references.
- Result: build succeeded with zero errors and zero warnings. The temporary generated-project entries were removed afterward.
- Static inspection confirmed exact shared transition duration, shortest-route heading interpolation, exact final heading, elevation suppression at Travel completion, successful-shot-only muzzle/smoke spawning, detached world-space smoke, velocity-only orientation reads, first-contact impact origin, one-shot impact guard, explicit owner/team metadata, owner collider-ignore, no owner exclusion from radial damage, unique health resolution, linear damage endpoints, and bounded spatial-cell tree traversal.
- Unity Editor and Play Mode were not launched. No runtime VFX, Animator, collision, damage, or tree-fall behavior was observed.
- Git status/diff was unavailable because the workspace does not expose functional repository metadata. Resulting files were inspected directly and the generated project-file validation edit was removed.

## Known Issues / Limitations

- Exact authored folding, repeated-cycle drift, and imported-bone axis behavior still require Play Mode inspection.
- Health has no built-in death presentation or controller/AI disable behavior.
- Damage uses the health component Transform position and has no armor, hitboxes, blast occlusion, fragmentation, terrain shielding, or team filtering.
- Projectile source team ID is stored for future use but currently has no gameplay effect.
- VFX prefab orientation, scale, lifetime, pooling behavior, and visual suitability depend on the manually assigned purchased assets.
- Explosion/tree radii and damage values are prototype defaults requiring an interactive feel pass.

## Manual Follow-up

1. On `KartikarisCannonWeapon`, preserve the existing shell and fire-point references; assign optional muzzle-fire and lingering-smoke prefabs. Use lifetime `0` for self-managed effects or keep the smoke prototype default at 30 seconds.
2. Confirm Player `modeTransitionDuration` and desired muzzle velocity. Code defaults are 2 seconds and 300 m/s; preserved current scene values are 6 seconds and 100 m/s.
3. Assign MapGenerator's `KartikarisTreeInteractionRuntime` to the weapon so launched shells can affect virtual trees.
4. On the shell prefab, retain `KartikarisArtilleryProjectile`, a non-kinematic Rigidbody, and an enabled non-trigger Collider. Assign optional impact VFX, then confirm impact layers, 30-second lifetime, 100 m outer radius, 30 m full-damage radius, 100 maximum damage, mech layers, and tree radius.
5. Add `KartikarisMechHealth` to Player and Enemy roots and set maximum health. Subscribe to `Killed` from code later if a death presentation is desired.
6. In Play Mode, test transition timing/fold/drift, VFX counts and detached smoke, projectile pitch, contact placement, owner collision-ignore plus close self-damage, damage falloff/multi-collider deduplication, and inside/outside tree blast behavior.

## Suggested Next Step

Perform the manual Play Mode acceptance pass with simple placeholder VFX first, then tune reset presentation, blast/tree radii, damage, and VFX lifetimes before adding Enemy ballistic aiming or death presentation.
