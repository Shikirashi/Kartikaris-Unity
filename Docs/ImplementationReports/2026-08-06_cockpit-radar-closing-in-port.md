# Cockpit Radar — Closing IN Port

## Summary

Implemented the first dedicated cockpit radar in `Assets/Scenes/Kartikaris_MapTest.unity`. The radar is a World Space UI attached to the existing imported left-side `radar panel` mesh, is rendered by `CockpitCamera`, and is excluded from `FrontFeedCamera` and the existing Screen Space Overlay VisionHUD.

The implementation ports only the useful visual language from `/home/shiki/Documents/github/FireForEffect`: the green grid, clockwise sweep, sweep-triggered contact brightening, and short fade. It replaces the donor's artillery-specific data model with a reusable Kartikaris contact framework and a separate sound-bearing reporting hook.

## Donor Inspection and Source Integrity

The donor project was inspected read-only before implementation. Relevant sources were:

- `Assets/Scripts/FireForEffect/RadarController.cs`;
- `Assets/Scenes/artillery.unity`;
- `Assets/Editor/FireForEffectSceneSetup.cs`;
- `Assets/Images/full radar.png`;
- `Assets/Images/radar grid.png`;
- `Assets/Images/radar sweep.png`.

The donor radar is a roughly 1033-pixel square UGUI presentation on a Screen Space Overlay Canvas. Its active presentation uses a stretched `radar grid.png` Image and stretched `radar sweep.png` Image, rotates the sweep clockwise at `0.8` rotations per second, brightens a contact when the sweep crosses its bearing, and fades it over `0.5` seconds. Its root composite `full radar.png` Image is disabled. It has no mask, custom radar shader, radar material, radar prefab, or radar-specific audio dependency.

The donor script's single enemy, artillery heading, projectile, impact, hit-marker, uncertainty, destruction, and map-clamping assumptions were deliberately not ported.

The target already contained `full radar.png`, `radar grid.png`, and `radar sweep.png` with the same Unity GUIDs and byte-for-byte identical PNG/meta hashes as the donor, so no donor file was copied. The existing target `circle.png` sprite was reused for contact and player dots. No sound-origin wedge asset was copied or generated, and its Inspector slot remains empty.

The donor began with user-owned modifications and untracked files, including changes to its editor setup, scenes, narration, artillery scripts, documentation, and generated/build files. A non-generated donor digest excluding `.git`, `Library`, `Temp`, `Logs`, `UserSettings`, `Build`, and `obj` was captured before and after work:

```text
Before: a3ac185f2137c57deacb36b0db7896b71ea8d020aeb909dc9a9f11a222e6aa59
After:  a3ac185f2137c57deacb36b0db7896b71ea8d020aeb909dc9a9f11a222e6aa59
```

The identical digest confirms that no non-generated donor file was changed.

## Files Created

- `Assets/Scripts/Radar.meta`
- `Assets/Scripts/Radar/RadarContact.cs`
- `Assets/Scripts/Radar/RadarContact.cs.meta`
- `Assets/Scripts/Radar/KartikarisRadarController.cs`
- `Assets/Scripts/Radar/KartikarisRadarController.cs.meta`
- `Docs/ImplementationReports/2026-08-06_cockpit-radar-closing-in-port.md`

Unity also generated `Assets/Images.meta` for the target's pre-existing Images folder during import. No radar PNG was added by this task.

## Files Modified

- `Assets/Scenes/Kartikaris_MapTest.unity`
- `Docs/PROJECT_STATE.md`

No prefab, package manifest, ProjectSettings file, render-pipeline asset, TerrainData, tree asset, Animator Controller, or existing runtime script was modified.

## Scene / Inspector Changes

The existing `cockpit/cockpit/radar panel` mesh was suitable and remains intact. The following hierarchy was added beneath it:

```text
radar panel
└── RadarCanvas
    └── RadarRoot
        ├── RadarBackground
        ├── Sweep
        ├── Contacts
        ├── PlayerCenter
        └── SoundOriginHint
```

