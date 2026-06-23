using DLSS_Swapper.Core.Interfaces;
using DLSS_Swapper.Core.Platform;
using DLSS_Swapper.Data.BattleNet;
using DLSS_Swapper.Data.EAApp;
using DLSS_Swapper.Data.EpicGamesStore;
using DLSS_Swapper.Data.GOG;
using DLSS_Swapper.Data.ManuallyAdded;
using DLSS_Swapper.Data.Steam;
using DLSS_Swapper.Data.UbisoftConnect;
using DLSS_Swapper.Data.Xbox;
using DLSS_Swapper.Interfaces;
using System.Collections.Generic;
using System.Linq;

namespace DLSS_Swapper.Platform.Windows;

public sealed class WindowsGameFactory : IGameLibraryFactory
{
    readonly Dictionary<GameLibrary, IGameLibrary> _libraries;
    readonly SteamLibrary _steamLibrary;

    public WindowsGameFactory(ISteamPathProvider steamPathProvider, IGameManager gameManager)
    {
        _steamLibrary = new SteamLibrary(steamPathProvider, gameManager);
        _libraries = new()
        {
            [GameLibrary.Steam] = _steamLibrary,
            [GameLibrary.GOG] = new GOGLibrary(),
            [GameLibrary.EpicGamesStore] = new EpicGamesStoreLibrary(),
            [GameLibrary.UbisoftConnect] = new UbisoftConnectLibrary(),
            [GameLibrary.XboxApp] = new XboxLibrary(),
            [GameLibrary.BattleNet] = new BattleNetLibrary(),
            [GameLibrary.EAApp] = new EAAppLibrary(),
            [GameLibrary.ManuallyAdded] = new ManuallyAddedLibrary(),
        };
    }

    /// <summary>
    /// Called after GameManager.Initialize to wire the real IGameManager into SteamLibrary.
    /// </summary>
    public void SetGameManager(IGameManager gameManager) => _steamLibrary.SetGameManager(gameManager);

    public IReadOnlyList<IGameLibrary> CreateEnabledLibraries() => _libraries.Values.ToList();

    public IGameLibrary? Get(GameLibrary gameLibrary)
        => _libraries.TryGetValue(gameLibrary, out var lib) ? lib : null;
}
