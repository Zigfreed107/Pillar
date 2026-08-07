# Indexed Mesh Conversion Plan

## Purpose

This document is the implementation brief for converting Pillar's authoritative model and generated-support geometry from triangle-soup buffers to genuinely indexed triangle meshes.

The conversion is intended to:

- give geometry algorithms reliable shared-edge topology
- reduce model memory and project-file size
- remove repeated position-quantization work from adjacency queries
- provide a stronger foundation for island detection and future 3MF import
- preserve Pillar's existing rendering, support-editing, persistence, and undo behavior

Implement this work incrementally. Do not combine it with mesh repair, boolean union, island detection, or a large support-domain redesign.

## Decisions Already Made

1. Authoritative geometry should use indexed triangles with shared position indices.
2. Smooth shading is not required.
3. Rendering geometry may be expanded into a completely non-indexed, flat-shaded triangle list. If Helix requires an index buffer, it may use sequential indices over three unique render vertices per face.
4. Topological vertices and rendering vertices are different concepts. A single authoritative vertex may become several render vertices.
5. `SupportEntity` must remain the compact, editable procedural definition of a support. Generated mesh data is derived output and must not replace the support domain model.
6. Initial welding must preserve triangle count and triangle order so persisted triangle identities remain valid.
7. The first conversion must not attempt tolerance-based mesh repair, remove duplicate faces, remove degenerate faces, change winding, or boolean-union intersecting shells.

## Current State

Pillar already exposes position and index buffers, but the main producers populate them as triangle soup:

- `StlImporter.AddTriangle` appends three new model positions and three sequential indices for every STL facet.
- `SupportMeshBuilder.AddTriangle` appends three new support positions, three face normals, and three sequential indices for every generated face.
- `MeshEntity` stores model `Vertices`, `TriangleIndices`, and one optional normal per stored vertex.
- `SupportMeshData` stores support `Positions`, `TriangleIndices`, and one normal per stored position.
- Geometry consumers generally dereference triangle indices correctly.
- Face-set, Area Support, and Contour Support reconstruct mesh adjacency by quantizing position coordinates because shared indices cannot currently be trusted.

Consequently, the work is a topology and normal-contract conversion rather than a replacement of every triangle-processing algorithm.

## Target Architecture

```text
Imported model source
    -> indexed authoritative model topology
    -> geometry analysis, selection, support placement, export, persistence
    -> rendering adapter
    -> expanded flat-shaded Helix mesh

SupportEntity and SupportLayerGroup
    -> SupportMeshBuilder
    -> indexed generated support topology
    -> export and geometry consumers
    -> rendering adapter
    -> expanded flat-shaded Helix mesh
```

The authoritative mesh contract should remain renderer-agnostic and use `System.Numerics` types. It should provide at least:

- position buffer
- triangle index buffer
- validation of finite positions and index ranges
- triangle count and stable triangle ordering
- optional or lazily calculated per-face normals
- optional cached edge ownership and triangle adjacency
- diagnostics for open and non-manifold edges where useful

Do not make per-render-vertex normals part of authoritative topology. Geometry algorithms should calculate face normals from indexed positions or use cached per-face normals.

A shared mesh payload type may be introduced if it removes real duplication, but it is not required for the first step. Preserving the current `MeshEntity.Vertices` and `MeshEntity.TriangleIndices` surface can reduce migration risk.

## Welding Policy

### Initial policy

Use exact local-space position equality to deduplicate STL positions during the first conversion. This is a representation change, not a repair operation.

For every imported triangle:

1. Resolve each facet position to an existing exact position or append it.
2. Append the three resolved indices in the original facet order.
3. Preserve the facet's triangle position in the triangle buffer.
4. Record invalid or degenerate input as diagnostics without silently changing triangle identity.

Exact welding may leave seams in noisy STL files. That is acceptable for the initial conversion.

### Explicitly deferred

Tolerance-based welding must be a separate, deliberate mesh-repair feature because it can:

- join separate shells that pass close to each other
- close intentional gaps
- collapse thin features
- create non-manifold edges
- alter face-selection and support-generation results

If tolerance welding is added later, use a spatial search that checks neighbouring cells rather than relying only on rounded coordinate keys.

## Model Changes

### Import

Update `StlImporter` so binary and ASCII facets feed an indexed mesh builder instead of appending three independent positions. `ReadBinary` and `ReadAscii` should retain their parsing responsibilities.

