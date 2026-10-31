# Artillery Assist, Module Actions, and Acoustic Affiliation

## Summary

Implemented the requested code-only milestone:

- ported Closing IN's recognizable nested yellow artillery-assist presentation to the north-up cockpit radar;
- added reusable swept Player ballistic impact/Airburst prediction that preserves the `1x/2x/3x` spatial solution;
- refactored top-row module slots into per-module button actions without a gameplay switch;
- added shared owner/affiliation-aware deployable identity, destruction, and cleanup;
- implemented Sound Decoy, mounted EMP Blaster, and the Tracking Drone state machine;
- made cannon/decoy acoustic events affiliation-aware and Sound Probe receivers relationship-filtered;
- made radar spots source-owned so multiple spotters and manual/debug spotting coexist safely;
- extended artillery explosion damage to optional deployable health.

No scene, prefab, ScriptableObject asset, imported image, texture, material, hierarchy, Inspector value, serialized reference, package, or ProjectSettings file was changed.

## Observed Existing Behavior

Before this task, `KartikarisModuleController` directly instantiated each definition's prefab once, specialized Sound Probe initialization inside the controller, and ignored later button presses. Cannon events had stable IDs and independent finite-speed receiver delivery, but carried no emitter affiliation, so every probe received Player fire. `RadarContact` had one boolean spot flag with no source ownership. There was no Player impact predictor or yellow assist presentation in Kartikaris.

The existing real projectile uses base launch velocity `v`, runtime velocity `v × k`, and private gravity `g × k²`. Airburst arms after exceeding configured height plus margin and detonates on the descending ground-clearance crossing. These behaviors remain authoritative.

## Donor Audit and Adaptation

The read-only donor is `/home/shiki/Documents/github/FireForEffect`, identified by the earlier radar-port report. Relevant donor sources were:

- `Assets/Scripts/FireForEffect/AimingAssistController.cs`;
- `Assets/Editor/FireForEffectSceneSetup.cs`;
- `Assets/Scenes/artillery.unity`;
- `Assets/Images/heading assist.png`;
- `Assets/Images/elevation assist.png`.

The donor uses a full-radar `HeadingAssist` Image and a child `ElevationAssist` Image. Heading rotates the parent clockwise (`-bearing` in UGUI Z rotation); predicted range slides the child along parent-local Y and clamps it to the radar radius. Both show only in the donor's easy/aiming state. It does not use a predicted endpoint dot for this graphic.

Kartikaris preserves the rotate-parent/slide-child presentation and optional rotation/radial tuning, but replaces the donor's flat ideal-range/charge formula with current fire-point/world collision prediction. Visibility is Player Fire-mode plus valid prediction rather than donor difficulty. The controller uses explicit RectTransform assignments and creates no Canvas hierarchy.

The target's already imported assist PNGs were not recreated or modified. Target/donor SHA-256 and Unity GUIDs match:

- heading: `adebd9a9d0993908b13afe27049e2ba872c6ada2e7dd74e0befe75c9a84ce586`, GUID `cc2629154e6d878d4a9c90864ed6cf05`;
- elevation: `bb1033c5ef47ff80cb8e2674e27da662d3ef9efa20ac5b0ac302b85438f1577c`, GUID `ca070b887c5ed46ea859d6cd52c4caa5`.

The donor was available read-only and was not modified.

## Files Created

- `Assets/Scripts/Combat/KartikarisFireControlPredictor.cs`
- `Assets/Scripts/Radar/KartikarisArtilleryAssistController.cs`
- `Assets/Scripts/Modules/KartikarisDeployable.cs`
- `Assets/Scripts/Modules/KartikarisDeployableHealth.cs`
- `Assets/Scripts/Modules/KartikarisModuleAction.cs`
- `Assets/Scripts/Modules/KartikarisSoundDecoy.cs`
- `Assets/Scripts/Modules/KartikarisSoundDecoyModuleAction.cs`
- `Assets/Scripts/Modules/KartikarisEmpModuleAction.cs`
- `Assets/Scripts/Modules/KartikarisTrackingDrone.cs`
- `Assets/Scripts/Modules/KartikarisTrackingDroneModuleAction.cs`
- `Docs/ImplementationReports/2026-08-07_artillery-assist-modules-acoustic-affiliation.md`

