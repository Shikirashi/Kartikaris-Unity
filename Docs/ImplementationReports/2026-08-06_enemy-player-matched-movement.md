# Enemy Player-Matched Movement

Date: 2026-08-06  
Project: `/home/shiki/Documents/github/Kartikaris`  
Unity: `6000.3.9f1`

## Outcome

Replaced the Enemy's `10 m/s`, `120°/s`, instant-speed Transform relocation with the same movement model and tuning used by Player. The only teleport that remains is the intentional one-time random startup placement within 300 m. Every relocation after startup now turns, accelerates, moves, brakes, and interacts with trees through player-style tank locomotion.

## Shared Tuning

`MechFirstPersonController` now exposes read-only accessors for its existing acceleration, deceleration, gravity, and grounded vertical velocity alongside its existing speed and turn-rate accessors. `EnemyTest` holds a serialized reference to the Player controller and reads those values live rather than keeping duplicate speed fields.

Current shared values are:

| Setting | Player | Enemy |
|---|---:|---:|
| Forward speed | 6 m/s | 6 m/s |
| Acceleration | 12 m/s² | 12 m/s² |
| Deceleration | 16 m/s² | 16 m/s² |
| Turn speed | 45°/s | 45°/s |
| Gravity | -25 m/s² | -25 m/s² |
| Grounded vertical velocity | -2 m/s | -2 m/s |

Changing these Player values now changes Enemy relocation tuning automatically at runtime.

## Movement Behavior

The autonomous destination loop remains bounded to 100 m and retains the existing slope/attempt rules. During `Relocating`, the Enemy now:

1. Calculates the signed angle from its own forward vector to the destination.
2. Turns in place at no more than the Player's turn rate.
3. Applies forward throttle once aligned within 15°.
4. Accelerates toward the Player's forward-speed cap.
5. Calculates braking distance from current speed and the Player's deceleration.
6. Releases throttle and decelerates before the one-metre arrival tolerance.
7. Settles only after speed reaches approximately zero.

Translation is always along `Enemy.transform.forward`; there is no sideways movement. The autonomous prototype does not use reverse throttle, NavMesh, route planning, or obstacle avoidance.

## CharacterController and Trees

The saved `Enemy` object now has exactly one CharacterController and one `MechTreeCrusher`. Its controller copies the Player's current geometry:

- height: 4 m
- radius: 1 m
- centre Y: 2 m
- slope limit: 45°
- step offset: 0.5 m
- skin width: 0.08 m
- minimum move distance: 0.001 m

The Enemy crusher shares the same `KartikarisTreeInteractionRuntime`, `1.25 m` crush radius, and `0.03 m` impact skin as Player. A blocking virtual trunk clamps that frame's movement, deactivates/falls once, and zeros Enemy longitudinal speed. The Enemy can then accelerate again on the following frame just like Player.

The existing Animator continues to receive only the `cannon` bool. No guessed walking parameter or new leg-animation controller was introduced.

## Validation

All Unity invocations used `6000.3.9f1`, one process at a time. Before each run and after every exit, Unity Editor, shader compiler, and licensing processes were checked. The first setup attempt encountered a zero-byte orphaned `Temp/UnityLockfile`; it was removed only after repeated empty process checks and an empty `lsof` owner check. No Unity-generated directory was deleted or cleaned.

Saved-scene validation passed:

- exactly one Enemy CharacterController and one Enemy `MechTreeCrusher`;
- matching Player/Enemy controller height, radius, centre, slope, and step values;
- shared tree-interaction runtime and crush radius;
- serialized Player movement-tuning reference;
- equal speed, acceleration, deceleration, and turn-rate values;
- retained 100 m relocation bound;
- zero missing scripts.

Result:

```text
KARTIKARIS_ENEMY_MATCHED_MOVEMENT_SETUP_OK
```

A live production-scene Play Mode run observed the real dry-fire-to-relocation transition and measured each movement frame. It confirmed gradual acceleration, real translation, speed and displacement below the Player cap, and rotation below the Player yaw-rate cap.

```text
KARTIKARIS_ENEMY_MATCHED_MOVEMENT_PLAYMODE_OK playerSpeed=6.0 enemyMaxObserved=6.000 acceleration=12.0 deceleration=16.0 turnSpeed=45.0
```

The temporary setup/validation asset and metadata were removed after validation. Headless `-nographics` emitted the existing render-pass warnings, but there were no movement exceptions or failed checks.

## Files Changed

- `Assets/Scripts/Enemy/EnemyTest.cs`
- `Assets/Scripts/Player/MechFirstPersonController.cs`
- `Assets/Scenes/Kartikaris_MapTest.unity`
- `Docs/PROJECT_STATE.md`
- `Docs/ImplementationReports/2026-08-06_enemy-player-matched-movement.md`

Repository git metadata remains unavailable, so the final audit uses filesystem and serialized-scene inspection rather than `git status` or `git diff`.
