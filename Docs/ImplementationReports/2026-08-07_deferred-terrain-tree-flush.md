# Deferred Terrain Tree Flush

## Summary

Deferred gameplay-triggered `Terrain.Flush()` calls until `LateUpdate` so artillery explosions can crush Terrain trees during physics contacts without asking Unity to rebuild Terrain-managed objects inside the contact callback.

## Files Created

- `Docs/ImplementationReports/2026-08-07_deferred-terrain-tree-flush.md`

## Files Modified

- `Assets/Scripts/WorldInteraction/KartikarisTreeInteractionRuntime.cs`
- `Docs/PROJECT_STATE.md`

## Scene / Prefab / Inspector Changes

None. Existing tree, Terrain, projectile, and explosion assignments are unchanged.

## Implementation Details

- Added a private pending-flush flag to `KartikarisTreeInteractionRuntime`.
- `TryCrushFirstBlockingTree`, `CrushTreesAlongSegment`, and `CrushTreesInRadius` now request a refresh instead of calling `Terrain.Flush()` directly.
- `LateUpdate` consumes the flag and performs at most one Terrain flush per rendered frame, after ordinary physics contact callbacks have finished.
- Multiple tree-crush operations before `LateUpdate` are automatically batched.
- Tree active-state changes, zero-scale `TreeInstance` writes, falling-tree creation, sound, diagnostics, and synchronous crushed-tree return counts remain unchanged.
- Runtime initialization and shutdown keep their direct Terrain flushes because they are not part of the gameplay physics-contact path.
- Shutdown clears any pending request before restoring the original TerrainData.

## Decisions and Rationale

The stack trace ended at `Terrain.Flush()` called from `CrushTreesInRadius` through the projectile's `OnCollisionEnter`. The projectile already uses deferred `Destroy(gameObject)`, and the runtime helper uses normal `Destroy` in Play Mode, so replacing explicit destruction was not the relevant fix. Deferring only the expensive Terrain refresh preserves immediate gameplay state and explosion results while moving Unity's internal Terrain object synchronization outside the forbidden callback.

## Validation Performed

- `Assembly-CSharp.csproj` built with zero errors.
- The build retained one unrelated existing warning from Mirza Beig's `DemoController.camera` member hiding `Component.camera`.
- Static inspection confirmed no gameplay tree-crush method directly invokes `Terrain.Flush()`.
- Static inspection confirmed all gameplay paths share one pending flag and one `LateUpdate` consumer.
- Static inspection confirmed the projectile still receives the synchronous crushed-tree count and still uses normal deferred `Destroy`.
- Unity Editor, batch mode, and Play Mode were not run.

## Known Issues / Limitations

- The original runtime error must be rechecked with a real projectile collision in Play Mode.
- The Terrain renderer refresh is deferred until `LateUpdate`, although virtual collision state and falling visuals change immediately.
- Initialization or shutdown invoked manually from another forbidden Unity callback would still require separate guarding; current project flow does not do that.
- Repository git metadata remains unavailable, so a normal git diff/status inspection could not be performed.

## Manual Follow-up

- Fire a shell into a group of Terrain trees and confirm the immediate-destruction error no longer appears.
- Confirm affected standing trees disappear and falling visuals appear without a visible extra-frame delay.
- Trigger several tree crushes in one frame and confirm every tree changes while the Console remains clean.
- Confirm Player/Enemy movement crushing still stops at and topples the first blocking tree correctly.

## Suggested Next Step

Run the focused projectile/tree Play Mode checks above before changing any Terrain or tree settings.