Unity `.meta` files were not manually authored; Unity will create them on the next normal import.

## Files Modified

- `Assets/Scripts/Audio/KartikarisAcousticReceiver.cs`
- `Assets/Scripts/Audio/KartikarisSoundPropagation.cs`
- `Assets/Scripts/Combat/KartikarisCannonWeapon.cs`
- `Assets/Scripts/Combat/KartikarisCombatAffiliation.cs`
- `Assets/Scripts/Combat/KartikarisExplosion.cs`
- `Assets/Scripts/Modules/KartikarisModuleController.cs`
- `Assets/Scripts/Modules/KartikarisModuleDefinition.cs`
- `Assets/Scripts/Modules/KartikarisSoundProbe.cs`
- `Assets/Scripts/Radar/KartikarisRadarController.cs`
- `Assets/Scripts/Radar/RadarContact.cs`
- `Docs/PROJECT_STATE.md`

`Assembly-CSharp.csproj` was temporarily given compile entries for the new scripts for validation, then restored. It is not an intentional final modification.

## Scene / Prefab / Inspector Changes

None.

All setup and wiring remain manual by request.

## Implementation Details

### Ballistic prediction and yellow assists

`KartikarisFireControlPredictor` evaluates:

`p(t) = firePoint.position + baseVelocity × t + 0.5 × Physics.gravity × t²`

where base velocity uses the current fire-point forward and the locked shell's effective base muzzle velocity. This is not an independent gameplay arc: evaluating the real scaled projectile at real time `t/k` reduces algebraically to the same spatial equation. Reported real flight time is divided by `k`.

Every consecutive sample is ray/sphere swept with non-alloc casts through configurable layers. Hits are filtered by the Player root and nearest valid distance. Standard HE reports the first hit. Airburst reproduces configured height/margin arming and interpolates the descending ground-clearance crossing, while still respecting an earlier physical world hit. Homing and Guided Missile return no prediction.

`KartikarisArtilleryAssistController` maps the predicted world point through `KartikarisRadarController.WorldPositionToRadar`, rotates the heading parent using the radar's north-up bearing convention, and slides the child to the mapped radius. It polls live aim and configuration in `LateUpdate`, shows only in fully entered Player Fire mode, optionally clamps to `EffectiveRadarRadius`, and never creates an endpoint contact.

### Module action refactor

Each slot can explicitly reference a `KartikarisModuleAction`; the controller initializes it with owner, owner affiliation, mount, definition, slot index, and sound service, then only forwards top-row `1`/`2` presses. An empty specialized-action field gets a generic one-shot deploy action, preserving existing Sound Probe semantics without a module-type switch. Module definitions now identify Sound Probe, Sound Decoy, EMP Blaster, or Tracking Drone but do not cycle at runtime.

### Shared deployable lifecycle and damage

`IKartikarisDeployable` and the base `KartikarisDeployable` expose owner, owner affiliation, deployment/destruction state, and one `DestroyDeployable(reason)` path. Reasons distinguish damage, EMP, battery, recovery, owner loss, and external destruction. Cleanup/event notification runs once before disable/destruction.

Sound Probe now uses that lifecycle. Disabling it unregisters `KartikarisAcousticReceiver`; unregistration also removes pending receiver deliveries. Decoy/drone actions subscribe to destruction so stale runtime references are cleared and the correct terminal/cooldown state begins.

`KartikarisDeployableHealth` defaults to configurable `100 HP`. Radial and cone explosions resolve it once across multiple colliders alongside, but independently from, mech health. Tracking Drone intentionally has no invented normal-health default.

### Affiliation-aware acoustics and deception

