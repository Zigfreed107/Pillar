# Support Editing Mode Behaviours

This document defines how support editing operations such as clustering, bracing, and deletion should fit into the support-layer architecture.

## Core Model

A generated support layer should retain two separate kinds of data:

1. Source generator definition
   The original point, line, ring, contour, area, or future generator settings.
2. Post-process operations
   An ordered collection of stored operations that modify the generated result.

Concrete `SupportEntity` children are derived output, not the authoritative definition of the layer. They may be replaced whenever the source generator or a persistent post-process operation changes.

## Regeneration Pipeline

The preferred pipeline is:

1. Generate raw supports from the source generator definition.
2. Apply each persistent whole-layer post-process operation in a deterministic order.
3. Apply any transient sub-selection edits to the current generated result.
4. Replace the layer's concrete support entities with the final result.

Rendering should consume only the final concrete entities and must not contain generator or post-processing logic.

## Persistent Whole-Layer Operations

Whole-layer automatic operations such as clustering or bracing should be persistent. Each operation should store:

- operation type
- enabled state
- parameters
- ordering

Persistence should capture the user's intent rather than cached geometry. If the source support count or positions change, the operation is reapplied to the regenerated raw supports.

## Transient Sub-Selection Operations

Operations applied only to selected supports can remain non-persistent in the initial architecture. They affect the current generated entities and participate in undo and redo, but regeneration may discard them.

Examples:

- manually clustering selected supports
- manually adding braces between selected supports
- deleting individual generated supports

The UI should communicate clearly when an edit is temporary across regeneration.

## Editing Behaviour

Editing ring, line, contour, area, or other source parameters should trigger full regeneration of the layer and then reapply all enabled persistent operations in stored order. Operations should not repeatedly modify already processed geometry in place, because that would make results depend on edit history.

## Persistence Requirements

`SupportLayerGroup` should retain generator settings and gain an ordered, renderer-independent collection of post-process operation definitions. These definitions should be versionable domain types and must not reference WPF, HelixToolkit, SharpDX, viewport selections, or generated mesh objects.

## UI Contract

An Edit Supports tab can expose tools such as Cluster Supports, Brace Supports, and Delete Supports.

- Whole layer operations should store or update a persistent operation and regenerate the layer.
- Selected support operations can remain transient until stable support identities exist.

## Initial Scope

The initial architecture should support:

- persistent whole-layer automatic clustering
- persistent whole-layer automatic bracing
- transient selected-support clustering
- transient selected-support bracing
- transient selected-support deletion
