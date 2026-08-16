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
  Either a truncated cone rising from the build plate or a tapered, penetrating tip attached to an upward-facing model surface.
- Stem
  The main body between base and head.
- Branch
  An optional offset section that moves the stem away from the model before the head approaches the contact point.
- Head
  The angled or vertical tip section that attaches to the model.

`SupportMeshBuilder` converts a `SupportEntity` plus the configured side count into triangle geometry. The builder should tolerate short supports by clamping output sensibly instead of rejecting the entire support when possible.

Each support entity records whether its base contact belongs to the build plate or the model. Model contacts also store an upward base direction, which follows the supporting face normal until the preset's maximum angle from vertical is reached. The model-base height, penetration depth, and bottom diameter are independent from the build-plate base dimensions.

Point, Line, Ring, Contour, and Area tools expose a base-generation policy. `BuildPlateOnly` preserves the original grounded behavior, `ModelOnly` requires an upward-facing model contact, and the two fallback policies exhaust the preferred attachment type before trying the other type. Generated tool settings retain this policy so previews, edits, saves, loads, and model-transform regeneration use the same placement rules.

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
- Area supports

Each generator should store compact settings and use a shared regeneration path where practical.

## Feature-Specific Notes

### Point Supports

Point supports are ordinary support entities. During model transform regeneration, each support tip acts as the model-relative anchor and the support is rebuilt from transformed tip data plus the original profile. The existing entity's base attachment kind determines which exclusive placement policy is used during regeneration.

### Ring Supports

Ring support groups should store circumference points, spacing, the selected surface-targeting policy, and any face set required by that policy. Regeneration should transform the stored anchor points, rebuild the circle, project new guide points using that policy, and regenerate supports. Lowest Reachable ranks valid vertical intersections from the build plate rather than from the drawn ring height. Selected Faces Only targeting uses the accepted Ring Support face set, or the toolbar's last accepted face set when the operation has no local selection, and Apply prompts the user when no valid faces are available.

### Line Supports

Line support groups should store polyline points, spacing, bend-placement behavior, the selected surface-targeting policy, and any face set required by that policy. Regeneration should transform the stored polyline, redistribute guide points, reproject onto the model using that policy, and regenerate concrete supports. Lowest Reachable ranks valid vertical intersections from the build plate, while Nearest to Line ranks targets from each drawn 3D guide point.

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
