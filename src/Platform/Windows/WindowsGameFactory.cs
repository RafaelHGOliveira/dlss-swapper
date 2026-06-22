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

    public WindowsGameFactory(ISteamPathProvider steamPathProvider)
    {
        _libraries = new()
        {
            [GameLibrary.Steam] = new SteamLibrary(steamPathProvider),
            [GameLibrary.GOG] = new GOGLibrary(),
            [GameLibrary.EpicGamesStore] = new EpicGamesStoreLibrary(),
            [GameLibrary.UbisoftConnect] = new UbisoftConnectLibrary(),
            [GameLibrary.XboxApp] = new XboxLibrary(),
            [GameLibrary.BattleNet] = new BattleNetLibrary(),
            [GameLibrary.EAApp] = new EAAppLibrary(),
            [GameLibrary.ManuallyAdded] = new ManuallyAddedLibrary(),
        };
    }

    public IReadOnlyList<IGameLibrary> CreateEnabledLibraries() => _libraries.Values.ToList();

    public IGameLibrary? Get(GameLibrary gameLibrary)
        => _libraries.TryGetValue(gameLibrary, out var lib) ? lib : null;
}
