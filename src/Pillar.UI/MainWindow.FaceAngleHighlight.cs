// MainWindow.FaceAngleHighlight.cs
// Wires toolbar settings for horizontal face-angle highlighting without placing render logic in the main window shell.
using HelixToolkit.Maths;
using Pillar.UI.Properties;
using System;
using System.Windows;
using System.Windows.Media;

namespace Pillar.UI;

public partial class MainWindow
{
    private const int MinimumFaceAngleThresholdDegrees = 1;
    private const int MaximumFaceAngleThresholdDegrees = 90;
    private const int DefaultFaceAngleThresholdDegrees = 45;
    private const string DefaultFaceAngleHighlightColor = "#CCFF0000";
    private bool _isSynchronizingFaceAngleHighlightControls;
    private bool _isFaceAngleHighlightInitialized;
    private bool _wasFaceAngleHighlightPopupOpenBeforeArrowMouseDown;

    /// <summary>
    /// Loads persisted toolbar values and applies the initial face-angle highlight state to the scene.
    /// </summary>
    private void InitializeFaceAngleHighlightControls()
    {
        _isSynchronizingFaceAngleHighlightControls = true;

        try
        {
            int thresholdDegrees = ClampFaceAngleThreshold(Settings.Default.FaceAngleThresholdDegrees);
            FaceAngleHighlightToggleButton.IsChecked = Settings.Default.FaceAngleHighlightEnabled;
            FaceAngleHighlightDropDownButton.IsChecked = false;
            FaceAngleThresholdNumericUpDown.Value = thresholdDegrees;
        }
        finally
        {
            _isSynchronizingFaceAngleHighlightControls = false;
        }

        _isFaceAngleHighlightInitialized = true;
        ApplyFaceAngleHighlightSettings();
    }

    /// <summary>
    /// Persists toggle changes and applies the active shallow-face highlight state.
    /// </summary>
    private void FaceAngleHighlightToggleButton_Changed(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;

        if (!_isFaceAngleHighlightInitialized
            || _isSynchronizingFaceAngleHighlightControls
            || !AreFaceAngleHighlightControlsReady())
        {
            return;
        }

        bool isEnabled = FaceAngleHighlightToggleButton.IsChecked == true;
        Settings.Default.FaceAngleHighlightEnabled = isEnabled;
        Settings.Default.Save();
        ApplyFaceAngleHighlightSettings();
    }

    /// <summary>
    /// Captures popup state before WPF's outside-click popup handling can close it.
    /// </summary>
    private void FaceAngleHighlightDropDownButton_PreviewMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        _ = sender;
        _ = e;

        _wasFaceAngleHighlightPopupOpenBeforeArrowMouseDown = FaceAngleHighlightSettingsPopup.IsOpen;
    }

    /// <summary>
    /// Toggles the angle settings popup from the split-button arrow.
    /// </summary>
    private void FaceAngleHighlightDropDownButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;

        if (!AreFaceAngleHighlightControlsReady())
        {
            return;
        }

        bool shouldOpenPopup = !_wasFaceAngleHighlightPopupOpenBeforeArrowMouseDown;
        FaceAngleHighlightSettingsPopup.IsOpen = shouldOpenPopup;
        FaceAngleHighlightDropDownButton.IsChecked = shouldOpenPopup;
        _wasFaceAngleHighlightPopupOpenBeforeArrowMouseDown = false;
    }

    /// <summary>
    /// Keeps the arrow toggle visually synchronized when the popup closes from outside clicks.
    /// </summary>
    private void FaceAngleHighlightSettingsPopup_Closed(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;

        if (!AreFaceAngleHighlightControlsReady())
        {
            return;
        }

        FaceAngleHighlightDropDownButton.IsChecked = false;
    }

    /// <summary>
    /// Persists integer angle changes and refreshes the cached highlight overlay.
    /// </summary>
    private void FaceAngleThresholdNumericUpDown_ValueChanged(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;

        if (!_isFaceAngleHighlightInitialized
            || _isSynchronizingFaceAngleHighlightControls
            || !AreFaceAngleHighlightControlsReady())
        {
            return;
        }

        int thresholdDegrees = ClampFaceAngleThreshold((int)Math.Round(FaceAngleThresholdNumericUpDown.Value));

        if (Math.Abs(FaceAngleThresholdNumericUpDown.Value - thresholdDegrees) > double.Epsilon)
        {
            _isSynchronizingFaceAngleHighlightControls = true;

            try
            {
                FaceAngleThresholdNumericUpDown.Value = thresholdDegrees;
            }
            finally
            {
                _isSynchronizingFaceAngleHighlightControls = false;
            }
        }

        Settings.Default.FaceAngleThresholdDegrees = thresholdDegrees;
        Settings.Default.Save();
        ApplyFaceAngleHighlightSettings();
    }

    /// <summary>
    /// Pushes the current toolbar and settings values into the rendering scene.
    /// </summary>
    private void ApplyFaceAngleHighlightSettings()
    {
        if (_scene == null || !AreFaceAngleHighlightControlsReady())
        {
            return;
        }

        bool isEnabled = FaceAngleHighlightToggleButton.IsChecked == true;
        int thresholdDegrees = ClampFaceAngleThreshold((int)Math.Round(FaceAngleThresholdNumericUpDown.Value));
        Color4 highlightColor = ReadFaceAngleHighlightColor();
        _scene.ConfigureFaceAngleHighlight(isEnabled, thresholdDegrees, highlightColor);
    }

    /// <summary>
    /// Returns true after XAML has created the toolbar controls used by this partial class.
    /// </summary>
    private bool AreFaceAngleHighlightControlsReady()
    {
        return FaceAngleHighlightToggleButton != null
            && FaceAngleHighlightDropDownButton != null
            && FaceAngleHighlightSettingsPopup != null
            && FaceAngleThresholdNumericUpDown != null;
    }

    /// <summary>
    /// Reads the user-customizable highlight color from settings, falling back to translucent red if the setting is invalid.
    /// </summary>
    private static Color4 ReadFaceAngleHighlightColor()
    {
        string configuredColor = Settings.Default.FaceAngleHighlightColor;

        if (string.IsNullOrWhiteSpace(configuredColor))
        {
            configuredColor = DefaultFaceAngleHighlightColor;
        }

        try
        {
            object? convertedColor = ColorConverter.ConvertFromString(configuredColor);

            if (convertedColor is System.Windows.Media.Color color)
            {
                return new Color4(
                    color.R / 255.0f,
                    color.G / 255.0f,
                    color.B / 255.0f,
                    color.A / 255.0f);
            }
        }
        catch (FormatException)
        {
        }

        return new Color4(1.0f, 0.0f, 0.0f, 0.8f);
    }

    /// <summary>
    /// Keeps angle thresholds in the toolbar's supported whole-degree range.
    /// </summary>
    private static int ClampFaceAngleThreshold(int thresholdDegrees)
    {
        return Math.Min(
            MaximumFaceAngleThresholdDegrees,
            Math.Max(MinimumFaceAngleThresholdDegrees, thresholdDegrees == 0 ? DefaultFaceAngleThresholdDegrees : thresholdDegrees));
    }
}
