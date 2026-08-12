# Island Detection

This document defines the implementation plan for mesh-based island detection as a reusable Pillar analysis capability. The analysis is available from the main toolbar and can also be called by support-generation and other geometry-aware tools.

## Purpose

Island Detection identifies parts of an imported model that begin above the build plate as independent geometric components and only connect to older geometry later in the print direction.

The feature is intended to help users find model regions that should be considered for supports. It is an analysis and guidance feature; it does not prove that a print will succeed or automatically mutate the document.

## Core Decision: Analyze The Mesh, Do Not Slice It

The detector operates directly on the transformed triangle mesh. It must not generate print layers, rasterize cross-sections, or depend on printer pixel resolution.

The detector models an upward sweep through the mesh using world-space Z as a height function. It tracks when connected mesh regions are born and when they merge into older regions. This is commonly described as a lower-star sweep or merge-tree calculation.

This decision provides fast, reusable geometric analysis without adding a slicing subsystem. It also establishes an important limitation: results are geometric island candidates, not exact predictions of the pixels that a particular slicer and printer will expose.

## Terminology

- **Birth**
  The lowest point or connected flat plateau at which a new mesh component appears during the upward sweep.
- **Merge**
  The height at which a younger component first joins an older component.
- **Island candidate**
  A component born above the build plate. It remains an island candidate until it merges or the mesh ends.
- **Grounded component**
  A component whose birth region touches the build plate within the configured tolerance.
- **Persistence height**
  The vertical distance between an island's birth and merge heights. An unmerged floating shell has no finite merge height.
- **Overhang**
  Downward-facing or laterally expanding geometry that remains connected to older geometry. Overhangs and islands are related support concerns but are not the same condition.

## Scope

The first implementation should:

- analyze one selected `MeshEntity` at a time
- use the mesh's current `WorldTransform`
- consume authoritative indexed positions and triangle indices without tolerance rewelding
- detect point, edge, and flat-plateau births
- track each island candidate until it merges into older geometry
- report completely disconnected floating shells
- calculate enough metrics to rank and filter results
- expose renderer-independent results for other tools
- display transient markers and highlights when launched from the main toolbar
- invalidate stale results when the model transform or analysis settings change
- support cancellation and progress reporting

The first implementation should not:

- slice or rasterize the model
- emulate printer pixels, antialiasing, exposure, or resin behaviour
- automatically add supports
- treat every downward-facing triangle as an island
- save analysis overlays or raw analysis results in the project file
- make island analysis depend on WPF, Helix Toolkit, SharpDX, or viewport objects
- perform mesh Boolean unions for touching or intersecting shells

## Architectural Shape

Island Detection has two distinct layers:

1. A reusable geometry analysis service.
2. A transient user workflow that launches the service and presents its results.

The geometry service is the source of truth. The toolbar, render overlay, and client tools are consumers of the same result.

### Pillar.Geometry

`Pillar.Geometry.Analysis` should own:

- indexed vertex adjacency needed by the detector
- world-space height evaluation
- plateau grouping
- the union-find sweep and merge tree
- island candidate metrics
- geometry diagnostics
- analysis request, settings, and result types

Suggested initial types:

- `MeshIslandAnalyzer`
- `IslandDetectionSettings`
- `IslandDetectionResult`
- `IslandCandidate`
- `IslandDetectionDiagnostics`
- an internal indexed vertex graph, or a focused extension to `IndexedMeshTopology`
- an internal union-find implementation

These names describe responsibilities rather than imposing a required class breakdown. Keep the first implementation compact and split types only where they protect a real boundary or make the algorithm testable.

### Pillar.Rendering

Rendering should own:

- transient island markers
- highlighted birth regions or branch triangles
- the selected-result appearance
- showing, hiding, and clearing the overlay
- keeping diagnostic visuals out of normal entity hit testing and selection post-effects

Suggested renderer:

- `IslandDetectionPreviewRenderer`

The renderer consumes triangle identities and world-space points from the analysis result. It must not recompute island topology or infer analysis state from Helix geometry.

### Pillar.UI

The UI should own:

- the main-toolbar launch action
- resolving the selected model
- running analysis asynchronously
- progress, cancellation, and error presentation
- result navigation and filtering
- deciding when cached results are stale
- coordinating the renderer with the current result

