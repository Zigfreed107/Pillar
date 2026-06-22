# Support Editing Mode Behaviours

This document defines how support editing operations such as clustering, bracing, and deletion should fit into the support-layer architecture. It is an implementation contract for future Edit Supports tools.

## Core Model

A generated support layer must retain two separate kinds of data:

1. **Source generator definition:** The original Ring, Line, Contour, Area, Point, or future generator settings that define the unedited support positions.
2. **Post-process operations:** An ordered collection of stored operations that modify the generated supports, such as automatic clustering or automatic bracing.

The concrete `SupportEntity` children are derived output, not the authoritative definition of the layer. They may be replaced whenever the source generator or a persistent post-process operation changes.

The regeneration pipeline should be:

1. Generate the raw supports from the source generator definition.
2. Apply each persistent whole-layer post-process operation in a deterministic order.
3. Apply any transient sub-selection edits to the current generated result.
4. Replace the layer's concrete support entities with the final result.

Rendering should continue to consume only the final concrete entities and must not contain generator or post-processing logic.

## Persistent Whole-Layer Operations

Automatic operations applied to the whole layer are persistent. Examples include clustering every compatible support in the layer or automatically bracing the layer.

Each persistent operation must store its operation type, enabled state, parameters, and ordering in the project document. It must store the user's intent rather than cached geometry. When source generator parameters change, the application regenerates the raw supports and reapplies the stored operations using their saved parameters.

The resulting clusters or braces may differ after regeneration because the source support count and positions may have changed. This is expected: persistence guarantees that the operation is reapplied, not that identical edited geometry is preserved. An operation that cannot produce a valid result should leave the affected supports unmodified and report a warning or status; it must not corrupt or invalidate the source layer.

## Transient Sub-Selection Operations

Operations applied only to selected supports are non-persistent. They affect the current concrete support entities and remain available to undo and redo during the current document state, but they are discarded when the layer is regenerated from its source definition.

This rule applies to manually clustering selected supports, manually adding braces between selected supports, and deleting individual selected supports. The UI should clearly identify these edits as temporary across regeneration and warn before a generator change discards them.

The first implementation should not attempt to rematch selected supports after regeneration. Generated entities currently have no stable source identity, and proximity matching would create unpredictable edits. Stable anchor identities could support persistent sub-selection edits in a later design, but are not required by this architecture.

## Editing and Regeneration Behaviour

Editing Ring, Line, Contour, Area, or other source parameters must remain possible after post-process operations are added. Editing the source definition triggers full regeneration of the layer, followed by reapplication of all enabled persistent operations in their stored order. Transient sub-selection edits are removed.

Changing a persistent operation's settings should also regenerate the layer from the source definition before reapplying the complete operation sequence. Operations should not repeatedly modify already processed geometry, because doing so would make results depend on edit history.

Disabling or deleting a persistent operation regenerates the layer without that operation. Reordering operations regenerates the layer using the new order. Generator edits and operation edits should be represented by commands that atomically preserve the source settings, operation list, and resulting entities for undo and redo.

## Domain and Persistence Requirements

`SupportLayerGroup` should retain its existing generator kind and generator settings and gain an ordered, renderer-independent collection of post-process operation definitions. Operation definitions should be explicit domain types with versionable serialized data; they must not reference WPF, HelixToolkit, SharpDX, viewport selections, or generated mesh objects.

A shared regeneration service should own the complete generator-plus-post-process pipeline. Support creation tools, generator-edit commands, model-transform regeneration, project loading, and post-process commands should all use this service so they cannot produce different results from the same stored layer definition.

Project serialization must save the source generator definition and persistent operation definitions. Concrete support entities may continue to be serialized for compatibility or fast loading, but they remain rebuildable derived data. Loading should validate operation versions and parameters; unsupported operations should be preserved where possible, skipped safely, and surfaced to the user.

## Edit Supports UI Contract

The Support Mode may expose an **Edit Supports** tab containing tools such as **Cluster Supports**, **Brace Supports**, and **Delete Supports**. A tool operates on the selected support layer and either the whole layer or the current viewport selection.

- **Whole layer:** Store or update a persistent post-process operation and regenerate the layer.
- **Selected supports:** Apply a transient command to the current generated entities and disclose that regeneration will discard it.

The layer panel should distinguish layers with active persistent operations and allow those operations to be edited, enabled, reordered, or removed without losing the original generator definition.

## Initial Scope

The initial architecture should support persistent whole-layer automatic clustering and bracing, plus transient selected-support clustering, bracing, and deletion. Persistent editing of arbitrary selected supports is explicitly deferred until generated supports have stable identities and a deterministic rematching policy.
