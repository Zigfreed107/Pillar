// TagMeshBuilder.cs
// Generates the tapered body and places reusable local-space text for one raft tag.
using Pillar.Core.Tags;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace Pillar.Geometry.Tags;

/// <summary>
/// Builds a closed tag body with independent inner and outer distances from its raft tangent.
/// </summary>
public static class TagMeshBuilder
{
    private const float MinimumBodyDimension = 0.01f;

    /// <summary>
    /// Generates one tapered rectangular body from measured text width and durable placement.
    /// </summary>
    public static TagMeshData Build(
        TagSettings settings,
        float measuredTextWidth,
        TagPlacement placement)
    {
        return Build(settings, measuredTextWidth, placement, null);
    }

    /// <summary>
    /// Generates one complete tag from a reusable local-space text mesh and durable placement.
    /// </summary>
    public static TagMeshData Build(
        TagSettings settings,
        TagTextMeshData textMesh,
        TagPlacement placement)
    {
        if (textMesh == null)
        {
            throw new ArgumentNullException(nameof(textMesh));
        }

        return Build(settings, textMesh.MeasuredWidth, placement, textMesh);
    }

    /// <summary>
    /// Generates the body and optionally appends placed text without retriangulating glyphs.
    /// </summary>
    private static TagMeshData Build(
        TagSettings settings,
        float measuredTextWidth,
        TagPlacement placement,
        TagTextMeshData? textMesh)
    {
        if (settings == null)
        {
            throw new ArgumentNullException(nameof(settings));
        }

        if (!float.IsFinite(measuredTextWidth) || measuredTextWidth < 0.0f)
        {
            throw new ArgumentOutOfRangeException(nameof(measuredTextWidth), "Measured tag text width must be finite and non-negative.");
        }

        Vector2 tangent = placement.Tangent;

        if (!float.IsFinite(tangent.X)
            || !float.IsFinite(tangent.Y)
            || tangent.LengthSquared() <= 0.00000001f)
        {
            throw new ArgumentOutOfRangeException(nameof(placement), "A tag placement tangent must be finite and non-zero.");
        }

        tangent = Vector2.Normalize(tangent);
        Vector2 normal = new Vector2(-tangent.Y, tangent.X);
        Vector2 center = new Vector2(placement.AttachmentPoint.X, placement.AttachmentPoint.Y);
        float bottomLength = MathF.Max(
            MinimumBodyDimension,
            measuredTextWidth + settings.BorderOffset * 2.0f);
        float outset = CalculateChamferOutset(settings.TagHeight, settings.EdgeAngleDegrees);
        float topLength = bottomLength + outset * 2.0f;

        Vector2[] bottom = CreateRectangle(
            center,
            tangent,
            normal,
            bottomLength,
            settings.InnerWidth,
            settings.OuterWidth);
        Vector2[] top = CreateRectangle(
            center,
            tangent,
            normal,
            topLength,
            settings.InnerWidth + outset,
            settings.OuterWidth + outset);
        List<Vector3> positions = new List<Vector3>(8);
        List<int> indices = new List<int>(36);
        AddPrism(
            bottom,
            top,
            placement.AttachmentPoint.Z,
            placement.AttachmentPoint.Z + settings.TagHeight,
            positions,
            indices);

        if (textMesh != null)
        {
            AddPlacedText(
                textMesh,
                center,
                tangent,
                normal,
                settings.OuterWidth + outset,
                settings.BorderOffset,
                settings.IsTextFlipped,
                placement.AttachmentPoint.Z,
                positions,
                indices);
        }

        return new TagMeshData(positions, indices);
    }

    /// <summary>
    /// Transforms one centered local text mesh into the tag's tangent frame.
    /// </summary>
    private static void AddPlacedText(
        TagTextMeshData textMesh,
        Vector2 center,
        Vector2 tangent,
        Vector2 normal,
        float topOuterDistance,
        float borderOffset,
        bool isTextFlipped,
        float attachmentZ,
        List<Vector3> positions,
        List<int> indices)
    {
        int vertexOffset = positions.Count;
        float orientation = isTextFlipped ? 1.0f : -1.0f;
        float orientedMaximumY = isTextFlipped ? textMesh.MaximumY : -textMesh.MinimumY;
        float textCenterOffset = topOuterDistance - borderOffset - orientedMaximumY;
        Vector2 textCenter = center + normal * textCenterOffset;

        for (int positionIndex = 0; positionIndex < textMesh.Positions.Count; positionIndex++)
        {
            Vector3 local = textMesh.Positions[positionIndex];
            Vector2 world = textCenter
                + tangent * (local.X * orientation)
                + normal * (local.Y * orientation);
            positions.Add(new Vector3(world, attachmentZ + local.Z));
        }

        for (int index = 0; index < textMesh.TriangleIndices.Count; index++)
        {
            indices.Add(vertexOffset + textMesh.TriangleIndices[index]);
        }
    }

    /// <summary>
    /// Creates a counter-clockwise rectangle in the local tangent/normal frame.
    /// </summary>
    private static Vector2[] CreateRectangle(
        Vector2 center,
        Vector2 tangent,
        Vector2 normal,
        float length,
        float innerWidth,
        float outerWidth)
    {
        Vector2 tangentOffset = tangent * (length * 0.5f);
        Vector2 innerOffset = normal * innerWidth;
        Vector2 outerOffset = normal * outerWidth;

        return new[]
        {
            center - tangentOffset - innerOffset,
            center + tangentOffset - innerOffset,
            center + tangentOffset + outerOffset,
            center - tangentOffset + outerOffset
        };
    }

    /// <summary>
    /// Adds caps and side walls for corresponding bottom and top rectangles.
    /// </summary>
    private static void AddPrism(
        IReadOnlyList<Vector2> bottom,
        IReadOnlyList<Vector2> top,
        float bottomZ,
        float topZ,
        List<Vector3> positions,
        List<int> indices)
    {
        int bottomStart = positions.Count;

        for (int i = 0; i < bottom.Count; i++)
        {
            positions.Add(new Vector3(bottom[i], bottomZ));
        }

        int topStart = positions.Count;

        for (int i = 0; i < top.Count; i++)
        {
            positions.Add(new Vector3(top[i], topZ));
        }

        AddTriangle(bottomStart, bottomStart + 2, bottomStart + 1, indices);
        AddTriangle(bottomStart, bottomStart + 3, bottomStart + 2, indices);
        AddTriangle(topStart, topStart + 1, topStart + 2, indices);
        AddTriangle(topStart, topStart + 2, topStart + 3, indices);

        for (int i = 0; i < bottom.Count; i++)
        {
            int next = (i + 1) % bottom.Count;
            AddTriangle(bottomStart + i, bottomStart + next, topStart + next, indices);
            AddTriangle(bottomStart + i, topStart + next, topStart + i, indices);
        }
    }

    /// <summary>
    /// Converts the face angle from vertical into horizontal growth at the top edge.
    /// </summary>
    private static float CalculateChamferOutset(float height, float angleDegrees)
    {
        if (angleDegrees >= 89.999f)
        {
            return 0.0f;
        }

        float radians = angleDegrees * MathF.PI / 180.0f;
        return height / MathF.Tan(radians);
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
}
