# Direct Edit Tool

This tool allows the user to directly edit individual supports by clicking on different parts of them and interacting with on screen gizmos. For example:
- Moving the base of a support to change the stem's position, while keeping the head tip in place.
- Moving the ball at the interseciton of the stem and branch/head to change the height of the stem and therefore the branch/head angle, while keeping the base and intersection of the model in place.

This allows the user to fine tune supports so that they better fit around the model, and make them easier to remove after printing.

# GUI

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
If the user is eding a support that is joined to others by bracing, then the bracing will need to be redrawn to continue to connect the supports. Since each bracing entity remembers its maximum and minimum bracing angles and length, these can be used when redrawing the bracing:
	- if the new length is greater than the remembered bracing length, then it is not recreated.
	- if the angle of the brace cross member compared to the XY plane is greater than the maximum angle, then it is not recreated.
	- if the angle of the brace cross member compared to the XY plane is less than the minimum angle, then it is not recreated.


# Notes
- If anything is unclear, ask for clarification. Do not make assumptions or guesses.
