# HUD Notch Spacing Controls

## Summary

Added code-only, independently adjustable pixel spacing for heading and elevation notches. The visual interval can now be widened without changing the semantic resolution: every notch still represents exactly `0.1°`, and tape motion is derived from the configured interval.

No Unity scene, prefab, hierarchy, Inspector value, serialized reference, tag, layer, or component wiring was modified.

## Files Created

- `Docs/ImplementationReports/2026-08-06_hud-notch-spacing-controls.md`

## Files Modified

- `Assets/Scripts/UI/KartikarisHeadingElevationHUD.cs`
- `Docs/PROJECT_STATE.md`

## Scene / Prefab / Inspector Changes

None. This was intentionally a code-only change.

On the next Unity script reload, `KartikarisHeadingElevationHUD` exposes these fields under **Tape Scale**:

- `Heading Notch Spacing Pixels` — default `6`
- `Elevation Notch Spacing Pixels` — default `6`

The previous pixels-per-degree fields are replaced by the more direct spacing controls. Existing source/container/template/text references remain untouched.

## Implementation Details

Notch semantics remain constant:

```text
degreesPerNotch = 0.1°
pixelsPerDegree = notchSpacingPixels / degreesPerNotch
```

At the default spacing:

```text
pixelsPerDegree = 6 px / 0.1° = 60 px/degree
movement for 0.1° = 0.1° × 60 px/degree = 6 px
movement for 1.0° = 1.0° × 60 px/degree = 60 px = 10 notch intervals
```

The same configured spacing is used for:

- fixed pooled-notch placement;
- pool-count calculation from the visible container extent;
- fractional heading scroll movement;
- fractional elevation scroll movement.

This keeps the rendered grid and scrolling phase synchronized for any practical spacing value. Public read-only properties expose both interval spacing and the derived pixels-per-degree values.

## Decisions and Rationale

- Exposed separate heading/elevation controls because horizontal and vertical readability may need different tuning.
- Defaulted both to `6 px`; the existing notch graphics are approximately `2 px` thick, leaving a visible gap instead of the previous tightly packed presentation.
- Kept `0.1°` as a code constant so Inspector spacing changes cannot redefine measurement resolution.
- Derived movement scale rather than asking the user to maintain two coupled values manually.
- Preserved pooling, wrapping, finite elevation limits, text formatting, and camera independence.

## Validation Performed

The existing Unity-generated `Assembly-CSharp.csproj` was restored locally and built against the configured Unity assemblies:

```text
Build succeeded.
0 Warning(s)
0 Error(s)
```

Static calculation confirmed:

- default heading/elevation interval: `6 px`;
- derived scale: `60 px/degree`;
- each `0.1°` change: `6 px` motion;
- each `1°` change: exactly ten intervals;
- heading and elevation movement both use their respective derived scale.

The scene hash remained:

```text
97a99d141b4674e0ed36acf15ccb0b21b1ba9312a3cfcbd3c9930e72ed1bbc59  Assets/Scenes/Kartikaris_MapTest.unity
```

Unity Editor/Play Mode was not launched for this code-only revision because a previous process had left an ownerless zero-byte lock and no scene-level validation was required. The lock was left untouched.

## Known Issues / Limitations

- Final spacing is subjective and should be checked at the intended game resolution.
- Extremely small spacing values create larger pools because more notches are required to cover the masked area.
- Existing scene YAML may retain obsolete serialized field names until the user next saves it in Unity; the code ignores those fields.

## Manual Follow-up

1. Open `Kartikaris_MapTest` normally in Unity.
2. Select the existing VisionHUD `Canvas` with `KartikarisHeadingElevationHUD`.
3. Under **Tape Scale**, start with `6` for both spacing fields.
4. Enter Play Mode and turn the hull slowly; confirm each heading notch has a visible gap and every one-degree change crosses ten notches.
5. Use the manual elevation context commands or `SetElevationDegrees` and confirm vertical spacing/motion stays synchronized.
6. Adjust heading and elevation independently if the cockpit layout needs different densities.

## Suggested Next Step

Perform the visual spacing pass in Play Mode at the target display resolution. No further code is needed unless major/minor tick styling or numerical tick labels are requested.
