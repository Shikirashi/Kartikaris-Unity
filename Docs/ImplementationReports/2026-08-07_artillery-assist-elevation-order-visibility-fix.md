# Artillery Assist Elevation Order and Visibility Fix

## Summary

Fixed the yellow elevation assist remaining near the radar centre while heading responded, and changed valid Standard HE/Airburst assistance to show in every Player operating state by default.

## Files Created

- `Docs/ImplementationReports/2026-08-07_artillery-assist-elevation-order-visibility-fix.md`

## Files Modified

- `Assets/Scripts/Combat/KartikarisFireControlPredictor.cs`
- `Assets/Scripts/Radar/KartikarisArtilleryAssistController.cs`
- `Docs/PROJECT_STATE.md`

## Scene / Prefab / Inspector Changes

None.

The user's saved assist hierarchy and references were inspected read-only and found correctly wired. The scene remains manually owned.

## Implementation Details

Unity Animator evaluation restores the animated elevation pivot to its authored base pose before LateUpdate scripts. If `KartikarisArtilleryAssistController.LateUpdate` ran before the weapon/controller LateUpdates, prediction read the restored near-vertical `firePoint.forward`, repeatedly producing an impact near the Player and an approximately `0.6` radar radius. Heading continued working because its pivot was not being restored in the same way.

`KartikarisFireControlPredictor.TryPredict` now calls `KartikarisCannonWeapon.ApplyProceduralPoseAfterAnimation()` immediately before reading the fire point. This is the same idempotent final-pose method already called immediately before actual projectile spawning. Its smoothing is already limited to one update per frame, so repeated same-frame calls do not advance aim twice or accumulate rotation.

`KartikarisArtilleryAssistController` now exposes `requireFireMode`, disabled by default. With it disabled, valid Standard HE/Airburst assistance remains visible across Travel, transitions, Fire, and post-fire commitment. Enabling it restores the former fully-entered-Fire-only behavior. Missing/invalid trajectories and Homing/Guided still hide to avoid false static solutions.

## Decisions and Rationale

- Prediction explicitly finalizes the weapon pose instead of relying on unspecified MonoBehaviour LateUpdate ordering.
- The firing path and prediction path now sample the same final physical muzzle orientation.
- Always-visible means every operating state with a valid authoritative ballistic solution; it does not override the intentional Homing/Guided/invalid hiding policy.

## Validation Performed

- Read-only scene inspection confirmed Player, weapon, Terrain, predictor, radar, HeadingAssist, ElevationAssist, sprites, nesting, stretch anchors, layers, and raycast settings are assigned correctly.
- Source analysis identified the Animator/procedural LateUpdate order difference between heading and elevation.
- `Assembly-CSharp.csproj` built successfully with zero errors and one unrelated pre-existing Mirza Beig warning.
- No Unity Editor/Play Mode process was launched.

## Known Issues / Limitations

- Runtime radial motion still requires Play Mode confirmation.
- The scene predictor currently uses `Everything` and `0.05 m`; the real projectile mask is `1112` and its capsule radius is `0.0025 m`. These should be matched manually to prevent predicted grazing contacts.
- Radar-edge clamping still deliberately flattens all predictions beyond the radar's `500 m` range to one edge radius.

## Manual Follow-up

1. Let Unity import/recompile, keep `Require Fire Mode` disabled, and enter Play Mode.
2. Change elevation and confirm `Predicted Radar Position` and `Displayed Radius` now update while heading behavior remains correct.
3. Set Prediction Layers to Enemy, Water, World, and Player; set Sweep Radius near `0.0025`.
4. Compare controlled predicted and real Standard HE impacts before tuning radial scale/clamping.

## Suggested Next Step

Run the focused elevation sweep from vertical toward horizontal and compare several predicted positions against actual shell impacts.
