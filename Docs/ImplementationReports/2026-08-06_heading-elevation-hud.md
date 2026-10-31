# Heading Compass and Elevation Tape HUD

Date: 2026-08-06  
Project: `/home/shiki/Documents/github/Kartikaris`  
Unity: `6000.3.9f1`

## Outcome

Implemented the first functional heading compass and elevation tape on the existing cockpit VisionHUD. The implementation adds one production component, `Assets/Scripts/UI/KartikarisHeadingElevationHUD.cs`, attaches it to the existing `Canvas`, binds the physical `Player` hull as heading source, reuses the existing notch and TextMeshProUGUI objects, and clips both tapes with `RectMask2D`.

No second VisionHUD Canvas, camera, input binding, firing control, cannon motion, projectile, targeting, or ballistics system was added.

## Donor Audit

The requested Ritual Division donor was found at `/home/shiki/Documents/github/Ritual Division` and inspected read-only.

Relevant source:

- `Assets/Scripts/UI/Navigation/CompassUI.cs`
- `Assets/Scenes/GameplayScene.unity`

The donor established a useful directional convention: compare world angles to the current yaw and move fixed world headings left as yaw increases. Kartikaris retained that direction and the wrap-safe `Mathf.DeltaAngle` idea, but did not port the donor's full 360-degree instantiated strip, cardinal labels, markers, camera dependency, or unrelated navigation presentation. Ritual Division has no matching finite elevation tape.

Post-work donor SHA-256 values:

```text
91b2e4ecea2e83b37dabe6ece4c690a885b49229ba06b438953af122bada88a1  CompassUI.cs
de3e9a82fdacbbfc73d5e8640ac8a3d44a18361acd18d3763050041dc074c479  GameplayScene.unity
```

No donor file was copied or modified.

## Existing Hierarchy and Presentation Reuse

The actual saved VisionHUD hierarchy was preserved:

```text
Canvas
├── Heading
│   └── Heading Notch
├── Elevation
│   └── Elevation Notch
├── Elevation text
└── Heading text
```

`Heading` retains its horizontal stretch/top layout and `2 × 16` notch graphic. `Elevation` retains its right-side vertical layout and `16 × 2` notch graphic. Both existing text objects remain `TextMeshProUGUI`. The disabled background Images and existing anchors/positions were not repurposed. A `RectMask2D` was added to each tape container because neither previously clipped escaping bars.

The production controller is on the existing layer-9 `Canvas`. That Canvas remains Screen Space Camera with `CockpitCamera` assigned at plane distance 1. `FrontFeedCamera` still excludes layer 9. The separate pre-existing World Space `RadarCanvas` remains intact; it is not a duplicate VisionHUD.

## Heading Convention and Source

The serialized `headingSource` is the physical `Player` transform:

| World hull forward | Heading |
|---|---:|
| `+Z` | `000.0°` |
| `+X` | `090.0°` |
| `-Z` | `180.0°` |
| `-X` | `270.0°` |

Heading normalizes into `[0, 360)`. Display formatting uses one decimal place and three integer digits. The component accumulates a continuous heading with `Mathf.DeltaAngle`, so crossing from `359.9°` to `0°` advances through north rather than reversing or rebuilding a strip.

Increasing heading scrolls bars left; decreasing heading scrolls them right. The controller reads neither `LookYaw`, `LookPitch`, `CockpitCamera`, nor `FrontFeedCamera`, so pilot head look does not steer the compass. Updates occur every `LateUpdate` while stationary or moving and are not gated by firing mode or Animator state.

## Elevation Framework

The imported Player model was audited in Unity. It exposes these cannon-named candidates:

```text
Player/Kartikaris Mech/Armature/root/waist/torso/cannon.1
Player/Kartikaris Mech/Armature/root/waist/torso/cannon.1/cannon.2
Player/Kartikaris Mech/cannon.1
Player/Kartikaris Mech/cannon.2
```

Because this does not identify one clearly intended elevation pivot, `elevationSource` is deliberately unassigned. The current prototype uses serialized `manualElevationDegrees` and public `SetElevationDegrees(float)`. Both clamp to the finite `-30°..+90°` display range. Context commands provide `-30`, `0`, `45`, and `90` degree development checks only when no transform source is assigned.

The optional future transform mode supports a selectable local X/Y/Z rotation axis and an angle offset, but it does not move or control that transform. Increasing elevation scrolls bars downward and decreasing elevation scrolls them upward. Notches representing values outside the finite range are disabled at the tape ends.

