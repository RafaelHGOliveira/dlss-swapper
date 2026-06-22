namespace DLSS_Swapper.Core.Platform;

public interface ISteamPathProvider
{
    /// <summary>
    /// Diretório raiz da instalação do Steam, ou null se não detectado.
    /// </summary>
    string? GetSteamInstallPath();
}
