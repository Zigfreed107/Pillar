// Program.cs
// Runs dependency-free geometry smoke tests for procedural support meshes so export regressions are caught early.
using Pillar.Commands;
using Pillar.Core.Document;
using Pillar.Core.Entities;
using Pillar.Core.Layers;
using Pillar.Core.Persistence;
using Pillar.Core.Selection;
using Pillar.Core.Supports;
using Pillar.Geometry.Analysis;
using Pillar.Geometry.Supports;
using Pillar.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text.Json.Nodes;

namespace Pillar.Geometry.SmokeTests;

/// <summary>
/// Provides a small executable validation harness for generated support meshes.
/// </summary>
public static class Program
{
    private const float TriangleAreaTolerance = 0.00000001f;
    private const float CoordinateQuantizationScale = 100000.0f;

    /// <summary>
    /// Runs all smoke tests and returns a process exit code.
    /// </summary>
    public static int Main()
    {
        List<string> failures = new List<string>();

        RunTest(failures, "Normal support mesh is closed", ValidateNormalSupportMesh);
        RunTest(failures, "Short support mesh has no radius mismatch boundary", ValidateShortSupportMesh);
        RunTest(failures, "Angled seam closes without a 2 PI endpoint", ValidateAngledSeamSupportMesh);
        RunTest(failures, "Default support head direction stays vertical", ValidateDefaultSupportHeadDirectionStaysVertical);
        RunTest(failures, "Support head direction clamps to profile angle", ValidateSupportHeadDirectionClampsToProfileAngle);
        RunTest(failures, "Side face support head approaches from outside", ValidateSideFaceSupportHeadApproachesFromOutside);
        RunTest(failures, "Near-vertical positive-Z side face remains supportable", ValidateNearVerticalPositiveZSideFaceRemainsSupportable);
        RunTest(failures, "Upward support contact is rejected", ValidateUpwardSupportContactIsRejected);
        RunTest(failures, "Downward overhang support placement remains valid", ValidateDownwardOverhangSupportPlacementRemainsValid);
        RunTest(failures, "Angled head support mesh is closed", ValidateAngledHeadSupportMesh);
        RunTest(failures, "Joint ball normals point outward", ValidateJointBallNormalsPointOutward);
        RunTest(failures, "Default branch setting creates no branch", ValidateDefaultBranchSettingCreatesNoBranch);
        RunTest(failures, "Branch profile fields validate non-negative values", ValidateBranchProfileFieldsValidateNonNegativeValues);
        RunTest(failures, "Branch angle is independent from head angle", ValidateBranchAngleIsIndependentFromHeadAngle);
        RunTest(failures, "Branch support mesh is closed with outward balls", ValidateBranchSupportMeshIsClosedWithOutwardBalls);
        RunTest(failures, "Branch is omitted when stem is already clear", ValidateBranchIsOmittedWhenStemIsAlreadyClear);
        RunTest(failures, "Support is skipped when branch cannot clear model", ValidateSupportIsSkippedWhenBranchCannotClearModel);
        RunTest(failures, "Angled head omits branch inside model clearance", ValidateAngledHeadOmitsBranchInsideModelClearance);
        RunTest(failures, "Branch is used when vertical stem intersects model", ValidateBranchIsUsedWhenVerticalStemIntersectsModel);
        RunTest(failures, "Individual support dimensions derive branch from stem top", ValidateIndividualSupportDimensionsDeriveBranchFromStemTop);
        RunTest(failures, "Clustered support dimensions use explicit branch diameter", ValidateClusteredSupportDimensionsUseExplicitBranchDiameter);
        RunTest(failures, "Cluster branch diameter affects support mesh", ValidateClusterBranchDiameterAffectsSupportMesh);
        RunTest(failures, "Cluster head branch joint uses branch diameter", ValidateClusterHeadBranchJointUsesBranchDiameter);
        RunTest(failures, "Individual branched supports remain cluster eligible", ValidateIndividualBranchedSupportsRemainClusterEligible);
        RunTest(failures, "Cluster modifier redirects nearby supports", ValidateClusterModifierRedirectsNearbySupports);
        RunTest(failures, "Direct Edit preserves contact and rebuilds the branch", ValidateDirectEditPreservesContactAndRebuildsBranch);
        RunTest(failures, "Direct Edit source restoration reverses cumulative edits", ValidateDirectEditSourceRestorationReversesCumulativeEdits);
        RunTest(failures, "Direct Edit multi-selection preserves relative stem positions", ValidateDirectEditMultiSelectionPreservesRelativePositions);
        RunTest(failures, "Selection cluster modifier ignores unselected supports", ValidateSelectionClusterModifierIgnoresUnselectedSupports);
        RunTest(failures, "Later selection cluster preserves existing clusters", ValidateLaterSelectionClusterPreservesExistingClusters);
        RunTest(failures, "Cumulative cluster modifier keeps Apply batches separate", ValidateCumulativeClusterModifierKeepsApplyBatchesSeparate);
        RunTest(failures, "Selected individual joins selected cluster", ValidateSelectedIndividualJoinsSelectedCluster);
        RunTest(failures, "Selected individual joins nearest feasible selected cluster", ValidateSelectedIndividualJoinsNearestFeasibleCluster);
        RunTest(failures, "Remaining selected individuals cluster after merge attempts", ValidateRemainingSelectedIndividualsClusterAfterMergeAttempts);
        RunTest(failures, "Unselected clustered supports remain unchanged", ValidateUnselectedClusteredSupportsRemainUnchanged);
        RunTest(failures, "Brace modifier adds printable brace members", ValidateBraceModifierAddsPrintableBraceMembers);
        RunTest(failures, "Brace modifier evaluates both endpoint directions", ValidateBraceModifierEvaluatesBothDirections);
        RunTest(failures, "Brace modifier starts at the top of the base", ValidateBraceModifierStartsAtBaseTop);
        RunTest(failures, "Brace modifier adds one rising return member", ValidateBraceModifierAddsRisingReturnMember);
        RunTest(failures, "Brace modifier defaults maximum angle to 70", ValidateBraceModifierDefaultsMaximumAngleTo70);
        RunTest(failures, "Brace modifier keeps only adjacent ring neighbors", ValidateBraceModifierKeepsAdjacentRingNeighbors);
        RunTest(failures, "Brace modifier does not skip collinear supports", ValidateBraceModifierDoesNotSkipCollinearSupports);
        RunTest(failures, "Brace modifier uses relative neighbors on irregular layouts", ValidateBraceModifierUsesRelativeNeighborhoodOnIrregularLayout);
        RunTest(failures, "Brace modifier preserves equal-distance neighbors", ValidateBraceModifierPreservesEqualDistanceNeighbors);
        RunTest(failures, "Brace modifier limits physical connections", ValidateBraceModifierLimitsConnections);
        RunTest(failures, "Brace pair exclusions preserve mixed-selection bracing", ValidateBracePairExclusionsPreserveMixedSelectionBracing);
        RunTest(failures, "Brace exclusion batches preserve mixed-selection bracing", ValidateBraceExclusionBatchesPreserveMixedSelectionBracing);
        RunTest(failures, "Modifier pipeline captures bracing diagnostics in one pass", ValidateModifierPipelineCapturesBracingDiagnostics);
        RunTest(failures, "Support output replacement retains unchanged entities", ValidateSupportOutputReplacementRetainsUnchangedEntities);
        RunTest(failures, "Equivalent regenerated reinforcement retains existing entities", ValidateEquivalentRegeneratedReinforcementRetainsExistingEntities);
        RunTest(failures, "Reinforcement excludes clustered supports", ValidateReinforcementExcludesClusteredSupports);
        RunTest(failures, "Buttress reapply preserves unselected targets", ValidateButtressReapplyPreservesUnselectedTargets);
        RunTest(failures, "Modifier session removal removes every internal action", ValidateModifierSessionRemovalRemovesEveryAction);
        RunTest(failures, "Cluster before Brace excludes clustered outputs", ValidateClusterBeforeBraceExcludesClusteredOutputs);
        RunTest(failures, "Cluster reconciliation removes reinforcement targets", ValidateClusterReconciliationRemovesReinforcementTargets);
        RunTest(failures, "Cluster reinforcement cleanup undo restores prior state", ValidateClusterReinforcementCleanupUndo);
        RunTest(failures, "Buttress modifier targets tall supports only", ValidateButtressModifierTargetsTallSupportsOnly);
        RunTest(failures, "Buttress modifier creates paired equilateral supports", ValidateButtressModifierCreatesPairedEquilateralSupports);
        RunTest(failures, "Buttress bracing keeps valid primary when return does not fit", ValidateButtressBracingKeepsPrimaryWhenReturnDoesNotFit);
        RunTest(failures, "Support placement rejects crossing angled head", ValidateSupportPlacementRejectsCrossingAngledHead);
        RunTest(failures, "Vertical projection returns triangle normal", ValidateVerticalProjectionReturnsTriangleNormal);
        RunTest(failures, "Vertical projection handles vertical side faces", ValidateVerticalProjectionHandlesVerticalSideFaces);
        RunTest(failures, "Support projection falls back to nearby vertical face", ValidateSupportProjectionFallsBackToNearbyVerticalFace);
        RunTest(failures, "Support projection fallback handles neighboring vertical face points", ValidateSupportProjectionFallbackHandlesNeighboringVerticalFacePoints);
        RunTest(failures, "Support projection fallback rejects distant vertical face", ValidateSupportProjectionFallbackRejectsDistantVerticalFace);
        RunTest(failures, "Vertical support projection chooses first exterior hit", ValidateVerticalSupportProjectionChoosesFirstExteriorHit);
        RunTest(failures, "Transform regeneration uses supportable projection", ValidateTransformRegenerationUsesSupportableProjection);
        RunTest(failures, "Rotation transform preserves its pivot and scale", ValidateRotationTransformPreservesPivotAndScale);
        RunTest(failures, "Zero rotation restores exact session transform", ValidateZeroRotationRestoresExactSessionTransform);
        RunTest(failures, "Reset rotation restores imported orientation", ValidateResetRotationRestoresImportedOrientation);
        RunTest(failures, "X rotation follows the world X axis", ValidateRotationTransformUsesWorldXAxis);
        RunTest(failures, "X rotation follows the model local X axis", ValidateRotationTransformUsesLocalXAxis);
        RunTest(failures, "Combined local rotation follows the changing model axes", ValidateCombinedLocalRotationUsesChangingModelAxes);
        RunTest(failures, "Line support pattern includes clicked endpoints", ValidateLineSupportPatternIncludesClickedEndpoints);
        RunTest(failures, "Line support pattern avoids duplicate shared vertices", ValidateLineSupportPatternAvoidsDuplicateSharedVertices);
        RunTest(failures, "Line support pattern respects spacing maximum", ValidateLineSupportPatternRespectsSpacingMaximum);
        RunTest(failures, "Line support pattern handles degenerate segments", ValidateLineSupportPatternHandlesDegenerateSegments);
        RunTest(failures, "Line support pattern can skip bend supports", ValidateLineSupportPatternCanSkipBendSupports);
        RunTest(failures, "Contour support stays in seeded connected patch", ValidateContourSupportStaysInSeededConnectedPatch);
        RunTest(failures, "Contour support traverses duplicated STL-style panel vertices", ValidateContourSupportTraversesDuplicatedPanelVertices);
        RunTest(failures, "Contour support selects nearby longer path when seed slice is short", ValidateContourSupportSelectsNearbyLongerPathWhenSeedSliceIsShort);
        RunTest(failures, "Contour support bridges tiny slice endpoint gaps", ValidateContourSupportBridgesTinySliceEndpointGaps);
        RunTest(failures, "Contour support threshold blocks sharp face transitions", ValidateContourSupportThresholdBlocksSharpFaceTransitions);
        RunTest(failures, "Contour support threshold works with duplicated STL-style vertices", ValidateContourSupportThresholdWorksWithDuplicatedVertices);
        RunTest(failures, "Contour support closed loop spacing is even", ValidateContourSupportClosedLoopSpacingIsEven);
        RunTest(failures, "Contour support closed loop start offset rotates supports", ValidateContourSupportClosedLoopStartOffsetRotatesSupports);
        RunTest(failures, "Contour support places supports on noisy near-vertical faces", ValidateContourSupportPlacesSupportsOnNoisyNearVerticalFaces);
        RunTest(failures, "Contour support open offsets respect spacing", ValidateContourSupportOpenOffsetsRespectSpacing);
        RunTest(failures, "Contour support Z edits choose nearest patch contour", ValidateContourSupportZEditsChooseNearestPatchContour);
        RunTest(failures, "Area support thin ring uses centreline fallback", ValidateAreaSupportThinRingUsesCentrelineFallback);
        RunTest(failures, "Area support thin fallback can be disabled", ValidateAreaSupportThinFallbackCanBeDisabled);
        RunTest(failures, "Area support ultra-thin regions respect minimum thickness", ValidateAreaSupportUltraThinRegionsRespectMinimumThickness);
        RunTest(failures, "Area support offsets internal hole boundaries", ValidateAreaSupportOffsetsInternalHoleBoundaries);
        RunTest(failures, "Area support splits coarse offset boundary preview paths", ValidateAreaSupportSplitsCoarseOffsetBoundaryPreviewPaths);
        RunTest(failures, "Area support boundary offset fill creates requested rings", ValidateAreaSupportBoundaryOffsetFillCreatesRequestedRings);
        RunTest(failures, "Area support boundary offset fill closes support spacing seam", ValidateAreaSupportBoundaryOffsetFillClosesSpacingSeam);
        RunTest(failures, "Area support boundary offset fill draws holes without supporting them", ValidateAreaSupportBoundaryOffsetFillDrawsHolesWithoutSupportingThem);
        RunTest(failures, "Area support boundary offset fill supports concave corners on each ring", ValidateAreaSupportBoundaryOffsetFillSupportsConcaveCornersOnEachRing);
        RunTest(failures, "Area support boundary offset fill handles collapsed rings", ValidateAreaSupportBoundaryOffsetFillHandlesCollapsedRings);
        RunTest(failures, "Line support settings survive save and load", ValidateLineSupportSettingsSurviveSaveAndLoad);
        RunTest(failures, "Contour support settings survive save and load", ValidateContourSupportSettingsSurviveSaveAndLoad);
        RunTest(failures, "Area support settings survive save and load", ValidateAreaSupportSettingsSurviveSaveAndLoad);
        RunTest(failures, "Support style and cluster branch diameter survive save and load", ValidateSupportStyleAndClusterBranchDiameterSurviveSaveAndLoad);
        RunTest(failures, "Buttress style and bracing settings survive save and load", ValidateButtressStyleAndBracingSettingsSurviveSaveAndLoad);
        RunTest(failures, "Cluster modifier target batches survive save and load", ValidateClusterModifierTargetBatchesSurviveSaveAndLoad);
        RunTest(failures, "Direct Edit geometry survives save and load", ValidateDirectEditSettingsSurviveSaveAndLoad);
        RunTest(failures, "Brace pair exclusions survive save and load", ValidateBracePairExclusionsSurviveSaveAndLoad);
        RunTest(failures, "Invalid cluster modifier revision is discarded on load", ValidateInvalidClusterModifierRevisionIsDiscardedOnLoad);
        RunTest(failures, "Invalid cluster modifier target is discarded on load", ValidateInvalidClusterModifierTargetIsDiscardedOnLoad);
        RunTest(failures, "Gph serializer rejects invalid format", ValidateGphSerializerRejectsInvalidFormat);
        RunTest(failures, "Horizontal face angle classifier includes downward horizontal faces", ValidateHorizontalFaceAngleClassifierIncludesDownwardHorizontalFace);
        RunTest(failures, "Horizontal face angle classifier excludes upward horizontal faces", ValidateHorizontalFaceAngleClassifierExcludesUpwardHorizontalFace);
        RunTest(failures, "Horizontal face angle classifier excludes vertical faces", ValidateHorizontalFaceAngleClassifierExcludesVerticalFace);
        RunTest(failures, "Horizontal face angle classifier uses mesh transforms", ValidateHorizontalFaceAngleClassifierUsesMeshTransform);

        SupportLayerDeletionSmokeTests.Run(failures);
        RaftSmokeTests.Run(failures);

        if (failures.Count > 0)
        {
            Console.Error.WriteLine("Support mesh smoke tests failed:");

            for (int i = 0; i < failures.Count; i++)
            {
                Console.Error.WriteLine(failures[i]);
            }

            return 1;
        }

        BracingPerformanceBenchmarks.RunIfRequested();
        Console.WriteLine("Support mesh smoke tests passed.");
        return 0;
    }

    /// <summary>
    /// Runs one named test and records any thrown validation error.
    /// </summary>
    private static void RunTest(List<string> failures, string name, Action test)
    {
        try
        {
            test();
        }
        catch (Exception ex)
        {
            failures.Add($"{name}: {ex.Message}");
        }
    }

    /// <summary>
    /// Validates a full-height default support mesh.
    /// </summary>
    private static void ValidateNormalSupportMesh()
    {
        SupportEntity support = CreateSupport(new Vector3(0.0f, 0.0f, 0.0f), new Vector3(0.0f, 0.0f, 12.0f), SupportDefaults.CreateProfile());
        SupportMeshData meshData = SupportMeshBuilder.Build(support, 16);

        ValidateClosedMesh(meshData);
    }

    /// <summary>
    /// Validates a support shorter than the default base height.
    /// </summary>
    private static void ValidateShortSupportMesh()
    {
        SupportEntity support = CreateSupport(new Vector3(4.0f, -2.0f, 0.0f), new Vector3(4.0f, -2.0f, 0.75f), SupportDefaults.CreateProfile());
        SupportMeshData meshData = SupportMeshBuilder.Build(support, 16);

        ValidateClosedMesh(meshData);
    }

    /// <summary>
    /// Validates that the final radial side closes against the first side exactly enough for topology checks.
    /// </summary>
    private static void ValidateAngledSeamSupportMesh()
    {
        SupportEntity support = CreateSupport(new Vector3(-1.0f, 2.0f, 0.0f), new Vector3(1.25f, 3.75f, 9.0f), SupportDefaults.CreateProfile());
        SupportMeshData meshData = SupportMeshBuilder.Build(support, 16);

        ValidateClosedMesh(meshData);
    }

    /// <summary>
    /// Validates that a zero-degree preset disables angled head placement.
    /// </summary>
    private static void ValidateDefaultSupportHeadDirectionStaysVertical()
    {
        SupportProfile profile = SupportDefaults.CreateProfile();
        Vector3 headDirection = SupportHeadDirectionCalculator.CreateHeadDirectionFromSurfaceNormal(Vector3.UnitX, profile);

        ValidateVectorNear(Vector3.UnitZ, headDirection, 0.0001f, "Expected the default head direction to stay vertical.");
    }

    /// <summary>
    /// Validates that steep face normals are capped to the selected preset angle.
    /// </summary>
    private static void ValidateSupportHeadDirectionClampsToProfileAngle()
    {
        SupportProfile profile = CreateAngledProfile(30.0f);
        Vector3 headDirection = SupportHeadDirectionCalculator.CreateHeadDirectionFromSurfaceNormal(Vector3.UnitX, profile);
        float expectedZ = MathF.Cos(30.0f * (MathF.PI / 180.0f));

        if (MathF.Abs(headDirection.Z - expectedZ) > 0.0001f)
        {
            throw new InvalidOperationException("Expected the head direction to be capped to 30 degrees from vertical.");
        }
    }

    /// <summary>
    /// Validates that side-face supports approach the contact from outside the model instead of through the interior.
    /// </summary>
    private static void ValidateSideFaceSupportHeadApproachesFromOutside()
    {
        SupportProfile profile = CreateAngledProfile(45.0f);
        Vector3 headDirection;

        if (!SupportHeadDirectionCalculator.TryCreateHeadDirectionFromSurfaceNormal(Vector3.UnitX, profile, out headDirection))
        {
            throw new InvalidOperationException("Expected a vertical side face to be supportable with an angled head.");
        }

        if (headDirection.X >= -0.0001f || headDirection.Z <= 0.0f)
        {
            throw new InvalidOperationException("Expected the head direction to move from the outside stem toward the side face.");
        }
    }

    /// <summary>
    /// Validates that STL tessellation noise on a vertical wall does not make the face look like an upward contact.
    /// </summary>
    private static void ValidateNearVerticalPositiveZSideFaceRemainsSupportable()
    {
        SupportProfile profile = CreateAngledProfile(45.0f);
        Vector3 noisySideNormal = Vector3.Normalize(new Vector3(1.0f, 0.0f, 0.0004f));
        Vector3 headDirection;

        if (!SupportHeadDirectionCalculator.TryCreateHeadDirectionFromSurfaceNormal(noisySideNormal, profile, out headDirection))
        {
            throw new InvalidOperationException("Expected a numerically noisy near-vertical face to remain supportable.");
        }

        if (headDirection.X >= -0.0001f || headDirection.Z <= 0.0f)
        {
            throw new InvalidOperationException("Expected the noisy side face to keep the same outside approach direction.");
        }

        float expectedZ = MathF.Cos(45.0f * (MathF.PI / 180.0f));

        if (MathF.Abs(headDirection.Z - expectedZ) > 0.0001f)
        {
            throw new InvalidOperationException("Expected the noisy side face head direction to be clamped to the profile angle.");
        }
    }

    /// <summary>
    /// Validates that upward-facing model contacts are skipped because supports cannot approach them from the build plate.
    /// </summary>
    private static void ValidateUpwardSupportContactIsRejected()
    {
        SupportProfile profile = CreateAngledProfile(45.0f);
        Vector3 headDirection;

        if (SupportHeadDirectionCalculator.TryCreateHeadDirectionFromSurfaceNormal(Vector3.UnitZ, profile, out headDirection))
        {
            throw new InvalidOperationException("Expected upward-facing contacts to be rejected for build-plate supports.");
        }
    }

    /// <summary>
    /// Validates that downward overhang contacts still accept ordinary build-plate supports.
    /// </summary>
    private static void ValidateDownwardOverhangSupportPlacementRemainsValid()
    {
        SupportProfile profile = CreateAngledProfile(45.0f);
        MeshEntity mesh = CreateSingleTriangleMesh(
            new Vector3(0.0f, 0.0f, 10.0f),
            new Vector3(0.0f, 1.0f, 10.0f),
            new Vector3(1.0f, 0.0f, 10.0f),
            Transform3DData.Identity);
        SupportPlacementPlan placementPlan;

        if (!SupportPlacementPlanner.TryCreatePlacement(mesh, new Vector3(0.25f, 0.25f, 10.0f), -Vector3.UnitZ, profile, out placementPlan))
        {
            throw new InvalidOperationException("Expected a downward overhang to accept a support placement.");
        }

        ValidateVectorNear(Vector3.UnitZ, placementPlan.HeadDirection, 0.0001f, "Expected the overhang head direction to be vertical.");
    }

    /// <summary>
    /// Validates a support with a shifted vertical stem and angled closed head.
    /// </summary>
    private static void ValidateAngledHeadSupportMesh()
    {
        SupportProfile profile = CreateAngledProfile(45.0f);
        Vector3 tipPosition = new Vector3(4.0f, 1.0f, 12.0f);
        Vector3 headDirection = SupportHeadDirectionCalculator.CreateHeadDirectionFromSurfaceNormal(Vector3.UnitX, profile);
        Vector3 basePosition = SupportHeadDirectionCalculator.CreateShiftedBasePosition(tipPosition, headDirection, profile);
        SupportEntity support = new SupportEntity(Guid.NewGuid(), tipPosition, basePosition, headDirection, profile);
        SupportMeshData meshData = SupportMeshBuilder.Build(support, 16);

        ValidateClosedMesh(meshData);
    }

    /// <summary>
    /// Validates that the ball joint is wound with outward-facing normals for back-face culled rendering.
    /// </summary>
    private static void ValidateJointBallNormalsPointOutward()
    {
        SupportProfile profile = CreateAngledProfile(45.0f);
        Vector3 tipPosition = new Vector3(4.0f, 1.0f, 12.0f);
        Vector3 headDirection = SupportHeadDirectionCalculator.CreateHeadDirectionFromSurfaceNormal(Vector3.UnitX, profile);
        Vector3 basePosition = SupportHeadDirectionCalculator.CreateShiftedBasePosition(tipPosition, headDirection, profile);
        SupportEntity support = new SupportEntity(Guid.NewGuid(), tipPosition, basePosition, headDirection, profile);
        SupportMeshData meshData = SupportMeshBuilder.Build(support, 16);
        Vector3 jointCenter = tipPosition - (headDirection * profile.HeadHeight);
        float ballRadius = profile.StemTopDiameter * 0.5f;
        int checkedTriangleCount = 0;

        for (int i = 0; i < meshData.TriangleIndices.Count; i += 3)
        {
            int indexA = meshData.TriangleIndices[i];
            int indexB = meshData.TriangleIndices[i + 1];
            int indexC = meshData.TriangleIndices[i + 2];
            Vector3 a = GetPosition(meshData, indexA);
            Vector3 b = GetPosition(meshData, indexB);
            Vector3 c = GetPosition(meshData, indexC);

            if (!IsNearSphereSurface(a, jointCenter, ballRadius)
                || !IsNearSphereSurface(b, jointCenter, ballRadius)
                || !IsNearSphereSurface(c, jointCenter, ballRadius))
            {
                continue;
            }

            Vector3 centroidDirection = ((a + b + c) / 3.0f) - jointCenter;
            Vector3 normal = meshData.Normals[indexA];

            if (Vector3.Dot(normal, centroidDirection) <= 0.0f)
            {
                throw new InvalidOperationException("Expected every joint ball triangle normal to point away from the ball center.");
            }

            checkedTriangleCount++;
        }

        if (checkedTriangleCount == 0)
        {
            throw new InvalidOperationException("Expected to find joint ball triangles in the generated support mesh.");
        }
    }

    /// <summary>
    /// Validates that the shipped default profile keeps branch generation disabled.
    /// </summary>
    private static void ValidateDefaultBranchSettingCreatesNoBranch()
    {
        SupportProfile profile = SupportDefaults.CreateProfile();
        MeshEntity mesh = CreateSingleTriangleMesh(
            new Vector3(20.0f, 20.0f, 5.0f),
            new Vector3(21.0f, 20.0f, 5.0f),
            new Vector3(20.0f, 21.0f, 5.0f),
            Transform3DData.Identity);
        Vector3 tipPosition = new Vector3(0.0f, 0.0f, 10.0f);
        SupportBranchPlan branchPlan;

        if (!SupportBranchPlanner.TryCreateBranchPlan(mesh, tipPosition, Vector3.UnitZ, profile, out branchPlan))
        {
            throw new InvalidOperationException("Expected default branch settings to preserve support creation.");
        }

        if (branchPlan.BranchLength != 0.0f)
        {
            throw new InvalidOperationException("Expected the default profile to create no branch.");
        }
    }

    /// <summary>
    /// Validates that branch dimensions reject negative input before geometry generation.
    /// </summary>
    private static void ValidateBranchProfileFieldsValidateNonNegativeValues()
    {
        ValidateProfileThrowsForBranchValues(-0.1f, SupportDefaults.DefaultModelClearance, "maximum branch length");
        ValidateProfileThrowsForBranchValues(SupportDefaults.DefaultMaximumBranchLength, -0.1f, "model clearance");
        ValidateProfileThrowsForBranchAngle(14.9f);
        ValidateProfileThrowsForBranchAngle(45.1f);
    }

    /// <summary>
    /// Validates that the branch planner uses the branch angle instead of reusing the head angle.
    /// </summary>
    private static void ValidateBranchAngleIsIndependentFromHeadAngle()
    {
        SupportProfile profile = CreateBranchProfile(45.0f, 4.0f, 0.5f, 15.0f);
        Vector3 headDirection = SupportHeadDirectionCalculator.CreateHeadDirectionFromSurfaceNormal(Vector3.UnitX, profile);
        Vector3 branchDirection = SupportBranchPlanner.CreateBranchDirection(headDirection, profile);
        float angleFromVerticalDegrees = MathF.Acos(Vector3.Dot(Vector3.Normalize(branchDirection), Vector3.UnitZ)) * (180.0f / MathF.PI);

        if (MathF.Abs(angleFromVerticalDegrees - 15.0f) > 0.0001f)
        {
            throw new InvalidOperationException("Expected the branch direction to use the branch angle setting.");
        }
    }

    /// <summary>
    /// Validates that an explicit branch support remains manifold and has outward-facing joint balls.
    /// </summary>
    private static void ValidateBranchSupportMeshIsClosedWithOutwardBalls()
    {
        SupportProfile profile = CreateBranchProfile(45.0f, 4.0f, 0.5f);
        Vector3 tipPosition = new Vector3(4.0f, 1.0f, 12.0f);
        Vector3 headDirection = SupportHeadDirectionCalculator.CreateHeadDirectionFromSurfaceNormal(Vector3.UnitX, profile);
        Vector3 headJointPosition = tipPosition - (headDirection * profile.HeadHeight);
        Vector3 branchDirection = SupportBranchPlanner.CreateBranchDirection(headDirection, profile);
        float branchLength = 2.0f;
        Vector3 stemJointPosition = headJointPosition - (branchDirection * branchLength);
        Vector3 basePosition = new Vector3(stemJointPosition.X, stemJointPosition.Y, 0.0f);
        SupportEntity support = new SupportEntity(Guid.NewGuid(), tipPosition, basePosition, headDirection, branchLength, branchDirection, profile);
        SupportMeshData meshData = SupportMeshBuilder.Build(support, 16);

        ValidateClosedMesh(meshData);
        ValidateBallNormalsPointOutward(meshData, stemJointPosition, profile.StemTopDiameter * 0.5f);
        ValidateBallNormalsPointOutward(meshData, headJointPosition, profile.StemTopDiameter * 0.5f);
    }

    /// <summary>
    /// Validates that no branch is emitted when the vertical stem already satisfies clearance.
    /// </summary>
    private static void ValidateBranchIsOmittedWhenStemIsAlreadyClear()
    {
        SupportProfile profile = CreateBranchProfile(45.0f, 6.0f, 0.5f);
        MeshEntity mesh = CreateSingleTriangleMesh(
            new Vector3(20.0f, 20.0f, 5.0f),
            new Vector3(21.0f, 20.0f, 5.0f),
            new Vector3(20.0f, 21.0f, 5.0f),
            Transform3DData.Identity);
        Vector3 tipPosition = new Vector3(0.0f, 0.0f, 10.0f);
        Vector3 headDirection = SupportHeadDirectionCalculator.CreateHeadDirectionFromSurfaceNormal(Vector3.UnitX, profile);
        SupportBranchPlan branchPlan;

        if (!SupportBranchPlanner.TryCreateBranchPlan(mesh, tipPosition, headDirection, profile, out branchPlan))
        {
            throw new InvalidOperationException("Expected a clear stem path to keep the support.");
        }

        if (branchPlan.BranchLength != 0.0f)
        {
            throw new InvalidOperationException("Expected the planner to omit the branch when length zero is clear.");
        }
    }

