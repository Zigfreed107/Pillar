// StlExporter.cs
// Writes renderer-agnostic CAD mesh and support geometry to STL files for external slicer workflows.
using Pillar.Core.Entities;
using Pillar.Geometry.Supports;
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text;

namespace Pillar.Geometry.Export;

/// <summary>
/// Exports model and procedural support triangle geometry to binary STL.
/// </summary>
public sealed class StlExporter
{
    private const int BinaryHeaderLength = 80;
    private const string HeaderText = "Graphite STL export";

    /// <summary>
    /// Writes one imported model and its support entities as a single binary STL mesh.
    /// </summary>
    public void ExportModelWithSupports(string filePath, MeshEntity model, IReadOnlyList<SupportEntity> supports, int supportSides)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("An export file path is required.", nameof(filePath));
        }

        if (model == null)
        {
            throw new ArgumentNullException(nameof(model));
        }

        if (supports == null)
        {
            throw new ArgumentNullException(nameof(supports));
        }

        ValidateExportFilePath(filePath);
        List<SupportMeshData> supportMeshes = BuildSupportMeshes(supports, supportSides);
        uint triangleCount = CountTriangles(model, supportMeshes);

        if (triangleCount == 0)
        {
            throw new InvalidDataException("There is no triangle geometry to export.");
        }

        using FileStream stream = File.Create(filePath);
        using BinaryWriter writer = new BinaryWriter(stream, Encoding.ASCII);

        WriteHeader(writer);
        writer.Write(triangleCount);
        WriteModelTriangles(writer, model);
        WriteSupportTriangles(writer, supportMeshes);
    }

    /// <summary>
    /// Writes only the supplied support entities as a single binary STL mesh.
    /// </summary>
    public void ExportSupports(string filePath, IReadOnlyList<SupportEntity> supports, int supportSides)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("An export file path is required.", nameof(filePath));
        }

        if (supports == null)
        {
            throw new ArgumentNullException(nameof(supports));
        }

        ValidateExportFilePath(filePath);
        List<SupportMeshData> supportMeshes = BuildSupportMeshes(supports, supportSides);
        uint triangleCount = CountSupportTriangles(supportMeshes);

        if (triangleCount == 0)
        {
            throw new InvalidDataException("The support group does not contain any support geometry to export.");
        }

        using FileStream stream = File.Create(filePath);
        using BinaryWriter writer = new BinaryWriter(stream, Encoding.ASCII);

        WriteHeader(writer);
        writer.Write(triangleCount);
        WriteSupportTriangles(writer, supportMeshes);
    }

    /// <summary>
    /// Validates that the target folder exists before opening the export stream.
    /// </summary>
    private static void ValidateExportFilePath(string filePath)
    {
        string? directoryPath = Path.GetDirectoryName(filePath);

        if (!string.IsNullOrWhiteSpace(directoryPath) && !Directory.Exists(directoryPath))
        {
            throw new DirectoryNotFoundException($"The export folder does not exist: {directoryPath}");
        }
    }

    /// <summary>
    /// Generates support meshes once so counting and writing use the exact same geometry.
    /// </summary>
    private static List<SupportMeshData> BuildSupportMeshes(IReadOnlyList<SupportEntity> supports, int supportSides)
    {
        List<SupportMeshData> supportMeshes = new List<SupportMeshData>(supports.Count);

        for (int i = 0; i < supports.Count; i++)
        {
            supportMeshes.Add(SupportMeshBuilder.Build(supports[i], supportSides));
        }

        return supportMeshes;
    }

    /// <summary>
    /// Counts all triangles and validates that the binary STL count field can represent them.
    /// </summary>
    private static uint CountTriangles(MeshEntity model, IReadOnlyList<SupportMeshData> supportMeshes)
    {
        ulong triangleCount = checked((ulong)(model.TriangleIndices.Count / 3) + CountSupportTrianglesAsUnsignedLong(supportMeshes));

        return ValidateBinaryStlTriangleCount(triangleCount);
    }

    /// <summary>
    /// Counts support triangles and validates that the binary STL count field can represent them.
    /// </summary>
    private static uint CountSupportTriangles(IReadOnlyList<SupportMeshData> supportMeshes)
    {
        ulong triangleCount = CountSupportTrianglesAsUnsignedLong(supportMeshes);

        return ValidateBinaryStlTriangleCount(triangleCount);
    }

    /// <summary>
    /// Counts support triangles using a wide integer before the binary STL uint limit is checked.
    /// </summary>
    private static ulong CountSupportTrianglesAsUnsignedLong(IReadOnlyList<SupportMeshData> supportMeshes)
    {
        ulong triangleCount = 0;

        for (int i = 0; i < supportMeshes.Count; i++)
        {
            triangleCount = checked(triangleCount + (ulong)(supportMeshes[i].TriangleIndices.Count / 3));
        }

        return triangleCount;
    }

    /// <summary>
    /// Validates the binary STL triangle-count limit and returns the serialized count value.
    /// </summary>
    private static uint ValidateBinaryStlTriangleCount(ulong triangleCount)
    {
        if (triangleCount > uint.MaxValue)
        {
            throw new InvalidDataException("The STL export contains too many triangles for the binary STL format.");
        }

        return (uint)triangleCount;
    }

    /// <summary>
    /// Writes the fixed-width binary STL header.
    /// </summary>
    private static void WriteHeader(BinaryWriter writer)
    {
        byte[] header = new byte[BinaryHeaderLength];
        byte[] textBytes = Encoding.ASCII.GetBytes(HeaderText);
        int bytesToCopy = Math.Min(textBytes.Length, header.Length);
        Array.Copy(textBytes, header, bytesToCopy);
        writer.Write(header);
    }

    /// <summary>
    /// Writes transformed imported-model triangles in their current world-space placement.
    /// </summary>
    private static void WriteModelTriangles(BinaryWriter writer, MeshEntity model)
    {
        Matrix4x4 worldTransform = model.WorldTransform;

        for (int i = 0; i < model.TriangleIndices.Count; i += 3)
        {
            Vector3 a = Vector3.Transform(model.Vertices[model.TriangleIndices[i]], worldTransform);
            Vector3 b = Vector3.Transform(model.Vertices[model.TriangleIndices[i + 1]], worldTransform);
            Vector3 c = Vector3.Transform(model.Vertices[model.TriangleIndices[i + 2]], worldTransform);
            WriteTriangle(writer, a, b, c);
        }
    }

    /// <summary>
    /// Writes generated support triangles, which are already stored in world coordinates.
    /// </summary>
    private static void WriteSupportTriangles(BinaryWriter writer, IReadOnlyList<SupportMeshData> supportMeshes)
    {
        for (int meshIndex = 0; meshIndex < supportMeshes.Count; meshIndex++)
        {
            SupportMeshData supportMesh = supportMeshes[meshIndex];

            for (int i = 0; i < supportMesh.TriangleIndices.Count; i += 3)
            {
                Vector3 a = supportMesh.Positions[supportMesh.TriangleIndices[i]];
                Vector3 b = supportMesh.Positions[supportMesh.TriangleIndices[i + 1]];
                Vector3 c = supportMesh.Positions[supportMesh.TriangleIndices[i + 2]];
                WriteTriangle(writer, a, b, c);
            }
        }
    }

    /// <summary>
    /// Writes one binary STL triangle with a recomputed normal and empty attribute bytes.
    /// </summary>
    private static void WriteTriangle(BinaryWriter writer, Vector3 a, Vector3 b, Vector3 c)
    {
        Vector3 normal = CalculateTriangleNormal(a, b, c);
        WriteVector(writer, normal);
        WriteVector(writer, a);
        WriteVector(writer, b);
        WriteVector(writer, c);
        writer.Write((ushort)0);
    }

    /// <summary>
    /// Calculates a stable normal for STL consumers, falling back for degenerate triangles.
    /// </summary>
    private static Vector3 CalculateTriangleNormal(Vector3 a, Vector3 b, Vector3 c)
    {
        Vector3 normal = Vector3.Cross(b - a, c - a);

        if (normal.LengthSquared() <= float.Epsilon)
        {
            return Vector3.UnitZ;
        }

        return Vector3.Normalize(normal);
    }

    /// <summary>
    /// Writes one STL vector as three little-endian single-precision floats.
    /// </summary>
    private static void WriteVector(BinaryWriter writer, Vector3 vector)
    {
        writer.Write(vector.X);
        writer.Write(vector.Y);
        writer.Write(vector.Z);
    }
}
