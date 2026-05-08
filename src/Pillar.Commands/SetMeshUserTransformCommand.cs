// SetMeshUserTransformCommand.cs
// Provides the undoable command boundary for changing an imported mesh model transform.
using Pillar.Core.Entities;
using System;

namespace Pillar.Commands;

/// <summary>
/// Replaces an imported mesh user transform and can restore the previous transform.
/// </summary>
public sealed class SetMeshUserTransformCommand : ICadCommand
{
    private readonly MeshEntity _mesh;
    private readonly Transform3DData _oldTransform;
    private readonly Transform3DData _newTransform;
    private bool _hasExecuted;

    /// <summary>
    /// Creates an undoable mesh transform edit.
    /// </summary>
    public SetMeshUserTransformCommand(MeshEntity mesh, Transform3DData oldTransform, Transform3DData newTransform, string? displayName = null)
    {
        _mesh = mesh ?? throw new ArgumentNullException(nameof(mesh));
        _oldTransform = oldTransform;
        _newTransform = newTransform;
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? "Scale Model" : displayName.Trim();
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
        _hasExecuted = false;
    }
}
