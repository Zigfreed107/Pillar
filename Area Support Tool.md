# Area Support Tool

The Area Support tool creates supports over one or more contiguous areas defined by selected model faces when viewed from the Z-up direction. It is intended for broad regions where regular support coverage is faster than placing point, line, ring, or contour supports manually.

## User Workflow

1. The user selects **Area Support** in the Supports tab of the Mode Panel.
2. The Area Support options panel opens above the Support Presets panel.
3. The user clicks **Select faces**. The Face Set Selection tool activates and returns the accepted face set.
4. The tool previews the generated support positions as blue crosses and draws the selected area boundary in yellow.
5. The user adjusts:
   - **Spacing**: maximum interior grid spacing, default `3.0`.
   - **Boundary spacing**: absolute spacing along the offset boundary, default `2.4`.
   - **Concave angle**: concave corner threshold in degrees, default `30`.
   - **Show support spacing**: shows transparent blue spacing circles around previewed supports.
6. The user clicks **Apply**. A support layer group named **Area Support** is created or updated, and the tool enters edit mode.
7. The user clicks **Close** to exit edit mode.

## Generation Behavior

Area Support stores selected `FaceSelectionKey` values plus its numeric settings in `AreaSupportSettings`, so support geometry can be regenerated after edits, save/load, and model transforms.

For each contiguous selected face island:

- Boundary edges are extracted from selected triangles and assembled into ordered XY boundary loops.
- The visible yellow boundary remains the original selected face boundary.
- Boundary support candidates are placed on an implicit offset boundary that is `Spacing / 2` inward from the original boundary.
- Boundary candidates are stepped using **Boundary spacing**, not the interior grid spacing.
- Concave corners are detected from ordered loop winding. If the concave turn angle is greater than **Concave angle**, an extra support is placed on the offset corner.
- Interior supports are generated on a hexagonal grid.
- All generated candidates must be inside the selected projected face area and outside the half-spacing boundary exclusion band.
- Candidates are vertically projected back onto the uppermost selected face below their XY position before support entities are created.

## Code Map

- `src/Pillar.Core/Layers/AreaSupportSettings.cs`: persistent generator settings.
- `src/Pillar.Geometry/Supports/AreaSupportPattern.cs`: boundary extraction, loop ordering, boundary/corner/grid support placement, and projection.
- `src/Pillar.Rendering/Tools/AreaSupportOperation.cs`: tool state, preview refresh, Apply/update commands, and edit-mode support selection.
- `src/Pillar.Rendering/Preview/AreaSupportPreviewRenderer.cs`: yellow boundary, blue support crosses, and optional spacing circles.
- `src/Pillar.UI/Modes/AreaSupportToolOptionsControl.xaml`: Area Support options UI.
- `src/Pillar.Core/Persistence/GphDocumentSerializer.cs`: `.gph` persistence for Area Support settings.
