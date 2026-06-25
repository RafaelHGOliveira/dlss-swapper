using DLSS_Swapper.Core.Data;
using DLSS_Swapper.Core.Interfaces;
using DLSS_Swapper.Data;
using DLSS_Swapper.Interfaces;
using System.Collections.Generic;
using System.Linq;

namespace DLSS_Swapper.Linux.Platform.Linux;

// Minimal in-memory IGameManager for v1 Linux. No persistence yet.
public sealed class LinuxGameManager : IGameManager
{
    readonly List<GameBase> _games = new();
    readonly Dictionary<GameLibrary, GameLibrarySettings> _settings = new();

    public GameBase AddGame(GameBase game, bool scrollIntoView = false)
    {
        var existing = _games.FirstOrDefault(g => g.GetType() == game.GetType() && g.PlatformId == game.PlatformId);
        if (existing is not null)
            return existing;
        _games.Add(game);
        return game;
    }

    public void RemoveGame(GameBase game)
    {
        _games.Remove(game);
    }

    public List<TGame> GetGames<TGame>() where TGame : GameBase
        => _games.OfType<TGame>().ToList();

    public TGame? GetGame<TGame>(string platformId) where TGame : GameBase
        => _games.OfType<TGame>().FirstOrDefault(g => g.PlatformId == platformId);

    public void AddUnknownGameAssets(GameLibrary gameLibrary, string gameTitle, List<GameAsset> gameAssets)
    {
        // No-op in v1 — unknown assets not surfaced in UI yet
    }

    public GameLibrarySettings? GetGameLibrarySettings(GameLibrary gameLibrary)
    {
        if (!_settings.TryGetValue(gameLibrary, out var s))
        {
            s = new GameLibrarySettings { GameLibrary = gameLibrary, IsEnabled = true };
            _settings[gameLibrary] = s;
        }
        return s;
    }

    public List<GameLibrary> GetGameLibraries(bool onlyEnabled)
    {
        if (!onlyEnabled) return _settings.Keys.ToList();
        return _settings.Where(kv => kv.Value.IsEnabled).Select(kv => kv.Key).ToList();
    }
}
