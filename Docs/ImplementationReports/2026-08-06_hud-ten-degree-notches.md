# HUD Ten-Degree Notches

## Summary

Changed heading and elevation tape notches from `0.1°` to `10°` per notch. Text readouts remain accurate to `0.1°`; only the visual tape interval changed.

This was a code-only revision. No Unity scene, prefab, hierarchy, Inspector value, serialized reference, tag, layer, or component wiring was modified.

## Files Created

- `Docs/ImplementationReports/2026-08-06_hud-ten-degree-notches.md`

## Files Modified

- `Assets/Scripts/UI/KartikarisHeadingElevationHUD.cs`
- `Docs/PROJECT_STATE.md`

## Scene / Prefab / Inspector Changes

None.

The code defaults both heading and elevation notch spacing to `40 px`. During final audit, a concurrent/external scene save was observed with both fields serialized as `30 px`; Codex did not issue that scene write. Users can adjust the two fields independently on the existing component when desired.

## Implementation Details

Tape resolution and text precision are now separate constants:

```text
notch resolution = 10°
text precision = 0.1°
```

The movement scale remains derived from the adjustable spacing:

```text
pixelsPerDegree = notchSpacingPixels / 10°
```

At the new `40 px` code defaults:

```text
pixelsPerDegree = 40 / 10 = 4 px/degree
10° movement = 40 px = one notch interval
360° heading = 36 intervals = 1,440 px
-30°..+90° elevation = 12 intervals = 480 px
```

Heading phase now divides continuous wrap-aware heading by ten degrees. Elevation pooling divides the effective elevation by ten degrees and enables only represented ticks from `-30°` through `+90°`. Numeric text still rounds independently to tenths, so values such as `045.2°` and `+45.2°` remain possible between notch crossings.

## Decisions and Rationale

- Used `10°` as a fixed semantic tape interval exactly as requested.
- Separated text-tenth math from notch math to avoid degrading numeric readout precision.
- Increased code defaults to `40 px` because 6 px at ten degrees would compress a full heading circle into only 216 px and visually repeat it across the wide heading mask.
- Kept independent heading/elevation spacing controls and automatic movement-scale derivation.
- Preserved pooling, wrap continuity, direction, finite elevation limits, and per-frame allocation behavior.

## Validation Performed

The Unity-generated `Assembly-CSharp.csproj` build completed with zero errors and warnings after the ten-degree math change.

Static checks confirmed:

- heading and elevation share the `10°` semantic interval;
- text formatting still uses ten text ticks per degree;
- heading wrap uses `heading / 10°` rather than text-tenth scaling;
- elevation range ticks are `-3..9`, representing `-30°..+90°`;
- `40 px / 10° = 4 px/degree`;
- initial scene SHA-256 was recorded as `97a99d141b4674e0ed36acf15ccb0b21b1ba9312a3cfcbd3c9930e72ed1bbc59` before the later external save.

Unity Editor/Play Mode was not launched and the existing ownerless zero-byte Unity lock was left untouched. The scene initially had SHA-256 `97a99d141b4674e0ed36acf15ccb0b21b1ba9312a3cfcbd3c9930e72ed1bbc59`. A final audit later observed an external save at `2026-08-06 19:50:19 +0700`, with SHA-256 `e99584f7294c6a9b654a3d799ce71df04452216b751d50014dad8b01d592290a`; that save serialized both spacing fields as `30`. No Codex command edited or saved the scene.

## Known Issues / Limitations

- Ten-degree bars are intentionally coarse; no separate minor ticks or major/minor styling exists.
- Final 40 px spacing still needs subjective inspection at the target resolution.
- Obsolete serialized field names may remain in scene YAML until the user next saves the scene; current code ignores them.

## Manual Follow-up

1. Open the existing scene normally and select `KartikarisHeadingElevationHUD` on the VisionHUD Canvas.
2. The current scene is set to `30 px`; use that as the starting point or try the `40 px` code default.
3. Rotate the hull through `0°`, `10°`, `20°`, and `30°`; confirm one bar interval passes per ten degrees while text changes continuously by tenths.
4. Check manual elevation values `-30°`, `0°`, `45°`, and `90°`; confirm the finite tape contains ten-degree intervals and the text remains decimal.
5. Adjust heading/elevation spacing independently if cockpit composition requires it.

## Suggested Next Step

If ten-degree-only bars are too sparse, add visual-only minor ticks between them while retaining ten-degree major ticks and labels as the semantic navigation scale.
