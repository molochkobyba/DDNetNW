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

    public string SettingsFilePath => Path.Combine(SettingsDirectory, "settings.json");

    public AppSettings Load()
    {
        var path = GetSettingsPathForRead();
        if (path is null)
        {
            return new AppSettings();
        }

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
    }

    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(SettingsDirectory);
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(SettingsFilePath, json);
    }

    private string? GetSettingsPathForRead()
    {
        if (File.Exists(SettingsFilePath))
        {
            return SettingsFilePath;
        }

        return null;
    }
}
