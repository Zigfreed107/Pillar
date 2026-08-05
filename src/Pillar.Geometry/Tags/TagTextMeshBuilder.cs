// TagTextMeshBuilder.cs
// Triangulates flattened glyph outlines, including holes, and extrudes them into printable solids.
using HelixToolkit.Geometry;
using Pillar.Core.Tags;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace Pillar.Geometry.Tags;

/// <summary>
/// Converts centered glyph contours into a local-space solid text mesh.
/// </summary>
public static class TagTextMeshBuilder
{
    private const float AreaTolerance = 0.0000001f;

    /// <summary>
    /// Builds text from halfway inside the tag body through the configured height above it.
    /// </summary>
    public static TagTextMeshData Build(TagSettings settings, TagTextOutlineData outline)
    {
        if (settings == null)
        {
            throw new ArgumentNullException(nameof(settings));
        }

        float bottomZ = settings.TagHeight * 0.5f;
        float topZ = settings.TagHeight + settings.TextHeight;
        return Build(bottomZ, topZ, outline);
    }

    /// <summary>
    /// Builds text between caller-supplied local Z planes so raft text can share glyph triangulation.
    /// </summary>
    public static TagTextMeshData Build(
        float bottomZ,
        float topZ,
        TagTextOutlineData outline)
    {
        if (!float.IsFinite(bottomZ)
            || !float.IsFinite(topZ)
            || topZ <= bottomZ)
        {
            throw new ArgumentOutOfRangeException(nameof(topZ), "Text extrusion planes must be finite and ordered.");
        }

        if (outline == null)
        {
            throw new ArgumentNullException(nameof(outline));
        }

        List<ContourNode> contours = CreateContourHierarchy(outline.Contours);
        List<Vector3> positions = new List<Vector3>();
        List<int> indices = new List<int>();

        for (int contourIndex = 0; contourIndex < contours.Count; contourIndex++)
        {
            ContourNode outerNode = contours[contourIndex];

            if ((outerNode.Depth & 1) != 0)
            {
                continue;
            }

            List<Vector2> outer = CreateOrientedCopy(outerNode.Points, isCounterClockwise: true);
            List<List<Vector2>> holes = new List<List<Vector2>>();

            for (int childIndex = 0; childIndex < contours.Count; childIndex++)
            {
                ContourNode child = contours[childIndex];

                if (child.ParentIndex == contourIndex && (child.Depth & 1) != 0)
                {
                    holes.Add(CreateOrientedCopy(child.Points, isCounterClockwise: false));
                }
            }

            AddExtrudedPolygon(outer, holes, bottomZ, topZ, positions, indices);
        }

        return new TagTextMeshData(outline.MeasuredWidth, positions, indices);
    }

    /// <summary>
    /// Creates containment links so alternating outline depths become solids and holes.
    /// </summary>
    private static List<ContourNode> CreateContourHierarchy(
        IReadOnlyList<IReadOnlyList<Vector2>> sourceContours)
    {
        List<ContourNode> contours = new List<ContourNode>(sourceContours.Count);

        for (int contourIndex = 0; contourIndex < sourceContours.Count; contourIndex++)
        {
            List<Vector2> points = RemoveDuplicatePoints(sourceContours[contourIndex]);

            if (points.Count >= 3 && MathF.Abs(CalculateSignedArea(points)) > AreaTolerance)
            {
                contours.Add(new ContourNode(points));
            }
        }

        for (int contourIndex = 0; contourIndex < contours.Count; contourIndex++)
        {
            ContourNode contour = contours[contourIndex];
            float contourArea = MathF.Abs(CalculateSignedArea(contour.Points));
            float parentArea = float.PositiveInfinity;

            for (int candidateIndex = 0; candidateIndex < contours.Count; candidateIndex++)
            {
                if (candidateIndex == contourIndex)
                {
                    continue;
                }

                ContourNode candidate = contours[candidateIndex];
                float candidateArea = MathF.Abs(CalculateSignedArea(candidate.Points));

                if (candidateArea > contourArea
                    && candidateArea < parentArea
                    && ContainsPoint(candidate.Points, contour.Points[0]))
                {
                    contour.ParentIndex = candidateIndex;
                    parentArea = candidateArea;
                }
            }
        }

        for (int contourIndex = 0; contourIndex < contours.Count; contourIndex++)
        {
            contours[contourIndex].Depth = CalculateDepth(contours, contourIndex);
        }

        return contours;
    }

    /// <summary>
    /// Removes repeated closing and adjacent vertices before triangulation.
    /// </summary>
    private static List<Vector2> RemoveDuplicatePoints(IReadOnlyList<Vector2> source)
    {
        List<Vector2> points = new List<Vector2>(source.Count);

        for (int pointIndex = 0; pointIndex < source.Count; pointIndex++)
        {
            Vector2 point = source[pointIndex];

            if (!float.IsFinite(point.X) || !float.IsFinite(point.Y))
            {
                continue;
            }

            if (points.Count == 0 || Vector2.DistanceSquared(points[^1], point) > AreaTolerance)
            {
                points.Add(point);
            }
        }

        if (points.Count > 1 && Vector2.DistanceSquared(points[0], points[^1]) <= AreaTolerance)
        {
            points.RemoveAt(points.Count - 1);
        }

        return points;
    }

    /// <summary>
    /// Walks parent links to determine whether one contour is a solid or hole.
    /// </summary>
    private static int CalculateDepth(IReadOnlyList<ContourNode> contours, int contourIndex)
    {
        int depth = 0;
        int parentIndex = contours[contourIndex].ParentIndex;

        while (parentIndex >= 0 && depth <= contours.Count)
        {
            depth++;
            parentIndex = contours[parentIndex].ParentIndex;
        }

        return depth;
    }