Keep event wiring in a focused partial such as `MainWindow.IslandDetection.cs`. If the workflow grows beyond a small partial, introduce a focused `IslandDetectionCoordinator` rather than placing session state and asynchronous orchestration directly in `MainWindow.xaml.cs`.

### Pillar.Core And Commands

No new document command is required to run or display island analysis because the operation is transient and does not mutate durable state.

If a user later creates supports from island candidates, the consuming support tool must translate accepted candidates into ordinary support definitions and commit them through the existing command and support-layer paths. Analysis results themselves should not become document entities by default.

## Reuse Contract For Other Tools

Other tools should call the geometry analyzer directly with a mesh, an explicit transform or the mesh's current world transform, settings, cancellation, and optional progress reporting. They should not simulate a toolbar click or depend on the toolbar-owned session.

The reusable result must contain renderer-independent information such as:

- source model identity
- the transform snapshot used for analysis
- candidate birth and merge heights
- birth positions or plateau vertices
- original source triangle identities
- branch triangle identities when available
- world-space bounds
- persistence height
- downward-facing area and total branch area
- grounded, merged, or unmerged state
- confidence and diagnostic flags

Callers remain responsible for interpreting the result. Examples include:

- a manual support tool navigating to the next island
- a future automatic support tool proposing contact points
- an orientation tool comparing island counts between rotations
- an export validator warning that candidates remain unresolved

Presentation filters should not destroy the raw result. A toolbar workflow may hide tiny candidates while another tool requests or examines the complete candidate collection.

## Main-Toolbar Workflow

Island Detection is launched from the main toolbar because it is a document/model analysis available across workflow modes. The toolbar entry is a launcher and status surface, not the owner of geometry logic or detailed settings.

Recommended workflow:

1. The user selects a model layer.
2. The Island Detection toolbar button becomes enabled.
3. Clicking the button starts analysis for the selected model or reuses a valid cached result.
4. The application shows cancellable progress without blocking viewport rendering.
5. When analysis completes, a compact result panel opens and transient markers appear.
6. The user moves through candidates with Previous and Next actions.
7. Selecting a candidate focuses or frames it and emphasizes its birth region.
8. The user can adjust presentation filters without rerunning topology analysis.
9. Refresh explicitly reruns the detector when geometry settings change.
10. Close hides the result panel and clears the overlay without changing the document.

The initial toolbar action should not replace or cancel the active viewport tool. Analysis does not require pointer ownership and therefore should not be registered as an `ITool`. A later action such as **Add Support Here** may deliberately activate the appropriate support tool after preserving the selected candidate as input.

Recommended result-panel information:

- candidate count and current index
- birth height
- merge height or `Unmerged`
- persistence height
- approximate affected area
- severity or confidence
- Previous, Next, Refresh, and Close actions
- filters for minimum persistence and minimum area

Avoid putting these detailed controls directly into the main toolbar.

## Mesh Analysis Algorithm

### 1. Capture An Immutable Analysis Snapshot

Capture:

- the selected `MeshEntity` identity
- local indexed positions and triangle indices
- `WorldTransform`
- build-plate Z
- height-grouping and build-plate contact tolerances
- the analysis settings fingerprint

Transform each indexed position into world space once. Do not repeatedly transform positions inside adjacency or merge loops.

### 2. Use The Authoritative Indexed Mesh

`StlImporter` already builds an indexed mesh for both binary and ASCII STL files. Its `IndexedStlMeshBuilder` reuses a position index whenever two facet vertices have exactly equal local-space `Vector3` coordinates. Triangle order and winding remain unchanged.

Island Detection should treat `MeshEntity.Vertices` and `MeshEntity.TriangleIndices` as the authoritative topology:

1. Validate the position and index buffers with `IndexedMeshValidator`.
2. Preserve every authoritative position index and triangle ordinal.
3. Build vertex adjacency directly from the indexed triangle edges.
4. Retain position-to-triangle mappings for highlighting and downstream tools.
5. Reuse `IndexedMeshTopology` for edge ownership and mesh diagnostics where its existing API is sufficient.

