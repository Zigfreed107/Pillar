# Line Support Tool

This document describes the intended user workflow and code architecture for the Line Support tool.

## Purpose

The Line Support tool creates a generated support group from a user-picked polyline on an imported model. It follows the same CAD feature pattern as Ring Support: the saved line definition is the source of truth, and the individual `SupportEntity` objects are regenerated output.

## User Workflow

1. Select one imported model in the viewport or Layer Panel.
2. Open the Supports tab in the Mode Panel and choose Line Support.
3. The tool options panel opens with spacing, bend-placement, and surface-targeting settings.
4. Left-click the selected model to place the first line point.
5. Left-click more model points to extend the polyline while the viewport shows preview geometry.
6. Finish the line and enter preview editing.
7. Adjust spacing, bend behavior, or clicked points as needed.
8. Click Apply to create or update the generated support layer group.
9. Click Close or cancel to leave the tool and clear preview state.

## Behaviour Notes

- Points must be picked on the selected model.
- The preview line follows the actual picked 3D surface points rather than flattening them to a construction plane.
- Spacing is a maximum distance between generated guide points.
- Bend placement can either force supports at clicked vertices or distribute supports continuously across the whole polyline.
- Surface targeting can choose the lowest reachable surface or the surface nearest to each sampled 3D line point.
- Nearest-to-line targeting chooses user intent before support validation. If that exact target cannot be reached from the build plate without intersecting the model, the support is skipped rather than moved to another surface.
- Selected Faces Only targeting tests only the accepted faces belonging to the Line Support model. Its Select faces button launches the reusable face-selection helper, while the toolbar's last accepted face set is used when the Line Support operation has no local face set.
- Applying Selected Faces Only targeting without any selected faces is rejected with a prompt to select faces and try again.
- Deleting generated supports in edit mode should not silently change the saved line feature definition.

## Architecture

- `LineSupportSettings` stores the persistent polyline points, spacing, bend-placement behavior, and surface-targeting policy.
- `SupportLayerGroup` stores optional line support metadata and identifies the generator kind.
- `LineSupportPattern` converts settings into renderer-agnostic guide points.
- `LineSupportOperation` owns viewport interaction, transient preview state, and Apply or Close behavior.
- `LineSupportPreviewRenderer` owns reusable preview geometry.
- `UpdateLineSupportGroupCommand` replaces generated supports and settings as one undoable edit.
- transform regeneration should rebuild supports from the saved line definition rather than moving generated meshes directly

## CAD Rule

Generated supports are disposable output. The saved line feature plus the current support preset or profile are the durable source of truth.
