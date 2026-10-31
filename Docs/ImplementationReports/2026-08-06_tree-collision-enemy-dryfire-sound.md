# Tree Collision, Enemy Dry-Fire, and Sound Propagation

## Summary

Implemented the prototype sequence requested for this milestone:

```text
Player intersects virtual trunk
→ movement clamps before impact with no CharacterController tree-slide
→ longitudinal speed becomes zero
→ standing instance becomes inactive immediately
→ non-blocking visual falls for 0.7 s
→ held throttle accelerates again

Enemy prepares and dry-fires at A
→ no shell/projectile/impact/damage
→ Enemy relocates at most 100 m
→ recorded sound from A arrives after distance / 343 m/s
→ delayed boom + radar sound hint (+ subtle beep)
→ Enemy remains unspotted
```

The existing TerrainData clone, stable tree indices, spatial hash, CharacterController tank controls, radar bearing/error API, camera architecture, authored 25,000-tree forest, and initial 300 m enemy placement were preserved.

## Observed Existing Behavior

### Tree collision audit

The saved scene contradicted the prior project-state claim: `TerrainCollider.m_EnableTreeColliders` was `1`. Every assigned Synty Terrain-tree prototype also has enabled source collision geometry:

- `SM_Env_Tree_Big_01` and `SM_Env_Tree_Big_02`: one enabled non-convex `MeshCollider` each, using the visual tree mesh;
- `SM_Generic_Tree_01` and `SM_Generic_Tree_02`: two enabled convex `MeshCollider` components each;
- `SM_Gen_Env_Tree_01/02/03` and `SM_Gen_Env_Tree_Pine_01/02/03`: one enabled upright `CapsuleCollider` each.

This produces six source MeshColliders across four prototypes plus six CapsuleColliders across six prototypes. With Terrain tree collision enabled, Unity used those prefab collision shapes for the Terrain instances. The mesh-based shapes can include substantially more than a gameplay trunk and were the concrete source of the unexpected blocking/side-sliding reports.

Existing runtime fallen-tree copies were not the source. `KartikarisTreeInteractionRuntime` already disabled every copied Collider and removed Rigidbody collision behavior after instantiation. The original Player movement, however, committed `CharacterController.Move` first and only crushed trees afterward in `MechTreeCrusher.LateUpdate`; when Terrain tree colliders were enabled, normal CharacterController collision resolution could slide tangentially before the tree was hidden.

### Radar sound wedge

The scene already had the user-owned `Assets/Images/sound_origin_hint.png` assigned before this work. It was preserved without creation or modification. The radar's null-sprite-safe behavior also remains intact.

## Files Created

- `Assets/Scripts/Audio.meta`
- `Assets/Scripts/Audio/PrototypeAudioSynthesis.cs`
- `Assets/Scripts/Audio/PrototypeAudioSynthesis.cs.meta`
- `Assets/Scripts/Audio/KartikarisSoundPropagation.cs`
- `Assets/Scripts/Audio/KartikarisSoundPropagation.cs.meta`
- `Assets/Scripts/WorldInteraction/FallingTreeVisual.cs`
- `Assets/Scripts/WorldInteraction/FallingTreeVisual.cs.meta`
- `Docs/ImplementationReports/2026-08-06_tree-collision-enemy-dryfire-sound.md`

## Files Modified

- `Assets/Scripts/WorldInteraction/KartikarisTreeInteractionRuntime.cs`
- `Assets/Scripts/Player/MechTreeCrusher.cs`
- `Assets/Scripts/Player/MechFirstPersonController.cs`
- `Assets/Scripts/Enemy/EnemyTest.cs`
- `Assets/Scenes/Kartikaris_MapTest.unity`
- `Docs/PROJECT_STATE.md`
- `Assets/FrontVision.renderTexture` — Unity refreshed the live asset during GPU-backed Play Mode validation; inspected settings remain `1280 × 720` with the existing format/configuration.

No Synty prefab, model, material, Animator Controller, TerrainData asset, radar script, package, or ProjectSettings file was modified.

## Scene / Prefab / Inspector Changes

