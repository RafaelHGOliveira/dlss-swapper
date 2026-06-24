using System;
using System.IO;
using System.Net.Http;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using DLSS_Swapper;
using DLSS_Swapper.Core.Data;
using DLSS_Swapper.Linux.Platform.Linux;
using DLSS_Swapper.Linux.Views;

namespace DLSS_Swapper.Linux;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override async void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            try
            {
                var storageProvider = new LinuxStoragePathProvider();
                Storage.Initialize(storageProvider);
                Logger.Init(Path.Combine(Storage.GetTemp(), "logs"), LoggingLevel.Info);

                var database = new LinuxDatabase();
                await database.InitializeAsync();

                var httpClient = new HttpClient();
                var dllManager = new LinuxDLLManager(httpClient);
                var settings = new LinuxSettings();
                var gameManager = new LinuxGameManager();
                var steamProvider = new LinuxSteamPathProvider();
                var gameFactory = new LinuxGameFactory(steamProvider, gameManager);

                GameBase.DatabaseService = database;
                GameBase.DllManagerService = dllManager;
                GameBase.SettingsService = settings;
                GameBase.GameManagerService = gameManager;
                GameBase.HttpClientService = httpClient;
                GameBase.SteamPathService = steamProvider;

                desktop.MainWindow = new MainWindow(gameFactory, dllManager);
                _ = dllManager.LoadManifestAsync();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Startup failed");
            }
            finally
            {
                base.OnFrameworkInitializationCompleted();
            }
        }
        else
        {
            base.OnFrameworkInitializationCompleted();
        }
    }
}
