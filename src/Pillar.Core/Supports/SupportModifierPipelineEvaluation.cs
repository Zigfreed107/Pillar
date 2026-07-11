// SupportModifierPipelineEvaluation.cs
// Carries one modifier-stack output plus optional diagnostics captured during that same evaluation pass.
using Pillar.Core.Entities;
using System;
using System.Collections.Generic;

namespace Pillar.Core.Supports;

/// <summary>
/// Stores final support output and diagnostics for one observed bracing modifier.
/// </summary>
public sealed class SupportModifierPipelineEvaluation
{
    /// <summary>
    /// Creates one completed modifier-pipeline evaluation.
    /// </summary>
    public SupportModifierPipelineEvaluation(
        IReadOnlyList<SupportEntity> supportEntities,
        SupportBracingEvaluationResult? capturedBracingResult)
    {
        SupportEntities = supportEntities ?? throw new ArgumentNullException(nameof(supportEntities));
        CapturedBracingResult = capturedBracingResult;
    }

    /// <summary>
    /// Gets the final support output after all enabled modifiers have run.
    /// </summary>
    public IReadOnlyList<SupportEntity> SupportEntities { get; }

    /// <summary>
    /// Gets diagnostics captured for the requested Brace or Buttress modifier.
    /// </summary>
    public SupportBracingEvaluationResult? CapturedBracingResult { get; }
}
