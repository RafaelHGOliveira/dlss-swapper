using System;
using System.Collections.Generic;
using System.IO;
using DLSS_Swapper.Core.Data.Steam;
using DLSS_Swapper.Data.Steam.Manifest;
using DLSS_Swapper.Helpers;
using ValveKeyValue;

namespace DLSS_Swapper.Data.Steam;

public static class SteamGameScanner
{
    // Walks <installPath>/steamapps, returns one SteamGame per resolvable appmanifest.
    // Pure: no DB, no cache, no UI. Games whose install dir is missing are skipped.
    public static IReadOnlyList<SteamGame> Scan(string installPath)
    {
        var result = new List<SteamGame>();

        var baseSteamAppsFolder = Path.Combine(installPath, "steamapps");

        if (string.IsNullOrWhiteSpace(baseSteamAppsFolder))
        {
            return result;
        }

        var libraryFoldersFile = Path.Combine(baseSteamAppsFolder, "libraryfolders.vdf");
        if (File.Exists(libraryFoldersFile) == false)
        {
            return result;
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
        }

        // Look for files within steamAppsPaths.
        // If the file already exists in knownAppManifestPaths we can skip it.
        // If the file does not exist in knownAppManifestPaths we should process it as it is likely freshly installed.

        foreach (var steamAppPath in steamAppsPaths)
        {
            var appManifestPaths = Directory.GetFiles(steamAppPath, "*.acf", SearchOption.TopDirectoryOnly);
            if (appManifestPaths?.Length > 0)
            {
                foreach (var appManifestPath in appManifestPaths)
                {
                    var appId = SteamManifestParser.TryGetAppIdFromManifestPath(appManifestPath);
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
                        Logger.Error($"SteamGameScanner could not load game {game.Title} ({game.PlatformId}) because install path does not exist: {installDir}");
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

            result.Add(game);
        }

        return result;
    }
}
