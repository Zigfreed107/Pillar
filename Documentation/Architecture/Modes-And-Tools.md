# Modes And Tools

This document defines the naming and responsibility boundaries between modes, tools, operations, and commands.

## Hierarchy

The intended interaction hierarchy is:

Mode
  -> Tool
    -> Operation
      -> Command

Each level solves a different problem and should stay narrow in scope.

## Mode

A mode is a high-level workflow area the user has entered, mostly at the shell and UI level.

Examples:

- Select
- Transform
- Manual Support
- Edit Supports

A mode decides what broad task the user is performing. It does not directly mutate the document.

## Tool

A tool is the active viewport input controller for the current mode. It receives mouse or keyboard input through `ToolManager`.

Examples:

- `SelectTool`
- `ManualSupportTool`
- future transform tools such as rotation or translation session controllers

A tool owns transient interaction state and routes input to the correct behavior.

## Operation

An operation is a selectable behavior inside a tool. This is what a mode panel or tool-specific UI typically toggles between.

Examples:

- `PointSupportOperation`
- `LineSupportOperation`
- `RingSupportOperation`

The operation is responsible for hit testing requests, preview state, click sequencing, and deciding when enough information exists to commit a change.

## Command

A command is the undoable document mutation created after an operation has enough information to commit.

Examples:

- `AddSupportCommand`
- `UpdateLineSupportGroupCommand`
- `SetMeshUserTransformCommand`

Commands should operate on durable data, not on WPF control state or transient renderer objects.

## UI Contract

The UI should respect the same boundaries:

- The Mode Panel chooses the workflow area and active operation.
- The Tool Options Panel edits parameters for the active tool or operation.
- The viewport tool owns interaction and preview state.
- The command performs the durable document edit.

The main pitfall to avoid is letting overlay buttons directly create or mutate geometry. The overlay should activate a tool or operation, not perform the final change itself.

## Manual Support Example

Manual Support follows the pattern cleanly:

- Mode: Manual Support
- Tool: `ManualSupportTool`
- Operation selector: `ManualSupportOperationKind`
- Operations: point, line, ring, and future support workflows
- Commands: one or more undoable changes that update support groups or support entities

## Naming Rules

- Use `Mode` for high-level workflow state.
- Use `Tool` for the viewport input controller.
- Use `Operation` for a sub-tool or selectable behavior inside a tool.
- Use `Command` for the undoable document change.
- Use `Panel` for visible GUI areas.
- Use `Overlay` for a panel instance floating over the viewport.

## Design Guidance

- Keep tools and operations renderer-aware only where needed for hit testing and previews.
- Keep commands renderer-agnostic.
- Prefer storing durable user intent in explicit settings types.
- If a feature needs several small behaviors, add operations before inventing a whole new mode.