Elevation text is signed and fixed-width at one decimal place, for example `+45.2°`, ` 0.0°`, and `-30.0°`. Heading and elevation text assignments occur only when the displayed tenth changes.

## Exact Notch Resolution and Pooling

Each notch represents exactly `0.1°`. The current scales are independently tunable and both default to `30 px/degree`, yielding:

```text
notch spacing = 30 px/degree × 0.1 degree = 3 px
one degree = exactly 10 notch intervals = 30 px
```

At startup, pool sizes are derived from each container's current RectTransform extent plus four buffered notches on both sides. Only visible-strip coverage and buffer are created. There is no global 3,600-object heading strip or 1,200-object elevation strip.

During normal angle changes, the component moves one pooled RectTransform root for each tape. It does not instantiate, destroy, use LINQ, rebuild hierarchy, or rewrite every notch each frame. Elevation end visibility updates only when crossing a `0.1°` tick. A real Play Mode run at the batch game-view dimensions created 267 heading notches and 261 elevation notches; the counts remained unchanged across repeated heading/elevation sweeps.

## Scene Hookup

The saved controller references are:

- heading source: `Player`
- heading container/template/text: `Heading`, `Heading Notch`, `Heading text`
- elevation container/template/text: `Elevation`, `Elevation Notch`, `Elevation text`
- elevation source: unassigned/manual mode
- heading/elevation scale: `30 px/degree`
- pool buffer: four notches per side
- elevation range: `-30°..+90°`

## Validation

All Unity runs used only Unity `6000.3.9f1`. Before each invocation and after each exit, process checks confirmed that no Unity Editor, UnityShaderCompiler, or UnityLicensingClient process was still active. No Unity-generated directory was deleted or cleaned.

The saved-scene and runtime-math validation confirmed:

- successful C# compilation and scene serialization;
- exactly one VisionHUD `Canvas` and one HUD controller, while preserving `RadarCanvas`;
- Screen Space Camera ownership by `CockpitCamera`, layer 9, and FrontFeed exclusion;
- all seven required source/UI references and manual elevation mode;
- both `RectMask2D` components and preserved template graphic types/sizes;
- zero missing scripts;
- all four cardinal heading values and fixed-width text;
- heading scroll direction in both directions;
- heading independence from cockpit `LookYaw`;
- continuous accumulated motion across `359.99° → 0°`;
- positive, zero, and negative elevation formatting;
- exact `-30°` and `+90°` clamps;
- downward tape motion for increasing elevation;
- hidden out-of-range notches at the finite ends;
- exact 3 px `0.1°` spacing and ten intervals per degree;
- stable pool counts through angle-only updates.

Result:

```text
KARTIKARIS_HEADING_ELEVATION_SETUP_OK
```

A separate live Play Mode smoke run let the production `Awake`, `Start`, and `LateUpdate` lifecycle create and update both pools, then swept the Player hull and manual elevation repeatedly. Pool roots existed and neither pool grew.

```text
KARTIKARIS_HEADING_ELEVATION_PLAYMODE_OK headingPool=267 elevationPool=261 frame=3
```

The temporary editor setup/validation utility and its metadata were removed after use. Headless Play Mode emitted the project's existing `Nature/Soft Occlusion` tree warnings and `-nographics` render-pass warnings; there were no HUD exceptions, compiler errors, missing references, or failed validation checks.

## Manual Tuning and Follow-up

1. In Play Mode, turn the physical hull through all four cardinal directions and through north in both directions.
2. Hold the hull still while looking around the cockpit to confirm heading remains hull-relative and visually readable.
3. Use the component's four manual elevation context commands to inspect signed text, downward/upward scroll direction, and both finite tape ends.
4. Tune `headingPixelsPerDegree` and `elevationPixelsPerDegree` if the bars are too dense or sparse at the intended resolution. The `0.1°` semantic notch resolution remains fixed.
5. Adjust only the existing text/tape RectTransforms if cockpit readability requires it.
6. Assign `elevationSource`, its local axis, and offset only after one transform is verified to be the real weapon-elevation pivot.

## Files Changed

- `Assets/Scripts/UI/KartikarisHeadingElevationHUD.cs`
- `Assets/Scripts/UI/KartikarisHeadingElevationHUD.cs.meta`
- `Assets/Scenes/Kartikaris_MapTest.unity`
- `Docs/PROJECT_STATE.md`
- `Docs/ImplementationReports/2026-08-06_heading-elevation-hud.md`

Repository git metadata remains unavailable in this workspace, so final review uses a filesystem and serialized-scene audit rather than `git status`/`git diff`.
