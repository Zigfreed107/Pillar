// ClipRangeSlider.xaml.cs
// Implements the two-handle Z clipping range control used by the viewport overlay without coupling UI drag behavior to rendering.
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Pillar.UI.Controls;

/// <summary>
/// Provides a vertical two-handle range slider for selecting the visible print-volume Z interval.
/// </summary>
public partial class ClipRangeSlider : UserControl
{
    public static readonly DependencyProperty MinimumZProperty =
        DependencyProperty.Register(
            nameof(MinimumZ),
            typeof(double),
            typeof(ClipRangeSlider),
            new PropertyMetadata(0.0, OnRangePropertyChanged));

    public static readonly DependencyProperty MaximumZProperty =
        DependencyProperty.Register(
            nameof(MaximumZ),
            typeof(double),
            typeof(ClipRangeSlider),
            new PropertyMetadata(1.0, OnRangePropertyChanged));

    public static readonly DependencyProperty LowerZProperty =
        DependencyProperty.Register(
            nameof(LowerZ),
            typeof(double),
            typeof(ClipRangeSlider),
            new PropertyMetadata(0.0, OnClipValuePropertyChanged));

    public static readonly DependencyProperty UpperZProperty =
        DependencyProperty.Register(
            nameof(UpperZ),
            typeof(double),
            typeof(ClipRangeSlider),
            new PropertyMetadata(1.0, OnClipValuePropertyChanged));

    public static readonly DependencyProperty SelectedModelLowerZProperty =
        DependencyProperty.Register(
            nameof(SelectedModelLowerZ),
            typeof(double),
            typeof(ClipRangeSlider),
            new PropertyMetadata(0.0, OnSelectedModelBoundsPropertyChanged));

    public static readonly DependencyProperty SelectedModelUpperZProperty =
        DependencyProperty.Register(
            nameof(SelectedModelUpperZ),
            typeof(double),
            typeof(ClipRangeSlider),
            new PropertyMetadata(0.0, OnSelectedModelBoundsPropertyChanged));

    public static readonly DependencyProperty IsSelectedModelBoundsVisibleProperty =
        DependencyProperty.Register(
            nameof(IsSelectedModelBoundsVisible),
            typeof(bool),
            typeof(ClipRangeSlider),
            new PropertyMetadata(false, OnSelectedModelBoundsPropertyChanged));

    public static readonly DependencyProperty SelectedModelBoundsBrushProperty =
        DependencyProperty.Register(
            nameof(SelectedModelBoundsBrush),
            typeof(Brush),
            typeof(ClipRangeSlider),
            new PropertyMetadata(Brushes.Transparent));

    private const double HandleHeight = 14.0;
    private const double TrackWidth = 8.0;
    private bool _isDraggingUpperHandle;
    private bool _isDraggingLowerHandle;
    private bool _isSynchronizingValues;

    /// <summary>
    /// Creates the range slider and subscribes to mouse events that need control-level capture.
    /// </summary>
    public ClipRangeSlider()
    {
        InitializeComponent();
        Loaded += ClipRangeSlider_Loaded;
        MouseMove += ClipRangeSlider_MouseMove;
        MouseLeftButtonUp += ClipRangeSlider_MouseLeftButtonUp;
    }

    /// <summary>
    /// Raised after the selected visible Z range changes.
    /// </summary>
    public event EventHandler<ClipRangeChangedEventArgs>? ClipRangeChanged;

    /// <summary>
    /// Gets or sets the lowest printable Z value represented by the slider.
    /// </summary>
    public double MinimumZ
    {
        get { return (double)GetValue(MinimumZProperty); }
        set { SetValue(MinimumZProperty, value); }
    }

    /// <summary>
    /// Gets or sets the highest printable Z value represented by the slider.
    /// </summary>
    public double MaximumZ
    {
        get { return (double)GetValue(MaximumZProperty); }
        set { SetValue(MaximumZProperty, value); }
    }

