# Support Functionality

This document summarizes how model-owned support groups are stored and regenerated in Graphite.

## Architectural Rules

Support data lives in the document layer and rendering only displays the current document state. A support tool should store enough domain data to regenerate its supports later, but it should not store Helix, WPF, or viewport objects.

Support entities are concrete generated geometry inputs: each `SupportEntity` stores a tip position, a build-plane base position, and a `SupportProfile`. When a model transform changes, the support entities are not scaled or rotated as mesh objects. Instead, they are removed and regenerated with new world-space tip and base positions.

`SupportLayerGroup` owns the relationship between an imported model and a group of supports. Ring supports also store `RingSupportSettings`, which are the three circumference points and spacing needed to regenerate the group.

## Support Model

`SupportProfile` describes the reusable dimensions for one support. It is intentionally renderer-agnostic and is cloned when it crosses ownership boundaries.

The current support model has three conceptual sections:

- Base: a truncated cone from the build plate upward. It stores `BaseBottomRadius` and `BaseHeight`. Its top radius is derived from the next section so the base connects cleanly without a visible seam.
- Stem: a cone between the base and the head. It stores `StemBottomDiameter` and `StemTopDiameter`. If the remaining distance after base and head placement is zero or negative, no stem mesh is emitted, but the support still keeps stem settings in its profile.
- Head: a truncated cone that attaches to the model. It stores `HeadHeight`, `HeadPenetrationDepth`, and `HeadTopDiameter`. Its bottom diameter is always derived from `StemTopDiameter`, so the stem and head meet without a diameter mismatch. `HeadTopDiameter` is measured at the model intersection point. The penetration section continues past the intersection into the model using that intersection diameter.

`SupportMeshBuilder` converts a `SupportEntity` plus a support side count into triangle geometry. The builder clamps too-short supports so the base and head fit inside the available axis length instead of rejecting the entire support. This keeps the domain entity valid even when a short regenerated support cannot express every requested section at full height.

The side count comes from the application `SupportSides` setting. `MainWindow` passes it into `SceneManager`, and the rendering layer passes it to `SupportMeshBuilder`. This keeps UI settings out of domain and geometry code.

## Support Presets

Support presets are stored by `SupportPresetService` in the UI layer. Each preset has a name and a `SupportProfile`. The service loads and saves presets as JSON in the user setting `SupportPresetsJson`, which keeps reusable user preferences outside individual project files.

The compact `SupportPresetPanel` appears underneath the Tool Options Panel when Point Support or Ring Support is active. Its combo box selects the active preset used for newly created or edited supports. Its placeholder rectangle reserves space for a future support diagram. The Advanced button opens `SupportPresetEditorWindow`.

`SupportPresetEditorWindow` is a floating WPF window. It lets the user select, create, or overwrite presets. Numeric fields are displayed with one decimal place for the base, stem, and head dimensions. Saving updates `SupportPresetService`, persists user settings, and selects the saved preset so the compact panel and support tools use it immediately.

Support creation tools do not read WPF controls directly. `ManualSupportTool` receives a `Func<SupportProfile>` from `MainWindow`, and point/ring operations call it when they create supports. This keeps support tools testable and prevents UI state from leaking into geometry logic.

## Model Transform Regeneration

Model transform edits are applied through `SetMeshUserTransformCommand`. Before the command executes, `SupportGroupTransformRegenerator.CreateRegenerations` builds replacement snapshots for every support group owned by the transformed model.

For each stored support anchor, the code:

1. Converts the current world-space point into model-local space using the old world transform.
2. Converts that model-local point back into world space using the new world transform.
3. Regenerates support entities from the transformed positions.

This keeps supports attached to the same logical place on the model while avoiding the CAD mistake of scaling or rotating the support meshes themselves.

Undo and redo use the same command snapshot, so the model transform, generated support entities, and ring generator settings move together as one atomic edit.

## Point Supports

Point supports are represented by ordinary support entities in a support layer group. During model transform regeneration, each support tip is treated as the model-relative anchor.

The regenerated support uses:

- The transformed tip position.
- A new base position directly below the tip on the build plane at `Z = 0`.
- The original support profile, so support thickness and tip dimensions do not scale with the model.

If a transformed tip would create an invalid support, for example because it falls below the build plane, that regenerated support is skipped and the group remains valid.

## Ring Supports

Ring support groups store `RingSupportSettings`: three circumference points and the requested spacing.

During model transform regeneration:

1. The three stored ring points are transformed through the old-to-new model transform path.
2. The second and third points are flattened onto the horizontal plane defined by the transformed first point.
3. A new circle is calculated from those horizontal points.
4. Guide points are distributed around the circle using the original spacing.
5. Each guide point is projected vertically in the Z direction onto the transformed model.
6. Concrete support entities are regenerated at the projected hits.

This means the ring follows the model's translated and scaled location, but it remains coplanar with the XY plane. After rotations, the supports may not hit the exact same surface triangles as before, because ring supports are intentionally reprojected vertically onto the transformed mesh.

## Extension Notes

Future support tools should follow the same pattern:

- Store compact generator metadata in `SupportLayerGroup` or a related domain type.
- Regenerate concrete `SupportEntity` children from that metadata when the model changes.
- Keep rendering out of support generation code.
- Preserve physical support dimensions unless the user explicitly edits support settings.
- Put all transform-related support updates inside the same undoable command as the model transform.