The detector must not perform a second tolerance-based weld. The importer deliberately leaves merely near-equal coordinates separate because joining them can change intended topology, collapse narrow gaps, and create false connectivity. If tolerance-based repair is needed later, it should be an explicit mesh-repair feature that produces new authoritative geometry before Island Detection runs.

Exact duplicate coordinates with different indices can still occur in geometry created outside the current STL importer. Island Detection should respect those indices and may report a disconnected-position diagnostic rather than silently changing the mesh.

### 3. Build Mesh Adjacency

For each non-degenerate source triangle:

- read its three authoritative position indices
- add its undirected indexed edges to the vertex graph
- record triangle ownership for each indexed position and edge
- ignore collapsed edges whose two position indices are equal
- count degenerate and non-manifold conditions for diagnostics

The existing `IndexedMeshTopology` provides triangle adjacency, indexed edge ownership, and open, non-manifold, and degenerate diagnostics. The merge-tree sweep additionally needs position-to-position adjacency. Add that focused capability to the shared topology type if other geometry tools will benefit; otherwise keep a compact vertex graph private to the island analyzer.

The detector should use indexed graph adjacency rather than coordinate proximity or face normals to decide whether regions are connected.

### 4. Establish Height And Plate Contact

Evaluate height from each indexed position's transformed world-space Z coordinate.

Any birth plateau at or below `BuildPlateZ + BuildPlateContactTolerance` is grounded. A component born above that range is an island candidate.

Height comparisons need a named tolerance so a flat imported face does not become hundreds of artificial minima because of floating-point noise.

### 5. Group Equal-Height Plateaus

Process connected vertices whose heights are equal within the height-grouping tolerance as one plateau.

For each plateau, collect distinct neighbouring components that are strictly lower:

- no lower component: a new component is born
- one lower component: the plateau extends that component
- multiple lower components: the components merge at this plateau

Plateau processing must finish before higher vertices are activated. This prevents a flat underside from being reported as many vertex-sized islands.

### 6. Sweep Upward With Union-Find

Sort indexed positions or plateau groups by world-space height and activate them from low to high.

Use union-find to maintain connected active components. Each active component records:

- birth height
- birth plateau
- whether it is grounded
- accumulated bounds and metrics
- the branch or candidate identity it currently represents

When components merge, use a deterministic elder rule:

- the oldest grounded component survives when present
- otherwise the component with the lowest birth height survives
- use a stable identifier as the final tie-breaker
- younger components close at the merge height

Each closed above-plate branch becomes an island candidate. An above-plate component still alive at the end becomes an unmerged floating-shell candidate.

### 7. Attribute Geometry To Candidates

At minimum, retain the birth plateau and nearby source triangles so the UI can place a useful marker.

For richer highlighting and future support generation, attribute source triangles to the active branch during the sweep until that branch merges. Store original triangle numbers rather than Helix indices or render objects.

Calculate candidate metrics after topology is known:

- persistence height
- total attributed surface area
- downward-facing attributed area
- XY bounds and world bounds
- lowest representative points
- number of birth vertices and triangles
- whether the candidate is merged or permanently disconnected

### 8. Rank Without Hiding Uncertainty

Severity can initially be based on:

- unmerged floating shell status
- persistence height
- downward-facing area
- total branch area
- XY span

Keep severity separate from detection. A small candidate is still a detected candidate even if the toolbar hides it with a filter.

Diagnostic flags should identify candidates affected by:

- non-manifold edges
- degenerate triangles
- open mesh boundaries
- coincident positions represented by distinct authoritative indices
- a zero-area birth plateau

## Settings

Separate topology settings from presentation filters.

Analysis settings that require reanalysis:

- height-grouping tolerance
- build-plate Z
- build-plate contact tolerance

Presentation filters that should not require reanalysis:

- minimum persistence height
- minimum branch area
- minimum downward-facing area
- show or hide low-confidence candidates
- show or hide grounded diagnostics

Start with safe application defaults and keep advanced height and plate-contact tolerances out of the primary toolbar UI. These values can materially change birth and grounding classification and should not look like ordinary visual preferences.

## Result Lifetime, Caching, And Invalidation

Analysis results are transient session data. A result is valid only for the captured:

- model identity
- mesh buffer identity or geometry revision
- world transform
- topology settings
- build-plate definition

