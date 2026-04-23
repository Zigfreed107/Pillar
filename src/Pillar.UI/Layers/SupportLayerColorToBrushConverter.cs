// SupportLayerColorToBrushConverter.cs
// Converts core support-layer colors into WPF brushes for the Layer Panel without coupling the core layer model to WPF.
using Pillar.Core.Layers;
using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Pillar.UI.Layers;

/// <summary>
/// Converts support-layer RGB values into solid WPF brushes.
/// </summary>
public sealed class SupportLayerColorToBrushConverter : IValueConverter
{
    /// <summary>
    /// Converts one support-layer color into a frozen brush.
    /// </summary>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        _ = targetType;
        _ = parameter;
        _ = culture;

        if (value is SupportLayerColor supportLayerColor)
        {
            SolidColorBrush brush = new SolidColorBrush(Color.FromRgb(
                supportLayerColor.Red,
                supportLayerColor.Green,
                supportLayerColor.Blue));

            brush.Freeze();
            return brush;
        }

        return Brushes.Transparent;
    }

    /// <summary>
    /// Converts back is not supported because editing is handled by a color dialog in the view layer.
    /// </summary>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        _ = value;
        _ = targetType;
        _ = parameter;
        _ = culture;
        throw new NotSupportedException("Support layer color conversion is one-way.");
    }
}
