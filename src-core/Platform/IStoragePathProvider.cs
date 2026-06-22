namespace DLSS_Swapper.Core.Platform;

public interface IStoragePathProvider
{
    /// <summary>
    /// Diretório base onde ficam settings, banco e dados locais.
    /// Windows: %LOCALAPPDATA%/DLSS Swapper. Linux: $XDG_CONFIG_HOME/dlss-swapper.
    /// </summary>
    string GetStorageRoot();
}
