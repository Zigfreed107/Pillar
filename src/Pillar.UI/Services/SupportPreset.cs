// SupportPreset.cs
// Stores one named support profile preset for UI selection without coupling support tools to WPF controls.
using Pillar.Core.Supports;
using System;

namespace Pillar.UI.Services;

/// <summary>
/// Represents one user-selectable support preset.
/// </summary>
public sealed class SupportPreset
{
    /// <summary>
    /// Creates one named support preset with a defensive profile copy.
    /// </summary>
    public SupportPreset(string name, SupportProfile profile)
    {
        Name = NormalizeName(name);
        Profile = profile?.Clone() ?? throw new ArgumentNullException(nameof(profile));
    }

    /// <summary>
    /// Gets the user-facing preset name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the support dimensions used when this preset is selected.
    /// </summary>
    public SupportProfile Profile { get; }

    /// <summary>
    /// Returns the name so editable combo boxes show useful text.
    /// </summary>
    public override string ToString()
    {
        return Name;
    }

    /// <summary>
    /// Converts blank preset names into a stable default.
    /// </summary>
    private static string NormalizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "Default";
        }

        return name.Trim();
    }
}
