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
        using FileStream stream = File.OpenRead(filePath);
        using BinaryReader reader = new BinaryReader(stream);

        stream.Position = BinaryHeaderLength;
        string name = Path.GetFileNameWithoutExtension(filePath);
        uint triangleCount = reader.ReadUInt32();
        int indexCapacity = CalculateIndexCapacity(triangleCount);
        IndexedStlMeshBuilder meshBuilder = new IndexedStlMeshBuilder(indexCapacity);

        for (uint triangleIndex = 0; triangleIndex < triangleCount; triangleIndex++)
        {
            _ = ReadVector(reader);
            Vector3 a = ReadVector(reader);
            Vector3 b = ReadVector(reader);
            Vector3 c = ReadVector(reader);
            _ = reader.ReadUInt16();

            meshBuilder.AddTriangle(a, b, c);
        }

        return new MeshEntity(name, meshBuilder.Positions, meshBuilder.TriangleIndices, filePath, originalFileName: Path.GetFileName(filePath));
    }

    /// <summary>
    /// Reads an ASCII STL file into mesh buffers and keeps the filename as the entity name.
    /// </summary>
    private static MeshEntity ReadAscii(string filePath)
    {
        IndexedStlMeshBuilder meshBuilder = new IndexedStlMeshBuilder();
        List<Vector3> pendingVertices = new List<Vector3>(3);
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
                continue;
            }

            if (!trimmed.StartsWith("vertex ", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            pendingVertices.Add(ParseVector(trimmed.Substring("vertex ".Length)));

            if (pendingVertices.Count == 3)
            {
                meshBuilder.AddTriangle(pendingVertices[0], pendingVertices[1], pendingVertices[2]);
                pendingVertices.Clear();
            }
        }

        return new MeshEntity(name, meshBuilder.Positions, meshBuilder.TriangleIndices, filePath, originalFileName: Path.GetFileName(filePath));
    }

    /// <summary>
    /// Converts the binary facet count into the list capacity required by the triangle index buffer.
    /// </summary>
    private static int CalculateIndexCapacity(uint triangleCount)
    {
        if (triangleCount > int.MaxValue / 3)
        {
            throw new InvalidDataException("The STL file contains too many triangles for an in-memory mesh.");
        }

        return (int)triangleCount * 3;
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

    /// <summary>
    /// Accumulates STL facets while reusing position indices for exactly equal local-space coordinates.
    /// </summary>
    private sealed class IndexedStlMeshBuilder
    {
        private readonly Dictionary<Vector3, int> _positionIndices;

        /// <summary>
        /// Creates an indexed STL mesh builder with optional preallocation for binary imports.
        /// </summary>
        public IndexedStlMeshBuilder(int indexCapacity = 0)
        {
            Positions = new List<Vector3>(indexCapacity);
            TriangleIndices = new List<int>(indexCapacity);
            _positionIndices = new Dictionary<Vector3, int>(indexCapacity);
        }

        /// <summary>
        /// Gets the unique local-space positions in first-seen order.
        /// </summary>
        public List<Vector3> Positions { get; }

        /// <summary>
        /// Gets triangle position indices in original STL facet order.
        /// </summary>
        public List<int> TriangleIndices { get; }

        /// <summary>
        /// Appends one facet without changing its winding or triangle ordinal.
        /// </summary>
        public void AddTriangle(Vector3 a, Vector3 b, Vector3 c)
        {
            TriangleIndices.Add(GetOrAddPosition(a));
            TriangleIndices.Add(GetOrAddPosition(b));
            TriangleIndices.Add(GetOrAddPosition(c));
        }

        /// <summary>
        /// Resolves one finite coordinate to its exact shared position index.
        /// </summary>
        private int GetOrAddPosition(Vector3 position)
        {
            if (!float.IsFinite(position.X) || !float.IsFinite(position.Y) || !float.IsFinite(position.Z))
            {
                throw new InvalidDataException("An STL facet contains a non-finite vertex position.");
            }

            if (_positionIndices.TryGetValue(position, out int positionIndex))
            {
                return positionIndex;
            }

            positionIndex = Positions.Count;
            Positions.Add(position);
            _positionIndices.Add(position, positionIndex);
            return positionIndex;
        }
    }
}
