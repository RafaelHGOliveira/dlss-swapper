using DLSS_Swapper.Core.Data;
using DLSS_Swapper.Core.Platform;
using DLSS_Swapper.Data.Steam.Manifest;
using DLSS_Swapper.Helpers;
using DLSS_Swapper.Interfaces;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ValveKeyValue;

namespace DLSS_Swapper.Data.Steam;

internal partial class SteamLibrary : IGameLibrary
{
    public GameLibrary GameLibrary => GameLibrary.Steam;
    public string Name => "Steam";

    public Type GameType => typeof(SteamGame);

    GameLibrarySettings? _gameLibrarySettings;
    public GameLibrarySettings? GameLibrarySettings => _gameLibrarySettings ??= GameManager.Instance.GetGameLibrarySettings(GameLibrary);

    readonly ISteamPathProvider _steamPathProvider;

    public SteamLibrary(ISteamPathProvider steamPathProvider)
    {
        _steamPathProvider = steamPathProvider;
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
        if (installPath is null)
        {
            return new List<Game>();
        }

        var cachedGames = GameManager.Instance.GetGames<SteamGame>();

        // I hope this runs on a background thread.
        // Tasks are whack.

        // Base steamapps folder contains libraryfolders.vdf which has references to other steamapps folders and individual installed Steam games.
        // All of these folders contain appmanifest_[some_id].acf which contains information about the game.

        var baseSteamAppsFolder = Path.Combine(installPath, "steamapps");

        // This should never happen, but it is a compiler hint for later.
        if (string.IsNullOrWhiteSpace(baseSteamAppsFolder))
        {
            return new List<Game>();
        }

        var libraryFoldersFile = Path.Combine(baseSteamAppsFolder, "libraryfolders.vdf");
        if (File.Exists(libraryFoldersFile) == false)
        {
            return new List<Game>();
        }

        var libraryFoldersFileInfo = new FileInfo(libraryFoldersFile);

        var steamAppsPaths = new List<string>()
        {
            baseSteamAppsFolder
        };
        var knownAppManifestPaths = new Dictionary<string, string>();

        var kvSerializer = KVSerializer.Create(KVSerializationFormat.KeyValues1Text);

        try
        {
            using (var fileStream = File.OpenRead(libraryFoldersFile))
            {
                var libraryFoldersVDF = kvSerializer.Deserialize<Dictionary<string, LibraryFoldersVDF>>(fileStream);
                foreach (var libraryFolderVDF in libraryFoldersVDF)
                {
                    var path = PathHelpers.NormalizePath(libraryFolderVDF.Value.Path);
                    path = Path.Combine(path, "steamapps");

                    if (string.IsNullOrWhiteSpace(path) == false && Directory.Exists(path))
                    {
                        steamAppsPaths.Add(path);

                        foreach (var steamApp in libraryFolderVDF.Value.Apps)
                        {
                            // If the appManifestPath does not exist it is likely the game was freshly uninstalled.
                            var appManifestPath = Path.Combine(path, $"appmanifest_{steamApp.Key}.acf");
                            if (File.Exists(appManifestPath))
                            {
                                if (knownAppManifestPaths.ContainsKey(steamApp.Key) == false)
                                {
                                    knownAppManifestPaths[steamApp.Key] = appManifestPath;
                                }
                                else
                                {
                                    Logger.Error($"Went to add {steamApp.Key} to knownAppManifestPaths, but this key already exists.");
                                }
                            }
                            else
                            {
                                Logger.Error($"Expected manifest path was not found - {appManifestPath}");
                            }
                        }
                    }
                }
            }
        }
        catch (Exception err)
        {
            Logger.Error(err, $"Unable to process {libraryFoldersFile}");
            Debugger.Break();
        }

        // Look for files within steamAppsPaths.
        // If the file already exists in knownAppManifestPaths we can skip it.
        // If the file does not exist in knownAppManifestPaths we should process it as it is likly freshly installed.

        foreach (var steamAppPath in steamAppsPaths)
        {
            var appManifestPaths = Directory.GetFiles(steamAppPath, "*.acf", SearchOption.TopDirectoryOnly);
            if (appManifestPaths?.Length > 0)
            {
                foreach (var appManifestPath in appManifestPaths)
                {
                    var appId = DLSS_Swapper.Core.Data.Steam.SteamManifestParser.TryGetAppIdFromManifestPath(appManifestPath);
                    if (appId is not null)
                    {

                        // If the app_id is not known this is either a new install or a corrupt/leftover file.
                        if (knownAppManifestPaths.ContainsKey(appId) == false)
                        {
                            var fileInfo = new FileInfo(appManifestPath);

                            // If the appManifest is newer than the last time libraryFoldersFileInfo was updated it is likely a new install.
                            if (fileInfo.LastWriteTime > libraryFoldersFileInfo.LastWriteTime)
                            {
                                knownAppManifestPaths[appId] = appManifestPath;
                            }
                            else
                            {
                                Logger.Error($"Found potential rogue file when loading Steam manifests: appId {appId}, {appManifestPath}");
                            }
                        }
                    }
                }
            }
        }

        var games = new List<Game>();

        foreach (var appManifestPath in knownAppManifestPaths.Values)
        {
            SteamGame? game;

            try
            {
                using (var fileStream = File.OpenRead(appManifestPath))
                {
                    var appManifestACF = kvSerializer.Deserialize<AppManifestACF>(fileStream);

                    if (appManifestACF is null || string.IsNullOrEmpty(appManifestACF.AppId))
                    {
                        Logger.Error($"Unable to parse app manifest - {appManifestPath}");
                        continue;
                    }

                    game = new SteamGame(appManifestACF.AppId);

                    if (Enum.TryParse(appManifestACF.StateFlags, out SteamStateFlag stateFlags) == false)
                    {
                        // The AppState couldn't be parsed from the appmanifest_*.acf
                        Logger.Error($"Unable to parse StateFlags {appManifestACF.StateFlags} for app {appManifestACF.AppId} in {appManifestPath}");
                        continue;
                    }
                    game.StateFlags = stateFlags;
                    game.Title = appManifestACF.Name;

                    var baseDir = Path.GetDirectoryName(appManifestPath);
                    if (string.IsNullOrEmpty(baseDir))
                    {
                        continue;
                    }

                    var installDir = PathHelpers.NormalizePath(Path.Combine(baseDir, "common", appManifestACF.InstallDir));
                    if (Directory.Exists(installDir) == false)
                    {
                        // If the install directory does not exist, skip this game.
                        Logger.Error($"SteamLibary could not load game {game.Title} ({game.PlatformId}) because install path does not exist: {installDir}");
                        continue;
                    }
                    game.InstallPath = installDir;
                }
            }
            catch (Exception err)
            {
                Logger.Error(err);
                continue;
            }

            var cachedGame = GameManager.Instance.GetGame<SteamGame>(game.PlatformId);
            var activeGame = cachedGame ?? game;

            if (activeGame.IsHidden is null && _defaultHiddenGames.Contains(activeGame.PlatformId))
            {
                activeGame.IsHidden = true;
            }


            activeGame.Title = game.Title;  // TODO: Will this be a problem if the game is already loaded
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
        try
        {
            SteamGame[] games;
            using (await Database.Instance.Mutex.LockAsync())
            {
                games = await Database.Instance.Connection.Table<SteamGame>().ToArrayAsync().ConfigureAwait(false);
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
