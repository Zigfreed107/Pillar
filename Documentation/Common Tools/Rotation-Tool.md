# Rotation Tool

This document describes the Rotate tool in terms of user workflow and architectural boundaries.

## Purpose

The Rotate tool lets the user preview and then commit model rotation without modifying imported mesh data directly.

## User Workflow

1. Select one model in the viewport or Layer Panel.
2. Open Transform mode and choose Rotate.
3. The Mode Panel yields to the rotation tool session and the tool options panel shows rotation inputs.
4. The viewport shows non-interactive visual guides around the model pivot.
5. The user enters X, Y, and Z rotation values and sees a live preview.
6. Reset returns the preview to the session baseline.
7. Finish commits one undoable transform change.
8. Cancel exits the tool and restores the starting transform.

## Tool Expectations

- Rotation preview should update the selected model only.
- Preview changes should not create commands.
- Finish should commit one atomic transform command.
- Support groups owned by the model should regenerate together with the committed transform.

## Architectural Notes

- Imported orientation remains separate from user transform data.
- Rotation preview belongs to the active tool session.
- Rendering owns the guide visuals.
- The document owns the durable transform state.

## Future Refinements

This feature can later expand with local versus world rotation options, richer pivot controls, or direct manipulator interaction while preserving the same command and preview boundaries.