- Set the saved TerrainCollider tree-collision property to disabled.
- Added `KartikarisSoundPropagation` to `MapGenerator`, referencing Player and `KartikarisRadarController`.
- Connected `KartikarisTreeInteractionRuntime` and `EnemyTest` to that sound service.
- Gave `KartikarisTreeInteractionRuntime` ten serialized/editable trunk profiles and a `0.7 s` fall duration.
- Preserved the Enemy's explicit Player, Terrain, Animator, and initially-unspotted `RadarContact` references.
- Replaced the old five-second toggle settings with:
  - `2 s` preparation;
  - `300 m` notional-target radius;
  - `100 m` relocation limit;
  - `10 m/s` movement;
  - `120°/s` rotation;
  - `1 m` arrival tolerance;
  - `1 s` settle;
  - `35°` destination slope limit;
  - twelve bounded destination attempts.
- Preserved exactly one enabled AudioListener on `FrontFeedCamera`; no listener was added to `CockpitCamera`.

The six source CapsuleCollider profiles seed these radii/heights automatically:

| Prototype | Radius | Height |
|---|---:|---:|
| `SM_Gen_Env_Tree_01` | 0.19 m | 4.98 m |
| `SM_Gen_Env_Tree_02` | 0.18 m | 7.05 m |
| `SM_Gen_Env_Tree_03` | 0.14 m | 5.14 m |
| `SM_Gen_Env_Tree_Pine_01` | 0.19 m | 9.4701 m |
| `SM_Gen_Env_Tree_Pine_02` | 0.20 m | 6.9123 m |
| `SM_Gen_Env_Tree_Pine_03` | 0.24 m | 6.7599 m |

The four mesh-only prototypes deliberately do not derive collision from renderer/leaf bounds. Their editable fallback radius/height pairs are Big 01 `0.62/11.5 m`, Big 02 `0.56/10.5 m`, Generic 01 `0.38/7.5 m`, and Generic 02 `0.42/8.5 m`.

## Tree Blocking Architecture

Virtual trunk volumes were chosen instead of actual Collider proxies. The runtime already indexes all stable Terrain tree positions, so each active record now also caches scaled radius, bottom, and top values. A query examines only overlapping spatial-hash cells and performs:

- a 2D XZ moving-circle versus trunk-circle intersection;
- radius expansion by the Player's `1.25 m` crush footprint;
- sensible CharacterController/trunk vertical overlap;
- earliest hit selection along the intended frame segment.

This preserves 25,000 Terrain instances without adding 25,000 GameObjects, CapsuleColliders, MeshColliders, or Rigidbodies. Source Synty prefabs remain untouched.

## Stop / No-Slide Behavior

`MechFirstPersonController` now builds the intended horizontal displacement before calling `CharacterController.Move`. `MechTreeCrusher.ResolveHorizontalMovement` asks the tree runtime for the earliest trunk intersection and returns an endpoint just before impact.

On a hit, the runtime first marks that stable tree index inactive, hides its instance in the runtime TerrainData clone, and starts the fall. The controller then commits only the clamped longitudinal displacement plus gravity and sets its longitudinal speed to zero. Since Terrain prefab-derived tree colliders are disabled, the CharacterController has no standing-tree surface from which to calculate a tangential slide. There is no stun timer; held W/S starts accelerating again on the next frame.

The inactive flag changes before the fall animation, so the same tree cannot block or trigger again while falling.

## Tree Fall

`FallingTreeVisual` receives an upright yaw, a settled rotation, the configured duration, and the shared sound service. It uses smoothstep-eased quaternion interpolation from upright to approximately horizontal. The final tree-up direction is the impact direction projected onto the local Terrain tangent, preserving the original Terrain tree scale and yaw contribution.

The copy begins at the original grounded position. All copied Colliders are disabled immediately and any copied Rigidbody is made non-colliding/kinematic and removed. The visual remains after settling as session trail evidence.

## Prototype Audio

Closing IN was inspected read-only, specifically `Assets/Scripts/FireForEffect/ArtilleryAudio.cs`. Kartikaris adapts its proven runtime synthesis style: fixed-rate sample arrays, deterministic local noise, exponential envelopes, sine-based low-frequency bodies, `AudioClip.Create`, and one `SetData` call per clip.

The Kartikaris service creates four clips once during `Awake`:

- cannon: sharp noise transient, falling low-frequency body/sub-body, decaying noise tail;
- radar: short quiet rising chirp;
- tree break: short noise snap plus descending wooden tone;
- tree impact: low dull thud plus brief debris noise.

