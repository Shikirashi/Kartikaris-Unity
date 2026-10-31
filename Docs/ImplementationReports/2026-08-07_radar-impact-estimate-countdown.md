# Radar Impact Estimate Countdown

## Summary

The user-created `RadarRoot/ImpactEstimate` TextMeshProUGUI label now displays a successful Player shell's estimated remaining flight time in the form `Impact: 5.53s`. It begins only after the cannon reports a successful launch, counts down using game time, and hides when that exact projectile is destroyed by impact or lifetime expiry.

## Files Created

- `Docs/ImplementationReports/2026-08-07_radar-impact-estimate-countdown.md`

## Files Modified

- `Assets/Scripts/Radar/KartikarisArtilleryAssistController.cs`
- `Assets/Scripts/Combat/KartikarisFireControlPredictor.cs`
- `Docs/PROJECT_STATE.md`

## Scene / Prefab / Inspector Changes

None. The existing user-created `ImpactEstimate` object was inspected read-only and not edited.

It is already a direct `RadarRoot` child with `TextMeshProUGUI`, so the controller's exact-name fallback resolves it automatically. The new serialized reference can be assigned manually if the object is later renamed or nested.

## Implementation Details

- Added an optional `TMP_Text impactEstimate` reference to `KartikarisArtilleryAssistController`.
- Resolves direct child `ImpactEstimate` automatically when the reference is empty and disables its raycast targeting.
- Subscribes only to the predictor's Player `KartikarisCannonWeapon.Fired` event.
- On successful launch, obtains one Standard/Airburst prediction and stores `estimatedImpactTime = Time.time + prediction.FlightTime`.
- Tracks the projectile returned by the successful firing event rather than following later aim changes.
- Updates the label through TMP's allocation-conscious numeric formatting as `Impact: {seconds with two decimals}s`.
- Uses scaled game time so pause/time-scale behavior stays aligned with Rigidbody flight.
- Keeps displaying `0.00s` if the estimated time elapses before the physical projectile resolves, then hides when Unity destroys the projectile.
- Clears and hides on component disable, failed prediction, actual impact destruction, or projectile lifetime expiry.
- Added projectile-collider rejection to fire-control prediction so a shell spawned immediately before the `Fired` callback cannot be mistaken for the predicted world impact.

## Decisions and Rationale

The countdown represents an actually launched shot, not a continuously changing pre-fire aim preview. Capturing once at successful launch ensures later cannon movement cannot alter the in-flight ETA and rejected fire attempts cannot create misleading timers.

Tracking the returned projectile gives a simple authoritative end condition without modifying projectile gameplay or adding a new impact event. The timer remains an estimate: PhysX integration and unexpected contacts can make real impact earlier or later.

## Validation Performed

- Built `Assembly-CSharp.csproj` with `dotnet build --no-restore` after regenerating its missing temporary restore metadata.
- Result: zero errors and one existing unrelated Mirza Beig warning about `DemoController.camera` hiding `Component.camera`.
- Statically confirmed the scene's `ImpactEstimate` is a direct `RadarRoot` child and uses `TMPro.TextMeshProUGUI`.
- Statically confirmed Player-weapon event ownership, successful-fire-only callback timing, two-decimal formatting, scaled-time countdown, tracked-projectile cleanup, and predictor projectile filtering.

Unity Editor and Play Mode were not started.

## Known Issues / Limitations

- The countdown is based on the no-drag fire-control prediction captured at launch; collisions, PhysX integration, and unmodeled behavior can change actual impact time.
- If the prediction reaches zero before the projectile resolves, the label remains at `Impact: 0.00s` until that projectile is destroyed.
- Homing and Guided Missile trajectories deliberately have no static fire-control prediction, so they do not start this countdown.
- Only the latest successfully fired Player projectile is tracked if future cooldown/loadout tuning permits overlapping shots.

## Manual Follow-up

1. Enter Play Mode and confirm the label starts hidden.
2. Fire a Standard HE or Airburst shell and verify the exact `Impact: 5.53s`-style format counts down.
3. Move the aim after firing and confirm the in-flight countdown does not change course or reset.
4. Confirm the label hides on shell impact/lifetime and does not activate for a rejected or Enemy shot.
5. Compare several estimates against observed physical impacts and tune predictor sampling/collision layers if discrepancies are material.

## Suggested Next Step

Run controlled short/high-angle Standard HE and Airburst shots to measure ETA error against actual impact time before adding any optional impact-state wording or audible countdown cues.
