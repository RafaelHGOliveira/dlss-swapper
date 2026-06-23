using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
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
            var storageProvider = new LinuxStoragePathProvider();
            Storage.Initialize(storageProvider);

            var gameManager = new LinuxGameManager();
            var steamProvider = new LinuxSteamPathProvider();
            var gameFactory = new LinuxGameFactory(steamProvider, gameManager);

            desktop.MainWindow = new MainWindow(gameFactory, gameManager);
        }

        base.OnFrameworkInitializationCompleted();
    }
}