Clips are not regenerated per frame/event. Spatial sounds use a small reusable pool capped at eight emitters by default. The radar beep uses one non-spatial source.

The donor's non-generated digest was identical before and after work:

```text
a3ac185f2137c57deacb36b0db7896b71ea8d020aeb909dc9a9f11a222e6aa59
```

## Enemy State Machine

`EnemyTest` was refactored rather than adding a second component, ensuring that only one script controls the Animator and movement.

1. Startup preserves random grounded placement within 300 m.
2. Preparing chooses a bounded, grounded notional target and sets `cannon = true`.
3. Dry-fire captures the Enemy's current position, emits one propagation event, invokes a future-facing debug event, and sets `cannon = false`.
4. Relocation chooses a bounded/slope-checked grounded destination no farther than 100 m, rotates toward it, moves directly, and resamples Terrain Y.
5. Settling waits briefly, then returns to Preparing.

Runtime diagnostics expose the current state, destination, notional target, last fire source, state time, dry-fire count, and completed relocation count. Scene gizmos show destination and target.

Enemy movement intentionally does not use NavMesh or tree avoidance and may move through visual trees.

## Ballistics Status

**No ballistic/projectile system was implemented.**

There is no shell GameObject, trajectory, muzzle-to-target solution, projectile physics, impact, explosion, damage, targeting logic, or actual cannon aiming.

## Sound Propagation

At dry-fire, the service records the source Vector3 and calculates:

```text
distance = Vector3.Distance(recordedSource, playerPositionAtEmission)
delay = distance / 343 m/s
```

The event retains that source even after Enemy relocation. Nothing is played for the Player at emission. Once `Time.time` reaches arrival, the service positions a pooled spatial emitter at the recorded source, plays the boom, reports the same Vector3 to radar, and optionally plays the cockpit beep. Multiple events may remain pending and due events are delivered chronologically.

Player motion during travel does not recalculate the delay. At arrival, the existing radar calculates bearing from the Player's then-current position. This is the documented first-prototype moving-receiver approximation.

## Radar Integration

Arrival calls only `KartikarisRadarController.ReportSoundArrival(recordedSource)`, preserving the radar's north-up mapping, one sampled `±15°` error, hint timing, and newest-event replacement. The sound service never accesses `RadarContact.SetSpotted`.

Automated runtime validation confirmed that the Sound Origin Hint activates after delay while the Enemy remains `IsSpotted == false` and radar-ineligible. The existing assigned sprite therefore shows historical approximate direction to the firing point, not a precise live enemy contact.

## Decisions and Rationale

- Reused the spatial tree architecture because it provides stable indices and localized queries without per-tree objects.
- Used source capsule metadata where it represents a trunk and explicit editable fallback values where source geometry is mesh-based.
- Moved collision resolution into the pre-movement path so stop/no-slide behavior is deterministic and independent of CharacterController collision normals.
- Applied momentum loss only; no artificial stun was introduced.
- Kept tree fall visual-only and deterministic rather than adding Rigidbody physics.
- Refactored the existing Enemy component to avoid two owners fighting over `cannon` or transform movement.
- Kept finite-speed delivery separate from radar presentation and spotting eligibility.
- Used a bounded reusable audio pool rather than creating/destroying an emitter for every event.

## Validation Performed

All Unity validation used `/home/shiki/Unity/Hub/Editor/6000.3.9f1/Editor/Unity` and respected one-editor-at-a-time execution. After every invocation, process state was checked before another Unity run. Failed compiler/test attempts left orphaned zero-byte `Temp/UnityLockfile` files; each was removed only after confirming no Unity, shader compiler, licensing client, or file owner remained. No generated directory was deleted or cleaned.

### Collider/source audit

- Inspected all ten exact prototype prefabs and counted six CapsuleCollider prototypes, four MeshCollider prototypes, and six MeshCollider components.
- Confirmed the saved Terrain collider flag was enabled before this work and disabled afterward.
- Confirmed fallen clones disable copied colliders and do not use Rigidbody simulation.
- Recorded SHA-256 hashes for all ten source prefab YAML files after implementation; none was edited by this task.

### Saved-scene validation

A Unity editor validator passed 27 checks covering:

