# Support Bracing & Buttressing Tool

Support Bracing connects neighbouring supports using angled cross members to form a truss-like structure. Buttressing adds secondary reinforcing supports to tall stems. Both workflows operate as support-layer modifiers inside **Edit Supports** mode.

The main goals are to:

- connect nearby support stems so they reinforce each other and better resist warping or bending
- strengthen tall supports without increasing the primary stem diameter so much that removal or resin usage becomes excessive

## GUI

The Tool Options Panel should show from top to bottom:

- **Bracing** section header label
  - **Maximum Brace Angle** numeric control (min: 10, max: 80, default: 70). A brace cross member will not be created if the angle between it and the horizontal plane is greater than this.
  - **Minimum Brace Angle** numeric control (min: 10, max: 80, default: 50). A brace cross member will not be created if the angle between it and the horizontal plane is less than this.
  - **Maximum Brace Length** numeric control (min: 0, default: 10). A brace cross member will not be created if it would exceed this length.
  - **Diameter** numeric control. The diameter of the brace cross member.
  - **Brace Selected** button. Captures the currently selected eligible supports and appends one selected-only Brace modifier using the current parameters. Existing braces whose two endpoints are selected are replaced; selected-to-unselected and unselected-to-unselected braces remain owned by their existing modifiers.
  - **Brace All** button. Captures every eligible support in the selected support layer and applies one revision-bound Brace modifier to the complete layer.
  - **Remove Bracing From Selected** button. When editing an existing Brace modifier, removes bracing affecting the selected captured targets from that modifier and rebuilds the support layer. If no brace targets remain in that modifier, the modifier is removed.
  - **Remove All Bracing** button. Removes every Brace modifier from the selected support layer and rebuilds through the surviving modifier stack as one undoable action.

- **Buttress** section header label
  - **Buttress supports taller than** numeric control (min: 0, default: 10). Only supports taller than this will be buttressed.
  - **Buttress spacing** numeric control (min: 0, default: 2). This is the side length of the equilateral plan triangle formed by the original support and both buttress bases.
  - **Buttress Selected** button. Captures the currently selected eligible supports and the current Bracing and Buttress parameters, then applies one undoable Buttress modifier.
  - **Buttress All** button. Captures every eligible support in the selected support layer that is taller than the configured threshold and applies the same revision-bound Buttress modifier path as **Buttress Selected**.
  - **Remove Buttressing From Selected** button. When editing an existing Buttress modifier, removes buttressing affecting the selected captured targets from that modifier and rebuilds the support layer. If no buttress targets remain in that modifier, the modifier is removed.
  - **Remove All Buttresses** button. Removes every Buttress modifier from the selected support layer and rebuilds through the surviving modifier stack as one undoable action.

- **Close**. Closes the tool and options panel. Closing leaves the document unchanged when creating a modifier, or discards uncommitted parameter changes when editing one. Any support layers hidden by the tool are restored to their prior visibility state.

## Workflow

The user selects a support layer, opens **Edit Supports** mode, and chooses **Brace**.

If more than one support layer is selected, a warning dialog should instruct the user to select only one support layer and try again.

All other support layers are hidden until the user exits the tool, then their visibility is restored. The tool may also hide unrelated model context if needed to keep the edited support layer readable.

1. Confirm that exactly one support layer is selected.
2. Read the support geometry produced by the source generator and any preceding modifiers.
3. Capture target support identities from either the viewport selection or all eligible supports in the selected layer.
4. Capture the support layer's current source generator revision.
5. Evaluate and preview brace or buttress results only against supports that exist at the current point in the ordered modifier pipeline.
6. On Apply, create a revision-bound Brace or Buttress modifier storing modifier identity, modifier type, parameters, target support identities, ordering, and source generator revision.
7. Rebuild the support layer from its generator output and complete modifier stack.
8. Add a child row such as **Brace (8)** or **Buttress (5)** beneath the support layer in the Layer Panel.

Different parameter sets should remain independently editable as separate modifier rows. Selecting a modifier's edit button should activate **Edit Supports** mode, reopen this tool, open the options panel, and restore that modifier's saved parameters and captured targets.

Repeated **Brace Selected** operations are pair-scoped. Earlier Brace modifiers store durable exclusions only for pairs whose two endpoints were selected by the later operation. The later selected-only modifier then regenerates those pairs with its captured parameters. A pair with one selected and one unselected endpoint is not excluded or regenerated.

If the user edits a Brace or Buttress modifier that has later modifiers underneath it in the stack, the UI must warn that applying the edit will delete those downstream modifiers and offer Cancel.

## Bracing Logic

Bracing connects neighbouring supports using cross members by attempting to join the centre of the top of the base of one support to the centre of the top of the stem of another.

