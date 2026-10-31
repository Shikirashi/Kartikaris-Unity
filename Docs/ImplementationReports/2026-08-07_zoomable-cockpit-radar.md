# Zoomable Cockpit Radar

## Summary

The cockpit radar now supports presentation-only mouse-wheel zoom through the Unity New Input System. Its runtime display range starts at `3000 m`, zooms proportionally between configurable `250 m` and `3000 m` bounds, and remains the single mapping scale for world-position radar information.

Contacts and yellow artillery prediction hide outside the current range and return when zooming out. Existing TDOA curves reproject only when range changes, using unchanged historical acoustic measurements.

## Files Created

- `Docs/ImplementationReports/2026-08-07_zoomable-cockpit-radar.md`

## Files Modified

- `Assets/Scripts/Radar/KartikarisRadarController.cs`
- `Assets/Scripts/Radar/KartikarisArtilleryAssistController.cs`
- `Assets/Scripts/Radar/KartikarisAcousticLocalization.cs`
- `Assets/Scripts/Radar/KartikarisTdoaHyperbolaGraphic.cs`
- `Docs/PROJECT_STATE.md`

## Scene / Prefab / Inspector Changes

None. This implementation is code-only and retains the existing radar hierarchy and references.

The former serialized `radarRange` field migrates to `defaultRadarRange`. The saved scene already contains `3000`; an older untouched `500 m` component default is promoted once to the new `3000 m` default, while deliberate non-default legacy values are retained.

## Implementation Details

- Added serialized default, minimum, maximum, and exponential zoom-sensitivity settings.
- Initializes the read-only `CurrentRadarRange` runtime diagnostic from the configured default in `Awake`.
- Reads `Mouse.current.scroll` without consuming or changing cockpit-look input.
- Positive scroll decreases range; negative scroll increases it; all values clamp to the configured bounds.
- Keeps `WorldPositionToRadar` and `WorldOffsetToRadar` as the shared north-up conversion paths, now using `CurrentRadarRange`.
- Keeps Player centred and leaves all physical RectTransforms, background/mask images, sweep, marker sizes, and contact icon sizes unchanged.
- Uses the shared current-range test for normal contacts and artillery predictions.
- Removes the artillery assist's independent radial scale and edge-clamp behavior so it uses the exact common mapping and hides out of range.
- Raises `RadarRangeChanged` only when the clamped range actually changes.
- `KartikarisAcousticLocalization` listens for that event and asks each TDOA graphic to re-render from retained historical Player/probe positions and `DeltaDistance`.
- TDOA validity remains a property of the acoustic measurement rather than whether the current zoom happens to leave two visible samples, allowing a clipped curve to return after zooming out.
- Zoom reprojection does not reset event lifetime or recompute arrival time, distance difference, receiver baseline, or receiver positions.

## Decisions and Rationale

Exponential scaling provides a similar proportional change at both ends of a large `250–3000 m` span. The provisional `0.002` sensitivity corresponds to approximately a 21% range reduction for a common `+120` wheel notch.

TDOA rebuild ownership remains with acoustic localization rather than the graphic updating every frame. This keeps measurement data authoritative and makes range changes one of the small set of explicit mesh-dirty events.

The bearing-only sound-origin wedge is unaffected because it communicates direction rather than a world-distance position.

## Validation Performed

- Built `Assembly-CSharp.csproj` using `dotnet build --no-restore`.
- Result: zero errors and one pre-existing unrelated warning from `Assets/Mirza Beig/Cinematic Explosions FREE/Scripts/DemoController.cs` about hiding `Component.camera`.
- Statically traced all production uses of `RadarRange`, `WorldPositionToRadar`, `WorldOffsetToRadar`, and `EffectiveRadarRadius`.
- Confirmed contacts, drone-spotted target contacts, artillery assistance, and TDOA projection use the shared controller mapping.
- Confirmed Sound Probes do not yet create radar contacts; any future probe `RadarContact` will automatically use the same contact mapping.
- Confirmed the saved scene's existing range value is `3000` and no scene asset was edited.
- Confirmed TDOA range-change handling does not write event ID, historical receiver positions, arrival times, `DeltaTime`, `DeltaDistance`, receiver baseline, or visualization start time.

Unity Editor and Play Mode were not started.

## Known Issues / Limitations

- Mouse-wheel feel depends on platform scroll units and requires an interactive tuning pass; `zoomSensitivity` is serialized for this purpose.
- There is no textual range readout on the radar; the current value is available as a runtime component diagnostic/property.
- Sound Probe blue-dot radar contacts remain a separate, not-yet-implemented presentation feature.

## Manual Follow-up

1. Enter Play Mode and confirm `Current Radar Range` initializes to `3000`.
2. Test both wheel directions and both `250/3000 m` clamps while cockpit look remains normal.
3. Compare a fixed contact and yellow prediction at `3000`, `1500`, and approximately `1000 m`.
4. Confirm out-of-range views hide and reappear without changing icon sizes.
5. Create one/two-probe TDOA curves, record their diagnostics, zoom in/out, and confirm only projected points/sample count change.
6. Move Player after the acoustic event and confirm the historical curves still do not follow current Player position.

## Suggested Next Step

After the Play Mode feel pass, tune `zoomSensitivity` if the platform reports unusually small or large scroll deltas, then add an optional range label only if the cockpit needs stronger zoom feedback.
