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



# Rotation Transform Implementation

## Workflow

Select one imported model, then open **Transform > Rotate**. The Mode Panel hides and the Rotate Options panel shows one World/Local toggle followed by X, Y, and Z degree inputs. The tool starts in World space so its initial behavior remains predictable for new users.

- **World** applies the inputs around the fixed scene X, Y, and Z axes.
- **Local** applies the inputs around the model's own axes. The filled axis guides follow the model orientation while Local is active.
- Switching World/Local preserves the current preview, changes the input baseline to that orientation, and resets the three displayed values to zero. This prevents entered values from jumping or being reinterpreted.
- **Reset** returns all inputs to zero and removes every user rotation applied since import. Scale and the model pivot position are preserved, and the selected World/Local space remains active. Reset is still a preview until Finish; Cancel restores the orientation present when the tool opened.
- **Finish** records the complete preview as one undoable Rotate Model command.
- **Cancel**, Escape, a mode change, or invalid selection restores the starting transform without history.

## Code architecture

- `RotationCoordinateSpace` in Core defines the renderer-independent World and Local choices.
- `MeshRotationTransform` in Core owns fixed-origin rotation composition without UI or rendering dependencies.
- `RotationOriginPreviewRenderer` in Rendering owns three reusable, non-hit-testable filled guide discs and accepts an orientation only for display.
- `RotationToolOptionsControl` owns input synchronization and emits preview, coordinate-space, Reset, Finish, and Cancel requests.
- `MainWindow.TransformRotation.cs` coordinates the original session transform, the temporary input baseline, and live preview state.
- `SetMeshUserTransformCommand` owns the permanent transform and its Undo/Redo behavior.

The imported vertices and `ImportPlacementTransform` never change. Rotation is stored in `UserTransform`, separately preserving the imported orientation. Reset sets `UserTransform.Rotation` to identity and compensates `UserTransform.Translation` around the stable pivot, so the model returns to its import orientation without jumping or losing scale. The stable pivot is the same bottom-center import-space origin used by Scale. X, Y, then Z deltas are composed after the starting orientation for World space and before it for Local space. Translation compensation keeps the pivot fixed while retaining existing scale.

Guide diameter is calculated once at session start as 1.2 times the largest model bounding-box dimension. X is a 75% translucent red YZ disc, Y is a green XZ disc, and Z is a blue XY disc. The discs remain fixed to scene axes in World space and follow the preview rotation in Local space. Their vertex and normal collections are preallocated and reused; input changes update the existing buffers rather than creating scene objects.

Preview changes update only `MeshEntity.UserTransform`; they create no commands and do not regenerate supports. Finish restores the original transform, calculates support-group regeneration once, and executes `SetMeshUserTransformCommand`, so model rotation and attached generated supports Undo/Redo together.
