// SupportModifierPipeline.cs
// Evaluates support-layer modifier stacks against generated support entities without depending on rendering.
using Pillar.Core.Entities;
using Pillar.Core.Layers;
using System;
using System.Collections.Generic;
using System.Linq;

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
        if (sourceSupports == null)
        {
            throw new ArgumentNullException(nameof(sourceSupports));
        }

        if (modifiers == null)
        {
            throw new ArgumentNullException(nameof(modifiers));
        }

        List<SupportEntity> currentSupports = new List<SupportEntity>(sourceSupports);
        List<SupportModifierDefinition> orderedModifiers = modifiers
            .Where((SupportModifierDefinition modifier) => modifier.IsEnabled)
            .OrderBy((SupportModifierDefinition modifier) => modifier.Order)
            .ToList();

        for (int i = 0; i < orderedModifiers.Count; i++)
        {
            SupportModifierDefinition modifier = orderedModifiers[i];

            if (modifier.Kind == SupportModifierKind.Cluster)
            {
                SupportClusterEvaluationResult result = SupportClusterPlanner.Evaluate(currentSupports, modifier);
                currentSupports = new List<SupportEntity>(result.SupportEntities);
            }
            else if (modifier.Kind == SupportModifierKind.Brace)
            {
                SupportBracingEvaluationResult result = SupportBracingPlanner.EvaluateBrace(currentSupports, modifier);
                currentSupports = new List<SupportEntity>(result.SupportEntities);
            }
            else if (modifier.Kind == SupportModifierKind.Buttress)
            {
                SupportBracingEvaluationResult result = SupportBracingPlanner.EvaluateButtress(currentSupports, modifier);
                currentSupports = new List<SupportEntity>(result.SupportEntities);
            }
        }

        return currentSupports;
    }
}