Cannon/decoy events now carry `KartikarisAffiliation` through pending scheduling and delivered detections while retaining stable event IDs, timing, and internal source positions. `KartikarisCannonWeapon` supplies its existing source affiliation. Decoy supplies its runtime owner affiliation.

Receivers expose `All`, `HostileOnly`, and `NonFriendly` policies plus optional Neutral acceptance. Sound Probe initialization sets `HostileOnly`. Hostility is evaluated through the shared affiliation relationship: Player and Friendly are allied; Hostile is opposing; Neutral is not hardcoded as either side. Only accepted events are scheduled, so TDOA cannot correlate rejected friendly emissions.

The direct Player delivery remains separately configurable and still drives cockpit boom/beep/sound-origin presentation regardless of probe policy.

Sound Decoy press one deploys silently. Later ready presses emit one ordinary cannon acoustic event, one explicitly assigned muzzle effect with prefab local X aligned to activation-point forward, and one detached smoke effect. No projectile, impact, explosion, or landing event is created. Successful activation alone starts the configurable `15 s` cooldown. Damage/EMP disables it permanently for that deployment.

### EMP

EMP is a mounted action with configurable `30 m` radius, `60 s` cooldown, layer mask, buffer capacity, origin, and optional VFX. A ready press runs one `OverlapSphereNonAlloc`, resolves `IKartikarisDeployable` parents, deduplicates them, and destroys all factions. It never invokes mech health, projectile, Terrain, or tree APIs. One completed pulse starts cooldown even if no deployable was found.

### Tracking Drone

The drone records deployment position and the owner's horizontal forward once. Outbound always follows that fixed direction to configurable `500 m`; later owner rotation cannot bend the route. All movement uses `Vector3.MoveTowards` with acceleration and a hard `7 m/s` 3D displacement cap. Each local waypoint samples assigned Terrain, active Terrain, or a bounded ground-ray fallback and targets `15 m` clearance without constant-world-Y assumptions.

A non-alloc overlap runs at a configurable interval and provisional `100 m` range. It accepts only living relationship-hostile `KartikarisMechHealth` targets inside a `30°` full cone whose center is pitched `45°` down. The nearest first valid candidate is locked and never swapped while valid. Tracking uses a target-local horizontal offset while Terrain still owns altitude.

Second press in Outbound/Tracking recalls. Reaching `500 m` or losing/dying target also returns to the owner's current position. Arrival is horizontal-tolerance based because the drone remains at clearance above ground. Successful recovery starts configurable `120 s` cooldown. The `300 s` battery continues through every flight state; expiry, including during return, destroys the drone and enters the configurable `120 s` loss cooldown. EMP/external/owner loss use the same loss path.

### Source-aware spotting

`RadarContact` retains serialized/manual `SetSpotted`. Runtime source IDs are an additional set. Drone acquisition adds its instance ID; recall, target loss, recovery, battery, EMP, or destruction removes exactly that ID. Eligibility uses manual spotted OR at least one source, preventing one drone from clearing another valid spot.

## Decisions and Rationale

- Base-time prediction makes ballistic multiplier spatial invariance explicit and avoids per-frame projectile/physics-object creation.
- Segment sweeps prevent isolated samples from jumping through Terrain/world colliders.
- The donor's two-image presentation is preserved because it is recognizable and requires no invented impact dot.
- Per-module actions keep repeated activation, recall, and cooldown rules out of the slot controller.
- One deployable contract makes EMP and cleanup extensible without scene-wide scans.
- Relationship-based acoustic filters naturally support Player-owned and future Enemy-owned probes/decoys.
- Transform drone movement keeps this prototype independent of NavMesh/Rigidbody flight while enforcing the requested absolute speed ceiling.
- Loss uses the same `120 s` default as successful recovery, satisfying replacement commitment without inventing a different balance value.
- `100 m` detection range and `(-20 m local Z)` tracking offset are explicitly provisional/tunable because only the range default was directed and no final follow placement was specified.

## Validation Performed

