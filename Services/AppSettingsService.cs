using System;
using System.IO;
using System.Text.Json;
using DDNetNW.Models;

namespace DDNetNW.Services;

public sealed class AppSettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public string SettingsDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        AppMetadata.Name);

    public string SettingsFilePath => Path.Combine(SettingsDirectory, "settings-v1.35.json");

    public string PreviousSettingsFilePath => Path.Combine(SettingsDirectory, "settings-v1.20.json");

    public string LegacySettingsFilePath => Path.Combine(SettingsDirectory, "settings.json");

    public AppSettings Load()
    {
        var filePath = File.Exists(SettingsFilePath)
            ? SettingsFilePath
            : File.Exists(PreviousSettingsFilePath)
                ? PreviousSettingsFilePath
                : LegacySettingsFilePath;

        if (!File.Exists(filePath))
        {
            return new AppSettings();
        }

        var json = File.ReadAllText(filePath);
        return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
    }

    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(SettingsDirectory);
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(SettingsFilePath, json);
    }
}
