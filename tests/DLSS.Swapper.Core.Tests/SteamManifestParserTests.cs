using DLSS_Swapper.Core.Data.Steam;
using Xunit;

namespace DLSS_Swapper.Core.Tests;

public class SteamManifestParserTests
{
    [Fact]
    public void ExtractsAppId_FromLinuxPath()
    {
        var result = SteamManifestParser.TryGetAppIdFromManifestPath(
            "/home/user/.local/share/Steam/steamapps/appmanifest_220.acf");
        Assert.Equal("220", result);
    }

    [Fact]
    public void ExtractsAppId_FromWindowsPath()
    {
        var result = SteamManifestParser.TryGetAppIdFromManifestPath(
            @"C:\Program Files (x86)\Steam\steamapps\appmanifest_570.acf");
        Assert.Equal("570", result);
    }

    [Fact]
    public void ReturnsNull_WhenNotAManifest()
    {
        var result = SteamManifestParser.TryGetAppIdFromManifestPath(
            "/home/user/.local/share/Steam/steamapps/libraryfolders.vdf");
        Assert.Null(result);
    }

    [Fact]
    public void ReturnsNull_WhenAppIdMissing()
    {
        var result = SteamManifestParser.TryGetAppIdFromManifestPath(
            "/steam/steamapps/appmanifest_.acf");
        Assert.Null(result);
    }
}