    /// <summary>
    /// Gets or sets the lower clipping plane Z value.
    /// </summary>
    public double LowerZ
    {
        get { return (double)GetValue(LowerZProperty); }
        set { SetValue(LowerZProperty, value); }
    }

    /// <summary>
    /// Gets or sets the upper clipping plane Z value.
    /// </summary>
    public double UpperZ
    {
        get { return (double)GetValue(UpperZProperty); }
        set { SetValue(UpperZProperty, value); }
    }

    /// <summary>
    /// Gets or sets the selected model's lower world-space Z bound represented behind the clipping range.
    /// </summary>
    public double SelectedModelLowerZ
    {
        get { return (double)GetValue(SelectedModelLowerZProperty); }
        set { SetValue(SelectedModelLowerZProperty, value); }
    }

    /// <summary>
    /// Gets or sets the selected model's upper world-space Z bound represented behind the clipping range.
    /// </summary>
    public double SelectedModelUpperZ
    {
        get { return (double)GetValue(SelectedModelUpperZProperty); }
        set { SetValue(SelectedModelUpperZProperty, value); }
    }

    /// <summary>
    /// Gets or sets whether the selected model bounds rectangle is visible.
    /// </summary>
    public bool IsSelectedModelBoundsVisible
    {
        get { return (bool)GetValue(IsSelectedModelBoundsVisibleProperty); }
        set { SetValue(IsSelectedModelBoundsVisibleProperty, value); }
    }

    /// <summary>
    /// Gets or sets the brush used to draw the selected model height indicator.
    /// </summary>
    public Brush SelectedModelBoundsBrush
    {
        get { return (Brush)GetValue(SelectedModelBoundsBrushProperty); }
        set { SetValue(SelectedModelBoundsBrushProperty, value); }
    }

    /// <summary>
    /// Configures the represented print volume and initial clipping interval in one notification.
    /// </summary>
    public void ConfigureRange(double minimumZ, double maximumZ, double lowerZ, double upperZ)
    {
        _isSynchronizingValues = true;

        try
        {
            MinimumZ = NormalizeFiniteValue(minimumZ, 0.0);
            MaximumZ = Math.Max(MinimumZ, NormalizeFiniteValue(maximumZ, MinimumZ));
            double normalizedLowerZ = ClampToRange(lowerZ);
            double normalizedUpperZ = ClampToRange(upperZ);

            if (normalizedUpperZ < normalizedLowerZ)
            {
                normalizedUpperZ = normalizedLowerZ;
            }

            LowerZ = normalizedLowerZ;
            UpperZ = normalizedUpperZ;
        }
        finally
        {
            _isSynchronizingValues = false;
        }

        UpdateVisualState();
        RaiseClipRangeChanged();
    }

    /// <summary>
    /// Shows the selected model's current world-space Z bounds behind the clipping track.
    /// </summary>
    public void ShowSelectedModelBounds(double lowerZ, double upperZ, Brush boundsBrush)
    {
        double normalizedLowerZ = ClampToRange(Math.Min(lowerZ, upperZ));
        double normalizedUpperZ = ClampToRange(Math.Max(lowerZ, upperZ));

        SelectedModelLowerZ = normalizedLowerZ;
        SelectedModelUpperZ = normalizedUpperZ;
        SelectedModelBoundsBrush = boundsBrush ?? Brushes.Transparent;
        IsSelectedModelBoundsVisible = normalizedUpperZ > normalizedLowerZ;
        UpdateVisualState();
    }

    /// <summary>
    /// Hides the selected model height indicator when no single model is selected.
    /// </summary>
    public void HideSelectedModelBounds()
    {
        IsSelectedModelBoundsVisible = false;
        UpdateVisualState();
    }

    /// <summary>
    /// Refreshes handle positions after WPF has measured the template.
    /// </summary>
    private void ClipRangeSlider_Loaded(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        UpdateVisualState();
    }

