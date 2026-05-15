# Current Task

It is often only necessary to add supports to faces that are less than a certain angle from the horizontal. I wish to:
- Identify these faces in the viewer by colouring those faces on the model a red colour.
- Allow support tools to give the option to only consider these faces when calculating intercections for support generation. This will be implemented in a future task, but the groundwork for identifying these faces should be laid in this task.

## User Interface:
- A checkbox in the main toolbar to enable/disable the angle threshold feature. When enabled, faces that are less than the specified angle from the horizontal will be coloured red in the viewer.
- Add a numericUpDown control to the main toolbar that allows the user to set the angle threshold (default 45 degrees). The box should only acept positive values, and should have a maximum value of 90 degrees. No decimals. This box is disabled when the checkbox is unchecked.
- Faces are coloured red if the angle between the face normal and the horizontal plane is less than the specified angle threshold. The horizontal plane is defined as the plane parallel to the build plate (XY plane). Use the application settings to store and retrieve the angle threshold value. Also store the highlight colour in the application settings, allowing users to customize the colour used for highlighting faces.

