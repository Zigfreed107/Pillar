# Upgrade app to use Indexed meshes
I wish to upgrade the app to use indexed meshes instead of non-indexed meshes. This will: 
- allow for more efficient rendering and better performance, especially for complex models.
- allow us to use 3MF file formats without splitting them into individual faces, then joining them again.
- 


# Notes:
The rendered view does not need to smooth shade any model, support, raft, or any other entity added. Flat shading is fine. So when preparing the mesh for rendering, there is no need to decide which faces should share verticies to preserve smooth shading. This means that the mesh can be prepared for rendering by simply creating a new vertex for each face, and not worrying about whether the face shares a vertex with another face.



The recommended change is therefore:
Make mesh topology genuinely indexed and shared, while keeping rendering vertices and procedural support definitions separate.

Current state
Models:
[MeshEntity.cs (line 75)](C:/Users/allan/.codex/worktrees/f1a9/Pillar/src/Pillar.Core/Entities/MeshEntity.cs:75) already stores Vertices, TriangleIndices, and Normals.
[StlImporter.AddTriangle (line 153)](C:/Users/allan/.codex/worktrees/f1a9/Pillar/src/Pillar.Core/Import/StlImporter.cs:153) always appends three new vertices and sequential indices.
Supports:
SupportEntity remains a compact procedural definition—tip, base, profile, branch, style—which is correct.
[SupportMeshData.cs (line 18)](C:/Users/allan/.codex/worktrees/f1a9/Pillar/src/Pillar.Geometry/Supports/SupportMeshData.cs:18) already carries positions and triangle indices.
[SupportMeshBuilder.AddTriangle (line 517)](C:/Users/allan/.codex/worktrees/f1a9/Pillar/src/Pillar.Geometry/Supports/SupportMeshBuilder.cs:517) similarly duplicates all three positions and normals for every triangle.
Functions that must be updated
1. Model import and mesh contract
[StlImporter.AddTriangle (line 153)](C:/Users/allan/.codex/worktrees/f1a9/Pillar/src/Pillar.Core/Import/StlImporter.cs:153) would need to build or feed a topology-normalization stage that:
Finds an existing matching position.
Reuses its index where safe.
Preserves triangle order.
Rejects or reports degenerate triangles.
Records non-manifold edges and other diagnostics.
ReadBinary and ReadAscii can retain most of their parsing logic and pass facets into the new builder.
[MeshEntity (line 75)](C:/Users/allan/.codex/worktrees/f1a9/Pillar/src/Pillar.Core/Entities/MeshEntity.cs:75) needs a revised normal contract. It currently requires either zero normals or exactly one normal per vertex. That is insufficient for a welded vertex shared by differently oriented faces.
A good authoritative representation would contain:
Positions
Triangle indices
Optionally face normals
Optionally cached edge/adjacency topology
No Helix objects
Per-face normals can always be computed from triangle geometry. Render normals should not be the source of geometric truth.
2. Support mesh generation
These would need structural updates:
[SupportMeshBuilder.Build (line 32)](C:/Users/allan/.codex/worktrees/f1a9/Pillar/src/Pillar.Geometry/Supports/SupportMeshBuilder.cs:32)
[AddFrustum (line 366)](C:/Users/allan/.codex/worktrees/f1a9/Pillar/src/Pillar.Geometry/Supports/SupportMeshBuilder.cs:366)
[AddCap (line 399)](C:/Users/allan/.codex/worktrees/f1a9/Pillar/src/Pillar.Geometry/Supports/SupportMeshBuilder.cs:399)
[AddSphere (line 454)](C:/Users/allan/.codex/worktrees/f1a9/Pillar/src/Pillar.Geometry/Supports/SupportMeshBuilder.cs:454)
[AddTriangle (line 517)](C:/Users/allan/.codex/worktrees/f1a9/Pillar/src/Pillar.Geometry/Supports/SupportMeshBuilder.cs:517)
For example, AddFrustum should add each circular ring once and index its triangles into those rings. AddCap may intentionally need a separate copy of the boundary ring because the cap and side require different render normals.
[SupportMeshData (line 18)](C:/Users/allan/.codex/worktrees/f1a9/Pillar/src/Pillar.Geometry/Supports/SupportMeshData.cs:18) should gain the same validation rules as model meshes: valid index ranges, complete triangles, finite coordinates, and an explicit normal policy.
Importantly, SupportEntity should remain procedural. Replacing it with a persisted triangle mesh would undermine editable supports, modifier regeneration, transforms, undo/redo, and the existing architecture.
3. Rendering and normals
These are the most visually sensitive functions:
[MeshRenderer.Create (line 41)](C:/Users/allan/.codex/worktrees/f1a9/Pillar/src/Pillar.Rendering/EntityRenderers/MeshRenderer.cs:41)
[MeshRenderer.ApplyFaceAngleHighlight (line 210)](C:/Users/allan/.codex/worktrees/f1a9/Pillar/src/Pillar.Rendering/EntityRenderers/MeshRenderer.cs:210)
[MeshRenderer.ApplyFaceSelection (line 268)](C:/Users/allan/.codex/worktrees/f1a9/Pillar/src/Pillar.Rendering/EntityRenderers/MeshRenderer.cs:268)
[SupportRenderer.Create (line 37)](C:/Users/allan/.codex/worktrees/f1a9/Pillar/src/Pillar.Rendering/EntityRenderers/SupportRenderer.cs:37)
A topological vertex and a rendering vertex should not necessarily be the same thing.
For example, the corner of a cube is one topological position but needs three render vertices if each face is to retain a sharp, flat-shaded normal. The renderer should build a Helix buffer that splits topology vertices at:
Hard edges
Flat-shaded faces
Material boundaries
Future color or texture-property seams
Face selection and highlighting must continue referring to stable domain triangle indices even if the renderer creates a different vertex layout.
4. Adjacency and boundary analysis
These currently reconstruct topology by quantizing vertex coordinates:
[FaceSetSelectionAnalyzer.CreateTriangleAdjacency (line 283)](C:/Users/allan/.codex/worktrees/f1a9/Pillar/src/Pillar.Geometry/Analysis/FaceSetSelectionAnalyzer.cs:283)
[AreaSupportPattern adjacency (line 317)](C:/Users/allan/.codex/worktrees/f1a9/Pillar/src/Pillar.Geometry/Supports/AreaSupportPattern.cs:317)
[AreaSupportPattern.CreateBoundaryEdges (line 444)](C:/Users/allan/.codex/worktrees/f1a9/Pillar/src/Pillar.Geometry/Supports/AreaSupportPattern.cs:444)
[ContourSupportPattern.CreateTriangleAdjacency (line 356)](C:/Users/allan/.codex/worktrees/f1a9/Pillar/src/Pillar.Geometry/Supports/ContourSupportPattern.cs:356)
Once indexed topology is authoritative, mesh edges can be keyed using ordered pairs of vertex indices:
(min(vertexA, vertexB), max(vertexA, vertexB))
This is faster and deterministic compared with repeatedly creating quantized spatial keys.
However, contour slice-segment assembly should continue using positional tolerances. Slice intersection points are newly calculated values and do not necessarily coincide bit-for-bit even when their source mesh is indexed.
5. Persistence and migration
These paths need review:
Model serialization in [GphDocumentSerializer.cs (line 293)](C:/Users/allan/.codex/worktrees/f1a9/Pillar/src/Pillar.Core/Persistence/GphDocumentSerializer.cs:293)
Model reconstruction in [GphDocumentSerializer.cs (line 703)](C:/Users/allan/.codex/worktrees/f1a9/Pillar/src/Pillar.Core/Persistence/GphDocumentSerializer.cs:703)
Mesh DTO fields in [GphDocumentSerializer.cs (line 1957)](C:/Users/allan/.codex/worktrees/f1a9/Pillar/src/Pillar.Core/Persistence/GphDocumentSerializer.cs:1957)
The existing file already stores vertices and indices, so the basic shape is compatible. The complication is normals and old triangle-soup project files.
A migration should:
Load old triangle-soup meshes.
Weld only their positions initially.
Remap the existing indices.
Preserve triangle ordering.
Avoid deleting or reordering triangles.
Recompute or discard legacy per-vertex normals as appropriate.
Preserving triangle order matters because face selections and contour support definitions persist triangle indices. The project format currently has a format name but no explicit schema version, so adding versioning would be advisable before changing normal semantics.
6. Tests
Existing tests that assume one face normal per duplicated position need changes, particularly:
Joint-ball normal tests around [Program.cs (line 346)](C:/Users/allan/.codex/worktrees/f1a9/Pillar/tests/Pillar.Geometry.SmokeTests/Program.cs:346)
Closed-mesh validation around [Program.cs (line 5698)](C:/Users/allan/.codex/worktrees/f1a9/Pillar/tests/Pillar.Geometry.SmokeTests/Program.cs:5698)
New tests should cover:
Two triangles sharing an indexed edge.
Flat shading across a welded sharp edge.
Smooth support cylinders and spheres.
Exact duplicate welding.
Near-but-separate surfaces that must not be welded.
Non-manifold edges with three or more owners.
Open meshes.
Old .gph triangle-soup migration.
Preserved triangle indices after migration.
Save/load topology round trips.
STL export from an indexed mesh.
Functions that largely would not need algorithm changes
These already dereference the index buffer correctly:
[HorizontalFaceAngleAnalyzer (line 31)](C:/Users/allan/.codex/worktrees/f1a9/Pillar/src/Pillar.Geometry/Analysis/HorizontalFaceAngleAnalyzer.cs:31)
[MeshVerticalProjection (line 119)](C:/Users/allan/.codex/worktrees/f1a9/Pillar/src/Pillar.Geometry/Supports/MeshVerticalProjection.cs:119)
[SupportBranchPlanner (line 291)](C:/Users/allan/.codex/worktrees/f1a9/Pillar/src/Pillar.Geometry/Supports/SupportBranchPlanner.cs:291)
[StlExporter.WriteModelTriangles (line 219)](C:/Users/allan/.codex/worktrees/f1a9/Pillar/src/Pillar.Geometry/Export/StlExporter.cs:219)
[StlExporter.WriteSupportTriangles (line 235)](C:/Users/allan/.codex/worktrees/f1a9/Pillar/src/Pillar.Geometry/Export/StlExporter.cs:235)
Bounds, scale, rotation, projection, and most support placement functions
They may need signature changes if a new common mesh type is introduced, but their algorithms already support shared vertex indices.
Main risks
1. Incorrect welding changes geometry
The greatest correctness risk is the welding tolerance.
A tolerance that is too large can:
Join separate shells that merely pass close to one another.
Close intentionally small gaps.
Collapse thin features.
Create non-manifold edges.
Change island and face-connectivity results.
A tolerance that is too small can leave STL seams unwelded.
I would start with exact-coordinate deduplication as the non-destructive normalization. Tolerance-based repair should be a separate, explicit mesh-repair operation with diagnostics.
2. Shading regressions
If coincident vertices are merged and their normals simply averaged, sharp models become visibly rounded. Conversely, retaining a single facet normal produces incorrect lighting on the other incident faces.
This is why topology and rendering buffers need separation.
3. Persisted triangle identity
Welding positions alone can preserve triangle order. Removing duplicate or degenerate triangles cannot.
Reordering or deleting triangles can invalidate:
FaceSelectionKey
Area-support selected faces
Contour-support SeedTriangleIndex
Saved generator settings
Initial normalization should not reorder or delete faces.
4. Non-manifold and damaged input
STL files commonly contain:
Open boundaries
Inconsistent winding
Duplicate triangles
T-junctions
Edges owned by more than two faces
Intersecting shells
Indexed topology makes these problems easier to detect, but it does not automatically repair them. Algorithms must decide whether to reject, warn, or continue conservatively.
5. Support geometry is composed of overlapping primitives
Current supports are built from separately closed frustums, caps, branches, and joint balls. Converting each primitive to indexed geometry does not turn the whole support into one boolean-unioned manifold surface.
That is acceptable for rendering and STL export. Creating a single unified manifold support would require boolean union or remeshing and is a much larger, riskier project.
6. Performance trade-offs
Likely benefits:
Much lower model position-buffer memory.
Faster topology and adjacency queries.
Smaller project files.
Better foundation for 3MF and island analysis.
Possible costs:
Import-time welding work.
Edge/adjacency cache memory.
Renderer-side vertex splitting.
Additional mapping between domain triangles and render triangles.
Because meshes are immutable, adjacency and render buffers can be calculated once and cached, making this a favourable trade-off.
Recommended scope
I would split this into three levels:
Weld imported model topology: medium risk and high value.
Make generated support mesh output properly indexed: medium risk, mostly isolated to SupportMeshBuilder and normal handling.
Replace procedural supports with stored meshes: high risk and not recommended.
The clean target is:
MeshEntity
    → authoritative indexed topology
    → geometry analysis and export

SupportEntity
    → editable procedural definition
    → SupportMeshBuilder
    → authoritative indexed generated mesh

Indexed topology
    → rendering adapter
    → Helix-specific vertices split where normals require it