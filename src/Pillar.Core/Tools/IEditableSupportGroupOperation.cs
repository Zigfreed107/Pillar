// IEditableSupportGroupOperation.cs
// Marks support operations that are editing an existing generated support group.
using System;

namespace Pillar.Core.Tools;

/// <summary>
/// Exposes the active generated support group being edited by a support operation.
/// </summary>
public interface IEditableSupportGroupOperation
{
    /// <summary>
    /// Gets the support layer group currently being edited, or null when the operation is creating new supports.
    /// </summary>
    Guid? EditingSupportLayerGroupId { get; }
}
