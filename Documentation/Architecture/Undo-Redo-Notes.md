# Undo Redo Notes

This document captures the intended undo and redo philosophy for Pillar.

## General Rule

Undo and redo should operate on meaningful document edits, not on every tiny transient preview change.

## Preferred Command Shape

Commands should commit atomic, user-meaningful changes such as:

- import or remove a model
- create, update, or delete a support group
- change a model transform
- apply a persistent support post-process operation
- rename or reorganize user-visible items

If the user perceives one tool session as one action, the default should be one command.

## Preview Sessions

Preview edits during an active tool session usually should not create commands for each intermediate value.

Examples:

- typing rotation values before pressing Finish
- dragging temporary line support handles before pressing Apply
- adjusting support spacing in a preview-only phase

These changes should stay transient until accepted.

## Regeneration Rule

When a committed edit causes support regeneration, the source change and regenerated output should live inside the same undoable command. Undo should restore both the durable settings and the resulting support entities together.

## Persistent Versus Transient Support Edits

- Whole-layer persistent operations should be saved as durable definitions and participate in undo and redo.
- Session-local or generated-entity-only edits can be transient if regeneration is allowed to discard them.

If a transient edit is user-visible but not durable across regeneration, the UI should make that clear.

## Future Direction

As more CAD-style features are added, favor commands that capture intent-rich state transitions instead of low-level mesh changes. This keeps save/load, regeneration, and history behavior aligned.
