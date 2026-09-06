// Program.cs
// Runs focused smoke tests for reusable WPF user-interface controls.
using System;
using System.Collections.Generic;

namespace Pillar.UI.SmokeTests;

/// <summary>
/// Provides the executable entry point for UI smoke checks.
/// </summary>
public static class Program
{
    /// <summary>
    /// Runs all UI smoke tests and returns a process exit code.
    /// </summary>
    [STAThread]
    public static int Main()
    {
        List<string> failures = new List<string>();
        AppResourceSmokeTests.Run(failures);
        FaceHitMappingSmokeTests.Run(failures);
        FaceSetSelectionToolPanelSmokeTests.Run(failures);
        NumericUpDownSmokeTests.Run(failures);
        ViewportRightClickGestureSmokeTests.Run(failures);

        if (failures.Count > 0)
        {
            Console.Error.WriteLine("UI smoke tests failed:");

            for (int i = 0; i < failures.Count; i++)
            {
                Console.Error.WriteLine(failures[i]);
            }

            return 1;
        }

        Console.WriteLine("UI smoke tests passed.");
        return 0;
    }
}