    /// <summary>
    /// Validates that supports are skipped when every candidate vertical stem intersects clearance.
    /// </summary>
    private static void ValidateSupportIsSkippedWhenBranchCannotClearModel()
    {
        SupportProfile profile = CreateBranchProfile(45.0f, 4.0f, 0.5f);
        MeshEntity mesh = CreateBlockingWallMesh(-10.0f, 10.0f, Transform3DData.Identity);
        Vector3 tipPosition = new Vector3(0.0f, 0.0f, 10.0f);
        Vector3 headDirection = SupportHeadDirectionCalculator.CreateHeadDirectionFromSurfaceNormal(Vector3.UnitX, profile);
        SupportBranchPlan branchPlan;

        if (SupportBranchPlanner.TryCreateBranchPlan(mesh, tipPosition, headDirection, profile, out branchPlan))
        {
            throw new InvalidOperationException("Expected the planner to skip a support that cannot clear the model.");
        }
    }

    /// <summary>
    /// Validates that a clear angled head does not need a branch just because it sits inside model clearance.
    /// </summary>
    private static void ValidateAngledHeadOmitsBranchInsideModelClearance()
    {
        SupportProfile profile = CreateBranchProfile(45.0f, 6.0f, 4.0f);
        MeshEntity mesh = CreateSideContactPanelMesh();
        SupportPlacementPlan placementPlan;

        if (!SupportPlacementPlanner.TryCreatePlacement(mesh, new Vector3(0.0f, 5.0f, 10.0f), Vector3.UnitX, profile, out placementPlan))
        {
            throw new InvalidOperationException("Expected a clear angled head support to remain valid.");
        }

        if (placementPlan.BranchLength != 0.0f)
        {
            throw new InvalidOperationException("Expected the planner to omit the branch when only model clearance is violated.");
        }
    }

    /// <summary>
    /// Validates that the planner still creates a branch when the direct vertical stem physically intersects the model.
    /// </summary>
    private static void ValidateBranchIsUsedWhenVerticalStemIntersectsModel()
    {
        SupportProfile profile = CreateBranchProfile(45.0f, 6.0f, 4.0f);
        Vector3 tipPosition = new Vector3(0.0f, 5.0f, 10.0f);
        Vector3 headDirection = SupportHeadDirectionCalculator.CreateHeadDirectionFromSurfaceNormal(Vector3.UnitX, profile);
        Vector3 headJointPosition = tipPosition - (headDirection * profile.HeadHeight);
        MeshEntity mesh = CreateStemBlockingPanelMesh(headJointPosition.X);
        SupportPlacementPlan placementPlan;

        if (!SupportPlacementPlanner.TryCreatePlacement(mesh, tipPosition, Vector3.UnitX, profile, out placementPlan))
        {
            throw new InvalidOperationException("Expected the planner to find a branch that moves the vertical stem clear.");
        }

        if (placementPlan.BranchLength <= 0.0f)
        {
            throw new InvalidOperationException("Expected the planner to use a branch when the vertical stem intersects the model.");
        }
    }

    /// <summary>
    /// Validates that individual support dimensions derive branch and head-bottom diameters from stem top.
    /// </summary>
    private static void ValidateIndividualSupportDimensionsDeriveBranchFromStemTop()
    {
        SupportProfile profile = new SupportProfile(
            0.25f,
            0.25f,
            0.80f,
            0.55f,
            4.0f,
            0.5f,
            30.0f,
            1.0f,
            0.2f,
            0.20f,
            45.0f);
        SupportPartDimensions dimensions = SupportDimensionResolver.Resolve(profile, SupportStyle.Individual);

        if (MathF.Abs(dimensions.BranchDiameter - profile.StemTopDiameter) > 0.0001f
            || MathF.Abs(dimensions.HeadBottomDiameter - profile.StemTopDiameter) > 0.0001f)
        {
            throw new InvalidOperationException("Expected individual branch and head-bottom diameters to come from stem top diameter.");
        }
    }

    /// <summary>
    /// Validates that clustered support dimensions use an explicit branch diameter without changing stem diameters.
    /// </summary>
    private static void ValidateClusteredSupportDimensionsUseExplicitBranchDiameter()
    {
        SupportProfile profile = new SupportProfile(
            0.25f,
            0.25f,
            0.80f,
            0.55f,
            4.0f,
            0.5f,
            30.0f,
            1.0f,
            0.2f,
            0.20f,
            45.0f);
        SupportPartDimensions dimensions = SupportDimensionResolver.Resolve(profile, new ClusteredSupportStyle(1.35f));

        if (MathF.Abs(dimensions.StemTopDiameter - profile.StemTopDiameter) > 0.0001f
            || MathF.Abs(dimensions.BranchDiameter - 1.35f) > 0.0001f
            || MathF.Abs(dimensions.HeadBottomDiameter - 1.35f) > 0.0001f)
        {
            throw new InvalidOperationException("Expected clustered dimensions to keep stem top and use explicit branch diameter for branch and head bottom.");
        }
    }

    /// <summary>
    /// Validates that support mesh generation uses clustered branch diameter independently from stem top diameter.
    /// </summary>
    private static void ValidateClusterBranchDiameterAffectsSupportMesh()
    {
        SupportProfile profile = new SupportProfile(
            0.10f,
            0.10f,
            0.10f,
            0.20f,
            4.0f,
            0.5f,
            30.0f,
            1.0f,
            0.2f,
            0.10f,
            45.0f);
        SupportEntity support = new SupportEntity(
            Guid.NewGuid(),
            new Vector3(5.0f, 0.0f, 5.0f),
            new Vector3(0.0f, 0.0f, 0.0f),
            Vector3.UnitZ,
            4.0f,
            Vector3.UnitX,
            profile,
            new ClusteredSupportStyle(2.0f));
        SupportMeshData meshData = SupportMeshBuilder.Build(support, 16);
        float maximumY = 0.0f;

        for (int i = 0; i < meshData.Positions.Count; i++)
        {
            maximumY = MathF.Max(maximumY, MathF.Abs(meshData.Positions[i].Y));
        }

        if (maximumY < 0.90f)
        {
            throw new InvalidOperationException("Expected clustered branch diameter to visibly widen the generated support mesh.");
        }
    }

    /// <summary>
    /// Validates that the clustered head-to-branch ball uses the branch diameter, not the central stem top diameter.
    /// </summary>
    private static void ValidateClusterHeadBranchJointUsesBranchDiameter()
    {
        SupportProfile profile = new SupportProfile(
            0.10f,
            0.10f,
            0.10f,
            1.60f,
            4.0f,
            0.5f,
            30.0f,
            1.0f,
            0.2f,
            0.10f,
            45.0f);
        Vector3 tipPosition = new Vector3(5.0f, 0.0f, 5.0f);
        Vector3 headDirection = Vector3.UnitZ;
        SupportEntity support = new SupportEntity(
            Guid.NewGuid(),
            tipPosition,
            new Vector3(0.0f, 0.0f, 0.0f),
            headDirection,
            4.0f,
            Vector3.UnitX,
            profile,
            new ClusteredSupportStyle(0.40f));
        SupportMeshData meshData = SupportMeshBuilder.Build(support, 16);
        Vector3 headJointPosition = tipPosition - (headDirection * profile.HeadHeight);
        Vector3 expectedBallTop = headJointPosition + (Vector3.UnitZ * 0.20f);
        Vector3 stemSizedBallTop = headJointPosition + (Vector3.UnitZ * 0.80f);
        bool foundExpectedBallTop = false;
        bool foundStemSizedBallTop = false;

        for (int i = 0; i < meshData.Positions.Count; i++)
        {
            if (Vector3.Distance(meshData.Positions[i], expectedBallTop) <= 0.0001f)
            {
                foundExpectedBallTop = true;
            }

            if (Vector3.Distance(meshData.Positions[i], stemSizedBallTop) <= 0.0001f)
            {
                foundStemSizedBallTop = true;
            }
        }

        if (!foundExpectedBallTop)
        {
            throw new InvalidOperationException("Expected clustered head branch joint ball to use the branch radius.");
        }

        if (foundStemSizedBallTop)
        {
            throw new InvalidOperationException("Expected clustered head branch joint ball not to use the central stem top radius.");
        }
    }

    /// <summary>
    /// Validates that individual supports with ordinary branches can still become clustered supports.
    /// </summary>
    private static void ValidateIndividualBranchedSupportsRemainClusterEligible()
    {
        SupportProfile profile = CreateBranchProfile(45.0f, 4.0f, 0.5f);
        List<SupportEntity> supports = new List<SupportEntity>
        {
            new SupportEntity(Guid.NewGuid(), new Vector3(0.0f, 0.0f, 12.0f), new Vector3(0.0f, 0.0f, 0.0f), Vector3.UnitZ, 1.0f, Vector3.UnitX, profile),
            new SupportEntity(Guid.NewGuid(), new Vector3(1.0f, 0.0f, 12.0f), new Vector3(1.0f, 0.0f, 0.0f), Vector3.UnitZ, 1.0f, Vector3.UnitX, profile)
        };
        SupportClusterModifierSettings settings = new SupportClusterModifierSettings(
            3.0f,
            2,
            4,
            45.0f,
            SupportClusterStemSizingMode.Automatic,
            SupportDefaults.DefaultStemBottomDiameter,
            SupportDefaults.DefaultStemTopDiameter,
            0.42f);
        SupportModifierDefinition modifier = SupportModifierDefinition.CreateNew(
            SupportModifierKind.Cluster,
            0,
            settings,
            new List<Guid> { supports[0].Id, supports[1].Id },
            0);
        SupportClusterEvaluationResult result = SupportClusterPlanner.Evaluate(supports, modifier);

        if (result.ClusteredSupportCount != 2)
        {
            throw new InvalidOperationException("Expected individual branched supports to remain eligible for clustering.");
        }
    }

    /// <summary>
    /// Validates that an all-target Cluster modifier redirects nearby supports onto shared stem axes.
    /// </summary>
    private static void ValidateClusterModifierRedirectsNearbySupports()
    {
        SupportProfile profile = SupportDefaults.CreateProfile();
        List<SupportEntity> supports = new List<SupportEntity>
        {
            CreateSupport(new Vector3(0.0f, 0.0f, 0.0f), new Vector3(0.0f, 0.0f, 12.0f), profile),
            CreateSupport(new Vector3(2.0f, 0.0f, 0.0f), new Vector3(2.0f, 0.0f, 12.0f), profile),
            CreateSupport(new Vector3(10.0f, 0.0f, 0.0f), new Vector3(10.0f, 0.0f, 12.0f), profile)
        };
        SupportClusterModifierSettings settings = new SupportClusterModifierSettings(
            3.0f,
            2,
            4,
            45.0f,
            SupportClusterStemSizingMode.Automatic,
            SupportDefaults.DefaultStemBottomDiameter,
            SupportDefaults.DefaultStemTopDiameter,
            0.42f);
        SupportModifierDefinition modifier = SupportModifierDefinition.CreateNew(
            SupportModifierKind.Cluster,
            0,
            settings,
            new List<Guid> { supports[0].Id, supports[1].Id, supports[2].Id },
            0);
        SupportClusterEvaluationResult result = SupportClusterPlanner.Evaluate(supports, modifier);

        if (result.ClusterCount != 1 || result.ClusteredSupportCount != 2)
        {
            throw new InvalidOperationException("Expected one two-member cluster.");
        }

        if (result.SupportEntities[0].BranchLength <= 0.0f || result.SupportEntities[1].BranchLength <= 0.0f)
        {
            throw new InvalidOperationException("Expected nearby supports to become branched cluster members.");
        }

        if (result.SupportEntities[2].BranchLength > 0.0f)
        {
            throw new InvalidOperationException("Expected the distant support to remain individual.");
        }

        float expectedBottomDiameter = MathF.Sqrt(2.0f) * SupportDefaults.DefaultStemBottomDiameter;

        if (result.SupportEntities[0].Style.Kind != SupportStyleKind.Clustered)
        {
            throw new InvalidOperationException("Expected clustered output supports to use clustered style dimensions.");
        }

        SupportPartDimensions dimensions = SupportDimensionResolver.Resolve(result.SupportEntities[0].Profile, result.SupportEntities[0].Style);

        if (MathF.Abs(result.SupportEntities[0].Profile.StemBottomDiameter - SupportDefaults.DefaultStemBottomDiameter) > 0.0001f)
        {
            throw new InvalidOperationException("Expected clustered output to keep the source profile stem diameter for later restoration.");
        }

        if (MathF.Abs(dimensions.StemBottomDiameter - expectedBottomDiameter) > 0.0001f)
        {
            throw new InvalidOperationException("Expected automatic stem sizing to preserve combined stem area in resolved dimensions.");
        }

        if (MathF.Abs(dimensions.BranchDiameter - 0.42f) > 0.0001f)
        {
            throw new InvalidOperationException("Expected clustered output branch diameter to come from Cluster settings.");
        }
    }

    /// <summary>
    /// Validates that targeted Cluster modifiers do not absorb nearby untargeted supports.
    /// </summary>
    private static void ValidateSelectionClusterModifierIgnoresUnselectedSupports()
    {
        SupportProfile profile = SupportDefaults.CreateProfile();
        List<SupportEntity> supports = new List<SupportEntity>
        {
            CreateSupport(new Vector3(0.0f, 0.0f, 0.0f), new Vector3(0.0f, 0.0f, 12.0f), profile),
            CreateSupport(new Vector3(2.0f, 0.0f, 0.0f), new Vector3(2.0f, 0.0f, 12.0f), profile),
            CreateSupport(new Vector3(1.0f, 1.0f, 0.0f), new Vector3(1.0f, 1.0f, 12.0f), profile)
        };
        SupportClusterModifierSettings settings = new SupportClusterModifierSettings(
            3.0f,
            2,
            4,
            45.0f,
            SupportClusterStemSizingMode.Automatic,
            SupportDefaults.DefaultStemBottomDiameter,
            SupportDefaults.DefaultStemTopDiameter,
            0.42f);
        SupportModifierDefinition modifier = SupportModifierDefinition.CreateNew(
            SupportModifierKind.Cluster,
            0,
            settings,
            new List<Guid> { supports[0].Id, supports[1].Id },
            0);
        SupportClusterEvaluationResult result = SupportClusterPlanner.Evaluate(supports, modifier);

        if (result.ClusteredSupportCount != 2)
        {
            throw new InvalidOperationException("Expected only selected supports to be clustered.");
        }

        if (result.SupportEntities[2].BranchLength > 0.0f)
        {
            throw new InvalidOperationException("Expected the unselected nearby support to remain individual.");
        }
    }

    /// <summary>
    /// Validates that appending a later targeted Cluster modifier preserves earlier clustered output.
    /// </summary>
    private static void ValidateLaterSelectionClusterPreservesExistingClusters()
    {
        SupportProfile profile = SupportDefaults.CreateProfile();
        List<SupportEntity> supports = new List<SupportEntity>
        {
            CreateSupport(new Vector3(0.0f, 0.0f, 0.0f), new Vector3(0.0f, 0.0f, 12.0f), profile),
            CreateSupport(new Vector3(2.0f, 0.0f, 0.0f), new Vector3(2.0f, 0.0f, 12.0f), profile),
            CreateSupport(new Vector3(10.0f, 0.0f, 0.0f), new Vector3(10.0f, 0.0f, 12.0f), profile),
            CreateSupport(new Vector3(12.0f, 0.0f, 0.0f), new Vector3(12.0f, 0.0f, 12.0f), profile)
        };
        SupportClusterModifierSettings settings = CreateSmokeClusterSettings(3.0f, 4);
        List<SupportModifierDefinition> modifiers = new List<SupportModifierDefinition>
        {
            SupportModifierDefinition.CreateNew(SupportModifierKind.Cluster, 0, settings, new List<Guid> { supports[0].Id, supports[1].Id }, 0),
            SupportModifierDefinition.CreateNew(SupportModifierKind.Cluster, 1, settings, new List<Guid> { supports[2].Id, supports[3].Id }, 0)
        };
        IReadOnlyList<SupportEntity> result = SupportModifierPipeline.ApplyModifiers(supports, modifiers);

        if (result[0].Style.Kind != SupportStyleKind.Clustered || result[1].Style.Kind != SupportStyleKind.Clustered)
        {
            throw new InvalidOperationException("Expected the first selected cluster to remain clustered after a later selection cluster is applied.");
        }

        if (MathF.Abs(result[0].BasePosition.X - 1.0f) > 0.0001f || MathF.Abs(result[1].BasePosition.X - 1.0f) > 0.0001f)
        {
            throw new InvalidOperationException("Expected the first selected cluster stem center to be preserved.");
        }

        if (result[2].Style.Kind != SupportStyleKind.Clustered || result[3].Style.Kind != SupportStyleKind.Clustered)
        {
            throw new InvalidOperationException("Expected the later selected supports to become clustered.");
        }
    }

    /// <summary>
    /// Validates that one cumulative Cluster modifier preserves separate Apply batch boundaries.
    /// </summary>
    private static void ValidateCumulativeClusterModifierKeepsApplyBatchesSeparate()
    {
        SupportProfile profile = SupportDefaults.CreateProfile();
        List<SupportEntity> supports = new List<SupportEntity>
        {
            CreateSupport(new Vector3(0.0f, 0.0f, 0.0f), new Vector3(0.0f, 0.0f, 12.0f), profile),
            CreateSupport(new Vector3(1.0f, 0.0f, 0.0f), new Vector3(1.0f, 0.0f, 12.0f), profile),
            CreateSupport(new Vector3(2.0f, 0.0f, 0.0f), new Vector3(2.0f, 0.0f, 12.0f), profile),
            CreateSupport(new Vector3(3.0f, 0.0f, 0.0f), new Vector3(3.0f, 0.0f, 12.0f), profile),
            CreateSupport(new Vector3(4.0f, 0.0f, 0.0f), new Vector3(4.0f, 0.0f, 12.0f), profile),
            CreateSupport(new Vector3(5.0f, 0.0f, 0.0f), new Vector3(5.0f, 0.0f, 12.0f), profile)
        };
        SupportClusterModifierSettings settings = CreateSmokeClusterSettings(3.0f, 6);
        List<SupportModifierTargetBatch> batches = new List<SupportModifierTargetBatch>
        {
            new SupportModifierTargetBatch(new List<Guid> { supports[0].Id, supports[1].Id, supports[2].Id }),
            new SupportModifierTargetBatch(new List<Guid> { supports[3].Id, supports[4].Id, supports[5].Id })
        };
        SupportModifierDefinition modifier = SupportModifierDefinition.CreateNew(
            SupportModifierKind.Cluster,
            0,
            settings,
            null,
            batches,
            0);
        SupportClusterEvaluationResult result = SupportClusterPlanner.Evaluate(supports, modifier);

        if (result.ClusterCount != 2)
        {
            throw new InvalidOperationException("Expected cumulative targeted clustering to preserve two Apply batches as two clusters.");
        }

        for (int i = 0; i < result.SupportEntities.Count; i++)
        {
            if (result.SupportEntities[i].Style.Kind != SupportStyleKind.Clustered)
            {
                throw new InvalidOperationException("Expected every batched support to be clustered.");
            }
        }

        if (MathF.Abs(result.SupportEntities[0].BasePosition.X - 1.0f) > 0.0001f
            || MathF.Abs(result.SupportEntities[1].BasePosition.X - 1.0f) > 0.0001f
            || MathF.Abs(result.SupportEntities[2].BasePosition.X - 1.0f) > 0.0001f)
        {
            throw new InvalidOperationException("Expected the first Apply batch to keep its own shared stem center.");
        }

        if (MathF.Abs(result.SupportEntities[3].BasePosition.X - 4.0f) > 0.0001f
            || MathF.Abs(result.SupportEntities[4].BasePosition.X - 4.0f) > 0.0001f
            || MathF.Abs(result.SupportEntities[5].BasePosition.X - 4.0f) > 0.0001f)
        {
            throw new InvalidOperationException("Expected the second Apply batch to keep its own shared stem center.");
        }
    }

    /// <summary>
    /// Validates that selecting a clustered member and a nearby individual support replans the whole selected cluster.
    /// </summary>
    private static void ValidateSelectedIndividualJoinsSelectedCluster()
    {
        SupportProfile profile = SupportDefaults.CreateProfile();
        List<SupportEntity> supports = new List<SupportEntity>
        {
            CreateSupport(new Vector3(0.0f, 0.0f, 0.0f), new Vector3(0.0f, 0.0f, 12.0f), profile),
            CreateSupport(new Vector3(2.0f, 0.0f, 0.0f), new Vector3(2.0f, 0.0f, 12.0f), profile),
            CreateSupport(new Vector3(3.0f, 0.0f, 0.0f), new Vector3(3.0f, 0.0f, 12.0f), profile)
        };
        SupportClusterModifierSettings settings = CreateSmokeClusterSettings(3.0f, 4);
        SupportClusterEvaluationResult existingCluster = SupportClusterPlanner.Evaluate(
            supports,
            SupportModifierDefinition.CreateNew(SupportModifierKind.Cluster, 0, settings, new List<Guid> { supports[0].Id, supports[1].Id }, 0));
        SupportClusterEvaluationResult result = SupportClusterPlanner.Evaluate(
            existingCluster.SupportEntities,
            SupportModifierDefinition.CreateNew(SupportModifierKind.Cluster, 1, settings, new List<Guid> { supports[0].Id, supports[2].Id }, 0));

        if (result.SupportEntities[0].Style.Kind != SupportStyleKind.Clustered
            || result.SupportEntities[1].Style.Kind != SupportStyleKind.Clustered
            || result.SupportEntities[2].Style.Kind != SupportStyleKind.Clustered)
        {
            throw new InvalidOperationException("Expected the selected individual support to join the selected existing cluster.");
        }

        if (MathF.Abs(result.SupportEntities[0].BasePosition.X - result.SupportEntities[2].BasePosition.X) > 0.0001f)
        {
            throw new InvalidOperationException("Expected the joined individual and selected cluster members to share a replanned stem center.");
        }
    }

    /// <summary>
    /// Validates that a selected individual joins the nearest feasible selected cluster when more than one can accept it.
    /// </summary>
    private static void ValidateSelectedIndividualJoinsNearestFeasibleCluster()
    {
        SupportProfile profile = SupportDefaults.CreateProfile();
        List<SupportEntity> supports = new List<SupportEntity>
        {
            CreateSupport(new Vector3(0.0f, 0.0f, 0.0f), new Vector3(0.0f, 0.0f, 12.0f), profile),
            CreateSupport(new Vector3(2.0f, 0.0f, 0.0f), new Vector3(2.0f, 0.0f, 12.0f), profile),
            CreateSupport(new Vector3(8.0f, 0.0f, 0.0f), new Vector3(8.0f, 0.0f, 12.0f), profile),
            CreateSupport(new Vector3(10.0f, 0.0f, 0.0f), new Vector3(10.0f, 0.0f, 12.0f), profile),
            CreateSupport(new Vector3(5.5f, 0.0f, 0.0f), new Vector3(5.5f, 0.0f, 12.0f), profile)
        };
        SupportClusterModifierSettings settings = CreateSmokeClusterSettings(3.1f, 4);
        List<SupportModifierDefinition> setupModifiers = new List<SupportModifierDefinition>
        {
            SupportModifierDefinition.CreateNew(SupportModifierKind.Cluster, 0, settings, new List<Guid> { supports[0].Id, supports[1].Id }, 0),
            SupportModifierDefinition.CreateNew(SupportModifierKind.Cluster, 1, settings, new List<Guid> { supports[2].Id, supports[3].Id }, 0)
        };
        IReadOnlyList<SupportEntity> clusteredSupports = SupportModifierPipeline.ApplyModifiers(supports, setupModifiers);
        SupportClusterEvaluationResult result = SupportClusterPlanner.Evaluate(
            clusteredSupports,
            SupportModifierDefinition.CreateNew(SupportModifierKind.Cluster, 2, settings, new List<Guid> { supports[0].Id, supports[2].Id, supports[4].Id }, 0));

        if (MathF.Abs(result.SupportEntities[4].BasePosition.X - result.SupportEntities[2].BasePosition.X) > 0.0001f)
        {
            throw new InvalidOperationException("Expected the individual support to join the nearer selected cluster.");
        }

        if (MathF.Abs(result.SupportEntities[0].BasePosition.X - 1.0f) > 0.0001f)
        {
            throw new InvalidOperationException("Expected the farther selected cluster to remain unchanged.");
        }
    }

    /// <summary>
    /// Validates that individuals which do not join selected clusters still cluster with other selected individuals.
    /// </summary>
    private static void ValidateRemainingSelectedIndividualsClusterAfterMergeAttempts()
    {
        SupportProfile profile = SupportDefaults.CreateProfile();
        List<SupportEntity> supports = new List<SupportEntity>
        {
            CreateSupport(new Vector3(0.0f, 0.0f, 0.0f), new Vector3(0.0f, 0.0f, 12.0f), profile),
            CreateSupport(new Vector3(2.0f, 0.0f, 0.0f), new Vector3(2.0f, 0.0f, 12.0f), profile),
            CreateSupport(new Vector3(3.0f, 0.0f, 0.0f), new Vector3(3.0f, 0.0f, 12.0f), profile),
            CreateSupport(new Vector3(10.0f, 0.0f, 0.0f), new Vector3(10.0f, 0.0f, 12.0f), profile),
            CreateSupport(new Vector3(12.0f, 0.0f, 0.0f), new Vector3(12.0f, 0.0f, 12.0f), profile)
        };
        SupportClusterModifierSettings settings = CreateSmokeClusterSettings(3.0f, 3);
        SupportClusterEvaluationResult existingCluster = SupportClusterPlanner.Evaluate(
            supports,
            SupportModifierDefinition.CreateNew(SupportModifierKind.Cluster, 0, settings, new List<Guid> { supports[0].Id, supports[1].Id }, 0));
        SupportClusterEvaluationResult result = SupportClusterPlanner.Evaluate(
            existingCluster.SupportEntities,
            SupportModifierDefinition.CreateNew(SupportModifierKind.Cluster, 1, settings, new List<Guid> { supports[0].Id, supports[2].Id, supports[3].Id, supports[4].Id }, 0));

        if (result.SupportEntities[2].Style.Kind != SupportStyleKind.Clustered
            || MathF.Abs(result.SupportEntities[2].BasePosition.X - result.SupportEntities[0].BasePosition.X) > 0.0001f)
        {
            throw new InvalidOperationException("Expected the nearby individual support to join the selected existing cluster.");
        }

        if (result.SupportEntities[3].Style.Kind != SupportStyleKind.Clustered
            || result.SupportEntities[4].Style.Kind != SupportStyleKind.Clustered
            || MathF.Abs(result.SupportEntities[3].BasePosition.X - result.SupportEntities[4].BasePosition.X) > 0.0001f)
        {
            throw new InvalidOperationException("Expected remaining selected individuals to form their own cluster.");
        }
    }

    /// <summary>
    /// Validates that selected mixed clustering does not disturb unselected existing clusters.
    /// </summary>
    private static void ValidateUnselectedClusteredSupportsRemainUnchanged()
    {
        SupportProfile profile = SupportDefaults.CreateProfile();
        List<SupportEntity> supports = new List<SupportEntity>
        {
            CreateSupport(new Vector3(0.0f, 0.0f, 0.0f), new Vector3(0.0f, 0.0f, 12.0f), profile),
            CreateSupport(new Vector3(2.0f, 0.0f, 0.0f), new Vector3(2.0f, 0.0f, 12.0f), profile),
            CreateSupport(new Vector3(3.0f, 0.0f, 0.0f), new Vector3(3.0f, 0.0f, 12.0f), profile),
            CreateSupport(new Vector3(10.0f, 0.0f, 0.0f), new Vector3(10.0f, 0.0f, 12.0f), profile),
            CreateSupport(new Vector3(12.0f, 0.0f, 0.0f), new Vector3(12.0f, 0.0f, 12.0f), profile)
        };
        SupportClusterModifierSettings settings = CreateSmokeClusterSettings(3.0f, 4);
        List<SupportModifierDefinition> setupModifiers = new List<SupportModifierDefinition>
        {
            SupportModifierDefinition.CreateNew(SupportModifierKind.Cluster, 0, settings, new List<Guid> { supports[0].Id, supports[1].Id }, 0),
            SupportModifierDefinition.CreateNew(SupportModifierKind.Cluster, 1, settings, new List<Guid> { supports[3].Id, supports[4].Id }, 0)
        };
        IReadOnlyList<SupportEntity> clusteredSupports = SupportModifierPipeline.ApplyModifiers(supports, setupModifiers);
        SupportClusterEvaluationResult result = SupportClusterPlanner.Evaluate(
            clusteredSupports,
            SupportModifierDefinition.CreateNew(SupportModifierKind.Cluster, 2, settings, new List<Guid> { supports[0].Id, supports[2].Id }, 0));

        if (MathF.Abs(result.SupportEntities[3].BasePosition.X - clusteredSupports[3].BasePosition.X) > 0.0001f
            || MathF.Abs(result.SupportEntities[4].BasePosition.X - clusteredSupports[4].BasePosition.X) > 0.0001f)
        {
            throw new InvalidOperationException("Expected the unselected existing cluster to remain unchanged.");
        }
    }

    /// <summary>
    /// Creates reusable cluster settings for support-clustering smoke tests.
    /// </summary>
    private static SupportClusterModifierSettings CreateSmokeClusterSettings(float maximumClusterRadius, int maximumSupportsPerCluster)
    {
        return new SupportClusterModifierSettings(
            maximumClusterRadius,
            2,
            maximumSupportsPerCluster,
            45.0f,
            SupportClusterStemSizingMode.Automatic,
            SupportDefaults.DefaultStemBottomDiameter,
            SupportDefaults.DefaultStemTopDiameter,
            0.42f);
    }

