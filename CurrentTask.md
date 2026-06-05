# Current Task
I would like to implement the line support tool. This is found in the Supports tab of the Mode Panel.

# User Interface
The Line Support Options panel has the following controls:
- A numeric entry with label "Spacing" (the same as in Ring Support Options panel). This control sets the distance between supports generated along the line.
- A Delete button.
- An Apply button at the bottom left.
- A Close button at the bottom right.

# Workflow
- The user selects a model, either from the viewer or Layer panel.
- The user selects the Line tool from the Supports tab in the Mode Panel.
- A Line Support Options panel is displayed. Underneath the Support Preset panel is displayed.
- The user left clicks on the model in the viewer. This is the first point on the line.
- As the user moves the mouse, a line is drawn between the first point and the mouse cursor. The line is a preview that updates as the mouse moves.
- As in the Ring Support Tool, a transparent blue sphere with diameter the same as the support Spacing control in the Line Support Options panel is drawn around the mouse cursor. A solid cricle is also drawn planar to the XY plane with the same diameter as the sphere at the mouse cursor
- The user clicks a second point on the model. The preview line is drawn between the first point and this second point that is clicked. The second point is the hit point on the model where the mouse was clicked. Unlike the Ring Support tool, the line support tool is not drawn horizontally, but between points clicked on the model.
- The user can click as many more points as they want, with the preview line extending to each new point (like a poly line in CAD).
- When the user right clicks, that indicates that the final point has been selected, and that preview supports should be generated along the line. 
	- This generation of preview supports is similar to what happens with the Ring support tool after the third diameter point is clicked, blue crosses are drawn on the model at the point where they would intersect the model when the project up from the spacing on the preview line in the z direction.
- The user can change the spacing in the Spacing control, at which point the preivew locations are updated.
- When the user clicks the Apply button, supports are generated at those locations on the model. The tool is now in Edit Mode (see below for more information). The Layer Options panel now shows the line support as a sub layer of the model they were added to.
- The user clicks the Close button, and the tool and edit mode is closed.

# How to generate points along the line
At each point clicked and equally spaced along each line segment at a distance no greater than the "Spacing" setting in the Line Support Options panel. 

# Edit mode
The user enters Edit mode for Line Supports in two ways:
- After clicking the Apply button on the Line Support Options panel after creating the preview line.
- Clicking on the Edit button next to the support layer for a Line Support layer in the Layer Panel.

In Edit mode, the user can edit the supports (just like with the Ring Support tool), by selecting them in the viewer and clicking the Delete button in the Line Support Options panel, or pressing the DEL key on the keyboard.