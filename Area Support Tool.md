Add a new support tool (called the Area Supports tool) for creating supports over a area defined by a selection of model faces when viewed from the Z up direction.

This tool makes use of a reusable sub-tool (called Face Select) that allows the user to select and deselect faces on a model forming a selection set that can be used by the area support tool. The code for this sub-tool should be made so it can be easily reused by other future tools.

# Workflow
## Creating the supports
### Step 1 - Starting the tool
- The user selects the Area Supports button in the Mode Panel in the Supports tab.
- The settings panel for the tool opens.
- Underneath that, the support presets panel is displayed.
### Step 2 - Selecting the area to support. This makes use of the Face Select "sub tool"
- The user clicks the "Select Area" button. This starts the face select sub tool.
- A floating window (Face Select controls panel) belonging to the Face Select tool is displayed with buttons allowing the user to select faces on the model.



# GUI
## Area Support Options panel

## Face Select controls panel
A floating window that provides controls to allow the addition or subtraction of model faces to a selection set.
- A Select contiguous faces by angle split button.
	- clicking the down arrow of the split button displays a settings flyout with:
		- a label "Coplanar threshold"
		- a numeric entry box (min 0 degrees, max360 degrees).
	- clicking the enters a mode where when the user clicks on a face, it selects all faces that touch each other as long as the angle between each face's normal is less than the angle "Coplanar threshold".
- A select by 
- An "add to selection" button. When pressed, any *************
- A "subtract from selection" toggle button. When this is toggled on, any face selections are removed from the selection set.
- The "add to selection" and "subtract from selection" buttons act as a group where only one can be on at a time.

The face select tool 

