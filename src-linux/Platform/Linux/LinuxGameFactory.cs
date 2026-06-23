using DLSS_Swapper.Core.Interfaces;
using DLSS_Swapper.Core.Platform;
using DLSS_Swapper.Data.Steam;
using DLSS_Swapper.Interfaces;
using System.Collections.Generic;
using System.Linq;

namespace DLSS_Swapper.Linux.Platform.Linux;

public sealed class LinuxGameFactory : IGameLibraryFactory
{
    readonly Dictionary<GameLibrary, IGameLibrary> _libraries;

    public LinuxGameFactory(ISteamPathProvider steamPathProvider, IGameManager gameManager)
    {
        _libraries = new()
        {
            [GameLibrary.Steam] = new SteamLibrary(steamPathProvider, gameManager),
        };
    }

    public IReadOnlyList<IGameLibrary> CreateEnabledLibraries() => _libraries.Values.ToList();

    public IGameLibrary? Get(GameLibrary gameLibrary)
        => _libraries.TryGetValue(gameLibrary, out var lib) ? lib : null;
}
