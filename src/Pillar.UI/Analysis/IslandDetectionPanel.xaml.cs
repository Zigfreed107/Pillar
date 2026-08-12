// IslandDetectionPanel.xaml.cs
// Converts island analysis panel gestures into focused shell workflow events and formats result summaries.
using Pillar.Geometry.Analysis;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace Pillar.UI.Analysis;

/// <summary>
/// Presents transient island progress, metrics, filters, and navigation without owning geometry analysis.
/// </summary>
public partial class IslandDetectionPanel : UserControl
{
    public event EventHandler? PreviousRequested;
    public event EventHandler? NextRequested;
    public event EventHandler? RefreshRequested;
    public event EventHandler? CloseRequested;
    public event EventHandler? CancelRequested;
    public event EventHandler? FiltersChanged;

    /// <summary>
    /// Creates and initializes the compact result panel.
    /// </summary>
    public IslandDetectionPanel()
    {
        InitializeComponent();
        SetMessage("Select one model and run the analysis.", true);
    }

    /// <summary>
    /// Switches the panel to cancellable progress presentation.
    /// </summary>
    public void SetRunning(IslandDetectionProgress progress)
    {
        ProgressPanel.Visibility = Visibility.Visible;
        ResultPanel.Visibility = Visibility.Collapsed;
        ProgressMessageTextBlock.Text = progress.Message;
        AnalysisProgressBar.Value = progress.Fraction * 100.0;
    }

    /// <summary>
    /// Displays an empty, stale, cancelled, or error result message.
    /// </summary>
    public void SetMessage(string message, bool canRefresh)
    {
        ProgressPanel.Visibility = Visibility.Collapsed;
        ResultPanel.Visibility = Visibility.Visible;
        SummaryTextBlock.Text = message;
        CandidateIndexTextBlock.Visibility = Visibility.Collapsed;
        CandidateMetricsTextBlock.Visibility = Visibility.Collapsed;
        DiagnosticsTextBlock.Visibility = Visibility.Collapsed;
        PreviousButton.IsEnabled = false;
        NextButton.IsEnabled = false;
        RefreshButton.IsEnabled = canRefresh;
    }

    /// <summary>
    /// Displays one active candidate and overall filtered-result context.
    /// </summary>
    public void SetCandidate(
        IslandCandidate candidate,
        int visibleIndex,
        int visibleCount,
        int rawCount,
        IslandDetectionDiagnostics diagnostics)
    {
        if (candidate == null)
        {
            throw new ArgumentNullException(nameof(candidate));
        }

        ProgressPanel.Visibility = Visibility.Collapsed;
        ResultPanel.Visibility = Visibility.Visible;
        SummaryTextBlock.Text = visibleCount == rawCount
            ? $"{rawCount} candidate{Pluralize(rawCount)} found"
            : $"Showing {visibleCount} of {rawCount} candidates";
        CandidateIndexTextBlock.Text = $"Candidate {visibleIndex + 1} of {visibleCount} · {candidate.Severity}";
        CandidateIndexTextBlock.Visibility = Visibility.Visible;
        string mergeText = candidate.MergeHeight.HasValue
            ? candidate.MergeHeight.Value.ToString("0.###", CultureInfo.CurrentCulture) + " mm"
            : "Unmerged";
        string persistenceText = candidate.PersistenceHeight.HasValue
            ? candidate.PersistenceHeight.Value.ToString("0.###", CultureInfo.CurrentCulture) + " mm"
            : "Unbounded";
        CandidateMetricsTextBlock.Text =
            $"Birth: {candidate.BirthHeight:0.###} mm\n" +
            $"Merge: {mergeText}\n" +
            $"Persistence: {persistenceText}\n" +
            $"Branch area: {candidate.TotalBranchArea:0.##} mm²\n" +
            $"Downward area: {candidate.DownwardFacingArea:0.##} mm²\n" +
            $"Confidence: {candidate.Confidence}";
        CandidateMetricsTextBlock.Visibility = Visibility.Visible;
        DiagnosticsTextBlock.Text = diagnostics.HasMeshQualityWarnings
            ? $"Mesh warnings: {diagnostics.OpenEdgeCount} open edges, " +
              $"{diagnostics.NonManifoldEdgeCount} non-manifold edges, " +
              $"{diagnostics.DegenerateTriangleCount} degenerate triangles."
            : string.Empty;
        DiagnosticsTextBlock.Visibility = diagnostics.HasMeshQualityWarnings
            ? Visibility.Visible
            : Visibility.Collapsed;
        PreviousButton.IsEnabled = visibleCount > 1;
        NextButton.IsEnabled = visibleCount > 1;
        RefreshButton.IsEnabled = true;
    }

    /// <summary>
    /// Reads presentation-only filters from the panel.
    /// </summary>
    public IslandPresentationFilter CreateFilter()
    {
        return new IslandPresentationFilter(
            (float)MinimumPersistenceControl.Value,
            (float)MinimumAreaControl.Value,
            0.0f,
            ShowLowConfidenceCheckBox.IsChecked == true);
    }

    /// <summary>
    /// Raises navigation toward the preceding filtered result.
    /// </summary>
    private void PreviousButton_Click(object sender, RoutedEventArgs e)
    {
        PreviousRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Raises navigation toward the next filtered result.
    /// </summary>
    private void NextButton_Click(object sender, RoutedEventArgs e)
    {
        NextRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Requests a topology-analysis refresh.
    /// </summary>
    private void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Requests that the transient workflow and every overlay close.
    /// </summary>
    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Requests cancellation of the current background analysis.
    /// </summary>
    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        CancelRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Raises non-destructive filtering after a numeric presentation control changes.
    /// </summary>
    private void NumericFilter_ValueChanged(object? sender, EventArgs e)
    {
        FiltersChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Raises non-destructive filtering after the confidence checkbox changes.
    /// </summary>
    private void FilterCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        FiltersChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Returns the plural suffix for compact result text.
    /// </summary>
    private static string Pluralize(int count)
    {
        return count == 1 ? string.Empty : "s";
    }
}
