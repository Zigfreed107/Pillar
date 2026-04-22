// GphDocumentData.cs
// Carries loaded Graphite project entities and layer metadata before they are applied to a document.
using Pillar.Core.Entities;
using Pillar.Core.Layers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Pillar.Core.Persistence;

/// <summary>
/// Represents the complete document payload read from a Graphite project file.
/// </summary>
public sealed class GphDocumentData
{
    /// <summary>
    /// Creates an immutable loaded document payload.
    /// </summary>
    public GphDocumentData(IReadOnlyList<CadEntity> entities, IReadOnlyList<SupportLayerGroup> supportLayerGroups)
    {
        if (entities == null)
        {
            throw new ArgumentNullException(nameof(entities));
        }

        if (supportLayerGroups == null)
        {
            throw new ArgumentNullException(nameof(supportLayerGroups));
        }

        Entities = new ReadOnlyCollection<CadEntity>(new List<CadEntity>(entities));
        SupportLayerGroups = new ReadOnlyCollection<SupportLayerGroup>(new List<SupportLayerGroup>(supportLayerGroups));
    }

    /// <summary>
    /// Gets the loaded CAD entities.
    /// </summary>
    public IReadOnlyList<CadEntity> Entities { get; }

    /// <summary>
    /// Gets the loaded support layer groups.
    /// </summary>
    public IReadOnlyList<SupportLayerGroup> SupportLayerGroups { get; }
}
