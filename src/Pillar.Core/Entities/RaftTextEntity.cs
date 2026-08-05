// RaftTextEntity.cs
// Defines one model-owned raft text using renderer-neutral settings, placement, and triangle buffers.
using Pillar.Core.Layers;
using Pillar.Core.RaftTexts;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Numerics;

namespace Pillar.Core.Entities;

/// <summary>
/// Represents one editable text solid placed on the raft owned by an imported model.
/// </summary>
public sealed class RaftTextEntity : CadEntity
{
    private SupportLayerColor _color;

    /// <summary>
    /// Creates one generated raft text entity.
    /// </summary>
    public RaftTextEntity(
        Guid modelEntityId,
        RaftTextSettings settings,
        Vector3 placement,
        IReadOnlyList<Vector3> vertices,
        IReadOnlyList<int> triangleIndices,
        SupportLayerColor? color = null)
        : base((settings ?? throw new ArgumentNullException(nameof(settings))).GetDisplayName())
    {
        if (modelEntityId == Guid.Empty)
        {
            throw new ArgumentException("Raft text must belong to an imported model.", nameof(modelEntityId));
        }

        if (!IsFinite(placement))
        {
            throw new ArgumentOutOfRangeException(nameof(placement), "A raft text placement must be finite.");
        }

        ModelEntityId = modelEntityId;
        Settings = settings;
        Placement = placement;
        _color = color ?? SupportLayerColorGenerator.CreateRandom();
        Vertices = new ReadOnlyCollection<Vector3>(new List<Vector3>(vertices ?? throw new ArgumentNullException(nameof(vertices))));
        TriangleIndices = new ReadOnlyCollection<int>(new List<int>(triangleIndices ?? throw new ArgumentNullException(nameof(triangleIndices))));
        ValidateMesh();
    }

    public Guid ModelEntityId { get; }
    public RaftTextSettings Settings { get; }
    public Vector3 Placement { get; }
    public IReadOnlyList<Vector3> Vertices { get; }
    public IReadOnlyList<int> TriangleIndices { get; }

    /// <summary>
    /// Gets the user-selected display color for this raft text.
    /// </summary>
    public SupportLayerColor Color
    {
        get { return _color; }
        private set
        {
            if (_color == value)
            {
                return;
            }

            _color = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Recreates one saved raft text while preserving its document identity.
    /// </summary>
    public static RaftTextEntity CreateLoaded(
        Guid id,
        Guid modelEntityId,
        RaftTextSettings settings,
        Vector3 placement,
        IReadOnlyList<Vector3> vertices,
        IReadOnlyList<int> triangleIndices,
        SupportLayerColor? color = null)
    {
        SupportLayerColor loadedColor = color ?? SupportLayerColorGenerator.CreateFromStableSeed(id);
        RaftTextEntity raftText = new RaftTextEntity(
            modelEntityId,
            settings,
            placement,
            vertices,
            triangleIndices,
            loadedColor);
        raftText.Id = id;
        return raftText;
    }

    /// <summary>
    /// Applies a completed display-color edit to this raft text.
    /// </summary>
    public void SetColor(SupportLayerColor color)
    {
        Color = color;
    }

    /// <summary>
    /// Returns bounds for framing, hit testing, and selection.
    /// </summary>
    public override (Vector3 Min, Vector3 Max) GetBounds()
    {
        if (Vertices.Count == 0)
        {
            return (Placement, Placement);
        }

        Vector3 minimum = Vertices[0];
        Vector3 maximum = Vertices[0];

        for (int i = 1; i < Vertices.Count; i++)
        {
            minimum = Vector3.Min(minimum, Vertices[i]);
            maximum = Vector3.Max(maximum, Vertices[i]);
        }

        return (minimum, maximum);
    }

    /// <summary>
    /// Validates indices before generated data enters the document.
    /// </summary>
    private void ValidateMesh()
    {
        if (TriangleIndices.Count % 3 != 0)
        {
            throw new ArgumentException("Raft text triangle indices must be grouped in threes.");
        }

        for (int i = 0; i < TriangleIndices.Count; i++)
        {
            if (TriangleIndices[i] < 0 || TriangleIndices[i] >= Vertices.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(TriangleIndices), "A raft text triangle index is outside its vertex buffer.");
            }
        }
    }

    /// <summary>
    /// Checks one placement value without introducing rendering dependencies.
    /// </summary>
    private static bool IsFinite(Vector3 value)
    {
        return float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
    }
}
