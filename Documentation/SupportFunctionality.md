# Support Functionality

This document summarizes how model-owned support groups are stored and regenerated in Graphite.

## Architectural Rules

Support data lives in the document layer and rendering only displays the current document state. A support tool should store enough domain data to regenerate its supports later, but it should not store Helix, WPF, or viewport objects.

Support entities are concrete generated geometry inputs: each `SupportEntity` stores a tip position, a build-plane base position, and a `SupportProfile`. When a model transform changes, the support entities are not scaled or rotated as mesh objects. Instead, they are removed and regenerated with new world-space tip and base positions.

`SupportLayerGroup` owns the relationship between an imported model and a group of supports. Ring supports also store `RingSupportSettings`, which are the three circumference points and spacing needed to regenerate the group.

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
