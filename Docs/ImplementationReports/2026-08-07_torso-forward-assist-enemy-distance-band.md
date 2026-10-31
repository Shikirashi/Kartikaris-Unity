# Torso-Forward Artillery Assist and Enemy Distance Band

## Summary

The yellow artillery assist now derives heading from the same assigned torso/heading Transform used by the compass. Its presentation-only elevation is clamped into the torso-forward `0°..90°` firing hemisphere, preventing the deployment/folding animation's temporary backward and downward barrel poses from producing misleading assist directions.

Enemy startup placement now uses a Terrain-valid horizontal distance band of `300–1000 m` from Player. Subsequent relocation choices must remain in that band as well, so Enemy cannot autonomously cross inside `300 m`.

## Files Created

- `Docs/ImplementationReports/2026-08-07_torso-forward-assist-enemy-distance-band.md`

## Files Modified

- `Assets/Scripts/Combat/KartikarisFireControlPredictor.cs`
- `Assets/Scripts/Radar/KartikarisArtilleryAssistController.cs`
- `Assets/Scripts/Enemy/EnemyTest.cs`
- `Docs/PROJECT_STATE.md`

## Scene / Prefab / Inspector Changes

None. This was a code-only implementation.

The former serialized `maximumSpawnDistance` value migrates to `minimumSpawnDistance`. A separately named `spawnMaximumDistance` field retains the new `1000 m` initializer, avoiding a direct scene edit while preserving the existing `300 m` value as the lower bound.

## Implementation Details

- Added an assist-only direction reconstruction helper to `KartikarisFireControlPredictor`.
- Projects the weapon's assigned heading-pivot forward onto the Player/reference horizontal plane for compass-compatible heading.
- Measures signed physical muzzle elevation against the same up axis, clamps it to `0°..90°`, then reconstructs a direction in the forward hemisphere.
- Exposes the resulting heading direction, launch direction, and clamped elevation as runtime diagnostics.
- Drives heading-assist rotation from the explicit prediction heading rather than inferring bearing from the predicted endpoint.
- Samples Enemy startup positions uniformly over the Terrain rectangle and accepts only points inside the configured Player-distance band.
- Rejects later relocation candidates that would finish nearer than `300 m` or farther than `1000 m` from Player.
- Reports a placement error and leaves Enemy unmoved if the bounded search finds no valid Terrain point; it never falls back to a too-close position.

## Decisions and Rationale

Assist clamping changes only the firing-computer presentation. Real artillery remains physics-driven and continues launching from the physical `firePoint.forward`, preserving existing aiming and firing behavior.

Sampling over Terrain first avoids the old behavior where an annulus candidate could be clamped to the Terrain edge and silently collapse inside the requested minimum distance. The configured upper bound remains `1000 m`, although the current Terrain geometry naturally determines which distances are reachable.

## Validation Performed

- Built `Assembly-CSharp.csproj` with `dotnet build --no-restore`.
- Result: zero errors and one existing unrelated warning from `Assets/Mirza Beig/Cinematic Explosions FREE/Scripts/DemoController.cs` about `DemoController.camera` hiding `Component.camera`.
- Statically confirmed the saved scene assigns the same Transform file ID to the Player weapon heading pivot and heading HUD source.
- Inspected the changed source paths for physical-fire separation, distance checks, serialized-value migration, and bounded failure behavior.

Unity Editor and Play Mode were not started.

## Known Issues / Limitations

- The current square Terrain cannot necessarily provide every radius up to the configured `1000 m`; placement is random over the valid intersection between the Terrain and distance band.
- Startup placement still does not reject visual/virtual tree overlap.
- The transition-animation presentation and repeated random placement still require interactive Play Mode validation.

## Manual Follow-up

1. Re-enter Play Mode several times and confirm Enemy always starts at least `300 m` away while remaining on Terrain.
2. Observe several relocation cycles and confirm no destination crosses inside `300 m`.
3. Compare assist heading against the cockpit compass while rotating the torso.
4. Watch the full deploy/fold animation and confirm the always-visible assist stays in the torso-forward hemisphere even while the physical barrel points backward or downward.
5. Fire controlled Standard HE/Airburst shots after deployment and confirm the settled assist still matches physical results.

## Suggested Next Step

Run the focused Play Mode checks above, then tune the existing predictor collision layer and sweep-radius scene values to match the real projectile collider and valid-impact mask.