- 25,000 authored trees and ten prototypes/profiles;
- six source-capsule-derived and four mesh-fallback profiles;
- disabled Terrain tree collisions;
- Player CharacterController, tree runtime, cockpit-look, FrontFeed RenderTexture, CockpitCamera, VisionHUD, and World Space radar references;
- `300 m` Enemy spawn and `100 m` relocation limits;
- `343 m/s` propagation;
- one enabled AudioListener;
- initially unspotted/radar-ineligible Enemy;
- no saved `FallenTrees` or falling visual;
- no projectile/shell/explosion/impact objects;
- zero missing scripts.

Result:

```text
KARTIKARIS_MILESTONE_SETUP_PASS profiles=10 sourceCapsules=6 meshFallbacks=4 listeners=1 checks=27
```

### Play Mode acceptance

Four of four GPU-backed tests passed:

1. A real 25,000-tree runtime-clone impact clamped movement with less than `0.02 m` lateral deviation, zeroed longitudinal speed, deactivated one stable tree exactly once, spawned an upright non-physical visual, allowed next-frame acceleration, and reached the terrain-tangent settled pose after the configured duration.
2. A source exactly 343 m away produced a computed `1.0 s` delay, created no immediate spatial emitter, delivered no early radar event at 0.45 s, then produced exactly one arrival, an active hint, fixed source, error within `±15°`, and no Enemy spot.
3. Enemy began grounded within 300 m, used coherent `cannon` preparation/relocation state, chose a destination within the production 100 m bound, completed relocation, dry-fired again, retained unspotted state, supported multiple pending propagation events, and created no projectile/shell/explosion/impact object.
4. Static delay checks passed for 100, 300, and 500 m at 343 m/s.

Final result:

```text
testcasecount=4 passed=4 failed=0
```

Temporary editor validators, test sources, test asmdefs, and the temporary runtime asmdef were removed. The final production-only Unity batch import/compile exited `0` with no C# errors or warnings.

The first scene setup attempt was blocked by a pre-existing orphaned lock. A subsequent compile attempt exposed and corrected an invalid assumption that Unity 6.3 publicly exposes the serialized Terrain tree-collider flag. Early test scaffolding attempts also corrected predefined-assembly referencing, zero-test discovery, and outgoing-scene reference timing. None of those invalid attempts is counted as a passing validation result.

Target `git status`/diff remains unavailable because the workspace has no functional repository metadata. A filesystem audit accounted for the files listed above. `Assets/Images/sound_origin_hint.png`, Synty assets, and unrelated materials were preserved.

## Known Issues / Limitations

- Virtual trunk defaults need subjective driving validation across all ten silhouettes.
- The four mesh-only prototypes use conservative name-based defaults rather than authored trunk measurements.
- Fallen visuals do not block, simulate mass, or guarantee exact branch-ground contact.
- Enemy moves directly across Terrain and can pass through trees; no AI navigation or avoidance exists.
- Sound has no occlusion, echoes, forest attenuation, terrain diffraction, Doppler, or moving-receiver interception.
- Delay uses receiver distance at emission; radar bearing uses receiver position at arrival.
- Synthesized clips are placeholders and may need volume/timbre tuning for the cockpit mix.
- Enemy animation indicates preparation only; the cannon is not actually aimed at the recorded target.
- Sound hints do not and must not spot the Enemy.
- The existing Synty `Nature/Soft Occlusion` Terrain-tree warnings remain.

## Manual Follow-up

1. Drive into thin, pine, generic, and big-tree trunks and tune only the serialized profile radius/height values that feel visually unfair.
2. Confirm the thunk → stop → fall → reaccelerate cadence feels readable at normal frame rate and tune the `0.7 s` fall duration if needed.
3. Listen from several enemy distances and tune cannon/tree/beep volumes without changing finite-speed timing.
4. Watch at least two production-speed Enemy cycles and assess direct relocation speed/rotation against the mech scale.
5. Look left and confirm the existing sound wedge artwork's north orientation; use the radar's existing rotation offset if the authored sprite requires compensation.
6. Profile the full 25,000-tree scene on target hardware.

## Suggested Next Step

Perform an interactive feel/tuning pass limited to trunk profiles, fall duration, placeholder audio mix, enemy relocation motion, and sound-wedge orientation. Keep projectiles, deployment, cannon aiming, combat AI, and spotting as separate explicitly requested milestones.
