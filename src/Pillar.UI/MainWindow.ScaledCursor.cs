// MainWindow.ScaledCursor.cs
// Wires the top-toolbar scaled cursor controls to a transient viewport guide without mixing rendering details into the shell.
using Pillar.UI.Properties;
using System;
using System.Numerics;
using System.Windows;
using System.Windows.Media;

namespace Pillar.UI;

public partial class MainWindow
{
    private const double MinimumScaledCursorDiameter = 0.10;
    private const double MaximumScaledCursorDiameter = 500.00;
    private const double DefaultScaledCursorDiameter = 10.00;
    private const float ScaledCursorPlaneZ = 0.0f;
    private const string DefaultScaledCursorCircleColor = "#FF00AEEF";

    private bool _isSynchronizingScaledCursorControls;
    private bool _isScaledCursorInitialized;
    private bool _hasLastScaledCursorScreenPosition;
    private bool _wasScaledCursorPopupOpenBeforeArrowMouseDown;
    private Vector2 _lastScaledCursorScreenPosition;

    /// <summary>
    /// Loads persisted scaled cursor values and prepares the toolbar controls.
    /// </summary>
    private void InitializeScaledCursorControls()
    {
        _isSynchronizingScaledCursorControls = true;

        try
        {
            ScaledCursorToggleButton.IsChecked = false;
            ScaledCursorDropDownButton.IsChecked = false;
            ScaledCursorDiameterNumericUpDown.Value = ReadScaledCursorDiameter();
        }
        finally
        {
            _isSynchronizingScaledCursorControls = false;
        }

        _isScaledCursorInitialized = true;
        _scene.HideScaledCursorPreview();
    }

    /// <summary>
    /// Shows or hides the scaled cursor preview when the toolbar toggle changes.
    /// </summary>
    private void ScaledCursorToggleButton_Changed(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;

        if (!_isScaledCursorInitialized
            || _isSynchronizingScaledCursorControls
            || !AreScaledCursorControlsReady())
        {
            return;
        }

        if (ScaledCursorToggleButton.IsChecked == true)
        {
            RefreshScaledCursorPreviewFromMouse();
            return;
        }

        _scene.HideScaledCursorPreview();
    }

    /// <summary>
    /// Captures popup state before WPF's outside-click popup handling can close it.
    /// </summary>
    private void ScaledCursorDropDownButton_PreviewMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        _ = sender;
        _ = e;