    /// <summary>
    /// Validates that Brace modifiers append simple printable brace member output.
    /// </summary>
    private static void ValidateBraceModifierAddsPrintableBraceMembers()
    {
        SupportProfile profile = SupportDefaults.CreateProfile();
        List<SupportEntity> supports = new List<SupportEntity>
        {
            CreateSupport(new Vector3(0.0f, 0.0f, 0.0f), new Vector3(0.0f, 0.0f, 12.0f), profile),
            CreateSupport(new Vector3(3.0f, 0.0f, 0.0f), new Vector3(3.0f, 0.0f, 12.0f), profile),
            CreateSupport(new Vector3(0.0f, 3.0f, 0.0f), new Vector3(0.0f, 3.0f, 12.0f), profile)
        };
        SupportBraceModifierSettings settings = new SupportBraceModifierSettings(10.0f, 80.0f, 20.0f, 0.5f);
        SupportModifierDefinition modifier = SupportModifierDefinition.CreateNewBrace(0, settings, new List<Guid> { supports[0].Id, supports[1].Id, supports[2].Id }, 0);
        IReadOnlyList<SupportEntity> result = SupportModifierPipeline.ApplyModifiers(supports, new List<SupportModifierDefinition> { modifier });

        if (result.Count <= supports.Count)
        {
            throw new InvalidOperationException("Expected Brace modifier to append generated brace members.");
        }

        bool foundBraceMember = false;

        for (int i = supports.Count; i < result.Count; i++)
        {
            if (result[i].Style.Kind == SupportStyleKind.BraceMember)
            {
                foundBraceMember = true;
                SupportMeshData meshData = SupportMeshBuilder.Build(result[i]);
                ValidateClosedMesh(meshData);
            }
        }

        if (!foundBraceMember)
        {
            throw new InvalidOperationException("Expected generated brace output to use BraceMember style.");
        }
    }

    /// <summary>
    /// Validates that Buttress modifiers create paired supports only for targets above the height threshold.
    /// </summary>
    private static void ValidateButtressModifierTargetsTallSupportsOnly()
    {
        SupportProfile profile = SupportDefaults.CreateProfile();
        List<SupportEntity> supports = new List<SupportEntity>
        {
            CreateSupport(new Vector3(0.0f, 0.0f, 0.0f), new Vector3(0.0f, 0.0f, 60.0f), profile),
            CreateSupport(new Vector3(6.0f, 0.0f, 0.0f), new Vector3(6.0f, 0.0f, 20.0f), profile)
        };
        SupportButtressModifierSettings settings = new SupportButtressModifierSettings(40.0f, 8.0f);
        SupportModifierDefinition modifier = SupportModifierDefinition.CreateNewButtress(
            0,
            settings,
            new List<Guid> { supports[0].Id, supports[1].Id },
            0);
        SupportBracingEvaluationResult evaluation = SupportBracingPlanner.EvaluateButtress(supports, modifier);

        if (evaluation.TargetSupportCount != 1
            || CountSupportsWithStyle(evaluation.SupportEntities, SupportStyleKind.Buttress) != 2)
        {
            throw new InvalidOperationException("Expected exactly two buttress supports for the one tall target.");
        }
    }

    /// <summary>
    /// Validates paired buttress placement, copied dimensions, branch geometry, meshes, and captured bracing parameters.
    /// </summary>
    private static void ValidateButtressModifierCreatesPairedEquilateralSupports()
    {
        const float Spacing = 10.0f;
        const float MinimumAngleDegrees = 30.0f;
        const float BraceDiameter = 0.77f;
        Guid supportLayerGroupId = Guid.NewGuid();
        SupportProfile profile = CreateAngledProfile(90.0f);
        Vector3 basePosition = Vector3.Zero;
        Vector3 tipPosition = new Vector3(0.0f, profile.HeadHeight, 60.0f);
        SupportEntity sourceSupport = new SupportEntity(
            supportLayerGroupId,
            tipPosition,
            basePosition,
            Vector3.UnitY,
            profile);
        SupportBraceModifierSettings braceSettings = new SupportBraceModifierSettings(
            MinimumAngleDegrees,
            70.0f,
            100.0f,
            BraceDiameter);
        SupportButtressModifierSettings buttressSettings = new SupportButtressModifierSettings(
            40.0f,
            Spacing,
            braceSettings);
        SupportModifierDefinition modifier = SupportModifierDefinition.CreateNewButtress(
            0,
            buttressSettings,
            new List<Guid> { sourceSupport.Id },
            0);
        SupportBracingEvaluationResult evaluation = SupportBracingPlanner.EvaluateButtress(
            new List<SupportEntity> { sourceSupport },
            modifier);
        List<SupportEntity> buttresses = new List<SupportEntity>();
        List<SupportEntity> braceMembers = new List<SupportEntity>();

        for (int i = 1; i < evaluation.SupportEntities.Count; i++)
        {
            SupportEntity generated = evaluation.SupportEntities[i];

            if (generated.Style.Kind == SupportStyleKind.Buttress)
            {
                buttresses.Add(generated);
                ValidateClosedMesh(SupportMeshBuilder.Build(generated));
            }
            else if (generated.Style is BraceMemberSupportStyle braceMemberStyle)
            {
                braceMembers.Add(generated);

                if (MathF.Abs(braceMemberStyle.Diameter - BraceDiameter) > 0.0001f)
                {
                    throw new InvalidOperationException("Expected buttress reinforcement members to use the captured Brace diameter.");
                }
            }
        }

        if (buttresses.Count != 2 || braceMembers.Count != 6)
        {
            throw new InvalidOperationException("Expected two buttress supports and three complete two-member brace pairs.");
        }

        Vector2 originalBase = new Vector2(sourceSupport.BasePosition.X, sourceSupport.BasePosition.Y);
        Vector2 firstBase = new Vector2(buttresses[0].BasePosition.X, buttresses[0].BasePosition.Y);
        Vector2 secondBase = new Vector2(buttresses[1].BasePosition.X, buttresses[1].BasePosition.Y);
        ValidateDistanceNear(Spacing, Vector2.Distance(originalBase, firstBase), "Expected the first buttress to be one spacing from the original.");
        ValidateDistanceNear(Spacing, Vector2.Distance(originalBase, secondBase), "Expected the second buttress to be one spacing from the original.");
        ValidateDistanceNear(Spacing, Vector2.Distance(firstBase, secondBase), "Expected both buttresses to be one spacing apart.");

        if (firstBase.Y >= originalBase.Y || secondBase.Y >= originalBase.Y)
        {
            throw new InvalidOperationException("Expected both buttress bases behind the source support's head direction.");
        }

        Vector3 originalStemTop = sourceSupport.TipPosition - (sourceSupport.HeadDirection * sourceSupport.Profile.HeadHeight);
        float sourceBranchDiameter = SupportDimensionResolver.Resolve(sourceSupport.Profile, sourceSupport.Style).BranchDiameter;

        for (int i = 0; i < buttresses.Count; i++)
        {
            SupportEntity buttress = buttresses[i];
            Vector3 stemJoint = buttress.TipPosition - (buttress.BranchDirection * buttress.BranchLength);
            Vector3 branchVector = buttress.TipPosition - stemJoint;
            float horizontalDistance = new Vector2(branchVector.X, branchVector.Y).Length();
            float branchAngleDegrees = MathF.Atan2(branchVector.Z, horizontalDistance) * (180.0f / MathF.PI);

            ValidateVectorNear(originalStemTop, buttress.TipPosition, 0.0001f, "Expected the buttress branch to end at the original stem top.");

            if (MathF.Abs(branchAngleDegrees - MinimumAngleDegrees) > 0.0001f
                || buttress.Style is not ButtressSupportStyle buttressStyle
                || MathF.Abs(buttressStyle.BranchDiameter - sourceBranchDiameter) > 0.0001f
                || MathF.Abs(buttress.Profile.BaseBottomRadius - sourceSupport.Profile.BaseBottomRadius) > 0.0001f
                || MathF.Abs(buttress.Profile.BaseHeight - sourceSupport.Profile.BaseHeight) > 0.0001f
                || MathF.Abs(buttress.Profile.StemBottomDiameter - sourceSupport.Profile.StemBottomDiameter) > 0.0001f
                || MathF.Abs(buttress.Profile.StemTopDiameter - sourceSupport.Profile.StemTopDiameter) > 0.0001f)
            {
                throw new InvalidOperationException("Expected buttress branch angle and all base, stem, and branch dimensions to match the source settings.");
            }
        }

        ValidateDirectionalButtressBracePair(braceMembers, 0, buttresses[0], sourceSupport, MinimumAngleDegrees, 70.0f);
        ValidateDirectionalButtressBracePair(braceMembers, 2, buttresses[1], sourceSupport, MinimumAngleDegrees, 70.0f);
        ValidateDirectionalButtressBracePair(braceMembers, 4, buttresses[0], buttresses[1], MinimumAngleDegrees, 70.0f);
    }

    /// <summary>
    /// Validates that each directional pair keeps its feasible lower member when the return angle is invalid.
    /// </summary>
    private static void ValidateButtressBracingKeepsPrimaryWhenReturnDoesNotFit()
    {
        const float FixedAngleDegrees = 70.0f;
        Guid supportLayerGroupId = Guid.NewGuid();
        SupportProfile profile = CreateAngledProfile(90.0f);
        SupportEntity sourceSupport = new SupportEntity(
            supportLayerGroupId,
            new Vector3(0.0f, profile.HeadHeight, 60.0f),
            Vector3.Zero,
            Vector3.UnitY,
            profile);
        SupportBraceModifierSettings braceSettings = new SupportBraceModifierSettings(
            FixedAngleDegrees,
            FixedAngleDegrees,
            100.0f,
            0.5f);
        SupportButtressModifierSettings buttressSettings = new SupportButtressModifierSettings(40.0f, 10.0f, braceSettings);
        SupportModifierDefinition modifier = SupportModifierDefinition.CreateNewButtress(
            0,
            buttressSettings,
            new List<Guid> { sourceSupport.Id },
            0);
        SupportBracingEvaluationResult evaluation = SupportBracingPlanner.EvaluateButtress(
            new List<SupportEntity> { sourceSupport },
            modifier);
        List<SupportEntity> buttresses = new List<SupportEntity>();
        List<SupportEntity> braceMembers = new List<SupportEntity>();

        for (int i = 1; i < evaluation.SupportEntities.Count; i++)
        {
            SupportEntity generated = evaluation.SupportEntities[i];

            if (generated.Style.Kind == SupportStyleKind.Buttress)
            {
                buttresses.Add(generated);
            }
            else if (generated.Style.Kind == SupportStyleKind.BraceMember)
            {
                braceMembers.Add(generated);
            }
        }

        if (buttresses.Count != 2 || braceMembers.Count != 3 || evaluation.AddedMemberCount != 5)
        {
            throw new InvalidOperationException("Expected two buttresses and only the three feasible lower brace members.");
        }

        for (int i = 0; i < braceMembers.Count; i++)
        {
            SupportEntity braceMember = braceMembers[i];
            bool startsOnButtress = IsSameXy(braceMember.BasePosition, buttresses[0].BasePosition)
                || IsSameXy(braceMember.BasePosition, buttresses[1].BasePosition);

            if (!startsOnButtress)
            {
                throw new InvalidOperationException("Expected every lower buttress brace to start on a buttress base top.");
            }

            ValidateBraceAngleRange(braceMember, FixedAngleDegrees, FixedAngleDegrees);
        }
    }

    /// <summary>
    /// Validates one primary-and-return zig-zag pair and its configured angle limits.
    /// </summary>
    private static void ValidateDirectionalButtressBracePair(
        IReadOnlyList<SupportEntity> braceMembers,
        int memberIndex,
        SupportEntity startSupport,
        SupportEntity endSupport,
        float minimumAngleDegrees,
        float maximumAngleDegrees)
    {
        SupportEntity primaryMember = braceMembers[memberIndex];
        SupportEntity returnMember = braceMembers[memberIndex + 1];
        Vector3 expectedBaseTop = startSupport.BasePosition + (Vector3.UnitZ * startSupport.Profile.BaseHeight);
        Vector3 expectedEndStemTop = CalculateTestStemTop(endSupport);
        Vector3 expectedReturnStemTop = CalculateTestStemTop(startSupport);

        ValidateVectorNear(expectedBaseTop, primaryMember.BasePosition, 0.0001f, "Expected the lower brace to start at the buttress base top.");

        if (!IsSameXy(primaryMember.TipPosition, expectedEndStemTop))
        {
            throw new InvalidOperationException("Expected the lower brace to rise toward the opposite support stem.");
        }

        ValidateVectorNear(primaryMember.TipPosition, returnMember.BasePosition, 0.0001f, "Expected the return brace to begin at the lower brace endpoint.");
        ValidateVectorNear(expectedReturnStemTop, returnMember.TipPosition, 0.0001f, "Expected the return brace to end at the originating buttress stem top.");
        ValidateBraceAngleRange(primaryMember, minimumAngleDegrees, maximumAngleDegrees);
        ValidateBraceAngleRange(returnMember, minimumAngleDegrees, maximumAngleDegrees);
    }

    /// <summary>
    /// Gets the stem top used by buttress brace tests.
    /// </summary>
    private static Vector3 CalculateTestStemTop(SupportEntity support)
    {
        if (support.Style.Kind == SupportStyleKind.Buttress)
        {
            return support.TipPosition - (Vector3.Normalize(support.BranchDirection) * support.BranchLength);
        }

        Vector3 headDirection = SupportHeadDirectionCalculator.ClampDirectionToProfile(support.HeadDirection, support.Profile);
        return support.TipPosition - (headDirection * support.Profile.HeadHeight);
    }

    /// <summary>
    /// Validates the angle of one generated brace member against inclusive limits.
    /// </summary>
    private static void ValidateBraceAngleRange(SupportEntity braceMember, float minimumAngleDegrees, float maximumAngleDegrees)
    {
        Vector3 braceVector = braceMember.TipPosition - braceMember.BasePosition;
        float horizontalDistance = new Vector2(braceVector.X, braceVector.Y).Length();
        float angleDegrees = MathF.Atan2(braceVector.Z, horizontalDistance) * (180.0f / MathF.PI);

        if (angleDegrees < minimumAngleDegrees - 0.0001f || angleDegrees > maximumAngleDegrees + 0.0001f)
        {
            throw new InvalidOperationException($"Expected brace angle in [{minimumAngleDegrees}, {maximumAngleDegrees}], actual {angleDegrees}.");
        }
    }

    /// <summary>
    /// Validates that support identity ordering cannot hide the only feasible brace direction.
    /// </summary>
    private static void ValidateBraceModifierEvaluatesBothDirections()
    {
        Guid supportLayerGroupId = Guid.NewGuid();
        SupportProfile profile = SupportDefaults.CreateProfile();
        SupportEntity tallSupport = CreateSupportForGroupWithId(
            Guid.Parse("00000000-0000-0000-0000-000000000001"),
            supportLayerGroupId,
            new Vector2(5.0f, 0.0f),
            20.0f,
            profile);
        SupportEntity shortSupport = CreateSupportForGroupWithId(
            Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff"),
            supportLayerGroupId,
            Vector2.Zero,
            7.0f,
            profile);
        List<SupportEntity> supports = new List<SupportEntity> { tallSupport, shortSupport };
        SupportModifierDefinition modifier = SupportModifierDefinition.CreateNewBrace(
            0,
            CreateNearestNeighborBraceSettings(),
            CreateSupportIds(supports),
            0);
        SupportBracingEvaluationResult evaluation = SupportBracingPlanner.EvaluateBrace(supports, modifier);

        if (evaluation.AddedMemberCount != 1
            || !IsSameXy(evaluation.SupportEntities[supports.Count].BasePosition, shortSupport.BasePosition)
            || !IsSameXy(evaluation.SupportEntities[supports.Count].TipPosition, tallSupport.BasePosition))
        {
            throw new InvalidOperationException("Expected the planner to choose the feasible short-base-top to tall-stem-top direction.");
        }
    }

    /// <summary>
    /// Validates that brace cylinders begin at the center of the printable base's top surface.
    /// </summary>
    private static void ValidateBraceModifierStartsAtBaseTop()
    {
        Guid supportLayerGroupId = Guid.NewGuid();
        SupportProfile profile = SupportDefaults.CreateProfile();
        List<SupportEntity> supports = CreateSupportsForGroup(
            supportLayerGroupId,
            new List<Vector2>
            {
                new Vector2(0.0f, 0.0f),
                new Vector2(3.0f, 0.0f)
            },
            20.0f,
            profile);
        SupportModifierDefinition modifier = SupportModifierDefinition.CreateNewBrace(
            0,
            CreateNearestNeighborBraceSettings(),
            CreateSupportIds(supports),
            0);
        SupportBracingEvaluationResult evaluation = SupportBracingPlanner.EvaluateBrace(supports, modifier);

        if (evaluation.AddedMemberCount == 0)
        {
            throw new InvalidOperationException("Expected at least one brace for the base-top endpoint test.");
        }

        SupportEntity member = evaluation.SupportEntities[supports.Count];
        int startSupportIndex = FindSupportIndexByXy(supports, member.BasePosition);
        float expectedStartZ = supports[startSupportIndex].BasePosition.Z + profile.BaseHeight;

        if (MathF.Abs(member.BasePosition.Z - expectedStartZ) > 0.0001f)
        {
            throw new InvalidOperationException("Expected the brace to start at the top of the support base.");
        }
    }

    /// <summary>
    /// Validates that sufficient remaining stem height creates one rising zig-zag return member.
    /// </summary>
    private static void ValidateBraceModifierAddsRisingReturnMember()
    {
        Guid supportLayerGroupId = Guid.NewGuid();
        SupportProfile profile = SupportDefaults.CreateProfile();
        List<SupportEntity> supports = CreateSupportsForGroup(
            supportLayerGroupId,
            new List<Vector2>
            {
                new Vector2(0.0f, 0.0f),
                new Vector2(3.0f, 0.0f)
            },
            20.0f,
            profile);
        SupportModifierDefinition modifier = SupportModifierDefinition.CreateNewBrace(
            0,
            SupportBraceModifierSettings.CreateDefault(),
            CreateSupportIds(supports),
            0);
        SupportBracingEvaluationResult evaluation = SupportBracingPlanner.EvaluateBrace(supports, modifier);

        if (evaluation.AddedMemberCount != 2)
        {
            throw new InvalidOperationException($"Expected a primary and return brace member but generated {evaluation.AddedMemberCount}.");
        }

        SupportEntity primaryMember = evaluation.SupportEntities[supports.Count];
        SupportEntity returnMember = evaluation.SupportEntities[supports.Count + 1];

        if (Vector3.DistanceSquared(primaryMember.TipPosition, returnMember.BasePosition) > 0.00000001f
            || returnMember.TipPosition.Z <= returnMember.BasePosition.Z
            || IsSameXy(returnMember.TipPosition, primaryMember.TipPosition))
        {
            throw new InvalidOperationException("Expected the return member to rise from the first brace endpoint back toward the opposite stem.");
        }
    }

    /// <summary>
    /// Validates the new-session maximum brace angle default.
    /// </summary>
    private static void ValidateBraceModifierDefaultsMaximumAngleTo70()
    {
        SupportBraceModifierSettings settings = SupportBraceModifierSettings.CreateDefault();

        if (MathF.Abs(settings.MaximumBraceAngleDegrees - 70.0f) > 0.0001f)
        {
            throw new InvalidOperationException("Expected the default maximum brace angle to be 70 degrees.");
        }
    }

    /// <summary>
    /// Validates that a regular ring receives only cyclically adjacent brace members.
    /// </summary>
    private static void ValidateBraceModifierKeepsAdjacentRingNeighbors()
    {
        Guid supportLayerGroupId = Guid.NewGuid();
        SupportProfile profile = SupportDefaults.CreateProfile();
        List<Vector2> positions = new List<Vector2>();
        const int SupportCount = 8;

        for (int i = 0; i < SupportCount; i++)
        {
            float angle = (MathF.PI * 2.0f * i) / SupportCount;
            positions.Add(new Vector2(MathF.Cos(angle) * 10.0f, MathF.Sin(angle) * 10.0f));
        }

        List<SupportEntity> supports = CreateSupportsForGroup(supportLayerGroupId, positions, 20.0f, profile);
        SupportModifierDefinition modifier = SupportModifierDefinition.CreateNewBrace(
            0,
            CreateNearestNeighborBraceSettings(),
            CreateSupportIds(supports),
            0);
        SupportBracingEvaluationResult evaluation = SupportBracingPlanner.EvaluateBrace(supports, modifier);

        if (evaluation.AddedMemberCount != SupportCount * 2)
        {
            throw new InvalidOperationException($"Expected {SupportCount * 2} adjacent ring brace members but generated {evaluation.AddedMemberCount}.");
        }

        for (int i = supports.Count; i < evaluation.SupportEntities.Count; i++)
        {
            SupportEntity member = evaluation.SupportEntities[i];
            int startIndex = FindSupportIndexByXy(supports, member.BasePosition);
            int endIndex = FindSupportIndexByXy(supports, member.TipPosition);
            int indexDifference = Math.Abs(startIndex - endIndex);

            if (indexDifference != 1 && indexDifference != SupportCount - 1)
            {
                throw new InvalidOperationException("Expected every ring brace to connect cyclically adjacent supports.");
            }
        }
    }

    /// <summary>
    /// Validates that a collinear brace graph cannot jump over an intermediate feasible support.
    /// </summary>
    private static void ValidateBraceModifierDoesNotSkipCollinearSupports()
    {
        Guid supportLayerGroupId = Guid.NewGuid();
        SupportProfile profile = SupportDefaults.CreateProfile();
        List<SupportEntity> supports = CreateSupportsForGroup(
            supportLayerGroupId,
            new List<Vector2>
            {
                new Vector2(0.0f, 0.0f),
                new Vector2(3.0f, 0.0f),
                new Vector2(6.0f, 0.0f),
                new Vector2(9.0f, 0.0f)
            },
            20.0f,
            profile);
        SupportModifierDefinition modifier = SupportModifierDefinition.CreateNewBrace(
            0,
            CreateNearestNeighborBraceSettings(),
            CreateSupportIds(supports),
            0);
        SupportBracingEvaluationResult evaluation = SupportBracingPlanner.EvaluateBrace(supports, modifier);

        if (evaluation.AddedMemberCount != (supports.Count - 1) * 2)
        {
            throw new InvalidOperationException("Expected two brace members between each adjacent pair of collinear supports.");
        }

        for (int i = supports.Count; i < evaluation.SupportEntities.Count; i++)
        {
            SupportEntity member = evaluation.SupportEntities[i];
            float horizontalSpan = MathF.Abs(member.BasePosition.X - member.TipPosition.X);

            if (MathF.Abs(horizontalSpan - 3.0f) > 0.0001f)
            {
                throw new InvalidOperationException("A collinear brace skipped a nearer intermediate support.");
            }
        }
    }

    /// <summary>
    /// Validates the relative-neighborhood invariant on an irregular generator-independent layout.
    /// </summary>
    private static void ValidateBraceModifierUsesRelativeNeighborhoodOnIrregularLayout()
    {
        Guid supportLayerGroupId = Guid.NewGuid();
        SupportProfile profile = SupportDefaults.CreateProfile();
        List<SupportEntity> supports = CreateSupportsForGroup(
            supportLayerGroupId,
            new List<Vector2>
            {
                new Vector2(0.0f, 0.0f),
                new Vector2(2.0f, 0.5f),
                new Vector2(4.0f, 0.0f),
                new Vector2(1.0f, 4.0f),
                new Vector2(5.0f, 3.0f)
            },
            20.0f,
            profile);
        SupportModifierDefinition modifier = SupportModifierDefinition.CreateNewBrace(
            0,
            CreateNearestNeighborBraceSettings(),
            CreateSupportIds(supports),
            0);
        SupportBracingEvaluationResult evaluation = SupportBracingPlanner.EvaluateBrace(supports, modifier);

        for (int i = supports.Count; i < evaluation.SupportEntities.Count; i++)
        {
            SupportEntity member = evaluation.SupportEntities[i];
            int startIndex = FindSupportIndexByXy(supports, member.BasePosition);
            int endIndex = FindSupportIndexByXy(supports, member.TipPosition);
            Vector2 start = new Vector2(supports[startIndex].BasePosition.X, supports[startIndex].BasePosition.Y);
            Vector2 end = new Vector2(supports[endIndex].BasePosition.X, supports[endIndex].BasePosition.Y);
            float pairDistanceSquared = Vector2.DistanceSquared(start, end);

            for (int supportIndex = 0; supportIndex < supports.Count; supportIndex++)
            {
                if (supportIndex == startIndex || supportIndex == endIndex)
                {
                    continue;
                }

                Vector2 other = new Vector2(supports[supportIndex].BasePosition.X, supports[supportIndex].BasePosition.Y);
                bool closerToStart = Vector2.DistanceSquared(start, other) + 0.0001f < pairDistanceSquared;
                bool closerToEnd = Vector2.DistanceSquared(end, other) + 0.0001f < pairDistanceSquared;

                if (closerToStart && closerToEnd)
                {
                    throw new InvalidOperationException("An irregular-layout brace crossed a closer feasible relative neighbor.");
                }
            }
        }
    }

    /// <summary>
    /// Validates that equally near supports do not block each other through floating-point tie noise.
    /// </summary>
    private static void ValidateBraceModifierPreservesEqualDistanceNeighbors()
    {
        Guid supportLayerGroupId = Guid.NewGuid();
        SupportProfile profile = SupportDefaults.CreateProfile();
        float triangleHeight = MathF.Sqrt(27.0f);
        List<SupportEntity> supports = CreateSupportsForGroup(
            supportLayerGroupId,
            new List<Vector2>
            {
                new Vector2(0.0f, 0.0f),
                new Vector2(6.0f, 0.0f),
                new Vector2(3.0f, triangleHeight)
            },
            20.0f,
            profile);
        SupportModifierDefinition modifier = SupportModifierDefinition.CreateNewBrace(
            0,
            CreateNearestNeighborBraceSettings(),
            CreateSupportIds(supports),
            0);
        SupportBracingEvaluationResult evaluation = SupportBracingPlanner.EvaluateBrace(supports, modifier);

        if (evaluation.AddedMemberCount != 6)
        {
            throw new InvalidOperationException("Expected both brace members for all three equal-distance triangle neighbors.");
        }
    }

    /// <summary>
    /// Validates that relative-neighborhood filtering still enforces the physical three-connection limit.
    /// </summary>
    private static void ValidateBraceModifierLimitsConnections()
    {
        Guid supportLayerGroupId = Guid.NewGuid();
        SupportProfile profile = SupportDefaults.CreateProfile();
        List<SupportEntity> supports = CreateSupportsForGroup(
            supportLayerGroupId,
            new List<Vector2>
            {
                Vector2.Zero,
                new Vector2(6.0f, 0.0f),
                new Vector2(-6.0f, 0.0f),
                new Vector2(0.0f, 6.0f),
                new Vector2(0.0f, -6.0f)
            },
            20.0f,
            profile);
        SupportModifierDefinition modifier = SupportModifierDefinition.CreateNewBrace(
            0,
            CreateNearestNeighborBraceSettings(),
            CreateSupportIds(supports),
            0);
        SupportBracingEvaluationResult evaluation = SupportBracingPlanner.EvaluateBrace(supports, modifier);
        HashSet<int> centralNeighborIndices = new HashSet<int>();

        for (int i = supports.Count; i < evaluation.SupportEntities.Count; i++)
        {
            SupportEntity member = evaluation.SupportEntities[i];
            int startIndex = FindSupportIndexByXy(supports, member.BasePosition);
            int endIndex = FindSupportIndexByXy(supports, member.TipPosition);

            if (startIndex == 0)
            {
                centralNeighborIndices.Add(endIndex);
            }
            else if (endIndex == 0)
            {
                centralNeighborIndices.Add(startIndex);
            }
        }

        if (centralNeighborIndices.Count != 3)
        {
            throw new InvalidOperationException($"Expected the central support to be limited to three neighbors but found {centralNeighborIndices.Count}.");
        }
    }

    /// <summary>
    /// Validates that replacing one selected pair preserves old selected-to-unselected and unselected pairs.
    /// </summary>
    private static void ValidateBracePairExclusionsPreserveMixedSelectionBracing()
    {
        Guid supportLayerGroupId = Guid.NewGuid();
        SupportProfile profile = SupportDefaults.CreateProfile();
        List<SupportEntity> supports = CreateSupportsForGroup(
            supportLayerGroupId,
            new List<Vector2>
            {
                new Vector2(0.0f, 0.0f),
                new Vector2(6.0f, 0.0f),
                new Vector2(12.0f, 0.0f)
            },
            30.0f,
            profile);
        IReadOnlyList<Guid> allTargetIds = CreateSupportIds(supports);
        SupportBraceModifierSettings oldSettings = new SupportBraceModifierSettings(10.0f, 70.0f, 50.0f, 0.4f);
        SupportBraceModifierSettings newSettings = new SupportBraceModifierSettings(10.0f, 70.0f, 50.0f, 0.9f);
        SupportModifierDefinition oldModifier = new SupportModifierDefinition(
            Guid.NewGuid(),
            SupportModifierKind.Brace,
            true,
            0,
            null,
            oldSettings,
            null,
            allTargetIds,
            null,
            0,
            new List<SupportBracePair> { new SupportBracePair(supports[0].Id, supports[2].Id) });
        SupportModifierDefinition newModifier = SupportModifierDefinition.CreateNewBrace(
            1,
            newSettings,
            new List<Guid> { supports[0].Id, supports[2].Id },
            0);
        IReadOnlyList<SupportEntity> result = SupportModifierPipeline.ApplyModifiers(
            supports,
            new List<SupportModifierDefinition> { oldModifier, newModifier });
        int oldSelectedPairMemberCount = 0;
        int oldFirstMixedPairMemberCount = 0;
        int oldSecondMixedPairMemberCount = 0;
        int newSelectedPairMemberCount = 0;
        int newMixedPairMemberCount = 0;

        for (int i = supports.Count; i < result.Count; i++)
        {
            SupportEntity member = result[i];

            if (member.Style is not BraceMemberSupportStyle braceStyle)
            {
                continue;
            }

            int firstSupportIndex = FindSupportIndexByXy(supports, member.BasePosition);
            int secondSupportIndex = FindSupportIndexByXy(supports, member.TipPosition);
            int minimumIndex = Math.Min(firstSupportIndex, secondSupportIndex);
            int maximumIndex = Math.Max(firstSupportIndex, secondSupportIndex);
            bool usesOldSettings = MathF.Abs(braceStyle.Diameter - 0.4f) <= 0.0001f;
            bool usesNewSettings = MathF.Abs(braceStyle.Diameter - 0.9f) <= 0.0001f;

            if (minimumIndex == 0 && maximumIndex == 2)
            {
                oldSelectedPairMemberCount += usesOldSettings ? 1 : 0;
                newSelectedPairMemberCount += usesNewSettings ? 1 : 0;
            }
            else if (minimumIndex == 0 && maximumIndex == 1)
            {
                oldFirstMixedPairMemberCount += usesOldSettings ? 1 : 0;
                newMixedPairMemberCount += usesNewSettings ? 1 : 0;
            }
            else if (minimumIndex == 1 && maximumIndex == 2)
            {
                oldSecondMixedPairMemberCount += usesOldSettings ? 1 : 0;
                newMixedPairMemberCount += usesNewSettings ? 1 : 0;
            }
        }

        if (oldSelectedPairMemberCount != 0
            || newSelectedPairMemberCount == 0
            || oldFirstMixedPairMemberCount == 0
            || oldSecondMixedPairMemberCount == 0
            || newMixedPairMemberCount != 0)
        {
            throw new InvalidOperationException("Expected only the selected-selected pair to use the replacement Brace settings.");
        }
    }

