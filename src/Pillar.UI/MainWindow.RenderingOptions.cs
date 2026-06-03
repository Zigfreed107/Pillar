// MainWindow.RenderingOptions.cs
// Applies viewport post-processing options for the CAD render surface after Helix creates its render host.
using HelixToolkit.SharpDX;
using HelixToolkit.SharpDX.Render;
using Pillar.UI.Properties;
using System;
using System.Diagnostics;
using System.Threading;
using System.Windows;

namespace Pillar.UI;

public partial class MainWindow
{
    private const double ViewportSsaoSamplingRadius = 8.0;
    private const double ViewportSsaoIntensity = 3.0;
    private const double DefaultViewportRenderScale = 1.5;
    private const double MinimumViewportRenderScale = 1.0;
    private const double MaximumViewportRenderScale = 2.0;
    private const FXAALevel ViewportFxaaLevel = FXAALevel.Ultra;
    private int _hasAppliedViewportPostProcessingAfterFirstRender;

    /// <summary>
    /// Wires viewport lifecycle hooks so post-processing is applied once the live Helix render host exists.
    /// </summary>
    private void InitializeViewportPostProcessingOptions()
    {
        ApplyViewportPostProcessingOptions();
        Viewport.Loaded += Viewport_LoadedApplyPostProcessingOptions;
        Viewport.OnRendered += Viewport_OnRenderedApplyPostProcessingOptions;
    }

    /// <summary>
    /// Removes viewport post-processing hooks owned by the main window.
    /// </summary>
    private void DisposeViewportPostProcessingOptions()
    {
        Viewport.Loaded -= Viewport_LoadedApplyPostProcessingOptions;
        Viewport.OnRendered -= Viewport_OnRenderedApplyPostProcessingOptions;
    }

    /// <summary>
    /// Reapplies post-processing after WPF has applied the viewport template.
    /// </summary>
    private void Viewport_LoadedApplyPostProcessingOptions(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        ApplyViewportPostProcessingOptions();
    }

    /// <summary>
    /// Reapplies post-processing after the first live Helix frame in case the render host was recreated late.
    /// </summary>
    private void Viewport_OnRenderedApplyPostProcessingOptions(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;

        if (Interlocked.Exchange(ref _hasAppliedViewportPostProcessingAfterFirstRender, 1) == 1)
        {
            return;
        }

        Dispatcher.BeginInvoke(new Action(() =>
        {
            Viewport.OnRendered -= Viewport_OnRenderedApplyPostProcessingOptions;
            ApplyViewportPostProcessingOptions();
        }));
    }

    /// <summary>
    /// Applies post-processing render settings to both the viewport dependency properties and the live render host.
    /// </summary>
    private void ApplyViewportPostProcessingOptions()
    {
        double configuredRenderScale = ReadViewportRenderScale();
        double targetDpiScale = global::System.Math.Max(Viewport.DpiScale, configuredRenderScale);

        Viewport.EnableDpiScale = true;
        Viewport.DpiScale = targetDpiScale;
        Viewport.MSAA = MSAALevel.Disable;
        Viewport.FXAALevel = ViewportFxaaLevel;
        Viewport.EnableSSAO = true;
        Viewport.SSAOQuality = SSAOQuality.High;
        Viewport.SSAOSamplingRadius = ViewportSsaoSamplingRadius;
        Viewport.SSAOIntensity = ViewportSsaoIntensity;

        IRenderHost? renderHost = Viewport.RenderHost;
        if (renderHost == null)
        {
            return;
        }

        renderHost.MSAA = MSAALevel.Disable;
        renderHost.DpiScale = (float)targetDpiScale;

        DX11RenderHostConfiguration renderConfiguration = renderHost.RenderConfiguration;
        renderConfiguration.FXAALevel = ViewportFxaaLevel;
        renderConfiguration.EnableSSAO = true;
        renderConfiguration.SSAOQuality = SSAOQuality.High;
        renderConfiguration.SSAORadius = (float)ViewportSsaoSamplingRadius;
        renderConfiguration.SSAOIntensity = (float)ViewportSsaoIntensity;
        renderHost.InvalidateRender();

        Debug.WriteLine(
            $"Viewport post-processing applied. FeatureLevel={renderHost.FeatureLevel}, ConfiguredRenderScale={configuredRenderScale}, RenderScale={renderHost.DpiScale}, MSAA={renderHost.MSAA}, FXAA={renderConfiguration.FXAALevel}, SSAO={renderConfiguration.EnableSSAO}, SSAORadius={renderConfiguration.SSAORadius}, SSAOIntensity={renderConfiguration.SSAOIntensity}");
    }

    /// <summary>
    /// Reads the user-configurable viewport render scale and clamps it to practical real-time CAD values.
    /// </summary>
    private static double ReadViewportRenderScale()
    {
        double configuredRenderScale = Settings.Default.ViewportRenderScale;

        if (double.IsNaN(configuredRenderScale) || double.IsInfinity(configuredRenderScale))
        {
            return DefaultViewportRenderScale;
        }

        return global::System.Math.Clamp(
            configuredRenderScale <= 0.0 ? DefaultViewportRenderScale : configuredRenderScale,
            MinimumViewportRenderScale,
            MaximumViewportRenderScale);
    }
}
