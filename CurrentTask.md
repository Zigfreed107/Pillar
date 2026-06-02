# Current Task
I want to expand on the functionality of supports. Supports are currently completely vertical, with the tip joining to the model vertically. I want to add an option to allow the tip of the support to join to the model at an angle, which will allow for better support of overhangs. This will be an optional feature that can be enabled or disabled by the user.

## User Interface:
In the Support Preset Editor, in the Head section:
- add a numeric box with the label "max head angle from vertical".
	- It can accept values from 0 (vertical) to 90 (horizontal).
- Ensure this parameter is added to the default settings for supports.


## Logic
- Supports now allow the head to be angled perpendicular to the face they intercept. 
- The head can never be more than the max head angle from verical as specified in the support preset. If the angle perpendicular to the intercepted face would require the head to be at a greater angle than this, then the head will need to be created at the max head angle.
- A ball mesh should be placed between the top of the Stem and bottom of the Head, so that the connection remains smooth.
- The top of the Stem and bottom of the Head meshes should be closed, since we can no longer guarentee the join.


# Final Notes
- Do not worry about backward compatibility with previous saves or projects - we are still building the software.
- Review SupportFunctionality.md for details on how supports are currently implemented, and ensure that the new functionality is consistent with the existing design.



