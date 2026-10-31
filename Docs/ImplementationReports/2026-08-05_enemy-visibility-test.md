# Enemy Visibility Test

## Summary

Implemented a focused enemy-visibility test for the existing duplicate mech in `Kartikaris_MapTest`. At startup the Enemy root is placed on the Terrain at a uniformly random horizontal position within 300 m of Player. Its existing Animator Controller's `cannon` bool starts false, becomes true after five seconds, and alternates every five seconds thereafter.

This implementation only supports forest-concealment observation. It does not add enemy movement, navigation, detection, targeting, combat, or AI.

## Observed Existing Behavior

- The scene already contained an active root GameObject named `Enemy` with a duplicate of the same mech FBX used by Player.
- The duplicate mech already had an Animator using `Assets/3D/Kartikaris Mech.controller`.
- The controller already contained a bool parameter named exactly `cannon` and transitions between `mech_idle` and `cannon_standby` driven by that parameter.
- Runtime tree interaction and `MechTreeCrusher` had already been restored to the scene before this task; they were preserved unchanged.

## Files Created

- `Assets/Scripts/Enemy.meta`
- `Assets/Scripts/Enemy/EnemyTest.cs`
- `Assets/Scripts/Enemy/EnemyTest.cs.meta`
- `Docs/ImplementationReports/2026-08-05_enemy-visibility-test.md`

## Files Modified

- `Assets/Scenes/Kartikaris_MapTest.unity`
- `Docs/PROJECT_STATE.md`

## Scene / Prefab / Inspector Changes

Added `EnemyTest` to the existing scene `Enemy` root and assigned:

- Player: scene `Player` transform;
- Target Terrain: scene `Terrain` component;
- Maximum Spawn Distance: `300` m;
- Ground Offset: `0` m;
- Enemy Animator: the duplicate mech child's existing Animator;
- Cannon Toggle Interval: `5` seconds.

No prefab, Animator Controller, animation clip, TerrainData, tree, package, or ProjectSettings asset was modified.

## Implementation Details

`EnemyTest` samples `Random.insideUnitCircle * maximumSpawnDistance`, giving a uniform random point over the horizontal disk rather than concentrating results by choosing a uniformly random radius. It adds that offset to Player X/Z, clamps the result to the assigned Terrain bounds, samples Terrain height, and moves the Enemy root to the grounded position.

Explicit scene references are assigned, with defensive fallback resolution for Player, Terrain, and the child Animator. Placement also has a component context command for focused Editor testing.

The Animator parameter hash is cached. Startup verifies that the assigned Animator has a bool parameter named `cannon`, explicitly initializes it to false, and schedules the first toggle five seconds later. Subsequent toggles schedule from the actual toggle time, preventing multiple rapid toggles after a delayed frame.

## Decisions and Rationale

- Randomized only horizontal position; Terrain determines vertical placement.
- Used the existing Animator Controller and parameter without changing animation assets.
- Kept the behavior on the Enemy root so the duplicate visual remains a child and can be replaced later.
- Did not enforce a minimum distance because the requested rule was only "within 300 m"; uniform disk sampling naturally favors more area at larger radii.
- Did not avoid Terrain trees because they are visual-only and overlap can still be useful in a concealment test.

## Validation Performed

- Confirmed the saved scene contains the Enemy root, duplicate mech, child Animator, Player, and valid Terrain.
- Confirmed `Kartikaris Mech.controller` contains a bool parameter named `cannon` and matching true/false transitions.
- Built `EnemyTest.cs` through the generated `Assembly-CSharp.csproj` against the Unity 6000.3.9f1 assembly references: succeeded with zero errors and zero warnings. The temporary project-file inclusion was removed afterward.
- Inspected the serialized scene hookup and confirmed all three object references plus values `300` and `5` are present.
- Confirmed the new Unity GUIDs are unique within `Assets`.
- Git diff/status remained unavailable because the workspace does not expose functional repository metadata.

Unity batch compilation and Play Mode observation were attempted but blocked because another Unity instance had the project open. No claim is made that the timed runtime behavior was interactively observed.

## Known Issues / Limitations

- The open Unity Editor may prompt to reload the externally modified scene. Reload/accept the on-disk version before saving from an older in-memory scene state.
- The Enemy may occasionally spawn very near Player because no minimum distance was requested.
- The Enemy may overlap a visual Terrain tree.
- Placement is nondeterministic between Play Mode starts by design.
- The script does not orient the Enemy toward Player.
- There is no enemy behavior beyond placement and animation toggling.

## Manual Follow-up

1. Return to Unity and reload `Kartikaris_MapTest` if Unity reports that the scene changed on disk.
2. Enter Play Mode several times and confirm Enemy appears grounded at different positions within 300 horizontal metres of Player.
3. Watch the enemy Animator for at least ten seconds: `cannon` should be false initially, true after five seconds, and false again after ten seconds.
4. Observe the enemy at different distances and decide whether 25,000 trees provide enough concealment.

## Suggested Next Step

Record the concealment result at representative random distances before changing forest density. Keep enemy AI and combat outside this focused test.
