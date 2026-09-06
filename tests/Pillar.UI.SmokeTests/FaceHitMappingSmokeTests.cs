// FaceHitMappingSmokeTests.cs
// Verifies Helix flat-shaded hit indices map back to Pillar's authoritative face ordinals.
using HelixToolkit.SharpDX;
using Pillar.Rendering.Scene;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace Pillar.UI.SmokeTests;

/// <summary>
/// Runs the focused regression check for mesh-face hit identity.
/// </summary>
internal static class FaceHitMappingSmokeTests
{
    /// <summary>
    /// Adds all face-hit mapping failures to the shared UI smoke-test result.
    /// </summary>
    public static void Run(List<string> failures)
    {
        RunTest(failures, "Face hits ignore unset Helix index-start sentinel", ValidateExpandedRenderVertexMapping);
    }

    /// <summary>
    /// Verifies the actual hit render vertices identify the source face even when IndiceStartLocation is unset.
    /// </summary>
    private static void ValidateExpandedRenderVertexMapping()
    {
        HitTestResult hit = new HitTestResult
        {
            IndiceStartLocation = -1,
            TriangleIndices = Tuple.Create(6, 7, 8)
        };
        MethodInfo method = typeof(SceneManager).GetMethod(
            "TryMapFlatShadedHitToTriangleIndex",
            BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Expected the face-hit identity mapper.");
        object?[] arguments = { hit, 4, -1 };
        bool didMap = method.Invoke(null, arguments) is bool result && result;
        int triangleIndex = arguments[2] is int mappedTriangleIndex ? mappedTriangleIndex : -1;

        if (!didMap || triangleIndex != 2)
        {
            throw new InvalidOperationException("Expected expanded render vertices 6, 7, and 8 to map to source face 2.");
        }
    }

    /// <summary>
    /// Records one failed validation while continuing the remaining UI smoke checks.
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
}
