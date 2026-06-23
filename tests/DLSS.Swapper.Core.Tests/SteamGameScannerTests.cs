using System.IO;
using System.Linq;
using DLSS_Swapper.Data.Steam;
using Xunit;

public class SteamGameScannerTests
{
    static string MakeSteamFixture()
    {
        var root = Path.Combine(Path.GetTempPath(), "steamfix_" + Path.GetRandomFileName());
        var steamapps = Path.Combine(root, "steamapps");
        var common = Path.Combine(steamapps, "common", "Portal");
        Directory.CreateDirectory(common);

        File.WriteAllText(Path.Combine(steamapps, "libraryfolders.vdf"),
            "\"libraryfolders\"\n{\n\t\"0\"\n\t{\n\t\t\"path\"\t\"" +
            root.Replace("\\", "\\\\") + "\"\n\t\t\"apps\"\n\t\t{\n\t\t\t\"400\"\t\"1\"\n\t\t}\n\t}\n}\n");

        File.WriteAllText(Path.Combine(steamapps, "appmanifest_400.acf"),
            "\"AppState\"\n{\n\t\"appid\"\t\"400\"\n\t\"name\"\t\"Portal\"\n\t\"StateFlags\"\t\"4\"\n\t\"installdir\"\t\"Portal\"\n}\n");
        return root;
    }

    [Fact]
    public void Scan_ParsesInstalledGame_FromFixture()
    {
        var root = MakeSteamFixture();
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
        var root = MakeSteamFixture();
        try
        {
            Directory.Delete(Path.Combine(root, "steamapps", "common", "Portal"), true);
            Assert.Empty(SteamGameScanner.Scan(root));
        }
        finally { Directory.Delete(root, true); }
    }
}
