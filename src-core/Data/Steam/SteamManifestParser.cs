using System.Text.RegularExpressions;

namespace DLSS_Swapper.Core.Data.Steam;

public static partial class SteamManifestParser
{
    [GeneratedRegex(@"^appmanifest_(?<app_id>\d+)\.acf$")]
    private static partial Regex ManifestFileNameRegex();

    /// <summary>
    /// Extrai o Steam app id do caminho de um appmanifest_*.acf.
    /// Agnóstico de separador de path (funciona com / e \).
    /// Retorna null se o nome do arquivo não for um appmanifest válido.
    /// </summary>
    public static string? TryGetAppIdFromManifestPath(string manifestPath)
    {
        if (string.IsNullOrEmpty(manifestPath))
        {
            return null;
        }

        var fileName = Path.GetFileName(manifestPath.Replace('\\', '/'));
        var match = ManifestFileNameRegex().Match(fileName);
        return match.Success ? match.Groups["app_id"].Value : null;
    }
}
