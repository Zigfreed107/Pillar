// GphDocumentSerializer.cs
// Saves and loads Graphite CAD documents using a self-contained JSON-based .gph file format.
using Pillar.Core.Document;
using Pillar.Core.Entities;
using Pillar.Core.Layers;
using Pillar.Core.Supports;
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text.Json;

namespace Pillar.Core.Persistence;

/// <summary>
/// Serializes CAD documents to and from the Graphite project file format.
/// </summary>
public sealed class GphDocumentSerializer
{
    private const string FormatName = "Graphite";
    private const int CurrentVersion = 4;
    private const int MinimumSupportedVersion = 1;
    private const string LineTypeName = "line";
    private const string MeshTypeName = "mesh";
    private const string SupportTypeName = "support";
    private const string DefaultSupportGroupName = "Supports Group 1";

    private static readonly JsonSerializerOptions SerializerOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    /// <summary>
    /// Saves all supported entities from a CAD document into a self-contained .gph file.
    /// </summary>
    public void Save(CadDocument document, string filePath)
    {
        if (document == null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("A save file path is required.", nameof(filePath));
        }

        GphDocumentDto dto = CreateDocumentDto(document);

        using FileStream stream = File.Create(filePath);
        JsonSerializer.Serialize(stream, dto, SerializerOptions);
    }

    /// <summary>
    /// Loads supported entities from a .gph file without mutating the current document.
    /// </summary>
    public IReadOnlyList<CadEntity> Load(string filePath)
    {
        return LoadDocument(filePath).Entities;
    }