`RadarCanvas` is World Space, explicitly references `CockpitCamera`, and uses layer 8 (`CockpitInteractable`) recursively. The current `CockpitCamera` culling mask includes layer 8; `FrontFeedCamera` excludes it. The existing VisionHUD remains a separate layer-9 Screen Space Overlay Canvas.

The imported cockpit panel carries inherited prefab scale. Radar placement therefore derives the canvas position and scale from the panel mesh's local bounds rather than using an assumed world/local scale. The resulting canvas centre is approximately `(-9.8, 0, 0)` in world space, flush with the existing panel, and the radar diameter occupies about 82 percent of the panel's usable local Y/Z extent.

Current radar settings are:

- range: `500 m`;
- usable radius: `87%` of the rectangular UI radius;
- sweep speed: `0.8` rotations per second;
- contact fade: `0.5 s`;
- friendly color: blue;
- enemy color: red;
- sound error: `±15°`;
- sound full-visibility duration: `2 s`;
- sound fade duration: `2 s`;
- sound-origin sprite: unassigned.

The existing `Enemy` root now has `RadarContact`, configured as affiliation `Enemy`, category `Mech`, contact active, and spotted false.

## Runtime Architecture

### RadarContact

`RadarContact` declares a position source, affiliation, category, spotted state, and active state. Enabled instances maintain a lightweight static registry, allowing future contacts to appear without editing the radar controller's serialized list.

Friendly contacts are eligible whenever their contact is active. Enemy contacts require both active contact state and `spotted == true`. Public setters and `Radar/Mark Spotted` / `Radar/Clear Spotted` context commands provide stable debug and future gameplay hooks. Categories currently include `Unspecified`, `Mech`, `Camera`, and `Drone`; categories are metadata and do not hardcode detection rules.

### KartikarisRadarController

The radar dynamically creates one non-raycast UGUI Image for each enabled `RadarContact`. It removes the view when the component is disabled, hides ineligible and out-of-range contacts, and does not create permanent fake contacts in the scene.

The map is strictly north-up and player-centred:

- world `+Z` maps to UI up;
- world `+X` maps to UI right;
- the player maps to `(0, 0)`;
- player rotation is never used in coordinate conversion;
- moving the player shifts world contacts relative to the fixed centre;
- contacts beyond the configured range are hidden, not edge-clamped.

The sweep advances with unscaled delta time and uses the donor's clockwise bearing crossing behavior. When the sweep crosses an eligible contact's bearing, that contact becomes fully opaque and fades to zero over the configured duration. Friendly and enemy colors are selected from affiliation rather than donor-specific object identity.

### Sound-origin reporting

`ReportSoundArrival(Vector3 sourceWorldPosition)` is the public integration point for a future sound propagation system. It computes the true world-north bearing, samples a local `System.Random` error within `±15°` exactly once, stores the displayed bearing, and does not touch `UnityEngine.Random` state. Player yaw does not affect the result.

The newest report resets/replaces the prior event. Its optional Image remains fully visible for two seconds, fades for two seconds, and then disables. When the sprite slot is null, reports still compute/store/age safely and the Image stays disabled. `Sound Hint North Rotation Offset` is exposed for compensating the authored up direction of a future wedge sprite.

No drone, spotting, sound propagation, AI, targeting, artillery, projectile, impact, combat, or external audio behavior was implemented.

## Validation Performed

All Unity operations used the canonical editor `/home/shiki/Unity/Hub/Editor/6000.3.9f1/Editor/Unity`.

### Compile and import

- Initial production scripts compiled with no C# errors.
- Scene setup saved successfully through Unity APIs.
- A final clean batch compile after removing temporary validation helpers exited `0` with no compiler errors.

### Automated scene and behavior validation

A temporary editor validator opened the saved scene and passed 40 checks covering:

