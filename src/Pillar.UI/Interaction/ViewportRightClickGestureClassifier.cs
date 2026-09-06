// ViewportRightClickGestureClassifier.cs
// Classifies a viewport right-button press as a quick command click without taking ownership away from camera navigation.
using System;
using System.Windows;

namespace Pillar.UI.Interaction;

/// <summary>
/// Distinguishes a stationary quick right-click from a camera-navigation drag.
/// </summary>
internal static class ViewportRightClickGestureClassifier
{
    /// <summary>
    /// Tests release timing and movement after the caller has tracked the full pointer gesture.
    /// </summary>
    public static bool IsQuickClick(
        Point pressPosition,
        Point releasePosition,
        bool exceededDragThreshold,
        long elapsedMilliseconds,
        double horizontalDragThreshold,
        double verticalDragThreshold,
        long maximumDurationMilliseconds)
    {
        if (exceededDragThreshold
            || elapsedMilliseconds < 0
            || elapsedMilliseconds > maximumDurationMilliseconds)
        {
            return false;
        }

        double horizontalMovement = Math.Abs(releasePosition.X - pressPosition.X);
        double verticalMovement = Math.Abs(releasePosition.Y - pressPosition.Y);
        return horizontalMovement < horizontalDragThreshold
            && verticalMovement < verticalDragThreshold;
    }
}
