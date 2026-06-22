using DLSS_Swapper.Core.Data;
using DLSS_Swapper.Data;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DLSS_Swapper.Interfaces;

public interface IGameLibrary
{
    GameLibrary GameLibrary { get; }
    GameLibrarySettings? GameLibrarySettings { get; }
    string Name { get; }
    Type GameType { get; }

    Task<IReadOnlyList<GameBase>> ListGamesAsync(bool forceNeedsProcessing);
    Task LoadGamesFromCacheAsync();
    bool IsInstalled();

    public bool IsEnabled
    {
        get
        {
            return GameLibrarySettings?.IsEnabled ?? false;
        }
    }

    public void Disable()
    {
        if (GameLibrarySettings is not null)
        {
            if (GameLibrarySettings.IsEnabled == true)
            {
                GameLibrarySettings.IsEnabled = false;
                GameBase.SettingsService?.SaveSettings();
            }
        }
    }

    public void Enable()
    {
        if (GameLibrarySettings is not null)
        {
            if (GameLibrarySettings.IsEnabled == false)
            {
                GameLibrarySettings.IsEnabled = true;
                GameBase.SettingsService?.SaveSettings();
            }
        }
    }
}
