# Tag Tool
The tag tool allows to add a protrusion to a raft so that rasied text can be displayed. This tag allows users to provide identification information to printed models.

## Tags on the Layer Panel
Tags are represented in the Layer Panel as a tag layer row that is a child of a raft layer. Tags cannot be added if a raft is not already added to the model. The tag layers sit at the same child level as support layers.

- Tag layers have a visibility button before the layer name. The functionality is the same as for support or raft layers, with option to make the tag in the viewer hidden or visible.
- Tag layers have a colour swatch that can be clicked and edited, with the chosen colour also being the colour the tag entity is rendered in the viewer. This is the same functionality as for support or raft layers.
- Tag layers have a name, called Tag [tag text] where [tag text] is the text that is displayed on the tag in the viewer. The layer name will need to be updated whenever the user changes this text in the tool's option panel and they press **Apply** in the panel.
- Tag layers have an edit button that re-opens the tag tool to edit the tag. This displayes the tool's option panel with the parameters used to create the tag's current state ready for editing.

Multiple tags can be added to the same raft.

## Accessing the tag tool
The tag tool is accessed by navigating to the "Raft" tab on the Mode Panel, and clicking the "Add Tag" button. 

If no model is selected (or no support layers belonging to a model are selected) AND that model has no raft, then the **Add Tag** button should:
- be disabled
- have its subtitle text changed to "Select a model with a raft."

If a model is selected or support layers belonging to a model are selected AND that model has a raft, then the **Add Tag** button should:
- be enabled
- have its subtitle text changed to "Add a tag to a raft."

If the user clicks on the enabled **Add Tag** button, then launch the tool and display the tool's Option Panel.

## GUI - Tag Tool's Option Panel
The Tag Tool's Option Panel should have the following controls in order:
- A numeric input for **Tag Height** (default: 0.7mm)
- A numeric input for **Edge Angle** (default: 45 degrees, minimum: 30 degrees, maximum: 90 degrees)
- A numeric input for **Border Offset** (default: 1.0mm, minimum: 0.0mm)
- A **text entry box** that allows the user to enter the text they wish displayed on the tag.
- A combo box to choose the **Font**. The combo box displays all fonts available on the users computer. 
- A numeric input for **Font size** that determines the size of the characters in mm.
- A numeric input for **Text Height** (default: 1mm, minimum 0.1mm)
- A **Place** Button
- A **Close** Button

## Tool Workflow
After the user starts the tool, whether from the Mode Panel or clicking the edit button on an existing tag in the Layer Panel, the Option Panel is displayed.

1. The user can enter or change any of the parameters in the panel.
2. The user clicks the Place button.
3. The options panel hides all controls and now displays instructions for the user "Move the mouse in the viewer to choose the tag's location. Click to place the tag"
4. A tag entity is generated in the viewer.
	1. The tag is always attached to the edge of the raft.
	1. As the user moves the mouse in the viewer, the tag is always displayed on the part of the raft edge that is closest to the mouse cursor. The tag slides around the edge of the raft dynamically as the mouse moves. The tag is transparent to indicate to the user it is not in its final position yet"
	1. When the user clicks the left mouse button, the tag is "locked" to the location it was in when they cicked. It is now drawn fully opaque. Moving the mouse no longer moves the tag.
5. The options panel's controls are shown again, and the instructions are hidden.
6. The user is free to edit the parameters once more, and the tag's geometry and text is updated.
7. The user can click the **Place** button again. The tag would then be "unlocked" from its location, and the behaviour described in points 3 and 4 above returns.
8. Clicking the Close button closes the tool.

## Tag Entity Descriptiuon
This section details what a tag looks like and how it should be constructed as a 3D entity in the viewer.

- The tag is rectangular in shape in the XY plane, and is extruded up in the Z axis by the **Tag Height**.
- The tag is always flat to the XY plane.
- Since the tag is placed around the raft, and the raft might be convex, it is aligned according to the tangent of the raft at the location where it joins. This will be referred to as the "tagnetial axis" in this document.
- There are four sides of the rectangular tag:
	- The "inner edge" which is parallel to the "tangential axis" and is closer to the centre of the raft and inside the raft boudnary.
	- The "outer edge" which is parallel to the "tangential axis" and is further from the centre of the raft and outside the raft boudnary.
	- Two "side edges" which are perpendicular to the "tangential axis". The "tangential axis" crosses the mid points of both these edges.
	- In other words, the tag is a rectangle that is half inside and half outside the raft boudnary it is attached to.
- The length of the "inner edge" and "outer edge" is enough so that the full text entered in the **text entry box** plus the **Border Offset** at each end of the text.
- The length of the"side edges" is the **Font Size** plus plus the **Border Offset** above and below the text.



## Notes
- If when editing a file, a the font used is no longer available, default back to Arial.
- Look for existing tools in Helix Toolkit or free libraries that allow the creation of 3D mesh entities by extruding fonts rather than creating your own. Before creating any code, discuss options you have found. If you cannot find any, then mention you will need to create your own.
- For now, allow the extruded text entites to overlap the tag entity, avoid searching for mesh boolean or csg libraries to union them together. If Helix or other libraries you are already using have the ability to boolean mesh entities together, discuss this before coding so I can change this directive.
- To begin with, we will start by only worrying about generating the tag base itself, and not the text. Once we have placing the tag base down around the raft working, we can extend the tool to add text to the tag.