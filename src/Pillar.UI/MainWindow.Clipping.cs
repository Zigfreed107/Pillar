// MainWindow.Clipping.cs
// Wires the viewport Z clipping overlay to render-layer clipping planes while keeping drag UI logic out of rendering code.
using Pillar.Core.Entities;
using Pillar.UI.Controls;
using System;
using System.ComponentModel;
using System.Numerics;
using System.Windows.Media;

namespace Pillar.UI;

public partial class MainWindow
{
    private MeshEntity? _selectedModelBoundsClipIndicatorMesh;
    private Brush? _selectedModelBoundsClipIndicatorBrush;

    /// <summary>
    /// Initializes the Z clipping overlay from the printable volume configured for the viewport grid.
    /// </summary>
    private void InitializeModelClippingControls()
    {
        ClipRangeSliderOverlay.ClipRangeChanged += ClipRangeSliderOverlay_ClipRangeChanged;
        ClipRangeSliderOverlay.ConfigureRange(
            0.0,
            _printableVolumeDefinition.ZDistance,
            0.0,
            _printableVolumeDefinition.ZDistance);
        UpdateSelectedModelBoundsClipIndicator();
    }

    /// <summary>
    /// Applies the selected visible Z range to all renderable mesh visuals.
    /// </summary>
    private void ClipRangeSliderOverlay_ClipRangeChanged(object? sender, ClipRangeChangedEventArgs e)
    {
        _ = sender;
        _scene.ConfigureModelClipRange((float)e.LowerZ, (float)e.UpperZ);
        _viewModel.SetStatusText($"Visible Z range: {e.LowerZ:F1} mm to {e.UpperZ:F1} mm");
    }

    /// <summary>
    /// Refreshes the slider's selected-model height marker after viewport or layer selection changes.
    /// </summary>
    private void UpdateSelectedModelBoundsClipIndicator()
    {
        MeshEntity? selectedMesh = GetSingleSelectedMeshEntity();
        SetSelectedModelBoundsClipIndicatorMesh(selectedMesh);

        if (selectedMesh == null)
        {
            ClipRangeSliderOverlay.HideSelectedModelBounds();
            return;
        }

        (Vector3 Min, Vector3 Max) bounds = selectedMesh.GetBounds();
        ClipRangeSliderOverlay.ShowSelectedModelBounds(
            bounds.Min.Z,
            bounds.Max.Z,
            GetSelectedModelBoundsClipIndicatorBrush());
    }

    /// <summary>
    /// Keeps the selected model bounds marker live while the active model is transformed.
    /// </summary>
    private void SelectedModelBoundsClipIndicatorMesh_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        _ = sender;

        if (string.Equals(e.PropertyName, nameof(MeshEntity.ImportPlacementTransform), StringComparison.Ordinal)
            || string.Equals(e.PropertyName, nameof(MeshEntity.UserTransform), StringComparison.Ordinal)
            || string.Equals(e.PropertyName, nameof(MeshEntity.WorldTransform), StringComparison.Ordinal))
        {
            UpdateSelectedModelBoundsClipIndicator();
        }
    }

    /// <summary>
    /// Tracks property changes only on the one mesh currently represented by the clip slider indicator.
    /// </summary>
    private void SetSelectedModelBoundsClipIndicatorMesh(MeshEntity? selectedMesh)
    {
        if (ReferenceEquals(_selectedModelBoundsClipIndicatorMesh, selectedMesh))
        {
            return;
        }

        if (_selectedModelBoundsClipIndicatorMesh != null)
        {
            _selectedModelBoundsClipIndicatorMesh.PropertyChanged -= SelectedModelBoundsClipIndicatorMesh_PropertyChanged;
        }

        _selectedModelBoundsClipIndicatorMesh = selectedMesh;

        if (_selectedModelBoundsClipIndicatorMesh != null)
        {
            _selectedModelBoundsClipIndicatorMesh.PropertyChanged += SelectedModelBoundsClipIndicatorMesh_PropertyChanged;
        }
    }

    /// <summary>
    /// Gets the selected mesh only when exactly one selected entity is an imported model.
    /// </summary>
    private MeshEntity? GetSingleSelectedMeshEntity()
    {
        if (_scene.SelectionManager.SelectedCount != 1)
        {
            return null;
        }

        Guid? selectedId = GetSingleSelectedEntityId();

        if (!selectedId.HasValue)
        {
            return null;
        }

        return FindEntityById(selectedId.Value) as MeshEntity;
    }

    /// <summary>
    /// Lazily creates the slider marker brush from the same application setting used for model mesh materials.
    /// </summary>
    private Brush GetSelectedModelBoundsClipIndicatorBrush()
    {
        if (_selectedModelBoundsClipIndicatorBrush == null)
        {
            _selectedModelBoundsClipIndicatorBrush = ReadDefaultModelBrush();
        }

        return _selectedModelBoundsClipIndicatorBrush;
    }
}
