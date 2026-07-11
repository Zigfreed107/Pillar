# Support Editing Mode Behaviours

This document defines how support editing operations such as clustering, bracing, and deletion fit into the support-layer modifier-stack architecture.

## Core Model

A generated support layer retains three separate kinds of data:

1. Source generator definition
   The original point, line, ring, contour, area, or future generator settings.
2. Modifier stack
   An ordered collection of stored operations that transform generated supports.
3. Generated output
   The final concrete `SupportEntity` instances produced by the generator and modifier stack.

The source generator and modifier stack are the authoritative definition of the support layer. Concrete support entities are derived output and may be replaced whenever the generator or a modifier changes.

Every modifier is revision-bound and targets explicit support identities. A modifier stores:

- stable tool-launch session identity
- one or more ordered internal actions created by that tool session
- enabled state
- tool parameters
- ordering
- target support identities, or ordered target batches for cumulative tools
- source generator revision

Modifier definitions are document operations owned by their support layer. They are not independent support layers, rendered geometry, or Helix scene objects.

## Modifier Lifetimes

Modifiers are saved, editable, undoable, and retained across project save and load only while their captured source generator revision and target support identities remain valid.

A modifier lifetime begins when its Support Editing tool is launched from the Mode Panel and ends when that tool is closed. Repeated actions during that lifetime update the same modifier session. Opening an existing modifier from the Layer Panel resumes that modifier's saved session.

Examples include:

- clustering targeted supports
- adding braces to targeted supports
- deleting targeted generated supports

When source-support regeneration changes the generator revision, all existing modifiers for that support layer are discarded. Regeneration should report which modifiers were removed. The generator change, regenerated supports, and modifier removal must form one undoable command so Undo restores the complete previous state.

## Evaluation Pipeline

The preferred pipeline is:

1. Generate raw supports from the source generator definition.
2. Apply each enabled modifier in stored order against its captured target IDs.
3. Replace the layer's concrete support entities with the final result.

Rendering should consume only the final concrete entities and must not contain generator or modifier logic.

Modifier order initially follows creation order. Editing, removing, resetting, or reordering a modifier should rebuild the result from the source generator and reapply all remaining enabled modifiers in order. Modifiers must not repeatedly alter already processed geometry in place, because that would make the result depend on edit history.

If an earlier topology-changing modifier is edited or removed, later modifiers may lose valid targets. Any invalid downstream modifiers should be discarded with a user notice as part of the same undoable command.

## Regeneration Behaviour

Editing point, line, ring, contour, area, or other source parameters triggers full regeneration of the support layer.

Regeneration should:

1. Generate new raw supports and advance the source generator revision.
2. Discard all existing modifiers tied to the previous revision, with a user notice.
3. Replace the concrete support entities atomically.

This intentionally favors predictable behavior over attempting ambiguous geometric matching between old and newly generated supports.

Before opening a support generator editor for a layer that already has modifiers, the UI must warn that editing the support layer will delete all modifiers below it and offer Cancel.

## Layer Panel Contract

The Layer Panel should display the modifier stack as child rows beneath its owning support layer. These rows expose document structure but remain modifier representations rather than true layers.

Example:

```text
Area Supports
|-- Cluster (12)
`-- Brace (8)
```

A modifier stack entry in the layer panel should have an edit button to the right of the label.

Each launch of a Support Editing tool creates one modifier row. Every Apply, Selected, All, remove, or related action performed before that tool is closed belongs to the same row, including mixed Brace and Buttress actions. Closing the tool and launching it again from the Mode Panel starts a new modifier row. Editing a row from the Layer Panel reopens that saved tool session instead of creating a new one. Internal action records may retain different parameter snapshots while remaining owned, edited, persisted, and removed as one modifier session.

Selecting the edit action for a modifier should:

1. Activate Edit Supports mode if it is not already active.
2. Activate the editing tool that created the modifier.
3. Open that tool's options panel.
4. Restore the modifier's saved parameters and captured targets.

The parent support layer should continue to provide access to its original generator settings separately from modifier editing.

Before opening a modifier editor when later modifiers exist underneath it, the UI must warn that editing this modifier will delete all modifiers underneath it and offer Cancel. Applying the edited modifier should remove those downstream modifiers as part of the same undoable command.

## Reset And Removal

Every modifier options panel should provide a `Reset` action. Reset removes the selected modifier and rebuilds the support layer from its source generator plus the remaining modifier stack. For clustering, this returns affected supports to the individual state they would have after preceding modifiers.

The parent support layer should provide a separate `Reset All Edits` action. This removes every modifier from that support layer and rebuilds the unedited generated supports.

Reset, Reset All Edits, modifier edits, and modifier removal should all be undoable. If all modifiers are removed, all modifier child rows should disappear automatically.

If a modifier is removed using the remove button in the layer panel, that modifier is removed and the support layer rebuilds from the source generator plus any remaining modifiers.

## Persistence Requirements

`SupportLayerGroup` retains generator settings and an ordered, renderer-independent collection of modifier definitions. These definitions must not reference WPF, HelixToolkit, SharpDX, viewport selections, generated meshes, or tool-control instances.

Project persistence saves revision-bound modifiers with their target identities or target batches and source generator revision. Target identities and source revisions must be validated when loading. Invalid modifiers should be discarded rather than applied to unrelated supports.
