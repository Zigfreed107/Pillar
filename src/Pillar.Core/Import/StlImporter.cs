// StlImporter.cs
// Converts STL files into MeshEntity document data while keeping file parsing out of UI and rendering layers.
using Pillar.Core.Entities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Numerics;

namespace Pillar.Core.Import;

/// <summary>
/// Imports binary and ASCII STL files into document mesh entities.
/// </summary>
public class StlImporter : IModelImporter
{
    private const int BinaryHeaderLength = 80;
    private const int BinaryTriangleLength = 50;

    /// <summary>
    /// Imports an STL file into a mesh entity named after the source filename.
    /// </summary>
    public CadEntity Import(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("A file path is required.", nameof(filePath));
        }

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("The STL file could not be found.", filePath);
        }

        MeshEntity mesh = IsBinaryStl(filePath)
            ? ReadBinary(filePath)
            : ReadAscii(filePath);

        if (mesh.TriangleIndices.Count == 0)
        {
            throw new InvalidDataException("The STL file did not contain any triangles.");
        }

        return mesh;
    }

    /// <summary>
    /// Detects binary STL files by checking whether the file length matches the binary triangle count.
    /// </summary>
    private static bool IsBinaryStl(string filePath)
    {
        long length = new FileInfo(filePath).Length;

        if (length < BinaryHeaderLength + sizeof(uint))
        {
            return false;
        }

        using FileStream stream = File.OpenRead(filePath);
        stream.Position = BinaryHeaderLength;

        using BinaryReader reader = new BinaryReader(stream);
        uint triangleCount = reader.ReadUInt32();
        long expectedLength = BinaryHeaderLength + sizeof(uint) + triangleCount * BinaryTriangleLength;

        return expectedLength == length;
    }

    /// <summary>
    /// Reads a binary STL file into mesh buffers without retaining STL header metadata as the entity name.
    /// </summary>
    private static MeshEntity ReadBinary(string filePath)
    {
        List<Vector3> vertices = new List<Vector3>();
        List<int> indices = new List<int>();
        List<Vector3> normals = new List<Vector3>();

        using FileStream stream = File.OpenRead(filePath);
        using BinaryReader reader = new BinaryReader(stream);

        stream.Position = BinaryHeaderLength;
        string name = Path.GetFileNameWithoutExtension(filePath);
        uint triangleCount = reader.ReadUInt32();

        for (uint triangleIndex = 0; triangleIndex < triangleCount; triangleIndex++)
        {
            Vector3 normal = ReadVector(reader);
            Vector3 a = ReadVector(reader);
            Vector3 b = ReadVector(reader);
            Vector3 c = ReadVector(reader);
            _ = reader.ReadUInt16();

            AddTriangle(vertices, indices, normals, a, b, c, normal);
        }

        return new MeshEntity(name, vertices, indices, normals, filePath);
    }

    /// <summary>
    /// Reads an ASCII STL file into mesh buffers and keeps the filename as the entity name.
    /// </summary>
    private static MeshEntity ReadAscii(string filePath)
    {
        List<Vector3> vertices = new List<Vector3>();
        List<int> indices = new List<int>();
        List<Vector3> normals = new List<Vector3>();
        List<Vector3> pendingVertices = new List<Vector3>(3);
        Vector3 currentNormal = Vector3.Zero;
        string name = Path.GetFileNameWithoutExtension(filePath);

        using StreamReader reader = new StreamReader(filePath);

        while (reader.ReadLine() is string line)
        {
            string trimmed = line.Trim();

            if (trimmed.Length == 0)
            {
                continue;
            }

            if (trimmed.StartsWith("solid ", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (trimmed.StartsWith("facet normal ", StringComparison.OrdinalIgnoreCase))
            {
                currentNormal = ParseVector(trimmed.Substring("facet normal ".Length));
                continue;
            }

            if (!trimmed.StartsWith("vertex ", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            pendingVertices.Add(ParseVector(trimmed.Substring("vertex ".Length)));

            if (pendingVertices.Count == 3)
            {
                AddTriangle(vertices, indices, normals, pendingVertices[0], pendingVertices[1], pendingVertices[2], currentNormal);
                pendingVertices.Clear();
            }
        }

        return new MeshEntity(name, vertices, indices, normals, filePath);
    }

    /// <summary>
    /// Appends one STL triangle to the mesh buffers and computes a fallback normal when required.
    /// </summary>
    private static void AddTriangle(
        List<Vector3> vertices,
        List<int> indices,
        List<Vector3> normals,
        Vector3 a,
        Vector3 b,
        Vector3 c,
        Vector3 normal)
    {
        if (normal.LengthSquared() < float.Epsilon)
        {
            Vector3 calculatedNormal = Vector3.Cross(b - a, c - a);
            normal = calculatedNormal.LengthSquared() < float.Epsilon
                ? Vector3.UnitZ
                : Vector3.Normalize(calculatedNormal);
        }

        int firstIndex = vertices.Count;
        vertices.Add(a);
        vertices.Add(b);
        vertices.Add(c);

        indices.Add(firstIndex);
        indices.Add(firstIndex + 1);
        indices.Add(firstIndex + 2);

        normals.Add(normal);
        normals.Add(normal);
        normals.Add(normal);
    }

    /// <summary>
    /// Reads one little-endian STL vector from the binary stream.
    /// </summary>
    private static Vector3 ReadVector(BinaryReader reader)
    {
        return new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
    }

    /// <summary>
    /// Parses one ASCII STL vector using invariant-culture floating-point values.
    /// </summary>
    private static Vector3 ParseVector(string value)
    {
        string[] parts = value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length != 3)
        {
            throw new InvalidDataException($"Expected three STL vector components but found {parts.Length}.");
        }

        return new Vector3(
            float.Parse(parts[0], CultureInfo.InvariantCulture),
            float.Parse(parts[1], CultureInfo.InvariantCulture),
            float.Parse(parts[2], CultureInfo.InvariantCulture));
    }
}