    /// <summary>
    /// Loads supported entities and layer metadata from a .gph file without mutating the current document.
    /// </summary>
    public GphDocumentData LoadDocument(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("An open file path is required.", nameof(filePath));
        }

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("The Graphite project file could not be found.", filePath);
        }

        GphDocumentDto? documentDto;

        try
        {
            using FileStream stream = File.OpenRead(filePath);
            documentDto = JsonSerializer.Deserialize<GphDocumentDto>(stream, SerializerOptions);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("The Graphite project file is not valid JSON.", ex);
        }

        if (documentDto == null)
        {
            throw new InvalidDataException("The Graphite project file is empty.");
        }

        ValidateDocumentHeader(documentDto);

        List<CadEntity> entities = new List<CadEntity>();
        HashSet<Guid> entityIds = new HashSet<Guid>();

        List<GphEntityDto> deferredSupportEntities = new List<GphEntityDto>();

        foreach (GphEntityDto entityDto in documentDto.Entities)
        {
            if (entityDto == null)
            {
                throw new InvalidDataException("The Graphite project file contains an empty entity entry.");
            }

            ValidateEntityHeader(entityDto);

            if (!entityIds.Add(entityDto.Id))
            {
                throw new InvalidDataException($"The Graphite project file contains duplicate entity id '{entityDto.Id}'.");
            }

            if (string.Equals(entityDto.Type, SupportTypeName, StringComparison.OrdinalIgnoreCase))
            {
                deferredSupportEntities.Add(entityDto);
            }
            else
            {
                entities.Add(CreateEntity(entityDto));
            }
        }

        List<SupportLayerGroup> supportLayerGroups = CreateSupportLayerGroups(documentDto, entities);
        AddDefaultSupportGroupsForMeshesWithoutGroups(entities, supportLayerGroups);
        AddSupportEntities(deferredSupportEntities, supportLayerGroups, entities);

        return new GphDocumentData(entities, supportLayerGroups);
    }

    /// <summary>
    /// Converts a document into the current persisted file shape.
    /// </summary>
    private static GphDocumentDto CreateDocumentDto(CadDocument document)
    {
        GphDocumentDto dto = new GphDocumentDto
        {
            Format = FormatName,
            Version = CurrentVersion
        };

        foreach (CadEntity entity in document.Entities)
        {
            dto.Entities.Add(CreateEntityDto(entity));
        }

        foreach (SupportLayerGroup supportLayerGroup in document.SupportLayerGroups)
        {
            dto.SupportLayerGroups.Add(CreateSupportLayerGroupDto(supportLayerGroup));
        }

        return dto;
    }

    /// <summary>
    /// Converts one supported entity into its persisted representation.
    /// </summary>
    private static GphEntityDto CreateEntityDto(CadEntity entity)
    {
        if (entity is global::LineEntity line)
        {
            return new GphEntityDto
            {
                Type = LineTypeName,
                Id = line.Id,
                Name = line.Name,
                Start = CreateVectorDto(line.Start),
                End = CreateVectorDto(line.End)
            };
        }

        if (entity is MeshEntity mesh)
        {
            GphEntityDto dto = new GphEntityDto
            {
                Type = MeshTypeName,
                Id = mesh.Id,
                Name = mesh.Name,
                SourcePath = mesh.SourcePath,
                TriangleIndices = new List<int>(mesh.TriangleIndices)
            };

            foreach (Vector3 vertex in mesh.Vertices)
            {
                dto.Vertices.Add(CreateVectorDto(vertex));
            }

            foreach (Vector3 normal in mesh.Normals)
            {
                dto.Normals.Add(CreateVectorDto(normal));
            }

            return dto;
        }

        if (entity is SupportEntity support)
        {
            return new GphEntityDto
            {
                Type = SupportTypeName,
                Id = support.Id,
                Name = support.Name,
                SupportLayerGroupId = support.SupportLayerGroupId,
                TipPosition = CreateVectorDto(support.TipPosition),
                BasePosition = CreateVectorDto(support.BasePosition),
                SupportProfile = CreateSupportProfileDto(support.Profile)
            };
        }

        throw new NotSupportedException($"Saving entity type '{entity.GetType().Name}' is not supported by the .gph format.");
    }

    /// <summary>
    /// Converts one support group into its persisted representation.
    /// </summary>
    private static GphSupportLayerGroupDto CreateSupportLayerGroupDto(SupportLayerGroup supportLayerGroup)
    {
        return new GphSupportLayerGroupDto
        {
            Id = supportLayerGroup.Id,
            ModelEntityId = supportLayerGroup.ModelEntityId,
            Name = supportLayerGroup.Name,
            Color = CreateSupportLayerColorDto(supportLayerGroup.Color)
        };
    }

    /// <summary>
    /// Validates the file header before reading entity payloads.
    /// </summary>
    private static void ValidateDocumentHeader(GphDocumentDto documentDto)
    {
        if (!string.Equals(documentDto.Format, FormatName, StringComparison.Ordinal))
        {
            throw new InvalidDataException("The selected file is not a Graphite project file.");
        }

        if (documentDto.Version < MinimumSupportedVersion || documentDto.Version > CurrentVersion)
        {
            throw new NotSupportedException($"Graphite project file version {documentDto.Version} is not supported.");
        }

        if (documentDto.Entities == null)
        {
            throw new InvalidDataException("The Graphite project file is missing its entity list.");
        }
    }

    /// <summary>
    /// Recreates support groups from saved layer metadata.
    /// </summary>
    private static List<SupportLayerGroup> CreateSupportLayerGroups(GphDocumentDto documentDto, IReadOnlyList<CadEntity> entities)
    {
        List<SupportLayerGroup> supportLayerGroups = new List<SupportLayerGroup>();

        if (documentDto.SupportLayerGroups == null)
        {
            return supportLayerGroups;
        }

        HashSet<Guid> groupIds = new HashSet<Guid>();
        HashSet<Guid> meshEntityIds = CreateMeshEntityIdSet(entities);

        foreach (GphSupportLayerGroupDto supportLayerGroupDto in documentDto.SupportLayerGroups)
        {
            ValidateSupportLayerGroup(supportLayerGroupDto, groupIds, meshEntityIds);

            supportLayerGroups.Add(SupportLayerGroup.CreateLoaded(
                supportLayerGroupDto.Id,
                supportLayerGroupDto.ModelEntityId,
                supportLayerGroupDto.Name,
                CreateSupportLayerColorOrDefault(supportLayerGroupDto)));
        }

        return supportLayerGroups;
    }

    /// <summary>
    /// Validates saved support group metadata before it reaches the document.
    /// </summary>
    private static void ValidateSupportLayerGroup(
        GphSupportLayerGroupDto supportLayerGroupDto,
        HashSet<Guid> groupIds,
        HashSet<Guid> meshEntityIds)
    {
        if (supportLayerGroupDto == null)
        {
            throw new InvalidDataException("The Graphite project file contains an empty support group entry.");
        }

        if (supportLayerGroupDto.Id == Guid.Empty)
        {
            throw new InvalidDataException("A saved support group is missing a valid identifier.");
        }

        if (!groupIds.Add(supportLayerGroupDto.Id))
        {
            throw new InvalidDataException($"The Graphite project file contains duplicate support group id '{supportLayerGroupDto.Id}'.");
        }

        if (!meshEntityIds.Contains(supportLayerGroupDto.ModelEntityId))
        {
            throw new InvalidDataException("A saved support group references an imported model that is not in the project.");
        }
    }

    /// <summary>
    /// Creates the mesh id lookup used to validate support group ownership.
    /// </summary>
    private static HashSet<Guid> CreateMeshEntityIdSet(IReadOnlyList<CadEntity> entities)
    {
        HashSet<Guid> meshEntityIds = new HashSet<Guid>();

        foreach (CadEntity entity in entities)
        {
            if (entity is MeshEntity)
            {
                meshEntityIds.Add(entity.Id);
            }
        }

        return meshEntityIds;
    }

    /// <summary>
    /// Gives legacy projects and partially populated files a default support group under each imported model.
    /// </summary>
    private static void AddDefaultSupportGroupsForMeshesWithoutGroups(
        IReadOnlyList<CadEntity> entities,
        List<SupportLayerGroup> supportLayerGroups)
    {
        HashSet<Guid> modelIdsWithGroups = new HashSet<Guid>();

        foreach (SupportLayerGroup supportLayerGroup in supportLayerGroups)
        {
            modelIdsWithGroups.Add(supportLayerGroup.ModelEntityId);
        }

        foreach (CadEntity entity in entities)
        {
            if (entity is MeshEntity && !modelIdsWithGroups.Contains(entity.Id))
            {
                supportLayerGroups.Add(new SupportLayerGroup(
                    entity.Id,
                    DefaultSupportGroupName,
                    SupportLayerColorGenerator.CreateFromStableSeed(entity.Id)));

                modelIdsWithGroups.Add(entity.Id);
            }
        }
    }

    /// <summary>
    /// Recreates saved support entities after support group ownership has been validated and restored.
    /// </summary>
    private static void AddSupportEntities(
        IReadOnlyList<GphEntityDto> deferredSupportEntities,
        IReadOnlyList<SupportLayerGroup> supportLayerGroups,
        List<CadEntity> entities)
    {
        HashSet<Guid> supportLayerGroupIds = new HashSet<Guid>();

        foreach (SupportLayerGroup supportLayerGroup in supportLayerGroups)
        {
            supportLayerGroupIds.Add(supportLayerGroup.Id);
        }

        foreach (GphEntityDto supportEntityDto in deferredSupportEntities)
        {
            entities.Add(CreateSupportEntity(supportEntityDto, supportLayerGroupIds));
        }
    }

    /// <summary>
    /// Converts one saved entity DTO into a runtime CAD entity.
    /// </summary>
    private static CadEntity CreateEntity(GphEntityDto entityDto)
    {
        ValidateEntityHeader(entityDto);

        if (string.Equals(entityDto.Type, LineTypeName, StringComparison.OrdinalIgnoreCase))
        {
            return CreateLineEntity(entityDto);
        }

        if (string.Equals(entityDto.Type, MeshTypeName, StringComparison.OrdinalIgnoreCase))
        {
            return CreateMeshEntity(entityDto);
        }

        if (string.Equals(entityDto.Type, SupportTypeName, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Support entities must be created after support groups are loaded.");
        }

        throw new NotSupportedException($"Graphite project entity type '{entityDto.Type}' is not supported.");
    }

    /// <summary>
    /// Validates common entity fields shared by all persisted entities.
    /// </summary>
    private static void ValidateEntityHeader(GphEntityDto entityDto)
    {
        if (entityDto.Id == Guid.Empty)
        {
            throw new InvalidDataException("A saved entity is missing a valid identifier.");
        }

        if (string.IsNullOrWhiteSpace(entityDto.Type))
        {
            throw new InvalidDataException("A saved entity is missing its type.");
        }
    }

    /// <summary>
    /// Recreates a line entity from saved vector data.
    /// </summary>
    private static global::LineEntity CreateLineEntity(GphEntityDto entityDto)
    {
        if (entityDto.Start == null || entityDto.End == null)
        {
            throw new InvalidDataException("A saved line entity is missing its start or end point.");
        }

        return global::LineEntity.CreateLoaded(
            entityDto.Id,
            entityDto.Name,
            CreateVector(entityDto.Start),
            CreateVector(entityDto.End));
    }

    /// <summary>
    /// Recreates a mesh entity from embedded vertex, index, and normal buffers.
    /// </summary>
    private static MeshEntity CreateMeshEntity(GphEntityDto entityDto)
    {
        List<Vector3> vertices = CreateVectorList(entityDto.Vertices, "vertices");
        List<Vector3> normals = CreateVectorList(entityDto.Normals, "normals");

        if (entityDto.TriangleIndices == null)
        {
            throw new InvalidDataException("A saved mesh entity is missing its triangle indices.");
        }

        List<int> triangleIndices = new List<int>(entityDto.TriangleIndices);

        return MeshEntity.CreateLoaded(
            entityDto.Id,
            entityDto.Name,
            vertices,
            triangleIndices,
            normals,
            entityDto.SourcePath);
    }

    /// <summary>
    /// Recreates a support entity after validating its owning support group and profile payload.
    /// </summary>
    private static SupportEntity CreateSupportEntity(GphEntityDto entityDto, HashSet<Guid> supportLayerGroupIds)
    {
        if (!entityDto.SupportLayerGroupId.HasValue || entityDto.SupportLayerGroupId.Value == Guid.Empty)
        {
            throw new InvalidDataException("A saved support entity is missing its owning support layer group id.");
        }

        if (!supportLayerGroupIds.Contains(entityDto.SupportLayerGroupId.Value))
        {
            throw new InvalidDataException("A saved support entity references a support layer group that is not in the project.");
        }

        if (entityDto.TipPosition == null || entityDto.BasePosition == null)
        {
            throw new InvalidDataException("A saved support entity is missing its tip or base position.");
        }

        if (entityDto.SupportProfile == null)
        {
            throw new InvalidDataException("A saved support entity is missing its support profile.");
        }

        return SupportEntity.CreateLoaded(
            entityDto.Id,
            entityDto.Name,
            entityDto.SupportLayerGroupId.Value,
            CreateVector(entityDto.TipPosition),
            CreateVector(entityDto.BasePosition),
            CreateSupportProfile(entityDto.SupportProfile));
    }

    /// <summary>
    /// Converts one runtime vector into serializable numeric components.
    /// </summary>
    private static GphVector3Dto CreateVectorDto(Vector3 vector)
    {
        return new GphVector3Dto
        {
            X = vector.X,
            Y = vector.Y,
            Z = vector.Z
        };
    }

    /// <summary>
    /// Converts one runtime support profile into serializable numeric components.
    /// </summary>
    private static GphSupportProfileDto CreateSupportProfileDto(SupportProfile profile)
    {
        return new GphSupportProfileDto
        {
            TipDiameter = profile.TipDiameter,
            TipLength = profile.TipLength,
            BodyDiameter = profile.BodyDiameter,
            BaseDiameter = profile.BaseDiameter,
            BaseHeight = profile.BaseHeight
        };
    }

    /// <summary>
    /// Converts one runtime support layer color into serializable channel values.
    /// </summary>
    private static GphSupportLayerColorDto CreateSupportLayerColorDto(SupportLayerColor color)
    {
        return new GphSupportLayerColorDto
        {
            Red = color.Red,
            Green = color.Green,
            Blue = color.Blue
        };
    }

    /// <summary>
    /// Converts one serialized vector into the runtime vector type.
    /// </summary>
    private static Vector3 CreateVector(GphVector3Dto vector)
    {
        return new Vector3(vector.X, vector.Y, vector.Z);
    }

    /// <summary>
    /// Converts one serialized profile into the runtime support profile type.
    /// </summary>
    private static SupportProfile CreateSupportProfile(GphSupportProfileDto supportProfileDto)
    {
        return new SupportProfile(
            supportProfileDto.TipDiameter,
            supportProfileDto.TipLength,
            supportProfileDto.BodyDiameter,
            supportProfileDto.BaseDiameter,
            supportProfileDto.BaseHeight);
    }

    /// <summary>
    /// Converts one serialized support group color into the runtime color type or a stable fallback.
    /// </summary>
    private static SupportLayerColor CreateSupportLayerColorOrDefault(GphSupportLayerGroupDto supportLayerGroupDto)
    {
        if (supportLayerGroupDto.Color == null)
        {
            return SupportLayerColorGenerator.CreateFromStableSeed(supportLayerGroupDto.Id);
        }

        return new SupportLayerColor(
            supportLayerGroupDto.Color.Red,
            supportLayerGroupDto.Color.Green,
            supportLayerGroupDto.Color.Blue);
    }

    /// <summary>
    /// Converts a serialized vector list into runtime vectors with a clear error when the field is missing.
    /// </summary>
    private static List<Vector3> CreateVectorList(List<GphVector3Dto>? vectors, string fieldName)
    {
        if (vectors == null)
        {
            throw new InvalidDataException($"A saved mesh entity is missing its {fieldName}.");
        }

        List<Vector3> result = new List<Vector3>(vectors.Count);

        for (int i = 0; i < vectors.Count; i++)
        {
            GphVector3Dto? vector = vectors[i];

            if (vector == null)
            {
                throw new InvalidDataException($"A saved mesh entity has a null {fieldName} entry at index {i}.");
            }

            result.Add(CreateVector(vector));
        }

        return result;
    }

    /// <summary>
    /// Root DTO for the Graphite project file.
    /// </summary>
    private sealed class GphDocumentDto
    {
        public string Format { get; set; } = string.Empty;
        public int Version { get; set; }
        public List<GphEntityDto> Entities { get; set; } = new List<GphEntityDto>();
        public List<GphSupportLayerGroupDto> SupportLayerGroups { get; set; } = new List<GphSupportLayerGroupDto>();
    }

    /// <summary>
    /// Entity DTO containing common fields plus optional type-specific payloads.
    /// </summary>
    private sealed class GphEntityDto
    {
        public string Type { get; set; } = string.Empty;
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public GphVector3Dto? Start { get; set; }
        public GphVector3Dto? End { get; set; }
        public string? SourcePath { get; set; }
        public List<GphVector3Dto> Vertices { get; set; } = new List<GphVector3Dto>();
        public List<int>? TriangleIndices { get; set; }
        public List<GphVector3Dto> Normals { get; set; } = new List<GphVector3Dto>();
        public Guid? SupportLayerGroupId { get; set; }
        public GphVector3Dto? TipPosition { get; set; }
        public GphVector3Dto? BasePosition { get; set; }
        public GphSupportProfileDto? SupportProfile { get; set; }
    }

    /// <summary>
    /// DTO for stable Vector3 serialization without relying on System.Numerics internals.
    /// </summary>
    private sealed class GphVector3Dto
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
    }

    /// <summary>
    /// DTO for persisted support profile values.
    /// </summary>
    private sealed class GphSupportProfileDto
    {
        public float TipDiameter { get; set; }
        public float TipLength { get; set; }
        public float BodyDiameter { get; set; }
        public float BaseDiameter { get; set; }
        public float BaseHeight { get; set; }
    }

    /// <summary>
    /// DTO for document-level support group metadata.
    /// </summary>
    private sealed class GphSupportLayerGroupDto
    {
        public Guid Id { get; set; }
        public Guid ModelEntityId { get; set; }
        public string Name { get; set; } = string.Empty;
        public GphSupportLayerColorDto? Color { get; set; }
    }

    /// <summary>
    /// DTO for persisted support group display colors.
    /// </summary>
    private sealed class GphSupportLayerColorDto
    {
        public byte Red { get; set; }
        public byte Green { get; set; }
        public byte Blue { get; set; }
    }
}