- Read `Docs/PROJECT_STATE.md`, the prior Closing IN radar report, donor script/editor/scene serialization, and all affected current runtime sources before implementation.
- Confirmed target/donor assist PNG SHA-256 hashes and Unity GUIDs match.
- Temporarily included all ten new production scripts in generated `Assembly-CSharp.csproj`, restored the missing NuGet project assets normally, and ran `dotnet build --no-restore`.
- Final production compile result: zero errors, one pre-existing vendor warning from Mirza Beig `DemoController.camera` hiding `Component.camera`.
- Removed all temporary generated-project compile entries after validation.
- Static checks confirmed top-row `<Keyboard>/1` and `/2`; no module gameplay switch; no `FindObjectsOfType`/scene-wide per-frame module scan; non-alloc EMP/drone queries; exact requested configurable defaults; owner/affiliation propagation; receiver rejection before scheduling; pending receiver cancellation; source-owned spot add/remove on every drone terminal path; no assist endpoint dot; and no runtime UI hierarchy creation.
- No Unity Editor or Play Mode process was started. No scene/prefab wiring exists yet, so the requested controlled runtime acceptance matrix was not claimed as executed.
- Git status/diff remains unavailable because the workspace has no functional git worktree metadata. Intentional files are accounted for in this report.

## Known Issues / Limitations

- All new references and prefabs require manual Unity setup.
- Predictor sampling/layers and swept radius require calibration against actual projectile collider size and representative Terrain/world shots.
- Predictor ignores virtual Terrain trees intentionally and models no drag, wind, or later Homing/Guided steering.
- PhysX integration and Airburst runtime crossing have not been visually compared with prediction.
- Yellow image authored scale, rotation offset, mask clipping, and cockpit readability need Game View tuning.
- Drone has no obstacle avoidance, Rigidbody flight, final model/animation, normal HP, retrieval inventory, or enemy AI.
- Drone detection buffer capacity is finite/configurable; an unusually dense candidate volume can truncate the overlap result.
- Terrain clearance is speed-limited with the rest of 3D movement, so extreme terrain discontinuities can temporarily produce clearance error rather than violating `7 m/s`.
- Sound Decoy uses the existing synthesized cannon event; final audio/VFX assets remain user assignments.
- New source `.meta` files will be generated by Unity on next import.

## Manual Follow-up

1. Yellow assist: add nested heading/elevation Images under the radar mask, assign the imported sprites and RectTransforms, then wire Player/weapon/Terrain/prediction layers/radar to predictor and assist; leave endpoint-dot UI absent.
2. Sound Probe: keep/add deployable, acoustic receiver, affiliation-owning Player, and Collider on the EMP layer; use hostile-only filtering. An empty slot action preserves generic one-shot deployment.
3. Sound Decoy: create its definition/prefab with decoy, `KartikarisDeployableHealth = 100`, Collider/layer, propagation, activation point, muzzle/smoke prefabs, `15 s`, and assign `KartikarisSoundDecoyModuleAction` to the slot.
4. EMP: assign `KartikarisEmpModuleAction`, owner/module origin, deployable mask, `30 m`, `60 s`, and optional VFX; its definition needs no world prefab.
5. Tracking Drone: create its definition/prefab with drone and Collider/layer; assign Terrain/ground and target masks; confirm `500 m`, `7 m/s`, `15 m`, `30°`, `45°`, provisional `100 m`, `300 s`, tracking offset/arrival tolerance, and action cooldowns `120 s`.
6. Run the focused acceptance checks in `Docs/PROJECT_STATE.md`: assist versus real Standard/Airburst at `1x/2x/3x`, friendly/hostile probe filtering and decoy deception, duplicate-collider/faction EMP, drone route/clearance/recall/battery/spot cleanup, and baseline Player/Enemy artillery/radar/TDOA regressions.

## Suggested Next Step

Wire the five manual groups above, then tune the yellow assist and drone using controlled Play Mode shots/targets before adding final module art or Enemy module AI.
