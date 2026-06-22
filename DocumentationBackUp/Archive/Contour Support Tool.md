Add a new support tool called "Contour Support Tool". 

# Workflow
## Creating the supports
- The user selects the Contour Support button in the Mode Panel in the Supports tab.
- The settings panel for the tool opens.
- Underneath that, the support presets panel is displayed.
- The user is prompted in the status bar to click on the model.
- The user clicks on the model, and the Z value of the hit point on the model is populated in the settings panel.
- A yellow contour is drawn around the model at that Z height. The contour should only be drawn on faces that form a connecting loop with the original face selected. That is to say, that contours should not be placed everywhere on the model at the given Z height, but restricted to the loop that is part of the original selected face. 
- The contour should also not cross onto a new faces if the angle between the current faces and next face normals is greater than the Coplanar threshold setting in the tool settings panel. 
- The support placement is previewed using blue crosses (the same as the Ring Support Tool or Line Tool), where the crosses are spaced according to the support spacing set in the settings panel. The supports should never be further apart than the spacing.
	- In the case of the contour forming a closed loop, the actual spacing can be reduced as little as possible to ensure consistent spacing between the first and last.
	- In the case the contour has a start and end point, supports are placed at an offset from the start and end based on the settings for start and final offset in the settings panel.
- The user clicks the Apply button, and the supports are generated according to the selected support preset. The tool enters edit mode where the user can select supports and delete them (clicking the Delete Button, or hitting DEL ont he keyboard) - the same as for other support tools. Once Apply is clicked, the supports are added as a sub layer to the model in the Layer Panel. 
- The user clicks the Close button and the tool (and edit mode) exits.
## Editing Supports created earlier
- When the user clicks on the Edit button on the support layer, the tool enters edit mode. the user can select supports and delete them (clicking the Delete Button, or hitting DEL on the keyboard). 
- The user can alter the Z height of the contour or any of the other settings. When the user clicks Apply, the support entities are recalculated. 

# GUI
- The settings panel has a numeric entry box that shows the Z height the user clicked on the model. The user can alter the Z height the supports will be generated on by altering this value.
- Next to the z height numeric entry is a button showing a mouse cursor icon. If the user clicks on this, they can re-click on the model to select a new Z height. 
- A numeric entry control asking for "Coplanar threshold" in degrees from 0 -360. Two faces are considered coplanar if their normal is less than the Coplanar threshold. 
- A numeric entry control asking for the support spacing.
- A numeric entry control asking for the start support offset. This is greyed out in the case the contour forms a loop. But is enabled as soon as any settings change that cause the contour to no longer form a closed loop.
- A numeric entry control asking for the final support offset. This is greyed out in the case the contour forms a loop. But is enabled as soon as any settings change that cause the contour to no longer form a closed loop.
- A Delete button that is only enabled when in edit mode and a support is selected.
- An Apply button at the bottom left of the settings panel.
- A Close button at the bottom right of the settings panel.

