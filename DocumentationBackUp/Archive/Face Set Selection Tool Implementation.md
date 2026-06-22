# Face Set Selection Tool (referred to as FSST in this document)
A tool that allows the user to build a selection set of model faces. This tool is designed as a helper that provides an input face selection set for other tools that launch it (these are the client tools). While the face set selection tool is designed to be accessed as part of another tools user interface, for testing we should add a button called "Face Select" to launch it from the Main toolbar.

## User Interface
The tool uses a floating window (called Face Set Selection Tool Panel) to provide tools that can add or subtract from a selection set of model faces. It has the following controls:
-A row of buttons (Face Selection Tools):
	- A "Select" button.
	- A "Line Select" button
	- A "Angle Select" split button.
		- Clicking the down arrow on the split button opens a flyout with:
			- a label "Coplanar threshold"
			- a numeric entry box (min 0 degrees, max 180 degrees). This is the Coplanar Threshold setting.
		- Clicking the button itself prompts the user to click on the model. 
- On a second row of buttons:
	- On the left:
		- Two linked toggle buttons (selection set modifiers):
			- An add to selection toggle button (button label = "+"). Holding down SHIFT toggles this button on.
			- A remove from selection toggle  button (button label = "-") Holding down ALT toggles this button on.
		- A clear selection button (button label = "Clear")
	On the right:
		- An OK button.
	
## Workflow
The user launches the tool.
- The Face Set Selection Tool Panel opens.
- The client tool passes its record of the current face selection set to the FSST. It is possible that there will be no faces in this set.
- The FSST colours the faces in the selection set yellow (the colour should be defined in the Application Settings).
- The user makes use of the Face Selection Tools mentioned above to select faces and add or remove them depending on whether the add to selection or remove from selection toggle button is active.
	- See "Modifying the selection set workflow" section below.
- The user clicks OK. 
	- The Face Set Selection Tool Panel is closed. 
	- The new selection set of faces is handed back to the client tool.

### Modifying the selection set workflow
- When the add to selection toggle button is active, faces selected by the Face Selection Tools add to the selection set.
- When the remove from selection toggle button is active, faces selected by the Face Selection Tools are removed from the selection set.
- The "Select" button allows the user to add or remove faces by clicking directly on them.
- The "Line Select" button allows the user to draw a polyline (multiple points and segments) on the screen and select any faces the line crosses (fully or partially).
- The "Angle Select" button lets the user click on a face on the model:
	- Neighbouring faces where the difference between the normals is less than the Coplanar Threshold setting are also selected. The selection continues to propagate selecting more faces as long as the angle between normals for neighbouring faces is less than the Coplanar Threshold setting.
- The "Clear Selection" button removes all faces from the selection set.

## Notes
- Since the FSST tool is used by other (client) tools to provide a selection set of faces, than the client tools that launch the Face Set Selection Tool will be responsible for remembering the selected faces, not the Face Set Selection Tool itself. The client tools will need to remember these selected faces in the saved project file for later editing.
- Undo and Redo needs to work for each addition or subtraction of faces to the selected face set.
- Only when clicking OK on the Face Selection Tool Panel is the modified selection set handed back to the original client tool.
- Please ask for more clarification if needed.
- Remember this tool is designed to be called by other tools, and use its results as an input. Make sure the design of the code reflects this.
- The tool can return several non-contiguous selections.
- Create a file "Face Set Selection Tool.md" that details what the tool does from a user workflow and code point of view. This will be used by myself to remember how the tool works, and by you when you need to create tools that use it.

## Implemented code map
- `src/Pillar.Core/Selection/FaceSelectionKey.cs`
	- Defines the data passed between the FSST and client tools.
	- A face is identified by the owning mesh entity id and the zero-based triangle index in that mesh.
	- This keeps face selection independent of WPF, Helix, or render models.
- `src/Pillar.Geometry/Analysis/FaceSetSelectionAnalyzer.cs`
	- Contains the renderer-agnostic mesh math for finding a clicked triangle, selecting projected triangles crossed by a screen-space line, and growing a connected coplanar face set from a seed triangle.
	- Angle selection builds triangle adjacency from shared local-space mesh edges, then flood-fills through neighbours whose world-space normal angle is within the threshold.
- `src/Pillar.Rendering/Tools/FaceSetSelectionTool.cs`
	- Owns one temporary face-selection editing session.
	- It implements `ITool`, receives viewport mouse input, applies Select / Line Select / Angle Select operations, and maintains a session-local undo/redo history.
	- It does not save state to the document. Client tools receive the final face set only after OK.
- `src/Pillar.Rendering/Scene/SceneManager.cs`
	- Adds mesh-face hit testing and a `ConfigureFaceSelection` rendering entry point.
	- This keeps Helix hit testing and visual overlays in the rendering layer.
- `src/Pillar.Rendering/EntityRenderers/MeshRenderer.cs`
	- Adds a visual-only selected-face overlay mesh.
	- The overlay is not hit-testable, so future picks still hit the real imported mesh.
- `src/Pillar.UI/Modes/FaceSetSelectionToolPanel.xaml`
	- Defines the floating panel UI: Select, Line Select, Angle Select settings, add/remove modifiers, Clear, Undo, Redo, and OK.
- `src/Pillar.UI/MainWindow.FaceSetSelection.cs`
	- Hosts the reusable helper from the current shell.
	- The toolbar "Face Select" button launches a test session and stores the accepted result in memory until a real client tool is added.

## Current user workflow
- Click "Face Select" in the main toolbar.
- Use "Select" to click individual faces.
- Use "Line Select" by clicking a first polyline point, then clicking subsequent points. A dashed screen-space line previews the active segment under the cursor. Each committed segment samples the line from the current camera viewpoint and selects only the front-most visible mesh faces hit under the line.
- While already in "Line Select", clicking the "Line Select" button again clears the current anchor and starts a fresh line selection from the next viewport click.
- Use "Angle Select" to click a seed face and select connected neighbours within the coplanar threshold.
- Use "+" or Shift to add candidate faces.
- Use "-" or Alt to remove candidate faces.
- Use Clear, Undo, and Redo for temporary selection edits.
- Click OK to accept the temporary face set. The toolbar test launcher stores the result in memory; future client tools should provide their own accept callback and persist the resulting `FaceSelectionKey` values as part of their own settings.

## Architecture notes
- The FSST is intentionally a helper session, not a document entity.
- The domain-facing result is only a collection of `FaceSelectionKey` values.
- Rendering is responsible for overlays and hit testing, while geometry is responsible for mesh traversal and selection math.
- This mirrors real CAD systems where selection filters produce lightweight ids, and the consuming command or feature owns what those ids mean.

## Known limitations and next steps
- The toolbar launch is a test harness. A real client tool should call the helper with its saved initial selection and handle the accepted selection.
- Line Select currently commits each segment as the user clicks the next polyline point. A future polish pass could add an explicit finish gesture and retain the full drawn polyline during the session.
- Line Select is intentionally viewpoint-dependent. It uses viewport hit testing along the screen-space line, so hidden faces behind the visible surface are not selected.
- Persisting selected faces for a real client tool should be done in that tool's settings/data model, not in the FSST session.
