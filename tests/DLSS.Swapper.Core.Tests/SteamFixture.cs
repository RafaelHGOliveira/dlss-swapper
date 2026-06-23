using System.IO;

/// <summary>
/// Shared helper that builds a minimal on-disk Steam fixture for tests.
/// </summary>
static class SteamFixture
{
    public static string Make()
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
}
