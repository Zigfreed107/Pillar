// AppResourceSmokeTests.cs
// Verifies that application theme dictionaries load and resolve shared control resources at runtime.
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace Pillar.UI.SmokeTests;

/// <summary>
/// Provides runtime checks for application-level theme resource loading.
/// </summary>
internal static class AppResourceSmokeTests
{
    /// <summary>
    /// Loads App.xaml resources and appends any resource-resolution failure to the shared harness.
    /// </summary>
    public static void Run(List<string> failures)
    {
        try
        {
            Pillar.UI.App application = new Pillar.UI.App();
            application.InitializeComponent();
            application.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            object cornerRadius = application.FindResource("Pillar.CornerRadius.Control");
            Style buttonStyle = (Style)application.FindResource("PillarButtonStyle");
            Style checkBoxStyle = (Style)application.FindResource("PillarCheckBoxStyle");
            Button button = new Button
            {
                Content = "Theme check",
                Style = buttonStyle
            };
            CheckBox checkBox = new CheckBox
            {
                Content = "Theme check",
                Style = checkBoxStyle
            };
            Pillar.UI.Layers.LayerPanel layerPanel = new Pillar.UI.Layers.LayerPanel();
            Pillar.UI.Controls.ClipRangeSlider clipRangeSlider = new Pillar.UI.Controls.ClipRangeSlider();

            button.ApplyTemplate();
            checkBox.ApplyTemplate();
            layerPanel.ApplyTemplate();
            clipRangeSlider.ApplyTemplate();
            GC.KeepAlive(cornerRadius);
        }
        catch (Exception exception)
        {
            failures.Add($"App theme resources load: {exception}");
        }
    }
}
