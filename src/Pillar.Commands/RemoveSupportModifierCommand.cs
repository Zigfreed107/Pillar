// RemoveSupportModifierCommand.cs
// Removes one saved support-layer modifier and rebuilds the layer output as a single undoable document edit.
using Pillar.Core.Document;
using Pillar.Core.Entities;
using Pillar.Core.Layers;
using Pillar.Core.Supports;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace Pillar.Commands;

/// <summary>
/// Removes one support modifier from a support layer and restores the remaining evaluated support output.
/// </summary>
public sealed class RemoveSupportModifierCommand : ICadCommand
{
    private readonly ReplaceSupportLayerOutputAndModifiersCommand _innerCommand;

    /// <summary>
    /// Creates an undoable modifier-removal edit for the supplied support layer.
    /// </summary>
    public RemoveSupportModifierCommand(CadDocument document, SupportLayerGroup supportLayerGroup, Guid supportModifierId)
    {
        if (document == null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        if (supportLayerGroup == null)
        {
            throw new ArgumentNullException(nameof(supportLayerGroup));
        }

        if (supportModifierId == Guid.Empty)
        {
            throw new ArgumentException("A support modifier id is required.", nameof(supportModifierId));
        }

        IReadOnlyList<SupportEntity> oldSupportEntities = document.GetSupportEntitiesForGroup(supportLayerGroup.Id);
        IReadOnlyList<SupportModifierDefinition> oldModifiers = supportLayerGroup.SupportModifiers;
        List<SupportModifierDefinition> newModifiers = new List<SupportModifierDefinition>();
        SupportModifierDefinition? removedModifier = null;

        for (int i = 0; i < oldModifiers.Count; i++)
        {
            if (oldModifiers[i].Id == supportModifierId)
            {
                removedModifier = oldModifiers[i];
                continue;
            }

            newModifiers.Add(oldModifiers[i]);
        }

        if (removedModifier == null)
        {
            throw new InvalidOperationException("The requested support modifier was not found in the selected support layer.");
        }

        IReadOnlyList<SupportEntity> sourceSupportEntities = RestoreIndividualSupportsForModifierReplay(oldSupportEntities);
        IReadOnlyList<SupportEntity> newSupportEntities = SupportModifierPipeline.ApplyModifiers(sourceSupportEntities, newModifiers);
        _innerCommand = new ReplaceSupportLayerOutputAndModifiersCommand(
            document,
            supportLayerGroup,
            oldSupportEntities,
            newSupportEntities,
            oldModifiers,
            newModifiers,
            $"Remove {GetModifierDisplayName(removedModifier.Kind)} Modifier");
    }

    /// <summary>
    /// Gets the short user-facing name shown in undo and redo status messages.
    /// </summary>
    public string DisplayName
    {
        get { return _innerCommand.DisplayName; }
    }

    /// <summary>
    /// Applies the modifier removal.
    /// </summary>
    public void Execute()
    {
        _innerCommand.Execute();
    }

    /// <summary>
    /// Restores the removed modifier and its evaluated support output.
    /// </summary>
    public void Undo()
    {
        _innerCommand.Undo();
    }

    /// <summary>
    /// Converts clustered output back to individual supports before replaying the remaining modifier stack.
    /// </summary>
    private static IReadOnlyList<SupportEntity> RestoreIndividualSupportsForModifierReplay(IReadOnlyList<SupportEntity> supportEntities)
    {
        List<SupportEntity> restoredSupports = new List<SupportEntity>(supportEntities.Count);

        for (int i = 0; i < supportEntities.Count; i++)
        {
            SupportEntity support = supportEntities[i];

            if (support.Style.Kind == SupportStyleKind.BraceMember || support.Style.Kind == SupportStyleKind.Buttress)
            {
                continue;
            }

            if (support.Style.Kind != SupportStyleKind.Clustered)
            {
                restoredSupports.Add(support);
                continue;
            }

            Vector3 headDirection = SupportHeadDirectionCalculator.ClampDirectionToProfile(support.HeadDirection, support.Profile);
            Vector3 headJointPosition = support.TipPosition - (headDirection * support.Profile.HeadHeight);
            Vector3 basePosition = new Vector3(headJointPosition.X, headJointPosition.Y, support.BasePosition.Z);
            restoredSupports.Add(SupportEntity.CreateLoaded(
                support.Id,
                support.Name,
                support.SupportLayerGroupId,
                support.TipPosition,
                basePosition,
                support.HeadDirection,
                0.0f,
                Vector3.UnitZ,
                support.Profile));
        }

        return restoredSupports;
    }

    /// <summary>
    /// Converts one modifier kind into a short user-facing action label.
    /// </summary>
    private static string GetModifierDisplayName(SupportModifierKind supportModifierKind)
    {
        switch (supportModifierKind)
        {
            case SupportModifierKind.Cluster:
                return "Cluster";

            case SupportModifierKind.Brace:
                return "Brace";

            case SupportModifierKind.Buttress:
                return "Buttress";

            case SupportModifierKind.Delete:
                return "Delete";

            default:
                return supportModifierKind.ToString();
        }
    }
}
