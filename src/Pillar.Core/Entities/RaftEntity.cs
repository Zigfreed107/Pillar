// RaftEntity.cs
// Defines one model-owned procedural raft using renderer-neutral settings and triangle buffers.
using Pillar.Core.Layers;
using Pillar.Core.Rafts;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Numerics;

namespace Pillar.Core.Entities;

/// <summary>
/// Represents the single optional raft owned by an imported model.
/// </summary>
public sealed class RaftEntity : CadEntity
{
    private SupportLayerColor _color;

    public Guid ModelEntityId { get; }
    public RaftSettings Settings { get; }

    /// <summary>
    /// Gets the user-selected display color for this raft.
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

    public IReadOnlyList<Vector3> Vertices { get; }
    public IReadOnlyList<int> TriangleIndices { get; }

    /// <summary>
    /// Creates one generated raft entity.
    /// </summary>
    public RaftEntity(
        Guid modelEntityId,
        RaftSettings settings,
        IReadOnlyList<Vector3> vertices,
        IReadOnlyList<int> triangleIndices,
        SupportLayerColor? color = null)
        : base((settings ?? throw new ArgumentNullException(nameof(settings))).GetDisplayName())
    {
        if (modelEntityId == Guid.Empty)
        {
            throw new ArgumentException("A raft must belong to an imported model.", nameof(modelEntityId));
        }

        ModelEntityId = modelEntityId;
        Settings = settings;
        _color = color ?? SupportLayerColorGenerator.CreateRandom();
        Vertices = new ReadOnlyCollection<Vector3>(new List<Vector3>(vertices ?? throw new ArgumentNullException(nameof(vertices))));
        TriangleIndices = new ReadOnlyCollection<int>(new List<int>(triangleIndices ?? throw new ArgumentNullException(nameof(triangleIndices))));
        ValidateMesh();
    }

    /// <summary>
    /// Recreates one saved raft while preserving its document identity.
    /// </summary>
    public static RaftEntity CreateLoaded(
        Guid id,
        Guid modelEntityId,
        RaftSettings settings,
        IReadOnlyList<Vector3> vertices,
        IReadOnlyList<int> triangleIndices,
        SupportLayerColor? color = null)
    {
        SupportLayerColor loadedColor = color ?? SupportLayerColorGenerator.CreateFromStableSeed(id);
        RaftEntity raft = new RaftEntity(modelEntityId, settings, vertices, triangleIndices, loadedColor);
        raft.Id = id;
        return raft;
    }

    /// <summary>
    /// Applies a completed display-color edit to this raft.
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
            return (Vector3.Zero, Vector3.Zero);
        }

        Vector3 min = Vertices[0];
        Vector3 max = Vertices[0];

        for (int i = 1; i < Vertices.Count; i++)
        {
            min = Vector3.Min(min, Vertices[i]);
            max = Vector3.Max(max, Vertices[i]);
        }

        return (min, max);
    }

    /// <summary>
    /// Validates indices before generated data enters the document.
    /// </summary>
    private void ValidateMesh()
    {
        if (TriangleIndices.Count % 3 != 0)
        {
            throw new ArgumentException("Raft triangle indices must be grouped in threes.");
        }

        for (int i = 0; i < TriangleIndices.Count; i++)
        {
            if (TriangleIndices[i] < 0 || TriangleIndices[i] >= Vertices.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(TriangleIndices), "A raft triangle index is outside its vertex buffer.");
            }
        }
    }
}
