# Rendering Boundaries

This document records the intended separation between rendering, domain logic, and UI workflow code.

## Goal

Rendering should display the current document state and transient preview state. It should not become the source of truth for support definitions, transforms, or workflow decisions.

## What Belongs In The Domain

The domain and document layers should own:

- imported models and support groups
- support generator settings
- support entities and support profiles
- transform data
- selection identities that must persist
- operation definitions that need to save and reload

Domain data should stay independent of WPF, HelixToolkit, SharpDX, and viewport objects.

## What Belongs In Rendering

The rendering layer should own:

- scene graph objects
- mesh buffers and materials
- viewport overlays
- hit testing against rendered meshes
- transient preview visuals
- visual highlighting and diagnostic aids

These are implementation details for display and interaction, not durable project data.

## What Belongs In UI And Workflow Code

The UI and shell orchestration layer should own:

- panel visibility
- tool activation
- binding view models to controls
- collecting user input from controls
- converting accepted user intent into commands or tool configuration

UI code should not be responsible for geometry generation.

## Support-Specific Rule

Support tools should save compact generator definitions and regenerate concrete `SupportEntity` output when needed. They should not save Helix meshes, viewport handles, or other renderer objects as durable project state.

## Preview Rule

Preview state can exist in rendering and tool code, but it must be disposable. Canceling a tool should be able to drop previews without leaving document state behind.

## Performance Rule

Avoid allocations and scene object churn in per-frame or per-mouse-move paths. Reuse preview buffers and scene objects where practical, especially for transform guides, support markers, and selection overlays.

## Typical Flow

1. The document stores durable settings.
2. A tool reads those settings and creates transient preview inputs.
3. Rendering displays the preview.
4. The user commits.
5. A command updates the durable document state.
6. Rendering refreshes from the new document state.

If a design requires reverse-engineering user intent from rendered geometry, that is usually a sign the boundary is in the wrong place.
