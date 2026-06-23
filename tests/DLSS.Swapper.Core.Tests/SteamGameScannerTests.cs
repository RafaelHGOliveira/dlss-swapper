using System.IO;
using System.Linq;
using DLSS_Swapper.Data.Steam;
using Xunit;

public class SteamGameScannerTests
{
    [Fact]
    public void Scan_ParsesInstalledGame_FromFixture()
    {
        var root = SteamFixture.Make();
        try
        {
            var games = SteamGameScanner.Scan(root);
            var portal = Assert.Single(games);
            Assert.Equal("400", portal.PlatformId);
            Assert.Equal("Portal", portal.Title);
            Assert.Equal(SteamStateFlag.StateFullyInstalled, portal.StateFlags);
            Assert.True(Directory.Exists(portal.InstallPath));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void Scan_SkipsGame_WhenInstallDirMissing()
    {
        var root = SteamFixture.Make();
        try
        {
            Directory.Delete(Path.Combine(root, "steamapps", "common", "Portal"), true);
            Assert.Empty(SteamGameScanner.Scan(root));
        }
        finally { Directory.Delete(root, true); }
    }
}
