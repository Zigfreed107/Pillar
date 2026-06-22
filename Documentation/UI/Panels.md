# Panels

This document maps the human-facing UI panels to their responsibilities in the Pillar shell.

## Main Window

The main window hosts the application shell: menu, toolbar, viewport, floating overlays, properties area, and status bar.

## File Menu

Purpose:
Document-level actions such as new, open, save, and import.

Rule:
Keep file and project actions here rather than burying them inside tool workflows.

## Main Toolbar

Purpose:
Application-level commands that are not specific to one viewport tool, such as undo, redo, and temporary development launchers.

Rule:
Do not overload this area with tool-specific options that belong in the Mode Panel or Tool Options Panel.

## Layer Panel

Purpose:
Shows the user-visible document structure. Imported models are top-level layers and support groups sit beneath their owning model.

Responsibilities:

- select the active layer context
- create or remove support groups
- manage visibility and organization
- surface the structure of the current document

## Mode Panel

Purpose:
Lets the user choose the current workflow area and active tool or operation.

Responsibilities:

- expose transform and support workflows
- guide the user toward the next action
- avoid becoming a dumping ground for detailed settings

## Tool Options Panel

Purpose:
Shows settings for the active tool or operation.

Responsibilities:

- expose tool parameters such as spacing or offsets
- support Apply, Finish, Close, or similar tool actions where appropriate
- remain hidden or compact when no active tool needs it

## Properties Panel

Purpose:
Displays and edits properties for the currently selected document entity.

Rule:
This panel is for selected object data, not active tool configuration.

## Status Bar

Purpose:
Shows short status messages, errors, and workflow hints.

Rule:
Use it for lightweight feedback, not for storing important state the user must reference later.

## Viewport Overlay Canvas

Purpose:
Hosts screen-space overlay visuals such as selection rectangles and future helper graphics.

Rule:
These overlays are transient visuals and should not become durable document state.

## Panel Relationship Rules

- Layer Panel organizes the document tree.
- Mode Panel chooses what the user wants to do.
- Tool Options Panel edits the active tool.
- Properties Panel edits the selected entity.

Keeping those roles clear will prevent the shell from becoming confusing as more tools are added.
