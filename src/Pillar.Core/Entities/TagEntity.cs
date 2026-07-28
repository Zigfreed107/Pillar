// TagEntity.cs
// Defines one model-owned raft tag using renderer-neutral settings, placement, and triangle buffers.
using Pillar.Core.Layers;
using Pillar.Core.Tags;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Numerics;

namespace Pillar.Core.Entities;

/// <summary>
/// Represents one editable tag attached to the raft owned by an imported model.
/// </summary>
public sealed class TagEntity : CadEntity
{
    private SupportLayerColor _color;

    /// <summary>
    /// Creates one generated tag entity.
    /// </summary>
    public TagEntity(
        Guid modelEntityId,
        TagSettings settings,
        Vector3 attachmentPoint,
        Vector2 tangent,
        IReadOnlyList<Vector3> vertices,
        IReadOnlyList<int> triangleIndices,
        SupportLayerColor? color = null)
        : base((settings ?? throw new ArgumentNullException(nameof(settings))).GetDisplayName())
    {
        if (modelEntityId == Guid.Empty)
        {
            throw new ArgumentException("A tag must belong to an imported model.", nameof(modelEntityId));
        }

        if (!IsFinite(attachmentPoint))
        {
            throw new ArgumentOutOfRangeException(nameof(attachmentPoint), "A tag attachment point must be finite.");
        }

        if (!IsFinite(tangent) || tangent.LengthSquared() <= 0.00000001f)
        {
            throw new ArgumentOutOfRangeException(nameof(tangent), "A tag tangent must be finite and non-zero.");
        }

        ModelEntityId = modelEntityId;
        Settings = settings;
        AttachmentPoint = attachmentPoint;
        Tangent = Vector2.Normalize(tangent);
        _color = color ?? SupportLayerColorGenerator.CreateRandom();
        Vertices = new ReadOnlyCollection<Vector3>(new List<Vector3>(vertices ?? throw new ArgumentNullException(nameof(vertices))));
        TriangleIndices = new ReadOnlyCollection<int>(new List<int>(triangleIndices ?? throw new ArgumentNullException(nameof(triangleIndices))));
        ValidateMesh();
    }

    public Guid ModelEntityId { get; }
    public TagSettings Settings { get; }
    public Vector3 AttachmentPoint { get; }
    public Vector2 Tangent { get; }
    public IReadOnlyList<Vector3> Vertices { get; }
    public IReadOnlyList<int> TriangleIndices { get; }

    /// <summary>
    /// Gets the user-selected display color for this tag.
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
    /// Recreates one saved tag while preserving its document identity.
    /// </summary>
    public static TagEntity CreateLoaded(
        Guid id,
        Guid modelEntityId,
        TagSettings settings,
        Vector3 attachmentPoint,
        Vector2 tangent,
        IReadOnlyList<Vector3> vertices,
        IReadOnlyList<int> triangleIndices,
        SupportLayerColor? color = null)
    {
        SupportLayerColor loadedColor = color ?? SupportLayerColorGenerator.CreateFromStableSeed(id);
        TagEntity tag = new TagEntity(
            modelEntityId,
            settings,
            attachmentPoint,
            tangent,
            vertices,
            triangleIndices,
            loadedColor);
        tag.Id = id;
        return tag;
    }

    /// <summary>
    /// Applies a completed display-color edit to this tag.
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
            return (AttachmentPoint, AttachmentPoint);
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
            throw new ArgumentException("Tag triangle indices must be grouped in threes.");
        }

        for (int i = 0; i < TriangleIndices.Count; i++)
        {
            if (TriangleIndices[i] < 0 || TriangleIndices[i] >= Vertices.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(TriangleIndices), "A tag triangle index is outside its vertex buffer.");
            }
        }
    }

    /// <summary>
    /// Checks a 3D placement value without introducing rendering dependencies.
    /// </summary>
    private static bool IsFinite(Vector3 value)
    {
        return float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
    }

    /// <summary>
    /// Checks a 2D direction value without introducing rendering dependencies.
    /// </summary>
    private static bool IsFinite(Vector2 value)
    {
        return float.IsFinite(value.X) && float.IsFinite(value.Y);
    }
}