The importer must continue to:

- preserve winding and triangle order
- tolerate binary and ASCII STL
- compute a fallback face normal only when needed for validation or diagnostics
- avoid coupling import to Helix or WPF

Future 3MF import should preserve valid source indices directly rather than unwelding and rewelding them.

### MeshEntity

Revise the `MeshEntity` normal contract. The current rule of zero normals or one normal per position is tied to triangle-soup rendering and cannot represent several flat face normals at one shared position.

Preferred direction:

- retain positions and triangle indices as durable geometry
- derive or cache one normal per triangle when geometry code benefits from it
- build flat render normals in the rendering layer
- keep bounds cached from authoritative positions

## Generated Support Changes

Keep `SupportEntity`, profiles, styles, generator settings, modifier definitions, and regeneration rules unchanged.

Update `SupportMeshBuilder` structurally:

- `AddFrustum` should add each ring once and index wall triangles into the rings.
- `AddSphere` should add poles and latitude rings once and reuse their indices.
- `AddCap` should add an indexed center and ring for the cap.
- Cap boundary positions may remain separate from wall boundary positions because topology sharing across the sharp edge is not required for rendering and separate closed primitives are acceptable.
- Replace the current position-based `AddTriangle` producer with index-based triangle emission where practical.
- Validate that every generated triangle references valid finite positions.

Current support geometry contains separately closed and sometimes overlapping primitives, including frustums, caps, branches, and joint balls. Indexed generation does not need to boolean-union these primitives into one manifold surface. Boolean union or remeshing is out of scope.

## Rendering Changes

Rendering must deliberately expand authoritative triangles into flat-shaded render triangles.

For every authoritative triangle:

1. Read its three indexed positions.
2. Calculate the face normal from the triangle winding.
3. Append three independent render positions.
4. Append the same face normal three times.
5. Append three sequential render indices if the Helix mesh API requires indices.

This approach intentionally gives up GPU vertex sharing. That is acceptable because flat shading is required and correctness and simplicity are more important than render-buffer deduplication.

Update or centralize this conversion for:

- `MeshRenderer.Create`
- `SupportRenderer.Create`
- face-angle highlight geometry
- face-selection highlight geometry

Domain triangle indices must remain stable even though render vertex indices differ. If any hit-testing path begins returning render triangle or render vertex identities directly, add an explicit mapping back to the authoritative triangle index. Current face picking largely recomputes the containing domain triangle from the hit position, so preserve that behavior unless a measured performance reason justifies changing it.

## Geometry Algorithm Changes

Most geometry algorithms already read positions through `TriangleIndices` and should retain their current algorithms. This includes:

- horizontal face-angle analysis
- vertical mesh projection
- support branch collision checks
- support placement planning
- bounds and transform calculations
- STL export

Update topology reconstruction to use ordered pairs of authoritative vertex indices instead of quantized positions:

```text
edge key = (min(firstVertexIndex, secondVertexIndex),
            max(firstVertexIndex, secondVertexIndex))
```

Apply this to:

- `FaceSetSelectionAnalyzer.CreateTriangleAdjacency`
- Area Support triangle adjacency
- Area Support boundary-edge ownership
- Contour Support triangle adjacency

Do not replace positional tolerance in contour slice-segment assembly. Plane-intersection endpoints are calculated values and still require tolerant positional matching.

Consider placing reusable edge ownership and adjacency construction in one renderer-independent geometry service after the first consumer conversions prove the common contract. Cache the result per immutable mesh if profiling shows repeated construction is significant.

## Persistence And Compatibility

The `.gph` format already stores model positions and triangle indices, but existing projects contain triangle-soup positions and legacy per-position normals.

Add an explicit project schema version before changing normal semantics.

When loading a legacy model:

1. Preserve the triangle buffer and triangle order.
2. Exactly weld duplicate positions.
3. Remap triangle indices to the welded position buffer.
4. Discard, convert, or ignore legacy render normals according to the new mesh contract.
5. Preserve entity IDs, transforms, names, source paths, and original filenames.

Do not remove or reorder triangles during migration. Persisted data currently refers to triangle indices through:

- `FaceSelectionKey`
- Area Support selected faces
- Contour Support `SeedTriangleIndex`

New project saves should write the indexed authoritative representation. Loading an old file and saving it in the new version may reduce its size, but must not change its visible geometry or support attachments.

Supports should continue to persist as procedural entities and support-layer definitions, not generated mesh buffers.

