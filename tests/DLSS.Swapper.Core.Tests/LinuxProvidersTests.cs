using DLSS_Swapper.Linux.Platform.Linux;
using System.IO;
using Xunit;

public class LinuxSteamPathProviderTests
{
    [Fact]
    public void GetSteamInstallPath_ReturnsNull_WhenNoPathExists()
    {
        var provider = new LinuxSteamPathProvider("/home/testuser", _ => false);
        Assert.Null(provider.GetSteamInstallPath());
    }

    [Fact]
    public void GetSteamInstallPath_ReturnsFirst_WhenDotSteamSteamExists()
    {
        var home = "/home/testuser";
        var expected = Path.Combine(home, ".steam", "steam");

        var provider = new LinuxSteamPathProvider(home, path => path == expected);
        Assert.Equal(expected, provider.GetSteamInstallPath());
    }

    [Fact]
    public void GetSteamInstallPath_ReturnsThird_WhenOnlyLocalShareSteamExists()
    {
        var home = "/home/testuser";
        var expected = Path.Combine(home, ".local", "share", "Steam");

        var provider = new LinuxSteamPathProvider(home, path => path == expected);
        Assert.Equal(expected, provider.GetSteamInstallPath());
    }

    [Fact]
    public void GetSteamInstallPath_ReturnsFirst_WhenAllPathsExist()
    {
        var home = "/home/testuser";
        var expectedFirst = Path.Combine(home, ".steam", "steam");

        var provider = new LinuxSteamPathProvider(home, _ => true);
        Assert.Equal(expectedFirst, provider.GetSteamInstallPath());
    }
}

public class LinuxStoragePathProviderTests
{
    [Fact]
    public void GetStorageRoot_UsesXdgConfigHome_WhenSet()
    {
        var provider = new LinuxStoragePathProvider("/home/testuser", key => key == "XDG_CONFIG_HOME" ? "/custom/config" : null);
        var result = provider.GetStorageRoot();
        Assert.Equal("/custom/config/dlss-swapper/", result);
    }

    [Fact]
    public void GetStorageRoot_FallsBackToHomeConfig_WhenXdgNotSet()
    {
        var provider = new LinuxStoragePathProvider("/home/testuser", _ => null);
        var result = provider.GetStorageRoot();
        Assert.Equal("/home/testuser/.config/dlss-swapper/", result);
    }
}
