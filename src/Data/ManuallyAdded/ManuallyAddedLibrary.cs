using DLSS_Swapper.Core.Data;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using DLSS_Swapper.Interfaces;

namespace DLSS_Swapper.Data.ManuallyAdded;

public class ManuallyAddedLibrary : IGameLibrary
{
    public GameLibrary GameLibrary => GameLibrary.ManuallyAdded;
    public string Name => "Manually Added";

    public Type GameType => typeof(ManuallyAddedGame);


    GameLibrarySettings? _gameLibrarySettings;
    public GameLibrarySettings? GameLibrarySettings => _gameLibrarySettings ??= GameManager.Instance.GetGameLibrarySettings(GameLibrary);

    public ManuallyAddedLibrary()
    {

    }

    public async Task<IReadOnlyList<GameBase>> ListGamesAsync(bool forceNeedsProcessing = false)
    {
        List<Game> games = new List<Game>();
        List<ManuallyAddedGame> dbGames;
        using (await Database.Instance.Mutex.LockAsync())
        {
            dbGames = await Database.Instance.Connection.Table<ManuallyAddedGame>().ToListAsync().ConfigureAwait(false);
        }
        foreach (var dbGame in dbGames)
        {
            var cachedGame = GameManager.Instance.GetGame<ManuallyAddedGame>(dbGame.PlatformId);
            var activeGame = cachedGame ?? dbGame;

            if (activeGame.IsInIgnoredPath())
            {
                continue;
            }

            // If the game is not from cache, force re-processing
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

        return games;
    }

    public bool IsInstalled()
    {
        return true;
    }

    public async Task LoadGamesFromCacheAsync()
    {
        try
        {
            ManuallyAddedGame[] games;
            using (await Database.Instance.Mutex.LockAsync())
            {
                games = await Database.Instance.Connection.Table<ManuallyAddedGame>().ToArrayAsync().ConfigureAwait(false);
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
                GameManager.Instance.AddGame(game);
            }
        }
        catch (Exception err)
        {
            Logger.Error(err);
            Debugger.Break();
        }
    }
}
