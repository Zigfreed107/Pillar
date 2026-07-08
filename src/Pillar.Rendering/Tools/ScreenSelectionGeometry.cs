// ScreenSelectionGeometry.cs
// Provides reusable screen-space rectangle tests for viewport window selection.
using System;
using System.Windows;

namespace Pillar.Rendering.Tools;

/// <summary>
/// Evaluates projected 2D geometry against a viewport selection rectangle.
/// </summary>
public static class ScreenSelectionGeometry
{
    private const double SegmentTolerance = 0.0001;

    /// <summary>
    /// Returns true when the rectangle contains every supplied point.
    /// </summary>
    public static bool ContainsAllPoints(Rect rectangle, ReadOnlySpan<Point> points)
    {
        if (points.Length == 0)
        {
            return false;
        }

        for (int i = 0; i < points.Length; i++)
        {
            if (!rectangle.Contains(points[i]))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Returns true when any point is inside the rectangle or any adjacent segment crosses it.
    /// </summary>
    public static bool ContainsOrCrossesPolyline(Rect rectangle, ReadOnlySpan<Point> points)
    {
        if (points.Length == 0)
        {
            return false;
        }

        for (int i = 0; i < points.Length; i++)
        {
            if (rectangle.Contains(points[i]))
            {
                return true;
            }
        }

        for (int i = 1; i < points.Length; i++)
        {
            if (SegmentIntersectsRectangle(points[i - 1], points[i], rectangle))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Returns true when any point is inside the rectangle.
    /// </summary>
    public static bool ContainsAnyPoint(Rect rectangle, ReadOnlySpan<Point> points)
    {
        for (int i = 0; i < points.Length; i++)
        {
            if (rectangle.Contains(points[i]))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Tests whether a 2D line segment crosses any edge of a rectangle.
    /// </summary>
    public static bool SegmentIntersectsRectangle(Point startPoint, Point endPoint, Rect rectangle)
    {
        Point topLeft = new Point(rectangle.Left, rectangle.Top);
        Point topRight = new Point(rectangle.Right, rectangle.Top);
        Point bottomRight = new Point(rectangle.Right, rectangle.Bottom);
        Point bottomLeft = new Point(rectangle.Left, rectangle.Bottom);

        return SegmentsIntersect(startPoint, endPoint, topLeft, topRight)
            || SegmentsIntersect(startPoint, endPoint, topRight, bottomRight)
            || SegmentsIntersect(startPoint, endPoint, bottomRight, bottomLeft)
            || SegmentsIntersect(startPoint, endPoint, bottomLeft, topLeft);
    }

    /// <summary>
    /// Tests two 2D line segments, including collinear overlap along rectangle edges.
    /// </summary>
    private static bool SegmentsIntersect(Point firstStart, Point firstEnd, Point secondStart, Point secondEnd)
    {
        double firstDirection = Cross(secondStart, secondEnd, firstStart);
        double secondDirection = Cross(secondStart, secondEnd, firstEnd);
        double thirdDirection = Cross(firstStart, firstEnd, secondStart);
        double fourthDirection = Cross(firstStart, firstEnd, secondEnd);

        if (((firstDirection > 0.0 && secondDirection < 0.0) || (firstDirection < 0.0 && secondDirection > 0.0))
            && ((thirdDirection > 0.0 && fourthDirection < 0.0) || (thirdDirection < 0.0 && fourthDirection > 0.0)))
        {
            return true;
        }

        return IsPointOnSegment(secondStart, secondEnd, firstStart, firstDirection)
            || IsPointOnSegment(secondStart, secondEnd, firstEnd, secondDirection)
            || IsPointOnSegment(firstStart, firstEnd, secondStart, thirdDirection)
            || IsPointOnSegment(firstStart, firstEnd, secondEnd, fourthDirection);
    }

    /// <summary>
    /// Calculates the signed 2D cross product used by segment intersection.
    /// </summary>
    private static double Cross(Point lineStart, Point lineEnd, Point point)
    {
        return ((point.X - lineStart.X) * (lineEnd.Y - lineStart.Y))
            - ((point.Y - lineStart.Y) * (lineEnd.X - lineStart.X));
    }

    /// <summary>
    /// Tests whether a point lies on a segment when the caller already knows it is collinear.
    /// </summary>
    private static bool IsPointOnSegment(Point segmentStart, Point segmentEnd, Point point, double cross)
    {
        if (global::System.Math.Abs(cross) > SegmentTolerance)
        {
            return false;
        }

        return point.X >= global::System.Math.Min(segmentStart.X, segmentEnd.X) - SegmentTolerance
            && point.X <= global::System.Math.Max(segmentStart.X, segmentEnd.X) + SegmentTolerance
            && point.Y >= global::System.Math.Min(segmentStart.Y, segmentEnd.Y) - SegmentTolerance
            && point.Y <= global::System.Math.Max(segmentStart.Y, segmentEnd.Y) + SegmentTolerance;
    }
}
