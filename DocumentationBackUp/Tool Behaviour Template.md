# Pattern For Future Tools

Support tools should follow the same pattern:

1. Add a settings class, for example LineSupportSettings, in Pillar.Core.
2. Add a generator kind, for example SupportGroupGeneratorKind.LineSupport.
3. Store the tool's feature definition on SupportLayerGroup.
4. Create a rendering operation, for example LineSupportOperation, to handle clicks, preview, Apply, Cancel, editing, and support selection.
5. Create an undoable update command, for example UpdateLineSupportGroupCommand.
6. Persist the settings in GphDocumentSerializer.
7. Update the Layer Panel selection flow so selecting a generated group reopens the correct Tool Options UI.
8. Make Apply create a new group when creating, or update the existing group when editing.
9. After Apply, keep the generated group loaded in edit mode so individual supports can be selected and deleted immediately.
10. Add a Close action in the Tool Options panel that exits the tool edit mode, clears previews, restores opacity, and closes the options UI.
11. Support click selection and drag/window selection for generated supports in the active edited group. Use left-to-right selection for supports fully inside the rectangle, and right-to-left selection for supports inside or crossing the rectangle.
12. Route Delete key and Tool Options Delete button actions through one undoable support deletion command.

The main rule is: generated supports are output, not the source of truth. The source of truth is the tool settings saved on the support group.

Deleting individual generated supports is a manual edit to the current generated output. It does not change the saved generator settings. If the user clicks Apply again, the group is regenerated from the saved tool definition and deleted supports may be recreated.
