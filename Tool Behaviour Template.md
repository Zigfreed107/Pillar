# Pattern For Future Tools

Support Tool should follow the same pattern:

1. Add a settings class, for example LineSupportSettings, in Pillar.Core.
2. Add a generator kind, for example SupportGroupGeneratorKind.LineSupport.
3. Store the tool’s feature definition on SupportLayerGroup.
4. Create a rendering operation, for example LineSupportOperation, to handle clicks, preview, Apply, Cancel, and editing.
5. Create an undoable update command, for example UpdateLineSupportGroupCommand.
6. Persist the settings in GphDocumentSerializer.
7. Update the Layer Panel selection flow so selecting a generated line group reopens the correct Tool Options UI.
8. Make Apply update the existing group when editing, or create a new group when creating.
9. Clear previews and exit the tool after successful Apply.

The main rule is: generated supports are output, not the source of truth. The source of truth is the tool settings saved on the support group.
