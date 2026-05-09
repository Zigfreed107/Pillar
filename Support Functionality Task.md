# CURRENT TASK:
Implement a more sophisticated supports model.

## Model
Supports should be modelled as follows:
	- Each is made of three sections: 
		- a "base" which is the part that attaches to the build plate
		- a "stem" which is the long thin part that extends up to the model
		- a "head" which is the part that attaches to the model.

### Base:
The base is a truncated cone. It has:
	- a bottom radius (the radius of the part that touches the build plate)
	- a height (the height of the base section).
	- the top radius is determined by the stem thickness setting, so that the stem can be attached to the base without a visible seam.

### Stem:
The stem is a cone. It has:
	- a bottom diameter (the diameter of the part that attaches to the base, and sets the top diameter of the base)
	- a top diameter (the diameter of the part that attaches to the head).
	- The stem's height is the distance between the top of the base and the bottom of the head.If this distance is zero or less, the stem is not created and the head is attached directly to the base. The model should still remember that the stem concept exists.

### Head:				
The head is a truncated cone. It has:
	- a bottom diameter (the diameter of the part that attaches to the stem, and sets the top diameter of the stem).
		- If the stem is not created, this sets the top diameter of the base instead.
	- a height, the distance between the point of intersection with the model, and the point where the stem (or base if no stem) begins. 
		- If the height of the stem is greater than the distance from the intersection of the model and the top of the base, then the head is shortened to fit. If the head needs to be shortened by more than 50% then while the support still exists as a model.
	- a penetration depth, which is how far the head penetrates into the model. This should be a positive value.
	- a top diameter, which is the diameter of the head at the point where it intersects the model, and not the top of the truncated cone that is the penetration depth into the model.

Supports are "cylindircal", approximated with a number of sides. The number of sides is determined by the "SupportSides" setting in the applications settings.

All other aspects remain the same as currently implemented.

## Support Presets
Support presets are a collection of the dimensions described in the Model section above, as well as some additional settings that will be added in the future. The user can create and save support presets, and then select them when creating supports to quickly apply a set of dimensions and settings to the supports they are creating. This will allow users to easily reuse their preferred support configurations across different models and projects, and quickly switch between different configurations as needed.

## GUI

### Support Panel:

Support models parameters  (described in the Model section of this document above) can be saved as "Presets". When creating suppors, the user will be required to select a support present which will be used to create the supports with the appropriate dimensions and behaviours. They will select this preset from a "Support Panel" that appears underneath the Tool Options Panel. The Support Panel will only appear when using a tool that adds or edits supports - at this point this is only Point supports and Ring supports.

The support panel has the following layout from top to bottom:
- A label at the top with content "Support Preset"
- A combobox that lets the user select from support presets they have created. 
- A diagram that visually represents a simplified support model. This will be implemented later. For now draw a placeholder rectangle.
- A button at the bottom right that says "Advanced". Clicking this will open the "Support Preset Editor" window.

Any supports created or edited while a preset is selected in the combobox will have their dimensions and behaviours set according to that preset.

### Support Preset Editor Window:
This is a floating window, not an overlay. The functionality in this window will be expanded on in later stages, but for now it has the following layout:
1. A label at the top with content "Support Preset Editor".
2. Underneath, a combobox that lets the user select from support presets they have created. Changing the selected preset will update the values in the rest of the UI to match the dimensions of the selected preset. If the user types in a new name, and the save button beside it is clicked then a new preset will be created with the dimensions currently in the UI and the name that the user typed in. If a preset with that name already exists (or if an existing preset is still selected int he combobox), then it will be overwritten with the new dimensions.
3. Beside this combobox to the right is a save button that saves the current dimensions as a preset, as described in the previous point.
	- Each section should have NumericUpDown controls for the relevant dimensions described in the Model section above.
5. At the bottom right, a button that says "Save & Close" that closes the window, and saves the dimensions over the preset selected in the combobox in the 2nd point above.

When closing the window, the support preset selected in the combobox in this window is automatically selected in the combobox in the Support Panel.

All controls should be WPF native if possible - avoid custom controls for now. All dimensions should have 1 decimal place.

### Backwards Compatibility:
There is no need to maintain backward compatibility with any existing project files.

## Finally:

Write a summary of how the above works in the code, so you can refer to it during later work. Update the file "Documents\upportFunctionality.md" with this summary.