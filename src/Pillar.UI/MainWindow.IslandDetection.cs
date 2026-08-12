// MainWindow.IslandDetection.cs
// Coordinates cancellable mesh island analysis, transient result caching, presentation filtering, navigation, and invalidation.
using Pillar.Core.Entities;
using Pillar.Geometry.Analysis;
using Pillar.UI.Analysis;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Media3D;

namespace Pillar.UI;

public partial class MainWindow
{
    private readonly MeshIslandAnalyzer _meshIslandAnalyzer = new MeshIslandAnalyzer();
    private readonly IslandDetectionSettings _islandDetectionSettings = new IslandDetectionSettings();
    private readonly List<IslandCandidate> _visibleIslandCandidates = new List<IslandCandidate>();
    private IslandDetectionPanel? _islandDetectionPanel;
    private CancellationTokenSource? _islandDetectionCancellation;
    private IslandDetectionResult? _islandDetectionResult;
    private MeshEntity? _islandDetectionMesh;
    private int _activeIslandCandidateIndex;
    private bool _isIslandDetectionPanelOpen;

    /// <summary>
    /// Creates the focused result panel and connects session-level invalidation events.
    /// </summary>
    private void InitializeIslandDetectionControls()
    {
        _islandDetectionPanel = new IslandDetectionPanel();
        _islandDetectionPanel.PreviousRequested += IslandDetectionPanel_PreviousRequested;
        _islandDetectionPanel.NextRequested += IslandDetectionPanel_NextRequested;
        _islandDetectionPanel.RefreshRequested += IslandDetectionPanel_RefreshRequested;
        _islandDetectionPanel.CloseRequested += IslandDetectionPanel_CloseRequested;
        _islandDetectionPanel.CancelRequested += IslandDetectionPanel_CancelRequested;
        _islandDetectionPanel.FiltersChanged += IslandDetectionPanel_FiltersChanged;
        IslandDetectionHostOverlay.Content = _islandDetectionPanel;
        _document.EntitiesChanged += Document_IslandDetectionEntitiesChanged;
        UpdateIslandDetectionToolbarState();
    }

    /// <summary>
    /// Releases island workflow subscriptions and cancels any outstanding background run.
    /// </summary>
    private void DisposeIslandDetectionControls()
    {
        CancelIslandDetectionAnalysis();
        _document.EntitiesChanged -= Document_IslandDetectionEntitiesChanged;
        SetIslandDetectionMesh(null);
        _scene.HideIslandDetectionPreview();

        if (_islandDetectionPanel != null)
        {
            _islandDetectionPanel.PreviousRequested -= IslandDetectionPanel_PreviousRequested;
            _islandDetectionPanel.NextRequested -= IslandDetectionPanel_NextRequested;
            _islandDetectionPanel.RefreshRequested -= IslandDetectionPanel_RefreshRequested;
            _islandDetectionPanel.CloseRequested -= IslandDetectionPanel_CloseRequested;
            _islandDetectionPanel.CancelRequested -= IslandDetectionPanel_CancelRequested;
            _islandDetectionPanel.FiltersChanged -= IslandDetectionPanel_FiltersChanged;
        }
    }

