# Direct Edit Tool

This tool allows the user to directly edit individual supports by clicking on different parts of them and interacting with on screen gizmos. For example:
- Moving the base of a support to change the stem's position, while keeping the head tip in place.
- Moving the ball at the interseciton of the stem and branch/head to change the height of the stem and therefore the branch/head angle, while keeping the base and intersection of the model in place.

This allows the user to fine tune supports so that they better fit around the model, and make them easier to remove after printing.

# GUI

- The Direct Edit options panel remains open while supports are selected and edited. It closes only when the user chooses **Close** or leaves the tool.
- Click a support stem to select it. Ctrl-click additional stems to add or remove them from the edit selection. The first editable selected stem displays the gizmos, and dragging it applies the same relative edit to every selected stem.
- Dragging from empty viewport space uses the normal Select tool window conventions: left-to-right selects fully enclosed support control paths, right-to-left selects crossing paths, Shift adds, and Ctrl subtracts. Selection is restricted to editable supports in the active support layer.
- **Highlight angles less than** numeric control. Any supports heads or branches that make an angle less than this with the XY plane are coloured according to the "FaceAngleHighlightColor" application setting.

# Editing the base of a support
1. The user clicks on the stem mesh of a support.
2. On the z=0 plane where the base sits, a XY gizmo is displayed.
	- The XY gizmo consists of:
		- A red arrow pointing in the X direction with origin at the base's origin.
		- A green arrow pointing in the Y direction with origin at the base's origin.
		- An yellow square plane drawn between the two arrows with side length equal to the arrow length, with one corner at the base's origin.
		- The length of the arrows for the gizmo are 1.5x the base's diameter. Save the 1.5x factor in the application settings.
3. The user interacts with the gizmo by:
	- clicking on the red X arrow and dragging - the base and stem of the support move in the X direction only in proportion to the mouse movement.
	- clicking on the green Y arrow and dragging - the base and stem of the support move in the Y direction only in proportion to the mouse movement.
	- clicking on the yellow square and dragging - the base and stem of the support moves in the XY plane only in proportion to the mouse movement.
4. As the stem and base move, the top of the head stays at the same place it intersects the model, and the branch and head are redrawn so that the stem is still connected to the branch and or head base (and ball joint in between)..
# Editing the Stem height
1. The user clicks on the stem mesh of a support.
2. On the ball mesh that joins a stem to the branch or head, a Z gizmo is displayed.
	- The Z gizmo consists of:
		- A blue arrow in the Z direction with the origin at the centre of the ball mesh.
		- The length of the arrow is 3x the ball's diameter, which is the stem's diameter. Save the 3x factor in the application settings.
3. The user interacts with the gizmo by:
	- clicking on the blue z arrow and dragging  - the ball mesh and top of the stem moves up and down in the z direction in proportion to the mouse movement. 
4. As the height of the stem changes, the top of the head stays at the same place it intersects the model, and the branch and head are redrawn so that the base of the branch or head is still connected to the top of the stem (and ball joint in between).

# Editing of clustered supports
If the user is editing a cluster of supports, then the editing takes place on the shared stem and each branch or head will have to be redrawn to keep the connection between the stem and the connection points on the model.

# Editing of braced supports
If the user is eding a support that is joined to others by bracing, then the bracing will need to be redrawn to continue to connect the supports. Since each bracing entity remembers its Maximum and Minimum Bracing Angles and Maximum Length used when creating them, these can be used when redrawing the bracing:
	- if the new length is greater than the remembered bracing length, then it is not recreated.
	- if the angle of the brace cross member compared to the XY plane is greater than the maximum angle, then it is not recreated.
	- if the angle of the brace cross member compared to the XY plane is less than the minimum angle, then it is not recreated.

# Editing of buttressed supports
The editing of buttressed supports and their buttresses has some nuance:
- Buttressed supports should rember and Minimum Bracing Angles and Buttress Spacing used to create them.
- A user can click on either the buttressed (original) support that joins to the model or any of the individual buttress supports that join to the original support.
- Only the original support being buttressed can be edited however, and gizmos will only be drawn on it.
- The original support can be edited the same way an individual support can.
- Once the original support is edited, the buttress is recalculated immediately using the remembered settings of Minimum Bracing Angles and Buttress Spacing. It should be recreated using the same logic that it is created with in the Support Edit Bracing tool.

# Notes
- If anything is unclear, ask for clarification. Do not make assumptions or guesses.
- Update this file if logic changes, but keep implmentation details in a new section - keep the above sections as a description of the tool and its workflows from a user perspective.

# Implementation

- Direct edits are stored as renderer-independent, revision-bound DirectEdit modifier actions. Each action records both its original and edited shared-stem geometry so modifier removal, undo, redo, save/load, and full stack replay remain deterministic.
- Direct Edit actions replay after clustering and before brace or buttress generation. This lets one edit target a clustered shared stem while ensuring stored reinforcement limits are reapplied to the edited geometry.
- A tool launch owns one modifier session. Repeated drags append actions to that session, and reopening its Layer Panel row resumes the session using the normal downstream-modifier warning behavior.
- The viewport controller owns hit testing and transient preview geometry. Document output changes only when a drag completes.
- Gizmos follow normal viewport support selection and remain visible on the first editable selected support. A multi-selection applies the first gizmo's relative drag delta independently to every selected individual or shared clustered stem.
- Axis arrows use solid cylinder-and-cone meshes with reusable transforms, providing larger visual and hit-test targets than line geometry.
- Clicking a generated buttress resolves its original support by the buttress connection point. Buttress regeneration runs immediately when the drag completes.
- Support output replacement groups document mutations so camera bounds and low-angle highlight geometry refresh once per completed edit rather than once per regenerated entity.
- Equivalent regenerated brace and buttress entities retain their existing document and render instances, so only reinforcement geometry affected by an edit rebuilds its mesh.
- Low-angle diagnostics are render-only overlays on the affected head and branch parts. The configured FaceAngleHighlightColor is used without changing support-layer colors.
- DirectEditXYGizmoScale and DirectEditZGizmoScale are application settings with defaults of 1.5 and 3.0. The angle threshold is a saved user setting.