    /// <summary>
    /// Keeps the handle positions aligned if the overlay is resized.
    /// </summary>
    private void SliderCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        _ = sender;
        _ = e;
        UpdateVisualState();
    }

    /// <summary>
    /// Begins dragging the upper clipping plane handle.
    /// </summary>
    private void UpperHandle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _ = sender;
        BeginDrag(true, e);
    }

    /// <summary>
    /// Begins dragging the lower clipping plane handle.
    /// </summary>
    private void LowerHandle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _ = sender;
        BeginDrag(false, e);
    }

    /// <summary>
    /// Updates the active handle while the user drags inside the captured control.
    /// </summary>
    private void ClipRangeSlider_MouseMove(object sender, MouseEventArgs e)
    {
        _ = sender;

        if (!_isDraggingUpperHandle && !_isDraggingLowerHandle)
        {
            return;
        }

        Point position = e.GetPosition(SliderCanvas);
        double requestedZ = GetZFromCanvasY(position.Y);

        if (_isDraggingUpperHandle)
        {
            SetUpperZFromDrag(requestedZ);
            return;
        }

        SetLowerZFromDrag(requestedZ);
    }

    /// <summary>
    /// Ends the current drag operation and releases mouse capture.
    /// </summary>
    private void ClipRangeSlider_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _ = sender;
        _ = e;
        EndDrag();
    }

    /// <summary>
    /// Restores the visible range to the full configured print-volume height.
    /// </summary>
    private void ResetButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        EndDrag();
        SetClipRange(MinimumZ, MaximumZ);
    }

    /// <summary>
    /// Normalizes dependent values and refreshes the control after the represented print-volume range changes.
    /// </summary>
    private static void OnRangePropertyChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        _ = e;
        ClipRangeSlider slider = (ClipRangeSlider)dependencyObject;

        if (slider._isSynchronizingValues)
        {
            return;
        }

        slider.NormalizeCurrentValues();
    }

    /// <summary>
    /// Refreshes the slider and notifies listeners after either clipping plane changes.
    /// </summary>
    private static void OnClipValuePropertyChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        _ = e;
        ClipRangeSlider slider = (ClipRangeSlider)dependencyObject;

        if (slider._isSynchronizingValues)
        {
            return;
        }

        slider.NormalizeCurrentValues();
        slider.RaiseClipRangeChanged();
    }

    /// <summary>
    /// Refreshes the selected model height marker when its dependency properties are updated.
    /// </summary>
    private static void OnSelectedModelBoundsPropertyChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        _ = e;
        ClipRangeSlider slider = (ClipRangeSlider)dependencyObject;
        slider.UpdateVisualState();
    }

    /// <summary>
    /// Starts mouse capture for one handle so dragging remains stable under quick pointer movement.
    /// </summary>
    private void BeginDrag(bool isUpperHandle, MouseButtonEventArgs e)
    {
        _isDraggingUpperHandle = isUpperHandle;
        _isDraggingLowerHandle = !isUpperHandle;
        CaptureMouse();
        e.Handled = true;
    }

    /// <summary>
    /// Stops any active handle drag and releases mouse capture.
    /// </summary>
    private void EndDrag()
    {
        if (!_isDraggingUpperHandle && !_isDraggingLowerHandle)
        {
            return;
        }

        _isDraggingUpperHandle = false;
        _isDraggingLowerHandle = false;
        ReleaseMouseCapture();
    }

    /// <summary>
    /// Moves the upper clipping plane, pulling the lower plane down if the handles cross.
    /// </summary>
    private void SetUpperZFromDrag(double requestedUpperZ)
    {
        double nextUpperZ = ClampToRange(requestedUpperZ);
        double nextLowerZ = LowerZ;

        if (nextUpperZ < nextLowerZ)
        {
            nextLowerZ = nextUpperZ;
        }

        SetClipRange(nextLowerZ, nextUpperZ);
    }

    /// <summary>
    /// Moves the lower clipping plane, pushing the upper plane up if the handles cross.
    /// </summary>
    private void SetLowerZFromDrag(double requestedLowerZ)
    {
        double nextLowerZ = ClampToRange(requestedLowerZ);
        double nextUpperZ = UpperZ;

        if (nextLowerZ > nextUpperZ)
        {
            nextUpperZ = nextLowerZ;
        }

        SetClipRange(nextLowerZ, nextUpperZ);
    }

    /// <summary>
    /// Applies both clipping values together so render listeners receive one coherent range.
    /// </summary>
    private void SetClipRange(double lowerZ, double upperZ)
    {
        _isSynchronizingValues = true;

        try
        {
            LowerZ = lowerZ;
            UpperZ = upperZ;
        }
        finally
        {
            _isSynchronizingValues = false;
        }

        UpdateVisualState();
        RaiseClipRangeChanged();
    }

    /// <summary>
    /// Revalidates the current range after direct dependency-property updates.
    /// </summary>
    private void NormalizeCurrentValues()
    {
        double minimumZ = NormalizeFiniteValue(MinimumZ, 0.0);
        double maximumZ = NormalizeFiniteValue(MaximumZ, minimumZ);

        if (maximumZ < minimumZ)
        {
            maximumZ = minimumZ;
        }

        double lowerZ = Math.Min(maximumZ, Math.Max(minimumZ, NormalizeFiniteValue(LowerZ, minimumZ)));
        double upperZ = Math.Min(maximumZ, Math.Max(minimumZ, NormalizeFiniteValue(UpperZ, maximumZ)));

        if (upperZ < lowerZ)
        {
            upperZ = lowerZ;
        }

        _isSynchronizingValues = true;

        try
        {
            MinimumZ = minimumZ;
            MaximumZ = maximumZ;
            LowerZ = lowerZ;
            UpperZ = upperZ;
        }
        finally
        {
            _isSynchronizingValues = false;
        }

        UpdateVisualState();
    }

    /// <summary>
    /// Updates the WPF primitives to match the current numeric range.
    /// </summary>
    private void UpdateVisualState()
    {
        if (SliderCanvas == null || SliderCanvas.ActualHeight <= HandleHeight)
        {
            return;
        }

        double canvasWidth = SliderCanvas.ActualWidth > 0.0 ? SliderCanvas.ActualWidth : SliderCanvas.Width;
        double trackLeft = (canvasWidth - TrackWidth) / 2.0;
        double upperCenterY = GetCanvasCenterYFromZ(UpperZ);
        double lowerCenterY = GetCanvasCenterYFromZ(LowerZ);

        Canvas.SetLeft(TrackRectangle, trackLeft);
        Canvas.SetTop(TrackRectangle, HandleHeight / 2.0);
        TrackRectangle.Height = Math.Max(0.0, SliderCanvas.ActualHeight - HandleHeight);

        UpdateSelectedModelBoundsRectangle(trackLeft);

        Canvas.SetLeft(VisibleRangeRectangle, trackLeft);
        Canvas.SetTop(VisibleRangeRectangle, upperCenterY);
        VisibleRangeRectangle.Height = Math.Max(0.0, lowerCenterY - upperCenterY);

        Canvas.SetLeft(UpperHandle, (canvasWidth - UpperHandle.Width) / 2.0);
        Canvas.SetTop(UpperHandle, upperCenterY - (HandleHeight / 2.0));

        Canvas.SetLeft(LowerHandle, (canvasWidth - LowerHandle.Width) / 2.0);
        Canvas.SetTop(LowerHandle, lowerCenterY - (HandleHeight / 2.0));

        RangeTextBlock.Text = string.Format(
            CultureInfo.InvariantCulture,
            "{0:F1} - {1:F1}",
            LowerZ,
            UpperZ);
    }

    /// <summary>
    /// Draws the selected model's vertical world bounds behind the active clipping range.
    /// </summary>
    private void UpdateSelectedModelBoundsRectangle(double trackLeft)
    {
        if (!IsSelectedModelBoundsVisible)
        {
            SelectedModelBoundsRectangle.Visibility = Visibility.Collapsed;
            return;
        }

        double upperCenterY = GetCanvasCenterYFromZ(SelectedModelUpperZ);
        double lowerCenterY = GetCanvasCenterYFromZ(SelectedModelLowerZ);
        double indicatorWidth = SelectedModelBoundsRectangle.Width;

        Canvas.SetLeft(SelectedModelBoundsRectangle, trackLeft - ((indicatorWidth - TrackWidth) / 2.0));
        Canvas.SetTop(SelectedModelBoundsRectangle, upperCenterY);
        SelectedModelBoundsRectangle.Height = Math.Max(0.0, lowerCenterY - upperCenterY);
        SelectedModelBoundsRectangle.Visibility = SelectedModelBoundsRectangle.Height > 0.0
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    /// <summary>
    /// Converts a Z value to a handle center position in canvas coordinates.
    /// </summary>
    private double GetCanvasCenterYFromZ(double z)
    {
        double trackHeight = Math.Max(1.0, SliderCanvas.ActualHeight - HandleHeight);
        double normalized = GetNormalizedZ(z);
        return (HandleHeight / 2.0) + ((1.0 - normalized) * trackHeight);
    }

    /// <summary>
    /// Converts a pointer Y coordinate into a Z value in the represented print volume.
    /// </summary>
    private double GetZFromCanvasY(double y)
    {
        double trackHeight = Math.Max(1.0, SliderCanvas.ActualHeight - HandleHeight);
        double normalized = 1.0 - ((y - (HandleHeight / 2.0)) / trackHeight);
        double clampedNormalized = Math.Min(1.0, Math.Max(0.0, normalized));
        return MinimumZ + ((MaximumZ - MinimumZ) * clampedNormalized);
    }

    /// <summary>
    /// Converts a Z value into a normalized slider value from zero to one.
    /// </summary>
    private double GetNormalizedZ(double z)
    {
        double range = MaximumZ - MinimumZ;

        if (range <= 0.0)
        {
            return 0.0;
        }

        return Math.Min(1.0, Math.Max(0.0, (z - MinimumZ) / range));
    }

    /// <summary>
    /// Keeps one Z value inside the represented print-volume range.
    /// </summary>
    private double ClampToRange(double z)
    {
        double finiteZ = NormalizeFiniteValue(z, MinimumZ);
        return Math.Min(MaximumZ, Math.Max(MinimumZ, finiteZ));
    }

    /// <summary>
    /// Replaces NaN and infinity with a known fallback value.
    /// </summary>
    private static double NormalizeFiniteValue(double value, double fallbackValue)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            return fallbackValue;
        }

        return value;
    }

    /// <summary>
    /// Notifies listeners that the selected render clipping range changed.
    /// </summary>
    private void RaiseClipRangeChanged()
    {
        ClipRangeChanged?.Invoke(this, new ClipRangeChangedEventArgs(LowerZ, UpperZ));
    }
}

/// <summary>
/// Carries the selected lower and upper Z clipping values from the slider to the viewport shell.
/// </summary>
public sealed class ClipRangeChangedEventArgs : EventArgs
{
    /// <summary>
    /// Creates one immutable clipping range event payload.
    /// </summary>
    public ClipRangeChangedEventArgs(double lowerZ, double upperZ)
    {
        LowerZ = lowerZ;
        UpperZ = upperZ;
    }

    /// <summary>
    /// Gets the lower clipping plane Z value.
    /// </summary>
    public double LowerZ { get; }

    /// <summary>
    /// Gets the upper clipping plane Z value.
    /// </summary>
    public double UpperZ { get; }
}
