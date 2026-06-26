using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using DLSS_Swapper.Core.Data;
using ISettings = DLSS_Swapper.Core.Interfaces.ISettings;

namespace DLSS_Swapper.Linux.Platform.Linux;

public sealed partial class LinuxSettings : ObservableObject, ISettings
{
    [JsonIgnore]
    string _settingsDirectory = string.Empty;

    public string AppTheme { get; set; } = "Default";
    public string Language { get; set; } = string.Empty;
    public LoggingLevel LoggingLevel { get; set; } = LoggingLevel.Info;
    public string[] IgnoredPaths { get; set; } = [];
    public bool AllowUntrusted { get; set; } = true;
    public bool AllowDebugDlls { get; set; } = false;
    public bool OnlyShowDownloadedDlls { get; set; } = false;
    public bool HasShownWarning { get; set; } = false;

    static string FilePath(string dir) => Path.Combine(dir, "settings.json");

    public static LinuxSettings Load(string settingsDirectory)
    {
        LinuxSettings? settings = null;
        var path = FilePath(settingsDirectory);
        if (File.Exists(path))
        {
            try
            {
                var json = File.ReadAllText(path);
                settings = JsonSerializer.Deserialize<LinuxSettings>(json);
            }
            catch { /* corrupt file → fall back to defaults */ }
        }
        settings ??= new LinuxSettings();
        settings._settingsDirectory = settingsDirectory;
        return settings;
    }

    public void SaveSettings()
    {
        Directory.CreateDirectory(_settingsDirectory);
        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(FilePath(_settingsDirectory), json);
    }
}