Invalidate or cancel the current result when:

- the analyzed model is removed
- its import placement or user transform changes
- the model's source geometry is replaced
- a topology setting changes
- the document is closed or replaced

Changing only presentation filters should reuse the current result.

Because imported indexed mesh buffers are immutable, local indexed topology may later be cached separately from world-height analysis. Rotation changes heights and merge ordering, but it does not change local adjacency. Add this cache only after profiling shows repeated topology construction is significant.

## Existing Supports

The core detector analyzes model geometry and should not depend on `SupportEntity` or support-layer structures.

The first toolbar version may therefore continue to show a geometric island after the user has manually supported it. If support-aware presentation is required later, add it as reconciliation outside the topology algorithm:

- the caller supplies renderer-independent support contact points
- candidate branches are checked for a valid contact near their birth region
- the UI marks candidates as addressed rather than deleting them from the geometric result

This preserves the rendering/domain boundary and allows non-support tools to consume unbiased model analysis.

## Rendering Plan

Implement rendering in increasing levels of complexity:

1. Show one world-space marker at each birth region.
2. Emphasize the active candidate and dim the other markers.
3. Add an optional overlay for birth triangles.
4. Add full branch-triangle highlighting only if it materially helps support placement.

Reuse scene objects and update their buffers rather than creating one Helix model per marker where possible. Overlay refreshes occur only when results, filtering, or the active candidate change; they must not allocate or rebuild geometry every render frame.

Markers and highlights are transient, non-selectable by default, and never become document entities.

## Error Handling And Diagnostics

Analysis should return a result with diagnostics when partial, meaningful analysis is possible. Reserve exceptions or complete failure for unusable input such as invalid triangle indices or a mesh with no valid triangles.

User-facing messages should distinguish:

- no islands found
- no model selected
- analysis cancelled
- model could not be analyzed
- candidates found with mesh-quality warnings

Do not silently claim that a damaged or heavily non-manifold mesh is island-free.

## Performance Plan

- Keep the analysis off the render and pointer-input paths.
- Transform each indexed position once per run.
- Pre-size lists and dictionaries from mesh counts where practical.
- Avoid LINQ in adjacency, sorting hot loops, and union-find processing.
- Use compact arrays for position state, parents, ranks, and component metadata.
- Report progress at coarse stages rather than for every vertex.
- Check cancellation between topology construction batches and height batches.
- Benchmark indexed imported meshes with at least hundreds of thousands of triangles.

Initial performance targets should be measured rather than guessed. Record topology-build time, sweep time, peak managed memory, indexed position count, triangle count, edge count, and candidate count.

## Implementation Phases

### Phase 1: Contracts And Test Fixtures

- define settings, result, candidate, and diagnostic types in `Pillar.Geometry.Analysis`
- create small deterministic triangle-mesh fixtures
- document default tolerances and coordinate assumptions
- add tests for transformed world-space height

Exit condition: callers can understand the complete renderer-independent result contract before UI work begins.

### Phase 2: Indexed Vertex Topology

- consume the authoritative indexed positions and triangle indices without rewelding
- build position adjacency and position-to-triangle reverse mappings
- reuse or extend `IndexedMeshTopology` for edge ownership and diagnostics
- detect degenerate, boundary, and non-manifold topology
- test exact shared indices and intentionally separate near-equal positions

Exit condition: known indexed surfaces produce stable adjacency while intentionally separate position indices remain disconnected.

### Phase 3: Birth And Merge Detection

- group flat plateaus
- implement the upward union-find sweep
- apply deterministic merge rules
- report grounded, merged, and unmerged components
- calculate persistence height

Exit condition: synthetic meshes produce the expected component birth and merge graph.

### Phase 4: Candidate Geometry And Filtering

- map candidates back to original triangles
- calculate bounds and surface metrics
- integrate downward-facing area classification
- implement non-destructive result filtering and ranking

Exit condition: each result has enough geometry for markers, navigation, and future tool consumption.

### Phase 5: Reusable Analysis Entry Point

- expose a focused analyzer entry point to Rendering and UI callers
- support explicit transform snapshots, cancellation, and progress
- verify that no WPF or Helix types cross into the analysis API
- add a simple consuming test that mimics another tool

