# Area Support Tool

The Area Support tool creates supports over one or more contiguous areas defined by selected model faces, viewed from the Z-up direction. It is intended for broad patches where the user wants regular support coverage without manually drawing every line or contour.

## User workflow

1. Open Manual Support mode and choose **Area Support** from the Supports tab.
2. The Area Support options panel appears above the Support Presets panel.
3. Click **Select faces** to launch the reusable Face Set Selection helper.
4. Select one or more model faces and accept the face selection.
5. The Area Support tool previews:
   - yellow topmost borders around each contiguous selected face island,
   - blue cross markers at proposed support tips,
   - optional transparent blue spacing circles when **Show support spacing** is enabled.
6. Adjust **Spacing** if needed. The preview refreshes after edits settle.
7. Click **Apply** to create or update an editable support layer group named **Area Support**.
8. Click **Close** to exit the Area Support edit session.

## Placement behavior

`AreaSupportPattern` consumes `AreaSupportSettings`, which stores selected `FaceSelectionKey` values plus spacing. It keeps generation renderer-agnostic:

- Selected faces are filtered to the target mesh.
- Shared-edge adjacency splits the selection into contiguous face islands.
- Boundary edges are extracted from selected triangles whose matching edge is not owned by another triangle in the island.
- Boundary supports are sampled roughly one spacing apart, then nudged inward toward the island centroid.
- Internal supports are sampled on a simple hexagonal XY grid.
- Each XY candidate is projected vertically back onto the uppermost selected triangle, and near-duplicates are skipped.
- Concrete `SupportEntity` instances are created through `SupportPlacementPlanner`, so head direction, base position, and branch behavior stay consistent with the other support tools.

The algorithm favors interactive speed and maintainability over optimal support count. It is acceptable for supports to be closer than the requested spacing where that helps cover the selected area.

## Code map

- `src/Pillar.Core/Layers/AreaSupportSettings.cs`: persistent generator settings for selected faces and spacing.
- `src/Pillar.Geometry/Supports/AreaSupportPattern.cs`: face-island extraction, boundary preview data, support sample generation, and vertical projection.
- `src/Pillar.Rendering/Tools/AreaSupportOperation.cs`: tool state, face-selection handoff, preview refresh, Apply/update commands, and edit-mode support selection.
- `src/Pillar.Rendering/Preview/AreaSupportPreviewRenderer.cs`: topmost yellow boundaries, blue markers, and optional spacing circles with reusable line buffers.
- `src/Pillar.Commands/UpdateAreaSupportGroupCommand.cs`: undoable regeneration for editing an existing Area Support group.
- `src/Pillar.UI/Modes/AreaSupportToolOptionsControl.xaml`: WPF options panel.
- `src/Pillar.UI/MainWindow.WorkspaceModes.cs` and `MainWindow.FaceSetSelection.cs`: shell wiring, mode-panel entry, face-selection restore, edit-from-layer behavior, and delete button state.
- `src/Pillar.Core/Persistence/GphDocumentSerializer.cs`: `.gph` save/load support for Area Support generator metadata.
- `src/Pillar.Geometry/Supports/SupportGroupTransformRegenerator.cs`: regeneration after model transforms while keeping selected triangle identities as the parametric anchor.
