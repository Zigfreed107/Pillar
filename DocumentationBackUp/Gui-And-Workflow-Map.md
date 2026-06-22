# GUI and Workflow Map

This document is a naming map for the main application shell. It connects the human-readable GUI names to the WPF controls, view models, tools, and workflow classes that currently make them work.



## GUI Panel Map

### 1. Main Window

- Human name: Main Window / CAD Workspace
- Code names: `MainWindow`
- Files: `src/Pillar.UI/MainWindow.xaml`, `src/Pillar.UI/MainWindow.xaml.cs`
- Contains: Top Application Chrome, Viewport Area, Viewport Overlay Panel Strip, Right Side Panel, Bottom Chrome
- Purpose: Hosts the whole application shell: menu, toolbar, viewport, overlay panels, properties panel, and status bar.

### 1.1 Top Application Chrome

- Human name: Top Application Chrome
- Code names: Main `DockPanel`, top `Menu`, `ToolBarTray`
- Contains: File Menu, Main Toolbar
- Purpose: Holds application-level controls above the viewport.

### 1.1.1 File Menu

- Human name: File Menu
- Code names: `MenuItem Header="_File"`, `NewProjectMenuItem_Click`, `OpenProjectMenuItem_Click`, `SaveProjectMenuItem_Click`, `ImportStlButton_Click`
- Contains: New, Open, Save, Import Model
- Purpose: Starts document-level workflows such as new, open, save, and model import.

### 1.1.2 Main Toolbar

- Human name: Main Toolbar
- Code names: `MainToolbar`, `UndoButton`, `RedoButton`
- Contains: Undo, Redo
- Purpose: Hosts application-level commands that are not specific to one viewport tool.

### 1.2 Viewport Area

- Human name: Viewport Area
- Code names: Main viewport `Grid`, `Viewport`, `ViewportOverlayCanvas`
- Contains: 3D Viewport, Viewport Overlay Canvas, Viewport Overlay Panel Strip
- Purpose: Holds the interactive 3D scene and the floating viewport UI.

### 1.2.1 3D Viewport

- Human name: 3D Viewport
- Code names: `Viewport`, `HelixToolkit.Wpf.SharpDX.Viewport3DX`
- Contains: Orthographic camera, ambient light, directional lights, rendered document visuals
- Purpose: Displays imported models, support geometry, previews, lights, camera aids, and viewport diagnostics.

### 1.2.2 Viewport Overlay Canvas

- Human name: Viewport Overlay Canvas
- Code names: `ViewportOverlayCanvas`, `SelectionWindowOverlay`, `SelectionWindowOverlayController`
- Contains: Selection window rectangle
- Purpose: Hosts screen-space viewport overlays, currently the drag-selection rectangle.

### 1.3 Viewport Overlay Panel Strip

- Human name: Viewport Overlay Panel Strip
- Code names: Floating overlay `Grid` in `MainWindow.xaml`
- Contains: Layer Panel, Mode Panel, Tool Options Panel
- Purpose: Groups the floating panels that sit over the viewport. It currently starts at the top-left of the viewport.

### 1.3.1 Layer Panel

- Human name: Layer Panel
- Code names: `LayerPanelOverlay`, `Pillar.UI.Layers.LayerPanel`, `LayerPanelViewModel`, `LayerTreeItemViewModel`
- Files: `src/Pillar.UI/Layers/LayerPanel.xaml`, `src/Pillar.UI/Layers/LayerPanel.xaml.cs`, `src/Pillar.ViewModels/LayerPanelViewModel.cs`, `src/Pillar.ViewModels/LayerTreeItemViewModel.cs`
- Contains:
  - Import model button
  - Remove selected model button
  - Add support group button
  - Remove support group button
  - Layer tree: imported model rows, support group child rows, support group color buttons, support group rename context menu
- Purpose: Shows the document structure from a user's point of view. Imported models are top-level layers, and support groups are child layers under a model.