        _wasScaledCursorPopupOpenBeforeArrowMouseDown = ScaledCursorSettingsPopup.IsOpen;
    }

    /// <summary>
    /// Toggles the diameter settings popup from the split-button arrow.
    /// </summary>
    private void ScaledCursorDropDownButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;

        if (!AreScaledCursorControlsReady())
        {
            return;
        }

        bool shouldOpenPopup = !_wasScaledCursorPopupOpenBeforeArrowMouseDown;
        ScaledCursorSettingsPopup.IsOpen = shouldOpenPopup;
        ScaledCursorDropDownButton.IsChecked = shouldOpenPopup;
        _wasScaledCursorPopupOpenBeforeArrowMouseDown = false;
    }

    /// <summary>
    /// Keeps the arrow toggle visually synchronized when the popup closes from outside clicks.
    /// </summary>
    private void ScaledCursorSettingsPopup_Closed(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;

        if (!AreScaledCursorControlsReady())
        {
            return;
        }

        ScaledCursorDropDownButton.IsChecked = false;
    }

    /// <summary>
    /// Persists diameter edits and updates the live guide if the scaled cursor is visible.
    /// </summary>
    private void ScaledCursorDiameterNumericUpDown_ValueChanged(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;

        if (!_isScaledCursorInitialized
            || _isSynchronizingScaledCursorControls
            || !AreScaledCursorControlsReady())
        {
            return;
        }

        double diameter = ClampScaledCursorDiameter(ScaledCursorDiameterNumericUpDown.Value);

        if (Math.Abs(ScaledCursorDiameterNumericUpDown.Value - diameter) > double.Epsilon)
        {
            _isSynchronizingScaledCursorControls = true;

            try
            {
                ScaledCursorDiameterNumericUpDown.Value = diameter;
            }
            finally
            {
                _isSynchronizingScaledCursorControls = false;
            }
        }

        Settings.Default.ScaledCursorDiameter = diameter;
        Settings.Default.Save();
        RefreshScaledCursorPreviewFromMouse();
    }

    /// <summary>
    /// Updates the guide circle to follow one viewport mouse position on the XY build plate.
    /// </summary>
    private void UpdateScaledCursorPreview(Vector2 screenPosition)
    {
        _lastScaledCursorScreenPosition = screenPosition;
        _hasLastScaledCursorScreenPosition = true;

        if (!_isScaledCursorInitialized
            || ScaledCursorToggleButton.IsChecked != true)
        {
            _scene.HideScaledCursorPreview();
            return;
        }

        Vector3 worldPoint;

        if (!_projection.TryGetWorldPointOnHorizontalPlane(screenPosition, ScaledCursorPlaneZ, out worldPoint))
        {
            _scene.HideScaledCursorPreview();
            return;
        }

        _scene.ShowScaledCursorPreview(
            worldPoint,
            (float)ReadScaledCursorDiameter(),
            ReadScaledCursorCircleColor());
    }

    /// <summary>
    /// Hides the scaled cursor when the mouse leaves the viewport.
    /// </summary>
    private void Viewport_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        _ = sender;
        _ = e;
        _scene.HideScaledCursorPreview();
    }

    /// <summary>
    /// Reuses the last viewport mouse position when settings change without new mouse movement.
    /// </summary>
    private void RefreshScaledCursorPreviewFromMouse()
    {
        if (!_isScaledCursorInitialized || ScaledCursorToggleButton.IsChecked != true)
        {
            _scene.HideScaledCursorPreview();
            return;
        }

        Point mousePosition = System.Windows.Input.Mouse.GetPosition(Viewport);

        if (mousePosition.X >= 0.0
            && mousePosition.Y >= 0.0
            && mousePosition.X <= Viewport.ActualWidth
            && mousePosition.Y <= Viewport.ActualHeight)
        {
            UpdateScaledCursorPreview(new Vector2((float)mousePosition.X, (float)mousePosition.Y));
            return;
        }

        if (_hasLastScaledCursorScreenPosition)
        {
            UpdateScaledCursorPreview(_lastScaledCursorScreenPosition);
            return;
        }

        _scene.HideScaledCursorPreview();
    }

    /// <summary>
    /// Returns true after XAML has created the scaled cursor toolbar controls.
    /// </summary>
    private bool AreScaledCursorControlsReady()
    {
        return ScaledCursorToggleButton != null
            && ScaledCursorDropDownButton != null
            && ScaledCursorSettingsPopup != null
            && ScaledCursorDiameterNumericUpDown != null;
    }

    /// <summary>
    /// Reads the persisted scaled cursor diameter and clamps invalid values to a useful default.
    /// </summary>
    private static double ReadScaledCursorDiameter()
    {
        return ClampScaledCursorDiameter(Settings.Default.ScaledCursorDiameter);
    }

    /// <summary>
    /// Reads the configured scaled cursor color, falling back to a visible cyan if the setting is invalid.
    /// </summary>
    private static Color ReadScaledCursorCircleColor()
    {
        string configuredColor = Settings.Default.ScaledCursorCircleColor;

        if (string.IsNullOrWhiteSpace(configuredColor))
        {
            configuredColor = DefaultScaledCursorCircleColor;
        }

        try
        {
            object? convertedColor = ColorConverter.ConvertFromString(configuredColor);

            if (convertedColor is Color color)
            {
                return color;
            }
        }
        catch (FormatException)
        {
        }

        return Color.FromRgb(0, 174, 239);
    }

    /// <summary>
    /// Keeps cursor diameters inside the toolbar editor's supported physical range.
    /// </summary>
    private static double ClampScaledCursorDiameter(double diameter)
    {
        if (double.IsNaN(diameter) || double.IsInfinity(diameter) || diameter <= 0.0)
        {
            return DefaultScaledCursorDiameter;
        }

        return Math.Min(MaximumScaledCursorDiameter, Math.Max(MinimumScaledCursorDiameter, diameter));
    }
}
