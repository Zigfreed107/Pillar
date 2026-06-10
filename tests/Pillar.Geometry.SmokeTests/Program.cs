// Program.cs
// Runs dependency-free geometry smoke tests for procedural support meshes so export regressions are caught early.
using Pillar.Core.Document;
using Pillar.Core.Entities;
using Pillar.Core.Layers;
using Pillar.Core.Persistence;
using Pillar.Core.Supports;
using Pillar.Geometry.Analysis;
using Pillar.Geometry.Supports;
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;

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
        RunTest(failures, "Branch support mesh is closed with outward balls", ValidateBranchSupportMeshIsClosedWithOutwardBalls);
        RunTest(failures, "Branch is omitted when stem is already clear", ValidateBranchIsOmittedWhenStemIsAlreadyClear);
        RunTest(failures, "Support is skipped when branch cannot clear model", ValidateSupportIsSkippedWhenBranchCannotClearModel);
        RunTest(failures, "Branch may approach valid contact inside model clearance", ValidateBranchMayApproachValidContactInsideModelClearance);
        RunTest(failures, "Support placement rejects crossing angled head", ValidateSupportPlacementRejectsCrossingAngledHead);
        RunTest(failures, "Vertical projection returns triangle normal", ValidateVerticalProjectionReturnsTriangleNormal);
        RunTest(failures, "Vertical projection handles vertical side faces", ValidateVerticalProjectionHandlesVerticalSideFaces);
        RunTest(failures, "Support projection falls back to nearby vertical face", ValidateSupportProjectionFallsBackToNearbyVerticalFace);
        RunTest(failures, "Support projection fallback handles neighboring vertical face points", ValidateSupportProjectionFallbackHandlesNeighboringVerticalFacePoints);
        RunTest(failures, "Support projection fallback rejects distant vertical face", ValidateSupportProjectionFallbackRejectsDistantVerticalFace);
        RunTest(failures, "Vertical support projection chooses first exterior hit", ValidateVerticalSupportProjectionChoosesFirstExteriorHit);
        RunTest(failures, "Transform regeneration uses supportable projection", ValidateTransformRegenerationUsesSupportableProjection);
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
        RunTest(failures, "Line support settings survive save and load", ValidateLineSupportSettingsSurviveSaveAndLoad);
        RunTest(failures, "Contour support settings survive save and load", ValidateContourSupportSettingsSurviveSaveAndLoad);
        RunTest(failures, "Gph serializer rejects invalid format", ValidateGphSerializerRejectsInvalidFormat);
        RunTest(failures, "Horizontal face angle classifier includes downward horizontal faces", ValidateHorizontalFaceAngleClassifierIncludesDownwardHorizontalFace);
        RunTest(failures, "Horizontal face angle classifier excludes upward horizontal faces", ValidateHorizontalFaceAngleClassifierExcludesUpwardHorizontalFace);
        RunTest(failures, "Horizontal face angle classifier excludes vertical faces", ValidateHorizontalFaceAngleClassifierExcludesVerticalFace);
        RunTest(failures, "Horizontal face angle classifier uses mesh transforms", ValidateHorizontalFaceAngleClassifierUsesMeshTransform);

        if (failures.Count > 0)
        {
            Console.Error.WriteLine("Support mesh smoke tests failed:");

            for (int i = 0; i < failures.Count; i++)
            {
                Console.Error.WriteLine(failures[i]);
            }

            return 1;
        }

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
        Vector3 branchDirection = SupportBranchPlanner.CreateMaximumAngleBranchDirection(headDirection, profile);
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
    /// Validates that the branch may approach the model contact while the vertical stem still observes model clearance.
    /// </summary>
    private static void ValidateBranchMayApproachValidContactInsideModelClearance()
    {
        SupportProfile profile = CreateBranchProfile(45.0f, 6.0f, 4.0f);
        MeshEntity mesh = CreateSideContactPanelMesh();
        SupportPlacementPlan placementPlan;

        if (!SupportPlacementPlanner.TryCreatePlacement(mesh, new Vector3(0.0f, 5.0f, 10.0f), Vector3.UnitX, profile, out placementPlan))
        {
            throw new InvalidOperationException("Expected a branch to move the vertical stem clear while still approaching the model contact.");
        }

        if (placementPlan.BranchLength <= 0.0f)
        {
            throw new InvalidOperationException("Expected this placement to require a branch.");
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
        return new SupportProfile(
            SupportDefaults.DefaultBaseBottomRadius,
            SupportDefaults.DefaultBaseHeight,
            SupportDefaults.DefaultStemBottomDiameter,
            SupportDefaults.DefaultStemTopDiameter,
            maximumBranchLength,
            modelClearance,
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
