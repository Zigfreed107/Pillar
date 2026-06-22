# Architecture Overview

This folder captures the stable technical shape of Pillar so feature work does not have to rediscover the same boundaries every time.

## System Shape

Pillar is a CAD-style desktop application for preparing resin-printing supports on imported meshes. The user imports one or more models, positions them for printing, creates support features, edits the result, and exports the supported geometry.

The application is easiest to reason about as five cooperating layers:

1. Document and domain model
   Stores imported models, support groups, support entities, settings, and durable user intent.
2. Geometry and analysis
   Performs mesh analysis, projection, contour extraction, support layout, and other renderer-agnostic calculations.
3. Rendering
   Turns document state and preview state into viewport visuals, hit testing, and overlays.
4. Commands and regeneration
   Applies undoable edits and rebuilds derived support geometry when source definitions change.
5. UI and workflow orchestration
   Hosts panels, tool activation, mode switching, and user-facing settings.

## Architectural Goals

- Keep rendering and domain logic separate.
- Store user intent in compact domain definitions rather than in generated meshes.
- Rebuild derived support output deterministically from saved settings.
- Avoid hot-path allocations and unnecessary scene churn.
- Keep the system understandable for a solo developer while leaving room for more tools later.

## Core Domain Concepts

- `CadDocument` is the durable project state.
- `MeshEntity` represents an imported model layer.
- `SupportLayerGroup` represents a user-visible support group associated with a model.
- `SupportEntity` represents one generated or manually placed support.
- Generator settings such as line, ring, contour, or future area settings describe how to rebuild support groups.

Generated supports are output, not the primary source of truth. The source of truth should be the compact feature or generator definition plus any durable post-processing definitions.

## Workflow Shape

The user works in modes such as selection, transform, and support creation. A mode activates a viewport tool. Some tools expose operations inside that tool, such as point, line, or ring support creation.

The preferred interaction chain is:

1. The user selects a model or support layer.
2. The user chooses a mode and then a tool or operation.
3. The active tool owns preview state and gathers enough input for a change.
4. A command commits the durable document mutation.
5. Rendering updates from document state and transient preview state.

## Regeneration Principle

When a model transform or generator setting changes, Pillar should regenerate the derived support output from the saved feature definition rather than scaling or rotating support meshes directly. This keeps support dimensions physically meaningful and preserves CAD-style editability.

## Related Documents

- `Documentation/Architecture/Modes-And-Tools.md`
- `Documentation/Architecture/Rendering-Boundaries.md`
- `Documentation/Architecture/Undo-Redo-Notes.md`
- `Documentation/UI/Workflow.md`
- `Documentation/Supports/Overview.md`
