# Procedural Radar Grid Revision

## Summary

Added a reusable procedural UGUI radar face to replace the static `radar grid.png` background. The new code draws a black circular face, crisp green boundary, `500 m` concentric rings, matching circle-clipped Cartesian subdivisions, cardinal axes, optional radial subdivisions, and a centre point. It shares the radar controller's live zoom mapping and never generates numeric distance labels.

The existing sweep remains image-based. Heading labels are also expected to remain an image overlay, but the current scene's static background combines those headings with the obsolete grid and distance numbers; separating that overlay is therefore a manual Unity/art-layout step under the code-only constraint.

## Files Created

- `Assets/Scripts/Radar/KartikarisRadarGridGraphic.cs`
- `Assets/Scripts/Radar/KartikarisRadarGridGraphic.cs.meta`
- `Docs/ImplementationReports/2026-08-07_procedural-radar-grid.md`

## Files Modified

- `Docs/PROJECT_STATE.md`

## Scene / Prefab / Inspector Changes

None. No scene, prefab, hierarchy, image, material, or serialized reference was changed.

The saved scene still has an active `RadarRoot/RadarBackground` Image using `Assets/Images/radar grid.png`. Manual setup is required before the procedural result appears.

## Implementation Details

- `KartikarisRadarGridGraphic` derives from `MaskableGraphic` and renders directly through `OnPopulateMesh`.
- A configurable opaque black filled circle replaces the background portion of the composite texture.
- Ring count derives from `floor(CurrentRadarRange / 500 m)` plus the current-range outer boundary.
- At exact multiples, the boundary doubles as the final distance ring, avoiding coincident geometry: `3000 m` produces exactly six visible rings and `1500 m` produces three.
- Cartesian lines are emitted at `±500 m`, `±1000 m`, and so on, with endpoints analytically clipped to the circular boundary.
- Four default radial subdivisions form the north/south/east/west cross axes; higher serialized counts optionally add spoke subdivisions.
- Ring, grid, axis, boundary, and centre-point thickness are presentation-space values and do not scale with zoom.
- The component consumes `CurrentRadarRange` and `EffectiveRadarRadius` from `KartikarisRadarController`, matching all contact/assist/TDOA projection semantics.
- A `RadarRangeChanged` subscription calls only `SetVerticesDirty`; RectTransform dimension and style validation do the same.
- No `Update`/`LateUpdate` exists, no hierarchy is generated, and no managed collection is allocated during normal frames.
- Geometry is intrinsically bounded to the circular face; no dedicated circular-mask reference is required for the grid itself.
- Runtime diagnostics expose rendered range/radius, ring count, grid-line count, and total geometry rebuilds.

## Decisions and Rationale

UGUI mesh generation is the smallest robust fit for the existing World Space Canvas. It keeps lines crisp, avoids texture magnification, participates in normal Canvas batching/masking, and preserves the existing sweep, contacts, assists, TDOA, and sound overlays.

Cartesian `500 m` subdivisions were selected because they most closely match the existing PNG, while the concentric rings retain radar-readable radial distance. Optional spoke count permits a more radial style without changing code.

The grid listens to the existing radar range event instead of polling every frame, matching the established TDOA zoom lifecycle.

## Validation Performed

- Inspected the existing `2048×2048` static grid and sweep PNGs and the saved `RadarRoot` hierarchy.
- Confirmed the old background combines an opaque black face, green rings/grid, `0/90/180/270` headings, and obsolete `1/2/3 km` guides; the sweep is already a separate Image.
- Temporarily included the new source file in Unity's generated `Assembly-CSharp.csproj`, built the full production assembly, and removed the temporary project-file entry afterward.
- Build result: zero errors and one unrelated existing warning from Mirza Beig's `DemoController.camera` hiding `Component.camera`.
- Statically verified `3000 / 500 = 6` rings, `1500 / 500 = 3` rings, and ring pixel radius `worldDistance / CurrentRadarRange × EffectiveRadarRadius`.
- Confirmed source contains no per-frame method and subscribes only to actual radar range changes.
- Confirmed no scene/prefab/Inspector/art asset was edited.

Unity Editor and Play Mode were not started, so rendered appearance, sibling order, mask interaction, and wheel feel remain manual checks.

## Known Issues / Limitations

- The new component is not wired into the saved scene by design.
- The existing static grid texture bakes cardinal headings and numeric distance guides into one image. Disabling it removes both, so a separate labels-only image overlay must be retained or prepared manually.
- The procedural layer does not create, position, or author heading labels; it intentionally renders no text.
- Circle smoothness defaults to 128 segments and can be tuned up to 512 at additional vertex cost.
- Very large custom ranges combined with unusually small grid spacing are bounded to 128 generated distance steps to keep UI vertex counts finite.

## Manual Follow-up

1. Under `RadarRoot`, create a full-stretch UI object named `ProceduralGrid` with centred pivot and zero offsets.
2. Add `KartikarisRadarGridGraphic` and assign `RadarRoot`'s `KartikarisRadarController`; parent lookup also resolves it automatically.
3. Put `ProceduralGrid` behind Sweep, heading labels, assists, TDOA, Contacts, PlayerCenter, and SoundOriginHint.
4. Disable `RadarRoot/RadarBackground`; do not delete the imported PNG unless you deliberately want to remove donor provenance.
5. Retain or make a separate image-only `0/90/180/270` overlay without the old `1/2/3 km` guides. The current composite cannot keep one set while hiding the other.
6. Keep the existing Sweep Image and RadarController Sweep reference unchanged.
7. Keep RadarController defaults at `3000 m`, `250 m`, `3000 m`, and tune zoom sensitivity there.
8. Keep Grid Spacing Meters at `500`; tune face/grid colors and regular/axis/boundary thickness on the new component.
9. Preserve existing clipping/mask hierarchy for other overlays; the procedural grid requires no extra mask reference.

## Suggested Next Step

Complete the manual hybrid-UI setup, then compare the procedural grid against the old image at `3000 m` before testing zoom, contacts, artillery assistance, and one/two-probe TDOA curves together.
