// IndexedMeshSmokeTests.cs
// Verifies STL welding, indexed topology, export geometry, and current-schema project round trips.
using Pillar.Core.Document;
using Pillar.Core.Entities;
using Pillar.Core.Import;
using Pillar.Core.Persistence;
using Pillar.Geometry.Export;
using Pillar.Geometry.Topology;
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text.Json.Nodes;

namespace Pillar.Geometry.SmokeTests;

/// <summary>
/// Runs focused correctness checks for authoritative indexed model geometry.
/// </summary>
public static class IndexedMeshSmokeTests
{
    /// <summary>
    /// Adds every indexed-mesh smoke test to the shared failure list.
    /// </summary>
    public static void Run(List<string> failures)
    {
        RunTest("ASCII STL exact welding", ValidateAsciiStlExactWelding, failures);
        RunTest("Binary STL exact welding", ValidateBinaryStlExactWelding, failures);
        RunTest("STL near positions stay separate", ValidateNearStlPositionsStaySeparate, failures);
        RunTest("Indexed adjacency uses shared position indices", ValidateIndexedAdjacency, failures);
        RunTest("Indexed topology reports boundary diagnostics", ValidateTopologyDiagnostics, failures);
        RunTest("Indexed STL export preserves triangles", ValidateIndexedStlExport, failures);
        RunTest("Indexed project topology round trip", ValidateProjectTopologyRoundTrip, failures);
        RunTest("Unversioned projects are rejected", ValidateUnversionedProjectsAreRejected, failures);
    }

    /// <summary>
    /// Verifies ASCII facets reuse exact coordinates without changing triangle order or winding.
    /// </summary>
    private static void ValidateAsciiStlExactWelding()
    {
        string filePath = CreateTemporaryPath("AsciiIndexed", ".stl");
        string stl = """
            solid indexed
              facet normal 0 0 1
                outer loop
                  vertex 0 0 0
                  vertex 1 0 0
                  vertex 1 1 0
                endloop
              endfacet
              facet normal 0 0 1
                outer loop
                  vertex 0 0 0
                  vertex 1 1 0
                  vertex 0 1 0
                endloop
              endfacet
            endsolid indexed
            """;

        try
        {
            File.WriteAllText(filePath, stl);
            MeshEntity mesh = ImportMesh(filePath);
            ValidateWeldedQuad(mesh);
        }
        finally
        {
            DeleteIfPresent(filePath);
        }
    }

