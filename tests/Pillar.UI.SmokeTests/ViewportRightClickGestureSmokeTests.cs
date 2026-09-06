// ViewportRightClickGestureSmokeTests.cs
// Verifies quick right-click completion stays distinct from right-button camera drags.
using Pillar.UI;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Windows;

namespace Pillar.UI.SmokeTests;

/// <summary>
/// Runs focused checks for viewport right-button gesture classification.
/// </summary>
internal static class ViewportRightClickGestureSmokeTests
{
    /// <summary>
    /// Adds all right-button classification failures to the shared UI smoke-test result.
    /// </summary>
    public static void Run(List<string> failures)
    {
        RunTest(failures, "Quick right-click finishes without treating an orbit as a click", ValidateQuickClickClassification);
    }

    /// <summary>
    /// Verifies timing, final displacement, and movement during the gesture all participate in classification.
    /// </summary>
    private static void ValidateQuickClickClassification()
    {
        Type classifierType = typeof(MainWindow).Assembly.GetType("Pillar.UI.Interaction.ViewportRightClickGestureClassifier")
            ?? throw new InvalidOperationException("Expected the viewport right-click gesture classifier.");
        MethodInfo method = classifierType.GetMethod("IsQuickClick", BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException("Expected the quick right-click classification method.");
        Point pressPosition = new Point(100.0, 100.0);
        bool quickClick = InvokeClassifier(method, pressPosition, new Point(101.0, 101.0), false, 150);
        bool returnedOrbit = InvokeClassifier(method, pressPosition, pressPosition, true, 150);
        bool displacedRelease = InvokeClassifier(method, pressPosition, new Point(106.0, 100.0), false, 150);
        bool longPress = InvokeClassifier(method, pressPosition, pressPosition, false, 500);

        if (!quickClick || returnedOrbit || displacedRelease || longPress)
        {
            throw new InvalidOperationException("Expected only a short stationary right-button gesture to finish drawing.");
        }
    }

    /// <summary>
    /// Invokes the internal UI classifier with stable smoke-test thresholds.
    /// </summary>
    private static bool InvokeClassifier(
        MethodInfo method,
        Point pressPosition,
        Point releasePosition,
        bool exceededDragThreshold,
        long elapsedMilliseconds)
    {
        object? result = method.Invoke(
            null,
            new object[]
            {
                pressPosition,
                releasePosition,
                exceededDragThreshold,
                elapsedMilliseconds,
                4.0,
                4.0,
                350L
            });
        return result is bool isQuickClick && isQuickClick;
    }

    /// <summary>
    /// Records one failed validation while continuing the remaining UI smoke checks.
    /// </summary>
    private static void RunTest(List<string> failures, string name, Action test)
    {
        try
        {
            test();
        }
        catch (Exception ex)
        {
            failures.Add($"{name}: {ex.Message}");
        }
    }
}
