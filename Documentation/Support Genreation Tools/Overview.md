# Supports Overview

This document summarizes how model-owned support groups are stored, regenerated, and edited in Pillar.

## Architectural Rules

Support data lives in the document layer and rendering only displays the current document and preview state. Support tools should store enough domain data to regenerate supports later, but they should not store Helix, WPF, or viewport objects.

Concrete `SupportEntity` instances are generated geometry inputs. A support entity stores placement and profile information needed to build support mesh geometry, while support groups store the compact feature definition that explains why those supports exist.

`SupportLayerGroup` owns the relationship between an imported model and a support group. Generated support tools should store compact settings on the group, such as line, ring, contour, or future area settings, so the group can be regenerated from user intent.

## Support Model

`SupportProfile` describes reusable dimensions for one support. It should remain renderer-agnostic and be cloned when it crosses ownership boundaries.

The current support model has four conceptual sections:

- Base
  A truncated cone rising from the build plane.
- Stem
  The main body between base and head.
- Branch
  An optional offset section that moves the stem away from the model before the head approaches the contact point.
- Head
  The angled or vertical tip section that attaches to the model.

`SupportMeshBuilder` converts a `SupportEntity` plus the configured side count into triangle geometry. The builder should tolerate short supports by clamping output sensibly instead of rejecting the entire support when possible.

## Support Presets

Support presets should remain a UI-layer concern for reusable user preferences. Support creation tools should request a `SupportProfile` through a clean callback or service boundary rather than reading WPF controls directly.

## Regeneration Principle

When the owning model transform or support generator settings change, Pillar should regenerate supports from saved feature definitions rather than scaling or rotating support meshes directly.

The preferred regeneration flow is:

1. Read the stored generator definition.
2. Transform or update the generator input as needed.
3. Rebuild concrete support entities.
4. Replace the group's generated support output atomically.

This keeps supports attached to the same logical place on the model while preserving physical dimensions.

## Supported Generator Types

The support system currently centers around these generator styles:

- Point supports
- Line supports
- Ring supports
- Contour supports
- future area and editing workflows

Each generator should store compact settings and use a shared regeneration path where practical.

## Feature-Specific Notes

### Point Supports

Point supports are ordinary support entities. During model transform regeneration, each support tip acts as the model-relative anchor and the support is rebuilt from transformed tip data plus the original profile.

### Ring Supports

Ring support groups should store ring settings such as circumference points and spacing. Regeneration should transform the stored anchor points, rebuild the circle, project new guide points, and regenerate supports.

### Line Supports

Line support groups should store polyline points, spacing, and bend-placement behavior. Regeneration should transform the stored polyline, redistribute guide points, reproject onto the model, and regenerate concrete supports.

### Contour Supports

Contour support groups should store the feature definition needed to reslice the selected face patch and redistribute supports along the resulting contour.

## Extension Notes

Future support tools should follow the same pattern:

- store compact generator metadata
- regenerate concrete support entities from that metadata
- keep rendering out of support generation code
- preserve physical support dimensions unless the user explicitly edits them
- keep transform-related support updates inside the same undoable command as the model transform

## Related Documents

- `Documentation/Supports/Editing-Mode-Behaviours.md`
- `Documentation/Supports/Line-Support-Tool.md`
- `Documentation/Supports/Area-Support-Tool.md`
- `Documentation/Supports/Contour-Support-Tool.md`
- `Documentation/Supports/Tool-Template.md`
