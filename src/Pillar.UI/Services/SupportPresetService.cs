// SupportPresetService.cs
// Loads, saves, and selects support presets from user settings so support tools can request profiles without knowing about UI controls.
using Pillar.Core.Supports;
using System;
using System.Collections.ObjectModel;
using System.Text.Json;

namespace Pillar.UI.Services;

/// <summary>
/// Provides user support presets and persists them between application sessions.
/// </summary>
public sealed class SupportPresetService
{
    private const string DefaultPresetName = "Default";

    /// <summary>
    /// Creates the service and loads saved presets from user settings.
    /// </summary>
    public SupportPresetService()
    {
        Presets = new ObservableCollection<SupportPreset>();
        LoadFromSettings();
    }

    /// <summary>
    /// Gets the mutable preset collection for WPF combo box binding.
    /// </summary>
    public ObservableCollection<SupportPreset> Presets { get; }

    /// <summary>
    /// Gets the preset currently selected for new supports.
    /// </summary>
    public SupportPreset SelectedPreset { get; private set; } = new SupportPreset(DefaultPresetName, SupportDefaults.CreateProfile());

    /// <summary>
    /// Raised when the selected preset changes.
    /// </summary>
    public event EventHandler? SelectedPresetChanged;

    /// <summary>
    /// Sets the selected preset by object reference or matching name.
    /// </summary>
    public void SelectPreset(SupportPreset? preset)
    {
        SupportPreset resolvedPreset = preset ?? EnsureDefaultPreset();

        foreach (SupportPreset existingPreset in Presets)
        {
            if (ReferenceEquals(existingPreset, resolvedPreset)
                || string.Equals(existingPreset.Name, resolvedPreset.Name, StringComparison.OrdinalIgnoreCase))
            {
                if (ReferenceEquals(SelectedPreset, existingPreset))
                {
                    return;
                }

                SelectedPreset = existingPreset;
                SelectedPresetChanged?.Invoke(this, EventArgs.Empty);
                return;
            }
        }

        SupportPreset defaultPreset = EnsureDefaultPreset();
        SelectedPreset = defaultPreset;
        SelectedPresetChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Creates or overwrites a preset and makes it the active selection.
    /// </summary>
    public SupportPreset SavePreset(string name, SupportProfile profile)
    {
        string normalizedName = NormalizePresetName(name);
        SupportPreset savedPreset = new SupportPreset(normalizedName, profile);

        for (int i = 0; i < Presets.Count; i++)
        {
            if (string.Equals(Presets[i].Name, normalizedName, StringComparison.OrdinalIgnoreCase))
            {
                Presets[i] = savedPreset;
                SelectedPreset = savedPreset;
                PersistToSettings();
                SelectedPresetChanged?.Invoke(this, EventArgs.Empty);
                return savedPreset;
            }
        }

        Presets.Add(savedPreset);
        SelectedPreset = savedPreset;
        PersistToSettings();
        SelectedPresetChanged?.Invoke(this, EventArgs.Empty);
        return savedPreset;
    }

    /// <summary>
    /// Gets a defensive profile copy for the currently selected preset.
    /// </summary>
    public SupportProfile CreateSelectedProfile()
    {
        return SelectedPreset.Profile.Clone();
    }

    /// <summary>
    /// Loads presets from settings and falls back to a default preset if the setting is empty or invalid.
    /// </summary>
    private void LoadFromSettings()
    {
        Presets.Clear();

        try
        {
            string json = Properties.Settings.Default.SupportPresetsJson;

            if (!string.IsNullOrWhiteSpace(json))
            {
                SupportPresetDto[]? presetDtos = JsonSerializer.Deserialize<SupportPresetDto[]>(json);

                if (presetDtos != null)
                {
                    for (int i = 0; i < presetDtos.Length; i++)
                    {
                        SupportPreset? preset = CreatePresetOrNull(presetDtos[i]);

                        if (preset != null)
                        {
                            Presets.Add(preset);
                        }
                    }
                }
            }
        }
        catch (JsonException)
        {
            Presets.Clear();
        }
        catch (ArgumentException)
        {
            Presets.Clear();
        }

        SupportPreset defaultPreset = EnsureDefaultPreset();
        SelectedPreset = defaultPreset;
    }

    /// <summary>
    /// Persists the current preset collection into the user settings store.
    /// </summary>
    private void PersistToSettings()
    {
        SupportPresetDto[] presetDtos = new SupportPresetDto[Presets.Count];

        for (int i = 0; i < Presets.Count; i++)
        {
            SupportPreset preset = Presets[i];
            presetDtos[i] = new SupportPresetDto
            {
                Name = preset.Name,
                BaseBottomRadius = preset.Profile.BaseBottomRadius,
                BaseHeight = preset.Profile.BaseHeight,
                StemBottomDiameter = preset.Profile.StemBottomDiameter,
                StemTopDiameter = preset.Profile.StemTopDiameter,
                MaximumBranchLength = preset.Profile.MaximumBranchLength,
                ModelClearance = preset.Profile.ModelClearance,
                HeadHeight = preset.Profile.HeadHeight,
                HeadPenetrationDepth = preset.Profile.HeadPenetrationDepth,
                HeadTopDiameter = preset.Profile.HeadTopDiameter,
                MaxHeadAngleFromVerticalDegrees = preset.Profile.MaxHeadAngleFromVerticalDegrees
            };
        }

        Properties.Settings.Default.SupportPresetsJson = JsonSerializer.Serialize(presetDtos);
        Properties.Settings.Default.Save();
    }

    /// <summary>
    /// Ensures the default preset exists and returns it.
    /// </summary>
    private SupportPreset EnsureDefaultPreset()
    {
        foreach (SupportPreset preset in Presets)
        {
            if (string.Equals(preset.Name, DefaultPresetName, StringComparison.OrdinalIgnoreCase))
            {
                return preset;
            }
        }

        SupportPreset defaultPreset = new SupportPreset(DefaultPresetName, SupportDefaults.CreateProfile());
        Presets.Insert(0, defaultPreset);
        return defaultPreset;
    }

    /// <summary>
    /// Converts one loaded DTO into a preset, or null when its payload is incomplete.
    /// </summary>
    private static SupportPreset? CreatePresetOrNull(SupportPresetDto presetDto)
    {
        if (presetDto == null || string.IsNullOrWhiteSpace(presetDto.Name))
        {
            return null;
        }

        SupportProfile profile = new SupportProfile(
            presetDto.BaseBottomRadius,
            presetDto.BaseHeight,
            presetDto.StemBottomDiameter,
            presetDto.StemTopDiameter,
            presetDto.MaximumBranchLength,
            presetDto.ModelClearance,
            presetDto.HeadHeight,
            presetDto.HeadPenetrationDepth,
            presetDto.HeadTopDiameter,
            presetDto.MaxHeadAngleFromVerticalDegrees);

        return new SupportPreset(presetDto.Name, profile);
    }

    /// <summary>
    /// Converts blank names into the default preset name.
    /// </summary>
    private static string NormalizePresetName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return DefaultPresetName;
        }

        return name.Trim();
    }

    /// <summary>
    /// JSON payload for one persisted support preset.
    /// </summary>
    private sealed class SupportPresetDto
    {
        public string Name { get; set; } = string.Empty;
        public float BaseBottomRadius { get; set; }
        public float BaseHeight { get; set; }
        public float StemBottomDiameter { get; set; }
        public float StemTopDiameter { get; set; }
        public float MaximumBranchLength { get; set; }
        public float ModelClearance { get; set; }
        public float HeadHeight { get; set; }
        public float HeadPenetrationDepth { get; set; }
        public float HeadTopDiameter { get; set; }
        public float MaxHeadAngleFromVerticalDegrees { get; set; }
    }
}
