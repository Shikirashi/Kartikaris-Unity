# Artillery Computer First Functional Pass

## Summary

Implemented the first functional right-side cockpit Artillery Computer pass: reusable camera-gaze interaction, four outlined physical buttons, six-position runtime Player shell selection, 0–3 propellant charge state, zero-charge firing rejection, presentation-only 3D TMP monitor, and explicit future loading-animation hooks. Enemy retains the fixed loadout path. The synchronized loading animation itself remains intentionally deferred.

## Files Created

- `Assets/Scripts/Cockpit/KartikarisCockpitInteractionController.cs`
- `Assets/Scripts/Cockpit/KartikarisCockpitInteractable.cs`
- `Assets/Scripts/Cockpit/KartikarisCockpitOutline.cs`
- `Assets/Scripts/Cockpit/KartikarisArtilleryComputer.cs`
- `Assets/Scripts/Cockpit/KartikarisArtilleryComputerButton.cs`
- `Assets/Shaders/KartikarisCockpitOutline.shader`
- `Assets/Materials/KartikarisCockpitOutline.mat`
- `Assets/Editor/KartikarisArtilleryComputerSetup.cs`
- Corresponding Unity folder/file `.meta` files.
- This report.

## Files Modified

- `Assets/Scripts/Combat/KartikarisShellLoadout.cs`
- `Assets/Scripts/Combat/KartikarisCannonWeapon.cs`
- `Assets/Scripts/Player/MechFirstPersonController.cs`
- `Assets/Scenes/Kartikaris_MapTest.unity`
- `Docs/PROJECT_STATE.md`

The imported cockpit FBX, imported materials, packages, and project settings were not intentionally modified. Unity dynamically populated its stock TMP fallback while testing unsupported Unicode square glyphs; the monitor was changed to reliable ASCII glyphs and the fallback asset was restored byte-for-byte to its pre-task hash.

## Scene / Prefab / Inspector Changes

- Enabled runtime selection only on the Player `KartikarisShellLoadout`; Enemy remains fixed.
- Configured Player slots as:
  1. `Super Explosive`, `SPEX`
  2. unassigned, `ABST`
  3. unassigned, `HOMG`
  4. unassigned, `GUID`
  5. unassigned, `----`
  6. unassigned, `----`
- Set Player initial charge count to `1`.
- Added `KartikarisArtilleryComputer` to `cockpit/right panel` and wired Player loadout, weapon, predictor, controller, monitor, `charge.1`–`charge.3` renderers, and all requested future bones.
- Added `ArtilleryComputerMonitor` as 3D TextMeshPro under `cockpit/right panel`. The setup derives its position and size from the current-pose submesh using the exact `monitor` material and offsets it `0.006 m` toward `CockpitCamera`.
- Added trigger `BoxCollider`, interactable, outline, and reusable button-action components to `shell prev`, `shell next`, `charge less`, and `charge more`; each action has its exact dotted Armature button bone assigned.
- Put the four visible button GameObjects on layer 8 (`CockpitInteractable`). Existing Cockpit Rendering Layer mask `256` remains on their renderers.
- Added the gaze controller to `CockpitCamera`, assigned the camera and layer-8 mask, and serialized `10 m` reach for this oversized 100× staged import. The reusable component's code default remains `2 m`.
- Created no new scene root and changed no prefab/source FBX.

## Implementation Details

`KartikarisShellLoadout` preserves `SelectedShell` and the fixed startup-lock path. Runtime mode owns exactly six optional slots; cycling skips null definitions, wraps, and publishes previous/new physical indices with Previous/Next direction. `KartikarisCannonWeapon` continues querying the current definition at fire time, while `KartikarisArtilleryProjectile.Launch` already snapshots the selected definition for the launched projectile.

