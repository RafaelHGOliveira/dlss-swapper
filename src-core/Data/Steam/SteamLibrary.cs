using DLSS_Swapper.Core.Data;
using DLSS_Swapper.Core.Interfaces;
using DLSS_Swapper.Core.Platform;
using DLSS_Swapper.Data;
using DLSS_Swapper.Helpers;
using DLSS_Swapper.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace DLSS_Swapper.Data.Steam;

public partial class SteamLibrary : IGameLibrary
{
    public GameLibrary GameLibrary => GameLibrary.Steam;
    public string Name => "Steam";

    public Type GameType => typeof(SteamGame);

    GameLibrarySettings? _gameLibrarySettings;
    public GameLibrarySettings? GameLibrarySettings => _gameLibrarySettings ??= _gameManager.GetGameLibrarySettings(GameLibrary);

    readonly ISteamPathProvider _steamPathProvider;
    IGameManager _gameManager;

    public SteamLibrary(ISteamPathProvider steamPathProvider, IGameManager gameManager)
    {
        _steamPathProvider = steamPathProvider;
        _gameManager = gameManager;
    }

    /// <summary>
    /// Allows the Windows host to wire the game manager after GameManager.Initialize completes.
    /// </summary>
    public void SetGameManager(IGameManager gameManager)
    {
        _gameManager = gameManager;
    }

    public bool IsInstalled()
    {
        return _steamPathProvider.GetSteamInstallPath() is not null;
    }

    readonly string[] _defaultHiddenGames = [
        "228980", // Steamworks Common Redistributables
];

    public async Task<IReadOnlyList<GameBase>> ListGamesAsync(bool forceNeedsProcessing = false)
    {
        // If we don't detect a steam install path return an empty list.
        var installPath = _steamPathProvider.GetSteamInstallPath();
        if (string.IsNullOrEmpty(installPath))
        {
            return new List<GameBase>();
        }

        var cachedGames = _gameManager.GetGames<SteamGame>();

        var scannedGames = SteamGameScanner.Scan(installPath);

        var games = new List<GameBase>();

        foreach (var game in scannedGames)
        {
            var cachedGame = _gameManager.GetGame<SteamGame>(game.PlatformId);
            var activeGame = cachedGame ?? game;

            if (activeGame.IsHidden is null && _defaultHiddenGames.Contains(activeGame.PlatformId))
            {
                activeGame.IsHidden = true;
            }

            activeGame.Title = game.Title;
            activeGame.InstallPath = game.InstallPath;
            activeGame.StateFlags = game.StateFlags;

            if (activeGame.IsInIgnoredPath())
            {
                continue;
            }

            await activeGame.SaveToDatabaseAsync().ConfigureAwait(false);

            // If the game is not from cache, force processing
            if (cachedGame is null)
            {
                activeGame.NeedsProcessing = true;
            }

            if (activeGame.NeedsProcessing == true || forceNeedsProcessing == true)
            {
                activeGame.ProcessGame(forceNeedsProcessing: forceNeedsProcessing);
            }

            games.Add(activeGame);
        }

        games.Sort();

        // Delete games that are no longer loaded, they are likely uninstalled
        foreach (var cachedGame in cachedGames)
        {
            // Game is to be deleted.
            if (games.Contains(cachedGame) == false)
            {
                await cachedGame.DeleteAsync().ConfigureAwait(false);
            }
        }

        return games;
    }

    public async Task LoadGamesFromCacheAsync()
    {
        if (GameBase.DatabaseService is null)
        {
            Logger.Error("DatabaseService is null; cannot load Steam games from cache.");
            return;
        }

        try
        {
            SteamGame[] games;
            using (await GameBase.DatabaseService.Mutex.LockAsync())
            {
                games = await GameBase.DatabaseService.Connection.Table<SteamGame>().ToArrayAsync().ConfigureAwait(false);
            }
            foreach (var game in games)
            {
                if (game.IsInIgnoredPath())
                {
                    continue;
                }

                if (Directory.Exists(game.InstallPath) == false)
                {
                    Logger.Warning($"{Name} library could not load game {game.Title} ({game.PlatformId}) from cache because install path does not exist: {game.InstallPath}");
                    // We remove the list of known game assets, but not the game itself.
                    // Removing the game will remove its history, notes, and other data.
                    // We don't want to do this in case it is just a temporary issue.
                    await game.RemoveGameAssetsFromCacheAsync().ConfigureAwait(false);
                    continue;
                }

                await game.LoadGameAssetsFromCacheAsync().ConfigureAwait(false);
                _gameManager.AddGame(game);
            }
        }
        catch (Exception err)
        {
            Logger.Error(err);
        }
    }
}