## Export

`StlExporter` already dereferences triangle indices and emits independent STL facets, so its core model and support writing algorithms should not need redesign.

Verify that export:

- preserves current world transforms
- recomputes outward face normals from triangle winding
- produces the same triangle count and geometry before and after conversion
- remains compatible with STL's facet-based representation

A future 3MF exporter should preserve indexed topology directly.

## Triangle Identity Invariant

The authoritative triangle index is its ordinal position in the triangle index buffer, not a vertex index and not a render index.

During the initial conversion and legacy migration:

- do not reorder triangles
- do not delete triangles
- do not insert triangles between existing triangles
- remap only position indices

Any later mesh-repair operation that changes triangle identity must explicitly reconcile or invalidate saved face selections and generator settings in one undoable operation.

## Risks And Mitigations

### Incorrect welding

Risk: nearby but separate geometry becomes connected.

Mitigation: exact equality only in the initial conversion; defer tolerance repair.

### Rendering appearance changes

Risk: shared topology accidentally produces smooth or averaged normals.

Mitigation: always expand render triangles and assign one calculated face normal to their three independent render vertices.

### Persisted selection and support corruption

Risk: triangle reordering invalidates face and seed triangle references.

Mitigation: preserve triangle order and count; add migration tests.

### Non-manifold input

Risk: authoritative edge ownership exposes edges with zero, one, or more than two neighbours.

Mitigation: retain diagnostics and make algorithms handle these cases conservatively rather than assuming every mesh is a closed two-manifold.

### Support topology misunderstanding

Risk: indexed support primitives are mistaken for a boolean-unioned support solid.

Mitigation: document that generated supports may contain overlapping closed components; keep boolean union out of this project.

### Performance regression

Risk: import welding, render expansion, or adjacency construction adds CPU cost.

Mitigation: perform work outside render loops, cache immutable results, benchmark large imported models and dense support layers, and avoid repeated full-scene rebuilds.

## Testing Requirements

Add focused tests for:

- two model triangles sharing an exact indexed edge
- duplicate STL coordinates producing fewer positions without changing triangle order
- near but unequal coordinates remaining separate
- triangle winding and computed face normals
- flat rendering of a welded cube corner
- face selection and face-angle overlays using authoritative triangle identities
- indexed frustum, cap, and sphere support generation
- valid support index ranges and finite positions
- open boundaries and non-manifold edge diagnostics
- Contour Support and Area Support adjacency on indexed meshes
- legacy `.gph` migration
- save/load topology round trips
- preserved `FaceSelectionKey` and `SeedTriangleIndex` values
- unchanged STL export triangle counts and world-space positions

Update existing support-normal tests so they validate geometric face normals or expanded render normals rather than assuming that `Normals[indexA]` is the authoritative triangle normal.

Performance checks should compare:

- import time
- model position-buffer memory
- project-file size
- adjacency construction time
- render conversion time
- support mesh generation time
- viewport behaviour with large models and dense support layers

## Suggested Implementation Sequence

1. Define the authoritative indexed mesh invariants and add validation helpers.
2. Add schema versioning and legacy migration tests before changing saved mesh semantics.
3. Convert STL import to exact indexed position reuse while preserving triangle order.
4. Update `MeshEntity` normal semantics.
5. Add the flat-shaded rendering expansion path for models and overlays.
6. Convert face and support-tool adjacency from positional keys to vertex-index edge keys.
7. Convert `SupportMeshBuilder` primitives to indexed output.
8. Route support rendering through the same flat-shaded expansion policy.
9. Update geometry, rendering, persistence, and export tests.
10. Benchmark representative large models and dense support layers.
11. Only after this conversion is stable, build island detection or 3MF import on top of the indexed topology.

Each step should compile and pass its focused tests before proceeding. If a step requires triangle reordering, tolerance welding, boolean union, or replacing procedural support definitions with meshes, stop and reassess the scope before implementing it.

## Completion Criteria

The conversion is complete when:

- imported models reuse shared exact position indices
- generated support primitives reuse appropriate position indices
- geometry adjacency uses authoritative indexed edges
- the viewport remains deliberately flat shaded
- render geometry is allowed to remain fully expanded and non-indexed
- existing projects load without losing selections or support definitions
- support entities remain editable and regenerable
- STL export remains geometrically equivalent
- no new topology or conversion work occurs inside render loops
- the focused correctness and performance tests pass

