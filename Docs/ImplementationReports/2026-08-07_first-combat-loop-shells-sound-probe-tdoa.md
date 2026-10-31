# First Combat Loop, Shells, Sound Probe, and TDOA Hyperbolas

## Summary

Connected the existing cannon/projectile, Enemy, sound, radar, and tree systems into a code-only first combat loop. Added immutable shell loadouts with four shared projectile behaviors, reusable ballistic solving, real Enemy Standard HE fire, two deployable Sound Probe slots, correlated multi-receiver acoustic events, and the revised physically derived Player↔Probe TDOA hyperbola visualization.

## Files Created

- `Assets/Scripts/Combat/KartikarisCombatAffiliation.cs`
- `Assets/Scripts/Combat/KartikarisShellDefinition.cs`
- `Assets/Scripts/Combat/KartikarisShellLoadout.cs`
- `Assets/Scripts/Combat/KartikarisBallisticSolver.cs`
- `Assets/Scripts/Combat/KartikarisGuidanceAntenna.cs`
- `Assets/Scripts/Combat/KartikarisGuidedMissileController.cs`
- `Assets/Scripts/Audio/KartikarisAcousticReceiver.cs`
- `Assets/Scripts/Modules/KartikarisModuleDefinition.cs`
- `Assets/Scripts/Modules/KartikarisModuleController.cs`
- `Assets/Scripts/Modules/KartikarisSoundProbe.cs`
- `Assets/Scripts/Radar/KartikarisTdoaHyperbolaGraphic.cs`
- `Assets/Scripts/Radar/KartikarisAcousticLocalization.cs`
- `Docs/ImplementationReports/2026-08-07_first-combat-loop-shells-sound-probe-tdoa.md`

## Files Modified

- `Assets/Scripts/Combat/KartikarisCannonWeapon.cs`
- `Assets/Scripts/Combat/KartikarisArtilleryProjectile.cs`
- `Assets/Scripts/Combat/KartikarisExplosion.cs`
- `Assets/Scripts/Combat/KartikarisMechHealth.cs`
- `Assets/Scripts/Enemy/EnemyTest.cs`
- `Assets/Scripts/Audio/KartikarisSoundPropagation.cs`
- `Docs/PROJECT_STATE.md`

## Scene / Prefab / Inspector Changes

None. No scene, prefab, animation, ScriptableObject asset, hierarchy, camera, UI object, layer, tag, or serialized reference was created or changed.

## Implementation Details

- `KartikarisShellDefinition` is a data asset for StandardHE, Airburst, Homing, or GuidedMissile. `KartikarisShellLoadout` snapshots and locks one selection at startup; no runtime switch input exists.
- `KartikarisCannonWeapon` selects the locked definition's prefab/velocity, preserves `firePoint.forward`, and launches through the existing shared projectile with owner, team, affiliation, definition, tree runtime, and guidance-antenna metadata.
- Standard HE defaults to `1000` maximum damage, full through `30 m`, linear zero at `100 m`, with existing tree blast compatibility.
- Airburst arms after clearing the configured `50 m` height plus margin, queries Terrain or world collision ground, detonates on downward crossing or impact fallback, and uses `200` maximum linearly falling damage inside the velocity-forward `30°` full cone out to `200 m`.
- Homing acquires the first living hostile inside `300 m` and the forward `30°` full cone, retains the target until invalid/dead, preserves speed while bounded-turn steering, and applies one `100` direct hit without splash.
- Guided Missile exposes prefab camera/mount references and a firing-mech controller API. It accepts steering only while the explicit antenna signal is valid, continues its current physics after signal loss, and applies one `800` direct hit without splash.
- `KartikarisBallisticSolver` provides independent low/high no-drag solutions with arbitrary target height, actual gravity vector, reachability failure, direction, and flight time.
- `EnemyTest` now deploys, solves against the Player's current position, sets its shared weapon articulation, verifies actual muzzle alignment, fires through `TryFire`, folds, and relocates. Solve/alignment/cooldown failure times out into relocation.
- `KartikarisSoundPropagation` assigns stable event IDs, stores source/emission internally, schedules the Player and registered receivers independently, and publishes source-free receiver arrival records while retaining existing Player boom/beep/radar-hint presentation.
- `KartikarisModuleController` uses top-row digit 1/2 Input System actions. Each fixed loadout slot deploys once at its explicit mount, detaches the prefab, and initializes a stable Sound Probe receiver; both slots may contain probes.
- TDOA uses `deltaTime = probeArrival - playerArrival` and `deltaDistance = deltaTime × soundSpeed`. It validates `abs(deltaDistance) <= horizontal baseline`, uses detection-time historical receiver positions, and never receives authoritative source position.
- The UI graphic analytically samples the signed branch satisfying `distance(P, probe) - distance(P, player) = deltaDistance`, clips to radar range, uses north-up offsets, and exposes color, presentation-space thickness, and bounded `16..512` resolution. One/two probes render one/two same-event curves; no rings, intersection solver, marker, dot, or arrow exists.

