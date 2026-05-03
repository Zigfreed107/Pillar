// SupportLayerGroup.cs
// Defines document-level support grouping metadata for imported mesh layers without coupling layers to rendering.
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Pillar.Core.Layers;

/// <summary>
/// Represents a user-managed support group under one imported model layer.
/// </summary>
public sealed class SupportLayerGroup : INotifyPropertyChanged
{
    private string _name;
    private SupportLayerColor _color;
    private SupportGroupGeneratorKind _generatorKind;
    private RingSupportSettings? _ringSupportSettings;

    /// <summary>
    /// Creates a new support group under the supplied imported model entity.
    /// </summary>
    public SupportLayerGroup(Guid modelEntityId, string name)
        : this(Guid.NewGuid(), modelEntityId, name, SupportLayerColorGenerator.CreateRandom())
    {
    }

    /// <summary>
    /// Creates a new support group under the supplied imported model entity using the supplied color.
    /// </summary>
    public SupportLayerGroup(Guid modelEntityId, string name, SupportLayerColor color)
        : this(Guid.NewGuid(), modelEntityId, name, color)
    {
    }

    /// <summary>
    /// Creates a support group with a stable identity, usually when loading a saved project.
    /// </summary>
    private SupportLayerGroup(Guid id, Guid modelEntityId, string name, SupportLayerColor color)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("A support group must have a valid identifier.", nameof(id));
        }

        if (modelEntityId == Guid.Empty)
        {
            throw new ArgumentException("A support group must belong to a model entity.", nameof(modelEntityId));
        }

        Id = id;
        ModelEntityId = modelEntityId;
        _name = NormalizeName(name);
        _color = color;
        _generatorKind = SupportGroupGeneratorKind.None;
    }

    /// <summary>
    /// Raised when user-visible layer metadata changes.
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Gets the stable identifier for this support group.
    /// </summary>
    public Guid Id { get; }

    /// <summary>
    /// Gets the imported mesh entity that owns this support group.
    /// </summary>
    public Guid ModelEntityId { get; }

    /// <summary>
    /// Gets the user-visible support group name.
    /// </summary>
    public string Name
    {
        get { return _name; }
        private set
        {
            string normalizedName = NormalizeName(value);

            if (string.Equals(_name, normalizedName, StringComparison.Ordinal))
            {
                return;
            }

            _name = normalizedName;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Gets the display color for supports that belong to this group.
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
    /// Gets the parametric generator kind that owns this support group, if any.
    /// </summary>
    public SupportGroupGeneratorKind GeneratorKind
    {
        get { return _generatorKind; }
        private set
        {
            if (_generatorKind == value)
            {
                return;
            }

            _generatorKind = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Gets a copy of the Ring Support settings when this group is Ring-tool generated.
    /// </summary>
    public RingSupportSettings? RingSupportSettings
    {
        get { return _ringSupportSettings?.Clone(); }
        private set
        {
            _ringSupportSettings = value?.Clone();
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Recreates a saved support group while preserving its document identity.
    /// </summary>
    public static SupportLayerGroup CreateLoaded(Guid id, Guid modelEntityId, string name, SupportLayerColor? color = null)
    {
        SupportLayerColor loadedColor = color ?? SupportLayerColorGenerator.CreateFromStableSeed(id);
        return new SupportLayerGroup(id, modelEntityId, name, loadedColor);
    }

    /// <summary>
    /// Recreates a saved support group with Ring Support generator metadata.
    /// </summary>
    public static SupportLayerGroup CreateLoaded(
        Guid id,
        Guid modelEntityId,
        string name,
        SupportLayerColor? color,
        RingSupportSettings? ringSupportSettings)
    {
        SupportLayerGroup supportLayerGroup = CreateLoaded(id, modelEntityId, name, color);

        if (ringSupportSettings != null)
        {
            supportLayerGroup.SetRingSupportSettings(ringSupportSettings);
        }

        return supportLayerGroup;
    }

    /// <summary>
    /// Applies a completed rename edit to this group.
    /// </summary>
    public void Rename(string name)
    {
        Name = name;
    }

    /// <summary>
    /// Applies a completed color edit to this group.
    /// </summary>
    public void SetColor(SupportLayerColor color)
    {
        Color = color;
    }

    /// <summary>
    /// Marks this group as Ring Support generated and stores the editable generator settings.
    /// </summary>
    public void SetRingSupportSettings(RingSupportSettings settings)
    {
        if (settings == null)
        {
            throw new ArgumentNullException(nameof(settings));
        }

        RingSupportSettings = settings;
        GeneratorKind = SupportGroupGeneratorKind.RingSupport;
    }

    /// <summary>
    /// Clears parametric generator metadata, leaving this as a plain support group.
    /// </summary>
    public void ClearGeneratorSettings()
    {
        RingSupportSettings = null;
        GeneratorKind = SupportGroupGeneratorKind.None;
    }

    /// <summary>
    /// Converts blank user-entered names into a stable fallback.
    /// </summary>
    private static string NormalizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "Supports Group";
        }

        return name.Trim();
    }

    /// <summary>
    /// Notifies observers that one property changed.
    /// </summary>
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
