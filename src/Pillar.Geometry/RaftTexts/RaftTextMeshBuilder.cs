// RaftTextMeshBuilder.cs
// Reuses the tag glyph extrusion path, applies plan-view orientation, and places text on a raft.
using Pillar.Core.RaftTexts;
using Pillar.Geometry.Tags;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace Pillar.Geometry.RaftTexts;

/// <summary>
/// Builds local-space solid glyphs and places them without rendering dependencies.
/// </summary>
public static class RaftTextMeshBuilder
{
    /// <summary>
    /// Extrudes and orients text around its local origin for later raft placement.
    /// </summary>
    public static RaftTextMeshData BuildLocal(RaftTextSettings settings, TagTextOutlineData outline)
    {
        if (settings == null)
        {
            throw new ArgumentNullException(nameof(settings));
        }

        TagTextMeshData textMesh = TagTextMeshBuilder.Build(
            -settings.TextHeight * 0.5f,
            settings.TextHeight,
            outline);
        IReadOnlyList<Vector3> positions = RotateInPlanView(
            textMesh.Positions,
            settings.OrientationDegrees);
        return new RaftTextMeshData(positions, textMesh.TriangleIndices);
    }

    /// <summary>
    /// Translates one reusable local text mesh to its accepted raft position.
    /// </summary>
    public static RaftTextMeshData Place(RaftTextMeshData localMesh, Vector3 placement)
    {
        if (localMesh == null)
        {
            throw new ArgumentNullException(nameof(localMesh));
        }

        if (!float.IsFinite(placement.X) || !float.IsFinite(placement.Y) || !float.IsFinite(placement.Z))
        {
            throw new ArgumentOutOfRangeException(nameof(placement), "A raft text placement must be finite.");
        }

        List<Vector3> positions = new List<Vector3>(localMesh.Positions.Count);

        for (int i = 0; i < localMesh.Positions.Count; i++)
        {
            positions.Add(localMesh.Positions[i] + placement);
        }

        return new RaftTextMeshData(positions, localMesh.TriangleIndices);
    }

    /// <summary>
    /// Rotates local glyph vertices counter-clockwise about +Z while preserving their extrusion.
    /// </summary>
    private static IReadOnlyList<Vector3> RotateInPlanView(
        IReadOnlyList<Vector3> sourcePositions,
        float orientationDegrees)
    {
        if (orientationDegrees == 0.0f || orientationDegrees == 360.0f)
        {
            return sourcePositions;
        }

        float radians = orientationDegrees * (MathF.PI / 180.0f);
        float cosine = MathF.Cos(radians);
        float sine = MathF.Sin(radians);
        List<Vector3> rotatedPositions = new List<Vector3>(sourcePositions.Count);

        for (int i = 0; i < sourcePositions.Count; i++)
        {
            Vector3 source = sourcePositions[i];
            rotatedPositions.Add(new Vector3(
                source.X * cosine - source.Y * sine,
                source.X * sine + source.Y * cosine,
                source.Z));
        }

        return rotatedPositions;
    }
}
