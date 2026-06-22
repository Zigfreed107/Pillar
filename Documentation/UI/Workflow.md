# UI Workflow

This document summarizes the main user workflow through Pillar's application shell and floating viewport panels.

## Primary Workflow

1. Import one or more models.
2. Select the model layer to work on.
3. Choose a workflow mode such as transform or supports.
4. Activate a tool or operation within that mode.
5. Use the viewport and tool options to preview changes.
6. Commit the change through an explicit command such as Apply or Finish.
7. Continue editing, organizing layers, or exporting the result.

## Workflow Principles

- The user should always understand which model or support group is active.
- Mode selection should answer "what am I doing now?"
- Tool options should answer "what parameters affect the current tool?"
- Properties should answer "what is selected?"
- Layer controls should answer "what exists in the document?"

## Main Workflow Areas

### File And Document Flow

The File menu and import actions create, open, save, and import document content. These actions are document-level workflows rather than viewport tool workflows.

### Layer Selection Flow

The Layer Panel is the user's structural view of imported models and support groups. It decides which model or support group is active for many subsequent tool workflows.

### Mode And Tool Flow

The Mode Panel lets the user switch between broad workflows such as transform and support creation. Inside support workflows, the selected operation defines what viewport clicks will do next.

### Tool Options Flow

The Tool Options Panel appears when the active tool exposes settings that matter to the current interaction. It should stay focused on the active tool only.

### Properties Flow

The Properties Panel is for the currently selected entity, not for active tool parameters.

## Expected User Experience

- Importing or selecting a model should make the next relevant workflows discoverable.
- Tool panels should avoid directly mutating the document until the user commits.
- Preview feedback should appear in the viewport as early as possible.
- Canceling a tool should leave the document unchanged.

## Related Documents

- `Documentation/UI/Panels.md`
- `Documentation/Architecture/Modes-And-Tools.md`
- `Documentation/Supports/Overview.md`