    /// <summary>
    /// Validates that compact exclusion batches suppress only pairs internal to the later selection.
    /// </summary>
    private static void ValidateBraceExclusionBatchesPreserveMixedSelectionBracing()
    {
        Guid supportLayerGroupId = Guid.NewGuid();
        List<SupportEntity> supports = CreateSupportsForGroup(
            supportLayerGroupId,
            new List<Vector2>
            {
                new Vector2(0.0f, 0.0f),
                new Vector2(6.0f, 0.0f),
                new Vector2(12.0f, 0.0f)
            },
            30.0f,
            SupportDefaults.CreateProfile());
        SupportBraceModifierSettings oldSettings = new SupportBraceModifierSettings(10.0f, 70.0f, 50.0f, 0.4f);
        SupportBraceModifierSettings newSettings = new SupportBraceModifierSettings(10.0f, 70.0f, 50.0f, 0.9f);
        List<Guid> selectedIds = new List<Guid> { supports[0].Id, supports[2].Id };
        SupportModifierDefinition oldModifier = new SupportModifierDefinition(
            Guid.NewGuid(),
            SupportModifierKind.Brace,
            true,
            0,
            null,
            oldSettings,
            null,
            CreateSupportIds(supports),
            null,
            0,
            null,
            new List<SupportModifierTargetBatch> { new SupportModifierTargetBatch(selectedIds) });
        SupportModifierDefinition newModifier = SupportModifierDefinition.CreateNewBrace(1, newSettings, selectedIds, 0);
        IReadOnlyList<SupportEntity> result = SupportModifierPipeline.ApplyModifiers(
            supports,
            new List<SupportModifierDefinition> { oldModifier, newModifier });
        int oldMixedPairCount = 0;
        int oldSelectedPairCount = 0;
        int newSelectedPairCount = 0;

        for (int i = supports.Count; i < result.Count; i++)
        {
            SupportEntity member = result[i];

            if (member.Style is not BraceMemberSupportStyle braceStyle)
            {
                continue;
            }

            int firstSupportIndex = FindSupportIndexByXy(supports, member.BasePosition);
            int secondSupportIndex = FindSupportIndexByXy(supports, member.TipPosition);
            int minimumIndex = Math.Min(firstSupportIndex, secondSupportIndex);
            int maximumIndex = Math.Max(firstSupportIndex, secondSupportIndex);
            bool usesOldSettings = MathF.Abs(braceStyle.Diameter - 0.4f) <= 0.0001f;
            bool usesNewSettings = MathF.Abs(braceStyle.Diameter - 0.9f) <= 0.0001f;

            if (minimumIndex == 0 && maximumIndex == 2)
            {
                oldSelectedPairCount += usesOldSettings ? 1 : 0;
                newSelectedPairCount += usesNewSettings ? 1 : 0;
            }
            else if (usesOldSettings)
            {
                oldMixedPairCount++;
            }
        }

        if (oldSelectedPairCount != 0 || newSelectedPairCount == 0 || oldMixedPairCount == 0)
        {
            throw new InvalidOperationException("Expected compact exclusion batches to replace only selected-selected bracing.");
        }
    }

    /// <summary>
    /// Validates that final output and requested diagnostics are produced by one pipeline evaluation.
    /// </summary>
    private static void ValidateModifierPipelineCapturesBracingDiagnostics()
    {
        Guid supportLayerGroupId = Guid.NewGuid();
        List<SupportEntity> supports = CreateSupportsForGroup(
            supportLayerGroupId,
            new List<Vector2> { Vector2.Zero, new Vector2(6.0f, 0.0f) },
            30.0f,
            SupportDefaults.CreateProfile());
        SupportModifierDefinition modifier = SupportModifierDefinition.CreateNewBrace(
            0,
            new SupportBraceModifierSettings(10.0f, 70.0f, 50.0f, 0.6f),
            CreateSupportIds(supports),
            0);
        SupportBracingEvaluationResult directResult = SupportBracingPlanner.EvaluateBrace(supports, modifier);
        SupportModifierPipelineEvaluation pipelineResult = SupportModifierPipeline.EvaluateModifiers(
            supports,
            new List<SupportModifierDefinition> { modifier },
            modifier.Id);
        SupportBracingEvaluationResult capturedResult = pipelineResult.CapturedBracingResult
            ?? throw new InvalidOperationException("Expected requested bracing diagnostics to be captured.");

        if (pipelineResult.SupportEntities.Count != directResult.SupportEntities.Count
            || capturedResult.AddedMemberCount != directResult.AddedMemberCount
            || capturedResult.TargetSupportCount != directResult.TargetSupportCount
            || capturedResult.RejectedCandidateCount != directResult.RejectedCandidateCount)
        {
            throw new InvalidOperationException("Expected captured pipeline diagnostics to match direct Brace evaluation.");
        }
    }

    /// <summary>
    /// Validates that reapplying buttressing replaces selected targets without disturbing unselected targets.
    /// </summary>
    private static void ValidateButtressReapplyPreservesUnselectedTargets()
    {
        Guid firstId = Guid.NewGuid();
        Guid selectedId = Guid.NewGuid();
        Guid thirdId = Guid.NewGuid();
        SupportButtressModifierSettings oldSettings = new SupportButtressModifierSettings(10.0f, 4.0f);
        SupportButtressModifierSettings newSettings = new SupportButtressModifierSettings(20.0f, 8.0f);
        SupportModifierDefinition firstModifier = SupportModifierDefinition.CreateNewButtress(
            0,
            oldSettings,
            new List<Guid> { firstId, selectedId },
            0);
        SupportModifierDefinition secondModifier = SupportModifierDefinition.CreateNewButtress(
            1,
            oldSettings,
            new List<Guid> { selectedId, thirdId },
            0);
        Guid toolSessionId = Guid.NewGuid();
        IReadOnlyList<SupportModifierDefinition> result = SupportReinforcementReconciler.ReapplyButtressTargets(
            new List<SupportModifierDefinition> { firstModifier, secondModifier },
            new List<Guid> { selectedId },
            newSettings,
            0,
            toolSessionId);

        if (result.Count != 3
            || result[0].Id != firstModifier.Id
            || result[0].TargetSupportIds.Count != 1
            || result[0].TargetSupportIds[0] != firstId
            || result[1].Id != secondModifier.Id
            || result[1].TargetSupportIds.Count != 1
            || result[1].TargetSupportIds[0] != thirdId
            || result[2].TargetSupportIds.Count != 1
            || result[2].TargetSupportIds[0] != selectedId
            || result[2].ToolSessionId != toolSessionId
            || result[2].ButtressSettings == null
            || MathF.Abs(result[2].ButtressSettings.ButtressSpacing - newSettings.ButtressSpacing) > 0.0001f)
        {
            throw new InvalidOperationException("Expected selected buttressing to be replaced while unselected targets retain their existing modifiers.");
        }
    }
    /// <summary>
    /// Validates that removing one visible tool session removes and restores all of its internal actions together.
    /// </summary>
    private static void ValidateModifierSessionRemovalRemovesEveryAction()
    {
        CadDocument document = new CadDocument();
        MeshEntity mesh = CreateSingleTriangleMesh(
            Vector3.Zero,
            new Vector3(20.0f, 0.0f, 0.0f),
            new Vector3(0.0f, 20.0f, 0.0f),
            Transform3DData.Identity);
        document.AddEntity(mesh);
        SupportLayerGroup supportLayerGroup = new SupportLayerGroup(mesh.Id, "Session Removal Supports");
        document.AddSupportLayerGroup(supportLayerGroup);
        List<SupportEntity> sourceSupports = CreateSupportsForGroup(
            supportLayerGroup.Id,
            new List<Vector2> { Vector2.Zero, new Vector2(6.0f, 0.0f) },
            60.0f,
            SupportDefaults.CreateProfile());
        Guid toolSessionId = Guid.NewGuid();
        SupportModifierDefinition braceAction = new SupportModifierDefinition(
            Guid.NewGuid(),
            SupportModifierKind.Brace,
            true,
            0,
            null,
            SupportBraceModifierSettings.CreateDefault(),
            null,
            CreateSupportIds(sourceSupports),
            null,
            supportLayerGroup.SourceGeneratorRevision,
            null,
            null,
            toolSessionId);
        SupportModifierDefinition buttressAction = new SupportModifierDefinition(
            Guid.NewGuid(),
            SupportModifierKind.Buttress,
            true,
            1,
            null,
            null,
            SupportButtressModifierSettings.CreateDefault(),
            new List<Guid> { sourceSupports[0].Id },
            null,
            supportLayerGroup.SourceGeneratorRevision,
            null,
            null,
            toolSessionId);
        Guid laterToolSessionId = Guid.NewGuid();
        SupportModifierDefinition laterBraceAction = new SupportModifierDefinition(
            Guid.NewGuid(),
            SupportModifierKind.Brace,
            false,
            2,
            null,
            SupportBraceModifierSettings.CreateDefault(),
            null,
            CreateSupportIds(sourceSupports),
            null,
            supportLayerGroup.SourceGeneratorRevision,
            null,
            null,
            laterToolSessionId);
        List<SupportModifierDefinition> actions = new List<SupportModifierDefinition> { braceAction, buttressAction, laterBraceAction };
        supportLayerGroup.SetSupportModifiers(actions);
        IReadOnlyList<SupportEntity> evaluatedSupports = SupportModifierPipeline.ApplyModifiers(sourceSupports, actions);

        for (int i = 0; i < evaluatedSupports.Count; i++)
        {
            document.AddEntity(evaluatedSupports[i]);
        }

        LayerPanelViewModel layerPanelViewModel = new LayerPanelViewModel(document);
        LayerTreeItemViewModel supportGroupRow = layerPanelViewModel.ModelLayers[0].Children[0];

        if (supportGroupRow.Children.Count != 2
            || supportGroupRow.Children[0].Id != toolSessionId
            || supportGroupRow.Children[1].Id != laterToolSessionId)
        {
            throw new InvalidOperationException("Expected one Layer Panel row per tool launch, with mixed actions grouped into their owning session.");
        }

        RemoveSupportModifierCommand command = new RemoveSupportModifierCommand(document, supportLayerGroup, toolSessionId);
        command.Execute();

        if (supportLayerGroup.SupportModifiers.Count != 1
            || supportLayerGroup.SupportModifiers[0].ToolSessionId != laterToolSessionId)
        {
            throw new InvalidOperationException("Expected session removal to remove every owned action without disturbing a later tool launch.");
        }

        command.Undo();

        if (supportLayerGroup.SupportModifiers.Count != 3
            || supportLayerGroup.SupportModifiers[0].ToolSessionId != toolSessionId
            || supportLayerGroup.SupportModifiers[1].ToolSessionId != toolSessionId
            || supportLayerGroup.SupportModifiers[2].ToolSessionId != laterToolSessionId)
        {
            throw new InvalidOperationException("Expected undo to restore the complete mixed bracing tool session.");
        }
    }
    /// <summary>
    /// Validates that output replacement adds and removes only generated members while retaining source instances.
    /// </summary>
    private static void ValidateSupportOutputReplacementRetainsUnchangedEntities()
    {
        CadDocument document = new CadDocument();
        MeshEntity mesh = CreateSingleTriangleMesh(
            Vector3.Zero,
            new Vector3(20.0f, 0.0f, 0.0f),
            new Vector3(0.0f, 20.0f, 0.0f),
            Transform3DData.Identity);
        document.AddEntity(mesh);
        SupportLayerGroup supportLayerGroup = new SupportLayerGroup(mesh.Id, "Retained Sources");
        document.AddSupportLayerGroup(supportLayerGroup);
        List<SupportEntity> sourceSupports = CreateSupportsForGroup(
            supportLayerGroup.Id,
            new List<Vector2> { Vector2.Zero, new Vector2(6.0f, 0.0f) },
            30.0f,
            SupportDefaults.CreateProfile());

        for (int i = 0; i < sourceSupports.Count; i++)
        {
            document.AddEntity(sourceSupports[i]);
        }

        SupportModifierDefinition modifier = SupportModifierDefinition.CreateNewBrace(
            0,
            new SupportBraceModifierSettings(10.0f, 70.0f, 50.0f, 0.6f),
            CreateSupportIds(sourceSupports),
            supportLayerGroup.SourceGeneratorRevision);
        List<SupportModifierDefinition> newModifiers = new List<SupportModifierDefinition> { modifier };
        IReadOnlyList<SupportEntity> newOutput = SupportModifierPipeline.ApplyModifiers(sourceSupports, newModifiers);
        ReplaceSupportLayerOutputAndModifiersCommand command = new ReplaceSupportLayerOutputAndModifiersCommand(
            document,
            supportLayerGroup,
            sourceSupports,
            newOutput,
            Array.Empty<SupportModifierDefinition>(),
            newModifiers,
            "Retain Source Supports");
        int addedCount = 0;
        int removedCount = 0;
        int batchCompletionCount = 0;
        document.EntityBatchUpdateCompleted += (sender, eventArgs) =>
        {
            _ = sender;
            _ = eventArgs;

            if (document.IsEntityBatchUpdateActive)
            {
                throw new InvalidOperationException("Expected the completed event after the entity batch ended.");
            }

            batchCompletionCount++;
        };
        document.EntitiesChanged += (sender, eventArgs) =>
        {
            _ = sender;
            addedCount += eventArgs.NewItems?.Count ?? 0;
            removedCount += eventArgs.OldItems?.Count ?? 0;
        };

        command.Execute();
        int generatedCount = newOutput.Count - sourceSupports.Count;

        if (removedCount != 0 || addedCount != generatedCount)
        {
            throw new InvalidOperationException("Expected execute to add only generated brace members.");
        }

        if (batchCompletionCount != 1)
        {
            throw new InvalidOperationException("Expected execute to complete one grouped entity update.");
        }

        IReadOnlyList<SupportEntity> executedOutput = document.GetSupportEntitiesForGroup(supportLayerGroup.Id);
        HashSet<SupportEntity> executedSupportSet = new HashSet<SupportEntity>(executedOutput);

        for (int i = 0; i < sourceSupports.Count; i++)
        {
            if (!executedSupportSet.Contains(sourceSupports[i]))
            {
                throw new InvalidOperationException("Expected execute to retain every source support instance.");
            }
        }

        addedCount = 0;
        removedCount = 0;
        command.Undo();

        if (addedCount != 0 || removedCount != generatedCount)
        {
            throw new InvalidOperationException("Expected undo to remove only generated brace members.");
        }

        if (batchCompletionCount != 2)
        {
            throw new InvalidOperationException("Expected undo to complete one additional grouped entity update.");
        }
    }

    /// <summary>
    /// Validates that deterministic reinforcement replay keeps existing entity and mesh identities.
    /// </summary>
    private static void ValidateEquivalentRegeneratedReinforcementRetainsExistingEntities()
    {
        Guid supportLayerGroupId = Guid.NewGuid();
        List<SupportEntity> sourceSupports = CreateSupportsForGroup(
            supportLayerGroupId,
            new List<Vector2> { Vector2.Zero, new Vector2(10.0f, 0.0f) },
            30.0f,
            SupportDefaults.CreateProfile());
        SupportModifierDefinition modifier = SupportModifierDefinition.CreateNewButtress(
            0,
            new SupportButtressModifierSettings(0.0f, 8.0f),
            CreateSupportIds(sourceSupports),
            0);
        IReadOnlyList<SupportEntity> firstOutput = SupportModifierPipeline.ApplyModifiers(
            sourceSupports,
            new[] { modifier });
        IReadOnlyList<SupportEntity> replayedOutput = SupportModifierPipeline.ApplyModifiers(
            sourceSupports,
            new[] { modifier });
        IReadOnlyList<SupportEntity> reconciledOutput = SupportOutputReferenceReconciler.ReuseEquivalentGeneratedSupports(
            firstOutput,
            replayedOutput);
        int reusedGeneratedCount = 0;

        for (int i = sourceSupports.Count; i < reconciledOutput.Count; i++)
        {
            if (ReferenceEquals(firstOutput[i], replayedOutput[i]))
            {
                throw new InvalidOperationException("Expected reinforcement replay to initially create fresh generated instances.");
            }

            if (ReferenceEquals(firstOutput[i], reconciledOutput[i]))
            {
                reusedGeneratedCount++;
            }
        }

        if (reusedGeneratedCount != firstOutput.Count - sourceSupports.Count)
        {
            throw new InvalidOperationException("Expected every equivalent generated reinforcement entity to be retained.");
        }
    }
    /// <summary>
    /// Validates that clustered supports are excluded from both Brace and Buttress target populations.
    /// </summary>
    private static void ValidateReinforcementExcludesClusteredSupports()
    {
        Guid supportLayerGroupId = Guid.NewGuid();
        SupportProfile profile = SupportDefaults.CreateProfile();
        List<SupportEntity> individualSupports = CreateSupportsForGroup(
            supportLayerGroupId,
            new List<Vector2>
            {
                new Vector2(0.0f, 0.0f),
                new Vector2(3.0f, 0.0f),
                new Vector2(6.0f, 0.0f)
            },
            50.0f,
            profile);
        SupportEntity clusteredSupport = CreateSupportWithStyle(
            individualSupports[1],
            new ClusteredSupportStyle(
                SupportDefaults.DefaultStemBottomDiameter,
                SupportDefaults.DefaultStemTopDiameter,
                SupportDefaults.DefaultStemTopDiameter));
        List<SupportEntity> supports = new List<SupportEntity>
        {
            individualSupports[0],
            clusteredSupport,
            individualSupports[2]
        };
        IReadOnlyList<Guid> targetIds = CreateSupportIds(supports);
        SupportModifierDefinition braceModifier = SupportModifierDefinition.CreateNewBrace(
            0,
            CreateNearestNeighborBraceSettings(),
            targetIds,
            0);
        SupportBracingEvaluationResult braceEvaluation = SupportBracingPlanner.EvaluateBrace(supports, braceModifier);

        if (braceEvaluation.TargetSupportCount != 2 || braceEvaluation.AddedMemberCount != 2)
        {
            throw new InvalidOperationException("Expected Brace evaluation to ignore the captured clustered support.");
        }

        SupportModifierDefinition buttressModifier = SupportModifierDefinition.CreateNewButtress(
            0,
            new SupportButtressModifierSettings(0.0f, 8.0f),
            targetIds,
            0);
        SupportBracingEvaluationResult buttressEvaluation = SupportBracingPlanner.EvaluateButtress(supports, buttressModifier);

        if (buttressEvaluation.TargetSupportCount != 2
            || CountSupportsWithStyle(buttressEvaluation.SupportEntities, SupportStyleKind.Buttress) != 4)
        {
            throw new InvalidOperationException("Expected Buttress evaluation to ignore the captured clustered support and create two buttresses per individual target.");
        }
    }

    /// <summary>
    /// Validates that a Brace modifier replayed after clustering cannot reinforce clustered outputs.
    /// </summary>
    private static void ValidateClusterBeforeBraceExcludesClusteredOutputs()
    {
        Guid supportLayerGroupId = Guid.NewGuid();
        SupportProfile profile = SupportDefaults.CreateProfile();
        List<SupportEntity> supports = CreateSupportsForGroup(
            supportLayerGroupId,
            new List<Vector2>
            {
                new Vector2(0.0f, 0.0f),
                new Vector2(2.0f, 0.0f),
                new Vector2(10.0f, 0.0f)
            },
            20.0f,
            profile);
        SupportModifierDefinition clusterModifier = SupportModifierDefinition.CreateNew(
            SupportModifierKind.Cluster,
            0,
            CreateSmokeClusterSettings(3.0f, 2),
            new List<Guid> { supports[0].Id, supports[1].Id },
            0);
        SupportModifierDefinition braceModifier = SupportModifierDefinition.CreateNewBrace(
            1,
            CreateNearestNeighborBraceSettings(),
            CreateSupportIds(supports),
            0);
        IReadOnlyList<SupportEntity> result = SupportModifierPipeline.ApplyModifiers(
            supports,
            new List<SupportModifierDefinition> { clusterModifier, braceModifier });

        if (CountSupportsWithStyle(result, SupportStyleKind.Clustered) != 2
            || CountSupportsWithStyle(result, SupportStyleKind.BraceMember) != 0)
        {
            throw new InvalidOperationException("Expected clustered outputs to remain unbraced during modifier replay.");
        }
    }

    /// <summary>
    /// Validates durable target pruning and minimum-target modifier removal after clustering.
    /// </summary>
    private static void ValidateClusterReconciliationRemovesReinforcementTargets()
    {
        Guid firstId = Guid.NewGuid();
        Guid secondId = Guid.NewGuid();
        Guid thirdId = Guid.NewGuid();
        SupportModifierDefinition braceModifier = SupportModifierDefinition.CreateNewBrace(
            0,
            CreateNearestNeighborBraceSettings(),
            new List<Guid> { firstId, secondId, thirdId },
            0);
        SupportModifierDefinition buttressModifier = SupportModifierDefinition.CreateNewButtress(
            1,
            new SupportButtressModifierSettings(0.0f, 8.0f),
            new List<Guid> { firstId },
            0);
        SupportModifierDefinition clusterModifier = SupportModifierDefinition.CreateNew(
            SupportModifierKind.Cluster,
            2,
            CreateSmokeClusterSettings(3.0f, 2),
            new List<Guid> { firstId, secondId },
            0);
        SupportReinforcementReconciliationResult reconciliation = SupportReinforcementReconciler.RemoveClusteredTargets(
            new List<SupportModifierDefinition> { braceModifier, buttressModifier, clusterModifier },
            new List<Guid> { firstId });

        if (reconciliation.RemovedTargetCount != 2
            || reconciliation.RemovedModifierCount != 1
            || reconciliation.AffectedModifierCount != 2
            || reconciliation.Modifiers.Count != 2)
        {
            throw new InvalidOperationException("Expected reconciliation to prune two targets and remove the empty Buttress modifier.");
        }

        SupportModifierDefinition remainingBrace = reconciliation.Modifiers[0];

        if (remainingBrace.Id != braceModifier.Id
            || remainingBrace.TargetSupportIds.Count != 2
            || ContainsSupportId(remainingBrace.TargetSupportIds, firstId)
            || reconciliation.Modifiers[1].Id != clusterModifier.Id
            || reconciliation.Modifiers[1].Order != 1)
        {
            throw new InvalidOperationException("Expected reconciliation to preserve surviving modifier identity, settings, and relative order.");
        }
    }

    /// <summary>
    /// Validates that the atomic replacement command restores reinforcement geometry and modifiers on Undo.
    /// </summary>
    private static void ValidateClusterReinforcementCleanupUndo()
    {
        CadDocument document = new CadDocument();
        MeshEntity mesh = CreateSingleTriangleMesh(
            new Vector3(0.0f, 0.0f, 10.0f),
            new Vector3(10.0f, 0.0f, 10.0f),
            new Vector3(0.0f, 10.0f, 10.0f),
            Transform3DData.Identity);
        SupportLayerGroup supportLayerGroup = new SupportLayerGroup(mesh.Id, "Undoable Reinforcement Cleanup");
        SupportProfile profile = SupportDefaults.CreateProfile();
        List<SupportEntity> sourceSupports = CreateSupportsForGroup(
            supportLayerGroup.Id,
            new List<Vector2>
            {
                new Vector2(0.0f, 0.0f),
                new Vector2(2.0f, 0.0f),
                new Vector2(10.0f, 0.0f)
            },
            20.0f,
            profile);
        SupportModifierDefinition braceModifier = SupportModifierDefinition.CreateNewBrace(
            0,
            CreateNearestNeighborBraceSettings(),
            CreateSupportIds(sourceSupports),
            supportLayerGroup.SourceGeneratorRevision);
        List<SupportModifierDefinition> oldModifiers = new List<SupportModifierDefinition> { braceModifier };
        IReadOnlyList<SupportEntity> oldOutput = SupportModifierPipeline.ApplyModifiers(sourceSupports, oldModifiers);
        SupportModifierDefinition clusterModifier = SupportModifierDefinition.CreateNew(
            SupportModifierKind.Cluster,
            1,
            CreateSmokeClusterSettings(3.0f, 2),
            new List<Guid> { sourceSupports[0].Id, sourceSupports[1].Id },
            supportLayerGroup.SourceGeneratorRevision);
        List<SupportModifierDefinition> provisionalModifiers = new List<SupportModifierDefinition>
        {
            braceModifier,
            clusterModifier
        };
        IReadOnlyList<SupportEntity> provisionalOutput = SupportModifierPipeline.ApplyModifiers(sourceSupports, provisionalModifiers);
        HashSet<Guid> clusteredIds = new HashSet<Guid>();

        for (int i = 0; i < provisionalOutput.Count; i++)
        {
            if (provisionalOutput[i].Style.Kind == SupportStyleKind.Clustered)
            {
                clusteredIds.Add(provisionalOutput[i].Id);
            }
        }

        SupportReinforcementReconciliationResult reconciliation = SupportReinforcementReconciler.RemoveClusteredTargets(
            provisionalModifiers,
            clusteredIds);
        IReadOnlyList<SupportEntity> newOutput = SupportModifierPipeline.ApplyModifiers(sourceSupports, reconciliation.Modifiers);

        document.AddEntity(mesh);
        document.AddSupportLayerGroup(supportLayerGroup);
        supportLayerGroup.SetSupportModifiers(oldModifiers);

        for (int i = 0; i < oldOutput.Count; i++)
        {
            document.AddEntity(oldOutput[i]);
        }

        ReplaceSupportLayerOutputAndModifiersCommand command = new ReplaceSupportLayerOutputAndModifiersCommand(
            document,
            supportLayerGroup,
            oldOutput,
            newOutput,
            oldModifiers,
            reconciliation.Modifiers,
            "Test Cluster Reinforcement Cleanup");
        command.Execute();

        if (supportLayerGroup.SupportModifiers.Count != 1
            || supportLayerGroup.SupportModifiers[0].Kind != SupportModifierKind.Cluster
            || CountSupportsWithStyle(document.GetSupportEntitiesForGroup(supportLayerGroup.Id), SupportStyleKind.BraceMember) != 0)
        {
            throw new InvalidOperationException("Expected cluster execution to replace the old reinforcement modifier and geometry.");
        }

        command.Undo();

        if (supportLayerGroup.SupportModifiers.Count != 1
            || supportLayerGroup.SupportModifiers[0].Kind != SupportModifierKind.Brace
            || CountSupportsWithStyle(document.GetSupportEntitiesForGroup(supportLayerGroup.Id), SupportStyleKind.BraceMember) == 0)
        {
            throw new InvalidOperationException("Expected Undo to restore the original reinforcement modifier and geometry.");
        }
    }

    /// <summary>
    /// Validates that the angled head centerline cannot cross another model face before the intended contact.
    /// </summary>
    private static void ValidateSupportPlacementRejectsCrossingAngledHead()
    {
        SupportProfile profile = CreateAngledProfile(45.0f);
        MeshEntity mesh = CreateHeadBlockingPanelMesh();
        SupportPlacementPlan placementPlan;

        if (SupportPlacementPlanner.TryCreatePlacement(mesh, new Vector3(0.0f, 0.0f, 10.0f), Vector3.UnitX, profile, out placementPlan))
        {
            throw new InvalidOperationException("Expected the placement planner to reject an angled head that crosses the model.");
        }
    }

    /// <summary>
    /// Validates that vertical support projection exposes the triangle normal needed by angled heads.
    /// </summary>
    private static void ValidateVerticalProjectionReturnsTriangleNormal()
    {
        MeshEntity mesh = CreateSingleTriangleMesh(
            new Vector3(0.0f, 0.0f, 2.0f),
            new Vector3(1.0f, 0.0f, 2.0f),
            new Vector3(0.0f, 1.0f, 2.0f),
            Transform3DData.Identity);
        MeshProjectionHit hit;

        if (!MeshVerticalProjection.TryProjectToMesh(mesh, new Vector3(0.25f, 0.25f, 0.0f), out hit))
        {
            throw new InvalidOperationException("Expected the vertical guide point to hit the test triangle.");
        }

        ValidateVectorNear(new Vector3(0.25f, 0.25f, 2.0f), hit.Point, 0.0001f, "Expected projection point to land on the triangle plane.");
        ValidateVectorNear(Vector3.UnitZ, hit.Normal, 0.0001f, "Expected projection normal to match the triangle normal.");
    }

    /// <summary>
    /// Validates vertical tube-like faces where the vertical projection line overlaps the triangle plane.
    /// </summary>
    private static void ValidateVerticalProjectionHandlesVerticalSideFaces()
    {
        MeshEntity mesh = CreateSideContactPanelMesh();
        MeshProjectionHit hit;

        if (!MeshVerticalProjection.TryProjectToMesh(mesh, new Vector3(0.0f, 5.0f, 10.0f), out hit))
        {
            throw new InvalidOperationException("Expected the vertical guide point to hit the vertical side face.");
        }

        ValidateVectorNear(new Vector3(0.0f, 5.0f, 10.0f), hit.Point, 0.0001f, "Expected projection to preserve the guide height on a vertical face.");
        ValidateVectorNear(Vector3.UnitX, hit.Normal, 0.0001f, "Expected projection normal to match the side face normal.");
    }

