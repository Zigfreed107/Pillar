# Support Bracing & Buttressing Tool

Support Bracing connects neighbouring supports using angled cross members to form a truss like structure. This is used to increase support stability and strength.

The main uses would be to:
- Connect support stems using cross members so that they can provide additional support to each other, and resist warping or bending.
- Produce buttresses on supports to strengthen tall support stems without making a large diameter support stem that would be harder to remove and use more resin.

## Accessing the tool
1. The tool is accessed by selecting a support layer either from:
	- The Layer Panel
	- From the viewer
2. Selecting clicking the **"Brace"** button in the Edit Supports tab of the Mode Panel. Note that when the Clustering Tool was added, the "Cluster" button was coded to be disabled if either no support layer or more than one support layer was selected. This must also apply to the "Brace" button.
3. The Brace Tool's option panel is displayed.
4. All support layers other than the one selected are hidden.
5. All models other than the one that the selected support layer belongs to are hidden.

## Tool Option Panel GUI

The Tool's Options Panel should show from top to bottom:

- **Bracing** section header label
	- **Maximum Brace Angle** numeric control (min:10, max:80, default:50). A brace cross member will not be created if the angle between it and the horizontal plane is greater than this.
	- **Minimum Brace Angle** numeric control (min:10, max:80, default:50). A brace cross member will not be created if the angle between it and the horizontal plane is less than this.
	- **Maximum Brace Length** numeric control (min: 0, default: 10). A brace cross member will not be drawn if it will be longer than this length.
	- **Diameter** numeric control. The diameter of the brace cross member.
	- **Brace Selected** button. Applies bracing to supports selected in the viewport. Bracing can only connect supports that are selected, they cannot connect a selected support to a non-selected support. Undoable command.
	- **Brace All** button. Applies bracing to all supports in the selected support layer. Bracing can only connect supports that are in the same support layer, they cannot connect a support in one layer to a support in another layer. Undoable command.
	-**Remove Bracing From Selected** button. Removes bracing from any selected supports. Undoable command.
	-**Remove All** button. Removes all bracing from the selected support layer. Undoable command.

- **Buttress** section header label
	-**Buttress supports taller than** numeric control (min:0, default:40). Only supports taller than this will be buttressed.
	-**Buttress spacing** numeric control (min:0, default:10). Buttresses will be placed at this distance from the base of the support being buttressed.
	-**Buttress Selected** button. Applies buttressing to supports selected in the viewport.  Undoable command.
	-**Buttress All** button. Applies buttressing to all supports in the selected support layer that are taller than the "Buttress supports taller than" value. Undoable command.
	-**Remove Buttressing From Selected** button. Removes buttresses from any selected supports or the butresses themselves if they are selected. Undoable command.
	-**Remove All** button. Removes all buttressing from the selected support layer. Undoable command.

- **Close**: Closes the tool and options panel. Unhides any support layers that were hidden when the tool started, but not any that were already hidded when the tool was started.


## Bracing Logic
Bracing connects neighbouring supports using cross members by attempting to join the centre of the bottom of the stem of one support to the centre of the top of the stem of another.
	- If the brace would have an angle to the XY plane less than the "Minimum Brace Angle" it will not be generated.
	- If the brace would have an angle to the XY plane greater than the "Maximum Brace Angle" then instead of being joined to the top of the neighbouring stem, it will join where the angle formed is the "Maximum Brace Angle".
	- The brace diameter is set by the "Diameter" numeric control.
	- A support can be joined by braces to no more than three other supports.


## Butressing Logic
Buttressing a pillar adds one supporting pillar whose tip or head joins to the top of the stem of the support being buttressed.
	- Buttresses are added in the opposite side/direction that the head of the original support points.
	- The added buttress support base will be placed at the distance given by "Buttress spacing" from the base of the original support.
	- The butress is also joined to the original support using bracing.


# Notes
Bracing and buttressing are a modifer to the support layer:
- The modifier is added underneath the support layer in the Layer Panel and is named "Bracing and Buttressing".
- The modifier should remember the last settings used in the options panel.
- The modifier row in the layer panel has an edit button like other layers. Clicking this button will display the options panel with the last used settings and open the relevant tool for continued editing.
- The modifier does not need to regenerate bracing or butressing when the support layer is regenerated,instead all modifiers under the support layer will be deleted and their changes "lost". A warning dialog should be displayed if the user clicks on the edit button of a support layer in the layer panel allowing the user to cancel editing so the modifiers are not deleted.
- If an earlier modifier is deleted, then all modifiers underneath it (in the order of creation) are also deleted. A warning dialog should be displayed if the user clicks on the delete button of a modifier in the layer panel allowing the user to cancel deleting.
- If the modifier is selected and the delete button in the layer panel is clicked, then the modifier is deleted and all modifiers underneath it (in the order of creation) are also deleted. A warning dialog should be displayed allowing the user to cancel deleting.
- This tool should work as described in "Support Editing Mode Behaviours.md".