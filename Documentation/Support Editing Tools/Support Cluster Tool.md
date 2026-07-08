# Support Clustering

Support Clustering replaces several nearby individual support stems and bases with a shared central stem. Each original model contact and head is preserved, while new branches connect those heads to the shared stem.

The main goals are to:

- reduce the number of stems and bases that must be removed after printing
- create more open space beneath the model for uncured resin to drain
- preserve the original support contact positions
- avoid weakening a support group through branches that are too long, too shallow, or obstructed by the model

## GUI

The Tool Options Panel should show:

- **Maximum Cluster Radius**: maximum horizontal distance from a cluster's central stem axis to any member's head joint
- **Minimum Supports Per Cluster**: normally two; smaller candidate groups remain unchanged
- **Maximum Supports Per Cluster**: limits branch crowding and shared-stem loading
- **Maximum Branch Angle From Vertical**: prevents shallow, difficult-to-print branches
- **Stem Sizing**: **Automatic** or **Manual**. Automatic should populate the control with the calculated value, but the control is disabled until Manual is selected. Manual sizing must still pass validation to prevent unprintable results, with an error dialog explaining the failure.
- **Central Stem Bottom Diameter** and **Central Stem Top Diameter**: editable when manual sizing is selected
- **Preview**: enabled by default and updated when parameters change
- **Uncluster Selected**: enabled only when one or more clusters are selected in the viewport, removing those clusters and returning their members to individual supports in one undoable command
- **Apply to Selected**: captures the currently selected eligible supports in the selected support layer and applies clustering to that target set
- **Apply to All**: captures every eligible support in the selected support layer and applies the same revision-bound path as **Apply to Selected**
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

The preview should use the current support selection when it resolves to one or more eligible supports. With no eligible selected supports, it previews all eligible supports in the selected layer. **Apply to Selected** remains disabled until at least two eligible supports are selected. **Apply to All** is enabled when the selected support layer contains at least two eligible supports.

The status text should report the number of proposed clusters, clustered supports, unchanged supports, and rejected candidates. Rejected supports should remain printable individual supports rather than disappearing.

## Workflow

The user selects a support layer, opens **Edit Supports** mode, and chooses **Cluster Supports**. If more than one layer is selected, a warning dialog instructs the user to select only one support layer and try again.

All other support layers are hidden until the user exits the tool, then their visibility is restored.

1. Confirm that one support layer is selected and contains at least two eligible individual supports.
2. Read the support geometry produced by the generator and any preceding modifiers.
3. Capture target support identities from either the viewport selection or all supports in the layer.
4. Capture the support layer's current source generator revision.
5. Find nearby candidates using a spatial index in the XY plane.
6. Process candidates in a stable order so the same inputs and parameters always produce the same clusters.
7. Build each candidate cluster without exceeding the maximum member count or maximum cluster radius.
8. Calculate a proposed shared-stem position and validate its branches.
9. Reject or reduce candidate clusters that fail branch length, branch angle, model-clearance, or printable-diameter checks.
10. Preview every valid cluster while leaving unclustered supports visible in their original state.
11. On Apply, create or update one revision-bound Cluster modifier containing ordered target batches, source revision, parameters, and modifier order.
12. Rebuild the support layer from its generator output and complete modifier stack.
13. Add or update one child row such as **Cluster (12)** beneath the support layer.

Normal tool-session Apply clicks append the newly captured target identities as a separate batch inside the existing Cluster modifier for that support layer instead of appending another Cluster modifier row. **Apply to All** does not create a different modifier type; it simply captures all current supports as the next target batch.

Only targets in the captured support layer participate. Untargeted supports must not be absorbed merely because they are nearby.

Selecting any member of an existing clustered shared-stem assembly counts as selecting that whole cluster for Cluster Supports operations. This keeps the shared stem internally consistent and avoids editing only one branch of a cluster.

When the captured target set includes both individual supports and existing clustered supports, Apply first tries to merge selected individual supports into the selected existing cluster or clusters. If an individual support can fit more than one selected cluster, it joins the nearest feasible selected cluster with stable identity ordering used only as a tie-break. Selected individual supports that cannot join a selected cluster remain eligible to form new clusters with the other remaining selected individual supports.

A user should be able to choose an individual cluster in the viewer and click **Uncluster Selected**. This removes the clustering from all supports in that cluster, returning them to individual supports.

A Cluster modifier survives save and load while its generator revision and target identities remain valid. If generator settings are changed, Cluster modifiers tied to the previous revision are discarded with a user notice. The generator update, modifier removal, and regenerated support output must be one undoable command so Undo restores the original supports and Cluster modifier together.

Reset removes the selected Cluster modifier and rebuilds the layer from the original generator output plus the remaining modifiers. The affected supports return to the individual state produced by preceding modifiers. If this was the final modifier, its child row disappears and the support layer has no modifier stack children.

# Notes

- Refer to **Documentation\Support Editing Tools\Support Editing Mode Behaviours.md** for wider context as to how this tool fits into the overall support editing workflow.
- Tool controls, viewport selection, and preview meshes are transient UI or rendering state. Saved modifier definitions and clustering geometry rules remain renderer-independent.
- If you edit how this modifier works in code, also update this document to reflect the new behaviour. If you edit this document, also update the code to reflect the new behaviour.

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
- be included in the captured target set
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

The initial version keeps manual clustering for a support layer in one cumulative Cluster modifier. Repeated Apply clicks append separate target batches to that modifier, so the Layer Panel shows one Cluster child row while Undo and Redo still step through each Apply command. Each batch is evaluated in Apply order; neighboring supports selected in different Apply clicks should not be regrouped together unless a later batch explicitly selects an existing clustered member and another support to merge them.

Editing or removing a Cluster modifier may invalidate later modifiers that target its derived output. Invalid downstream modifiers should be discarded with a notice as part of the same undoable command.

## Data And Rendering Boundaries

A Cluster modifier should store user intent rather than cached meshes:

- modifier identity
- enabled state
- ordered position
- clustering parameters
- ordered target support identity batches
- source generator revision

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