    /// <summary>
    /// Adds triangulated caps and boundary walls for one glyph region.
    /// </summary>
    private static void AddExtrudedPolygon(
        List<Vector2> outer,
        List<List<Vector2>> holes,
        float bottomZ,
        float topZ,
        List<Vector3> positions,
        List<int> indices)
    {
        IList<int>? capIndices = SweepLinePolygonTriangulator.Triangulate(outer, holes);

        if (capIndices == null || capIndices.Count == 0)
        {
            return;
        }

        List<Vector2> capPoints = new List<Vector2>(outer.Count);
        capPoints.AddRange(outer);

        for (int holeIndex = 0; holeIndex < holes.Count; holeIndex++)
        {
            capPoints.AddRange(holes[holeIndex]);
        }

        int bottomStart = positions.Count;

        for (int pointIndex = 0; pointIndex < capPoints.Count; pointIndex++)
        {
            positions.Add(new Vector3(capPoints[pointIndex], bottomZ));
        }

        int topStart = positions.Count;

        for (int pointIndex = 0; pointIndex < capPoints.Count; pointIndex++)
        {
            positions.Add(new Vector3(capPoints[pointIndex], topZ));
        }

        for (int index = 0; index + 2 < capIndices.Count; index += 3)
        {
            int first = capIndices[index];
            int second = capIndices[index + 1];
            int third = capIndices[index + 2];
            float cross = Cross(capPoints[second] - capPoints[first], capPoints[third] - capPoints[first]);

            if (cross >= 0.0f)
            {
                AddTriangle(topStart + first, topStart + second, topStart + third, indices);
                AddTriangle(bottomStart + first, bottomStart + third, bottomStart + second, indices);
            }
            else
            {
                AddTriangle(topStart + first, topStart + third, topStart + second, indices);
                AddTriangle(bottomStart + first, bottomStart + second, bottomStart + third, indices);
            }
        }

        AddContourWalls(outer, bottomZ, topZ, positions, indices);

        for (int holeIndex = 0; holeIndex < holes.Count; holeIndex++)
        {
            AddContourWalls(holes[holeIndex], bottomZ, topZ, positions, indices);
        }
    }

    /// <summary>
    /// Adds outward-facing quads along one consistently oriented contour.
    /// </summary>
    private static void AddContourWalls(
        IReadOnlyList<Vector2> contour,
        float bottomZ,
        float topZ,
        List<Vector3> positions,
        List<int> indices)
    {
        for (int pointIndex = 0; pointIndex < contour.Count; pointIndex++)
        {
            int nextIndex = (pointIndex + 1) % contour.Count;
            int start = positions.Count;
            positions.Add(new Vector3(contour[pointIndex], bottomZ));
            positions.Add(new Vector3(contour[nextIndex], bottomZ));
            positions.Add(new Vector3(contour[nextIndex], topZ));
            positions.Add(new Vector3(contour[pointIndex], topZ));
            AddTriangle(start, start + 1, start + 2, indices);
            AddTriangle(start, start + 2, start + 3, indices);
        }
    }

    /// <summary>
    /// Copies a contour and normalizes its winding for cap and wall generation.
    /// </summary>
    private static List<Vector2> CreateOrientedCopy(
        IReadOnlyList<Vector2> source,
        bool isCounterClockwise)
    {
        List<Vector2> result = new List<Vector2>(source);
        bool currentlyCounterClockwise = CalculateSignedArea(result) > 0.0f;

        if (currentlyCounterClockwise != isCounterClockwise)
        {
            result.Reverse();
        }

        return result;
    }

    /// <summary>
    /// Calculates twice the oriented polygon area divided by two.
    /// </summary>
    private static float CalculateSignedArea(IReadOnlyList<Vector2> points)
    {
        float twiceArea = 0.0f;

        for (int pointIndex = 0; pointIndex < points.Count; pointIndex++)
        {
            Vector2 current = points[pointIndex];
            Vector2 next = points[(pointIndex + 1) % points.Count];
            twiceArea += current.X * next.Y - next.X * current.Y;
        }

        return twiceArea * 0.5f;
    }

    /// <summary>
    /// Tests one point against a contour using an allocation-free even-odd ray cast.
    /// </summary>
    private static bool ContainsPoint(IReadOnlyList<Vector2> polygon, Vector2 point)
    {
        bool inside = false;
        int previousIndex = polygon.Count - 1;

        for (int currentIndex = 0; currentIndex < polygon.Count; currentIndex++)
        {
            Vector2 current = polygon[currentIndex];
            Vector2 previous = polygon[previousIndex];
            bool crosses = (current.Y > point.Y) != (previous.Y > point.Y);

            if (crosses)
            {
                float intersectionX = (previous.X - current.X)
                    * (point.Y - current.Y)
                    / (previous.Y - current.Y)
                    + current.X;

                if (point.X < intersectionX)
                {
                    inside = !inside;
                }
            }

            previousIndex = currentIndex;
        }

        return inside;
    }

    /// <summary>
    /// Returns the scalar two-dimensional cross product.
    /// </summary>
    private static float Cross(Vector2 first, Vector2 second)
    {
        return first.X * second.Y - first.Y * second.X;
    }

    /// <summary>
    /// Appends one triangle to the output index buffer.
    /// </summary>
    private static void AddTriangle(int first, int second, int third, List<int> indices)
    {
        indices.Add(first);
        indices.Add(second);
        indices.Add(third);
    }

    /// <summary>
    /// Stores one cleaned contour and its containment relationship.
    /// </summary>
    private sealed class ContourNode
    {
        public ContourNode(List<Vector2> points)
        {
            Points = points;
        }

        public List<Vector2> Points { get; }
        public int ParentIndex { get; set; } = -1;
        public int Depth { get; set; }
    }
}
