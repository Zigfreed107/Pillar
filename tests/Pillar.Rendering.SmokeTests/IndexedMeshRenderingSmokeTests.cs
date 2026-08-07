// IndexedMeshRenderingSmokeTests.cs
// Verifies indexed authoritative triangles expand into flat-shaded render geometry without losing triangle identity.
using HelixToolkit.Maths;
using HelixToolkit.SharpDX;
using HelixToolkit.Wpf.SharpDX;
using Pillar.Core.Entities;
using Pillar.Rendering.EntityRenderers;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace Pillar.Rendering.SmokeTests;

/// <summary>
/// Runs focused rendering checks for authoritative indexed meshes.
/// </summary>
public static class IndexedMeshRenderingSmokeTests
{
    /// <summary>
    /// Adds indexed rendering failures to the shared smoke-test result.
    /// </summary>
    public static void Run(List<string> failures)
    {
        RunTest("Indexed model renders flat shared corners", ValidateIndexedModelRendersFlatSharedCorners, failures);
        RunTest("Selected face keeps authoritative triangle identity", ValidateSelectedFaceTriangleIdentity, failures);
    }

    /// <summary>
    /// Verifies one shared authoritative position expands to different render normals at a hard corner.
    /// </summary>
    private static void ValidateIndexedModelRendersFlatSharedCorners()
    {
        MeshEntity mesh = CreateHardCornerMesh();
        GroupModel3D group = MeshRenderer.Create(mesh);
        MeshGeometry3D geometry = GetBaseGeometry(group);

        if (mesh.Vertices.Count != 4
            || geometry.Positions == null
            || geometry.Indices == null
            || geometry.Normals == null
            || geometry.Positions.Count != 6
            || geometry.Indices.Count != 6
            || geometry.Normals.Count != 6)
        {
            throw new InvalidOperationException("Expected two authoritative indexed faces to expand into six render vertices.");
        }

        for (int indexPosition = 0; indexPosition < geometry.Indices.Count; indexPosition++)
        {
            if (geometry.Indices[indexPosition] != indexPosition)
            {
                throw new InvalidOperationException("Expected sequential render indices after flat expansion.");
            }
        }

        ValidateNormal(Vector3.UnitZ, geometry.Normals[0]);
        ValidateNormal(Vector3.UnitZ, geometry.Normals[2]);
        ValidateNormal(Vector3.UnitY, geometry.Normals[3]);
        ValidateNormal(Vector3.UnitY, geometry.Normals[5]);

        if (geometry.Positions[0] != Vector3.Zero || geometry.Positions[3] != Vector3.Zero)
        {
            throw new InvalidOperationException("Expected the shared authoritative corner to expand once for each face.");
        }
    }

    /// <summary>
    /// Verifies a domain triangle ordinal selects the matching expanded render triangle.
    /// </summary>
    private static void ValidateSelectedFaceTriangleIdentity()
    {
        MeshEntity mesh = CreateHardCornerMesh();
        GroupModel3D group = MeshRenderer.Create(mesh);
        MeshRenderer.ApplyFaceSelection(
            group,
            mesh,
            new[] { 1 },
            new Color4(1.0f, 0.2f, 0.1f, 0.8f));
        MeshGeometryModel3D selectionModel = MeshRenderer.GetFaceSelectionModel(group)
            ?? throw new InvalidOperationException("Expected a selected-face overlay.");
        MeshGeometry3D geometry = selectionModel.Geometry as MeshGeometry3D
            ?? throw new InvalidOperationException("Expected selected-face mesh geometry.");

        if (geometry.Positions == null
            || geometry.Normals == null
            || geometry.Positions.Count != 3
            || geometry.Positions[0] != Vector3.Zero
            || geometry.Positions[1] != Vector3.UnitZ
            || geometry.Positions[2] != Vector3.UnitX)
        {
            throw new InvalidOperationException("Expected authoritative triangle one to produce the selected render face.");
        }

        ValidateNormal(Vector3.UnitY, geometry.Normals[0]);
    }

    /// <summary>
    /// Creates two perpendicular triangles sharing one authoritative edge.
    /// </summary>
    private static MeshEntity CreateHardCornerMesh()
    {
        Vector3[] positions =
        {
            Vector3.Zero,
            Vector3.UnitX,
            Vector3.UnitY,
            Vector3.UnitZ
        };
        int[] triangleIndices = { 0, 1, 2, 0, 3, 1 };
        return new MeshEntity("Hard corner", positions, triangleIndices);
    }

    /// <summary>
    /// Gets the selectable base geometry from one imported model visual.
    /// </summary>
    private static MeshGeometry3D GetBaseGeometry(GroupModel3D group)
    {
        MeshGeometryModel3D meshModel = MeshRenderer.GetMeshModel(group)
            ?? throw new InvalidOperationException("Expected an imported model visual.");
        return meshModel.Geometry as MeshGeometry3D
            ?? throw new InvalidOperationException("Expected imported mesh geometry.");
    }

    /// <summary>
    /// Validates one normalized render normal.
    /// </summary>
    private static void ValidateNormal(Vector3 expected, Vector3 actual)
    {
        if (Vector3.DistanceSquared(expected, actual) > 0.000001f)
        {
            throw new InvalidOperationException($"Expected render normal {expected} but found {actual}.");
        }
    }

    /// <summary>
    /// Records one failed check while allowing the remaining rendering tests to run.
    /// </summary>
    private static void RunTest(string name, Action test, List<string> failures)
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
}