Charges are clamped to `0..3`. The three visible renderers are enabled by index, bottom-to-top. Values 1–3 currently pass muzzle velocity through unchanged via `ApplyChargeMuzzleVelocity`; this is the intended future charge-ballistics seam. At zero, `TryAuthorizeFire` rejects before the weapon's cooldown check and all projectile/VFX/audio effects and raises one warning event per attempted call. Since `MechFirstPersonController` starts post-fire vulnerability only when `TryFire` succeeds, rejected attempts do not start that timer.

The monitor formats shell code, charge cells, predictor-derived horizontal range, predictor real-time flight duration, and ten controller-timer mode cells. Standard HE/Airburst use the existing predictor. Homing, Guided Missile, zero charge, and no-solution cases use unavailable values. `StringBuilder`, a 20 Hz interval, event dirtiness, and last-string caching avoid unnecessary TMP writes. The default project font is proportional Liberation Sans rather than monospace, so reliable `#`, `.`, and spaces are used as configurable fallback glyphs.

The outline is a Kartikaris-owned URP inverted-hull shader. Each focused button creates at most one renderer-only proxy, reuses one shared material, applies color/thickness through `MaterialPropertyBlock`, disables shadows/probes, and toggles only that proxy. It does not replace imported materials or allocate a material per frame.

## Decisions and Rationale

- Only `Super Explosive.asset` exists. No Airburst/Homing/Guided ScriptableObjects or future gameplay types were fabricated; null slots remain legal and automatically become selectable when real definitions are assigned.
- Gameplay shell/charge state remains in `KartikarisShellLoadout`; the monitor is only a view and other shell consumers do not depend on the Artillery Computer.
- Button presses expose events and optional Animator triggers but do not move any imported bone. This avoids conflicting with the existing Armature before the complete loading sequence is authored.
- The scene uses `10 m` rather than the `2 m` reusable default because diagnostic bounds place the current imported buttons about `7.5 m` from the origin cockpit camera.
- ASCII indicators avoid editing/populating the stock dynamic TMP fallback. A project-owned monospace TMP font can replace Liberation Sans later.

## Validation Performed

- Unity `6000.3.9f1` batchmode imported and compiled all changes successfully.
- Final setup validation reported `failures=0`, `roots=9`, and `missingScripts=0`.
- Automated scene checks confirmed all required references/components, Player runtime mode, exactly six slots, initial charge 1, Enemy fixed mode, the layer-8 interaction mask, and a usable scene interaction reach.
- Physics validation aimed rays from `CockpitCamera` at each button collider and hit the intended `shell prev`, `shell next`, `charge less`, and `charge more` collider.
- Transient runtime-state validation confirmed null-slot skipping, next/previous wrapping, event physical-index/direction payloads, one zero-charge signal per attempt, unchanged 1–3 charge velocity, fixed-loadout isolation, and four-character code padding/truncation.
- Checksum audit accounted for all created/modified files and confirmed the imported FBX/model source, vendor assets after TMP restoration, packages, and project settings did not change.
- A batch camera-render diagnostic returned only the camera clear color, so it was not treated as visual validation.
- No interactive Play Mode test was run.

## Known Issues / Limitations

- Only physical slot 1 is currently usable because it is the only real shell-definition asset.
- Liberation Sans is proportional; final monospace alignment and screen readability need an interactive visual pass.
- White outline thickness, 10 m gaze reach, and button collider feel need Play Mode tuning in the staged cockpit.
- The monitor uses a static landing solution only for Standard HE/Airburst, as intended.
- No warning text/sound exists for zero charge; only the event hook is implemented.
- The full coverpanel, ramrod, charges, selected-shell, and revolver sequence is not implemented.

## Manual Follow-up

1. Create/tune real Airburst, Homing, and Guided Missile definitions before assigning slots 2–4.
2. In Play Mode, tune monitor transform/font, outline thickness, colliders, and the 10 m interaction reach if needed.
3. Verify repeated zero-charge LMB attempts produce no shot side effects, then confirm normal firing at charges 1–3.

## Suggested Next Step

Complete the short interactive Artillery Computer acceptance pass before authoring the deferred synchronized loading animation.
