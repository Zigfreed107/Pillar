// MainWindow.SelectionSettings.cs
// Reads user-configurable selection rendering preferences and passes them into rendering services without coupling settings to the renderer.
using Pillar.UI.Properties;
using System;
using System.Windows.Media;

namespace Pillar.UI;

public partial class MainWindow
{
    private const string DefaultSelectionOutlineColor = "#FFFFD700";
    private const float DefaultSelectionOutlineSize = 4.0f;
    private const float MinimumSelectionOutlineSize = 1.0f;
    private const float MaximumSelectionOutlineSize = 12.0f;

    /// <summary>
    /// Reads the user-configurable selection outline color from settings, falling back to gold if the setting is invalid.
    /// </summary>
    private static Color ReadSelectionOutlineColor()
    {
        string configuredColor = Settings.Default.SelectionOutlineColor;

        if (string.IsNullOrWhiteSpace(configuredColor))
        {
            configuredColor = DefaultSelectionOutlineColor;
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

        return Color.FromRgb(255, 215, 0);
    }

    /// <summary>
    /// Reads the user-configurable selection outline size and clamps it to practical viewport values.
    /// </summary>
    private static float ReadSelectionOutlineSize()
    {
        double configuredSize = Settings.Default.SelectionOutlineSize;

        if (double.IsNaN(configuredSize) || double.IsInfinity(configuredSize))
        {
            return DefaultSelectionOutlineSize;
        }

        return (float)Math.Clamp(
            configuredSize <= 0.0 ? DefaultSelectionOutlineSize : configuredSize,
            MinimumSelectionOutlineSize,
            MaximumSelectionOutlineSize);
    }
}
