// MainWindow.ModelAppearanceSettings.cs
// Reads application-level model appearance settings and converts them into render-layer materials during startup composition.
using HelixToolkit.Maths;
using HelixToolkit.Wpf.SharpDX;
using Pillar.Rendering.EntityRenderers;
using Pillar.UI.Properties;
using System;
using System.Windows.Media;
using MediaColor = System.Windows.Media.Color;

namespace Pillar.UI;

public partial class MainWindow
{
    private const string DefaultModelColor = "#FFB3B3B3";

    /// <summary>
    /// Creates the configured material for imported model meshes, falling back to neutral grey when the setting is invalid.
    /// </summary>
    private static PhongMaterial ReadDefaultModelMaterial()
    {
        return MeshRenderer.CreateMaterial(ReadDefaultModelColor());
    }

    /// <summary>
    /// Creates the brush used by UI overlays that need to show model-related information consistently with viewport meshes.
    /// </summary>
    private static Brush ReadDefaultModelBrush()
    {
        MediaColor color = ReadDefaultModelMediaColor();
        SolidColorBrush brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    /// <summary>
    /// Reads the application-configured imported model color as a render-layer color value.
    /// </summary>
    private static Color4 ReadDefaultModelColor()
    {
        MediaColor color = ReadDefaultModelMediaColor();

        return new Color4(
            color.R / 255.0f,
            color.G / 255.0f,
            color.B / 255.0f,
            color.A / 255.0f);
    }

    /// <summary>
    /// Reads the application-configured imported model color as a WPF color for shell overlay drawing.
    /// </summary>
    private static MediaColor ReadDefaultModelMediaColor()
    {
        string configuredColor = Settings.Default.DefaultModelColor;

        if (string.IsNullOrWhiteSpace(configuredColor))
        {
            configuredColor = DefaultModelColor;
        }

        try
        {
            object? convertedColor = ColorConverter.ConvertFromString(configuredColor);

            if (convertedColor is MediaColor color)
            {
                return color;
            }
        }
        catch (FormatException)
        {
        }

        return MediaColor.FromRgb(179, 179, 179);
    }
}