The rule is generator-independent and applies to ordinary individual supports in any selected support layer, including point, line, ring, contour, area, and future generator types. Clustered supports and generated reinforcement members are not eligible bracing targets.

Neighbourhood is evaluated in the XY plane among the feasible supports captured by the modifier. A candidate pair A-B is not generated when another captured support C can form feasible pairs with both endpoints and is strictly closer in XY to both A and B. Equal-distance neighbours remain eligible. This prevents braces from skipping an adjacent support to reach one farther away while allowing the next feasible neighbour when a nearer pair fails the angle or length constraints.

For each unordered support pair, both base-top-to-stem-top brace directions are evaluated. The shortest valid direction is selected deterministically before relative-neighbourhood filtering and the connection limit are applied.

If enough stem height remains above the first brace, one additional return member is generated. It begins at the first brace's endpoint on the neighbouring stem and rises back toward the original stem. The return member must independently satisfy the minimum angle, maximum angle, and maximum length rules. Both members count as one support-to-support connection for the three-neighbour limit.

- If the brace would have an angle to the XY plane less than **Minimum Brace Angle**, it is not generated.
- If the brace would have an angle to the XY plane greater than **Maximum Brace Angle**, then instead of being joined to the top of the neighbouring stem, it joins where the angle formed is the configured maximum.
- If the brace would exceed **Maximum Brace Length**, it is not generated.
- The brace diameter is set by the **Diameter** numeric control.
- A support can be joined by braces to no more than three other supports.

## Buttressing Logic

Buttressing one eligible support creates two secondary buttress supports.

Each buttress:

- has a normal printable base and vertical stem
- has no model-contact head
- has a branch from its stem top to the top of the original support's stem
- sets that branch's angle to the XY plane to the captured **Minimum Brace Angle**
- copies the original support's base-bottom radius, base height, stem-bottom diameter, stem-top diameter, and resolved branch diameter
- is additionally braced to the original support using the captured minimum angle, maximum angle, maximum length, and brace diameter parameters

For each original-to-buttress connection, the lower brace starts at the top of the buttress base and rises toward the original support stem. When another rising member fits above it, that member returns from the first connection point to the top of the buttress stem to form a zig-zag. If the return member does not fit the captured angle or length limits, the valid lower brace is retained by itself.

The two buttresses are braced to each other using the same pattern: a lower member rises from the first buttress base top toward the second buttress stem, followed where feasible by a return member to the top of the first buttress stem.

The two buttress bases and the original support base occupy the vertices of an imaginary equilateral triangle in plan view:

- every side length equals **Buttress spacing**
- the midpoint between the two buttress bases lies behind the original support, opposite its head direction
- the two buttress bases are mirrored to either side of that rear direction
- the triangle is a placement rule only and is not saved or rendered as geometry

Only ordinary individual supports are eligible for buttressing. Clustered supports, generated buttress supports, and generated brace members are excluded. If a support is too short to contain a full base, stem, and branch at the configured minimum angle, no buttress pair is generated for that target.

# Notes

- Refer to **Documentation\\Support Editing Tools\\Support Editing Mode Behaviours.md** for the wider modifier-stack contract.
- Brace and Buttress entries are support-layer modifiers shown as child rows beneath their owning support layer. They are not independent support layers, rendered scene objects, or cached meshes.
- Modifier definitions are saved and remain editable only while their captured source generator revision and target support identities remain valid.
- Editing source support generator settings regenerates the layer, advances the generator revision, discards all existing modifiers tied to the previous revision, and reports that removal to the user. The generator change, regenerated supports, and modifier removal must be one undoable command.
- Before opening a support generator editor for a layer that already has modifiers, the UI must warn that editing the support layer will delete all modifiers below it and offer Cancel.
- If an earlier topology-changing modifier is edited or removed, later modifiers may lose valid targets. Only invalid downstream modifiers should be discarded, with a user notice, as part of the same undoable command.
- Removing a modifier from the Layer Panel removes that modifier and rebuilds the support layer from the source generator plus any remaining modifiers.
- When clustering succeeds for supports targeted by existing Brace or Buttress modifiers, those identities are removed from the reinforcement modifiers. Modifiers with too few remaining targets are removed after confirmation. The cluster, cleanup, and rebuilt output are one undoable command.
- **Remove All Bracing** and **Remove All Buttresses** are type-specific, layer-scoped actions. They preserve modifiers of every other kind and can be undone or redone without closing the tool.
- Undo and Redo keep the active Brace or Buttress tool and its options panel open. The tool preserves uncommitted option values, refreshes its support-layer context, and enables modifier-only actions only when the edited modifier exists in the restored history state.
- Tool controls, viewport selection, and preview geometry are transient UI or rendering state. Saved brace and buttress definitions must remain renderer-independent.