    /// <summary>
    /// Validates that generated supports can snap from a nearby guide point back onto a vertical wall.
    /// </summary>
    private static void ValidateSupportProjectionFallsBackToNearbyVerticalFace()
    {
        SupportProfile profile = CreateBranchProfile(45.0f, 6.0f, 4.0f);
        MeshEntity mesh = CreateSideContactPanelMesh();
        float fallbackRadius = MeshVerticalProjection.CalculateSupportFallbackRadius(5.0f, profile);
        MeshProjectionHit hit;
        SupportPlacementPlan placementPlan;

        if (!MeshVerticalProjection.TryProjectSupportToMesh(mesh, new Vector3(0.2f, 5.0f, 10.0f), profile, fallbackRadius, out hit, out placementPlan))
        {
            throw new InvalidOperationException("Expected fallback projection to find the nearby vertical face.");
        }

        ValidateVectorNear(new Vector3(0.0f, 5.0f, 10.0f), hit.Point, 0.0001f, "Expected fallback projection to snap to the closest side face point.");
    }

    /// <summary>
    /// Validates that neighboring guide points on a vertical wall project consistently.
    /// </summary>
    private static void ValidateSupportProjectionFallbackHandlesNeighboringVerticalFacePoints()
    {
        SupportProfile profile = CreateBranchProfile(45.0f, 6.0f, 4.0f);
        MeshEntity mesh = CreateSideContactPanelMesh();
        float fallbackRadius = MeshVerticalProjection.CalculateSupportFallbackRadius(5.0f, profile);
        MeshProjectionHit firstHit;
        MeshProjectionHit secondHit;
        SupportPlacementPlan firstPlacementPlan;
        SupportPlacementPlan secondPlacementPlan;

        if (!MeshVerticalProjection.TryProjectSupportToMesh(mesh, new Vector3(0.2f, 4.0f, 10.0f), profile, fallbackRadius, out firstHit, out firstPlacementPlan)
            || !MeshVerticalProjection.TryProjectSupportToMesh(mesh, new Vector3(0.2f, 6.0f, 10.0f), profile, fallbackRadius, out secondHit, out secondPlacementPlan))
        {
            throw new InvalidOperationException("Expected neighboring vertical-wall guide points to both produce supportable hits.");
        }

        ValidateVectorNear(new Vector3(0.0f, 4.0f, 10.0f), firstHit.Point, 0.0001f, "Expected the first neighboring point to snap to the side face.");
        ValidateVectorNear(new Vector3(0.0f, 6.0f, 10.0f), secondHit.Point, 0.0001f, "Expected the second neighboring point to snap to the side face.");
    }

    /// <summary>
    /// Validates that the fallback radius prevents supports from jumping to unrelated geometry.
    /// </summary>
    private static void ValidateSupportProjectionFallbackRejectsDistantVerticalFace()
    {
        SupportProfile profile = CreateBranchProfile(45.0f, 6.0f, 4.0f);
        MeshEntity mesh = CreateSideContactPanelMesh();
        float fallbackRadius = MeshVerticalProjection.CalculateSupportFallbackRadius(1.0f, profile);
        MeshProjectionHit hit;
        SupportPlacementPlan placementPlan;

        if (MeshVerticalProjection.TryProjectSupportToMesh(mesh, new Vector3(2.0f, 5.0f, 10.0f), profile, fallbackRadius, out hit, out placementPlan))
        {
            throw new InvalidOperationException("Expected fallback projection to reject a distant vertical face.");
        }
    }

    /// <summary>
    /// Validates that support projection searches from the build plate instead of choosing the nearest Z hit.
    /// </summary>
    private static void ValidateVerticalSupportProjectionChoosesFirstExteriorHit()
    {
        SupportProfile profile = SupportDefaults.CreateProfile();
        MeshEntity mesh = CreateStackedDownwardHorizontalFaces();
        MeshProjectionHit hit;
        SupportPlacementPlan placementPlan;

        if (!MeshVerticalProjection.TryProjectSupportToMesh(mesh, new Vector3(0.25f, 0.25f, 13.0f), profile, out hit, out placementPlan))
        {
            throw new InvalidOperationException("Expected the stacked mesh to produce a supportable lower exterior hit.");
        }

        ValidateVectorNear(new Vector3(0.25f, 0.25f, 10.0f), hit.Point, 0.0001f, "Expected support projection to choose the first exterior hit above the build plate.");
    }

    /// <summary>
    /// Validates that transform regeneration uses the same supportable projection as initial support generation.
    /// </summary>
    private static void ValidateTransformRegenerationUsesSupportableProjection()
    {
        CadDocument document = new CadDocument();
        MeshEntity mesh = CreateStackedDownwardHorizontalFaces();
        SupportLayerGroup supportLayerGroup = new SupportLayerGroup(mesh.Id, "Line Supports");
        supportLayerGroup.SetLineSupportSettings(new LineSupportSettings(
            new List<Vector3>
            {
                new Vector3(0.25f, 0.25f, 13.0f),
                new Vector3(0.75f, 0.25f, 13.0f)
            },
            10.0f));

        document.AddEntity(mesh);
        document.AddSupportLayerGroup(supportLayerGroup);

        IReadOnlyList<SupportGroupRegeneration> regenerations = SupportGroupTransformRegenerator.CreateRegenerations(
            document,
            mesh,
            Transform3DData.Identity,
            Transform3DData.Identity);

        if (regenerations.Count != 1 || regenerations[0].NewSupportEntities.Count == 0)
        {
            throw new InvalidOperationException("Expected transform regeneration to recreate line supports.");
        }

        for (int i = 0; i < regenerations[0].NewSupportEntities.Count; i++)
        {
            if (MathF.Abs(regenerations[0].NewSupportEntities[i].TipPosition.Z - 10.0f) > 0.0001f)
            {
                throw new InvalidOperationException("Expected regenerated supports to use the lower exterior projection hit.");
            }
        }
    }

    /// <summary>
    /// Validates that a rotation preview keeps its model pivot fixed and preserves existing scale.
    /// </summary>
    private static void ValidateRotationTransformPreservesPivotAndScale()
    {
        Transform3DData originalTransform = new Transform3DData(
            new Vector3(3.0f, 4.0f, 5.0f),
            Quaternion.CreateFromAxisAngle(Vector3.UnitZ, MathF.PI / 6.0f),
            new Vector3(2.0f, 3.0f, 4.0f));
        Vector3 importSpaceOrigin = new Vector3(1.0f, 2.0f, -1.0f);
        Vector3 originalWorldOrigin = MeshRotationTransform.CalculateWorldOrigin(originalTransform, importSpaceOrigin);
        Transform3DData rotatedTransform = MeshRotationTransform.CreateUserTransformForRotation(
            originalTransform,
            new Vector3(90.0f, 15.0f, -20.0f),
            importSpaceOrigin,
            RotationCoordinateSpace.World);
        Vector3 rotatedWorldOrigin = MeshRotationTransform.CalculateWorldOrigin(rotatedTransform, importSpaceOrigin);

        ValidateVectorNear(originalWorldOrigin, rotatedWorldOrigin, 0.0001f, "Expected rotation to keep the model pivot fixed.");
        ValidateVectorNear(originalTransform.Scale, rotatedTransform.Scale, 0.0001f, "Expected rotation to preserve the existing user scale.");

        if (rotatedTransform.Rotation == originalTransform.Rotation)
        {
            throw new InvalidOperationException("Expected a non-zero rotation delta to change the user rotation.");
        }
    }

    /// <summary>
    /// Validates Reset semantics by requiring zero deltas to return the exact session-start value.
    /// </summary>
    private static void ValidateZeroRotationRestoresExactSessionTransform()
    {
        Transform3DData originalTransform = new Transform3DData(
            new Vector3(-2.0f, 7.0f, 1.5f),
            Quaternion.CreateFromAxisAngle(Vector3.UnitY, 0.7f),
            new Vector3(1.5f, 0.75f, 2.0f));
        Transform3DData resetTransform = MeshRotationTransform.CreateUserTransformForRotation(
            originalTransform,
            Vector3.Zero,
            new Vector3(4.0f, -3.0f, 0.0f),
            RotationCoordinateSpace.World);

        if (resetTransform != originalTransform)
        {
            throw new InvalidOperationException("Expected zero rotation deltas to restore the exact session-start transform.");
        }
    }

    /// <summary>
    /// Validates that Reset removes user rotation without changing scale or the stable pivot position.
    /// </summary>
    private static void ValidateResetRotationRestoresImportedOrientation()
    {
        Transform3DData rotatedTransform = new Transform3DData(
            new Vector3(8.0f, -3.0f, 6.0f),
            Quaternion.CreateFromYawPitchRoll(0.7f, -0.4f, 0.25f),
            new Vector3(1.5f, 0.75f, 2.0f));
        Vector3 importSpaceOrigin = new Vector3(2.0f, -1.0f, 0.5f);
        Vector3 originalWorldOrigin = MeshRotationTransform.CalculateWorldOrigin(rotatedTransform, importSpaceOrigin);
        Transform3DData resetTransform = MeshRotationTransform.CreateUserTransformForOriginalOrientation(
            rotatedTransform,
            importSpaceOrigin);
        Vector3 resetWorldOrigin = MeshRotationTransform.CalculateWorldOrigin(resetTransform, importSpaceOrigin);

        if (resetTransform.Rotation != Quaternion.Identity)
        {
            throw new InvalidOperationException("Expected Reset to remove all user rotation.");
        }

        ValidateVectorNear(rotatedTransform.Scale, resetTransform.Scale, 0.0001f, "Expected Reset to preserve user scale.");
        ValidateVectorNear(originalWorldOrigin, resetWorldOrigin, 0.0001f, "Expected Reset to keep the model pivot fixed.");
    }

    /// <summary>
    /// Validates that positive X input rotates around the fixed world X axis.
    /// </summary>
    private static void ValidateRotationTransformUsesWorldXAxis()
    {
        Transform3DData rotatedTransform = MeshRotationTransform.CreateUserTransformForRotation(
            Transform3DData.Identity,
            new Vector3(90.0f, 0.0f, 0.0f),
            Vector3.Zero,
            RotationCoordinateSpace.World);
        Vector3 rotatedYAxis = Vector3.Transform(Vector3.UnitY, rotatedTransform.ToMatrix4x4());

        ValidateVectorNear(Vector3.UnitZ, rotatedYAxis, 0.0001f, "Expected positive X rotation to move +Y toward +Z.");
    }

    /// <summary>
    /// Validates that local X input rotates around the model's already-oriented X axis.
    /// </summary>
    private static void ValidateRotationTransformUsesLocalXAxis()
    {
        Transform3DData originalTransform = new Transform3DData(
            Vector3.Zero,
            Quaternion.CreateFromAxisAngle(Vector3.UnitZ, MathF.PI * 0.5f),
            Vector3.One);
        Transform3DData rotatedTransform = MeshRotationTransform.CreateUserTransformForRotation(
            originalTransform,
            new Vector3(90.0f, 0.0f, 0.0f),
            Vector3.Zero,
            RotationCoordinateSpace.Local);
        Vector3 rotatedLocalYAxis = Vector3.Transform(Vector3.UnitY, rotatedTransform.ToMatrix4x4());

        ValidateVectorNear(Vector3.UnitZ, rotatedLocalYAxis, 0.0001f, "Expected local X rotation to move the model's +Y axis toward its +Z axis.");
    }

    /// <summary>
    /// Validates that combined local inputs match X, then Y, then Z rotations around the model's changing axes.
    /// </summary>
    private static void ValidateCombinedLocalRotationUsesChangingModelAxes()
    {
        Transform3DData originalTransform = new Transform3DData(
            Vector3.Zero,
            Quaternion.CreateFromAxisAngle(Vector3.UnitZ, 0.35f),
            Vector3.One);
        Vector3 rotationDegrees = new Vector3(25.0f, -40.0f, 15.0f);
        Transform3DData combinedTransform = MeshRotationTransform.CreateUserTransformForRotation(
            originalTransform,
            rotationDegrees,
            Vector3.Zero,
            RotationCoordinateSpace.Local);
        Transform3DData sequentialTransform = MeshRotationTransform.CreateUserTransformForRotation(
            originalTransform,
            new Vector3(rotationDegrees.X, 0.0f, 0.0f),
            Vector3.Zero,
            RotationCoordinateSpace.Local);
        sequentialTransform = MeshRotationTransform.CreateUserTransformForRotation(
            sequentialTransform,
            new Vector3(0.0f, rotationDegrees.Y, 0.0f),
            Vector3.Zero,
            RotationCoordinateSpace.Local);
        sequentialTransform = MeshRotationTransform.CreateUserTransformForRotation(
            sequentialTransform,
            new Vector3(0.0f, 0.0f, rotationDegrees.Z),
            Vector3.Zero,
            RotationCoordinateSpace.Local);
        Matrix4x4 combinedMatrix = combinedTransform.ToMatrix4x4();
        Matrix4x4 sequentialMatrix = sequentialTransform.ToMatrix4x4();

        ValidateVectorNear(
            Vector3.Transform(Vector3.UnitX, sequentialMatrix),
            Vector3.Transform(Vector3.UnitX, combinedMatrix),
            0.0001f,
            "Expected combined local rotation to match sequential X, Y, and Z rotations around the changing model axes.");
        ValidateVectorNear(
            Vector3.Transform(Vector3.UnitY, sequentialMatrix),
            Vector3.Transform(Vector3.UnitY, combinedMatrix),
            0.0001f,
            "Expected combined local rotation to preserve the sequential local-axis orientation.");
        ValidateVectorNear(
            Vector3.Transform(Vector3.UnitZ, sequentialMatrix),
            Vector3.Transform(Vector3.UnitZ, combinedMatrix),
            0.0001f,
            "Expected combined local rotation to preserve the sequential local-axis orientation.");
    }

    /// <summary>
    /// Validates that Line Support guide generation preserves the user's clicked endpoints.
    /// </summary>
    private static void ValidateLineSupportPatternIncludesClickedEndpoints()
    {
        List<Vector3> points = new List<Vector3>
        {
            new Vector3(0.0f, 0.0f, 1.0f),
            new Vector3(10.0f, 0.0f, 1.0f)
        };
        List<Vector3> guidePoints = new List<Vector3>();

        LineSupportPattern.FillGuidePoints(points, 3.0f, guidePoints);

        ValidateVectorNear(points[0], guidePoints[0], 0.0001f, "Expected the first clicked point to be included.");
        ValidateVectorNear(points[1], guidePoints[guidePoints.Count - 1], 0.0001f, "Expected the final clicked point to be included.");
    }

    /// <summary>
    /// Validates that adjacent line segments do not duplicate their shared clicked vertex.
    /// </summary>
    private static void ValidateLineSupportPatternAvoidsDuplicateSharedVertices()
    {
        List<Vector3> points = new List<Vector3>
        {
            new Vector3(0.0f, 0.0f, 1.0f),
            new Vector3(10.0f, 0.0f, 1.0f),
            new Vector3(10.0f, 10.0f, 1.0f)
        };
        List<Vector3> guidePoints = new List<Vector3>();
        int sharedVertexCount = 0;

        LineSupportPattern.FillGuidePoints(points, 5.0f, guidePoints);

        for (int i = 0; i < guidePoints.Count; i++)
        {
            if (Vector3.Distance(guidePoints[i], points[1]) <= 0.0001f)
            {
                sharedVertexCount++;
            }
        }

        if (sharedVertexCount != 1)
        {
            throw new InvalidOperationException("Expected the shared polyline vertex to be emitted exactly once.");
        }
    }

    /// <summary>
    /// Validates that no generated interval exceeds the requested spacing.
    /// </summary>
    private static void ValidateLineSupportPatternRespectsSpacingMaximum()
    {
        List<Vector3> points = new List<Vector3>
        {
            new Vector3(0.0f, 0.0f, 1.0f),
            new Vector3(10.0f, 0.0f, 1.0f)
        };
        List<Vector3> guidePoints = new List<Vector3>();

        LineSupportPattern.FillGuidePoints(points, 3.0f, guidePoints);

        for (int i = 1; i < guidePoints.Count; i++)
        {
            float intervalLength = Vector3.Distance(guidePoints[i - 1], guidePoints[i]);

            if (intervalLength > 3.0001f)
            {
                throw new InvalidOperationException("Expected every generated line interval to be no larger than the spacing setting.");
            }
        }
    }

    /// <summary>
    /// Validates that degenerate segments are skipped without producing invalid points.
    /// </summary>
    private static void ValidateLineSupportPatternHandlesDegenerateSegments()
    {
        List<Vector3> points = new List<Vector3>
        {
            new Vector3(2.0f, 2.0f, 1.0f),
            new Vector3(2.0f, 2.0f, 1.0f),
            new Vector3(2.1f, 2.0f, 1.0f)
        };
        List<Vector3> guidePoints = new List<Vector3>();

        LineSupportPattern.FillGuidePoints(points, 5.0f, guidePoints);

        for (int i = 0; i < guidePoints.Count; i++)
        {
            ValidateFinite(guidePoints[i]);
        }

        ValidateVectorNear(points[0], guidePoints[0], 0.0001f, "Expected the first valid point to be preserved.");
        ValidateVectorNear(points[2], guidePoints[guidePoints.Count - 1], 0.0001f, "Expected the tiny non-degenerate endpoint to be preserved.");
    }

    /// <summary>
    /// Validates that continuous Line Support spacing can avoid forcing a support at an interior bend.
    /// </summary>
    private static void ValidateLineSupportPatternCanSkipBendSupports()
    {
        List<Vector3> points = new List<Vector3>
        {
            new Vector3(0.0f, 0.0f, 1.0f),
            new Vector3(2.0f, 0.0f, 1.0f),
            new Vector3(2.0f, 8.0f, 1.0f)
        };
        List<Vector3> guidePoints = new List<Vector3>();

        LineSupportPattern.FillGuidePoints(points, 4.0f, false, guidePoints);

        if (guidePoints.Count != 4)
        {
            throw new InvalidOperationException("Expected the continuous polyline to be split into four support locations.");
        }

        for (int i = 0; i < guidePoints.Count; i++)
        {
            if (Vector3.Distance(guidePoints[i], points[1]) <= 0.0001f)
            {
                throw new InvalidOperationException("Expected the interior bend to be skipped when bend supports are disabled.");
            }
        }

        ValidateVectorNear(points[0], guidePoints[0], 0.0001f, "Expected continuous spacing to preserve the first clicked point.");
        ValidateVectorNear(points[2], guidePoints[guidePoints.Count - 1], 0.0001f, "Expected continuous spacing to preserve the final clicked point.");
    }

    /// <summary>
    /// Validates that a contour only uses the connected face patch seeded by the clicked face.
    /// </summary>
    private static void ValidateContourSupportStaysInSeededConnectedPatch()
    {
        MeshEntity mesh = CreateTwoDisconnectedVerticalPanels();
        ContourSupportSettings settings = new ContourSupportSettings(
            new Vector3(0.0f, 2.0f, 2.0f),
            0,
            5.0f,
            180.0f,
            2.0f,
            0.0f,
            0.0f);
        ContourSupportResult result;

        if (!ContourSupportPattern.TryCreate(mesh, settings, out result))
        {
            throw new InvalidOperationException("Expected a contour on the seeded panel.");
        }

        for (int i = 0; i < result.ContourPoints.Count; i++)
        {
            if (result.ContourPoints[i].X > 1.0f)
            {
                throw new InvalidOperationException("Expected the contour to stay on the seeded connected patch only.");
            }
        }
    }

    /// <summary>
    /// Validates that imported STL-style triangle soup still traverses coincident geometric edges.
    /// </summary>
    private static void ValidateContourSupportTraversesDuplicatedPanelVertices()
    {
        MeshEntity mesh = CreateStlStyleSingleVerticalPanel();
        ContourSupportSettings settings = new ContourSupportSettings(
            new Vector3(0.0f, 2.0f, 2.0f),
            0,
            5.0f,
            1.0f,
            5.0f,
            0.0f,
            0.0f);
        ContourSupportResult result;

        if (!ContourSupportPattern.TryCreate(mesh, settings, out result))
        {
            throw new InvalidOperationException("Expected a contour on the duplicated-vertex panel.");
        }

        if (result.Length < 9.999f)
        {
            throw new InvalidOperationException("Expected the contour to span both duplicated-vertex triangles edge-to-edge.");
        }

        float minimumY = float.MaxValue;
        float maximumY = float.MinValue;

        for (int i = 0; i < result.ContourPoints.Count; i++)
        {
            minimumY = MathF.Min(minimumY, result.ContourPoints[i].Y);
            maximumY = MathF.Max(maximumY, result.ContourPoints[i].Y);
        }

        if (minimumY > 0.0001f || maximumY < 9.999f)
        {
            throw new InvalidOperationException("Expected the duplicated-vertex panel contour to reach both panel edges.");
        }
    }

    /// <summary>
    /// Validates that a short seed-fragment does not win over a nearby longer contour in the same patch.
    /// </summary>
    private static void ValidateContourSupportSelectsNearbyLongerPathWhenSeedSliceIsShort()
    {
        MeshEntity mesh = CreateConnectedShortAndLongPanels();
        ContourSupportSettings settings = new ContourSupportSettings(
            new Vector3(0.0f, 0.05f, 5.0f),
            0,
            5.0f,
            180.0f,
            5.0f,
            0.0f,
            0.0f);
        ContourSupportResult result;

        if (!ContourSupportPattern.TryCreate(mesh, settings, out result))
        {
            throw new InvalidOperationException("Expected a contour from the connected short-and-long panel patch.");
        }

        if (!result.Diagnostics.UsedNearestLongerPath)
        {
            throw new InvalidOperationException("Expected the extractor to replace the short seed path with the nearby longer contour.");
        }

        if (result.Length < 9.999f)
        {
            throw new InvalidOperationException("Expected the selected contour to use the longer nearby panel.");
        }
    }

    /// <summary>
    /// Validates that tiny slice endpoint gaps do not split one connected contour into fragments.
    /// </summary>
    private static void ValidateContourSupportBridgesTinySliceEndpointGaps()
    {
        MeshEntity mesh = CreateConnectedPanelWithTinySliceEndpointGap();
        ContourSupportSettings settings = new ContourSupportSettings(
            new Vector3(2.0f, 0.0f, 5.0f),
            0,
            5.0f,
            180.0f,
            5.0f,
            0.0f,
            0.0f);
        ContourSupportResult result;

        if (!ContourSupportPattern.TryCreate(mesh, settings, out result))
        {
            throw new InvalidOperationException("Expected a contour across the tiny endpoint gap.");
        }

        if (result.Length < 9.999f)
        {
            throw new InvalidOperationException("Expected contour assembly to bridge the tiny slice endpoint gap.");
        }
    }

    /// <summary>
    /// Validates that the coplanar threshold prevents contour traversal around a sharp corner.
    /// </summary>
    private static void ValidateContourSupportThresholdBlocksSharpFaceTransitions()
    {
        MeshEntity mesh = CreateBentVerticalPanels();
        ContourSupportSettings blockedSettings = new ContourSupportSettings(
            new Vector3(0.0f, 2.0f, 2.0f),
            0,
            5.0f,
            10.0f,
            2.0f,
            0.0f,
            0.0f);
        ContourSupportSettings allowedSettings = new ContourSupportSettings(
            new Vector3(0.0f, 2.0f, 2.0f),
            0,
            5.0f,
            100.0f,
            2.0f,
            0.0f,
            0.0f);
        ContourSupportResult blockedResult;
        ContourSupportResult allowedResult;

        if (!ContourSupportPattern.TryCreate(mesh, blockedSettings, out blockedResult)
            || !ContourSupportPattern.TryCreate(mesh, allowedSettings, out allowedResult))
        {
            throw new InvalidOperationException("Expected both contour threshold cases to produce contours.");
        }

        if (allowedResult.Length <= blockedResult.Length + 1.0f)
        {
            throw new InvalidOperationException("Expected the relaxed threshold to include the adjacent sharp face.");
        }

        if (blockedResult.Diagnostics.ThresholdBlockedAdjacencyCount == 0)
        {
            throw new InvalidOperationException("Expected diagnostics to record threshold-blocked adjacency.");
        }
    }

    /// <summary>
    /// Validates that geometric adjacency still respects normal-angle thresholding on STL-style meshes.
    /// </summary>
    private static void ValidateContourSupportThresholdWorksWithDuplicatedVertices()
    {
        MeshEntity mesh = CreateStlStyleBentVerticalPanels();
        ContourSupportSettings blockedSettings = new ContourSupportSettings(
            new Vector3(0.0f, 2.0f, 2.0f),
            0,
            5.0f,
            10.0f,
            2.0f,
            0.0f,
            0.0f);
        ContourSupportSettings allowedSettings = new ContourSupportSettings(
            new Vector3(0.0f, 2.0f, 2.0f),
            0,
            5.0f,
            100.0f,
            2.0f,
            0.0f,
            0.0f);
        ContourSupportResult blockedResult;
        ContourSupportResult allowedResult;

        if (!ContourSupportPattern.TryCreate(mesh, blockedSettings, out blockedResult)
            || !ContourSupportPattern.TryCreate(mesh, allowedSettings, out allowedResult))
        {
            throw new InvalidOperationException("Expected both duplicated-vertex threshold cases to produce contours.");
        }

        if (blockedResult.Length < 9.999f)
        {
            throw new InvalidOperationException("Expected the blocked duplicated-vertex contour to still span the seeded panel.");
        }

        if (allowedResult.Length <= blockedResult.Length + 9.0f)
        {
            throw new InvalidOperationException("Expected the relaxed threshold to include the adjacent duplicated-vertex face.");
        }
    }

    /// <summary>
    /// Validates closed contour distribution reduces spacing evenly so no interval exceeds the requested spacing.
    /// </summary>
    private static void ValidateContourSupportClosedLoopSpacingIsEven()
    {
        MeshEntity mesh = CreateOpenTopCubeSideMesh();
        ContourSupportSettings settings = new ContourSupportSettings(
            new Vector3(0.0f, 2.0f, 2.0f),
            0,
            5.0f,
            100.0f,
            3.0f,
            0.0f,
            0.0f);
        ContourSupportResult result;

        if (!ContourSupportPattern.TryCreate(mesh, settings, out result))
        {
            throw new InvalidOperationException("Expected a closed cube-side contour.");
        }

        if (!result.IsClosed)
        {
            throw new InvalidOperationException("Expected the cube side contour to form a closed loop.");
        }

        for (int i = 0; i < result.SupportSamples.Count; i++)
        {
            Vector3 current = result.SupportSamples[i].Position;
            Vector3 next = result.SupportSamples[(i + 1) % result.SupportSamples.Count].Position;

            if (Vector3.Distance(current, next) > 3.0001f)
            {
                throw new InvalidOperationException("Expected closed contour intervals to stay within the requested spacing.");
            }
        }
    }

    /// <summary>
    /// Validates that closed contour start offset rotates support positions without changing count or spacing.
    /// </summary>
    private static void ValidateContourSupportClosedLoopStartOffsetRotatesSupports()
    {
        MeshEntity mesh = CreateOpenTopCubeSideMesh();
        ContourSupportSettings baselineSettings = new ContourSupportSettings(
            new Vector3(0.0f, 2.0f, 2.0f),
            0,
            5.0f,
            100.0f,
            3.0f,
            0.0f,
            0.0f);
        ContourSupportSettings offsetSettings = new ContourSupportSettings(
            new Vector3(0.0f, 2.0f, 2.0f),
            0,
            5.0f,
            100.0f,
            3.0f,
            2.0f,
            0.0f);
        ContourSupportResult baselineResult;
        ContourSupportResult offsetResult;

        if (!ContourSupportPattern.TryCreate(mesh, baselineSettings, out baselineResult)
            || !ContourSupportPattern.TryCreate(mesh, offsetSettings, out offsetResult))
        {
            throw new InvalidOperationException("Expected both closed contour offset cases to produce supports.");
        }

        if (!baselineResult.IsClosed || !offsetResult.IsClosed)
        {
            throw new InvalidOperationException("Expected both contour offset cases to stay closed.");
        }

        if (baselineResult.SupportSamples.Count != offsetResult.SupportSamples.Count)
        {
            throw new InvalidOperationException("Expected start offset to preserve the closed-loop support count.");
        }

        float firstShiftDistance = Vector3.Distance(
            baselineResult.SupportSamples[0].Position,
            offsetResult.SupportSamples[0].Position);

        if (MathF.Abs(firstShiftDistance - 2.0f) > 0.0001f)
        {
            throw new InvalidOperationException("Expected the closed-loop start offset to shift the first support around the contour.");
        }

        for (int i = 0; i < offsetResult.SupportSamples.Count; i++)
        {
            Vector3 current = offsetResult.SupportSamples[i].Position;
            Vector3 next = offsetResult.SupportSamples[(i + 1) % offsetResult.SupportSamples.Count].Position;

            if (Vector3.Distance(current, next) > 3.0001f)
            {
                throw new InvalidOperationException("Expected shifted closed contour intervals to stay within the requested spacing.");
            }
        }
    }

    /// <summary>
    /// Validates that contour samples on near-vertical STL-noisy faces all pass support placement.
    /// </summary>
    private static void ValidateContourSupportPlacesSupportsOnNoisyNearVerticalFaces()
    {
        MeshEntity mesh = CreateNoisyOpenTopCubeSideMesh();
        ContourSupportSettings settings = new ContourSupportSettings(
            new Vector3(0.0f, 2.0f, 2.0f),
            0,
            5.0f,
            100.0f,
            5.0f,
            0.0f,
            0.0f);
        SupportProfile profile = CreateBranchProfile(45.0f, 20.0f, 0.5f);
        ContourSupportResult result;

        if (!ContourSupportPattern.TryCreate(mesh, settings, out result))
        {
            throw new InvalidOperationException("Expected a closed contour on the noisy near-vertical tube mesh.");
        }

        if (!result.IsClosed)
        {
            throw new InvalidOperationException("Expected the noisy tube contour to form a closed loop.");
        }

        int placementCount = 0;

        for (int i = 0; i < result.SupportSamples.Count; i++)
        {
            ContourSupportSample sample = result.SupportSamples[i];

            if (sample.Normal.Z <= 0.0001f)
            {
                throw new InvalidOperationException("Expected this synthetic contour to exercise tiny positive-Z normals.");
            }

            SupportPlacementPlan placementPlan;

            if (!SupportPlacementPlanner.TryCreatePlacement(mesh, sample.Position, sample.Normal, profile, out placementPlan))
            {
                throw new InvalidOperationException("Expected every noisy near-vertical contour sample to accept a support placement.");
            }

            placementCount++;
        }

        if (placementCount != result.SupportSamples.Count)
        {
            throw new InvalidOperationException("Expected support placement to cover every contour sample.");
        }
    }

