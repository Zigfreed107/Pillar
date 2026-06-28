# Support Clustering

Support Clustering replaces several nearby individual support stems and bases with a shared central stem. Each original model contact and head is preserved, while new branches connect those heads to the shared stem.

The main goals are to:

- reduce the number of stems and bases that must be removed after printing
- create more open space beneath the model for uncured resin to drain
- preserve the original support contact positions
- avoid weakening a support group through branches that are too long, too shallow, or obstructed by the model


## Clustering applied to a whole support layer

### GUI

The Tool Options Panel should show:

- **Apply To**: **Whole Layer** or **Selected Supports**. Tool tips on the button mention the difference between the two scopes (mainly that whole-layer will automatically reapply if the way the support layer is generated changes, while selected can not.
- **Maximum Cluster Radius**: maximum horizontal distance from a cluster's central stem axis to any member's head joint
- **Minimum Supports Per Cluster**: normally two; smaller candidate groups remain unchanged
- **Maximum Supports Per Cluster**: limits branch crowding and shared-stem loading
- **Maximum Branch Angle From Vertical**: prevents shallow, difficult-to-print branches
- **Stem Sizing**: **Automatic** or **Manual**. Automatic should still populate the control with the calculated value, but the control is disabled until Manual is selected. If switching to manual, the current automatic diameter should be remain in control for editing. Manual sizing must still pass validation to prevent unprintable results with an error dialog displaying a message if it fails validation stating why.
- **Central Stem Bottom Diameter** and **Central Stem Top Diameter**: editable when manual sizing is selected
- **Preview**: enabled by default and updated when parameters change
- An **Uncluster Selected** button enabled only when one or more clusters are selected in the viewport, which removes those clusters and returns their members to individual supports in one undoable command. 
- **Apply**: creates the modifier using the current preview
- **Remove All**: available when editing an existing Cluster modifier and removes that modifier
- **Close**: leaves the document unchanged when creating a modifier, or discards uncommitted parameter changes when editing one

Automatic stem sizing should account for the combined load of all members. A suitable starting rule is to preserve at least the combined cross-sectional area of the replaced stems:

    automatic diameter = sqrt(sum of each member diameter squared)

The bottom and top calculations should use the corresponding original stem diameters. Results should be clamped to named minimum and maximum limits so large clusters cannot create unreasonable trunks. Manual sizing overrides the calculated diameters but must still pass validation.

The viewport preview should distinguish:

- supports that will be clustered
- proposed shared stems and branches
- supports that remain individual
- candidates rejected by a geometry or clearance rule

The status text should report the number of proposed clusters, clustered supports, unchanged supports, and rejected candidates. Rejected supports should remain printable individual supports rather than disappearing.

### Workflow
The user selects a support layer, opens the **Edit Supports** mode, and chooses **Cluster Supports**. If more than one layer is selected when the choose **Cluster Supports**, then a warning dialog is shown that instructs the user to select only one support layer and try again, with an OK button that exits out of the tool.

All other support layers are now made invisible until the user exits the tool when they are made visible again.

The user selects **Whole Layer** from the Tool Options Panel.

1. Confirm that a support layer is selected and contains at least two eligible individual supports.
2. Read the input support geometry produced by the generator and any preceding modifiers.
3. Exclude supports already consumed by an incompatible earlier modifier or represented only by derived cluster geometry.
4. Find nearby candidates using a spatial index in the XY plane.
5. Process candidates in a stable order so the same inputs and parameters always produce the same clusters.
6. Build each candidate cluster without exceeding the maximum member count or maximum cluster radius.
7. Calculate a proposed shared-stem position and validate its branches.
8. Reject or reduce candidate clusters that fail branch length, branch angle, model-clearance, or printable-diameter checks.
9. Preview every valid cluster while leaving unclustered supports visible in their original state.
10. On Apply, create one whole-layer Cluster modifier. One modifier may produce several separate cluster assemblies.
11. Rebuild the support layer from its generator output and complete modifier stack.
12. Add one child row such as **Cluster - Whole Layer** beneath the support layer.

Whole-layer clustering is replayable. If the source generator is edited, Pillar regenerates its individual supports and reapplies the Cluster modifier to the new support population using the saved parameters. The resulting cluster memberships may change because they are recalculated from the new positions.

Editing the modifier row should activate Edit Supports mode, open Cluster Supports, restore the saved whole-layer parameters, and regenerate the preview. Applying changed parameters replaces the modifier definition through an undoable command.

A user should be able to choose an individual cluster in the viewer, and click the "Uncluster Selected" button. This would remove the clustering from all supports in that cluster, returning them to individual supports. In Whole Layer mode, this would convert the modifier to a Selected Supports scope (including renaming the modifier row in the Layer Panel. The user will be warned (with an option to cancel) that this action will convert to a selection-scoped modifier and lose automatic reapplication behavior if the way supports are generated is edited.


## Clustering applied to a selection of supports in a layer

### GUI
The same clustering parameters used for whole-layer clustering should be shown.

Only supports in the selected support layer participate. Unselected supports must not be absorbed merely because they are nearby.

Apply should remain disabled when fewer than two eligible supports are selected. If the selected supports form several separated groups, one selection-scoped modifier may create several clusters; the preview and status text should make that result clear.

When an existing selection-scoped modifier is edited, the options panel should restore its saved parameters and identify its stored target set. The targets should be highlighted even if the user entered the tool from the modifier row rather than from an active viewport selection.

### Workflow
The user selects a support layer, opens the **Edit Supports** mode, and chooses **Cluster Supports**. If more than one layer is selected when the choose **Cluster Supports**, then a warning dialog is shown that instructs the user to select only one support layer and try again, with an OK button that exits out of the tool.

All other support layers are now made invisible until the user exits the tool when they are made visible again.

The user selects **Selected Supports** from the Tool Options Panel.

1. The user must select (or have already selected) at least two eligible supports.
2. Capture the stable identities of the eligible selected supports and the support layer's current generator revision.
3. Run the same deterministic grouping and geometry validation used for whole-layer clustering, but only against the captured target set.
4. Preserve selected supports that cannot form a valid cluster as unchanged individual supports.
5. Preview the proposed shared stems and branches without mutating the document.
6. On Apply, create one revision-bound Cluster modifier containing the target identities, source revision, parameters, and resulting modifier order.
7. Rebuild the support layer from its generator output and complete modifier stack.
8. Add one child row such as **Cluster - Selection (12)** beneath the support layer.

A user should be able to choose an individual cluster in the viewer, and click the "Uncluster Selected" button. This would remove the clustering from all supports in that cluster, returning them to individual supports.

The selection-scoped modifier must survive save and load while its generator revision remains valid. It should remain editable and resettable until regeneration changes the source support population.

If generator settings are changed, selection-scoped Cluster modifiers tied to the previous revision are discarded with a user notice. The generator update, modifier removal, and regenerated support output must be one undoable command so Undo restores the original supports and Cluster modifier together.

Reset removes the selected Cluster modifier and rebuilds the layer from the original generator output plus the remaining modifiers. The affected supports return to the individual state produced by preceding modifiers. If this was the final modifier, its child row disappears and the support layer has no modifier stack children.

A user should be able to choose an individual cluster in the viewer, and click the "Uncluster Selected" button. This would remove the clustering from all supports in that cluster, returning them to individual supports. 

# Notes

- Refer to **Documentation\Support Editing Tools\Support Editing Mode Behaviours.md** for wider context as to how this tool fits into the overall support editing workflow.
- Whole-layer and selection-scoped clustering must share one domain clustering implementation so their geometry rules cannot drift apart.
- Tool controls, viewport selection, and preview meshes are transient UI or rendering state. Saved modifier definitions and clustering geometry rules remain renderer-independent.
- If you edit how this modifier works in code, also update this document to reflect the new behaviour. If you edit this document, also update the code to reflect the new behaviour. This document is part of the contract for how the modifier works and should not be allowed to diverge from the implementation.

# Clustering Logic

## Cluster Geometry

Each original support contributes a fixed model contact, penetration tip, head direction, head dimensions, and head-joint position. Clustering must not move or merge those model contacts.

For each valid cluster:

1. Preserve every original head from its head joint to its model contact.
2. Choose one central XY position for the shared stem.
3. Place the shared stem base at that XY position on the build plane.
4. Choose a common junction height below every member's head joint.
5. Connect the junction to each preserved head joint with an individual branch.
6. Build a tapered central stem using the calculated or manual top and bottom diameters.
7. Blend branch-to-junction connections into a printable, watertight assembly.

The initial central XY candidate should be the centroid of member head-joint positions, then be refined or replaced with a nearby feasible point when necessary. Every member must remain within Maximum Cluster Radius of the final stem axis. This bounded-radius rule prevents single-link chaining, where a long row of pairwise-near supports incorrectly becomes one very wide cluster.

For horizontal offset **r**, head-joint height **h**, maximum branch angle **a**, and maximum branch length **L**, a junction height **z** is feasible only when:

    atan2(r, h - z) <= a
    sqrt(r^2 + (h - z)^2) <= L
    z < h

The planner should choose the highest common junction that satisfies every member and leaves enough room for a printable junction. If no common height exists, it should remove the least suitable member and retry, split the candidate into smaller clusters, or leave the supports individual.

The central stem and every branch must maintain the configured model clearance and must not pass through the model. A bounded search may test nearby central positions and feasible junction heights. Failure to find a collision-free result must leave the affected supports unchanged.

## Grouping Rules

Eligible members must:

- belong to the support layer being modified
- exist at the current point in the ordered modifier pipeline
- have an individual stem/head definition that can be redirected
- have finite, valid geometry
- satisfy the current scope
- not be consumed by an incompatible earlier modifier

A practical deterministic first implementation is:

1. Find neighbors with a spatial grid rather than comparing every support with every other support.
2. Choose the unassigned support with the most eligible nearby neighbors; break ties by stable support identity.
3. Add nearest candidates one at a time.
4. Recalculate the proposed center and junction after each addition.
5. Accept an addition only when the entire resulting group remains within every geometric and member-count limit.
6. Finalize the group when no more candidates can be added.
7. Leave groups below the minimum size unchanged.

This favors predictable and bounded behavior over globally optimal packing. More sophisticated optimization can be added later without changing the modifier contract.

## Modifier Ordering And Dependencies

Cluster modifiers run in creation order with the rest of the support layer's modifier stack. Changing a Cluster modifier rebuilds the layer from generator output and reapplies all modifiers in order.

The initial version should not create clusters from the derived shared stems of earlier Cluster modifiers. Later Cluster modifiers may operate on individual supports left unclustered by preceding modifiers.

Editing or removing a Cluster modifier may invalidate later selection-scoped modifiers that target its derived output. Invalid downstream modifiers should be discarded with a notice as part of the same undoable command. Whole-layer downstream modifiers should be reevaluated.

## Data And Rendering Boundaries

A Cluster modifier should store user intent rather than cached meshes:

- modifier identity
- scope
- enabled state
- ordered position
- clustering parameters
- target support identities and generator revision for selection scope

The evaluated result should use renderer-independent cluster assembly data describing the shared base, stem, junction, branches, and preserved heads. The current single-head **SupportEntity** shape should not be stretched to become the authoritative definition of a multi-head cluster.

Initial implementation note: the first shipped implementation stores Cluster modifiers as durable renderer-independent intent and evaluates them through the shared support modifier pipeline, but renders each clustered member through the existing branched `SupportEntity` primitive. Clustered members preserve their original contacts and share the same planned stem XY axis and junction height; a future multi-head cluster assembly type should replace this render representation when the renderer can consume shared-stem assemblies directly.

Rendering should visualize final evaluated assemblies and transient previews only. Clustering, feasibility checks, modifier ordering, persistence, and undo behavior must not depend on WPF, HelixToolkit, SharpDX, or scene objects.

## Undo, Reset, And Failure Behaviour

Creating, editing, resetting, or removing a Cluster modifier must be one undoable document command. Preview changes create no commands.

Reset affects the modifier currently being edited. **Reset All Edits** belongs to the parent support layer and removes its complete modifier stack.

Invalid numeric settings should be rejected without changing the document. Partial geometry failures should not fail the whole modifier: valid clusters may be created while rejected candidates remain individual, with the outcome reported to the user.

## Future Considerations

- user-pinned cluster membership or manually positioned shared stems
- optional weighting of center placement by support size or estimated load
- alternate multi-level branching for large clusters
- drainage-aware scoring that favors open channels beneath the model
- collision checks against other support layers
- modifier enable/disable and drag reordering
- visual diagnostics for the specific rule that rejected a candidate