    /// <summary>
    /// Starts analysis from the toolbar without changing the active viewport tool.
    /// </summary>
    private async void IslandDetectionLaunchButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        await StartIslandDetectionAsync(false);
    }

    /// <summary>
    /// Runs or reuses island analysis for the only selected mesh.
    /// </summary>
    private async Task StartIslandDetectionAsync(bool forceRefresh)
    {
        MeshEntity? selectedMesh = GetSingleSelectedMeshEntity();

        if (selectedMesh == null)
        {
            _viewModel.SetStatusText("Select one model to detect islands");
            return;
        }

        OpenIslandDetectionPanel();

        if (!forceRefresh && IsCachedIslandResultValid(selectedMesh))
        {
            RefreshIslandDetectionPresentation(true);
            _viewModel.SetStatusText(CreateIslandStatusText(_islandDetectionResult!));
            return;
        }

        CancelIslandDetectionAnalysis();
        SetIslandDetectionMesh(selectedMesh);
        _islandDetectionResult = null;
        _visibleIslandCandidates.Clear();
        _scene.HideIslandDetectionPreview();
        CancellationTokenSource cancellation = new CancellationTokenSource();
        _islandDetectionCancellation = cancellation;
        IProgress<IslandDetectionProgress> progress = new Progress<IslandDetectionProgress>(UpdateIslandDetectionProgress);
        _islandDetectionPanel?.SetRunning(new IslandDetectionProgress(
            IslandDetectionStage.TransformingVertices,
            0.0,
            "Preparing mesh analysis"));
        UpdateIslandDetectionToolbarState();

        try
        {
            IslandDetectionResult result = await Task.Run(
                () => _meshIslandAnalyzer.Analyze(
                    selectedMesh,
                    _islandDetectionSettings,
                    cancellation.Token,
                    progress),
                cancellation.Token);

            if (cancellation.IsCancellationRequested
                || !ReferenceEquals(_islandDetectionMesh, selectedMesh)
                || GetSingleSelectedMeshEntity()?.Id != selectedMesh.Id
                || result.TransformSnapshot != selectedMesh.WorldTransform)
            {
                return;
            }

            _islandDetectionResult = result;
            _activeIslandCandidateIndex = 0;
            RefreshIslandDetectionPresentation(true);
            _viewModel.SetStatusText(CreateIslandStatusText(result));
        }
        catch (OperationCanceledException)
        {
            if (_isIslandDetectionPanelOpen && ReferenceEquals(_islandDetectionCancellation, cancellation))
            {
                _islandDetectionPanel?.SetMessage("Analysis cancelled. Refresh to run it again.", true);
                _viewModel.SetStatusText("Island detection cancelled");
            }
        }
        catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException)
        {
            if (_isIslandDetectionPanelOpen && ReferenceEquals(_islandDetectionCancellation, cancellation))
            {
                _islandDetectionPanel?.SetMessage("The selected model could not be analyzed.", true);
                _viewModel.SetStatusText("Island detection failed");
                MessageBox.Show(this, ex.Message, "Island Detection Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        finally
        {
            if (ReferenceEquals(_islandDetectionCancellation, cancellation))
            {
                _islandDetectionCancellation = null;
                UpdateIslandDetectionToolbarState();
            }

            cancellation.Dispose();
        }
    }

    /// <summary>
    /// Applies one marshalled coarse progress update while the panel remains open.
    /// </summary>
    private void UpdateIslandDetectionProgress(IslandDetectionProgress progress)
    {
        if (_isIslandDetectionPanelOpen && _islandDetectionCancellation != null)
        {
            _islandDetectionPanel?.SetRunning(progress);
        }
    }

    /// <summary>
    /// Rebuilds only the filtered candidate view and transient overlay.
    /// </summary>
    private void RefreshIslandDetectionPresentation(bool frameActiveCandidate)
    {
        if (_islandDetectionPanel == null
            || _islandDetectionResult == null
            || _islandDetectionMesh == null)
        {
            return;
        }

        int previousCandidateId = _activeIslandCandidateIndex >= 0
            && _activeIslandCandidateIndex < _visibleIslandCandidates.Count
            ? _visibleIslandCandidates[_activeIslandCandidateIndex].CandidateId
            : -1;
        IslandPresentationFilter filter = _islandDetectionPanel.CreateFilter();
        _visibleIslandCandidates.Clear();

        for (int candidateIndex = 0; candidateIndex < _islandDetectionResult.Candidates.Count; candidateIndex++)
        {
            IslandCandidate candidate = _islandDetectionResult.Candidates[candidateIndex];

            if (filter.Includes(candidate))
            {
                _visibleIslandCandidates.Add(candidate);
            }
        }

        if (_islandDetectionResult.Candidates.Count == 0)
        {
            string message = _islandDetectionResult.Diagnostics.HasMeshQualityWarnings
                ? "No islands found, but the mesh has quality warnings. Review diagnostics before treating it as island-free."
                : "No islands found for the selected model.";
            _islandDetectionPanel.SetMessage(message, true);
            _scene.HideIslandDetectionPreview();
            return;
        }

        if (_visibleIslandCandidates.Count == 0)
        {
            _activeIslandCandidateIndex = 0;
            _islandDetectionPanel.SetMessage("No candidates match the current presentation filters.", true);
            _scene.HideIslandDetectionPreview();
            return;
        }

        _activeIslandCandidateIndex = FindVisibleCandidateIndex(previousCandidateId);
        IslandCandidate activeCandidate = _visibleIslandCandidates[_activeIslandCandidateIndex];
        _islandDetectionPanel.SetCandidate(
            activeCandidate,
            _activeIslandCandidateIndex,
            _visibleIslandCandidates.Count,
            _islandDetectionResult.Candidates.Count,
            _islandDetectionResult.Diagnostics);
        _scene.ShowIslandDetectionPreview(
            _islandDetectionMesh,
            _visibleIslandCandidates,
            _activeIslandCandidateIndex,
            CalculateIslandMarkerRadius(_islandDetectionMesh));

        if (frameActiveCandidate)
        {
            FrameIslandCandidate(activeCandidate);
        }
    }

    /// <summary>
    /// Finds the prior candidate after filters change, falling back to the first visible result.
    /// </summary>
    private int FindVisibleCandidateIndex(int candidateId)
    {
        for (int candidateIndex = 0; candidateIndex < _visibleIslandCandidates.Count; candidateIndex++)
        {
            if (_visibleIslandCandidates[candidateIndex].CandidateId == candidateId)
            {
                return candidateIndex;
            }
        }

        return 0;
    }

    /// <summary>
    /// Moves candidate navigation by one with wraparound.
    /// </summary>
    private void NavigateIslandCandidate(int delta)
    {
        if (_visibleIslandCandidates.Count == 0)
        {
            return;
        }

        _activeIslandCandidateIndex = (_activeIslandCandidateIndex + delta + _visibleIslandCandidates.Count)
            % _visibleIslandCandidates.Count;
        IslandCandidate activeCandidate = _visibleIslandCandidates[_activeIslandCandidateIndex];
        _islandDetectionPanel?.SetCandidate(
            activeCandidate,
            _activeIslandCandidateIndex,
            _visibleIslandCandidates.Count,
            _islandDetectionResult?.Candidates.Count ?? _visibleIslandCandidates.Count,
            _islandDetectionResult!.Diagnostics);
        _scene.ShowIslandDetectionPreview(
            _islandDetectionMesh!,
            _visibleIslandCandidates,
            _activeIslandCandidateIndex,
            CalculateIslandMarkerRadius(_islandDetectionMesh!));
        FrameIslandCandidate(activeCandidate);
    }

    /// <summary>
    /// Frames one candidate's world bounds through the camera service.
    /// </summary>
    private void FrameIslandCandidate(IslandCandidate candidate)
    {
        Vector3 size = candidate.WorldBoundsMax - candidate.WorldBoundsMin;
        float padding = MathF.Max(0.5f, size.Length() * 0.15f);
        Vector3 paddedMinimum = candidate.WorldBoundsMin - new Vector3(padding);
        Vector3 paddedMaximum = candidate.WorldBoundsMax + new Vector3(padding);
        Rect3D bounds = new Rect3D(
            paddedMinimum.X,
            paddedMinimum.Y,
            paddedMinimum.Z,
            Math.Max(0.01, paddedMaximum.X - paddedMinimum.X),
            Math.Max(0.01, paddedMaximum.Y - paddedMinimum.Y),
            Math.Max(0.01, paddedMaximum.Z - paddedMinimum.Z));
        _viewportCameraService.FrameBounds(bounds);
    }

    /// <summary>
    /// Scales markers to the analyzed model without allowing them to dominate tiny or large scenes.
    /// </summary>
    private static float CalculateIslandMarkerRadius(MeshEntity mesh)
    {
        (Vector3 Min, Vector3 Max) bounds = mesh.GetBounds();
        return Math.Clamp((bounds.Max - bounds.Min).Length() * 0.0125f, 0.2f, 2.5f);
    }

    /// <summary>
    /// Checks every cache key required by the transient result contract.
    /// </summary>
    private bool IsCachedIslandResultValid(MeshEntity mesh)
    {
        return _islandDetectionResult != null
            && _islandDetectionResult.SourceModelId == mesh.Id
            && _islandDetectionResult.TransformSnapshot == mesh.WorldTransform
            && _islandDetectionResult.Settings.Equals(_islandDetectionSettings);
    }

    /// <summary>
    /// Shows the compact panel without changing pointer ownership or active mode.
    /// </summary>
    private void OpenIslandDetectionPanel()
    {
        _isIslandDetectionPanelOpen = true;
        IslandDetectionHostOverlay.Visibility = Visibility.Visible;
    }

    /// <summary>
    /// Cancels only the background analysis token owned by this workflow.
    /// </summary>
    private void CancelIslandDetectionAnalysis()
    {
        _islandDetectionCancellation?.Cancel();
    }

    /// <summary>
    /// Invalidates cached and displayed results after selection, geometry, or ownership changes.
    /// </summary>
    private void InvalidateIslandDetection(string message)
    {
        CancellationTokenSource? invalidatedCancellation = _islandDetectionCancellation;
        _islandDetectionCancellation = null;
        invalidatedCancellation?.Cancel();
        _islandDetectionResult = null;
        _visibleIslandCandidates.Clear();
        _activeIslandCandidateIndex = 0;
        _scene.HideIslandDetectionPreview();

        if (_isIslandDetectionPanelOpen)
        {
            _islandDetectionPanel?.SetMessage(message, GetSingleSelectedMeshEntity() != null);
        }
    }

    /// <summary>
    /// Tracks one model for transform invalidation while retaining a closed-panel cache.
    /// </summary>
    private void SetIslandDetectionMesh(MeshEntity? mesh)
    {
        if (ReferenceEquals(_islandDetectionMesh, mesh))
        {
            return;
        }

        if (_islandDetectionMesh != null)
        {
            _islandDetectionMesh.PropertyChanged -= IslandDetectionMesh_PropertyChanged;
        }

        _islandDetectionMesh = mesh;

        if (_islandDetectionMesh != null)
        {
            _islandDetectionMesh.PropertyChanged += IslandDetectionMesh_PropertyChanged;
        }
    }

    /// <summary>
    /// Invalidates height ordering immediately when the analyzed model transform changes.
    /// </summary>
    private void IslandDetectionMesh_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.Equals(e.PropertyName, nameof(MeshEntity.WorldTransform), StringComparison.Ordinal)
            || string.Equals(e.PropertyName, nameof(MeshEntity.ImportPlacementTransform), StringComparison.Ordinal)
            || string.Equals(e.PropertyName, nameof(MeshEntity.UserTransform), StringComparison.Ordinal))
        {
            InvalidateIslandDetection("The model transform changed. Refresh to analyze its new orientation.");
        }
    }

    /// <summary>
    /// Invalidates the workflow when its source model leaves the current document.
    /// </summary>
    private void Document_IslandDetectionEntitiesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        _ = sender;

        if (_islandDetectionMesh == null || e.OldItems == null)
        {
            return;
        }

        foreach (CadEntity removedEntity in e.OldItems)
        {
            if (ReferenceEquals(removedEntity, _islandDetectionMesh))
            {
                InvalidateIslandDetection("The analyzed model was removed.");
                SetIslandDetectionMesh(null);
                return;
            }
        }
    }

    /// <summary>
    /// Keeps toolbar enablement and active results aligned with single-model selection.
    /// </summary>
    private void HandleIslandDetectionSelectionChanged()
    {
        MeshEntity? selectedMesh = GetSingleSelectedMeshEntity();
        UpdateIslandDetectionToolbarState();

        if (_islandDetectionMesh != null && selectedMesh?.Id != _islandDetectionMesh.Id)
        {
            InvalidateIslandDetection("Selection changed. Select one model and refresh the analysis.");
            SetIslandDetectionMesh(null);
        }
    }

    /// <summary>
    /// Enables the toolbar launcher only for a single selected model outside an active run.
    /// </summary>
    private void UpdateIslandDetectionToolbarState()
    {
        IslandDetectionLaunchButton.IsEnabled = GetSingleSelectedMeshEntity() != null
            && _islandDetectionCancellation == null;
    }

    /// <summary>
    /// Creates concise status text that distinguishes clean results from mesh warnings.
    /// </summary>
    private static string CreateIslandStatusText(IslandDetectionResult result)
    {
        if (result.Candidates.Count == 0)
        {
            return result.Diagnostics.HasMeshQualityWarnings
                ? "No islands found; mesh warnings require review"
                : "No islands found";
        }

        return result.Diagnostics.HasMeshQualityWarnings
            ? $"Found {result.Candidates.Count} island candidates with mesh warnings"
            : $"Found {result.Candidates.Count} island candidates";
    }

    /// <summary>
    /// Navigates to the preceding filtered candidate.
    /// </summary>
    private void IslandDetectionPanel_PreviousRequested(object? sender, EventArgs e)
    {
        NavigateIslandCandidate(-1);
    }

    /// <summary>
    /// Navigates to the next filtered candidate.
    /// </summary>
    private void IslandDetectionPanel_NextRequested(object? sender, EventArgs e)
    {
        NavigateIslandCandidate(1);
    }

    /// <summary>
    /// Explicitly reruns topology analysis for the current selected model.
    /// </summary>
    private async void IslandDetectionPanel_RefreshRequested(object? sender, EventArgs e)
    {
        await StartIslandDetectionAsync(true);
    }

    /// <summary>
    /// Hides all session UI and rendering while retaining a valid cache for the same model.
    /// </summary>
    private void IslandDetectionPanel_CloseRequested(object? sender, EventArgs e)
    {
        CancelIslandDetectionAnalysis();
        _isIslandDetectionPanelOpen = false;
        IslandDetectionHostOverlay.Visibility = Visibility.Collapsed;
        _scene.HideIslandDetectionPreview();
    }

    /// <summary>
    /// Cancels the current background calculation without changing the active viewport tool.
    /// </summary>
    private void IslandDetectionPanel_CancelRequested(object? sender, EventArgs e)
    {
        CancelIslandDetectionAnalysis();
    }

    /// <summary>
    /// Reuses raw topology results when presentation-only filters change.
    /// </summary>
    private void IslandDetectionPanel_FiltersChanged(object? sender, EventArgs e)
    {
        RefreshIslandDetectionPresentation(false);
    }
}
