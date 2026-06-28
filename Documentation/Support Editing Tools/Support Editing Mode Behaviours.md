# Support Editing Mode Behaviours

This document defines how support editing operations such as clustering, bracing, and deletion should fit into the support-layer modifier-stack architecture.

## Core Model

A generated support layer should retain three separate kinds of data:

1. Source generator definition
   The original point, line, ring, contour, area, or future generator settings.
2. Modifier stack
   An ordered collection of stored operations that transform the generated supports.
3. Generated output
   The final concrete `SupportEntity` instances produced by the generator and modifier stack.

The source generator and modifier stack are the authoritative definition of the support layer. Concrete support entities are derived output and may be replaced whenever the generator or a modifier changes.

Each modifier invocation creates a separate modifier definition, even when several modifiers use the same editing tool. A modifier should store:

- stable modifier identity
- modifier or tool type
- whole-layer or selected-support scope
- enabled state
- tool parameters
- ordering
- target support identities when selection-scoped
- source generator revision when selection-scoped

Modifier definitions are document operations owned by their support layer. They are not independent support layers, rendered geometry, or Helix scene objects.

## Modifier Lifetimes

Whole-layer modifiers are replayable. Automatic operations such as clustering or bracing are reapplied to newly generated supports when source generator settings change.

Selection-scoped modifiers are revision-bound. They are saved, editable, undoable, and retained across project save and load while the source generator revision remains unchanged. Examples include:

- clustering selected supports
- adding braces to selected supports
- deleting selected generated supports

Selection-scoped modifiers are discarded when source-support regeneration changes the generator revision and invalidates their target identities. Regeneration should report which modifiers were removed. The generator change, regenerated supports, retained whole-layer modifiers, and discarded selection modifiers must form one undoable command so Undo restores the complete previous state.

## Evaluation Pipeline

The preferred pipeline is:

1. Generate raw supports from the source generator definition.
2. Apply each enabled modifier in stored order.
3. Replace the layer's concrete support entities with the final result.

Rendering should consume only the final concrete entities and must not contain generator or modifier logic.

Modifier order initially follows creation order. Editing, removing, resetting, or reordering a modifier should rebuild the result from the source generator and reapply all remaining enabled modifiers in order. Modifiers must not repeatedly alter already processed geometry in place, because that would make the result depend on edit history.

If an earlier topology-changing modifier is edited or removed, later selection-scoped modifiers may lose valid targets. Any invalid downstream modifiers should be discarded with a user notice as part of the same undoable command. Whole-layer modifiers remain eligible for reevaluation.

## Regeneration Behaviour

Editing point, line, ring, contour, area, or other source parameters triggers full regeneration of the support layer.

Regeneration should:

1. Generate new raw supports and advance the source generator revision.
2. Discard selection-scoped modifiers tied to the previous revision, with a user notice.
3. Reapply all enabled whole-layer modifiers in stored order.
4. Replace the concrete support entities atomically.

This intentionally favors predictable behavior over attempting ambiguous geometric matching between old and newly generated supports.

## Layer Panel Contract

The Layer Panel should display the modifier stack as child rows beneath its owning support layer. These rows expose document structure but remain modifier representations rather than true layers.

Example:

```text
Area Supports
|-- Cluster - Selection (12)
|-- Cluster - Whole Layer
`-- Brace - Selection (8)
```

A modifier stack entry in the layer panel should have an edit button to the right of the label.

Each invocation receives its own row so different scopes and parameter sets remain independently editable. Removing the final modifier should leave the support layer with no modifier children.

Selecting the edit action for a modifier should:

1. Activate Edit Supports mode if it is not already active.
2. Activate the editing tool that created the modifier.
3. Open that tool's options panel.
4. Restore the modifier's saved parameters and scope information.

The parent support layer should continue to provide access to its original generator settings separately from modifier editing.

## Reset And Removal

Every modifier options panel should provide a `Reset` action. Reset removes the selected modifier and rebuilds the support layer from its source generator plus the remaining modifier stack. For clustering, this returns affected supports to the individual state they would have after preceding modifiers.

The parent support layer should provide a separate `Reset All Edits` action. This removes every modifier from that support layer and rebuilds the unedited generated supports.

Reset, Reset All Edits, modifier edits, and modifier removal should all be undoable. If all modifiers are removed, all modifier child rows should disappear automatically.

If a modifier is removed using the remove button in the layer panel, all edits should be removed and the support layer should return to its unedited state.

## Persistence Requirements

`SupportLayerGroup` should retain generator settings and gain an ordered, renderer-independent collection of modifier definitions. These definitions should be versionable domain types and must not reference WPF, HelixToolkit, SharpDX, viewport selections, generated meshes, or tool-control instances.

Project persistence should save both whole-layer and revision-bound selection modifiers. Target identities and source revisions must be validated when loading. Invalid selection modifiers should not be silently applied to unrelated supports.

## Initial Scope

The initial architecture should support:

- persistent whole-layer automatic clustering
- persistent whole-layer automatic bracing
- revision-bound selected-support clustering
- revision-bound selected-support bracing
- revision-bound selected-support deletion
- one modifier row per editing invocation
- modifier editing, reset, removal, undo, redo, save, and load
