Ring Support Tool Summary

The Ring Support Tool behaves like a small parametric CAD feature. The support group remembers how it was created, while the individual support entities are treated as generated output. This is the key pattern to reuse for future tools.

When the user selects the Ring Support Tool in Support Mode, the Tool Options panel opens with Ring Support settings. The user clicks a first point on the selected model surface. That first point must be a real mesh hit and locks the horizontal construction plane. After that, mouse movement previews the ring while the user picks the second and third circumference points.

The second and third points can be placed either on the model or off the model. If the mouse is off the model, the tool projects the cursor onto a horizontal construction plane through the first point. The ring itself remains horizontal, parallel to the XY plane. Supports are generated only after the third point, and only where the ring guide points can vertically project back onto the selected mesh.

Once the user clicks the third point, the blue support-tip markers appear. When the user clicks Apply, the generated supports are committed into a new SupportLayerGroup or regenerated into the existing edited group. The Tool Options panel stays open, the mouse cursor returns to the normal arrow, and the group remains loaded in edit mode so the user can immediately select and delete individual generated supports.

Clicking Close in the Ring Support Options panel exits Ring Support edit mode. Close clears transient preview geometry, restores normal support opacity, deselects the Ring Support operation, and closes the Tool Options panel.

The main implementation pieces are:

RingSupportOperation.cs handles mouse input, preview generation, Apply, Cancel, editing, off-model point projection, support selection, and support drag selection.
ManualSupportTool.cs owns the currently active support operation, switches tools cleanly, exposes active edit-group state, and routes selected-support deletion.
RingSupportToolOptionsControl.xaml and RingSupportToolOptionsControl.xaml.cs expose Ring Support settings, Apply, Close, and Delete.
MainWindow.WorkspaceModes.cs connects UI events, selected layers, support mode, tool activation, Tool Options visibility, and the Ring Support Delete button.
MainWindow.Selection.cs keeps the active generated support group as the workflow context while supports are selected.
MainWindow.Commands.cs routes the Delete key through the same selected-support deletion path as the Tool Options Delete button.
SupportLayerGroup.cs stores the support group plus optional generator metadata.
RingSupportSettings.cs stores the feature definition: first point, second point, third point, and spacing.
SupportGroupGeneratorKind.cs identifies which tool generated a support group.
UpdateRingSupportGroupCommand.cs makes regenerating an existing generated group undoable.
RemoveSupportEntitiesCommand.cs makes deleting one or more individual generated supports undoable.
IEditableSupportGroupOperation.cs marks operations that are editing an existing generated support group.
GphDocumentSerializer.cs persists generator metadata into .gph files.
SceneManager.cs and SupportRenderer.cs handle support rendering, temporary edit opacity, support hit testing, and support window-selection bounds.

Layer Behaviour

When a Ring Support group is created, it is added as a support layer group. The child supports remain individual SupportEntity objects, so they can render, hide, export, be selected, be deleted, and participate in layer visibility like normal supports.

The group also stores metadata saying:

GeneratorKind = SupportGroupGeneratorKind.RingSupport
RingSupportSettings = first point, second point, third point, spacing

This is important because the app does not try to infer the original ring from the generated supports. Instead, the support group remembers the original feature definition.

That is how real CAD systems usually work: a feature owns parameters, and geometry is regenerated from those parameters.

Editing Behaviour

When the user selects a Ring-generated support group in the Layer Panel, the app recognises the generator metadata and reopens the Ring Support settings in the Tool Options panel.

The saved spacing is loaded into the panel. The saved ring points are reused as the ring definition. When the user changes spacing, the preview updates in real time using the stored ring and edited spacing.

While editing a Ring Support group, the committed supports are made 50% transparent. This lets the user see the existing generated output while comparing the live regenerated preview.

When Apply is clicked during editing, the app updates the same support group rather than creating a new one. The old generated supports are replaced with the newly generated supports, and the updated settings are saved back onto the same group. The group remains loaded in edit mode after Apply.

Undo/redo for regeneration is handled by an explicit command. The command stores the old settings, old support entities, new settings, and new support entities. Undo restores the previous metadata and supports. Redo reapplies the new metadata and supports.

Manual edits inside a Ring-generated group are intentionally overwritten when the group is regenerated.

Support Selection And Deletion Behaviour

While a Ring Support group is in edit mode, the generated support entities in the active edited group can be selected directly in the viewport.

Clicking a support selects that individual support. Shift-click adds a support to the current selection. Ctrl-click removes a support from the current selection.

Dragging a selection rectangle selects supports in the active edited group:

Left-to-right drag selects only supports fully inside the selection rectangle.
Right-to-left drag selects supports that are inside the rectangle or crossing the rectangle.

The Mode Panel and Tool Options panel stay visible while supports are selected. The selected support group remains the workflow context even when one or more child support entities are selected.

The Ring Support Options panel includes a Delete button under the spacing control. It is disabled when no supports from the active edited group are selected, and enabled when at least one is selected. Pressing the Delete key or clicking the Delete button uses the same undoable deletion path.

Deleting generated supports removes the selected SupportEntity objects from the document but does not change RingSupportSettings. If the user clicks Apply again, the group is regenerated from the ring settings, so deleted supports may be recreated.

Pattern For Future Tools

A future Line Support Tool should follow the same pattern:

Add a settings class, for example LineSupportSettings, in Pillar.Core.
Add a generator kind, for example SupportGroupGeneratorKind.LineSupport.
Store the line tool's feature definition on SupportLayerGroup.
Create a rendering operation, for example LineSupportOperation, to handle clicks, preview, Apply, Cancel, editing, support selection, and support drag selection.
Create an undoable update command, for example UpdateLineSupportGroupCommand.
Use RemoveSupportEntitiesCommand, or the same command pattern, for deleting selected generated supports.
Implement IEditableSupportGroupOperation so the shell can identify the active edited support group.
Persist the settings in GphDocumentSerializer.
Update the Layer Panel selection flow so selecting a generated line group reopens the correct Tool Options UI.
Make Apply update the existing group when editing, or create a new group when creating.
After Apply, keep the generated group loaded in edit mode so generated supports can be selected and deleted immediately.
Add a Close action to exit edit mode, close the Tool Options panel, and clear transient previews.
Keep the Mode Panel and Tool Options panel visible while supports in the active edited group are selected.

The main rule is: generated supports are output, not the source of truth. The source of truth is the tool settings saved on the support group.

Common Pitfalls

Avoid inferring tool settings from support positions. That becomes fragile once spacing, skipped projections, mesh holes, or future settings are involved.

Avoid putting tool logic directly into MainWindow. MainWindow should coordinate UI flow, but the operation class should own interaction behaviour.

Avoid coupling core layer metadata to rendering types. The core project should know about settings and support groups, not Helix or viewport previews.

Avoid regenerating committed supports during every mouse move. Use transient preview geometry while editing, then commit only on Apply.

Avoid creating a new support group when editing an existing generated group. Editing should preserve the same layer identity.

Avoid treating deleted generated supports as changes to the feature definition. Deletion is a manual output edit and can be overwritten by regeneration.

CAD Architecture Principle

The Ring Support Tool now works like a simple parametric feature:

User input -> saved feature settings -> generated support entities -> optional manual output edits -> undoable regeneration

That same model should guide future tools like line, grid, curve, array, or pattern supports.