    /// <summary>
    /// Validates open contour offsets are applied before spacing distribution.
    /// </summary>
    private static void ValidateContourSupportOpenOffsetsRespectSpacing()
    {
        MeshEntity mesh = CreateSingleVerticalPanel();
        ContourSupportSettings settings = new ContourSupportSettings(
            new Vector3(0.0f, 2.0f, 2.0f),
            0,
            5.0f,
            1.0f,
            5.0f,
            2.0f,
            2.0f);
        ContourSupportResult result;

        if (!ContourSupportPattern.TryCreate(mesh, settings, out result))
        {
            throw new InvalidOperationException("Expected an open panel contour.");
        }

        if (result.IsClosed)
        {
            throw new InvalidOperationException("Expected a single-panel contour to stay open.");
        }

        ValidateVectorNear(new Vector3(0.0f, 2.0f, 5.0f), result.SupportSamples[0].Position, 0.0001f, "Expected the first support to honor the start offset.");
        ValidateVectorNear(new Vector3(0.0f, 8.0f, 5.0f), result.SupportSamples[result.SupportSamples.Count - 1].Position, 0.0001f, "Expected the final support to honor the final offset.");

        for (int i = 1; i < result.SupportSamples.Count; i++)
        {
            if (Vector3.Distance(result.SupportSamples[i - 1].Position, result.SupportSamples[i].Position) > 5.0001f)
            {
                throw new InvalidOperationException("Expected open contour intervals to stay within the requested spacing.");
            }
        }
    }

    /// <summary>
    /// Validates edited Z heights can select the nearest contour in the same seeded face patch.
    /// </summary>
    private static void ValidateContourSupportZEditsChooseNearestPatchContour()
    {
        MeshEntity mesh = CreateSingleVerticalPanel();
        ContourSupportSettings settings = new ContourSupportSettings(
            new Vector3(0.0f, 2.0f, 1.0f),
            0,
            8.0f,
            1.0f,
            3.0f,
            0.0f,
            0.0f);
        ContourSupportResult result;

        if (!ContourSupportPattern.TryCreate(mesh, settings, out result))
        {
            throw new InvalidOperationException("Expected the edited Z height to find a contour in the seeded patch.");
        }

        for (int i = 0; i < result.ContourPoints.Count; i++)
        {
            if (MathF.Abs(result.ContourPoints[i].Z - 8.0f) > 0.0001f)
            {
                throw new InvalidOperationException("Expected every edited-Z contour point to use the requested Z height.");
            }
        }
    }

    /// <summary>
    /// Validates that a ring thinner than support spacing still receives centreline fallback supports when enabled.
    /// </summary>
    private static void ValidateAreaSupportThinRingUsesCentrelineFallback()
    {
        MeshEntity mesh = CreateSquareRingAreaMesh(2.0f);
        AreaSupportSettings settings = CreateAreaSupportSettingsForAllFaces(mesh, 3.0f, 2.0f, true, 1.0f);
        AreaSupportResult result;

        if (!AreaSupportPattern.TryCreate(mesh, settings, out result))
        {
            throw new InvalidOperationException("Expected thin-ring centreline fallback to generate supports.");
        }

        if (result.SupportSamples.Count == 0)
        {
            throw new InvalidOperationException("Expected at least one fallback support sample.");
        }
    }

    /// <summary>
    /// Validates that thin-region centreline fallback is skipped when the option is disabled.
    /// </summary>
    private static void ValidateAreaSupportThinFallbackCanBeDisabled()
    {
        MeshEntity mesh = CreateSquareRingAreaMesh(2.0f);
        AreaSupportSettings settings = CreateAreaSupportSettingsForAllFaces(mesh, 3.0f, 2.0f, false, 1.0f);
        AreaSupportResult result;

        if (AreaSupportPattern.TryCreate(mesh, settings, out result))
        {
            throw new InvalidOperationException("Expected disabled thin-region fallback to leave the too-thin ring unsupported.");
        }
    }

    /// <summary>
    /// Validates that ultra-thin regions below the configured minimum thickness do not receive fallback supports.
    /// </summary>
    private static void ValidateAreaSupportUltraThinRegionsRespectMinimumThickness()
    {
        MeshEntity mesh = CreateSquareRingAreaMesh(0.5f);
        AreaSupportSettings settings = CreateAreaSupportSettingsForAllFaces(mesh, 3.0f, 2.0f, true, 1.0f);
        AreaSupportResult result;

        if (AreaSupportPattern.TryCreate(mesh, settings, out result))
        {
            throw new InvalidOperationException("Expected regions thinner than the minimum thickness to receive no centreline supports.");
        }
    }

    /// <summary>
    /// Validates that Area Support creates normal offset supports around internal hole boundaries.
    /// </summary>
    private static void ValidateAreaSupportOffsetsInternalHoleBoundaries()
    {
        const float OuterHalfSize = 6.0f;
        const float BandThickness = 4.0f;
        const float InnerHalfSize = OuterHalfSize - BandThickness;
        const float Spacing = 2.0f;
        float expectedInnerOffsetCoordinate = InnerHalfSize + AreaSupportSettings.CalculateDefaultBoundaryOffset(Spacing);
        MeshEntity mesh = CreateSquareRingAreaMesh(BandThickness);
        AreaSupportSettings settings = CreateAreaSupportSettingsForAllFaces(mesh, Spacing, 1.5f, false, 1.0f);
        AreaSupportResult result;

        if (!AreaSupportPattern.TryCreate(mesh, settings, out result))
        {
            throw new InvalidOperationException("Expected wide ring area support generation to succeed.");
        }

        for (int i = 0; i < result.SupportSamples.Count; i++)
        {
            Vector3 samplePosition = result.SupportSamples[i].Position;
            float maximumCoordinate = MathF.Max(MathF.Abs(samplePosition.X), MathF.Abs(samplePosition.Y));

            if (MathF.Abs(maximumCoordinate - expectedInnerOffsetCoordinate) <= 0.25f)
            {
                return;
            }
        }

        throw new InvalidOperationException("Expected at least one support on the internal hole offset boundary.");
    }

    /// <summary>
    /// Validates that coarse selected mesh edges are drawn as sampled offset pieces rather than long raw chords.
    /// </summary>
    private static void ValidateAreaSupportSplitsCoarseOffsetBoundaryPreviewPaths()
    {
        MeshEntity mesh = CreateSquareAreaMesh(40.0f);
        AreaSupportSettings settings = CreateAreaSupportSettingsForAllFaces(mesh, 3.0f, 2.0f, false, 1.0f);
        AreaSupportResult result;

        if (!AreaSupportPattern.TryCreate(mesh, settings, out result))
        {
            throw new InvalidOperationException("Expected coarse square area support generation to succeed.");
        }

        for (int i = 0; i < result.OffsetBoundarySegments.Count; i++)
        {
            AreaSupportBoundarySegment segment = result.OffsetBoundarySegments[i];
            float segmentLength = Vector3.Distance(segment.Start, segment.End);

            if (segmentLength > settings.Spacing * 1.5f)
            {
                throw new InvalidOperationException("Expected coarse offset boundary preview to be split into short validated pieces.");
            }
        }
    }

    /// <summary>
    /// Validates that Boundary Offsets mode creates only the requested cumulative outer rings.
    /// </summary>
    private static void ValidateAreaSupportBoundaryOffsetFillCreatesRequestedRings()
    {
        MeshEntity mesh = CreateSquareAreaMesh(40.0f);
        AreaSupportSettings settings = CreateAreaSupportSettingsForAllFaces(
            mesh,
            3.0f,
            2.5f,
            false,
            1.0f,
            2.0f,
            AreaSupportFillMode.BoundaryOffsets,
            2,
            3.0f);

        if (!AreaSupportPattern.TryCreate(mesh, settings, out AreaSupportResult result))
        {
            throw new InvalidOperationException("Expected Boundary Offsets mode to generate supports.");
        }

        bool foundFirstRing = false;
        bool foundSecondRing = false;
        bool foundThirdRing = false;

        for (int i = 0; i < result.SupportSamples.Count; i++)
        {
            Vector3 position = result.SupportSamples[i].Position;
            float ringCoordinate = MathF.Max(MathF.Abs(position.X), MathF.Abs(position.Y));
            foundFirstRing |= MathF.Abs(ringCoordinate - 18.0f) <= 0.01f;
            foundSecondRing |= MathF.Abs(ringCoordinate - 15.0f) <= 0.01f;
            foundThirdRing |= MathF.Abs(ringCoordinate - 12.0f) <= 0.01f;

            if (MathF.Abs(ringCoordinate - 18.0f) > 0.01f
                && MathF.Abs(ringCoordinate - 15.0f) > 0.01f
                && MathF.Abs(ringCoordinate - 12.0f) > 0.01f)
            {
                throw new InvalidOperationException("Boundary Offsets mode generated a support away from its requested rings.");
            }
        }

        if (!foundFirstRing || !foundSecondRing || !foundThirdRing)
        {
            throw new InvalidOperationException("Expected supports on the original boundary offset and both additional offsets.");
        }
    }

    /// <summary>
    /// Validates that closed-ring spacing includes an equal final-to-first interval.
    /// </summary>
    private static void ValidateAreaSupportBoundaryOffsetFillClosesSpacingSeam()
    {
        const float RingHalfSize = 18.0f;
        const float RequestedSpacing = 3.0f;
        float perimeter = RingHalfSize * 8.0f;
        MeshEntity mesh = CreateSquareAreaMesh(40.0f);
        AreaSupportSettings settings = CreateAreaSupportSettingsForAllFaces(
            mesh,
            3.0f,
            RequestedSpacing,
            false,
            1.0f,
            2.0f,
            AreaSupportFillMode.BoundaryOffsets,
            0);

        if (!AreaSupportPattern.TryCreate(mesh, settings, out AreaSupportResult result))
        {
            throw new InvalidOperationException("Expected one closed Boundary Offsets support ring.");
        }

        List<float> distances = new List<float>(result.SupportSamples.Count);

        for (int i = 0; i < result.SupportSamples.Count; i++)
        {
            distances.Add(GetSquarePerimeterDistance(result.SupportSamples[i].Position, RingHalfSize));
        }

        distances.Sort();
        float expectedSpacing = perimeter / distances.Count;

        for (int i = 0; i < distances.Count; i++)
        {
            float nextDistance = i + 1 < distances.Count ? distances[i + 1] : distances[0] + perimeter;
            float gap = nextDistance - distances[i];

            if (gap > RequestedSpacing + 0.001f || MathF.Abs(gap - expectedSpacing) > 0.01f)
            {
                throw new InvalidOperationException("Expected equal Boundary Offsets spacing across the closing seam.");
            }
        }
    }

    /// <summary>
    /// Validates that a hole offset remains visible but contributes no generated supports.
    /// </summary>
    private static void ValidateAreaSupportBoundaryOffsetFillDrawsHolesWithoutSupportingThem()
    {
        MeshEntity mesh = CreateSquareRingAreaMesh(4.0f);
        AreaSupportSettings settings = CreateAreaSupportSettingsForAllFaces(
            mesh,
            3.0f,
            2.0f,
            false,
            1.0f,
            1.0f,
            AreaSupportFillMode.BoundaryOffsets,
            1);

        if (!AreaSupportPattern.TryCreate(mesh, settings, out AreaSupportResult result))
        {
            throw new InvalidOperationException("Expected Boundary Offsets mode to support the square ring area.");
        }

        bool foundHoleOffsetPreview = false;

        for (int i = 0; i < result.OffsetBoundarySegments.Count; i++)
        {
            AreaSupportBoundarySegment segment = result.OffsetBoundarySegments[i];
            float segmentCoordinate = MathF.Max(MathF.Abs(segment.Start.X), MathF.Abs(segment.Start.Y));

            if (MathF.Abs(segmentCoordinate - 3.0f) <= 0.01f)
            {
                foundHoleOffsetPreview = true;
                break;
            }
        }

        if (!foundHoleOffsetPreview)
        {
            throw new InvalidOperationException("Expected the internal-hole offset boundary to remain in the preview.");
        }

        for (int i = 0; i < result.SupportSamples.Count; i++)
        {
            Vector3 position = result.SupportSamples[i].Position;
            float ringCoordinate = MathF.Max(MathF.Abs(position.X), MathF.Abs(position.Y));

            if (MathF.Abs(ringCoordinate - 3.0f) <= 0.1f
                || (MathF.Abs(position.X) < 2.0f && MathF.Abs(position.Y) < 2.0f))
            {
                throw new InvalidOperationException("Internal holes must not generate Boundary Offsets supports.");
            }
        }
    }

    /// <summary>
    /// Validates that the concave-corner rule is applied independently to every generated ring.
    /// </summary>
    private static void ValidateAreaSupportBoundaryOffsetFillSupportsConcaveCornersOnEachRing()
    {
        MeshEntity mesh = CreateConcaveAreaMesh();
        AreaSupportSettings settings = CreateAreaSupportSettingsForAllFaces(
            mesh,
            3.0f,
            2.5f,
            false,
            1.0f,
            1.0f,
            AreaSupportFillMode.BoundaryOffsets,
            1);

        if (!AreaSupportPattern.TryCreate(mesh, settings, out AreaSupportResult result))
        {
            throw new InvalidOperationException("Expected the concave area to generate offset-ring supports.");
        }

        bool foundFirstConcaveCorner = false;
        bool foundSecondConcaveCorner = false;
        float closestFirstDistance = float.MaxValue;
        float closestSecondDistance = float.MaxValue;

        for (int i = 0; i < result.SupportSamples.Count; i++)
        {
            Vector3 position = result.SupportSamples[i].Position;
            float firstDistance = Vector2.Distance(new Vector2(position.X, position.Y), new Vector2(5.0f, 5.0f));
            float secondDistance = Vector2.Distance(new Vector2(position.X, position.Y), new Vector2(4.0f, 4.0f));
            closestFirstDistance = MathF.Min(closestFirstDistance, firstDistance);
            closestSecondDistance = MathF.Min(closestSecondDistance, secondDistance);
            foundFirstConcaveCorner |= firstDistance <= 0.01f;
            foundSecondConcaveCorner |= secondDistance <= 0.01f;
        }

        if (!foundFirstConcaveCorner || !foundSecondConcaveCorner)
        {
            throw new InvalidOperationException($"Expected a support at the concave corner of every offset ring. Closest distances were {closestFirstDistance} and {closestSecondDistance}.");
        }
    }

    /// <summary>
    /// Validates that offsets which fully collapse return no generated pattern without throwing.
    /// </summary>
    private static void ValidateAreaSupportBoundaryOffsetFillHandlesCollapsedRings()
    {
        MeshEntity mesh = CreateSquareAreaMesh(4.0f);
        AreaSupportSettings settings = CreateAreaSupportSettingsForAllFaces(
            mesh,
            3.0f,
            2.0f,
            false,
            1.0f,
            3.0f,
            AreaSupportFillMode.BoundaryOffsets,
            2);

        if (AreaSupportPattern.TryCreate(mesh, settings, out AreaSupportResult _))
        {
            throw new InvalidOperationException("Expected fully collapsed Boundary Offsets rings to generate no supports.");
        }
    }

    /// <summary>
    /// Maps a point on a square contour to one sortable distance around its perimeter.
    /// </summary>
    private static float GetSquarePerimeterDistance(Vector3 point, float halfSize)
    {
        const float Tolerance = 0.01f;

        if (MathF.Abs(point.Y + halfSize) <= Tolerance)
        {
            return point.X + halfSize;
        }

        if (MathF.Abs(point.X - halfSize) <= Tolerance)
        {
            return (2.0f * halfSize) + point.Y + halfSize;
        }

        if (MathF.Abs(point.Y - halfSize) <= Tolerance)
        {
            return (4.0f * halfSize) + halfSize - point.X;
        }

        if (MathF.Abs(point.X + halfSize) <= Tolerance)
        {
            return (6.0f * halfSize) + halfSize - point.Y;
        }

        throw new InvalidOperationException("Expected every Boundary Offsets sample to lie on the square contour.");
    }

