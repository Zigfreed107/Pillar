# Rotation Transform
Build the Rotation Transform tool found on the Rotate button on the Transform tab of the main Mode panel. Currently, the button points to a stub function.

## User Interface
The Options Panel for the Rotation tool should have the following controls arranged in a vertical column:
- Numeric inputs for X, Y, and Z axis rotation.
	- The X label should be red
	- The Y label should be green
	- The Z label should be blue
- A Reset Button.
- A Finish button
- A Cancel button

## Workflow
- The user selects a model either in the viewer or Layer panel
- The user opens the tool by clicking on the Rotate button in the Transform tab of the Mode panel.
- The mode panel is hidden while the tool is active
- The Options Panel for the Rotate function is shown
- Three 50% translucent circles are drawn at the rotation origin of the model. These circles should have a diamter 1.2 times the largest bounding box size of the model. These are visual only, the user cannot interact with them. The colours of each circle is the same as the colour of the axis it represents as used by the axis widget drawn in Helix Toolkit..
	- X axis circle (the circle is on the YZ plane)
	- Y axis circle (the circle is on the XZ plane)
	- Z axis circle (the circle is on the XY plane)
- The user enters a rotational value (in degrees) in the Numeric Inputs for the X,Y,Z axis controls. The model rotates automatically as values are entered as a form of preview.
- If the user clicks the Finish button, the roations is applied to the model permanatly.
- If the user clicks the Cancel button, the tool exits and no rotations are applied.
- If the user clicks the Reset button, all rotations are set to zero and the model is set back to the same rotation it was before.


# Notes
- The models initial orientation on import is always remembered. Rotations are stored as a separate transform. This is similar to how Scale transforms work.
- Undo and Redo is only available once the user clicks Finish. It is not required to undo and redo each change they make to the Numeric inputs for X, Y, and Z axis roation, nor for the preview as those values are changed.
- Document how the tool works in the code and workflow in a file "Rotation Transform Implementaiton.md"

