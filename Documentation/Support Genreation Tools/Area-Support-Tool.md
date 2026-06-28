# Area Support Tool

This document captures the intended shape of the future Area Support tool so it can be implemented consistently with the rest of the support system.

## Purpose

Area Support should create a support group from a user-defined surface region rather than from isolated points or a single curve. It should remain a generator-driven feature, not a manually edited mesh dump.

## Intended Workflow

1. Select the owning model.
2. Enter the support workflow and activate Area Support.
3. Define the target region using the chosen selection workflow, likely face-based selection or a projected drawing workflow.
4. Preview candidate support positions inside that region.
5. Adjust parameters such as spacing, margins, offsets, or density controls.
6. Apply the result to create or update a generated support layer group.

## Architectural Expectations

- Store a compact region definition and generator settings on the support group.
- Keep region selection identities independent of rendering objects.
- Regenerate concrete supports from the saved region definition whenever the source settings or model transform changes.
- Reuse the shared support regeneration path where possible.

## Open Design Questions

- whether the source region should be defined by face selection, contours, projected sketches, or more than one strategy
- how spacing, edge offset, and hole avoidance should behave
- whether the first version should preserve a simple uniform distribution before adding more advanced packing logic

## Recommendation

When this tool is implemented, it should produce a focused feature document under `Documentation/Features/` plus any stable support-specific rules needed here.
