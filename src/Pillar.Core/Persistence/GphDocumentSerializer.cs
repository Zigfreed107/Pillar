// GphDocumentSerializer.cs
// Saves and loads Graphite CAD documents using a self-contained JSON-based .gph file format.
using Pillar.Core.Document;
using Pillar.Core.Entities;
using Pillar.Core.Layers;
using Pillar.Core.Rafts;
using Pillar.Core.RaftTexts;
using Pillar.Core.Selection;
using Pillar.Core.Supports;
using Pillar.Core.Tags;
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
    private const int CurrentSchemaVersion = 1;
    private const string LineTypeName = "line";
    private const string MeshTypeName = "mesh";
    private const string SupportTypeName = "support";
    private const string RaftTypeName = "raft";
    private const string TagTypeName = "tag";
    private const string RaftTextTypeName = "raftText";
    private const string RingSupportGeneratorName = "ringSupport";
    private const string LineSupportGeneratorName = "lineSupport";
    private const string ContourSupportGeneratorName = "contourSupport";
    private const string AreaSupportGeneratorName = "areaSupport";
    private const string FirstReachableRingSurfaceTargetName = "firstReachable";
    private const string SelectedFacesOnlyRingSurfaceTargetName = "selectedFacesOnly";
    private const string FirstReachableLineSurfaceTargetName = "firstReachable";
    private const string NearestToLineSurfaceTargetName = "nearestToLine";
    private const string SelectedFacesOnlyLineSurfaceTargetName = "selectedFacesOnly";
    private const string ClusterModifierName = "cluster";
    private const string BraceModifierName = "brace";
    private const string ButtressModifierName = "buttress";
    private const string DirectEditModifierName = "directEdit";
    private const string DeleteModifierName = "delete";
    private const string AutomaticClusterStemSizingName = "automatic";
    private const string ManualClusterStemSizingName = "manual";
    private const string IndividualSupportStyleName = "individual";
    private const string ClusteredSupportStyleName = "clustered";
    private const string BraceMemberSupportStyleName = "braceMember";
    private const string ButtressSupportStyleName = "buttress";

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

        ValidateLoadedRafts(entities);
        ValidateLoadedTags(entities);
        ValidateLoadedRaftTexts(entities);
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
            Format = FormatName,
            SchemaVersion = CurrentSchemaVersion
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

        if (entity is RaftEntity raft)
        {
            GphEntityDto dto = new GphEntityDto
            {
                Type = RaftTypeName,
                Id = raft.Id,
                Name = raft.Name,
                ModelEntityId = raft.ModelEntityId,
                TriangleIndices = new List<int>(raft.TriangleIndices),
                Color = CreateSupportLayerColorDto(raft.Color),
                RaftSettings = new GphRaftSettingsDto
                {
                    Type = raft.Settings.Type,
                    RaftHeight = raft.Settings.RaftHeight,
                    LipHeight = raft.Settings.LipHeight,
                    LipWidth = raft.Settings.LipWidth,
                    FootprintOffset = raft.Settings.FootprintOffset,
                    RaftThickness = raft.Settings.RaftThickness,
                    LineThickness = raft.Settings.LineThickness,
                    MaxSideLength = raft.Settings.MaxSideLength,
                    FootSize = raft.Settings.FootSize,
                    EdgeAngleDegrees = raft.Settings.EdgeAngleDegrees
                }
            };

            foreach (Vector3 vertex in raft.Vertices)
            {
                dto.Vertices.Add(CreateVectorDto(vertex));
            }

            return dto;
        }

        if (entity is TagEntity tag)
        {
            GphEntityDto dto = new GphEntityDto
            {
                Type = TagTypeName,
                Id = tag.Id,
                Name = tag.Name,
                ModelEntityId = tag.ModelEntityId,
                AttachmentPoint = CreateVectorDto(tag.AttachmentPoint),
                Tangent = CreateVectorDto(tag.Tangent),
                TriangleIndices = new List<int>(tag.TriangleIndices),
                Color = CreateSupportLayerColorDto(tag.Color),
                TagSettings = new GphTagSettingsDto
                {
                    TagHeight = tag.Settings.TagHeight,
                    EdgeAngleDegrees = tag.Settings.EdgeAngleDegrees,
                    BorderOffset = tag.Settings.BorderOffset,
                    Text = tag.Settings.Text,
                    FontFamilyName = tag.Settings.FontFamilyName,
                    FontSize = tag.Settings.FontSize,
                    TextHeight = tag.Settings.TextHeight,
                    IsTextFlipped = tag.Settings.IsTextFlipped,
                    OuterWidth = tag.Settings.OuterWidth,
                    InnerWidth = tag.Settings.InnerWidth
                }
            };

            foreach (Vector3 vertex in tag.Vertices)
            {
                dto.Vertices.Add(CreateVectorDto(vertex));
            }

            return dto;
        }
        if (entity is RaftTextEntity raftText)
        {
            GphEntityDto dto = new GphEntityDto
            {
                Type = RaftTextTypeName,
                Id = raftText.Id,
                Name = raftText.Name,
                ModelEntityId = raftText.ModelEntityId,
                AttachmentPoint = CreateVectorDto(raftText.Placement),
                TriangleIndices = new List<int>(raftText.TriangleIndices),
                Color = CreateSupportLayerColorDto(raftText.Color),
                RaftTextSettings = new GphRaftTextSettingsDto
                {
                    Text = raftText.Settings.Text,
                    FontFamilyName = raftText.Settings.FontFamilyName,
                    FontSize = raftText.Settings.FontSize,
                    TextHeight = raftText.Settings.TextHeight,
                    BorderOffset = raftText.Settings.BorderOffset,
                    OrientationDegrees = raftText.Settings.OrientationDegrees
                }
            };

            foreach (Vector3 vertex in raftText.Vertices)
            {
                dto.Vertices.Add(CreateVectorDto(vertex));
            }

            return dto;
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
                BaseAttachmentKind = (int)support.BaseAttachmentKind,
                BaseDirection = CreateVectorDto(support.BaseDirection),
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

        if (documentDto.SchemaVersion != CurrentSchemaVersion)
        {
            throw new InvalidDataException(
                $"The project uses unsupported schema version {documentDto.SchemaVersion}. Expected version {CurrentSchemaVersion}.");
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
    /// Validates model ownership and the one-raft-per-model invariant before applying loaded data.
    /// </summary>
    private static void ValidateLoadedRafts(IReadOnlyList<CadEntity> entities)
    {
        HashSet<Guid> meshEntityIds = CreateMeshEntityIdSet(entities);
        HashSet<Guid> raftModelEntityIds = new HashSet<Guid>();

        foreach (CadEntity entity in entities)
        {
            if (entity is not RaftEntity raft)
            {
                continue;
            }

            if (!meshEntityIds.Contains(raft.ModelEntityId))
            {
                throw new InvalidDataException("A saved raft references an imported model that is not in the project.");
            }

            if (!raftModelEntityIds.Add(raft.ModelEntityId))
            {
                throw new InvalidDataException("A saved model contains more than one raft.");
            }
        }
    }

    /// <summary>
    /// Validates that every saved tag belongs to a model with a saved raft.
    /// </summary>
    private static void ValidateLoadedTags(IReadOnlyList<CadEntity> entities)
    {
        HashSet<Guid> raftModelEntityIds = new HashSet<Guid>();

        foreach (CadEntity entity in entities)
        {
            if (entity is RaftEntity raft)
            {
                raftModelEntityIds.Add(raft.ModelEntityId);
            }
        }

        foreach (CadEntity entity in entities)
        {
            if (entity is TagEntity tag && !raftModelEntityIds.Contains(tag.ModelEntityId))
            {
                throw new InvalidDataException("A saved tag references a model without a raft.");
            }
        }
    }

    /// <summary>
    /// Validates that every saved raft text belongs to a model with a saved raft.
    /// </summary>
    private static void ValidateLoadedRaftTexts(IReadOnlyList<CadEntity> entities)
    {
        HashSet<Guid> raftModelEntityIds = new HashSet<Guid>();

        foreach (CadEntity entity in entities)
        {
            if (entity is RaftEntity raft)
            {
                raftModelEntityIds.Add(raft.ModelEntityId);
            }
        }

        foreach (CadEntity entity in entities)
        {
            if (entity is RaftTextEntity raftText && !raftModelEntityIds.Contains(raftText.ModelEntityId))
            {
                throw new InvalidDataException("Saved raft text references a model without a raft.");
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

        if (string.Equals(entityDto.Type, RaftTypeName, StringComparison.OrdinalIgnoreCase))
        {
            return CreateRaftEntity(entityDto);
        }

        if (string.Equals(entityDto.Type, TagTypeName, StringComparison.OrdinalIgnoreCase))
        {
            return CreateTagEntity(entityDto);
        }
        if (string.Equals(entityDto.Type, RaftTextTypeName, StringComparison.OrdinalIgnoreCase))
        {
            return CreateRaftTextEntity(entityDto);
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
    /// Recreates a mesh entity from embedded position and triangle-index buffers.
    /// </summary>
    private static MeshEntity CreateMeshEntity(GphEntityDto entityDto)
    {
        List<Vector3> vertices = CreateVectorList(entityDto.Vertices, "vertices");

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
            entityDto.SourcePath,
            entityDto.OriginalFileName,
            CreateTransformOrIdentity(entityDto.ImportPlacementTransform),
            CreateTransformOrIdentity(entityDto.UserTransform));
    }

    /// <summary>
    /// Recreates a generated raft and its settings from embedded triangle buffers.
    /// </summary>
    private static RaftEntity CreateRaftEntity(GphEntityDto entityDto)
    {
        if (!entityDto.ModelEntityId.HasValue || entityDto.ModelEntityId.Value == Guid.Empty)
        {
            throw new InvalidDataException("A saved raft is missing its owning model id.");
        }

        if (entityDto.RaftSettings == null || entityDto.TriangleIndices == null)
        {
            throw new InvalidDataException("A saved raft is missing settings or triangle indices.");
        }

        GphRaftSettingsDto settingsDto = entityDto.RaftSettings;
        RaftSettings settings = new RaftSettings(
            settingsDto.Type,
            settingsDto.RaftHeight,
            settingsDto.LipHeight,
            settingsDto.LipWidth,
            MathF.Max(0.0f, settingsDto.FootprintOffset),
            settingsDto.RaftThickness,
            settingsDto.LineThickness,
            settingsDto.FootSize,
            settingsDto.EdgeAngleDegrees,
            settingsDto.MaxSideLength);

        return RaftEntity.CreateLoaded(
            entityDto.Id,
            entityDto.ModelEntityId.Value,
            settings,
            CreateVectorList(entityDto.Vertices, "raft vertices"),
            new List<int>(entityDto.TriangleIndices),
            CreateLayerColorOrDefault(entityDto.Color, entityDto.Id));
    }

    /// <summary>
    /// Recreates a generated raft tag and its editable settings from embedded triangle buffers.
    /// </summary>
    private static TagEntity CreateTagEntity(GphEntityDto entityDto)
    {
        if (!entityDto.ModelEntityId.HasValue || entityDto.ModelEntityId.Value == Guid.Empty)
        {
            throw new InvalidDataException("A saved tag is missing its owning model id.");
        }

        if (entityDto.TagSettings == null
            || entityDto.AttachmentPoint == null
            || entityDto.Tangent == null
            || entityDto.TriangleIndices == null)
        {
            throw new InvalidDataException("A saved tag is missing settings, placement, or triangle indices.");
        }

        GphTagSettingsDto settingsDto = entityDto.TagSettings;
        TagSettings settings = new TagSettings(
            settingsDto.TagHeight,
            settingsDto.EdgeAngleDegrees,
            settingsDto.BorderOffset,
            settingsDto.Text,
            settingsDto.FontFamilyName,
            settingsDto.FontSize,
            settingsDto.TextHeight,
            settingsDto.IsTextFlipped,
            settingsDto.OuterWidth,
            settingsDto.InnerWidth);

        return TagEntity.CreateLoaded(
            entityDto.Id,
            entityDto.ModelEntityId.Value,
            settings,
            CreateVector(entityDto.AttachmentPoint),
            CreateVector(entityDto.Tangent),
            CreateVectorList(entityDto.Vertices, "tag vertices"),
            new List<int>(entityDto.TriangleIndices),
            CreateLayerColorOrDefault(entityDto.Color, entityDto.Id));
    }

    /// <summary>
    /// Recreates saved raft text and its editable settings from embedded triangle buffers.
    /// </summary>
    private static RaftTextEntity CreateRaftTextEntity(GphEntityDto entityDto)
    {
        if (!entityDto.ModelEntityId.HasValue || entityDto.ModelEntityId.Value == Guid.Empty)
        {
            throw new InvalidDataException("Saved raft text is missing its owning model id.");
        }

        if (entityDto.RaftTextSettings == null
            || entityDto.AttachmentPoint == null
            || entityDto.TriangleIndices == null)
        {
            throw new InvalidDataException("Saved raft text is missing settings, placement, or triangle indices.");
        }

        GphRaftTextSettingsDto settingsDto = entityDto.RaftTextSettings;
        RaftTextSettings settings = new RaftTextSettings(
            settingsDto.Text,
            settingsDto.FontFamilyName,
            settingsDto.FontSize,
            settingsDto.TextHeight,
            settingsDto.BorderOffset,
            settingsDto.OrientationDegrees);

        return RaftTextEntity.CreateLoaded(
            entityDto.Id,
            entityDto.ModelEntityId.Value,
            settings,
            CreateVector(entityDto.AttachmentPoint),
            CreateVectorList(entityDto.Vertices, "raft text vertices"),
            new List<int>(entityDto.TriangleIndices),
            CreateLayerColorOrDefault(entityDto.Color, entityDto.Id));
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
            CreateSupportStyleOrDefault(entityDto.SupportStyle),
            CreateBaseAttachmentKindOrDefault(entityDto.BaseAttachmentKind),
            entityDto.BaseDirection == null ? Vector3.UnitZ : CreateVector(entityDto.BaseDirection));
    }

    /// <summary>
    /// Converts an optional saved attachment value while preserving build-plate behavior for older projects.
    /// </summary>
    private static SupportBaseAttachmentKind CreateBaseAttachmentKindOrDefault(int? savedValue)
    {
        if (!savedValue.HasValue)
        {
            return SupportBaseAttachmentKind.BuildPlate;
        }

        SupportBaseAttachmentKind attachmentKind = (SupportBaseAttachmentKind)savedValue.Value;

        if (!Enum.IsDefined(attachmentKind))
        {
            throw new InvalidDataException("A saved support entity contains an unsupported base attachment kind.");
        }

        return attachmentKind;
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
    /// Converts one runtime 2D vector into a stable serialized shape.
    /// </summary>
    private static GphVector2Dto CreateVectorDto(Vector2 vector)
    {
        return new GphVector2Dto
        {
            X = vector.X,
            Y = vector.Y
        };
    }

    /// <summary>
    /// Converts one serialized 2D vector into its runtime value.
    /// </summary>
    private static Vector2 CreateVector(GphVector2Dto vector)
    {
        return new Vector2(vector.X, vector.Y);
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
            ModelBaseHeight = profile.ModelBaseHeight,
            ModelBasePenetrationDepth = profile.ModelBasePenetrationDepth,
            ModelBaseBottomDiameter = profile.ModelBaseBottomDiameter,
            MaxModelBaseAngleFromVerticalDegrees = profile.MaxModelBaseAngleFromVerticalDegrees,
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

        if (style is ButtressSupportStyle buttressStyle)
        {
            return new GphSupportStyleDto
            {
                Kind = ButtressSupportStyleName,
                BranchDiameter = buttressStyle.BranchDiameter
            };
        }

        if (style is BraceMemberSupportStyle braceMemberStyle)
        {
            return new GphSupportStyleDto
            {
                Kind = BraceMemberSupportStyleName,
                BranchDiameter = braceMemberStyle.Diameter
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

        GphRingSupportSettingsDto dto = new GphRingSupportSettingsDto
        {
            FirstPoint = CreateVectorDto(settings.FirstPoint),
            SecondPoint = CreateVectorDto(settings.SecondPoint),
            ThirdPoint = CreateVectorDto(settings.ThirdPoint),
            Spacing = settings.Spacing,
            SurfaceTarget = CreateRingSupportSurfaceTargetName(settings.SurfaceTargetMode),
            BaseGenerationMode = (int)settings.BaseGenerationMode
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
    /// Converts one Ring Support surface-targeting policy into a stable persisted name.
    /// </summary>
    private static string CreateRingSupportSurfaceTargetName(RingSupportSurfaceTargetMode surfaceTargetMode)
    {
        return surfaceTargetMode switch
        {
            RingSupportSurfaceTargetMode.FirstReachable => FirstReachableRingSurfaceTargetName,
            RingSupportSurfaceTargetMode.SelectedFacesOnly => SelectedFacesOnlyRingSurfaceTargetName,
            _ => throw new InvalidDataException($"Ring Support surface target mode '{surfaceTargetMode}' is not supported.")
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
            PlaceSupportsAtBends = settings.PlaceSupportsAtBends,
            SurfaceTarget = CreateLineSupportSurfaceTargetName(settings.SurfaceTargetMode),
            BaseGenerationMode = (int)settings.BaseGenerationMode
        };

        for (int i = 0; i < settings.Points.Count; i++)
        {
            dto.Points.Add(CreateVectorDto(settings.Points[i]));
        }

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
    /// Converts one Line Support surface-targeting policy into a stable persisted name.
    /// </summary>
    private static string CreateLineSupportSurfaceTargetName(LineSupportSurfaceTargetMode surfaceTargetMode)
    {
        return surfaceTargetMode switch
        {
            LineSupportSurfaceTargetMode.FirstReachable => FirstReachableLineSurfaceTargetName,
            LineSupportSurfaceTargetMode.NearestToLine => NearestToLineSurfaceTargetName,
            LineSupportSurfaceTargetMode.SelectedFacesOnly => SelectedFacesOnlyLineSurfaceTargetName,
            _ => throw new InvalidDataException($"Line Support surface target mode '{surfaceTargetMode}' is not supported.")
        };
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
            FinalOffset = settings.FinalOffset,
            BaseGenerationMode = (int)settings.BaseGenerationMode
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
            OffsetSpacing = settings.OffsetSpacing,
            BaseGenerationMode = (int)settings.BaseGenerationMode
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
                ToolSessionId = modifier.ToolSessionId,
                Kind = CreateSupportModifierKindDto(modifier.Kind),
                IsEnabled = modifier.IsEnabled,
                Order = modifier.Order,
                SourceGeneratorRevision = modifier.SourceGeneratorRevision,
                TargetSupportIds = new List<Guid>(modifier.TargetSupportIds),
                TargetSupportIdBatches = CreateTargetSupportIdBatchDtos(modifier.TargetSupportIdBatches),
                ExcludedBracePairs = CreateBracePairDtos(modifier.ExcludedBracePairs),
                ExcludedBraceTargetBatches = CreateTargetSupportIdBatchDtos(modifier.ExcludedBraceTargetBatches),
                ClusterSettings = CreateClusterModifierSettingsDto(modifier.ClusterSettings),
                BraceSettings = CreateBraceModifierSettingsDto(modifier.BraceSettings),
                ButtressSettings = CreateButtressModifierSettingsDto(modifier.ButtressSettings),
                DirectEditSettings = CreateDirectEditSettingsDto(modifier.DirectEditSettings)
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
    /// Converts excluded Brace pairs into their persisted representation.
    /// </summary>
    private static List<GphBracePairDto> CreateBracePairDtos(IReadOnlyList<SupportBracePair> pairs)
    {
        List<GphBracePairDto> result = new List<GphBracePairDto>(pairs.Count);

        for (int i = 0; i < pairs.Count; i++)
        {
            result.Add(new GphBracePairDto
            {
                FirstSupportId = pairs[i].FirstSupportId,
                SecondSupportId = pairs[i].SecondSupportId
            });
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

            case SupportModifierKind.Buttress:
                return ButtressModifierName;

            case SupportModifierKind.DirectEdit:
                return DirectEditModifierName;

            case SupportModifierKind.Delete:
                return DeleteModifierName;

            default:
                throw new NotSupportedException($"Support modifier kind '{kind}' is not supported by the .gph format.");
        }
    }


    /// <summary>
    /// Converts Direct Edit settings into their persisted representation when present.
    /// </summary>
    private static GphDirectEditSettingsDto? CreateDirectEditSettingsDto(SupportDirectEditSettings? settings)
    {
        if (settings == null)
        {
            return null;
        }

        return new GphDirectEditSettingsDto
        {
            BasePosition = CreateVectorDto(settings.BasePosition),
            StemTopZ = settings.StemTopZ,
            BaseAttachmentKind = settings.BaseAttachmentKind.HasValue
                ? (int)settings.BaseAttachmentKind.Value
                : null,
            BaseDirection = settings.BaseDirection.HasValue
                ? CreateVectorDto(settings.BaseDirection.Value)
                : null,
            ModelBaseLength = settings.ModelBaseLength,
            OriginalBasePosition = CreateVectorDto(settings.OriginalBasePosition),
            OriginalStemTopZ = settings.OriginalStemTopZ,
            OriginalBaseAttachmentKind = settings.OriginalBaseAttachmentKind.HasValue
                ? (int)settings.OriginalBaseAttachmentKind.Value
                : null,
            OriginalBaseDirection = settings.OriginalBaseDirection.HasValue
                ? CreateVectorDto(settings.OriginalBaseDirection.Value)
                : null,
            OriginalModelBaseLength = settings.OriginalModelBaseLength
        };
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
    /// Converts brace modifier settings into their persisted representation when present.
    /// </summary>
    private static GphBraceModifierSettingsDto? CreateBraceModifierSettingsDto(SupportBraceModifierSettings? settings)
    {
        if (settings == null)
        {
            return null;
        }

        return new GphBraceModifierSettingsDto
        {
            MinimumBraceAngleDegrees = settings.MinimumBraceAngleDegrees,
            MaximumBraceAngleDegrees = settings.MaximumBraceAngleDegrees,
            MaximumBraceLength = settings.MaximumBraceLength,
            BraceDiameter = settings.BraceDiameter
        };
    }

    /// <summary>
    /// Converts buttress modifier settings into their persisted representation when present.
    /// </summary>
    private static GphButtressModifierSettingsDto? CreateButtressModifierSettingsDto(SupportButtressModifierSettings? settings)
    {
        if (settings == null)
        {
            return null;
        }

        return new GphButtressModifierSettingsDto
        {
            MinimumButtressHeight = settings.MinimumButtressHeight,
            ButtressSpacing = settings.ButtressSpacing,
            BraceSettings = CreateBraceModifierSettingsDto(settings.BraceSettings)
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
            supportProfileDto.MaxHeadAngleFromVerticalDegrees,
            supportProfileDto.ModelBaseHeight ?? SupportDefaults.DefaultModelBaseHeight,
            supportProfileDto.ModelBasePenetrationDepth ?? SupportDefaults.DefaultModelBasePenetrationDepth,
            supportProfileDto.ModelBaseBottomDiameter ?? SupportDefaults.DefaultModelBaseBottomDiameter,
            supportProfileDto.MaxModelBaseAngleFromVerticalDegrees ?? SupportDefaults.DefaultMaxModelBaseAngleFromVerticalDegrees);
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

        if (string.Equals(supportStyleDto.Kind, ButtressSupportStyleName, StringComparison.OrdinalIgnoreCase))
        {
            if (!supportStyleDto.BranchDiameter.HasValue)
            {
                throw new InvalidDataException("A buttress support style is missing its branch diameter.");
            }

            return new ButtressSupportStyle(supportStyleDto.BranchDiameter.Value);
        }

        if (string.Equals(supportStyleDto.Kind, BraceMemberSupportStyleName, StringComparison.OrdinalIgnoreCase))
        {
            if (!supportStyleDto.BranchDiameter.HasValue)
            {
                throw new InvalidDataException("A brace member support style is missing its diameter.");
            }

            return new BraceMemberSupportStyle(supportStyleDto.BranchDiameter.Value);
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
    /// Converts an optional saved entity color into the runtime layer color or a stable legacy fallback.
    /// </summary>
    private static SupportLayerColor CreateLayerColorOrDefault(GphSupportLayerColorDto? colorDto, Guid stableSeed)
    {
        if (colorDto == null)
        {
            return SupportLayerColorGenerator.CreateFromStableSeed(stableSeed);
        }

        return new SupportLayerColor(colorDto.Red, colorDto.Green, colorDto.Blue);
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

        RingSupportSurfaceTargetMode surfaceTargetMode = CreateRingSupportSurfaceTargetModeOrDefault(
            supportLayerGroupDto.RingSupport.SurfaceTarget);
        List<FaceSelectionKey> selectedFaces = new List<FaceSelectionKey>();

        if (supportLayerGroupDto.RingSupport.SelectedFaces != null)
        {
            for (int i = 0; i < supportLayerGroupDto.RingSupport.SelectedFaces.Count; i++)
            {
                GphFaceSelectionDto? selectedFace = supportLayerGroupDto.RingSupport.SelectedFaces[i];

                if (selectedFace == null)
                {
                    throw new InvalidDataException($"A Ring Support group has a null selected face at index {i}.");
                }

                selectedFaces.Add(new FaceSelectionKey(selectedFace.MeshEntityId, selectedFace.TriangleIndex));
            }
        }

        if (surfaceTargetMode == RingSupportSurfaceTargetMode.SelectedFacesOnly && selectedFaces.Count == 0)
        {
            throw new InvalidDataException("A Selected Faces Only Ring Support group is missing its selected faces.");
        }

        return new RingSupportSettings(
            CreateVector(supportLayerGroupDto.RingSupport.FirstPoint),
            CreateVector(supportLayerGroupDto.RingSupport.SecondPoint),
            CreateVector(supportLayerGroupDto.RingSupport.ThirdPoint),
            supportLayerGroupDto.RingSupport.Spacing,
            surfaceTargetMode,
            selectedFaces,
            CreateBaseGenerationModeOrDefault(supportLayerGroupDto.RingSupport.BaseGenerationMode));
    }

    /// <summary>
    /// Converts a saved Ring Support surface-target name while preserving legacy first-reachable behaviour.
    /// </summary>
    private static RingSupportSurfaceTargetMode CreateRingSupportSurfaceTargetModeOrDefault(string? surfaceTargetName)
    {
        if (string.IsNullOrWhiteSpace(surfaceTargetName)
            || string.Equals(surfaceTargetName, FirstReachableRingSurfaceTargetName, StringComparison.OrdinalIgnoreCase))
        {
            return RingSupportSettings.DefaultSurfaceTargetMode;
        }

        if (string.Equals(surfaceTargetName, SelectedFacesOnlyRingSurfaceTargetName, StringComparison.OrdinalIgnoreCase))
        {
            return RingSupportSurfaceTargetMode.SelectedFacesOnly;
        }

        throw new InvalidDataException($"Ring Support surface target '{surfaceTargetName}' is not supported.");
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
        LineSupportSurfaceTargetMode surfaceTargetMode = CreateLineSupportSurfaceTargetModeOrDefault(
            supportLayerGroupDto.LineSupport.SurfaceTarget);
        List<FaceSelectionKey> selectedFaces = new List<FaceSelectionKey>();

        if (supportLayerGroupDto.LineSupport.SelectedFaces != null)
        {
            for (int i = 0; i < supportLayerGroupDto.LineSupport.SelectedFaces.Count; i++)
            {
                GphFaceSelectionDto? selectedFace = supportLayerGroupDto.LineSupport.SelectedFaces[i];

                if (selectedFace == null)
                {
                    throw new InvalidDataException($"A Line Support group has a null selected face at index {i}.");
                }

                selectedFaces.Add(new FaceSelectionKey(selectedFace.MeshEntityId, selectedFace.TriangleIndex));
            }
        }

        if (surfaceTargetMode == LineSupportSurfaceTargetMode.SelectedFacesOnly && selectedFaces.Count == 0)
        {
            throw new InvalidDataException("A Selected Faces Only Line Support group is missing its selected faces.");
        }

        return new LineSupportSettings(
            points,
            supportLayerGroupDto.LineSupport.Spacing,
            placeSupportsAtBends,
            surfaceTargetMode,
            selectedFaces,
            CreateBaseGenerationModeOrDefault(supportLayerGroupDto.LineSupport.BaseGenerationMode));
    }

    /// <summary>
    /// Converts a saved Line Support surface-target name while preserving legacy first-reachable behaviour.
    /// </summary>
    private static LineSupportSurfaceTargetMode CreateLineSupportSurfaceTargetModeOrDefault(string? surfaceTargetName)
    {
        if (string.IsNullOrWhiteSpace(surfaceTargetName)
            || string.Equals(surfaceTargetName, FirstReachableLineSurfaceTargetName, StringComparison.OrdinalIgnoreCase))
        {
            return LineSupportSettings.DefaultSurfaceTargetMode;
        }

        if (string.Equals(surfaceTargetName, NearestToLineSurfaceTargetName, StringComparison.OrdinalIgnoreCase))
        {
            return LineSupportSurfaceTargetMode.NearestToLine;
        }

        if (string.Equals(surfaceTargetName, SelectedFacesOnlyLineSurfaceTargetName, StringComparison.OrdinalIgnoreCase))
        {
            return LineSupportSurfaceTargetMode.SelectedFacesOnly;
        }

        throw new InvalidDataException($"Line Support surface target '{surfaceTargetName}' is not supported.");
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
            supportLayerGroupDto.ContourSupport.FinalOffset,
            CreateBaseGenerationModeOrDefault(supportLayerGroupDto.ContourSupport.BaseGenerationMode));
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
            offsetSpacing,
            CreateBaseGenerationModeOrDefault(supportLayerGroupDto.AreaSupport.BaseGenerationMode));
    }

    /// <summary>
    /// Converts an optional saved base-generation mode while preserving legacy build-plate-only behavior.
    /// </summary>
    private static SupportBaseGenerationMode CreateBaseGenerationModeOrDefault(int? savedValue)
    {
        if (!savedValue.HasValue)
        {
            return SupportBaseGenerationMode.BuildPlateOnly;
        }

        SupportBaseGenerationMode generationMode = (SupportBaseGenerationMode)savedValue.Value;

        if (!Enum.IsDefined(generationMode))
        {
            throw new InvalidDataException("A support generator contains an unsupported base generation mode.");
        }

        return generationMode;
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
                CreateBraceModifierSettingsOrDefault(modifierDto),
                CreateButtressModifierSettingsOrDefault(modifierDto),
                modifierDto.TargetSupportIds ?? new List<Guid>(),
                CreateTargetSupportIdBatchesOrDefault(modifierDto),
                modifierDto.SourceGeneratorRevision,
                CreateExcludedBracePairsOrDefault(modifierDto),
                CreateExcludedBraceTargetBatchesOrDefault(modifierDto),
                modifierDto.ToolSessionId,
                CreateDirectEditSettingsOrDefault(modifierDto)));
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
    /// Restores optional excluded Brace pairs from persisted modifier data.
    /// </summary>
    private static IReadOnlyList<SupportBracePair>? CreateExcludedBracePairsOrDefault(GphSupportModifierDto modifierDto)
    {
        if (modifierDto.ExcludedBracePairs == null || modifierDto.ExcludedBracePairs.Count == 0)
        {
            return null;
        }

        List<SupportBracePair> result = new List<SupportBracePair>(modifierDto.ExcludedBracePairs.Count);

        for (int i = 0; i < modifierDto.ExcludedBracePairs.Count; i++)
        {
            GphBracePairDto pair = modifierDto.ExcludedBracePairs[i]
                ?? throw new InvalidDataException("A Brace modifier contains a null excluded pair.");
            result.Add(new SupportBracePair(pair.FirstSupportId, pair.SecondSupportId));
        }

        return result;
    }

    /// <summary>
    /// Restores compact Brace exclusion batches from persisted modifier data.
    /// </summary>
    private static IReadOnlyList<SupportModifierTargetBatch>? CreateExcludedBraceTargetBatchesOrDefault(
        GphSupportModifierDto modifierDto)
    {
        if (modifierDto.ExcludedBraceTargetBatches == null || modifierDto.ExcludedBraceTargetBatches.Count == 0)
        {
            return null;
        }

        List<SupportModifierTargetBatch> result = new List<SupportModifierTargetBatch>(modifierDto.ExcludedBraceTargetBatches.Count);

        for (int i = 0; i < modifierDto.ExcludedBraceTargetBatches.Count; i++)
        {
            List<Guid>? targetSupportIds = modifierDto.ExcludedBraceTargetBatches[i];

            if (targetSupportIds == null)
            {
                throw new InvalidDataException($"A Brace modifier has a null exclusion batch at index {i}.");
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

        if (string.Equals(kind, ButtressModifierName, StringComparison.OrdinalIgnoreCase))
        {
            return SupportModifierKind.Buttress;
        }

        if (string.Equals(kind, DirectEditModifierName, StringComparison.OrdinalIgnoreCase))
        {
            return SupportModifierKind.DirectEdit;
        }

        if (string.Equals(kind, DeleteModifierName, StringComparison.OrdinalIgnoreCase))
        {
            return SupportModifierKind.Delete;
        }

        throw new InvalidDataException($"Support modifier kind '{kind}' is not supported.");
    }


    /// <summary>
    /// Restores Direct Edit geometry from persisted modifier data.
    /// </summary>
    private static SupportDirectEditSettings? CreateDirectEditSettingsOrDefault(GphSupportModifierDto modifierDto)
    {
        if (!string.Equals(modifierDto.Kind, DirectEditModifierName, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (modifierDto.DirectEditSettings?.BasePosition == null)
        {
            throw new InvalidDataException("A Direct Edit support modifier is missing its geometry settings.");
        }

        Vector3 basePosition = CreateVector(modifierDto.DirectEditSettings.BasePosition);
        Vector3 originalBasePosition = modifierDto.DirectEditSettings.OriginalBasePosition == null
            ? basePosition
            : CreateVector(modifierDto.DirectEditSettings.OriginalBasePosition);
        float originalStemTopZ = modifierDto.DirectEditSettings.OriginalStemTopZ
            ?? modifierDto.DirectEditSettings.StemTopZ;
        SupportBaseAttachmentKind? baseAttachmentKind = CreateBaseAttachmentKindOrNull(
            modifierDto.DirectEditSettings.BaseAttachmentKind);
        SupportBaseAttachmentKind? originalBaseAttachmentKind = CreateBaseAttachmentKindOrNull(
            modifierDto.DirectEditSettings.OriginalBaseAttachmentKind);
        return new SupportDirectEditSettings(
            basePosition,
            modifierDto.DirectEditSettings.StemTopZ,
            baseAttachmentKind,
            modifierDto.DirectEditSettings.BaseDirection == null
                ? null
                : CreateVector(modifierDto.DirectEditSettings.BaseDirection),
            originalBasePosition,
            originalStemTopZ,
            originalBaseAttachmentKind,
            modifierDto.DirectEditSettings.OriginalBaseDirection == null
                ? null
                : CreateVector(modifierDto.DirectEditSettings.OriginalBaseDirection),
            modifierDto.DirectEditSettings.ModelBaseLength,
            modifierDto.DirectEditSettings.OriginalModelBaseLength);
    }

    /// <summary>
    /// Converts an optional saved Direct Edit attachment while preserving legacy geometry-only edits.
    /// </summary>
    private static SupportBaseAttachmentKind? CreateBaseAttachmentKindOrNull(int? savedValue)
    {
        if (!savedValue.HasValue)
        {
            return null;
        }

        SupportBaseAttachmentKind attachmentKind = (SupportBaseAttachmentKind)savedValue.Value;

        if (!Enum.IsDefined(attachmentKind))
        {
            throw new InvalidDataException("A Direct Edit modifier contains an unsupported base attachment kind.");
        }

        return attachmentKind;
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
    /// Converts saved Brace settings into the runtime settings object when present.
    /// </summary>
    private static SupportBraceModifierSettings? CreateBraceModifierSettingsOrDefault(GphSupportModifierDto modifierDto)
    {
        if (!string.Equals(modifierDto.Kind, BraceModifierName, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (modifierDto.BraceSettings == null)
        {
            throw new InvalidDataException("A Brace support modifier is missing its settings.");
        }

        return new SupportBraceModifierSettings(
            modifierDto.BraceSettings.MinimumBraceAngleDegrees,
            modifierDto.BraceSettings.MaximumBraceAngleDegrees,
            modifierDto.BraceSettings.MaximumBraceLength,
            modifierDto.BraceSettings.BraceDiameter);
    }

    /// <summary>
    /// Converts saved Buttress settings into the runtime settings object when present.
    /// </summary>
    private static SupportButtressModifierSettings? CreateButtressModifierSettingsOrDefault(GphSupportModifierDto modifierDto)
    {
        if (!string.Equals(modifierDto.Kind, ButtressModifierName, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (modifierDto.ButtressSettings == null)
        {
            throw new InvalidDataException("A Buttress support modifier is missing its settings.");
        }

        GphBraceModifierSettingsDto? braceSettingsDto = modifierDto.ButtressSettings.BraceSettings;
        SupportBraceModifierSettings braceSettings = braceSettingsDto == null
            ? SupportBraceModifierSettings.CreateDefault()
            : new SupportBraceModifierSettings(
                braceSettingsDto.MinimumBraceAngleDegrees,
                braceSettingsDto.MaximumBraceAngleDegrees,
                braceSettingsDto.MaximumBraceLength,
                braceSettingsDto.BraceDiameter);
        return new SupportButtressModifierSettings(
            modifierDto.ButtressSettings.MinimumButtressHeight,
            modifierDto.ButtressSettings.ButtressSpacing,
            braceSettings);
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
        public int SchemaVersion { get; set; }
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
        public GphTransform3DDto? ImportPlacementTransform { get; set; }
        public GphTransform3DDto? UserTransform { get; set; }
        public Guid? SupportLayerGroupId { get; set; }
        public GphVector3Dto? TipPosition { get; set; }
        public GphVector3Dto? BasePosition { get; set; }
        public int? BaseAttachmentKind { get; set; }
        public GphVector3Dto? BaseDirection { get; set; }
        public GphVector3Dto? HeadDirection { get; set; }
        public float BranchLength { get; set; }
        public GphVector3Dto? BranchDirection { get; set; }
        public GphSupportProfileDto? SupportProfile { get; set; }
        public GphSupportStyleDto? SupportStyle { get; set; }
        public Guid? ModelEntityId { get; set; }
        public GphSupportLayerColorDto? Color { get; set; }
        public GphRaftSettingsDto? RaftSettings { get; set; }
        public GphTagSettingsDto? TagSettings { get; set; }
        public GphRaftTextSettingsDto? RaftTextSettings { get; set; }
        public GphVector3Dto? AttachmentPoint { get; set; }
        public GphVector2Dto? Tangent { get; set; }
    }

    /// <summary>
    /// DTO for persisted procedural raft settings.
    /// </summary>
    private sealed class GphRaftSettingsDto
    {
        public RaftType Type { get; set; } = RaftType.Footprint;
        public float RaftHeight { get; set; } = RaftSettings.DefaultRaftHeight;
        public float LipHeight { get; set; } = RaftSettings.DefaultLipHeight;
        public float LipWidth { get; set; } = RaftSettings.DefaultLipWidth;
        public float FootprintOffset { get; set; }
        public float RaftThickness { get; set; } = RaftSettings.DefaultRaftThickness;
        public float LineThickness { get; set; } = RaftSettings.DefaultLineThickness;
        public float MaxSideLength { get; set; } = RaftSettings.DefaultMaxSideLength;
        public float FootSize { get; set; } = RaftSettings.DefaultFootSize;
        public float EdgeAngleDegrees { get; set; } = RaftSettings.DefaultEdgeAngleDegrees;
    }

    /// <summary>
    /// DTO for persisted editable tag settings.
    /// </summary>
    private sealed class GphTagSettingsDto
    {
        public float TagHeight { get; set; } = TagSettings.DefaultTagHeight;
        public float EdgeAngleDegrees { get; set; } = TagSettings.DefaultEdgeAngleDegrees;
        public float BorderOffset { get; set; } = TagSettings.DefaultBorderOffset;
        public string Text { get; set; } = string.Empty;
        public string FontFamilyName { get; set; } = TagSettings.DefaultFontFamilyName;
        public float FontSize { get; set; } = TagSettings.DefaultFontSize;
        public float TextHeight { get; set; } = TagSettings.DefaultTextHeight;
        public bool IsTextFlipped { get; set; }
        public float? OuterWidth { get; set; }
        public float? InnerWidth { get; set; }
    }

    /// <summary>
    /// DTO for persisted editable raft text settings.
    /// </summary>
    private sealed class GphRaftTextSettingsDto
    {
        public string Text { get; set; } = string.Empty;
        public string FontFamilyName { get; set; } = RaftTextSettings.DefaultFontFamilyName;
        public float FontSize { get; set; } = RaftTextSettings.DefaultFontSize;
        public float TextHeight { get; set; } = RaftTextSettings.DefaultTextHeight;
        public float BorderOffset { get; set; } = RaftTextSettings.DefaultBorderOffset;
        public float OrientationDegrees { get; set; } = RaftTextSettings.DefaultOrientationDegrees;
    }

    /// <summary>
    /// DTO for stable Vector2 serialization without relying on System.Numerics internals.
    /// </summary>
    private sealed class GphVector2Dto
    {
        public float X { get; set; }
        public float Y { get; set; }
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
        public float? ModelBaseHeight { get; set; }
        public float? ModelBasePenetrationDepth { get; set; }
        public float? ModelBaseBottomDiameter { get; set; }
        public float? MaxModelBaseAngleFromVerticalDegrees { get; set; }
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
        public Guid? ToolSessionId { get; set; }
        public string Kind { get; set; } = string.Empty;
        public bool IsEnabled { get; set; } = true;
        public int Order { get; set; }
        public int? SourceGeneratorRevision { get; set; }
        public List<Guid>? TargetSupportIds { get; set; }
        public List<List<Guid>>? TargetSupportIdBatches { get; set; }
        public List<GphBracePairDto>? ExcludedBracePairs { get; set; }
        public List<List<Guid>>? ExcludedBraceTargetBatches { get; set; }
        public GphClusterModifierSettingsDto? ClusterSettings { get; set; }
        public GphBraceModifierSettingsDto? BraceSettings { get; set; }
        public GphButtressModifierSettingsDto? ButtressSettings { get; set; }
        public GphDirectEditSettingsDto? DirectEditSettings { get; set; }
    }

    /// <summary>
    /// DTO for one persisted unordered Brace pair exclusion.
    /// </summary>
    private sealed class GphBracePairDto
    {
        public Guid FirstSupportId { get; set; }
        public Guid SecondSupportId { get; set; }
    }

    /// <summary>
    /// DTO for persisted Direct Edit geometry.
    /// </summary>
    private sealed class GphDirectEditSettingsDto
    {
        public GphVector3Dto? BasePosition { get; set; }
        public float StemTopZ { get; set; }
        public int? BaseAttachmentKind { get; set; }
        public GphVector3Dto? BaseDirection { get; set; }
        public float? ModelBaseLength { get; set; }
        public GphVector3Dto? OriginalBasePosition { get; set; }
        public float? OriginalStemTopZ { get; set; }
        public int? OriginalBaseAttachmentKind { get; set; }
        public GphVector3Dto? OriginalBaseDirection { get; set; }
        public float? OriginalModelBaseLength { get; set; }
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
    /// DTO for persisted Brace modifier settings.
    /// </summary>
    private sealed class GphBraceModifierSettingsDto
    {
        public float MinimumBraceAngleDegrees { get; set; } = SupportBraceModifierSettings.DefaultMinimumBraceAngleDegrees;
        public float MaximumBraceAngleDegrees { get; set; } = SupportBraceModifierSettings.DefaultMaximumBraceAngleDegrees;
        public float MaximumBraceLength { get; set; } = SupportBraceModifierSettings.DefaultMaximumBraceLength;
        public float BraceDiameter { get; set; } = SupportBraceModifierSettings.DefaultBraceDiameter;
    }

    /// <summary>
    /// DTO for persisted Buttress modifier settings.
    /// </summary>
    private sealed class GphButtressModifierSettingsDto
    {
        public float MinimumButtressHeight { get; set; } = SupportButtressModifierSettings.DefaultMinimumButtressHeight;
        public float ButtressSpacing { get; set; } = SupportButtressModifierSettings.DefaultButtressSpacing;
        public GphBraceModifierSettingsDto? BraceSettings { get; set; }
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
        public string? SurfaceTarget { get; set; }
        public int? BaseGenerationMode { get; set; }
        public List<GphFaceSelectionDto?> SelectedFaces { get; set; } = new List<GphFaceSelectionDto?>();
    }

    /// <summary>
    /// DTO for persisted Line Support generator settings.
    /// </summary>
    private sealed class GphLineSupportSettingsDto
    {
        public List<GphVector3Dto?> Points { get; set; } = new List<GphVector3Dto?>();
        public float Spacing { get; set; }
        public bool? PlaceSupportsAtBends { get; set; }
        public string? SurfaceTarget { get; set; }
        public int? BaseGenerationMode { get; set; }
        public List<GphFaceSelectionDto?> SelectedFaces { get; set; } = new List<GphFaceSelectionDto?>();
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
        public int? BaseGenerationMode { get; set; }
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
        public int? BaseGenerationMode { get; set; }
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
