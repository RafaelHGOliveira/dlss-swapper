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

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            try
            {
                var storageProvider = new LinuxStoragePathProvider();
                Storage.Initialize(storageProvider);
                Logger.Init(Path.Combine(Storage.GetTemp(), "logs"), LoggingLevel.Info);

                var httpClient = new HttpClient();
                var database = new LinuxDatabase();
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
                base.OnFrameworkInitializationCompleted();

                // DB table creation and manifest fetch run after the window is shown
                _ = InitializeAsync(database, dllManager);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Startup failed: {ex}");
                Logger.Error(ex, "Startup failed");
                base.OnFrameworkInitializationCompleted();
            }
        }
        else
        {
            base.OnFrameworkInitializationCompleted();
        }
    }

    private static async System.Threading.Tasks.Task InitializeAsync(LinuxDatabase database, LinuxDLLManager dllManager)
    {
        try
        {
            await database.InitializeAsync().ConfigureAwait(false);
            await dllManager.LoadManifestAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Async init failed: {ex}");
            Logger.Error(ex, "Async init failed");
        }
    }
}
