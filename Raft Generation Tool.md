# Raft Generation Tool
A tool for generating rafts as commonly used in 3D resin printing. The rafts purpose is to provide a stable base for the print and improve adhesion to the build plate.

## Rafts Layers on the Layer Panel
A model can only have one raft. If a raft is present, it will be displayed on the layer panel as a child layer underneath the relevant model, but a as a parent row to all supports layers.

A raft layer should have:
- a visibility toggle button to the left of the raft layer name - like other layer types.
- an edit button to the right of the raft layer name - like other layer types. Clicking this will open the raft tool with the current raft settings.
- The layer name should be the raft type used to generate the layer.

## GUI
### Mode Panel
1. Add a new tab to the Mode Panel called "Rafts".
2. On that tab, add a button labeled "Raft". Clicking this opens the tools option panel.

### Option Panel
Add the following controls:
- A combo box to select the raft type. Choices are "Footprint", "Mesh", and "Feet"
- An "Apply" button at the bottom of the panel that exits the tool keeping the generated raft entity. 
- A "Cancel" button at the bottom of the panel that closes the tool without generating a raft. If this tool is launced by editing an existing raft, the cancel button will revert the raft to its original state.

The remainder of the controls will depend on the selected raft type and are described in the subsections below. Theys should appear below the combo box and above the Apply/Cancel buttons.

#### For Footprint Type (this is the default selected type in the combo box):
- A numeric input for "Raft Height" (default: 0.7mm)
- A numeric input for "Lip Height" (default: 15mm)
- A numeric input for "Lip Width" (default: 0.7mm)
- A numeric input for "Edge Angle" (default: 45 degrees, minimum: 30 degrees, maximum: 90 degrees)
- A numeric input for "Footprint Offset" (default: 0.0mm, minimum: 0.0mm)

#### For Mesh Type:
- A numeric input for "Raft Thickness" (default: 0.7mm)
- A numeric input for "Line Thickness" (default: 1.5mm)
- A numeric input for "Max Side Length" (default: 50mm)
- A numeric input for "Edge Angle" (default: 45 degrees, minimum: 30 degrees, maximum: 90 degrees)

#### For Feet Type:
- A numeric input for "Raft Height" (default: 0.7mm)
- A numeric input for "Foot Size" (default: 10mm)
- A numeric input for "Edge Angle" (default: 45 degrees, minimum: 30 degrees, maximum: 90 degrees)

## User Workflow
The Raft button in the Mode panel should only be enabled if:
- exactly one model is selected in the viewer or layer panel, OR
- one or more support layers where all belong to the same model are selected in the viewer or layer panel.
- the resolved model owns at least one concrete support entity.

The Rafts tab should explain this prerequisite with the note: "Select a model with at least one support before creating a raft."
When the Raft button in the Mode panel is clicked, the Raft tool should be launced and its options panel should be displayed. The raft is to be added to the model that is selected, or the model that owns the selected support layers.

A raft entity is genreated using the settings in the option panel. The logic for how rafts should be generated is described in the **Logic** section below.

If the user changes the raft type, or any of the raft settings, the raft entity should be regenerated using the new settings.

If the user clicks the Apply button:
- For a model that has no existing raft - the raft entity is added as a raft layer and the tool exits.
- When editing an existing raft, updated raft entity replaces the old one.

If the user clicks the Close button:
- For a model that has no existing raft - the tool exits without adding a raft entity.
- When editing an existing raft, the raft entity is reverted to its original state before editing and the tool exits.

## Logic
The following subsections describe the logic for generating each type of raft.

### Footprint Raft
In plan view, the raft should be a convex, hole-free envelope around every support base owned by the model.
- Each support contributes its XY base location and physical BaseBottomRadius.
- Coincident locations use the largest base radius.
- One distinct base produces a disc, two produce a capsule, and three or more produce a convex hull.
- The envelope expands outwards by the non-negative "Footprint Offset" value.

The support envelope forms the bottom contour at Z=0. The top contour expands outward according to the requested edge angle and is placed at the "Raft Height" value. A lip should be added around the edge of the raft.

A lip is a vertical wall that extends up from the edge of the raft. This lip should extend up from the top of the raft a by a further height as specified by the "Lip Height" value. The lip's width should be as specified by the "Lip Width" value. that width is measured from the outer edge of the raft top (taking into account the edge angle) inwards towards the centre of the raft.

The edge angle is the angle of the raft and lip wall to the horizontal plane. The edge angle is measured from the horizontal plane to the wall of the raft and lip, with 0 degrees being horizontal and 90 degrees being vertical. The edge angle can never be below the minimum of 30. The wall expands outward as it rises so the physical support envelope is never cut away. If the requested lip width cannot fit, the footprint receives the minimum additional uniform outward padding needed for valid nested contours.


### Mesh Raft
A mesh raft joins the bases of all supports in a model by using lines of a given width and height to form a convex triangular mesh. It is like forming a delauny wireframe mesh of points.

It should:
- join each support bases together with lines to form triangles, but the interior of the triangles are not filled.
- the "lines" should really be rectangular prisms with the corss-sectional width given by "Line Thickness" and height "Raft thickness".
- the edge angle is the angle the raft wall makes from the horizontal plane. An angle of 90 would be vertical. The edge angle is measured from the horizontal plane to the wall of the raft and lip, with 0 degrees being horizontal and 90 degrees being vertical. The edge angle can never be below the minimum of 30, since that would result in a raft of zero height. When applying an edge anlge less than 90 degrees, the top width of the line is increased, the bottom width should always be equal to the "Line Thickness" value. In other words, with a non-90 degree edge angle, the cross section of each line would take the form of a trapezoid with the base segment of the trapezoid equal to the "Line Thickness".
- If a "line" is greater than the "Max Side Length" parameter, then it should not be generated.

### Feet Raft
This raft does not act as a monolithic base that connects all supports, but rather acts like an individual raft applied to the base of each support. This may result in these individual rafts ("feet") overlapping and forming a larger raft mass, but this is not always the case.

The raft or "foot" placed on each support:
- takes the form of a square with side length equal to the "Foot Size" parameter.
- has a height given by the "Raft Height" parameter.

The edge angle is the angle of the foot wall to the horizontal plane. The edge angle is measured from the horizontal plane to the wall of the raft and lip, with 0 degrees being horizontal and 90 degrees being vertical. The edge angle can never be below the minimum of 30, since that would result in a raft of zero height. The edge acts as a chamfer around the edge of the raft, with the chamfer cutting away from the edge as it moves underneath the raft, not extending the walls outward.  When applying an edge anlge less than 90 degrees, the top width of the foot is increased, the bottom width should always be equal to the "Foot Size" value. In other words, with a non-90 degree edge angle, the cross section of each foot would take the form of a trapezoid with the base segment of the trapezoid equal to the "Foot Size".