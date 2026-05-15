// MainWindow.SelectionSettings.cs
// Reads user-configurable selection rendering preferences and passes them into rendering services without coupling settings to the renderer.
using Pillar.UI.Properties;
using System;
using System.Windows.Media;

namespace Pillar.UI;

public partial class MainWindow
{
    private const string DefaultSelectionOutlineColor = "#FFFFD700";

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
}