- reuse of the existing radar-panel mesh;
- World Space Canvas mode and physical mesh placement;
- explicit CockpitCamera reference;
- recursive layer-8 assignment;
- CockpitCamera inclusion and FrontFeedCamera exclusion;
- separation from the exterior feed and VisionHUD;
- complete hierarchy and assigned grid/sweep sprites;
- intentionally null sound-wedge sprite;
- Enemy contact metadata and initial unspotted state;
- enemy invisibility before spotted and visibility after `SetSpotted(true)`;
- friendly eligibility without spotted and blue coloring;
- out-of-range rejection;
- north/east/south/west coordinate mapping;
- fixed centre and player-yaw independence;
- player-relative movement;
- normal and wraparound clockwise sweep crossings;
- cardinal sound bearings and `±15°` limits;
- fixed error throughout one sound event;
- newest-event replacement and timed expiry;
- null-sprite safety;
- zero missing script references.

The validator reported:

```text
KARTIKARIS_RADAR_VALIDATION_PASS checks=40 missingScripts=0
```

### Visual render

A temporary GPU-backed Unity camera render aimed from the cockpit camera position toward the left panel. It confirmed the panel-facing orientation, readable donor grid and sweep, centred green player dot, and corrected physical scale. The first diagnostic render exposed the cockpit prefab's inherited scale; placement was changed to derive from mesh-local bounds, and the corrected render placed the canvas at approximately `(-9.8, 0, 0)` rather than the erroneous `(-101.39, 0, 0)`. The preview artifact was written only to `/tmp`.

### Play Mode smoke test

A temporary Unity Test Framework Play Mode test loaded `Kartikaris_MapTest` and passed one of one tests. It observed:

- a runtime contact Image created from the contact registry;
- the scene enemy hidden while unspotted;
- the enemy view activated after `SetSpotted(true)`;
- a runtime friendly contact active without spotted and colored blue;
- sweep transform movement over real unscaled time;
- a null-safe `ReportSoundArrival` call and active logical event.

The temporary Editor/setup/test scripts, asmdef, folder metadata, and test directories were removed from `Assets` afterward.

### Integrity and workspace status

- The donor's non-generated digest was unchanged.
- No generated sound wedge remains in the target.
- No temporary inspection or test helper remains under `Assets`.
- Target `git status --short --untracked-files=all` is unavailable because `/home/shiki/Documents/github/Kartikaris` has no functional `.git` worktree metadata. The concrete production change inventory is therefore the created/modified file list in this report.

The Unity run continues to emit the pre-existing warnings that ordinary Synty tree prefabs do not use the legacy `Nature/Soft Occlusion` shader. Those warnings are unrelated to the radar and no tree material was changed.

## Known Issues / Limitations

- The scene begins with the enemy unspotted, and no gameplay spotting system changes that state yet. Use `Radar/Mark Spotted` for focused manual testing.
- The sound-origin sprite is intentionally empty, so sound reports have no visible wedge until an asset is assigned.
- No production system currently calls `ReportSoundArrival`.
- The radar is physically mounted on the left wall and is not in the straight-ahead camera view; look left to inspect it.
- Final visual readability, contact size, panel scale, and fade feel should still be judged interactively at the intended display resolution.
- The donor's artillery and audio implementations remain intentionally absent.

## Manual Follow-up

1. Open `Assets/Scenes/Kartikaris_MapTest.unity` in Unity `6000.3.9f1` and enter Play Mode.
2. Look toward the left radar wall and confirm the grid and clockwise sweep are readable from the pilot position.
3. Select `Enemy`, use `Radar/Mark Spotted`, and confirm a red ping appears only when the sweep reaches the enemy's bearing and then fades. Clear spotted and confirm it disappears.
4. Move and rotate the player; confirm the centre marker stays fixed, world north stays up, rotation does not rotate the map, and contact positions change only from relative world displacement.
5. Temporarily set a range smaller than the enemy's distance and confirm the enemy is hidden rather than clamped to the perimeter.
6. Optionally add a friendly `RadarContact` and confirm it is eligible without spotted and rendered blue.
7. When a suitable wedge sprite exists, assign `Sound Origin Hint Sprite`, use the four cardinal sound context commands, and adjust only the authored rotation offset if required.

## Suggested Next Step

Connect future spotting and sound-propagation systems to `RadarContact.SetSpotted` and `KartikarisRadarController.ReportSoundArrival` respectively. Keep those gameplay responsibilities outside the radar presentation component.
