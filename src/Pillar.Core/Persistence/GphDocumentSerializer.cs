// GphDocumentSerializer.cs
// Saves and loads Graphite CAD documents using a self-contained JSON-based .gph file format.
using Pillar.Core.Document;
using Pillar.Core.Entities;
using Pillar.Core.Layers;
using Pillar.Core.Selection;
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
    private const string LineTypeName = "line";
    private const string MeshTypeName = "mesh";
    private const string SupportTypeName = "support";
    private const string RingSupportGeneratorName = "ringSupport";
    private const string LineSupportGeneratorName = "lineSupport";
    private const string ContourSupportGeneratorName = "contourSupport";
    private const string AreaSupportGeneratorName = "areaSupport";
    private const string ClusterModifierName = "cluster";
    private const string BraceModifierName = "brace";
    private const string DeleteModifierName = "delete";
    private const string AutomaticClusterStemSizingName = "automatic";
    private const string ManualClusterStemSizingName = "manual";
    private const string IndividualSupportStyleName = "individual";
    private const string ClusteredSupportStyleName = "clustered";

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
        AddSupportEntities(deferredSupportEntities, supportLayerGroups, entities);
        ValidateLoadedSupportModifiers(supportLayerGroups, entities);

        return new GphDocumentData(entities, supportLayerGroups);
    }

    /// <summary>
    /// Converts a document into the current persisted file shape.
    /// </summary>
    private static GphDocumentDto CreateDocumentDto(CadDocument document)
    {
        GphDocumentDto dto = new GphDocumentDto
        {
            Format = FormatName
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
                OriginalFileName = mesh.OriginalFileName,
                TriangleIndices = new List<int>(mesh.TriangleIndices),
                ImportPlacementTransform = CreateTransformDto(mesh.ImportPlacementTransform),
                UserTransform = CreateTransformDto(mesh.UserTransform)
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
                HeadDirection = CreateVectorDto(support.HeadDirection),
                BranchLength = support.BranchLength,
                BranchDirection = CreateVectorDto(support.BranchDirection),
                SupportProfile = CreateSupportProfileDto(support.Profile),
                SupportStyle = CreateSupportStyleDto(support.Style)
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
            Color = CreateSupportLayerColorDto(supportLayerGroup.Color),
            GeneratorKind = CreateSupportGroupGeneratorKindDto(supportLayerGroup),
            SourceGeneratorRevision = supportLayerGroup.SourceGeneratorRevision,
            RingSupport = CreateRingSupportSettingsDto(supportLayerGroup.RingSupportSettings),
            LineSupport = CreateLineSupportSettingsDto(supportLayerGroup.LineSupportSettings),
            ContourSupport = CreateContourSupportSettingsDto(supportLayerGroup.ContourSupportSettings),
            AreaSupport = CreateAreaSupportSettingsDto(supportLayerGroup.AreaSupportSettings),
            SupportModifiers = CreateSupportModifierDtos(supportLayerGroup.SupportModifiers)
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
                CreateSupportLayerColorOrDefault(supportLayerGroupDto),
                CreateRingSupportSettingsOrDefault(supportLayerGroupDto),
                CreateLineSupportSettingsOrDefault(supportLayerGroupDto),
                CreateContourSupportSettingsOrDefault(supportLayerGroupDto),
                CreateAreaSupportSettingsOrDefault(supportLayerGroupDto),
                Math.Max(0, supportLayerGroupDto.SourceGeneratorRevision),
                CreateSupportModifiersOrDefault(supportLayerGroupDto)));
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
    /// Removes loaded modifiers whose saved targets no longer match the generated support population.
    /// </summary>
    private static void ValidateLoadedSupportModifiers(IReadOnlyList<SupportLayerGroup> supportLayerGroups, IReadOnlyList<CadEntity> entities)
    {
        Dictionary<Guid, HashSet<Guid>> supportIdsByGroupId = new Dictionary<Guid, HashSet<Guid>>();

        foreach (CadEntity entity in entities)
        {
            if (entity is not SupportEntity supportEntity)
            {
                continue;
            }

            if (!supportIdsByGroupId.TryGetValue(supportEntity.SupportLayerGroupId, out HashSet<Guid>? supportIds))
            {
                supportIds = new HashSet<Guid>();
                supportIdsByGroupId.Add(supportEntity.SupportLayerGroupId, supportIds);
            }

            supportIds.Add(supportEntity.Id);
        }

        foreach (SupportLayerGroup supportLayerGroup in supportLayerGroups)
        {
            IReadOnlyList<SupportModifierDefinition> modifiers = supportLayerGroup.SupportModifiers;
            List<SupportModifierDefinition> validModifiers = new List<SupportModifierDefinition>(modifiers.Count);
            supportIdsByGroupId.TryGetValue(supportLayerGroup.Id, out HashSet<Guid>? groupSupportIds);

            for (int i = 0; i < modifiers.Count; i++)
            {
                SupportModifierDefinition modifier = modifiers[i];

                if (IsModifierValid(modifier, supportLayerGroup, groupSupportIds))
                {
                    validModifiers.Add(modifier);
                }
            }

            if (validModifiers.Count != modifiers.Count)
            {
                supportLayerGroup.SetSupportModifiers(validModifiers);
            }
        }
    }

    /// <summary>
    /// Checks whether a loaded modifier still targets support ids from the saved generator revision.
    /// </summary>
    private static bool IsModifierValid(SupportModifierDefinition modifier, SupportLayerGroup supportLayerGroup, HashSet<Guid>? groupSupportIds)
    {
        if (!modifier.SourceGeneratorRevision.HasValue || modifier.SourceGeneratorRevision.Value != supportLayerGroup.SourceGeneratorRevision)
        {
            return false;
        }

        if (groupSupportIds == null)
        {
            return false;
        }

        for (int i = 0; i < modifier.TargetSupportIds.Count; i++)
        {
            if (!groupSupportIds.Contains(modifier.TargetSupportIds[i]))
            {
                return false;
            }
        }

        return true;
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
            entityDto.SourcePath,
            entityDto.OriginalFileName,
            CreateTransformOrIdentity(entityDto.ImportPlacementTransform),
            CreateTransformOrIdentity(entityDto.UserTransform));
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

        if (entityDto.TipPosition == null || entityDto.BasePosition == null || entityDto.HeadDirection == null || entityDto.BranchDirection == null)
        {
            throw new InvalidDataException("A saved support entity is missing its tip, base, head direction, or branch direction.");
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
            CreateVector(entityDto.HeadDirection),
            entityDto.BranchLength,
            CreateVector(entityDto.BranchDirection),
            CreateSupportProfile(entityDto.SupportProfile),
            CreateSupportStyleOrDefault(entityDto.SupportStyle));
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
            BaseBottomRadius = profile.BaseBottomRadius,
            BaseHeight = profile.BaseHeight,
            StemBottomDiameter = profile.StemBottomDiameter,
            StemTopDiameter = profile.StemTopDiameter,
            MaximumBranchLength = profile.MaximumBranchLength,
            ModelClearance = profile.ModelClearance,
            BranchAngleFromVerticalDegrees = profile.BranchAngleFromVerticalDegrees,
            HeadHeight = profile.HeadHeight,
            HeadPenetrationDepth = profile.HeadPenetrationDepth,
            HeadTopDiameter = profile.HeadTopDiameter,
            MaxHeadAngleFromVerticalDegrees = profile.MaxHeadAngleFromVerticalDegrees
        };
    }

    /// <summary>
    /// Converts one runtime support style into its persisted representation.
    /// </summary>
    private static GphSupportStyleDto CreateSupportStyleDto(SupportStyle style)
    {
        if (style is ClusteredSupportStyle clusteredStyle)
        {
            return new GphSupportStyleDto
            {
                Kind = ClusteredSupportStyleName,
                CentralStemBottomDiameter = clusteredStyle.CentralStemBottomDiameter,
                CentralStemTopDiameter = clusteredStyle.CentralStemTopDiameter,
                BranchDiameter = clusteredStyle.BranchDiameter
            };
        }

        return new GphSupportStyleDto
        {
            Kind = IndividualSupportStyleName
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
    /// Converts one support group's generator kind into the persisted wire value.
    /// </summary>
    private static string? CreateSupportGroupGeneratorKindDto(SupportLayerGroup supportLayerGroup)
    {
        if (supportLayerGroup.GeneratorKind == SupportGroupGeneratorKind.RingSupport)
        {
            return RingSupportGeneratorName;
        }

        if (supportLayerGroup.GeneratorKind == SupportGroupGeneratorKind.LineSupport)
        {
            return LineSupportGeneratorName;
        }

        if (supportLayerGroup.GeneratorKind == SupportGroupGeneratorKind.ContourSupport)
        {
            return ContourSupportGeneratorName;
        }

        if (supportLayerGroup.GeneratorKind == SupportGroupGeneratorKind.AreaSupport)
        {
            return AreaSupportGeneratorName;
        }

        return null;
    }

    /// <summary>
    /// Converts Ring Support settings into their persisted representation when present.
    /// </summary>
    private static GphRingSupportSettingsDto? CreateRingSupportSettingsDto(RingSupportSettings? settings)
    {
        if (settings == null)
        {
            return null;
        }

        return new GphRingSupportSettingsDto
        {
            FirstPoint = CreateVectorDto(settings.FirstPoint),
            SecondPoint = CreateVectorDto(settings.SecondPoint),
            ThirdPoint = CreateVectorDto(settings.ThirdPoint),
            Spacing = settings.Spacing
        };
    }

    /// <summary>
    /// Converts Line Support settings into their persisted representation when present.
    /// </summary>
    private static GphLineSupportSettingsDto? CreateLineSupportSettingsDto(LineSupportSettings? settings)
    {
        if (settings == null)
        {
            return null;
        }

        GphLineSupportSettingsDto dto = new GphLineSupportSettingsDto
        {
            Spacing = settings.Spacing,
            PlaceSupportsAtBends = settings.PlaceSupportsAtBends
        };

        for (int i = 0; i < settings.Points.Count; i++)
        {
            dto.Points.Add(CreateVectorDto(settings.Points[i]));
        }

        return dto;
    }

    /// <summary>
    /// Converts Contour Support settings into their persisted representation when present.
    /// </summary>
    private static GphContourSupportSettingsDto? CreateContourSupportSettingsDto(ContourSupportSettings? settings)
    {
        if (settings == null)
        {
            return null;
        }

        return new GphContourSupportSettingsDto
        {
            SeedPoint = CreateVectorDto(settings.SeedPoint),
            SeedTriangleIndex = settings.SeedTriangleIndex,
            ZHeight = settings.ZHeight,
            CoplanarThresholdDegrees = settings.CoplanarThresholdDegrees,
            Spacing = settings.Spacing,
            StartOffset = settings.StartOffset,
            FinalOffset = settings.FinalOffset
        };
    }

    /// <summary>
    /// Converts Area Support settings into their persisted representation when present.
    /// </summary>
    private static GphAreaSupportSettingsDto? CreateAreaSupportSettingsDto(AreaSupportSettings? settings)
    {
        if (settings == null)
        {
            return null;
        }

        GphAreaSupportSettingsDto dto = new GphAreaSupportSettingsDto
        {
            Spacing = settings.Spacing,
            BoundaryOffset = settings.BoundaryOffset,
            BoundarySpacing = settings.BoundarySpacing,
            ConcaveCornerAngleDegrees = settings.ConcaveCornerAngleDegrees,
            SupportThinRegions = settings.SupportThinRegions,
            MinimumThinRegionThickness = settings.MinimumThinRegionThickness,
            FillMode = settings.FillMode,
            AdditionalOffsetCount = settings.AdditionalOffsetCount,
            OffsetSpacing = settings.OffsetSpacing
        };

        for (int i = 0; i < settings.SelectedFaces.Count; i++)
        {
            FaceSelectionKey selectedFace = settings.SelectedFaces[i];
            dto.SelectedFaces.Add(new GphFaceSelectionDto
            {
                MeshEntityId = selectedFace.MeshEntityId,
                TriangleIndex = selectedFace.TriangleIndex
            });
        }

        return dto;
    }

    /// <summary>
    /// Converts support modifier definitions into their persisted representation.
    /// </summary>
    private static List<GphSupportModifierDto> CreateSupportModifierDtos(IReadOnlyList<SupportModifierDefinition> modifiers)
    {
        List<GphSupportModifierDto> result = new List<GphSupportModifierDto>(modifiers.Count);

        for (int i = 0; i < modifiers.Count; i++)
        {
            SupportModifierDefinition modifier = modifiers[i];
            result.Add(new GphSupportModifierDto
            {
                Id = modifier.Id,
                Kind = CreateSupportModifierKindDto(modifier.Kind),
                IsEnabled = modifier.IsEnabled,
                Order = modifier.Order,
                SourceGeneratorRevision = modifier.SourceGeneratorRevision,
                TargetSupportIds = new List<Guid>(modifier.TargetSupportIds),
                TargetSupportIdBatches = CreateTargetSupportIdBatchDtos(modifier.TargetSupportIdBatches),
                ClusterSettings = CreateClusterModifierSettingsDto(modifier.ClusterSettings)
            });
        }

        return result;
    }

    /// <summary>
    /// Converts cumulative target batches into their persisted representation.
    /// </summary>
    private static List<List<Guid>> CreateTargetSupportIdBatchDtos(IReadOnlyList<SupportModifierTargetBatch> targetSupportIdBatches)
    {
        List<List<Guid>> result = new List<List<Guid>>(targetSupportIdBatches.Count);

        for (int i = 0; i < targetSupportIdBatches.Count; i++)
        {
            result.Add(new List<Guid>(targetSupportIdBatches[i].TargetSupportIds));
        }

        return result;
    }

    /// <summary>
    /// Converts one modifier kind into its persisted wire value.
    /// </summary>
    private static string CreateSupportModifierKindDto(SupportModifierKind kind)
    {
        switch (kind)
        {
            case SupportModifierKind.Cluster:
                return ClusterModifierName;

            case SupportModifierKind.Brace:
                return BraceModifierName;

            case SupportModifierKind.Delete:
                return DeleteModifierName;

            default:
                throw new NotSupportedException($"Support modifier kind '{kind}' is not supported by the .gph format.");
        }
    }


    /// <summary>
    /// Converts cluster modifier settings into their persisted representation when present.
    /// </summary>
    private static GphClusterModifierSettingsDto? CreateClusterModifierSettingsDto(SupportClusterModifierSettings? settings)
    {
        if (settings == null)
        {
            return null;
        }

        return new GphClusterModifierSettingsDto
        {
            MaximumClusterRadius = settings.MaximumClusterRadius,
            MinimumSupportsPerCluster = settings.MinimumSupportsPerCluster,
            MaximumSupportsPerCluster = settings.MaximumSupportsPerCluster,
            MaximumBranchAngleFromVerticalDegrees = settings.MaximumBranchAngleFromVerticalDegrees,
            StemSizingMode = settings.StemSizingMode == SupportClusterStemSizingMode.Automatic
                ? AutomaticClusterStemSizingName
                : ManualClusterStemSizingName,
            ManualCentralStemBottomDiameter = settings.ManualCentralStemBottomDiameter,
            ManualCentralStemTopDiameter = settings.ManualCentralStemTopDiameter,
            ClusterBranchDiameter = settings.ClusterBranchDiameter
        };
    }

    /// <summary>
    /// Converts one runtime transform into a serializable DTO payload.
    /// </summary>
    private static GphTransform3DDto CreateTransformDto(Transform3DData transform)
    {
        return new GphTransform3DDto
        {
            Translation = CreateVectorDto(transform.Translation),
            Rotation = CreateQuaternionDto(transform.Rotation),
            Scale = CreateVectorDto(transform.Scale)
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
    /// Converts one serialized quaternion into the runtime quaternion type.
    /// </summary>
    private static Quaternion CreateQuaternion(GphQuaternionDto quaternion)
    {
        return new Quaternion(quaternion.X, quaternion.Y, quaternion.Z, quaternion.W);
    }

    /// <summary>
    /// Converts one serialized profile into the runtime support profile type.
    /// </summary>
    private static SupportProfile CreateSupportProfile(GphSupportProfileDto supportProfileDto)
    {
        return new SupportProfile(
            supportProfileDto.BaseBottomRadius,
            supportProfileDto.BaseHeight,
            supportProfileDto.StemBottomDiameter,
            supportProfileDto.StemTopDiameter,
            supportProfileDto.MaximumBranchLength,
            supportProfileDto.ModelClearance,
            supportProfileDto.BranchAngleFromVerticalDegrees ?? SupportDefaults.DefaultBranchAngleFromVerticalDegrees,
            supportProfileDto.HeadHeight,
            supportProfileDto.HeadPenetrationDepth,
            supportProfileDto.HeadTopDiameter,
            supportProfileDto.MaxHeadAngleFromVerticalDegrees);
    }

    /// <summary>
    /// Converts one serialized support style into the runtime style type, defaulting old files to individual supports.
    /// </summary>
    private static SupportStyle CreateSupportStyleOrDefault(GphSupportStyleDto? supportStyleDto)
    {
        if (supportStyleDto == null || string.IsNullOrWhiteSpace(supportStyleDto.Kind))
        {
            return SupportStyle.Individual;
        }

        if (string.Equals(supportStyleDto.Kind, ClusteredSupportStyleName, StringComparison.OrdinalIgnoreCase))
        {
            if (!supportStyleDto.BranchDiameter.HasValue)
            {
                throw new InvalidDataException("A clustered support style is missing its branch diameter.");
            }

            return new ClusteredSupportStyle(supportStyleDto.CentralStemBottomDiameter, supportStyleDto.CentralStemTopDiameter, supportStyleDto.BranchDiameter.Value);
        }

        if (string.Equals(supportStyleDto.Kind, IndividualSupportStyleName, StringComparison.OrdinalIgnoreCase))
        {
            return SupportStyle.Individual;
        }

        throw new InvalidDataException($"Support style '{supportStyleDto.Kind}' is not supported.");
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
    /// Converts saved generator metadata into Ring Support settings, or null for legacy/plain support groups.
    /// </summary>
    private static RingSupportSettings? CreateRingSupportSettingsOrDefault(GphSupportLayerGroupDto supportLayerGroupDto)
    {
        if (string.IsNullOrWhiteSpace(supportLayerGroupDto.GeneratorKind))
        {
            return null;
        }

        if (!string.Equals(supportLayerGroupDto.GeneratorKind, RingSupportGeneratorName, StringComparison.OrdinalIgnoreCase))
        {
            if (string.Equals(supportLayerGroupDto.GeneratorKind, LineSupportGeneratorName, StringComparison.OrdinalIgnoreCase)
                || string.Equals(supportLayerGroupDto.GeneratorKind, ContourSupportGeneratorName, StringComparison.OrdinalIgnoreCase)
                || string.Equals(supportLayerGroupDto.GeneratorKind, AreaSupportGeneratorName, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            throw new InvalidDataException($"Support group generator '{supportLayerGroupDto.GeneratorKind}' is not supported.");
        }

        if (supportLayerGroupDto.RingSupport == null)
        {
            throw new InvalidDataException("A Ring Support group is missing its generator settings.");
        }

        if (supportLayerGroupDto.RingSupport.FirstPoint == null
            || supportLayerGroupDto.RingSupport.SecondPoint == null
            || supportLayerGroupDto.RingSupport.ThirdPoint == null)
        {
            throw new InvalidDataException("A Ring Support group is missing one or more ring points.");
        }

        return new RingSupportSettings(
            CreateVector(supportLayerGroupDto.RingSupport.FirstPoint),
            CreateVector(supportLayerGroupDto.RingSupport.SecondPoint),
            CreateVector(supportLayerGroupDto.RingSupport.ThirdPoint),
            supportLayerGroupDto.RingSupport.Spacing);
    }

    /// <summary>
    /// Converts saved generator metadata into Line Support settings, or null for legacy/plain support groups.
    /// </summary>
    private static LineSupportSettings? CreateLineSupportSettingsOrDefault(GphSupportLayerGroupDto supportLayerGroupDto)
    {
        if (string.IsNullOrWhiteSpace(supportLayerGroupDto.GeneratorKind))
        {
            return null;
        }

        if (!string.Equals(supportLayerGroupDto.GeneratorKind, LineSupportGeneratorName, StringComparison.OrdinalIgnoreCase))
        {
            if (string.Equals(supportLayerGroupDto.GeneratorKind, RingSupportGeneratorName, StringComparison.OrdinalIgnoreCase)
                || string.Equals(supportLayerGroupDto.GeneratorKind, ContourSupportGeneratorName, StringComparison.OrdinalIgnoreCase)
                || string.Equals(supportLayerGroupDto.GeneratorKind, AreaSupportGeneratorName, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            throw new InvalidDataException($"Support group generator '{supportLayerGroupDto.GeneratorKind}' is not supported.");
        }

        if (supportLayerGroupDto.LineSupport == null)
        {
            throw new InvalidDataException("A Line Support group is missing its generator settings.");
        }

        if (supportLayerGroupDto.LineSupport.Points == null || supportLayerGroupDto.LineSupport.Points.Count < 2)
        {
            throw new InvalidDataException("A Line Support group is missing its polyline points.");
        }

        List<Vector3> points = new List<Vector3>(supportLayerGroupDto.LineSupport.Points.Count);

        for (int i = 0; i < supportLayerGroupDto.LineSupport.Points.Count; i++)
        {
            GphVector3Dto? point = supportLayerGroupDto.LineSupport.Points[i];

            if (point == null)
            {
                throw new InvalidDataException($"A Line Support group has a null point at index {i}.");
            }

            points.Add(CreateVector(point));
        }

        bool placeSupportsAtBends = supportLayerGroupDto.LineSupport.PlaceSupportsAtBends
            ?? LineSupportSettings.DefaultPlaceSupportsAtBends;

        return new LineSupportSettings(points, supportLayerGroupDto.LineSupport.Spacing, placeSupportsAtBends);
    }

    /// <summary>
    /// Converts saved generator metadata into Contour Support settings, or null for legacy/plain support groups.
    /// </summary>
    private static ContourSupportSettings? CreateContourSupportSettingsOrDefault(GphSupportLayerGroupDto supportLayerGroupDto)
    {
        if (string.IsNullOrWhiteSpace(supportLayerGroupDto.GeneratorKind))
        {
            return null;
        }

        if (!string.Equals(supportLayerGroupDto.GeneratorKind, ContourSupportGeneratorName, StringComparison.OrdinalIgnoreCase))
        {
            if (string.Equals(supportLayerGroupDto.GeneratorKind, RingSupportGeneratorName, StringComparison.OrdinalIgnoreCase)
                || string.Equals(supportLayerGroupDto.GeneratorKind, LineSupportGeneratorName, StringComparison.OrdinalIgnoreCase)
                || string.Equals(supportLayerGroupDto.GeneratorKind, AreaSupportGeneratorName, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            throw new InvalidDataException($"Support group generator '{supportLayerGroupDto.GeneratorKind}' is not supported.");
        }

        if (supportLayerGroupDto.ContourSupport == null)
        {
            throw new InvalidDataException("A Contour Support group is missing its generator settings.");
        }

        if (supportLayerGroupDto.ContourSupport.SeedPoint == null)
        {
            throw new InvalidDataException("A Contour Support group is missing its seed point.");
        }

        return new ContourSupportSettings(
            CreateVector(supportLayerGroupDto.ContourSupport.SeedPoint),
            supportLayerGroupDto.ContourSupport.SeedTriangleIndex,
            supportLayerGroupDto.ContourSupport.ZHeight,
            supportLayerGroupDto.ContourSupport.CoplanarThresholdDegrees,
            supportLayerGroupDto.ContourSupport.Spacing,
            supportLayerGroupDto.ContourSupport.StartOffset,
            supportLayerGroupDto.ContourSupport.FinalOffset);
    }

    /// <summary>
    /// Converts saved generator metadata into Area Support settings, or null for legacy/plain support groups.
    /// </summary>
    private static AreaSupportSettings? CreateAreaSupportSettingsOrDefault(GphSupportLayerGroupDto supportLayerGroupDto)
    {
        if (string.IsNullOrWhiteSpace(supportLayerGroupDto.GeneratorKind))
        {
            return null;
        }

        if (!string.Equals(supportLayerGroupDto.GeneratorKind, AreaSupportGeneratorName, StringComparison.OrdinalIgnoreCase))
        {
            if (string.Equals(supportLayerGroupDto.GeneratorKind, RingSupportGeneratorName, StringComparison.OrdinalIgnoreCase)
                || string.Equals(supportLayerGroupDto.GeneratorKind, LineSupportGeneratorName, StringComparison.OrdinalIgnoreCase)
                || string.Equals(supportLayerGroupDto.GeneratorKind, ContourSupportGeneratorName, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            throw new InvalidDataException($"Support group generator '{supportLayerGroupDto.GeneratorKind}' is not supported.");
        }

        if (supportLayerGroupDto.AreaSupport == null)
        {
            throw new InvalidDataException("An Area Support group is missing its generator settings.");
        }

        if (supportLayerGroupDto.AreaSupport.SelectedFaces == null || supportLayerGroupDto.AreaSupport.SelectedFaces.Count == 0)
        {
            throw new InvalidDataException("An Area Support group is missing its selected faces.");
        }

        List<FaceSelectionKey> selectedFaces = new List<FaceSelectionKey>(supportLayerGroupDto.AreaSupport.SelectedFaces.Count);

        for (int i = 0; i < supportLayerGroupDto.AreaSupport.SelectedFaces.Count; i++)
        {
            GphFaceSelectionDto? selectedFace = supportLayerGroupDto.AreaSupport.SelectedFaces[i];

            if (selectedFace == null)
            {
                throw new InvalidDataException($"An Area Support group has a null selected face at index {i}.");
            }

            selectedFaces.Add(new FaceSelectionKey(selectedFace.MeshEntityId, selectedFace.TriangleIndex));
        }

        float boundaryOffset = supportLayerGroupDto.AreaSupport.BoundaryOffset
            ?? AreaSupportSettings.CalculateDefaultBoundaryOffset(supportLayerGroupDto.AreaSupport.Spacing);
        float boundarySpacing = supportLayerGroupDto.AreaSupport.BoundarySpacing
            ?? AreaSupportSettings.CalculateDefaultBoundarySpacing(supportLayerGroupDto.AreaSupport.Spacing);
        float offsetSpacing = supportLayerGroupDto.AreaSupport.OffsetSpacing ?? boundaryOffset;
        float concaveCornerAngleDegrees = supportLayerGroupDto.AreaSupport.ConcaveCornerAngleDegrees
            ?? AreaSupportSettings.DefaultConcaveCornerAngleDegrees;
        bool supportThinRegions = supportLayerGroupDto.AreaSupport.SupportThinRegions ?? false;
        float minimumThinRegionThickness = supportLayerGroupDto.AreaSupport.MinimumThinRegionThickness
            ?? AreaSupportSettings.DefaultMinimumThinRegionThickness;

        return new AreaSupportSettings(
            selectedFaces,
            supportLayerGroupDto.AreaSupport.Spacing,
            boundaryOffset,
            boundarySpacing,
            concaveCornerAngleDegrees,
            supportThinRegions,
            minimumThinRegionThickness,
            supportLayerGroupDto.AreaSupport.FillMode,
            supportLayerGroupDto.AreaSupport.AdditionalOffsetCount,
            offsetSpacing);
    }

    /// <summary>
    /// Converts saved modifier metadata into ordered support modifier definitions.
    /// </summary>
    private static IReadOnlyList<SupportModifierDefinition> CreateSupportModifiersOrDefault(GphSupportLayerGroupDto supportLayerGroupDto)
    {
        if (supportLayerGroupDto.SupportModifiers == null || supportLayerGroupDto.SupportModifiers.Count == 0)
        {
            return Array.Empty<SupportModifierDefinition>();
        }

        List<SupportModifierDefinition> modifiers = new List<SupportModifierDefinition>(supportLayerGroupDto.SupportModifiers.Count);

        for (int i = 0; i < supportLayerGroupDto.SupportModifiers.Count; i++)
        {
            GphSupportModifierDto? modifierDto = supportLayerGroupDto.SupportModifiers[i];

            if (modifierDto == null)
            {
                throw new InvalidDataException($"A support group has a null modifier at index {i}.");
            }

            modifiers.Add(new SupportModifierDefinition(
                modifierDto.Id,
                CreateSupportModifierKind(modifierDto.Kind),
                modifierDto.IsEnabled,
                Math.Max(0, modifierDto.Order),
                CreateClusterModifierSettingsOrDefault(modifierDto),
                modifierDto.TargetSupportIds ?? new List<Guid>(),
                CreateTargetSupportIdBatchesOrDefault(modifierDto),
                modifierDto.SourceGeneratorRevision));
        }

        modifiers.Sort((left, right) => left.Order.CompareTo(right.Order));
        return modifiers;
    }

    /// <summary>
    /// Converts saved cumulative target batches into runtime modifier batches.
    /// </summary>
    private static IReadOnlyList<SupportModifierTargetBatch>? CreateTargetSupportIdBatchesOrDefault(GphSupportModifierDto modifierDto)
    {
        if (modifierDto.TargetSupportIdBatches == null || modifierDto.TargetSupportIdBatches.Count == 0)
        {
            return null;
        }

        List<SupportModifierTargetBatch> result = new List<SupportModifierTargetBatch>(modifierDto.TargetSupportIdBatches.Count);

        for (int i = 0; i < modifierDto.TargetSupportIdBatches.Count; i++)
        {
            List<Guid>? targetSupportIds = modifierDto.TargetSupportIdBatches[i];

            if (targetSupportIds == null)
            {
                throw new InvalidDataException($"A support modifier has a null target batch at index {i}.");
            }

            result.Add(new SupportModifierTargetBatch(targetSupportIds));
        }

        return result;
    }

    /// <summary>
    /// Converts saved modifier kind text into the runtime enum.
    /// </summary>
    private static SupportModifierKind CreateSupportModifierKind(string kind)
    {
        if (string.Equals(kind, ClusterModifierName, StringComparison.OrdinalIgnoreCase))
        {
            return SupportModifierKind.Cluster;
        }

        if (string.Equals(kind, BraceModifierName, StringComparison.OrdinalIgnoreCase))
        {
            return SupportModifierKind.Brace;
        }

        if (string.Equals(kind, DeleteModifierName, StringComparison.OrdinalIgnoreCase))
        {
            return SupportModifierKind.Delete;
        }

        throw new InvalidDataException($"Support modifier kind '{kind}' is not supported.");
    }


    /// <summary>
    /// Converts saved Cluster settings into the runtime settings object when present.
    /// </summary>
    private static SupportClusterModifierSettings? CreateClusterModifierSettingsOrDefault(GphSupportModifierDto modifierDto)
    {
        if (!string.Equals(modifierDto.Kind, ClusterModifierName, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (modifierDto.ClusterSettings == null)
        {
            throw new InvalidDataException("A Cluster support modifier is missing its settings.");
        }

        SupportClusterStemSizingMode stemSizingMode = string.Equals(
            modifierDto.ClusterSettings.StemSizingMode,
            ManualClusterStemSizingName,
            StringComparison.OrdinalIgnoreCase)
            ? SupportClusterStemSizingMode.Manual
            : SupportClusterStemSizingMode.Automatic;

        return new SupportClusterModifierSettings(
            modifierDto.ClusterSettings.MaximumClusterRadius,
            modifierDto.ClusterSettings.MinimumSupportsPerCluster,
            modifierDto.ClusterSettings.MaximumSupportsPerCluster,
            modifierDto.ClusterSettings.MaximumBranchAngleFromVerticalDegrees,
            stemSizingMode,
            modifierDto.ClusterSettings.ManualCentralStemBottomDiameter,
            modifierDto.ClusterSettings.ManualCentralStemTopDiameter,
            modifierDto.ClusterSettings.ClusterBranchDiameter ?? SupportDefaults.DefaultStemTopDiameter);
    }

    /// <summary>
    /// Converts one serialized transform into the runtime transform type, or returns identity for legacy files.
    /// </summary>
    private static Transform3DData CreateTransformOrIdentity(GphTransform3DDto? transformDto)
    {
        if (transformDto == null)
        {
            return Transform3DData.Identity;
        }

        if (transformDto.Translation == null)
        {
            throw new InvalidDataException("A saved mesh transform is missing its translation.");
        }

        if (transformDto.Rotation == null)
        {
            throw new InvalidDataException("A saved mesh transform is missing its rotation.");
        }

        if (transformDto.Scale == null)
        {
            throw new InvalidDataException("A saved mesh transform is missing its scale.");
        }

        return new Transform3DData(
            CreateVector(transformDto.Translation),
            CreateQuaternion(transformDto.Rotation),
            CreateVector(transformDto.Scale));
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
        public string? OriginalFileName { get; set; }
        public List<GphVector3Dto> Vertices { get; set; } = new List<GphVector3Dto>();
        public List<int>? TriangleIndices { get; set; }
        public List<GphVector3Dto> Normals { get; set; } = new List<GphVector3Dto>();
        public GphTransform3DDto? ImportPlacementTransform { get; set; }
        public GphTransform3DDto? UserTransform { get; set; }
        public Guid? SupportLayerGroupId { get; set; }
        public GphVector3Dto? TipPosition { get; set; }
        public GphVector3Dto? BasePosition { get; set; }
        public GphVector3Dto? HeadDirection { get; set; }
        public float BranchLength { get; set; }
        public GphVector3Dto? BranchDirection { get; set; }
        public GphSupportProfileDto? SupportProfile { get; set; }
        public GphSupportStyleDto? SupportStyle { get; set; }
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
    /// DTO for stable Quaternion serialization without relying on System.Numerics internals.
    /// </summary>
    private sealed class GphQuaternionDto
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
        public float W { get; set; }
    }

    /// <summary>
    /// DTO for persisted mesh transform data.
    /// </summary>
    private sealed class GphTransform3DDto
    {
        public GphVector3Dto? Translation { get; set; }
        public GphQuaternionDto? Rotation { get; set; }
        public GphVector3Dto? Scale { get; set; }
    }

    /// <summary>
    /// DTO for persisted support style values.
    /// </summary>
    private sealed class GphSupportStyleDto
    {
        public string Kind { get; set; } = IndividualSupportStyleName;
        public float? CentralStemBottomDiameter { get; set; }
        public float? CentralStemTopDiameter { get; set; }
        public float? BranchDiameter { get; set; }
    }

    /// <summary>
    /// DTO for persisted support profile values.
    /// </summary>
    private sealed class GphSupportProfileDto
    {
        public float BaseBottomRadius { get; set; }
        public float BaseHeight { get; set; }
        public float StemBottomDiameter { get; set; }
        public float StemTopDiameter { get; set; }
        public float MaximumBranchLength { get; set; }
        public float ModelClearance { get; set; }
        public float? BranchAngleFromVerticalDegrees { get; set; }
        public float HeadHeight { get; set; }
        public float HeadPenetrationDepth { get; set; }
        public float HeadTopDiameter { get; set; }
        public float MaxHeadAngleFromVerticalDegrees { get; set; }
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
        public string? GeneratorKind { get; set; }
        public int SourceGeneratorRevision { get; set; }
        public GphRingSupportSettingsDto? RingSupport { get; set; }
        public GphLineSupportSettingsDto? LineSupport { get; set; }
        public GphContourSupportSettingsDto? ContourSupport { get; set; }
        public GphAreaSupportSettingsDto? AreaSupport { get; set; }
        public List<GphSupportModifierDto>? SupportModifiers { get; set; }
    }

    /// <summary>
    /// DTO for one persisted support-layer modifier stack entry.
    /// </summary>
    private sealed class GphSupportModifierDto
    {
        public Guid Id { get; set; }
        public string Kind { get; set; } = string.Empty;
        public bool IsEnabled { get; set; } = true;
        public int Order { get; set; }
        public int? SourceGeneratorRevision { get; set; }
        public List<Guid>? TargetSupportIds { get; set; }
        public List<List<Guid>>? TargetSupportIdBatches { get; set; }
        public GphClusterModifierSettingsDto? ClusterSettings { get; set; }
    }

    /// <summary>
    /// DTO for persisted Cluster modifier settings.
    /// </summary>
    private sealed class GphClusterModifierSettingsDto
    {
        public float MaximumClusterRadius { get; set; } = SupportClusterModifierSettings.DefaultMaximumClusterRadius;
        public int MinimumSupportsPerCluster { get; set; } = SupportClusterModifierSettings.DefaultMinimumSupportsPerCluster;
        public int MaximumSupportsPerCluster { get; set; } = SupportClusterModifierSettings.DefaultMaximumSupportsPerCluster;
        public float MaximumBranchAngleFromVerticalDegrees { get; set; } = SupportDefaults.DefaultBranchAngleFromVerticalDegrees;
        public string StemSizingMode { get; set; } = AutomaticClusterStemSizingName;
        public float ManualCentralStemBottomDiameter { get; set; } = SupportDefaults.DefaultStemBottomDiameter;
        public float ManualCentralStemTopDiameter { get; set; } = SupportDefaults.DefaultStemTopDiameter;
        public float? ClusterBranchDiameter { get; set; } = SupportDefaults.DefaultStemTopDiameter;
    }

    /// <summary>
    /// DTO for persisted Ring Support generator settings.
    /// </summary>
    private sealed class GphRingSupportSettingsDto
    {
        public GphVector3Dto? FirstPoint { get; set; }
        public GphVector3Dto? SecondPoint { get; set; }
        public GphVector3Dto? ThirdPoint { get; set; }
        public float Spacing { get; set; }
    }

    /// <summary>
    /// DTO for persisted Line Support generator settings.
    /// </summary>
    private sealed class GphLineSupportSettingsDto
    {
        public List<GphVector3Dto?> Points { get; set; } = new List<GphVector3Dto?>();
        public float Spacing { get; set; }
        public bool? PlaceSupportsAtBends { get; set; }
    }

    /// <summary>
    /// DTO for persisted Contour Support generator settings.
    /// </summary>
    private sealed class GphContourSupportSettingsDto
    {
        public GphVector3Dto? SeedPoint { get; set; }
        public int SeedTriangleIndex { get; set; }
        public float ZHeight { get; set; }
        public float CoplanarThresholdDegrees { get; set; }
        public float Spacing { get; set; }
        public float StartOffset { get; set; }
        public float FinalOffset { get; set; }
    }

    /// <summary>
    /// DTO for persisted Area Support generator settings.
    /// </summary>
    private sealed class GphAreaSupportSettingsDto
    {
        public List<GphFaceSelectionDto?> SelectedFaces { get; set; } = new List<GphFaceSelectionDto?>();
        public float Spacing { get; set; }
        public float? BoundaryOffset { get; set; }
        public float? BoundarySpacing { get; set; }
        public float? ConcaveCornerAngleDegrees { get; set; }
        public bool? SupportThinRegions { get; set; }
        public float? MinimumThinRegionThickness { get; set; }
        public AreaSupportFillMode FillMode { get; set; } = AreaSupportSettings.DefaultFillMode;
        public int AdditionalOffsetCount { get; set; } = AreaSupportSettings.DefaultAdditionalOffsetCount;
        public float? OffsetSpacing { get; set; }
    }

    /// <summary>
    /// DTO for one persisted selected mesh face.
    /// </summary>
    private sealed class GphFaceSelectionDto
    {
        public Guid MeshEntityId { get; set; }
        public int TriangleIndex { get; set; }
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

    /// <summary>
    /// Converts one runtime quaternion into serializable numeric components.
    /// </summary>
    private static GphQuaternionDto CreateQuaternionDto(Quaternion quaternion)
    {
        return new GphQuaternionDto
        {
            X = quaternion.X,
            Y = quaternion.Y,
            Z = quaternion.Z,
            W = quaternion.W
        };
    }
}