Exit condition: a non-toolbar caller can run and consume Island Detection without UI dependencies.

### Phase 6: Rendering And Scene Integration

- add the transient marker renderer
- add selected-candidate emphasis
- add scene methods to show, update, and clear results
- ensure model clipping, selection, and normal scene refreshes do not leave stale overlays

Exit condition: results can be displayed and cleared without mutating the document or interfering with selection.

### Phase 7: Main-Toolbar Workflow

- add the toolbar launcher
- enable it only when a model context is available
- add focused UI orchestration outside `MainWindow.xaml.cs`
- run analysis asynchronously with progress and cancellation
- add the compact results panel and Previous/Next navigation
- frame the active candidate through the existing camera service
- implement result invalidation and refresh

Exit condition: the complete analysis workflow is usable from the toolbar without replacing the active viewport tool.

### Phase 8: Profiling And Refinement

- profile representative production meshes
- add a local-topology cache only if justified
- tune progress granularity and memory use
- evaluate whether branch highlighting is worth its geometry and rendering cost
- validate defaults against real supported and unsupported resin models

Exit condition: the feature is responsive on target mesh sizes and its limitations are clear to users.

## Required Tests

Geometry tests should include:

- a grounded cube with no candidates
- a floating cube reported as one unmerged candidate
- a model with one branch born above the plate and merging later
- several branches merging at the same saddle height
- a flat floating underside reported as one plateau, not one result per vertex
- a sphere or pointed feature with a single birth
- disconnected shells inside one `MeshEntity`
- an indexed quad whose two triangles share authoritative position indices
- exact duplicate STL coordinates that the importer converts to shared indices
- near-equal STL coordinates that the importer intentionally keeps separate
- coincident positions with distinct manually supplied indices, reported without implicit repair
- degenerate and repeated triangles
- open and non-manifold meshes with diagnostics
- deterministic results when vertex and triangle ordering changes
- translation, rotation, uniform scale, and non-uniform scale transforms
- candidates at, just below, and just above the build-plate tolerance
- cancellation during topology construction and during the height sweep

Workflow and rendering checks should include:

- toolbar enablement follows selected model state
- starting analysis does not cancel the active viewport tool
- changing selection during analysis cancels or discards the stale result
- changing `WorldTransform` invalidates the result
- presentation-filter changes reuse the analysis
- closing the panel clears every transient visual
- repeated refresh does not leak scene objects or event subscriptions
- another tool can consume the same geometry result without opening the toolbar UI

## Acceptance Criteria

The first release is complete when:

- island candidates are calculated entirely from model triangle geometry
- no slicing or raster layer representation is introduced
- grounded geometry is distinguished from above-plate births
- flat minima are grouped correctly
- candidate merge heights and persistence are deterministic
- floating disconnected shells are reported
- the result contains only renderer-independent data
- other tools can invoke the analyzer without UI dependencies
- the main toolbar launches a cancellable workflow for the selected model
- results can be navigated and visualized without replacing the active `ITool`
- results are invalidated after relevant model changes
- analysis and overlays do not mutate or persist document state
- mesh-quality limitations are reported instead of silently hidden

## Known Limitations To Communicate

- The detector does not reproduce a slicer's finite layer height or XY pixels.
- Two spatially intersecting shells are disconnected unless their authoritative index topology connects them or a later repair/Boolean feature reconciles them.
- Very noisy meshes may create small local minima; persistence and area filters reduce but do not eliminate this.
- A large connected overhang may need supports without creating an island candidate.
- Resin strength, exposure, peel forces, and support capacity are outside this geometric analysis.
- Existing supports do not change the underlying model candidates unless a caller performs optional support-contact reconciliation.

These limitations are reasons to present the output as support guidance rather than a guarantee of printability.

## Future Extensions

After the base detector is proven, possible extensions include:

- orientation comparison using candidate count, persistence, and area
- support-contact reconciliation and addressed/unaddressed state
- automatic contact-point proposals near stable birth regions
- grouping nearby candidates into one support-planning region
- composite analysis across deliberately connected model entities
- cached indexed topology for rapid rotation experiments
- export warnings for unresolved high-severity candidates

Each extension should continue to consume the renderer-independent mesh analysis rather than duplicating island logic inside individual tools.