    /// <summary>
    /// Validates that Line Support generator metadata is saved and loaded with the project.
    /// </summary>
    private static void ValidateLineSupportSettingsSurviveSaveAndLoad()
    {
        CadDocument document = new CadDocument();
        MeshEntity mesh = CreateSingleTriangleMesh(
            new Vector3(0.0f, 0.0f, 2.0f),
            new Vector3(10.0f, 0.0f, 2.0f),
            new Vector3(0.0f, 10.0f, 2.0f),
            Transform3DData.Identity);
        document.AddEntity(mesh);

        List<Vector3> points = new List<Vector3>
        {
            new Vector3(1.0f, 1.0f, 2.0f),
            new Vector3(6.0f, 1.0f, 2.0f),
            new Vector3(6.0f, 4.0f, 2.0f)
        };
        SupportLayerGroup supportLayerGroup = new SupportLayerGroup(mesh.Id, "Line Supports");
        supportLayerGroup.SetLineSupportSettings(new LineSupportSettings(points, 2.5f, false));
        document.AddSupportLayerGroup(supportLayerGroup);

        GphDocumentSerializer serializer = new GphDocumentSerializer();
        string filePath = Path.Combine(Environment.CurrentDirectory, "LineSupportSettingsSmoke.gph");

        try
        {
            serializer.Save(document, filePath);
            string savedJson = File.ReadAllText(filePath);

            if (savedJson.IndexOf("\"version\"", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                throw new InvalidOperationException("Expected saved Graphite project files to omit pre-V2 file version metadata.");
            }

            GphDocumentData loadedDocument = serializer.LoadDocument(filePath);

            if (loadedDocument.SupportLayerGroups.Count != 1)
            {
                throw new InvalidOperationException("Expected one loaded support layer group.");
            }

            SupportLayerGroup loadedSupportLayerGroup = loadedDocument.SupportLayerGroups[0];
            LineSupportSettings? loadedSettings = loadedSupportLayerGroup.LineSupportSettings;

            if (loadedSupportLayerGroup.GeneratorKind != SupportGroupGeneratorKind.LineSupport || loadedSettings == null)
            {
                throw new InvalidOperationException("Expected the loaded support layer group to preserve Line Support metadata.");
            }

            if (loadedSettings.Points.Count != points.Count || MathF.Abs(loadedSettings.Spacing - 2.5f) > 0.0001f)
            {
                throw new InvalidOperationException("Expected the loaded Line Support settings to preserve point count and spacing.");
            }

            if (loadedSettings.PlaceSupportsAtBends)
            {
                throw new InvalidOperationException("Expected the loaded Line Support settings to preserve bend placement behavior.");
            }

            for (int i = 0; i < points.Count; i++)
            {
                ValidateVectorNear(points[i], loadedSettings.Points[i], 0.0001f, "Expected loaded Line Support points to match saved points.");
            }
        }
        finally
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }

    /// <summary>
    /// Validates that Contour Support generator metadata is saved and loaded with the project.
    /// </summary>
    private static void ValidateContourSupportSettingsSurviveSaveAndLoad()
    {
        CadDocument document = new CadDocument();
        MeshEntity mesh = CreateSingleVerticalPanel();
        document.AddEntity(mesh);
        ContourSupportSettings settings = new ContourSupportSettings(
            new Vector3(0.0f, 2.0f, 2.0f),
            0,
            5.0f,
            15.0f,
            2.5f,
            0.5f,
            0.75f);
        SupportLayerGroup supportLayerGroup = new SupportLayerGroup(mesh.Id, "Contour Supports");
        supportLayerGroup.SetContourSupportSettings(settings);
        document.AddSupportLayerGroup(supportLayerGroup);

        GphDocumentSerializer serializer = new GphDocumentSerializer();
        string filePath = Path.Combine(Environment.CurrentDirectory, "ContourSupportSettingsSmoke.gph");

        try
        {
            serializer.Save(document, filePath);

            GphDocumentData loadedDocument = serializer.LoadDocument(filePath);

            if (loadedDocument.SupportLayerGroups.Count != 1)
            {
                throw new InvalidOperationException("Expected one loaded support layer group.");
            }

            SupportLayerGroup loadedSupportLayerGroup = loadedDocument.SupportLayerGroups[0];
            ContourSupportSettings? loadedSettings = loadedSupportLayerGroup.ContourSupportSettings;

            if (loadedSupportLayerGroup.GeneratorKind != SupportGroupGeneratorKind.ContourSupport || loadedSettings == null)
            {
                throw new InvalidOperationException("Expected the loaded support layer group to preserve Contour Support metadata.");
            }

            ValidateVectorNear(settings.SeedPoint, loadedSettings.SeedPoint, 0.0001f, "Expected loaded Contour Support seed point to match.");

            if (loadedSettings.SeedTriangleIndex != settings.SeedTriangleIndex
                || MathF.Abs(loadedSettings.ZHeight - settings.ZHeight) > 0.0001f
                || MathF.Abs(loadedSettings.CoplanarThresholdDegrees - settings.CoplanarThresholdDegrees) > 0.0001f
                || MathF.Abs(loadedSettings.Spacing - settings.Spacing) > 0.0001f
                || MathF.Abs(loadedSettings.StartOffset - settings.StartOffset) > 0.0001f
                || MathF.Abs(loadedSettings.FinalOffset - settings.FinalOffset) > 0.0001f)
            {
                throw new InvalidOperationException("Expected loaded Contour Support settings to preserve all numeric fields.");
            }
        }
        finally
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }

    /// <summary>
    /// Validates that Area Support generator metadata is saved and loaded with thin-region settings.
    /// </summary>
    private static void ValidateAreaSupportSettingsSurviveSaveAndLoad()
    {
        CadDocument document = new CadDocument();
        MeshEntity mesh = CreateSquareRingAreaMesh(2.0f);
        document.AddEntity(mesh);
        AreaSupportSettings settings = CreateAreaSupportSettingsForAllFaces(
            mesh,
            3.0f,
            2.0f,
            true,
            1.25f,
            1.75f,
            AreaSupportFillMode.BoundaryOffsets,
            3,
            2.25f);
        SupportLayerGroup supportLayerGroup = new SupportLayerGroup(mesh.Id, "Area Supports");
        supportLayerGroup.SetAreaSupportSettings(settings);
        document.AddSupportLayerGroup(supportLayerGroup);

        GphDocumentSerializer serializer = new GphDocumentSerializer();
        string filePath = Path.Combine(Environment.CurrentDirectory, "AreaSupportSettingsSmoke.gph");

        try
        {
            serializer.Save(document, filePath);

            GphDocumentData loadedDocument = serializer.LoadDocument(filePath);

            if (loadedDocument.SupportLayerGroups.Count != 1)
            {
                throw new InvalidOperationException("Expected one loaded support layer group.");
            }

            SupportLayerGroup loadedSupportLayerGroup = loadedDocument.SupportLayerGroups[0];
            AreaSupportSettings? loadedSettings = loadedSupportLayerGroup.AreaSupportSettings;

            if (loadedSupportLayerGroup.GeneratorKind != SupportGroupGeneratorKind.AreaSupport || loadedSettings == null)
            {
                throw new InvalidOperationException("Expected the loaded support layer group to preserve Area Support metadata.");
            }

            if (!loadedSettings.SupportThinRegions)
            {
                throw new InvalidOperationException("Expected Area Support thin-region setting to survive save and load.");
            }

            if (MathF.Abs(loadedSettings.BoundaryOffset - 1.75f) > 0.0001f)
            {
                throw new InvalidOperationException("Expected Area Support boundary offset to survive save and load.");
            }

            if (MathF.Abs(loadedSettings.MinimumThinRegionThickness - 1.25f) > 0.0001f)
            {
                throw new InvalidOperationException("Expected Area Support minimum thin-region thickness to survive save and load.");
            }

            if (loadedSettings.FillMode != AreaSupportFillMode.BoundaryOffsets || loadedSettings.AdditionalOffsetCount != 3)
            {
                throw new InvalidOperationException("Expected Area Support fill mode and offset count to survive save and load.");
            }

            if (MathF.Abs(loadedSettings.OffsetSpacing - 2.25f) > 0.0001f)
            {
                throw new InvalidOperationException("Expected Area Support offset spacing to survive save and load.");
            }
        }
        finally
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }

    /// <summary>
    /// Validates that cumulative Cluster modifier target batches survive project persistence.
    /// </summary>
    private static void ValidateClusterModifierTargetBatchesSurviveSaveAndLoad()
    {
        CadDocument document = new CadDocument();
        MeshEntity mesh = CreateSingleTriangleMesh(
            new Vector3(0.0f, 0.0f, 2.0f),
            new Vector3(10.0f, 0.0f, 2.0f),
            new Vector3(0.0f, 10.0f, 2.0f),
            Transform3DData.Identity);
        document.AddEntity(mesh);
        SupportLayerGroup supportLayerGroup = new SupportLayerGroup(mesh.Id, "Batched Cluster Supports");
        SupportProfile profile = SupportDefaults.CreateProfile();
        List<SupportEntity> supports = new List<SupportEntity>
        {
            new SupportEntity(supportLayerGroup.Id, new Vector3(0.0f, 0.0f, 6.0f), new Vector3(0.0f, 0.0f, 0.0f), Vector3.UnitZ, 0.0f, Vector3.UnitZ, profile),
            new SupportEntity(supportLayerGroup.Id, new Vector3(1.0f, 0.0f, 6.0f), new Vector3(1.0f, 0.0f, 0.0f), Vector3.UnitZ, 0.0f, Vector3.UnitZ, profile),
            new SupportEntity(supportLayerGroup.Id, new Vector3(2.0f, 0.0f, 6.0f), new Vector3(2.0f, 0.0f, 0.0f), Vector3.UnitZ, 0.0f, Vector3.UnitZ, profile),
            new SupportEntity(supportLayerGroup.Id, new Vector3(3.0f, 0.0f, 6.0f), new Vector3(3.0f, 0.0f, 0.0f), Vector3.UnitZ, 0.0f, Vector3.UnitZ, profile)
        };
        List<SupportModifierTargetBatch> batches = new List<SupportModifierTargetBatch>
        {
            new SupportModifierTargetBatch(new List<Guid> { supports[0].Id, supports[1].Id }),
            new SupportModifierTargetBatch(new List<Guid> { supports[2].Id, supports[3].Id })
        };
        supportLayerGroup.SetSupportModifiers(new List<SupportModifierDefinition>
        {
            SupportModifierDefinition.CreateNew(
                SupportModifierKind.Cluster,
                0,
                CreateSmokeClusterSettings(3.0f, 4),
                null,
                batches,
                supportLayerGroup.SourceGeneratorRevision)
        });
        document.AddSupportLayerGroup(supportLayerGroup);

        for (int i = 0; i < supports.Count; i++)
        {
            document.AddEntity(supports[i]);
        }

        GphDocumentSerializer serializer = new GphDocumentSerializer();
        string filePath = Path.Combine(Environment.CurrentDirectory, "ClusterBatchSmoke.gph");

        try
        {
            serializer.Save(document, filePath);
            string savedJson = File.ReadAllText(filePath);

            if (savedJson.Contains("scope", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Expected saved Cluster modifiers to omit the removed scope field.");
            }

            GphDocumentData loadedDocument = serializer.LoadDocument(filePath);
            IReadOnlyList<SupportModifierTargetBatch> loadedBatches = loadedDocument.SupportLayerGroups[0].SupportModifiers[0].TargetSupportIdBatches;

            if (loadedBatches.Count != 2)
            {
                throw new InvalidOperationException("Expected Cluster modifier target batches to survive save and load.");
            }

            if (loadedBatches[0].TargetSupportIds.Count != 2 || loadedBatches[1].TargetSupportIds.Count != 2)
            {
                throw new InvalidOperationException("Expected loaded Cluster modifier target batches to preserve their target counts.");
            }

            if (loadedDocument.SupportLayerGroups[0].SupportModifiers[0].SourceGeneratorRevision != supportLayerGroup.SourceGeneratorRevision)
            {
                throw new InvalidOperationException("Expected loaded Cluster modifier source revision to survive save and load.");
            }

            if (loadedDocument.SupportLayerGroups[0].SupportModifiers[0].TargetSupportIds.Count != 4)
            {
                throw new InvalidOperationException("Expected loaded Cluster modifier target identities to survive save and load.");
            }
        }
        finally
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }

    /// <summary>
    /// Validates that Direct Edit transition geometry survives project persistence.
    /// </summary>
    private static void ValidateDirectEditSettingsSurviveSaveAndLoad()
    {
        CadDocument document = new CadDocument();
        MeshEntity mesh = CreateSingleTriangleMesh(
            new Vector3(0.0f, 0.0f, 2.0f),
            new Vector3(10.0f, 0.0f, 2.0f),
            new Vector3(0.0f, 10.0f, 2.0f),
            Transform3DData.Identity);
        document.AddEntity(mesh);
        SupportLayerGroup group = new SupportLayerGroup(mesh.Id, "Direct Edit Supports");
        document.AddSupportLayerGroup(group);
        SupportEntity support = new SupportEntity(
            group.Id,
            new Vector3(0.0f, 0.0f, 12.0f),
            Vector3.Zero,
            SupportDefaults.CreateProfile());
        document.AddEntity(support);
        float originalStemTopZ = SupportDirectEditPlanner.CalculateStemTop(support).Z;
        SupportDirectEditSettings settings = new SupportDirectEditSettings(
            new Vector3(2.0f, 3.0f, 0.0f),
            originalStemTopZ - 1.0f,
            support.BasePosition,
            originalStemTopZ);
        SupportModifierDefinition modifier = new SupportModifierDefinition(
            Guid.NewGuid(),
            SupportModifierKind.DirectEdit,
            true,
            0,
            null,
            null,
            null,
            new[] { support.Id },
            null,
            group.SourceGeneratorRevision,
            null,
            null,
            Guid.NewGuid(),
            settings);
        group.SetSupportModifiers(new[] { modifier });

        string filePath = Path.Combine(Environment.CurrentDirectory, "DirectEditSmoke.gph");

        try
        {
            GphDocumentSerializer serializer = new GphDocumentSerializer();
            serializer.Save(document, filePath);
            GphDocumentData loaded = serializer.LoadDocument(filePath);
            SupportDirectEditSettings? loadedSettings = loaded.SupportLayerGroups[0].SupportModifiers[0].DirectEditSettings;

            if (loadedSettings == null)
            {
                throw new InvalidOperationException("Expected Direct Edit settings to survive save and load.");
            }

            ValidateVectorNear(settings.BasePosition, loadedSettings.BasePosition, 0.0001f, "The edited Direct Edit base did not survive persistence.");
            ValidateVectorNear(settings.OriginalBasePosition, loadedSettings.OriginalBasePosition, 0.0001f, "The original Direct Edit base did not survive persistence.");

            if (MathF.Abs(settings.StemTopZ - loadedSettings.StemTopZ) > 0.0001f
                || MathF.Abs(settings.OriginalStemTopZ - loadedSettings.OriginalStemTopZ) > 0.0001f)
            {
                throw new InvalidOperationException("Direct Edit stem heights did not survive persistence.");
            }
        }
        finally
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }

    /// <summary>
    /// Validates that selected-pair replacement exclusions survive project persistence.
    /// </summary>
    private static void ValidateBracePairExclusionsSurviveSaveAndLoad()
    {
        CadDocument document = new CadDocument();
        MeshEntity mesh = CreateSingleTriangleMesh(
            new Vector3(0.0f, 0.0f, 2.0f),
            new Vector3(10.0f, 0.0f, 2.0f),
            new Vector3(0.0f, 10.0f, 2.0f),
            Transform3DData.Identity);
        document.AddEntity(mesh);
        SupportLayerGroup supportLayerGroup = new SupportLayerGroup(mesh.Id, "Pair Exclusion Supports");
        List<SupportEntity> supports = CreateSupportsForGroup(
            supportLayerGroup.Id,
            new List<Vector2>
            {
                Vector2.Zero,
                new Vector2(5.0f, 0.0f),
                new Vector2(2.5f, 4.0f)
            },
            20.0f,
            SupportDefaults.CreateProfile());
        SupportBracePair excludedPair = new SupportBracePair(supports[0].Id, supports[1].Id);
        Guid toolSessionId = Guid.NewGuid();
        SupportModifierTargetBatch excludedBatch = new SupportModifierTargetBatch(new List<Guid> { supports[1].Id, supports[2].Id });
        SupportModifierDefinition modifier = new SupportModifierDefinition(
            Guid.NewGuid(),
            SupportModifierKind.Brace,
            true,
            0,
            null,
            SupportBraceModifierSettings.CreateDefault(),
            null,
            CreateSupportIds(supports),
            null,
            supportLayerGroup.SourceGeneratorRevision,
            new List<SupportBracePair> { excludedPair },
            new List<SupportModifierTargetBatch> { excludedBatch },
            toolSessionId);
        supportLayerGroup.SetSupportModifiers(new List<SupportModifierDefinition> { modifier });
        document.AddSupportLayerGroup(supportLayerGroup);

        for (int i = 0; i < supports.Count; i++)
        {
            document.AddEntity(supports[i]);
        }

        GphDocumentSerializer serializer = new GphDocumentSerializer();
        string filePath = Path.Combine(Environment.CurrentDirectory, "BracePairExclusionSmoke.gph");

        try
        {
            serializer.Save(document, filePath);
            GphDocumentData loadedDocument = serializer.LoadDocument(filePath);
            SupportModifierDefinition loadedModifier = loadedDocument.SupportLayerGroups[0].SupportModifiers[0];

            if (loadedModifier.ExcludedBracePairs.Count != 1
                || !loadedModifier.ExcludedBracePairs[0].Equals(excludedPair))
            {
                throw new InvalidOperationException("Expected the excluded Brace pair to survive save and load.");
            }

            if (loadedModifier.ExcludedBraceTargetBatches.Count != 1
                || loadedModifier.ExcludedBraceTargetBatches[0].TargetSupportIds.Count != 2)
            {
                throw new InvalidOperationException("Expected the compact Brace exclusion batch to survive save and load.");
            }
            if (loadedModifier.ToolSessionId != toolSessionId)
            {
                throw new InvalidOperationException("Expected the tool-launch session identity to survive save and load.");
            }
        }
        finally
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }

    /// <summary>
    /// Validates that support styles and cluster branch diameter settings are saved and loaded with the project.
    /// </summary>
    private static void ValidateSupportStyleAndClusterBranchDiameterSurviveSaveAndLoad()
    {
        CadDocument document = new CadDocument();
        MeshEntity mesh = CreateSingleTriangleMesh(
            new Vector3(0.0f, 0.0f, 2.0f),
            new Vector3(10.0f, 0.0f, 2.0f),
            new Vector3(0.0f, 10.0f, 2.0f),
            Transform3DData.Identity);
        document.AddEntity(mesh);
        SupportLayerGroup supportLayerGroup = new SupportLayerGroup(mesh.Id, "Clustered Supports");
        SupportClusterModifierSettings clusterSettings = new SupportClusterModifierSettings(
            3.0f,
            2,
            4,
            45.0f,
            SupportClusterStemSizingMode.Manual,
            1.25f,
            0.85f,
            0.45f);
        SupportEntity clusteredSupport = new SupportEntity(
            supportLayerGroup.Id,
            new Vector3(2.0f, 0.0f, 6.0f),
            new Vector3(0.0f, 0.0f, 0.0f),
            Vector3.UnitZ,
            2.0f,
            Vector3.UnitX,
            SupportDefaults.CreateProfile(),
            new ClusteredSupportStyle(1.25f, 0.85f, 0.45f));
        supportLayerGroup.SetSupportModifiers(new List<SupportModifierDefinition>
        {
            SupportModifierDefinition.CreateNew(
                SupportModifierKind.Cluster,
                0,
                clusterSettings,
                new List<Guid> { clusteredSupport.Id },
                supportLayerGroup.SourceGeneratorRevision)
        });
        document.AddSupportLayerGroup(supportLayerGroup);
        document.AddEntity(clusteredSupport);
        GphDocumentSerializer serializer = new GphDocumentSerializer();
        string filePath = Path.Combine(Environment.CurrentDirectory, "SupportStyleSmoke.gph");

        try
        {
            serializer.Save(document, filePath);

            GphDocumentData loadedDocument = serializer.LoadDocument(filePath);
            SupportEntity? loadedSupport = null;

            for (int i = 0; i < loadedDocument.Entities.Count; i++)
            {
                if (loadedDocument.Entities[i] is SupportEntity support)
                {
                    loadedSupport = support;
                    break;
                }
            }

            if (loadedSupport == null || loadedSupport.Style is not ClusteredSupportStyle loadedStyle)
            {
                throw new InvalidOperationException("Expected clustered support style to survive save and load.");
            }

            if (MathF.Abs(loadedStyle.BranchDiameter - 0.45f) > 0.0001f
                || !loadedStyle.CentralStemBottomDiameter.HasValue
                || !loadedStyle.CentralStemTopDiameter.HasValue
                || MathF.Abs(loadedStyle.CentralStemBottomDiameter.Value - 1.25f) > 0.0001f
                || MathF.Abs(loadedStyle.CentralStemTopDiameter.Value - 0.85f) > 0.0001f)
            {
                throw new InvalidOperationException("Expected clustered support style diameters to survive save and load.");
            }

            if (loadedDocument.SupportLayerGroups.Count != 1
                || loadedDocument.SupportLayerGroups[0].SupportModifiers.Count != 1
                || loadedDocument.SupportLayerGroups[0].SupportModifiers[0].ClusterSettings == null
                || MathF.Abs(loadedDocument.SupportLayerGroups[0].SupportModifiers[0].ClusterSettings!.ClusterBranchDiameter - 0.45f) > 0.0001f)
            {
                throw new InvalidOperationException("Expected Cluster modifier branch diameter to survive save and load.");
            }
        }
        finally
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }

    /// <summary>
    /// Validates that generated buttress styles and captured bracing parameters survive project persistence.
    /// </summary>
    private static void ValidateButtressStyleAndBracingSettingsSurviveSaveAndLoad()
    {
        CadDocument document = new CadDocument();
        MeshEntity mesh = CreateSingleTriangleMesh(
            new Vector3(0.0f, 0.0f, 2.0f),
            new Vector3(10.0f, 0.0f, 2.0f),
            new Vector3(0.0f, 10.0f, 2.0f),
            Transform3DData.Identity);
        document.AddEntity(mesh);
        SupportLayerGroup supportLayerGroup = new SupportLayerGroup(mesh.Id, "Buttressed Supports");
        SupportProfile profile = SupportDefaults.CreateProfile();
        SupportEntity sourceSupport = new SupportEntity(
            supportLayerGroup.Id,
            new Vector3(0.0f, 0.0f, 60.0f),
            Vector3.Zero,
            profile);
        SupportBraceModifierSettings braceSettings = new SupportBraceModifierSettings(25.0f, 68.0f, 33.0f, 0.66f);
        SupportButtressModifierSettings buttressSettings = new SupportButtressModifierSettings(35.0f, 7.0f, braceSettings);
        SupportModifierDefinition modifier = SupportModifierDefinition.CreateNewButtress(
            0,
            buttressSettings,
            new List<Guid> { sourceSupport.Id },
            supportLayerGroup.SourceGeneratorRevision);
        SupportBracingEvaluationResult evaluation = SupportBracingPlanner.EvaluateButtress(
            new List<SupportEntity> { sourceSupport },
            modifier);
        supportLayerGroup.SetSupportModifiers(new List<SupportModifierDefinition> { modifier });
        document.AddSupportLayerGroup(supportLayerGroup);

        for (int i = 0; i < evaluation.SupportEntities.Count; i++)
        {
            document.AddEntity(evaluation.SupportEntities[i]);
        }

        GphDocumentSerializer serializer = new GphDocumentSerializer();
        string filePath = Path.Combine(Environment.CurrentDirectory, "ButtressStyleSmoke.gph");

        try
        {
            serializer.Save(document, filePath);
            GphDocumentData loadedDocument = serializer.LoadDocument(filePath);
            int loadedButtressCount = 0;

            for (int i = 0; i < loadedDocument.Entities.Count; i++)
            {
                if (loadedDocument.Entities[i] is SupportEntity support
                    && support.Style is ButtressSupportStyle)
                {
                    loadedButtressCount++;
                }
            }

            if (loadedButtressCount != 2
                || loadedDocument.SupportLayerGroups.Count != 1
                || loadedDocument.SupportLayerGroups[0].SupportModifiers.Count != 1)
            {
                throw new InvalidOperationException("Expected two persisted buttress supports and one Buttress modifier.");
            }

            SupportButtressModifierSettings? loadedSettings =
                loadedDocument.SupportLayerGroups[0].SupportModifiers[0].ButtressSettings;

            if (loadedSettings == null
                || MathF.Abs(loadedSettings.MinimumButtressHeight - 35.0f) > 0.0001f
                || MathF.Abs(loadedSettings.ButtressSpacing - 7.0f) > 0.0001f
                || MathF.Abs(loadedSettings.BraceSettings.MinimumBraceAngleDegrees - 25.0f) > 0.0001f
                || MathF.Abs(loadedSettings.BraceSettings.MaximumBraceAngleDegrees - 68.0f) > 0.0001f
                || MathF.Abs(loadedSettings.BraceSettings.MaximumBraceLength - 33.0f) > 0.0001f
                || MathF.Abs(loadedSettings.BraceSettings.BraceDiameter - 0.66f) > 0.0001f)
            {
                throw new InvalidOperationException("Expected Buttress and captured Brace settings to survive save and load.");
            }
        }
        finally
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }

    /// <summary>
    /// Validates that loaded modifiers with mismatched generator revisions are discarded.
    /// </summary>
    private static void ValidateInvalidClusterModifierRevisionIsDiscardedOnLoad()
    {
        GphDocumentSerializer serializer = new GphDocumentSerializer();
        string filePath = Path.Combine(Environment.CurrentDirectory, "InvalidClusterRevisionSmoke.gph");

        try
        {
            serializer.Save(CreateClusterModifierPersistenceDocument(), filePath);
            JsonObject root = LoadJsonRoot(filePath);
            JsonObject modifier = GetFirstSupportModifierJsonObject(root);
            modifier["sourceGeneratorRevision"] = 999;
            File.WriteAllText(filePath, root.ToJsonString());

            GphDocumentData loadedDocument = serializer.LoadDocument(filePath);

            if (loadedDocument.SupportLayerGroups.Count != 1 || loadedDocument.SupportLayerGroups[0].SupportModifiers.Count != 0)
            {
                throw new InvalidOperationException("Expected source revision mismatch to discard the loaded Cluster modifier.");
            }
        }
        finally
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }

    /// <summary>
    /// Validates that loaded modifiers targeting missing supports are discarded.
    /// </summary>
    private static void ValidateInvalidClusterModifierTargetIsDiscardedOnLoad()
    {
        GphDocumentSerializer serializer = new GphDocumentSerializer();
        string filePath = Path.Combine(Environment.CurrentDirectory, "InvalidClusterTargetSmoke.gph");

        try
        {
            serializer.Save(CreateClusterModifierPersistenceDocument(), filePath);
            JsonObject root = LoadJsonRoot(filePath);
            JsonObject modifier = GetFirstSupportModifierJsonObject(root);
            string invalidTargetId = Guid.NewGuid().ToString();

            if (modifier["targetSupportIds"] is not JsonArray targetSupportIds || targetSupportIds.Count == 0)
            {
                throw new InvalidOperationException("Expected saved Cluster modifier target ids in smoke fixture.");
            }

            targetSupportIds[0] = invalidTargetId;

            if (modifier["targetSupportIdBatches"] is JsonArray targetBatches
                && targetBatches.Count > 0
                && targetBatches[0] is JsonArray firstBatch
                && firstBatch.Count > 0)
            {
                firstBatch[0] = invalidTargetId;
            }

            File.WriteAllText(filePath, root.ToJsonString());
            GphDocumentData loadedDocument = serializer.LoadDocument(filePath);

            if (loadedDocument.SupportLayerGroups.Count != 1 || loadedDocument.SupportLayerGroups[0].SupportModifiers.Count != 0)
            {
                throw new InvalidOperationException("Expected missing target ids to discard the loaded Cluster modifier.");
            }
        }
        finally
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }

    /// <summary>
    /// Creates a small document with one valid revision-bound Cluster modifier.
    /// </summary>
    private static CadDocument CreateClusterModifierPersistenceDocument()
    {
        CadDocument document = new CadDocument();
        MeshEntity mesh = CreateSingleTriangleMesh(
            new Vector3(0.0f, 0.0f, 2.0f),
            new Vector3(10.0f, 0.0f, 2.0f),
            new Vector3(0.0f, 10.0f, 2.0f),
            Transform3DData.Identity);
        document.AddEntity(mesh);
        SupportLayerGroup supportLayerGroup = new SupportLayerGroup(mesh.Id, "Invalid Modifier Fixture");
        SupportProfile profile = SupportDefaults.CreateProfile();
        List<SupportEntity> supports = new List<SupportEntity>
        {
            new SupportEntity(supportLayerGroup.Id, new Vector3(0.0f, 0.0f, 6.0f), new Vector3(0.0f, 0.0f, 0.0f), Vector3.UnitZ, 0.0f, Vector3.UnitZ, profile),
            new SupportEntity(supportLayerGroup.Id, new Vector3(1.0f, 0.0f, 6.0f), new Vector3(1.0f, 0.0f, 0.0f), Vector3.UnitZ, 0.0f, Vector3.UnitZ, profile)
        };
        supportLayerGroup.SetSupportModifiers(new List<SupportModifierDefinition>
        {
            SupportModifierDefinition.CreateNew(
                SupportModifierKind.Cluster,
                0,
                CreateSmokeClusterSettings(3.0f, 4),
                new List<Guid> { supports[0].Id, supports[1].Id },
                supportLayerGroup.SourceGeneratorRevision)
        });
        document.AddSupportLayerGroup(supportLayerGroup);

        for (int i = 0; i < supports.Count; i++)
        {
            document.AddEntity(supports[i]);
        }

        return document;
    }

    /// <summary>
    /// Reads a saved smoke-test project as mutable JSON.
    /// </summary>
    private static JsonObject LoadJsonRoot(string filePath)
    {
        JsonNode? rootNode = JsonNode.Parse(File.ReadAllText(filePath));

        if (rootNode is JsonObject root)
        {
            return root;
        }

        throw new InvalidOperationException("Expected saved smoke-test project to be a JSON object.");
    }

    /// <summary>
    /// Gets the first saved modifier object from a smoke-test project JSON tree.
    /// </summary>
    private static JsonObject GetFirstSupportModifierJsonObject(JsonObject root)
    {
        if (root["supportLayerGroups"] is JsonArray supportLayerGroups
            && supportLayerGroups.Count > 0
            && supportLayerGroups[0] is JsonObject supportLayerGroup
            && supportLayerGroup["supportModifiers"] is JsonArray supportModifiers
            && supportModifiers.Count > 0
            && supportModifiers[0] is JsonObject modifier)
        {
            return modifier;
        }

        throw new InvalidOperationException("Expected saved smoke-test project to contain one support modifier.");
    }
    /// <summary>
    /// Validates that removing file versioning does not remove the Graphite file identity check.
    /// </summary>
    private static void ValidateGphSerializerRejectsInvalidFormat()
    {
        GphDocumentSerializer serializer = new GphDocumentSerializer();
        string filePath = Path.Combine(Environment.CurrentDirectory, "InvalidFormatSmoke.gph");

        try
        {
            File.WriteAllText(filePath, "{\"format\":\"NotGraphite\",\"entities\":[],\"supportLayerGroups\":[]}");

            try
            {
                serializer.LoadDocument(filePath);
            }
            catch (InvalidDataException)
            {
                return;
            }

            throw new InvalidOperationException("Expected non-Graphite project files to be rejected.");
        }
        finally
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }

    /// <summary>
    /// Validates that a downward face coplanar with the build plate is classified as a support candidate.
    /// </summary>
    private static void ValidateHorizontalFaceAngleClassifierIncludesDownwardHorizontalFace()
    {
        MeshEntity mesh = CreateSingleTriangleMesh(
            new Vector3(0.0f, 0.0f, 0.0f),
            new Vector3(0.0f, 1.0f, 0.0f),
            new Vector3(1.0f, 0.0f, 0.0f),
            Transform3DData.Identity);

        IReadOnlyList<int> matchingTriangleIndices = HorizontalFaceAngleAnalyzer.CreateMatchingTriangleIndices(mesh, 1.0);

        if (matchingTriangleIndices.Count != 3)
        {
            throw new InvalidOperationException("Expected the downward horizontal triangle to be included.");
        }
    }

    /// <summary>
    /// Validates that an upward face coplanar with the build plate is not classified as a support candidate.
    /// </summary>
    private static void ValidateHorizontalFaceAngleClassifierExcludesUpwardHorizontalFace()
    {
        MeshEntity mesh = CreateSingleTriangleMesh(
            new Vector3(0.0f, 0.0f, 0.0f),
            new Vector3(1.0f, 0.0f, 0.0f),
            new Vector3(0.0f, 1.0f, 0.0f),
            Transform3DData.Identity);

        IReadOnlyList<int> matchingTriangleIndices = HorizontalFaceAngleAnalyzer.CreateMatchingTriangleIndices(mesh, 45.0);

        if (matchingTriangleIndices.Count != 0)
        {
            throw new InvalidOperationException("Expected the upward horizontal triangle to be excluded.");
        }
    }

    /// <summary>
    /// Validates that a vertical wall is not classified as a shallow face at the default threshold.
    /// </summary>
    private static void ValidateHorizontalFaceAngleClassifierExcludesVerticalFace()
    {
        MeshEntity mesh = CreateSingleTriangleMesh(
            new Vector3(0.0f, 0.0f, 0.0f),
            new Vector3(0.0f, 1.0f, 0.0f),
            new Vector3(0.0f, 0.0f, 1.0f),
            Transform3DData.Identity);

        IReadOnlyList<int> matchingTriangleIndices = HorizontalFaceAngleAnalyzer.CreateMatchingTriangleIndices(mesh, 45.0);

        if (matchingTriangleIndices.Count != 0)
        {
            throw new InvalidOperationException("Expected the vertical triangle to be excluded.");
        }
    }

    /// <summary>
    /// Validates that model transforms affect face-angle classification just like they affect viewport rendering.
    /// </summary>
    private static void ValidateHorizontalFaceAngleClassifierUsesMeshTransform()
    {
        Transform3DData rotationTransform = new Transform3DData(
            Vector3.Zero,
            Quaternion.CreateFromAxisAngle(Vector3.UnitX, MathF.PI / 2.0f),
            Vector3.One);
        MeshEntity mesh = CreateSingleTriangleMesh(
            new Vector3(0.0f, 0.0f, 0.0f),
            new Vector3(0.0f, 1.0f, 0.0f),
            new Vector3(1.0f, 0.0f, 0.0f),
            rotationTransform);

        IReadOnlyList<int> matchingTriangleIndices = HorizontalFaceAngleAnalyzer.CreateMatchingTriangleIndices(mesh, 45.0);

        if (matchingTriangleIndices.Count != 0)
        {
            throw new InvalidOperationException("Expected the rotated vertical triangle to be excluded.");
        }
    }

    /// <summary>
    /// Validates one scalar distance using the standard geometry tolerance.
    /// </summary>
    private static void ValidateDistanceNear(float expected, float actual, string message)
    {
        if (MathF.Abs(expected - actual) > 0.0001f)
        {
            throw new InvalidOperationException($"{message} Expected {expected}, actual {actual}.");
        }
    }

    /// <summary>
    /// Creates settings that keep every test pair feasible before neighborhood filtering.
    /// </summary>
    private static SupportBraceModifierSettings CreateNearestNeighborBraceSettings()
    {
        return new SupportBraceModifierSettings(10.0f, 50.0f, 50.0f, 0.5f);
    }

    /// <summary>
    /// Creates vertical supports at XY positions inside one support layer group.
    /// </summary>
    private static List<SupportEntity> CreateSupportsForGroup(
        Guid supportLayerGroupId,
        IReadOnlyList<Vector2> positions,
        float tipHeight,
        SupportProfile profile)
    {
        List<SupportEntity> supports = new List<SupportEntity>(positions.Count);

        for (int i = 0; i < positions.Count; i++)
        {
            Vector2 position = positions[i];
            supports.Add(new SupportEntity(
                supportLayerGroupId,
                new Vector3(position.X, position.Y, tipHeight),
                new Vector3(position.X, position.Y, 0.0f),
                profile));
        }

        return supports;
    }

    /// <summary>
    /// Creates one vertical support with an explicit identity for deterministic direction tests.
    /// </summary>
    private static SupportEntity CreateSupportForGroupWithId(
        Guid supportId,
        Guid supportLayerGroupId,
        Vector2 position,
        float tipHeight,
        SupportProfile profile)
    {
        return SupportEntity.CreateLoaded(
            supportId,
            "Support",
            supportLayerGroupId,
            new Vector3(position.X, position.Y, tipHeight),
            new Vector3(position.X, position.Y, 0.0f),
            Vector3.UnitZ,
            0.0f,
            Vector3.UnitZ,
            profile,
            SupportStyle.Individual);
    }

    /// <summary>
    /// Captures support identities in their current stable order.
    /// </summary>
    private static IReadOnlyList<Guid> CreateSupportIds(IReadOnlyList<SupportEntity> supports)
    {
        List<Guid> supportIds = new List<Guid>(supports.Count);

        for (int i = 0; i < supports.Count; i++)
        {
            supportIds.Add(supports[i].Id);
        }

        return supportIds;
    }

    /// <summary>
    /// Finds the source support whose base shares a generated member endpoint in XY.
    /// </summary>
    private static int FindSupportIndexByXy(IReadOnlyList<SupportEntity> supports, Vector3 position)
    {
        for (int i = 0; i < supports.Count; i++)
        {
            if (IsSameXy(supports[i].BasePosition, position))
            {
                return i;
            }
        }

        throw new InvalidOperationException("Generated brace endpoint did not match a source support base.");
    }

    /// <summary>
    /// Compares two positions in XY using the smoke-test geometry tolerance.
    /// </summary>
    private static bool IsSameXy(Vector3 left, Vector3 right)
    {
        float deltaX = left.X - right.X;
        float deltaY = left.Y - right.Y;
        return (deltaX * deltaX) + (deltaY * deltaY) <= 0.00000001f;
    }

    /// <summary>
    /// Recreates one support identity and geometry with a supplied durable support style.
    /// </summary>
    private static SupportEntity CreateSupportWithStyle(SupportEntity source, SupportStyle style)
    {
        return SupportEntity.CreateLoaded(
            source.Id,
            source.Name,
            source.SupportLayerGroupId,
            source.TipPosition,
            source.BasePosition,
            source.HeadDirection,
            source.BranchLength,
            source.BranchDirection,
            source.Profile,
            style);
    }

    /// <summary>
    /// Counts supports using one durable support style kind.
    /// </summary>
    private static int CountSupportsWithStyle(IReadOnlyList<SupportEntity> supports, SupportStyleKind styleKind)
    {
        int count = 0;

        for (int i = 0; i < supports.Count; i++)
        {
            if (supports[i].Style.Kind == styleKind)
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>
    /// Checks an identity list without introducing LINQ into the smoke-test hot path.
    /// </summary>
    private static bool ContainsSupportId(IReadOnlyList<Guid> supportIds, Guid supportId)
    {
        for (int i = 0; i < supportIds.Count; i++)
        {
            if (supportIds[i] == supportId)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Validates that per-stem actions apply one common delta without collapsing selected supports together.
    /// </summary>
    private static void ValidateDirectEditMultiSelectionPreservesRelativePositions()
    {
        SupportProfile profile = CreateAngledProfile(90.0f);
        SupportEntity first = CreateSupport(Vector3.Zero, new Vector3(0.0f, 0.0f, 12.0f), profile);
        SupportEntity second = CreateSupport(new Vector3(5.0f, 2.0f, 0.0f), new Vector3(5.0f, 2.0f, 12.0f), profile);
        Vector3 delta = new Vector3(3.0f, -1.0f, 0.0f);
        Guid sessionId = Guid.NewGuid();
        SupportDirectEditSettings firstSettings = new SupportDirectEditSettings(
            first.BasePosition + delta,
            SupportDirectEditPlanner.CalculateStemTop(first).Z,
            first.BasePosition,
            SupportDirectEditPlanner.CalculateStemTop(first).Z);
        SupportDirectEditSettings secondSettings = new SupportDirectEditSettings(
            second.BasePosition + delta,
            SupportDirectEditPlanner.CalculateStemTop(second).Z,
            second.BasePosition,
            SupportDirectEditPlanner.CalculateStemTop(second).Z);
        SupportModifierDefinition firstModifier = new SupportModifierDefinition(
            Guid.NewGuid(),
            SupportModifierKind.DirectEdit,
            true,
            0,
            null,
            null,
            null,
            new[] { first.Id },
            null,
            0,
            null,
            null,
            sessionId,
            firstSettings);
        SupportModifierDefinition secondModifier = new SupportModifierDefinition(
            Guid.NewGuid(),
            SupportModifierKind.DirectEdit,
            true,
            1,
            null,
            null,
            null,
            new[] { second.Id },
            null,
            0,
            null,
            null,
            sessionId,
            secondSettings);
        IReadOnlyList<SupportEntity> edited = SupportModifierPipeline.ApplyModifiers(
            new[] { first, second },
            new[] { firstModifier, secondModifier });

        ValidateVectorNear(first.BasePosition + delta, edited[0].BasePosition, 0.0001f, "The first selected stem did not receive the shared delta.");
        ValidateVectorNear(second.BasePosition + delta, edited[1].BasePosition, 0.0001f, "The second selected stem did not receive the shared delta.");
        ValidateVectorNear(
            second.BasePosition - first.BasePosition,
            edited[1].BasePosition - edited[0].BasePosition,
            0.0001f,
            "Direct Edit collapsed the relative spacing between selected stems.");
    }

    /// <summary>
    /// Validates that Direct Edit moves the vertical stem while retaining the model contact point.
    /// </summary>
    private static void ValidateDirectEditPreservesContactAndRebuildsBranch()
    {
        SupportProfile profile = CreateAngledProfile(90.0f);
        SupportEntity source = CreateSupport(Vector3.Zero, new Vector3(0.0f, 0.0f, 12.0f), profile);
        Vector3 originalTip = source.TipPosition;
        SupportDirectEditSettings settings = new SupportDirectEditSettings(
            new Vector3(2.0f, -1.0f, 0.0f),
            7.0f,
            source.BasePosition,
            SupportDirectEditPlanner.CalculateStemTop(source).Z);
        SupportEntity edited = SupportDirectEditPlanner.RebuildSupport(source, settings);

        ValidateVectorNear(originalTip, edited.TipPosition, 0.0001f, "Direct Edit changed the model contact point.");
        ValidateVectorNear(settings.BasePosition, edited.BasePosition, 0.0001f, "Direct Edit did not move the support base.");
        ValidateVectorNear(
            new Vector3(settings.BasePosition.X, settings.BasePosition.Y, settings.StemTopZ),
            SupportDirectEditPlanner.CalculateStemTop(edited),
            0.0001f,
            "Direct Edit did not rebuild the branch from the edited stem top.");
    }

    /// <summary>
    /// Validates that removing cumulative Direct Edit actions can recover generator source geometry.
    /// </summary>
    private static void ValidateDirectEditSourceRestorationReversesCumulativeEdits()
    {
        SupportProfile profile = CreateAngledProfile(90.0f);
        SupportEntity source = CreateSupport(Vector3.Zero, new Vector3(0.0f, 0.0f, 12.0f), profile);
        float originalStemTopZ = SupportDirectEditPlanner.CalculateStemTop(source).Z;
        SupportDirectEditSettings firstSettings = new SupportDirectEditSettings(
            new Vector3(1.0f, 0.0f, 0.0f),
            originalStemTopZ - 1.0f,
            source.BasePosition,
            originalStemTopZ);
        SupportModifierDefinition firstModifier = new SupportModifierDefinition(
            Guid.NewGuid(),
            SupportModifierKind.DirectEdit,
            true,
            0,
            null,
            null,
            null,
            new[] { source.Id },
            null,
            0,
            null,
            null,
            Guid.NewGuid(),
            firstSettings);
        SupportEntity firstEdited = SupportDirectEditPlanner.RebuildSupport(source, firstSettings);
        SupportDirectEditSettings secondSettings = new SupportDirectEditSettings(
            new Vector3(2.0f, 1.0f, 0.0f),
            originalStemTopZ - 2.0f,
            firstSettings.BasePosition,
            firstSettings.StemTopZ);
        SupportModifierDefinition secondModifier = new SupportModifierDefinition(
            Guid.NewGuid(),
            SupportModifierKind.DirectEdit,
            true,
            1,
            null,
            null,
            null,
            new[] { source.Id },
            null,
            0,
            null,
            null,
            firstModifier.ToolSessionId,
            secondSettings);
        SupportEntity secondEdited = SupportDirectEditPlanner.RebuildSupport(firstEdited, secondSettings);
        IReadOnlyList<SupportEntity> restored = SupportModifierSourceRestorer.Restore(
            new[] { secondEdited },
            new[] { firstModifier, secondModifier });

        if (restored.Count != 1)
        {
            throw new InvalidOperationException("Direct Edit source restoration changed the support count.");
        }

        ValidateVectorNear(source.BasePosition, restored[0].BasePosition, 0.0001f, "Direct Edit source restoration did not recover the original base.");
        ValidateVectorNear(
            SupportDirectEditPlanner.CalculateStemTop(source),
            SupportDirectEditPlanner.CalculateStemTop(restored[0]),
            0.0001f,
            "Direct Edit source restoration did not recover the original stem top.");
    }
    /// <summary>
    /// Creates one support entity with a fresh group identity.
    /// </summary>
    private static SupportEntity CreateSupport(Vector3 basePosition, Vector3 tipPosition, SupportProfile profile)
    {
        return new SupportEntity(Guid.NewGuid(), tipPosition, basePosition, profile);
    }

    /// <summary>
    /// Creates one support profile with a configurable head angle limit.
    /// </summary>
    private static SupportProfile CreateAngledProfile(float maxHeadAngleFromVerticalDegrees)
    {
        return new SupportProfile(
            SupportDefaults.DefaultBaseBottomRadius,
            SupportDefaults.DefaultBaseHeight,
            SupportDefaults.DefaultStemBottomDiameter,
            SupportDefaults.DefaultStemTopDiameter,
            SupportDefaults.DefaultMaximumBranchLength,
            SupportDefaults.DefaultModelClearance,
            SupportDefaults.DefaultBranchAngleFromVerticalDegrees,
            SupportDefaults.DefaultHeadHeight,
            SupportDefaults.DefaultHeadPenetrationDepth,
            SupportDefaults.DefaultHeadTopDiameter,
            maxHeadAngleFromVerticalDegrees);
    }

    /// <summary>
    /// Creates one support profile with configurable branch settings.
    /// </summary>
    private static SupportProfile CreateBranchProfile(float maxHeadAngleFromVerticalDegrees, float maximumBranchLength, float modelClearance)
    {
        return CreateBranchProfile(maxHeadAngleFromVerticalDegrees, maximumBranchLength, modelClearance, maxHeadAngleFromVerticalDegrees);
    }

    /// <summary>
    /// Creates one support profile with independently configurable head and branch angle settings.
    /// </summary>
    private static SupportProfile CreateBranchProfile(
        float maxHeadAngleFromVerticalDegrees,
        float maximumBranchLength,
        float modelClearance,
        float branchAngleFromVerticalDegrees)
    {
        return new SupportProfile(
            SupportDefaults.DefaultBaseBottomRadius,
            SupportDefaults.DefaultBaseHeight,
            SupportDefaults.DefaultStemBottomDiameter,
            SupportDefaults.DefaultStemTopDiameter,
            maximumBranchLength,
            modelClearance,
            branchAngleFromVerticalDegrees,
            SupportDefaults.DefaultHeadHeight,
            SupportDefaults.DefaultHeadPenetrationDepth,
            SupportDefaults.DefaultHeadTopDiameter,
            maxHeadAngleFromVerticalDegrees);
    }

    /// <summary>
    /// Validates that invalid branch profile values throw the expected exception.
    /// </summary>
    private static void ValidateProfileThrowsForBranchValues(float maximumBranchLength, float modelClearance, string fieldName)
    {
        try
        {
            SupportProfile profile = CreateBranchProfile(45.0f, maximumBranchLength, modelClearance);
            _ = profile;
        }
        catch (ArgumentOutOfRangeException)
        {
            return;
        }

        throw new InvalidOperationException($"Expected negative {fieldName} to be rejected.");
    }

    /// <summary>
    /// Validates that invalid branch angle values throw the expected exception.
    /// </summary>
    private static void ValidateProfileThrowsForBranchAngle(float branchAngleFromVerticalDegrees)
    {
        try
        {
            SupportProfile profile = CreateBranchProfile(45.0f, 4.0f, 0.5f, branchAngleFromVerticalDegrees);
            _ = profile;
        }
        catch (ArgumentOutOfRangeException)
        {
            return;
        }

        throw new InvalidOperationException("Expected invalid branch angle to be rejected.");
    }

    /// <summary>
    /// Creates a minimal mesh entity for classifier smoke tests.
    /// </summary>
    private static MeshEntity CreateSingleTriangleMesh(Vector3 first, Vector3 second, Vector3 third, Transform3DData userTransform)
    {
        return new MeshEntity(
            "Classifier test mesh",
            new List<Vector3>
            {
                first,
                second,
                third
            },
            new List<int>
            {
                0,
                1,
                2
            },
            new List<Vector3>(),
            userTransform: userTransform);
    }

    /// <summary>
    /// Creates Area Support settings that select every triangle in a mesh.
    /// </summary>
    private static AreaSupportSettings CreateAreaSupportSettingsForAllFaces(
        MeshEntity mesh,
        float spacing,
        float boundarySpacing,
        bool supportThinRegions,
        float minimumThinRegionThickness,
        float? boundaryOffset = null,
        AreaSupportFillMode fillMode = AreaSupportFillMode.HexGrid,
        int additionalOffsetCount = AreaSupportSettings.DefaultAdditionalOffsetCount,
        float? offsetSpacing = null)
    {
        int triangleCount = mesh.TriangleIndices.Count / 3;
        List<FaceSelectionKey> selectedFaces = new List<FaceSelectionKey>(triangleCount);

        for (int triangleIndex = 0; triangleIndex < triangleCount; triangleIndex++)
        {
            selectedFaces.Add(new FaceSelectionKey(mesh.Id, triangleIndex));
        }

        return new AreaSupportSettings(
            selectedFaces,
            spacing,
            boundaryOffset ?? AreaSupportSettings.CalculateDefaultBoundaryOffset(spacing),
            boundarySpacing,
            AreaSupportSettings.DefaultConcaveCornerAngleDegrees,
            supportThinRegions,
            minimumThinRegionThickness,
            fillMode,
            additionalOffsetCount,
            offsetSpacing ?? boundaryOffset ?? AreaSupportSettings.CalculateDefaultBoundaryOffset(spacing));
    }

    /// <summary>
    /// Creates a square annulus whose uniform band thickness is easy to reason about in XY.
    /// </summary>
    private static MeshEntity CreateSquareRingAreaMesh(float bandThickness)
    {
        float outer = 6.0f;
        float inner = outer - bandThickness;

        return new MeshEntity(
            "Square ring area",
            new List<Vector3>
            {
                new Vector3(-outer, -outer, 0.0f),
                new Vector3(outer, -outer, 0.0f),
                new Vector3(outer, outer, 0.0f),
                new Vector3(-outer, outer, 0.0f),
                new Vector3(-inner, -inner, 0.0f),
                new Vector3(inner, -inner, 0.0f),
                new Vector3(inner, inner, 0.0f),
                new Vector3(-inner, inner, 0.0f)
            },
            new List<int>
            {
                0,
                1,
                5,
                0,
                5,
                4,
                1,
                2,
                6,
                1,
                6,
                5,
                2,
                3,
                7,
                2,
                7,
                6,
                3,
                0,
                4,
                3,
                4,
                7
            },
            new List<Vector3>(),
            userTransform: Transform3DData.Identity);
    }

    /// <summary>
    /// Creates a very coarse square mesh that exposes long selected-boundary edges.
    /// </summary>
    private static MeshEntity CreateSquareAreaMesh(float size)
    {
        float halfSize = size * 0.5f;

        return new MeshEntity(
            "Coarse square area",
            new List<Vector3>
            {
                new Vector3(-halfSize, -halfSize, 0.0f),
                new Vector3(halfSize, -halfSize, 0.0f),
                new Vector3(halfSize, halfSize, 0.0f),
                new Vector3(-halfSize, halfSize, 0.0f)
            },
            new List<int>
            {
                0,
                1,
                2,
                0,
                2,
                3
            },
            new List<Vector3>(),
            userTransform: Transform3DData.Identity);
    }

    /// <summary>
    /// Creates a planar L-shaped selected area with one unambiguous 90-degree concave corner.
    /// </summary>
    private static MeshEntity CreateConcaveAreaMesh()
    {
        return new MeshEntity(
            "Concave area",
            new List<Vector3>
            {
                new Vector3(0.0f, 0.0f, 0.0f),
                new Vector3(10.0f, 0.0f, 0.0f),
                new Vector3(10.0f, 6.0f, 0.0f),
                new Vector3(6.0f, 6.0f, 0.0f),
                new Vector3(6.0f, 10.0f, 0.0f),
                new Vector3(0.0f, 10.0f, 0.0f)
            },
            new List<int>
            {
                0,
                1,
                2,
                0,
                2,
                3,
                0,
                3,
                5,
                3,
                4,
                5
            },
            new List<Vector3>(),
            userTransform: Transform3DData.Identity);
    }

    /// <summary>
    /// Creates two downward-facing horizontal faces stacked along Z for support projection ordering tests.
    /// </summary>
    private static MeshEntity CreateStackedDownwardHorizontalFaces()
    {
        return new MeshEntity(
            "Stacked support projection faces",
            new List<Vector3>
            {
                new Vector3(0.0f, 0.0f, 10.0f),
                new Vector3(0.0f, 1.0f, 10.0f),
                new Vector3(1.0f, 0.0f, 10.0f),
                new Vector3(0.0f, 0.0f, 14.0f),
                new Vector3(0.0f, 1.0f, 14.0f),
                new Vector3(1.0f, 0.0f, 14.0f)
            },
            new List<int>
            {
                0,
                1,
                2,
                3,
                4,
                5
            },
            new List<Vector3>(),
            userTransform: Transform3DData.Identity);
    }

    /// <summary>
    /// Creates a vertical panel that intersects the expected angled head centerline.
    /// </summary>
    private static MeshEntity CreateHeadBlockingPanelMesh()
    {
        return new MeshEntity(
            "Head blocking panel",
            new List<Vector3>
            {
                new Vector3(2.0f, -1.0f, 6.5f),
                new Vector3(2.0f, 1.0f, 6.5f),
                new Vector3(2.0f, 1.0f, 9.0f),
                new Vector3(2.0f, -1.0f, 9.0f)
            },
            new List<int>
            {
                0,
                1,
                2,
                0,
                2,
                3
            },
            new List<Vector3>(),
            userTransform: Transform3DData.Identity);
    }

    /// <summary>
    /// Creates a short panel through the direct vertical stem path while leaving room for a branch to move clear.
    /// </summary>
    private static MeshEntity CreateStemBlockingPanelMesh(float stemX)
    {
        return new MeshEntity(
            "Stem blocking panel",
            new List<Vector3>
            {
                new Vector3(stemX, 4.5f, 0.0f),
                new Vector3(stemX, 5.5f, 0.0f),
                new Vector3(stemX, 5.5f, 5.5f),
                new Vector3(stemX, 4.5f, 5.5f)
            },
            new List<int>
            {
                0,
                1,
                2,
                0,
                2,
                3
            },
            new List<Vector3>(),
            userTransform: Transform3DData.Identity);
    }

    /// <summary>
    /// Creates a side face with outward +X normals for support placement clearance tests.
    /// </summary>
    private static MeshEntity CreateSideContactPanelMesh()
    {
        return new MeshEntity(
            "Side contact panel",
            new List<Vector3>
            {
                new Vector3(0.0f, 0.0f, 0.0f),
                new Vector3(0.0f, 10.0f, 0.0f),
                new Vector3(0.0f, 10.0f, 14.0f),
                new Vector3(0.0f, 0.0f, 14.0f)
            },
            new List<int>
            {
                0,
                1,
                2,
                0,
                2,
                3
            },
            new List<Vector3>(),
            userTransform: Transform3DData.Identity);
    }

    /// <summary>
    /// Creates one vertical rectangular panel split into two triangles.
    /// </summary>
    private static MeshEntity CreateSingleVerticalPanel()
    {
        return new MeshEntity(
            "Single vertical panel",
            new List<Vector3>
            {
                new Vector3(0.0f, 0.0f, 0.0f),
                new Vector3(0.0f, 10.0f, 0.0f),
                new Vector3(0.0f, 10.0f, 10.0f),
                new Vector3(0.0f, 0.0f, 10.0f)
            },
            new List<int>
            {
                0,
                1,
                2,
                0,
                2,
                3
            },
            new List<Vector3>(),
            userTransform: Transform3DData.Identity);
    }

    /// <summary>
    /// Creates one STL-style vertical panel where adjacent triangles duplicate coincident vertices.
    /// </summary>
    private static MeshEntity CreateStlStyleSingleVerticalPanel()
    {
        return new MeshEntity(
            "STL-style single vertical panel",
            new List<Vector3>
            {
                new Vector3(0.0f, 0.0f, 0.0f),
                new Vector3(0.0f, 10.0f, 0.0f),
                new Vector3(0.0f, 10.0f, 10.0f),
                new Vector3(0.0f, 0.0f, 0.0f),
                new Vector3(0.0f, 10.0f, 10.0f),
                new Vector3(0.0f, 0.0f, 10.0f)
            },
            new List<int>
            {
                0,
                1,
                2,
                3,
                4,
                5
            },
            new List<Vector3>(),
            userTransform: Transform3DData.Identity);
    }

    /// <summary>
    /// Creates a patch where a small seed panel is connected above the slice to a larger nearby panel.
    /// </summary>
    private static MeshEntity CreateConnectedShortAndLongPanels()
    {
        return new MeshEntity(
            "Connected short and long contour panels",
            new List<Vector3>
            {
                new Vector3(0.0f, 0.0f, 0.0f),
                new Vector3(0.0f, 0.1f, 0.0f),
                new Vector3(0.0f, 0.1f, 10.0f),
                new Vector3(0.0f, 0.0f, 10.0f),
                new Vector3(0.2f, 0.0f, 0.0f),
                new Vector3(0.2f, 0.1f, 0.0f),
                new Vector3(0.2f, 10.0f, 0.0f),
                new Vector3(0.2f, 10.0f, 10.0f),
                new Vector3(0.2f, 0.1f, 10.0f),
                new Vector3(0.2f, 0.0f, 10.0f)
            },
            new List<int>
            {
                0,
                1,
                2,
                0,
                2,
                3,
                4,
                5,
                8,
                4,
                8,
                9,
                5,
                6,
                7,
                5,
                7,
                8,
                3,
                9,
                8,
                3,
                8,
                2
            },
            new List<Vector3>(),
            userTransform: Transform3DData.Identity);
    }

    /// <summary>
    /// Creates two vertical slice panels with a tiny endpoint gap but connected by top faces above the slice.
    /// </summary>
    private static MeshEntity CreateConnectedPanelWithTinySliceEndpointGap()
    {
        return new MeshEntity(
            "Connected panel with tiny slice endpoint gap",
            new List<Vector3>
            {
                new Vector3(0.0f, 0.0f, 0.0f),
                new Vector3(5.0f, 0.0f, 0.0f),
                new Vector3(5.0f, 0.0f, 10.0f),
                new Vector3(0.0f, 0.0f, 10.0f),
                new Vector3(5.0005f, 0.0f, 0.0f),
                new Vector3(10.0f, 0.0f, 0.0f),
                new Vector3(10.0f, 0.0f, 10.0f),
                new Vector3(5.0005f, 0.0f, 10.0f),
                new Vector3(0.0f, 0.1f, 10.0f),
                new Vector3(5.0f, 0.1f, 10.0f),
                new Vector3(5.0005f, 0.1f, 10.0f),
                new Vector3(10.0f, 0.1f, 10.0f)
            },
            new List<int>
            {
                0,
                1,
                2,
                0,
                2,
                3,
                4,
                5,
                6,
                4,
                6,
                7,
                3,
                2,
                9,
                3,
                9,
                8,
                2,
                7,
                10,
                2,
                10,
                9,
                7,
                6,
                11,
                7,
                11,
                10
            },
            new List<Vector3>(),
            userTransform: Transform3DData.Identity);
    }

    /// <summary>
    /// Creates two disconnected vertical panels at different X positions.
    /// </summary>
    private static MeshEntity CreateTwoDisconnectedVerticalPanels()
    {
        return new MeshEntity(
            "Disconnected vertical panels",
            new List<Vector3>
            {
                new Vector3(0.0f, 0.0f, 0.0f),
                new Vector3(0.0f, 10.0f, 0.0f),
                new Vector3(0.0f, 10.0f, 10.0f),
                new Vector3(0.0f, 0.0f, 10.0f),
                new Vector3(20.0f, 0.0f, 0.0f),
                new Vector3(20.0f, 10.0f, 0.0f),
                new Vector3(20.0f, 10.0f, 10.0f),
                new Vector3(20.0f, 0.0f, 10.0f)
            },
            new List<int>
            {
                0,
                1,
                2,
                0,
                2,
                3,
                4,
                5,
                6,
                4,
                6,
                7
            },
            new List<Vector3>(),
            userTransform: Transform3DData.Identity);
    }

    /// <summary>
    /// Creates two vertical panels sharing an edge at a 90-degree corner.
    /// </summary>
    private static MeshEntity CreateBentVerticalPanels()
    {
        return new MeshEntity(
            "Bent vertical panels",
            new List<Vector3>
            {
                new Vector3(0.0f, 0.0f, 0.0f),
                new Vector3(0.0f, 10.0f, 0.0f),
                new Vector3(0.0f, 10.0f, 10.0f),
                new Vector3(0.0f, 0.0f, 10.0f),
                new Vector3(10.0f, 10.0f, 0.0f),
                new Vector3(10.0f, 10.0f, 10.0f)
            },
            new List<int>
            {
                0,
                1,
                2,
                0,
                2,
                3,
                1,
                4,
                5,
                1,
                5,
                2
            },
            new List<Vector3>(),
            userTransform: Transform3DData.Identity);
    }

    /// <summary>
    /// Creates two bent STL-style panels where each triangle owns its own vertex records.
    /// </summary>
    private static MeshEntity CreateStlStyleBentVerticalPanels()
    {
        return new MeshEntity(
            "STL-style bent vertical panels",
            new List<Vector3>
            {
                new Vector3(0.0f, 0.0f, 0.0f),
                new Vector3(0.0f, 10.0f, 0.0f),
                new Vector3(0.0f, 10.0f, 10.0f),
                new Vector3(0.0f, 0.0f, 0.0f),
                new Vector3(0.0f, 10.0f, 10.0f),
                new Vector3(0.0f, 0.0f, 10.0f),
                new Vector3(0.0f, 10.0f, 0.0f),
                new Vector3(10.0f, 10.0f, 0.0f),
                new Vector3(10.0f, 10.0f, 10.0f),
                new Vector3(0.0f, 10.0f, 0.0f),
                new Vector3(10.0f, 10.0f, 10.0f),
                new Vector3(0.0f, 10.0f, 10.0f)
            },
            new List<int>
            {
                0,
                1,
                2,
                3,
                4,
                5,
                6,
                7,
                8,
                9,
                10,
                11
            },
            new List<Vector3>(),
            userTransform: Transform3DData.Identity);
    }

    /// <summary>
    /// Creates four connected vertical side panels that slice into a closed square loop.
    /// </summary>
    private static MeshEntity CreateOpenTopCubeSideMesh()
    {
        return new MeshEntity(
            "Open top cube sides",
            new List<Vector3>
            {
                new Vector3(0.0f, 0.0f, 0.0f),
                new Vector3(10.0f, 0.0f, 0.0f),
                new Vector3(10.0f, 10.0f, 0.0f),
                new Vector3(0.0f, 10.0f, 0.0f),
                new Vector3(0.0f, 0.0f, 10.0f),
                new Vector3(10.0f, 0.0f, 10.0f),
                new Vector3(10.0f, 10.0f, 10.0f),
                new Vector3(0.0f, 10.0f, 10.0f)
            },
            new List<int>
            {
                0,
                1,
                5,
                0,
                5,
                4,
                1,
                2,
                6,
                1,
                6,
                5,
                2,
                3,
                7,
                2,
                7,
                6,
                3,
                0,
                4,
                3,
                4,
                7
            },
            new List<Vector3>(),
            userTransform: Transform3DData.Identity);
    }

    /// <summary>
    /// Creates four connected side panels with tiny inward top-edge drift that makes their normals slightly positive in Z.
    /// </summary>
    private static MeshEntity CreateNoisyOpenTopCubeSideMesh()
    {
        const float Drift = 0.004f;

        return new MeshEntity(
            "Noisy open top cube sides",
            new List<Vector3>
            {
                new Vector3(0.0f, 0.0f, 0.0f),
                new Vector3(10.0f, 0.0f, 0.0f),
                new Vector3(10.0f, 10.0f, 0.0f),
                new Vector3(0.0f, 10.0f, 0.0f),
                new Vector3(Drift, Drift, 10.0f),
                new Vector3(10.0f - Drift, Drift, 10.0f),
                new Vector3(10.0f - Drift, 10.0f - Drift, 10.0f),
                new Vector3(Drift, 10.0f - Drift, 10.0f)
            },
            new List<int>
            {
                0,
                1,
                5,
                0,
                5,
                4,
                1,
                2,
                6,
                1,
                6,
                5,
                2,
                3,
                7,
                2,
                7,
                6,
                3,
                0,
                4,
                3,
                4,
                7
            },
            new List<Vector3>(),
            userTransform: Transform3DData.Identity);
    }

    /// <summary>
    /// Creates a vertical wall near the candidate stem path for branch clearance tests.
    /// </summary>
    private static MeshEntity CreateBlockingWallMesh(float minimumX, float maximumX, Transform3DData userTransform)
    {
        return new MeshEntity(
            "Branch clearance wall",
            new List<Vector3>
            {
                new Vector3(minimumX, -0.1f, 0.0f),
                new Vector3(maximumX, -0.1f, 0.0f),
                new Vector3(maximumX, -0.1f, 14.0f),
                new Vector3(minimumX, -0.1f, 14.0f)
            },
            new List<int>
            {
                0,
                1,
                2,
                0,
                2,
                3
            },
            new List<Vector3>(),
            userTransform: userTransform);
    }

    /// <summary>
    /// Validates basic manifold mesh rules expected by STL consumers.
    /// </summary>
    private static void ValidateClosedMesh(SupportMeshData meshData)
    {
        if (meshData.Positions.Count == 0)
        {
            throw new InvalidOperationException("The mesh did not contain positions.");
        }

        if (meshData.TriangleIndices.Count == 0 || meshData.TriangleIndices.Count % 3 != 0)
        {
            throw new InvalidOperationException("The mesh did not contain complete triangles.");
        }

        if (meshData.Normals.Count != meshData.Positions.Count)
        {
            throw new InvalidOperationException("The mesh normal count did not match the position count.");
        }

        Dictionary<EdgeKey, int> edgeUseCounts = new Dictionary<EdgeKey, int>();

        for (int i = 0; i < meshData.TriangleIndices.Count; i += 3)
        {
            int indexA = meshData.TriangleIndices[i];
            int indexB = meshData.TriangleIndices[i + 1];
            int indexC = meshData.TriangleIndices[i + 2];
            Vector3 a = GetPosition(meshData, indexA);
            Vector3 b = GetPosition(meshData, indexB);
            Vector3 c = GetPosition(meshData, indexC);

            ValidateFinite(a);
            ValidateFinite(b);
            ValidateFinite(c);
            ValidateTriangleArea(a, b, c);
            AddEdge(edgeUseCounts, a, b);
            AddEdge(edgeUseCounts, b, c);
            AddEdge(edgeUseCounts, c, a);
        }

        foreach (KeyValuePair<EdgeKey, int> edgeUseCount in edgeUseCounts)
        {
            if (edgeUseCount.Value % 2 != 0)
            {
                throw new InvalidOperationException($"Expected every mesh edge to have no open boundary, but an edge was used {edgeUseCount.Value} time(s).");
            }
        }
    }

    /// <summary>
    /// Validates that two vectors are approximately equal.
    /// </summary>
    private static void ValidateVectorNear(Vector3 expected, Vector3 actual, float tolerance, string message)
    {
        if (Vector3.Distance(expected, actual) > tolerance)
        {
            throw new InvalidOperationException(message);
        }
    }

    /// <summary>
    /// Checks whether a position lies on the generated joint ball surface.
    /// </summary>
    private static bool IsNearSphereSurface(Vector3 position, Vector3 center, float radius)
    {
        return MathF.Abs(Vector3.Distance(position, center) - radius) <= 0.0001f;
    }

    /// <summary>
    /// Validates that every triangle found on a generated ball has an outward normal.
    /// </summary>
    private static void ValidateBallNormalsPointOutward(SupportMeshData meshData, Vector3 center, float radius)
    {
        int checkedTriangleCount = 0;

        for (int i = 0; i < meshData.TriangleIndices.Count; i += 3)
        {
            int indexA = meshData.TriangleIndices[i];
            int indexB = meshData.TriangleIndices[i + 1];
            int indexC = meshData.TriangleIndices[i + 2];
            Vector3 a = GetPosition(meshData, indexA);
            Vector3 b = GetPosition(meshData, indexB);
            Vector3 c = GetPosition(meshData, indexC);

            if (!IsNearSphereSurface(a, center, radius)
                || !IsNearSphereSurface(b, center, radius)
                || !IsNearSphereSurface(c, center, radius))
            {
                continue;
            }

            Vector3 centroidDirection = ((a + b + c) / 3.0f) - center;
            Vector3 normal = meshData.Normals[indexA];

            if (Vector3.Dot(normal, centroidDirection) <= 0.0f)
            {
                throw new InvalidOperationException("Expected every joint ball triangle normal to point away from the ball center.");
            }

            checkedTriangleCount++;
        }

        if (checkedTriangleCount == 0)
        {
            throw new InvalidOperationException("Expected to find joint ball triangles in the generated support mesh.");
        }
    }

    /// <summary>
    /// Gets one indexed mesh position after validating the index.
    /// </summary>
    private static Vector3 GetPosition(SupportMeshData meshData, int index)
    {
        if (index < 0 || index >= meshData.Positions.Count)
        {
            throw new InvalidOperationException($"Triangle index {index} was outside the position buffer.");
        }

        return meshData.Positions[index];
    }

    /// <summary>
    /// Validates that one mesh position can be serialized safely.
    /// </summary>
    private static void ValidateFinite(Vector3 position)
    {
        if (!float.IsFinite(position.X) || !float.IsFinite(position.Y) || !float.IsFinite(position.Z))
        {
            throw new InvalidOperationException("The mesh contained a non-finite position.");
        }
    }

    /// <summary>
    /// Validates that one triangle has measurable area.
    /// </summary>
    private static void ValidateTriangleArea(Vector3 a, Vector3 b, Vector3 c)
    {
        Vector3 normal = Vector3.Cross(b - a, c - a);

        if (normal.LengthSquared() <= TriangleAreaTolerance)
        {
            throw new InvalidOperationException("The mesh contained a degenerate triangle.");
        }
    }

    /// <summary>
    /// Adds one undirected edge to the manifold edge-use counter.
    /// </summary>
    private static void AddEdge(Dictionary<EdgeKey, int> edgeUseCounts, Vector3 a, Vector3 b)
    {
        EdgeKey edgeKey = new EdgeKey(VertexKey.FromVector(a), VertexKey.FromVector(b));

        if (edgeUseCounts.TryGetValue(edgeKey, out int useCount))
        {
            edgeUseCounts[edgeKey] = useCount + 1;
        }
        else
        {
            edgeUseCounts.Add(edgeKey, 1);
        }
    }

    /// <summary>
    /// Stores one quantized vertex coordinate for stable topology comparison.
    /// </summary>
    private readonly struct VertexKey : IComparable<VertexKey>, IEquatable<VertexKey>
    {
        /// <summary>
        /// Creates one quantized vertex key.
        /// </summary>
        public VertexKey(long x, long y, long z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        /// <summary>
        /// Gets the quantized X component.
        /// </summary>
        public long X { get; }

        /// <summary>
        /// Gets the quantized Y component.
        /// </summary>
        public long Y { get; }

        /// <summary>
        /// Gets the quantized Z component.
        /// </summary>
        public long Z { get; }

        /// <summary>
        /// Creates a vertex key from a floating-point position.
        /// </summary>
        public static VertexKey FromVector(Vector3 position)
        {
            return new VertexKey(
                Quantize(position.X),
                Quantize(position.Y),
                Quantize(position.Z));
        }

        /// <summary>
        /// Compares this key to another key in lexicographic coordinate order.
        /// </summary>
        public int CompareTo(VertexKey other)
        {
            int xComparison = X.CompareTo(other.X);

            if (xComparison != 0)
            {
                return xComparison;
            }

            int yComparison = Y.CompareTo(other.Y);

            if (yComparison != 0)
            {
                return yComparison;
            }

            return Z.CompareTo(other.Z);
        }

        /// <summary>
        /// Checks whether two vertex keys describe the same quantized coordinate.
        /// </summary>
        public bool Equals(VertexKey other)
        {
            return X == other.X && Y == other.Y && Z == other.Z;
        }

        /// <summary>
        /// Checks whether an object is the same vertex key.
        /// </summary>
        public override bool Equals(object? obj)
        {
            return obj is VertexKey other && Equals(other);
        }

        /// <summary>
        /// Gets the hash code for dictionary lookups.
        /// </summary>
        public override int GetHashCode()
        {
            return HashCode.Combine(X, Y, Z);
        }

        /// <summary>
        /// Converts one component to a stable integer grid.
        /// </summary>
        private static long Quantize(float value)
        {
            return (long)MathF.Round(value * CoordinateQuantizationScale);
        }
    }

    /// <summary>
    /// Stores one undirected edge between two quantized vertices.
    /// </summary>
    private readonly struct EdgeKey : IEquatable<EdgeKey>
    {
        /// <summary>
        /// Creates one edge key with deterministic endpoint ordering.
        /// </summary>
        public EdgeKey(VertexKey first, VertexKey second)
        {
            if (first.CompareTo(second) <= 0)
            {
                First = first;
                Second = second;
            }
            else
            {
                First = second;
                Second = first;
            }
        }

        /// <summary>
        /// Gets the first ordered endpoint.
        /// </summary>
        public VertexKey First { get; }

        /// <summary>
        /// Gets the second ordered endpoint.
        /// </summary>
        public VertexKey Second { get; }

        /// <summary>
        /// Checks whether two edge keys describe the same undirected edge.
        /// </summary>
        public bool Equals(EdgeKey other)
        {
            return First.Equals(other.First) && Second.Equals(other.Second);
        }

        /// <summary>
        /// Checks whether an object is the same edge key.
        /// </summary>
        public override bool Equals(object? obj)
        {
            return obj is EdgeKey other && Equals(other);
        }

        /// <summary>
        /// Gets the hash code for dictionary lookups.
        /// </summary>
        public override int GetHashCode()
        {
            return HashCode.Combine(First, Second);
        }
    }
}