## Decisions and Rationale

Shared projectile infrastructure avoids four drifting physics/collision implementations. ScriptableObject definitions provide a pre-match API without prematurely building UI. Explicit affiliation prevents name-based hostility. Enemy validates the physical muzzle direction before firing so the shell never silently uses a mathematical direction the barrel has not reached. Acoustic event correlation is independent of real/decoy origin, and localization consumes only receiver geometry plus arrival differences, preserving future valid deception.

## Validation Performed

- Temporarily included all new scripts in the generated `Assembly-CSharp.csproj`, restored dependencies, and built against Unity `6000.3.9f1` references with zero errors. The temporary generated-project entries were removed afterward.
- The compile retained one unrelated existing warning from Mirza Beig's `DemoController.camera` hiding `Component.camera`.
- Static inspection confirmed the four exact default damage/radius/cone/detection values, immutable loadout path, metadata propagation, direct-hit versus radial separation, and no shell-switch input.
- Static inspection confirmed low/high solver selection, unreachable failure, Enemy physical-alignment gate, shared weapon fire, and relocation fallback.
- Static inspection confirmed stable event IDs, receiver-specific scheduling, source-free detection payloads, same-event two-probe pairing, signed baseline validation, source-independent analytical branch construction, bounded sampling, and no range-ring/intersection-marker implementation.
- Static inspection confirmed top-row digit bindings, one-shot two-slot deployment, detach behavior, and no Rigidbody dependency for probes.
- Unity Editor, batch mode, Play Mode, projectile flight, damage, Enemy aim, module deployment, receiver timing, and radar rendering were not run.

## Known Issues / Limitations

- All new assets/references are intentionally unwired; behavior cannot be exercised until the manual setup is completed.
- Enemy heading/elevation axis signs and high-arc reach must be verified against the imported rig. Misalignment safely times out instead of firing.
- Airburst ground queries do not predict terrain ahead between physics ticks.
- Homing uses straightforward bounded pursuit, not proportional navigation.
- Guided Missile has an API and optional InputAction reference but no cockpit monitor or authored antenna animation.
- Propagation schedules arrival from receiver position at emission while reporting its position at detection; moving-receiver interception is not simulated.
- UI masking/thickness and analytical curve appearance require visual verification under the existing World Space radar Canvas.
- Existing scene health values do not inherit the new `1000` code default automatically.
- Git metadata is unavailable, so normal status/diff inspection could not be performed.

## Manual Follow-up

- Create four shell-definition assets and one Sound Probe module-definition asset.
- Complete the Player, Enemy, projectile, Guided Missile, module, probe, and radar setup listed in `Docs/PROJECT_STATE.md`.
- Run checks 31–36 plus the retained locomotion/cannon/tree regression checks.

## Suggested Next Step

Wire Standard HE and Enemy first, validate one real shot and damage, then add one probe/hyperbola, the second probe, Airburst/Homing, and Guided Missile in that order so failures remain isolated.
