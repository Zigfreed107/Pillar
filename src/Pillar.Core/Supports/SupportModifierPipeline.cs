// SupportModifierPipeline.cs
// Evaluates support-layer modifier stacks against generated support entities without depending on rendering.
using Pillar.Core.Entities;
using Pillar.Core.Layers;
using System;
using System.Collections.Generic;

namespace Pillar.Core.Supports;

/// <summary>
/// Applies enabled support modifiers in their stored order.
/// </summary>
public static class SupportModifierPipeline
{
    /// <summary>
    /// Replays all enabled modifiers against the supplied generated support output.
    /// </summary>
    public static IReadOnlyList<SupportEntity> ApplyModifiers(
        IReadOnlyList<SupportEntity> sourceSupports,
        IReadOnlyList<SupportModifierDefinition> modifiers)
    {
        return EvaluateModifiers(sourceSupports, modifiers, null).SupportEntities;
    }

    /// <summary>
    /// Applies enabled modifiers while capturing diagnostics for one requested bracing modifier.
    /// </summary>
    public static SupportModifierPipelineEvaluation EvaluateModifiers(
        IReadOnlyList<SupportEntity> sourceSupports,
        IReadOnlyList<SupportModifierDefinition> modifiers,
        Guid? capturedBracingModifierId)
    {
        if (sourceSupports == null)
        {
            throw new ArgumentNullException(nameof(sourceSupports));
        }

        if (modifiers == null)
        {
            throw new ArgumentNullException(nameof(modifiers));
        }

        IReadOnlyList<SupportEntity> currentSupports = sourceSupports;
        List<SupportModifierDefinition> orderedModifiers = new List<SupportModifierDefinition>(modifiers.Count);
        SupportBracingEvaluationResult? capturedBracingResult = null;

        for (int i = 0; i < modifiers.Count; i++)
        {
            SupportModifierDefinition modifier = modifiers[i];

            if (!modifier.IsEnabled)
            {
                continue;
            }

            int insertIndex = orderedModifiers.Count;

            while (insertIndex > 0 && orderedModifiers[insertIndex - 1].Order > modifier.Order)
            {
                insertIndex--;
            }

            orderedModifiers.Insert(insertIndex, modifier);
        }

        MoveDirectEditsBeforeReinforcement(orderedModifiers);

        for (int i = 0; i < orderedModifiers.Count; i++)
        {
            SupportModifierDefinition modifier = orderedModifiers[i];

            if (modifier.Kind == SupportModifierKind.Cluster)
            {
                SupportClusterEvaluationResult result = SupportClusterPlanner.Evaluate(currentSupports, modifier);
                currentSupports = result.SupportEntities;
            }
            else if (modifier.Kind == SupportModifierKind.DirectEdit)
            {
                currentSupports = SupportDirectEditPlanner.Evaluate(currentSupports, modifier);
            }
            else if (modifier.Kind == SupportModifierKind.Brace)
            {
                SupportBracingEvaluationResult result = SupportBracingPlanner.EvaluateBrace(currentSupports, modifier);
                currentSupports = result.SupportEntities;

                if (capturedBracingModifierId.HasValue && modifier.Id == capturedBracingModifierId.Value)
                {
                    capturedBracingResult = result;
                }
            }
            else if (modifier.Kind == SupportModifierKind.Buttress)
            {
                SupportBracingEvaluationResult result = SupportBracingPlanner.EvaluateButtress(currentSupports, modifier);
                currentSupports = result.SupportEntities;

                if (capturedBracingModifierId.HasValue && modifier.Id == capturedBracingModifierId.Value)
                {
                    capturedBracingResult = result;
                }
            }
        }

        IReadOnlyList<SupportEntity> finalSupports = orderedModifiers.Count == 0
            ? new List<SupportEntity>(sourceSupports)
            : currentSupports;
        return new SupportModifierPipelineEvaluation(finalSupports, capturedBracingResult);
    }
    /// <summary>
    /// Moves direct geometry edits ahead of reinforcement so stored brace rules replay against edited stems.
    /// </summary>
    private static void MoveDirectEditsBeforeReinforcement(List<SupportModifierDefinition> modifiers)
    {
        int firstReinforcementIndex = modifiers.Count;

        for (int i = 0; i < modifiers.Count; i++)
        {
            if (modifiers[i].Kind == SupportModifierKind.Brace || modifiers[i].Kind == SupportModifierKind.Buttress)
            {
                firstReinforcementIndex = i;
                break;
            }
        }

        for (int i = firstReinforcementIndex + 1; i < modifiers.Count; i++)
        {
            if (modifiers[i].Kind != SupportModifierKind.DirectEdit)
            {
                continue;
            }

            SupportModifierDefinition directEdit = modifiers[i];
            modifiers.RemoveAt(i);
            modifiers.Insert(firstReinforcementIndex, directEdit);
            firstReinforcementIndex++;
        }
    }
}