    /// <summary>
    /// Verifies binary facets follow the same indexed position policy as ASCII facets.
    /// </summary>
    private static void ValidateBinaryStlExactWelding()
    {
        string filePath = CreateTemporaryPath("BinaryIndexed", ".stl");

        try
        {
            using (FileStream stream = File.Create(filePath))
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write(new byte[80]);
                writer.Write(2u);
                WriteBinaryFacet(
                    writer,
                    Vector3.UnitZ,
                    Vector3.Zero,
                    Vector3.UnitX,
                    new Vector3(1.0f, 1.0f, 0.0f));
                WriteBinaryFacet(
                    writer,
                    Vector3.UnitZ,
                    Vector3.Zero,
                    new Vector3(1.0f, 1.0f, 0.0f),
                    Vector3.UnitY);
            }

            MeshEntity mesh = ImportMesh(filePath);
            ValidateWeldedQuad(mesh);
        }
        finally
        {
            DeleteIfPresent(filePath);
        }
    }

    /// <summary>
    /// Verifies exact welding does not join coordinates that are merely close.
    /// </summary>
    private static void ValidateNearStlPositionsStaySeparate()
    {
        string filePath = CreateTemporaryPath("NearIndexed", ".stl");
        string stl = """
            solid near
              facet normal 0 0 1
                outer loop
                  vertex 0 0 0
                  vertex 1 0 0
                  vertex 0 1 0
                endloop
              endfacet
              facet normal 0 0 1
                outer loop
                  vertex 0.000001 0 0
                  vertex 0 1 0
                  vertex 1 1 0
                endloop
              endfacet
            endsolid near
            """;

        try
        {
            File.WriteAllText(filePath, stl);
            MeshEntity mesh = ImportMesh(filePath);

            if (mesh.Vertices.Count != 5)
            {
                throw new InvalidOperationException($"Expected five exact positions but found {mesh.Vertices.Count}.");
            }

            ValidateIndices(mesh.TriangleIndices, new[] { 0, 1, 2, 3, 2, 4 });
        }
        finally
        {
            DeleteIfPresent(filePath);
        }
    }

    /// <summary>
    /// Verifies full and selected-subset topology use shared authoritative edge indices.
    /// </summary>
    private static void ValidateIndexedAdjacency()
    {
        Vector3[] positions =
        {
            Vector3.Zero,
            Vector3.UnitX,
            Vector3.UnitY,
            new Vector3(1.0f, 1.0f, 0.0f)
        };
        int[] triangleIndices = { 0, 1, 2, 1, 3, 2 };
        IndexedMeshTopology topology = IndexedMeshTopology.Create(positions, triangleIndices);
        IReadOnlyList<int> firstNeighbors = topology.GetAdjacentTriangles(0);
        IReadOnlyList<int> secondNeighbors = topology.GetAdjacentTriangles(1);

        if (firstNeighbors.Count != 1
            || firstNeighbors[0] != 1
            || secondNeighbors.Count != 1
            || secondNeighbors[0] != 0)
        {
            throw new InvalidOperationException("Expected the two triangles to share one indexed edge.");
        }

        IndexedMeshTopology subsetTopology = IndexedMeshTopology.CreateForTriangles(
            positions,
            triangleIndices,
            new[] { 0 });

        if (subsetTopology.GetAdjacentTriangles(0).Count != 0
            || subsetTopology.GetAdjacentTriangles(1).Count != 0
            || subsetTopology.OpenEdgeCount != 3)
        {
            throw new InvalidOperationException("Expected subset topology to process only the requested triangle ordinal.");
        }
    }

    /// <summary>
    /// Verifies open, non-manifold, and degenerate diagnostics derive from indexed ownership.
    /// </summary>
    private static void ValidateTopologyDiagnostics()
    {
        Vector3[] openPositions = { Vector3.Zero, Vector3.UnitX, Vector3.UnitY };
        IndexedMeshTopology openTopology = IndexedMeshTopology.Create(openPositions, new[] { 0, 1, 2 });

        if (openTopology.OpenEdgeCount != 3 || openTopology.NonManifoldEdgeCount != 0)
        {
            throw new InvalidOperationException("Expected one triangle to report three open indexed edges.");
        }

        Vector3[] nonManifoldPositions =
        {
            Vector3.Zero,
            Vector3.UnitX,
            Vector3.UnitY,
            -Vector3.UnitY,
            Vector3.UnitZ
        };
        int[] nonManifoldIndices = { 0, 1, 2, 1, 0, 3, 0, 1, 4 };
        IndexedMeshTopology nonManifoldTopology = IndexedMeshTopology.Create(nonManifoldPositions, nonManifoldIndices);

        if (nonManifoldTopology.NonManifoldEdgeCount != 1)
        {
            throw new InvalidOperationException("Expected three owners of one indexed edge to report one non-manifold edge.");
        }

        Vector3[] degeneratePositions = { Vector3.Zero, Vector3.UnitX, Vector3.UnitX * 2.0f };
        IndexedMeshTopology degenerateTopology = IndexedMeshTopology.Create(degeneratePositions, new[] { 0, 1, 2 });

        if (degenerateTopology.DegenerateTriangleCount != 1)
        {
            throw new InvalidOperationException("Expected the preserved collinear triangle to be diagnosed as degenerate.");
        }
    }

    /// <summary>
    /// Verifies STL export expands indexed faces in stable order after applying the model world transform.
    /// </summary>
    private static void ValidateIndexedStlExport()
    {
        string filePath = CreateTemporaryPath("IndexedExport", ".stl");
        Vector3 translation = new Vector3(2.0f, 3.0f, 4.0f);
        Vector3[] positions =
        {
            Vector3.Zero,
            Vector3.UnitX,
            new Vector3(1.0f, 1.0f, 0.0f),
            Vector3.UnitY
        };
        MeshEntity mesh = new MeshEntity(
            "Exported indexed quad",
            positions,
            new[] { 0, 1, 2, 0, 2, 3 },
            userTransform: Transform3DData.CreateTranslation(translation));
        StlExporter exporter = new StlExporter();

        try
        {
            exporter.ExportModelWithSupports(filePath, mesh, Array.Empty<SupportEntity>(), 16);

            using FileStream stream = File.OpenRead(filePath);
            using BinaryReader reader = new BinaryReader(stream);
            stream.Position = 80;
            uint triangleCount = reader.ReadUInt32();

            if (triangleCount != 2)
            {
                throw new InvalidOperationException($"Expected two exported facets but found {triangleCount}.");
            }

            Vector3 firstNormal = ReadVector(reader);
            Vector3 firstA = ReadVector(reader);
            Vector3 firstB = ReadVector(reader);
            Vector3 firstC = ReadVector(reader);
            _ = reader.ReadUInt16();

            if (Vector3.DistanceSquared(firstNormal, Vector3.UnitZ) > 0.000001f
                || firstA != translation
                || firstB != translation + Vector3.UnitX
                || firstC != translation + new Vector3(1.0f, 1.0f, 0.0f))
            {
                throw new InvalidOperationException("Expected the first indexed triangle to export in transformed authoritative order.");
            }

            _ = ReadVector(reader);
            Vector3 secondA = ReadVector(reader);
            Vector3 secondB = ReadVector(reader);
            Vector3 secondC = ReadVector(reader);
            _ = reader.ReadUInt16();

            if (secondA != translation
                || secondB != translation + new Vector3(1.0f, 1.0f, 0.0f)
                || secondC != translation + Vector3.UnitY)
            {
                throw new InvalidOperationException("Expected the second indexed triangle to retain its authoritative ordinal.");
            }
        }
        finally
        {
            DeleteIfPresent(filePath);
        }
    }

    /// <summary>
    /// Verifies current project saves retain indexed buffers and triangle identity exactly.
    /// </summary>
    private static void ValidateProjectTopologyRoundTrip()
    {
        string filePath = CreateTemporaryPath("IndexedRoundTrip", ".gph");
        MeshEntity sourceMesh = CreateIndexedQuad();
        CadDocument document = new CadDocument();
        document.AddEntity(sourceMesh);
        GphDocumentSerializer serializer = new GphDocumentSerializer();

        try
        {
            serializer.Save(document, filePath);
            GphDocumentData loadedDocument = serializer.LoadDocument(filePath);

            if (loadedDocument.Entities.Count != 1 || loadedDocument.Entities[0] is not MeshEntity loadedMesh)
            {
                throw new InvalidOperationException("Expected one indexed model after the project round trip.");
            }

            if (loadedMesh.Id != sourceMesh.Id || loadedMesh.Vertices.Count != sourceMesh.Vertices.Count)
            {
                throw new InvalidOperationException("Expected model identity and shared position count to survive the round trip.");
            }

            ValidateIndices(loadedMesh.TriangleIndices, sourceMesh.TriangleIndices);
            IndexedMeshTopology topology = IndexedMeshTopology.Create(loadedMesh.Vertices, loadedMesh.TriangleIndices);

            if (topology.GetAdjacentTriangles(0).Count != 1)
            {
                throw new InvalidOperationException("Expected indexed adjacency to survive the project round trip.");
            }

            JsonObject root = JsonNode.Parse(File.ReadAllText(filePath))?.AsObject()
                ?? throw new InvalidOperationException("Expected a JSON project root.");

            if (root["schemaVersion"]?.GetValue<int>() != 1)
            {
                throw new InvalidOperationException("Expected the current indexed project schema version.");
            }

            JsonObject meshDto = root["entities"]?[0]?.AsObject()
                ?? throw new InvalidOperationException("Expected a saved mesh entity.");

            if (meshDto.ContainsKey("normals"))
            {
                throw new InvalidOperationException("Authoritative mesh persistence must not contain render normals.");
            }
        }
        finally
        {
            DeleteIfPresent(filePath);
        }
    }

    /// <summary>
    /// Verifies old unversioned projects fail clearly instead of loading triangle-soup topology.
    /// </summary>
    private static void ValidateUnversionedProjectsAreRejected()
    {
        string filePath = CreateTemporaryPath("UnversionedIndexed", ".gph");
        CadDocument document = new CadDocument();
        document.AddEntity(CreateIndexedQuad());
        GphDocumentSerializer serializer = new GphDocumentSerializer();

        try
        {
            serializer.Save(document, filePath);
            JsonObject root = JsonNode.Parse(File.ReadAllText(filePath))?.AsObject()
                ?? throw new InvalidOperationException("Expected a JSON project root.");
            root.Remove("schemaVersion");
            File.WriteAllText(filePath, root.ToJsonString());

            try
            {
                _ = serializer.LoadDocument(filePath);
            }
            catch (InvalidDataException)
            {
                return;
            }

            throw new InvalidOperationException("Expected an unversioned project to be rejected.");
        }
        finally
        {
            DeleteIfPresent(filePath);
        }
    }

    /// <summary>
    /// Imports one test STL and returns its mesh payload.
    /// </summary>
    private static MeshEntity ImportMesh(string filePath)
    {
        StlImporter importer = new StlImporter();

        if (importer.Import(filePath) is not MeshEntity mesh)
        {
            throw new InvalidOperationException("Expected STL import to produce a mesh entity.");
        }

        return mesh;
    }

    /// <summary>
    /// Validates the shared positions and stable triangle order of a two-facet quad.
    /// </summary>
    private static void ValidateWeldedQuad(MeshEntity mesh)
    {
        if (mesh.Vertices.Count != 4 || mesh.TriangleIndices.Count != 6)
        {
            throw new InvalidOperationException("Expected two facets to reuse four exact positions.");
        }

        ValidateIndices(mesh.TriangleIndices, new[] { 0, 1, 2, 0, 2, 3 });
    }

    /// <summary>
    /// Creates one indexed quad used by persistence tests.
    /// </summary>
    private static MeshEntity CreateIndexedQuad()
    {
        Vector3[] positions =
        {
            Vector3.Zero,
            Vector3.UnitX,
            new Vector3(1.0f, 1.0f, 0.0f),
            Vector3.UnitY
        };

        return new MeshEntity("Indexed quad", positions, new[] { 0, 1, 2, 0, 2, 3 });
    }

    /// <summary>
    /// Validates an index sequence without hiding triangle-order changes behind set comparisons.
    /// </summary>
    private static void ValidateIndices(IReadOnlyList<int> actual, IReadOnlyList<int> expected)
    {
        if (actual.Count != expected.Count)
        {
            throw new InvalidOperationException($"Expected {expected.Count} indices but found {actual.Count}.");
        }

        for (int indexPosition = 0; indexPosition < expected.Count; indexPosition++)
        {
            if (actual[indexPosition] != expected[indexPosition])
            {
                throw new InvalidOperationException($"Triangle index position {indexPosition} changed.");
            }
        }
    }

    /// <summary>
    /// Writes one binary STL facet in little-endian field order.
    /// </summary>
    private static void WriteBinaryFacet(BinaryWriter writer, Vector3 normal, Vector3 a, Vector3 b, Vector3 c)
    {
        WriteVector(writer, normal);
        WriteVector(writer, a);
        WriteVector(writer, b);
        WriteVector(writer, c);
        writer.Write((ushort)0);
    }

    /// <summary>
    /// Reads one little-endian binary STL vector.
    /// </summary>
    private static Vector3 ReadVector(BinaryReader reader)
    {
        return new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
    }

    /// <summary>
    /// Writes one binary STL vector.
    /// </summary>
    private static void WriteVector(BinaryWriter writer, Vector3 value)
    {
        writer.Write(value.X);
        writer.Write(value.Y);
        writer.Write(value.Z);
    }

    /// <summary>
    /// Creates a unique path inside the smoke-test working directory.
    /// </summary>
    private static string CreateTemporaryPath(string prefix, string extension)
    {
        return Path.Combine(Environment.CurrentDirectory, $"{prefix}-{Guid.NewGuid():N}{extension}");
    }

    /// <summary>
    /// Removes one temporary test artifact after success or failure.
    /// </summary>
    private static void DeleteIfPresent(string filePath)
    {
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }
    }

    /// <summary>
    /// Records one failed validation while allowing the remaining cases to run.
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
