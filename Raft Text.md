# Raft Text
The Raft Text tool allows the user to add text onto a raft.

## Raft Text on the Layer Panel
Raft Text is represented in the Layer Panel as a text layer row that is a child of a raft layer. Raft Text cannot be added if a raft is not already added to the model. The Raft Text layers sit at the same child level as support layers.

Raft Text layers:
- have a visibility button before the layer name. The functionality is the same as for support or raft layers, with option to make the Raft Text in the viewer hidden or visible.
- have a colour swatch that can be clicked and edited, with the chosen colour also being the colour the Raft Text entity is rendered in the viewer. This is the same functionality as for support or raft layers.
- have a name, called "Text [raft text]" where [raft text] is the text that is the actual text displayed by the raft text in the viewer. The layer name will need to be updated whenever the user changes this text in the tool's option panel and they press **Close** in the panel.
- have an edit button that re-opens the Raft Text tool to edit the text. This displayes the tool's option panel with the parameters used to create the text's current state ready for editing.

Multiple Raft Text entities/layers can be added to the same raft.

## Accessing the Raft Text tool
The  tool is accessed by navigating to the "Raft" tab on the Mode Panel, and clicking the "Raft Text" button. 

If no model is selected (or no support layers belonging to a model are selected) AND that model has no raft, then the **Raft Text** button should:
- be disabled
- have its subtitle text changed to "Select a model with a raft."

If a model is selected or support layers belonging to a model are selected AND that model has a raft, then the **Raft Text** button should:
- be enabled
- have its subtitle text changed to "Add a text to a raft."

If the user clicks on the enabled **Raft Text** button, then launch the tool and display the tool's Option Panel.

## GUI - Raft Text Tool's Option Panel
The Raft Text Tool's Option Panel should have the following controls in order:
- A **Text Entry Box** that allows the user to enter the text they wish displayed on the raft text.
- A combo box to choose the **Font**. The combo box displays all fonts available on the users computer. 
- A numeric input for **Font size** that determines the size of the characters in mm.
- A numeric input for **Text Height** (default: 1mm, minimum 0.1mm)
- A **Place** Button
- A **Close** Button

## Tool Workflow
After the user starts the tool, whether from the Mode Panel or clicking the edit button on an existing Raft Text in the Layer Panel, the Option Panel is displayed.

1. The user can enter or change any of the parameters in the panel.
2. The user clicks the Place button.
3. The options panel hides all controls and now displays instructions for the user "Move the mouse in the viewer to choose the text's location. Click to place the text"
4. A Raft Text entity is generated in the viewer.
	1. The Raft Text is always restricted to being within the raft.
	1. As the user moves the mouse in the viewer, the Raft Text is always displayed on the part of the raft that is under to the mouse cursor. The Raft Text slides around the raft dynamically as the mouse moves. The Raft Text is transparent to indicate to the user it is not in its final position yet"
	1. When the user clicks the left mouse button, the Raft Text is "locked" to the location it was in when they cicked. It is now drawn fully opaque. Moving the mouse no longer moves the Raft Text.
5. The options panel's controls are shown again, and the instructions are hidden.
6. The user is free to edit the parameters once more, and the Raft Text's geometry and text is updated.
7. The user can click the **Place** button again. The Raft Text would then be "unlocked" from its location, and the behaviour described in points 3 and 4 above returns.
8. Clicking the Close button closes the tool and updates the Layer name as described above.


## Raft Text Entity Descriptiuon
This section details what a Raft Text looks like and how it should be constructed as a 3D entity in the viewer.

The Raft Text is extruded 3D text that sits on top of the raft. 

- The text is extruded up from the top of the raft by the **Text Height**.
- The text is also extruded down into the raft by half the **Text Height**. This ensures there is not a small gap between the top of the raft and bottom of the text.
- The text is drawn using the selected **Font** at the selected **Font Size**. The font size will need to relate to the actual mm size the font will be drawn (assuming the viewers 1 unit = 1mm).
- The text itself is the contents of the **Text Entry Box**
- The text is always placed so that it is at least 1mm away from the outer edge.

# Notes
- If when editing a file, a the font used is no longer available, default back to Arial.
- Use the same logic you used when creating the Raft Tag tool for generating text.
- For now, allow the extruded text entites to overlap the raft entity, avoid searching for mesh boolean or csg libraries to union them together. If Helix or other libraries you are already using have the ability to boolean mesh entities together, discuss this before coding so I can change this directive.