### 1.3.2 Mode Panel

- Human name: Mode Panel
- Code names: `WorkflowModePanelOverlay`, `Pillar.UI.Modes.ModePanel`, `WorkspaceModeId`, `WorkspaceModeDefinition`
- Files: `src/Pillar.UI/Modes/ModePanel.xaml`, `src/Pillar.UI/Modes/ModePanel.xaml.cs`, `src/Pillar.UI/Modes/WorkspaceModeId.cs`, `src/Pillar.UI/Modes/WorkspaceModeDefinition.cs`
- Contains:
  - Import prompt
  - Selection prompt
  - Transform tab: Translate, Rotate, Scale
  - Supports tab: Point, Line, Ring
  - Multi-model selection prompt
- Purpose: Lets the user choose the current workflow area and tool. It decides what the user wants to do, not the detailed settings for that tool.

### 1.3.3 Tool Options Panel

- Human name: Tool Options Panel
- Code names: `ToolOptionsPanelOverlay`, `Pillar.UI.Modes.ToolOptionsPanel`
- Files: `src/Pillar.UI/Modes/ToolOptionsPanel.xaml`, `src/Pillar.UI/Modes/ToolOptionsPanel.xaml.cs`
- Contains:
  - Title: Tool Options Panel
  - Selected Tool label
  - Ring Support options: Spacing
- Purpose: Shows settings for the selected tool. It is hidden until a tool is selected. Right now, Ring Support is the first tool with a visible option: Spacing.

### 1.4 Right Side Panel

- Human name: Right Side Panel
- Code names: Right-side `Border`, right-side `DockPanel`
- Contains: Properties Panel
- Purpose: Holds persistent object/property editing UI beside the viewport.

### 1.4.1 Properties Panel

- Human name: Properties Panel
- Code names: `PropertiesPanelTitle`, `PropertiesPanelText`, `SelectedEntityType`, `SelectedEntityName`, `SelectedEntityNameTextBox`, `MainViewModel`
- Files: `src/Pillar.UI/MainWindow.xaml`, `src/Pillar.ViewModels/MainViewModel.cs`
- Contains: Selected entity summary, selected entity name editor
- Purpose: Shows properties for the currently selected document entity. This is for selected object data, not active tool settings.

### 1.5 Bottom Chrome

- Human name: Bottom Chrome
- Code names: Bottom `StatusBar`
- Contains: Status Bar
- Purpose: Holds persistent feedback at the bottom of the application shell.

### 1.5.1 Status Bar

- Human name: Status Bar
- Code names: `StatusText`, `MainViewModel`
- Contains: Current status message
- Purpose: Shows short workflow messages, errors, and command feedback.


## Main Workflow Concepts


Application Shell
  MainWindow
    Owns service composition.
    Wires WPF controls to document, rendering, command, selection, layer, and tool services.

  MainViewModel
    Owns shell text/state for:
      WindowTitle
      ToolPanelTitle / ToolPanelText
      PropertiesPanelTitle / PropertiesPanelText
      StatusText
      SelectedEntityType
      SelectedEntityName

Document Model
  CadDocument
    Owns document entities and support layer groups.

  CadEntity
    Base type for document objects.

  MeshEntity
    Imported model geometry.

  SupportEntity
    One resin support entity.

  SupportLayerGroup
    User-visible support grouping under a model layer.

Layer Workflow
  LayerPanel
    WPF control for the layer overlay.
    Converts GUI gestures into shell events.

  LayerPanelViewModel
    Builds the layer tree from CadDocument.
    Tracks selected model/support layer context.

  LayerTreeItemViewModel
    Represents one row in the layer tree.

  MainWindow.LayerPanel.cs
    Applies layer panel requests through undoable commands.

