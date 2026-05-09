// SupportGroupTransformRegenerator.cs
// Regenerates model-owned support groups after mesh transforms without coupling support behavior to WPF or Helix rendering.
using Pillar.Core.Document;
using Pillar.Core.Entities;
using Pillar.Core.Layers;
using Pillar.Core.Supports;
using Pillar.Geometry.Primitives;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace Pillar.Geometry.Supports;

/// <summary>
/// Builds support replacement snapshots for all support groups owned by a transformed mesh.
/// </summary>
public static class SupportGroupTransformRegenerator
{
    /// <summary>
    /// Creates regeneration snapshots that keep support groups attached to the same model-relative anchors after a mesh transform.
    /// </summary>
    public static IReadOnlyList<SupportGroupRegeneration> CreateRegenerations(
        CadDocument document,
        MeshEntity mesh,
        Transform3DData oldUserTransform,
        Transform3DData newUserTransform)
    {
        if (document == null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        if (mesh == null)
        {
            throw new ArgumentNullException(nameof(mesh));
        }

        Matrix4x4 oldWorldTransform = mesh.ImportPlacementTransform.ToMatrix4x4() * oldUserTransform.ToMatrix4x4();
        Matrix4x4 newWorldTransform = mesh.ImportPlacementTransform.ToMatrix4x4() * newUserTransform.ToMatrix4x4();
        Matrix4x4 oldInverseWorldTransform;

        if (!Matrix4x4.Invert(oldWorldTransform, out oldInverseWorldTransform))
        {
            return Array.Empty<SupportGroupRegeneration>();
        }

        List<SupportGroupRegeneration> regenerations = new List<SupportGroupRegeneration>();

        foreach (SupportLayerGroup supportLayerGroup in document.SupportLayerGroups)
        {
            if (supportLayerGroup.ModelEntityId != mesh.Id)
            {
                continue;
            }

            SupportGroupRegeneration? regeneration = CreateRegeneration(
                document,
                mesh,
                supportLayerGroup,
                oldInverseWorldTransform,
                newWorldTransform);

            if (regeneration != null)
            {
                regenerations.Add(regeneration);
            }
        }

        return regenerations;
    }

    /// <summary>
    /// Regenerates one support group using behavior selected from its stored generator metadata.
    /// </summary>
    private static SupportGroupRegeneration? CreateRegeneration(
        CadDocument document,
        MeshEntity mesh,
        SupportLayerGroup supportLayerGroup,
        Matrix4x4 oldInverseWorldTransform,
        Matrix4x4 newWorldTransform)
    {
        IReadOnlyList<SupportEntity> oldSupportEntities = document.GetSupportEntitiesForGroup(supportLayerGroup.Id);
        RingSupportSettings? oldRingSupportSettings = supportLayerGroup.RingSupportSettings;

        if (oldRingSupportSettings != null)
        {
            RingSupportSettings newRingSupportSettings = TransformRingSupportSettings(
                oldRingSupportSettings,
                oldInverseWorldTransform,
                newWorldTransform);
            List<SupportEntity> newRingSupports = CreateRingSupportEntities(
                mesh,
                supportLayerGroup.Id,
                newRingSupportSettings,
                ChooseSupportProfile(oldSupportEntities),
                newWorldTransform);

            return new SupportGroupRegeneration(
                supportLayerGroup,
                oldSupportEntities,
                newRingSupports,
                oldRingSupportSettings,
                newRingSupportSettings);
        }

        if (oldSupportEntities.Count == 0)
        {
            return null;
        }

        List<SupportEntity> newPointSupports = CreatePointSupportEntities(
            supportLayerGroup.Id,
            oldSupportEntities,
            oldInverseWorldTransform,
            newWorldTransform);

        return new SupportGroupRegeneration(
            supportLayerGroup,
            oldSupportEntities,
            newPointSupports,
            null,
            null);
    }

    /// <summary>
    /// Rebuilds point-style supports by transforming each tip anchor with the model and recreating a vertical support.
    /// </summary>
    private static List<SupportEntity> CreatePointSupportEntities(
        Guid supportLayerGroupId,
        IReadOnlyList<SupportEntity> oldSupportEntities,
        Matrix4x4 oldInverseWorldTransform,
        Matrix4x4 newWorldTransform)
    {
        List<SupportEntity> newSupportEntities = new List<SupportEntity>(oldSupportEntities.Count);

        for (int i = 0; i < oldSupportEntities.Count; i++)
        {
            SupportEntity oldSupportEntity = oldSupportEntities[i];
            Vector3 newTipPosition = TransformWorldPointBetweenModelTransforms(
                oldSupportEntity.TipPosition,
                oldInverseWorldTransform,
                newWorldTransform);
            Vector3 newBasePosition = new Vector3(newTipPosition.X, newTipPosition.Y, 0.0f);

            TryAddSupportEntity(newSupportEntities, supportLayerGroupId, newTipPosition, newBasePosition, oldSupportEntity.Profile);
        }

        return newSupportEntities;
    }

    /// <summary>
    /// Rebuilds ring supports from transformed three-point settings and vertical projection against the transformed mesh.
    /// </summary>
    private static List<SupportEntity> CreateRingSupportEntities(
        MeshEntity mesh,
        Guid supportLayerGroupId,
        RingSupportSettings settings,
        SupportProfile supportProfile,
        Matrix4x4 newWorldTransform)
    {
        Circle3D circle;

        if (!Circle3D.TryCreateFromThreePoints(settings.FirstPoint, settings.SecondPoint, settings.ThirdPoint, out circle))
        {
            return new List<SupportEntity>();
        }

        int requestedSupportCount = RingSupportPattern.CalculateSupportCount(circle, settings.Spacing);
        List<Vector3> guidePoints = new List<Vector3>(requestedSupportCount);
        List<SupportEntity> newSupportEntities = new List<SupportEntity>(requestedSupportCount);

        RingSupportPattern.FillGuidePoints(circle, settings.Spacing, guidePoints);

        for (int i = 0; i < guidePoints.Count; i++)
        {
            Vector3 guidePoint = guidePoints[i];
            Vector3 projectedPoint;

            if (!MeshVerticalProjection.TryProjectToMesh(mesh, newWorldTransform, guidePoint, out projectedPoint))
            {
                continue;
            }

            Vector3 basePosition = new Vector3(projectedPoint.X, projectedPoint.Y, 0.0f);
            TryAddSupportEntity(newSupportEntities, supportLayerGroupId, projectedPoint, basePosition, supportProfile);
        }

        return newSupportEntities;
    }

    /// <summary>
    /// Transforms the three stored ring points with the model, then flattens them back onto the first point's horizontal ring plane.
    /// </summary>
    private static RingSupportSettings TransformRingSupportSettings(
        RingSupportSettings oldSettings,
        Matrix4x4 oldInverseWorldTransform,
        Matrix4x4 newWorldTransform)
    {
        Vector3 transformedFirstPoint = TransformWorldPointBetweenModelTransforms(
            oldSettings.FirstPoint,
            oldInverseWorldTransform,
            newWorldTransform);
        Vector3 transformedSecondPoint = TransformWorldPointBetweenModelTransforms(
            oldSettings.SecondPoint,
            oldInverseWorldTransform,
            newWorldTransform);
        Vector3 transformedThirdPoint = TransformWorldPointBetweenModelTransforms(
            oldSettings.ThirdPoint,
            oldInverseWorldTransform,
            newWorldTransform);

        return new RingSupportSettings(
            transformedFirstPoint,
            NormalizePointToRingPlane(transformedFirstPoint, transformedSecondPoint),
            NormalizePointToRingPlane(transformedFirstPoint, transformedThirdPoint),
            oldSettings.Spacing);
    }

    /// <summary>
    /// Carries one world-space support anchor through the old model-local space and back out through the new transform.
    /// </summary>
    private static Vector3 TransformWorldPointBetweenModelTransforms(
        Vector3 worldPoint,
        Matrix4x4 oldInverseWorldTransform,
        Matrix4x4 newWorldTransform)
    {
        Vector3 modelLocalPoint = Vector3.Transform(worldPoint, oldInverseWorldTransform);
        return Vector3.Transform(modelLocalPoint, newWorldTransform);
    }

    /// <summary>
    /// Keeps ring construction points on the horizontal plane defined by the first transformed point.
    /// </summary>
    private static Vector3 NormalizePointToRingPlane(Vector3 firstPoint, Vector3 point)
    {
        return new Vector3(point.X, point.Y, firstPoint.Z);
    }

    /// <summary>
    /// Uses the existing group's first profile when available so regeneration does not scale or reset support dimensions.
    /// </summary>
    private static SupportProfile ChooseSupportProfile(IReadOnlyList<SupportEntity> oldSupportEntities)
    {
        if (oldSupportEntities.Count > 0)
        {
            return oldSupportEntities[0].Profile;
        }

        return SupportDefaults.CreateProfile();
    }

    /// <summary>
    /// Adds one support when the regenerated position is physically valid for the current build plane.
    /// </summary>
    private static void TryAddSupportEntity(
        List<SupportEntity> supportEntities,
        Guid supportLayerGroupId,
        Vector3 tipPosition,
        Vector3 basePosition,
        SupportProfile supportProfile)
    {
        try
        {
            supportEntities.Add(new SupportEntity(supportLayerGroupId, tipPosition, basePosition, supportProfile));
        }
        catch (ArgumentException)
        {
            // A transformed model can place an anchor below the build plane. Keep the group valid by skipping that support.
        }
    }
}
