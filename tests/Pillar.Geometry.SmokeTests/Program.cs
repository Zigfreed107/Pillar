// Program.cs
// Runs dependency-free geometry smoke tests for procedural support meshes so export regressions are caught early.
using Pillar.Core.Entities;
using Pillar.Core.Supports;
using Pillar.Geometry.Analysis;
using Pillar.Geometry.Supports;
using System;
using System.Collections.Generic;
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
        RunTest(failures, "Angled head support mesh is closed", ValidateAngledHeadSupportMesh);
        RunTest(failures, "Joint ball normals point outward", ValidateJointBallNormalsPointOutward);
        RunTest(failures, "Default branch setting creates no branch", ValidateDefaultBranchSettingCreatesNoBranch);
        RunTest(failures, "Branch profile fields validate non-negative values", ValidateBranchProfileFieldsValidateNonNegativeValues);
        RunTest(failures, "Branch support mesh is closed with outward balls", ValidateBranchSupportMeshIsClosedWithOutwardBalls);
        RunTest(failures, "Branch is omitted when stem is already clear", ValidateBranchIsOmittedWhenStemIsAlreadyClear);
        RunTest(failures, "Support is skipped when branch cannot clear model", ValidateSupportIsSkippedWhenBranchCannotClearModel);
        RunTest(failures, "Vertical projection returns triangle normal", ValidateVerticalProjectionReturnsTriangleNormal);
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
        MeshEntity mesh = CreateBlockingWallMesh(-10.0f, 10.0f, Transform3DData.Identity);
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
        MeshEntity mesh = CreateBlockingWallMesh(-10.0f, 1.0f, Transform3DData.Identity);
        Vector3 tipPosition = new Vector3(0.0f, 0.0f, 10.0f);
        Vector3 headDirection = SupportHeadDirectionCalculator.CreateHeadDirectionFromSurfaceNormal(Vector3.UnitX, profile);
        SupportBranchPlan branchPlan;

        if (SupportBranchPlanner.TryCreateBranchPlan(mesh, tipPosition, headDirection, profile, out branchPlan))
        {
            throw new InvalidOperationException("Expected the planner to skip a support that cannot clear the model.");
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