Mode and Tool Workflow
  WorkspaceModeId
    Stable ids for high-level modes:
      Select
      Line
      Transform
      ManualSupport

  WorkspaceModeDefinition
    Pairs a mode id with:
      Display name
      Status text
      Availability
      Viewport tool

  MainWindow.WorkspaceModes.cs
    Registers modes.
    Switches active mode.
    Synchronizes Mode Panel support operation state.
    Shows/hides Tool Options Panel.

  ToolManager
    Owns the active ITool.
    Cancels transient state when modes change.

  ITool
    Contract for a viewport tool:
      OnMouseDown
      OnMouseMove
      OnMouseUp
      Cancel

  IToolOperation
    Contract for an operation inside a tool.
    Used when one mode/tool owns multiple sub-tools or operations.

Manual Support Workflow
  ManualSupportTool
    Active viewport tool for Manual Support mode.
    Owns the selected ManualSupportOperationKind.
    Routes mouse input to the active support operation.

  ManualSupportOperationKind
    Selectable support operations:
      None
      Point
      Line
      Ring

  PointSupportOperation
    Current implemented support operation.
    Converts a click on the active model surface into one SupportEntity.

  Ring Support
    Current GUI state:
      Selectable in Mode Panel.
      Shows Spacing in Tool Options Panel.
    Current implementation state:
      RingSupportOperation creates a projected ring of individual supports from three surface picks.
      ManualSupportTool creates PointSupportOperation for Point and RingSupportOperation for Ring.


## How The Panels Relate In The User Workflow


1. User imports a model
   File Menu or Layer Panel
     -> DocumentFileService / import command flow
     -> CadDocument receives MeshEntity
     -> LayerPanelViewModel refreshes
     -> Layer Panel shows imported model row

2. User selects a model layer
   Layer Panel
     -> LayerPanelViewModel.SelectedLayer
     -> Mode Panel workflow tabs become visible

3. User chooses a workflow/tool
   Mode Panel
     -> ToolSelected event
     -> MainWindow.ShowToolOptionsPanel(...)
     -> Tool Options Panel appears with selected tool settings

4. User chooses a support operation
   Mode Panel Supports tab
     -> SupportOperationToggleRequested event
     -> MainWindow.ApplyManualSupportOperationSelection(...)
     -> ManualSupportTool.SetActiveOperation(...)
     -> ToolManager routes viewport input to ManualSupportTool

5. User interacts with the viewport
   Viewport mouse events
     -> MainWindow.ViewportInteraction.cs
     -> ToolManager.ActiveTool
     -> Active ITool / IToolOperation
     -> CadCommandRunner for durable document edits
     -> SceneManager renders document changes


## Naming Rules To Keep Using


Panel
  Use for visible GUI areas, especially WPF UserControls.
  Examples:
    LayerPanel
    ModePanel
    ToolOptionsPanel

Overlay
  Use for an instance of a panel floating over the viewport.
  Examples:
    LayerPanelOverlay
    WorkflowModePanelOverlay
    ToolOptionsPanelOverlay

Mode
  Use for a high-level application workflow state.
  Examples:
    Transform
    ManualSupport

Tool
  Use for a viewport interaction object that receives mouse input.
  Examples:
    SelectTool
    LineTool
    ManualSupportTool

Operation
  Use for a selectable sub-tool inside a larger tool.
  Examples:
    PointSupportOperation
    Future RingSupportOperation

Settings / Options
  Use Tool Options for active tool parameters.
  Avoid putting per-tool options directly into ModePanel.


## Common Confusions


Mode Panel vs Tool Options Panel
  Mode Panel chooses what tool the user wants.
  Tool Options Panel edits settings for that selected tool.

Layer Panel vs Properties Panel
  Layer Panel organizes the document tree.
  Properties Panel edits the currently selected entity.

ManualSupportTool vs PointSupportOperation
  ManualSupportTool is the mode-level viewport tool.
  PointSupportOperation is one specific operation inside ManualSupportTool.

Support group vs support entity
  SupportLayerGroup is the layer/group container.
  SupportEntity is an actual support in the document.

