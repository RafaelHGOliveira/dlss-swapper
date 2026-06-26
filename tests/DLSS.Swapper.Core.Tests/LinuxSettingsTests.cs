using DLSS_Swapper.Linux.Platform.Linux;
using System.IO;
using Xunit;

public class LinuxSettingsTests
{
    static string TempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "dlss-settings-test-" + Path.GetRandomFileName());
        Directory.CreateDirectory(dir);
        return dir;
    }

    [Fact]
    public void Load_ReturnsDefaults_WhenNoFileExists()
    {
        var settings = LinuxSettings.Load(TempDir());
        Assert.True(settings.AllowUntrusted);
        Assert.Empty(settings.IgnoredPaths);
        Assert.False(settings.AllowDebugDlls);
    }

    [Fact]
    public void SaveSettings_ThenLoad_RoundTripsValues()
    {
        var dir = TempDir();
        var settings = LinuxSettings.Load(dir);
        settings.AppTheme = "Dark";
        settings.AllowDebugDlls = true;
        settings.IgnoredPaths = new[] { "/games/ignore" };
        settings.HasShownWarning = true;
        settings.SaveSettings();

        var reloaded = LinuxSettings.Load(dir);
        Assert.Equal("Dark", reloaded.AppTheme);
        Assert.True(reloaded.AllowDebugDlls);
        Assert.Equal(new[] { "/games/ignore" }, reloaded.IgnoredPaths);
        Assert.True(reloaded.HasShownWarning);
    }

    [Fact]
    public void Load_ToleratesCorruptJson_ReturnsDefaults()
    {
        var dir = TempDir();
        File.WriteAllText(Path.Combine(dir, "settings.json"), "{ not valid json");
        var settings = LinuxSettings.Load(dir);
        Assert.True(settings.AllowUntrusted);
    }
}
