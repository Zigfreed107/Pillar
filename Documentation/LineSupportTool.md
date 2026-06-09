# Line Support Tool

## Purpose

The Line Support tool creates a generated support group from a user-picked polyline on an imported model. It follows the same CAD feature pattern as the Ring Support tool: the saved line definition is the source of truth, and the individual `SupportEntity` objects are regenerated output.

## GUI Workflow

1. Select one imported model in the viewport or Layer Panel.
2. Open the Supports tab in the Mode Panel and choose Line Support.
3. The Line Support Options panel opens on the right with Spacing, Place supports at bends, Delete, Apply, and Close controls. The Support Preset panel is shown underneath it.
4. Left-click the selected model to place the first line point.
5. Move the mouse over the model to preview the next segment. A transparent blue sphere and an XY guide circle show the current spacing diameter at the cursor.
6. Left-click more model points to extend the polyline.
7. Press Esc after the final left-clicked point to finish drawing the polyline and enter preview editing.
8. Transparent blue sphere handles appear at the clicked polyline points. Drag a handle to move that point on the selected model before applying.
9. Change Spacing or Place supports at bends to rebuild the preview markers.
10. Click Apply to create or update the generated support layer group.
11. Click Close to leave Line Support edit mode, clear previews, restore normal support opacity, and return Manual Support mode to operation selection.

## UX Behavior

- Points must be picked on the selected model. This keeps the Line Support definition anchored to a real model layer.
- The preview line follows the actual picked 3D surface points; it is not flattened onto a horizontal plane.
- Esc does not cancel Line Support drawing and does not create committed supports. It finishes the line using the last left-clicked point, shows editable point handles, and keeps the result as preview state until Apply.
- Apply also finishes an unfinished line before applying supports.
- Spacing is a maximum distance between generated guide points.
- When Place supports at bends is enabled, every clicked polyline point is emitted as a support location. Each segment is divided into enough equal intervals between its two clicked vertices that no interval is longer than Spacing.
- Shared polyline vertices are emitted once, so support locations do not stack at segment joins.
- When Place supports at bends is disabled, the full polyline length is divided continuously. The first and final clicked points remain support locations, but interior bends only receive supports if the continuous spacing happens to land there.
- After Apply, the tool enters edit mode. The generated support group is shown at reduced opacity, individual supports can be selected in the viewport, and selected supports can be deleted with the Delete button or DEL key.
- Preview editing and edit mode both show transparent blue sphere handles at each clicked polyline point. Dragging a handle moves that point to a new hit position on the selected model.
- Dragged point handles update the preview polyline immediately. The generated support entities are replaced only when Apply is clicked.
- Deleting generated supports does not change the saved line settings. Clicking Apply again regenerates the group from the stored polyline and spacing.

## Code Architecture

- `LineSupportSettings` stores the persistent polyline points, spacing, and bend placement behavior in `Pillar.Core.Layers`.
- `SupportLayerGroup` stores optional Line Support metadata and marks the group as `SupportGroupGeneratorKind.LineSupport`.
- `LineSupportPattern` converts polyline settings into renderer-agnostic guide points.
- `LineSupportOperation` owns viewport interaction, transient preview state, Apply, Close/cancel behavior, edit-mode selection, and generated support deletion workflow.
- `LineSupportPreviewRenderer` owns reusable Helix preview geometry for the polyline, projected support markers, and spacing guide.
- `UpdateLineSupportGroupCommand` replaces generated supports and line settings as one undoable edit.
- `SupportGroupTransformRegenerator` transforms saved line points through model-local space and regenerates concrete supports when the owning model transform changes.
- `GphDocumentSerializer` persists line generator settings in `.gph` files using the `lineSupport` generator kind.

## CAD Notes

The tool deliberately keeps rendering, UI controls, generator metadata, and support mesh output separate. That is the same reason real CAD systems store a feature definition and regenerate output geometry from it: it keeps editing, undo/redo, save/load, and transform updates predictable without reverse-engineering intent from generated meshes.

The most important pitfall is treating generated supports as the source of truth. They are not. They are disposable output from the saved line feature plus the current support preset/profile.
