// SetMeshUserTransformCommand.cs
// Provides the undoable command boundary for changing an imported mesh model transform and its attached support groups.
using Pillar.Core.Document;
using Pillar.Core.Entities;
using Pillar.Core.Layers;
using Pillar.Core.Supports;
using System;
using System.Collections.Generic;

namespace Pillar.Commands;

/// <summary>
/// Replaces an imported mesh user transform and can restore the previous transform.
/// </summary>
public sealed class SetMeshUserTransformCommand : ICadCommand
{
    private readonly CadDocument? _document;
    private readonly MeshEntity _mesh;
    private readonly Transform3DData _oldTransform;
    private readonly Transform3DData _newTransform;
    private readonly IReadOnlyList<SupportGroupRegeneration> _supportRegenerations;
    private bool _hasExecuted;

    /// <summary>
    /// Creates an undoable mesh transform edit.
    /// </summary>
    public SetMeshUserTransformCommand(MeshEntity mesh, Transform3DData oldTransform, Transform3DData newTransform, string? displayName = null)
        : this(null, mesh, oldTransform, newTransform, Array.Empty<SupportGroupRegeneration>(), displayName)
    {
    }

    /// <summary>
    /// Creates an undoable mesh transform edit that also replaces support groups regenerated for the new transform.
    /// </summary>
    public SetMeshUserTransformCommand(
        CadDocument? document,
        MeshEntity mesh,
        Transform3DData oldTransform,
        Transform3DData newTransform,
        IReadOnlyList<SupportGroupRegeneration> supportRegenerations,
        string? displayName = null)
    {
        _document = document;
        _mesh = mesh ?? throw new ArgumentNullException(nameof(mesh));
        _oldTransform = oldTransform;
        _newTransform = newTransform;
        _supportRegenerations = supportRegenerations ?? throw new ArgumentNullException(nameof(supportRegenerations));
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? "Scale Model" : displayName.Trim();

        if (_supportRegenerations.Count > 0 && _document == null)
        {
            throw new ArgumentException("A document is required when mesh transform support regenerations are supplied.", nameof(document));
        }
    }

    /// <summary>
    /// Gets the short user-facing name shown in undo and redo status messages.
    /// </summary>
    public string DisplayName { get; }

    /// <summary>
    /// Applies the new mesh transform.
    /// </summary>
    public void Execute()
    {
        if (_hasExecuted)
        {
            return;
        }

        _mesh.UserTransform = _newTransform;
        ApplySupportRegenerations(useNewState: true);
        _hasExecuted = true;
    }

    /// <summary>
    /// Restores the previous mesh transform.
    /// </summary>
    public void Undo()
    {
        if (!_hasExecuted)
        {
            return;
        }

        _mesh.UserTransform = _oldTransform;
        ApplySupportRegenerations(useNewState: false);
        _hasExecuted = false;
    }

    /// <summary>
    /// Replaces support entities and generator metadata with either the new regenerated state or the previous undo state.
    /// </summary>
    private void ApplySupportRegenerations(bool useNewState)
    {
        if (_document == null)
        {
            return;
        }

        for (int i = 0; i < _supportRegenerations.Count; i++)
        {
            SupportGroupRegeneration supportRegeneration = _supportRegenerations[i];
            IReadOnlyList<SupportEntity> supportsToRemove = useNewState
                ? supportRegeneration.OldSupportEntities
                : supportRegeneration.NewSupportEntities;
            IReadOnlyList<SupportEntity> supportsToAdd = useNewState
                ? supportRegeneration.NewSupportEntities
                : supportRegeneration.OldSupportEntities;

            ReplaceSupportEntities(supportsToRemove, supportsToAdd);
            ApplyGeneratorSettings(supportRegeneration, useNewState);
        }
    }

    /// <summary>
    /// Removes the obsolete generated support entities and adds their regenerated replacements.
    /// </summary>
    private void ReplaceSupportEntities(IReadOnlyList<SupportEntity> supportsToRemove, IReadOnlyList<SupportEntity> supportsToAdd)
    {
        if (_document == null)
        {
            return;
        }

        for (int i = supportsToRemove.Count - 1; i >= 0; i--)
        {
            _document.RemoveEntity(supportsToRemove[i]);
        }

        for (int i = 0; i < supportsToAdd.Count; i++)
        {
            _document.AddEntity(supportsToAdd[i]);
        }
    }

    /// <summary>
    /// Restores generator settings when a regenerated group is controlled by a parametric support tool.
    /// </summary>
    private static void ApplyGeneratorSettings(SupportGroupRegeneration supportRegeneration, bool useNewState)
    {
        RingSupportSettings? settings = useNewState
            ? supportRegeneration.NewRingSupportSettings
            : supportRegeneration.OldRingSupportSettings;

        if (settings != null)
        {
            supportRegeneration.SupportLayerGroup.SetRingSupportSettings(settings);
            return;
        }

        LineSupportSettings? lineSettings = useNewState
            ? supportRegeneration.NewLineSupportSettings
            : supportRegeneration.OldLineSupportSettings;

        if (lineSettings != null)
        {
            supportRegeneration.SupportLayerGroup.SetLineSupportSettings(lineSettings);
        }
    }
}
