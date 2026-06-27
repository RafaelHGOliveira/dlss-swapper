using System;
using System.IO;
using System.Net.Http;
using Microsoft.Extensions.Logging;
using Uno.Resizetizer;
using DLSS_Swapper;
using DLSS_Swapper.Core.Data;
using DLSS_Swapper.Core.Platform;
using DLSS_Swapper.Linux.Platform.Linux;

namespace DLSS.Swapper.UnoLinux;

public partial class App : Application
{
    /// <summary>
    /// Initializes the singleton application object. This is the first line of authored code
    /// executed, and as such is the logical equivalent of main() or WinMain().
    /// </summary>
    public App()
    {
        this.InitializeComponent();
    }

    protected Window? _mainWindow;

    public IGameLibraryFactory GameFactory { get; private set; } = null!;
    public LinuxDLLManager DllManager { get; private set; } = null!;
    public LinuxSettings Settings { get; private set; } = null!;
    public LinuxDatabase Database { get; private set; } = null!;
    public MainWindow MainWindowRef { get; private set; } = null!;

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var storageProvider = new LinuxStoragePathProvider();
        Storage.Initialize(storageProvider);

        var settings = LinuxSettings.Load(Storage.GetStorageFolder());
        Settings = settings;

        Logger.Init(Path.Combine(Storage.GetTemp(), "logs"), settings.LoggingLevel);

        var httpClient = new HttpClient();
        var database = new LinuxDatabase();
        var dllManager = new LinuxDLLManager(httpClient);
        var gameManager = new LinuxGameManager();
        var steamProvider = new LinuxSteamPathProvider();
        var gameFactory = new LinuxGameFactory(steamProvider, gameManager);

        GameBase.DatabaseService = database;
        GameBase.DllManagerService = dllManager;
        GameBase.SettingsService = settings;
        GameBase.GameManagerService = gameManager;
        GameBase.HttpClientService = httpClient;
        GameBase.SteamPathService = steamProvider;

        GameFactory = gameFactory;
        DllManager = dllManager;
        Database = database;

        var mainWindow = new MainWindow();
        MainWindowRef = mainWindow;
        _mainWindow = mainWindow;

        // UseStudio() (Uno Hot Reload) intentionally omitted: this prototype runs via
        // `dotnet run` with no Uno dev server, so it only logs a non-fatal
        // "DevServer isn't able to connect" error. C# hot reload is unavailable with a
        // debugger attached anyway. Re-add under #if DEBUG if running through the IDE.

        _mainWindow.SetWindowIcon();
        // Ensure the current window is active
        _mainWindow.Activate();

        // Apply saved theme on the next UI tick — in Uno/Skia the visual tree is
        // ready only after the first dispatch cycle post-Activate.
        var savedTheme = settings.AppTheme switch
        {
            "Light" => Microsoft.UI.Xaml.ElementTheme.Light,
            "Dark"  => Microsoft.UI.Xaml.ElementTheme.Dark,
            _       => Microsoft.UI.Xaml.ElementTheme.Default,
        };
        if (savedTheme != Microsoft.UI.Xaml.ElementTheme.Default)
            mainWindow.DispatcherQueue.TryEnqueue(() => mainWindow.ApplyTheme(savedTheme));
    }

    /// <summary>
    /// Configures global Uno Platform logging
    /// </summary>
    public static void InitializeLogging()
    {
#if DEBUG
        // Logging is disabled by default for release builds, as it incurs a significant
        // initialization cost from Microsoft.Extensions.Logging setup. If startup performance
        // is a concern for your application, keep this disabled. If you're running on the web or
        // desktop targets, you can use URL or command line parameters to enable it.
        //
        // For more performance documentation: https://platform.uno/docs/articles/Uno-UI-Performance.html

        var factory = LoggerFactory.Create(builder =>
        {
#if __WASM__
            builder.AddProvider(new global::Uno.Extensions.Logging.WebAssembly.WebAssemblyConsoleLoggerProvider());
#elif __IOS__
            builder.AddProvider(new global::Uno.Extensions.Logging.OSLogLoggerProvider());

            // Log to the Visual Studio Debug console
            builder.AddConsole();
#else
            builder.AddConsole();
#endif

            // Exclude logs below this level
            builder.SetMinimumLevel(LogLevel.Information);

            // Default filters for Uno Platform namespaces
            builder.AddFilter("Uno", LogLevel.Warning);
            builder.AddFilter("Windows", LogLevel.Warning);
            builder.AddFilter("Microsoft", LogLevel.Warning);

            // Generic Xaml events
            // builder.AddFilter("Microsoft.UI.Xaml", LogLevel.Debug );
            // builder.AddFilter("Microsoft.UI.Xaml.VisualStateGroup", LogLevel.Debug );
            // builder.AddFilter("Microsoft.UI.Xaml.StateTriggerBase", LogLevel.Debug );
            // builder.AddFilter("Microsoft.UI.Xaml.UIElement", LogLevel.Debug );
            // builder.AddFilter("Microsoft.UI.Xaml.FrameworkElement", LogLevel.Trace );

            // Layouter specific messages
            // builder.AddFilter("Microsoft.UI.Xaml.Controls", LogLevel.Debug );
            // builder.AddFilter("Microsoft.UI.Xaml.Controls.Layouter", LogLevel.Debug );
            // builder.AddFilter("Microsoft.UI.Xaml.Controls.Panel", LogLevel.Debug );

            // builder.AddFilter("Windows.Storage", LogLevel.Debug );

            // Binding related messages
            // builder.AddFilter("Microsoft.UI.Xaml.Data", LogLevel.Debug );
            // builder.AddFilter("Microsoft.UI.Xaml.Data", LogLevel.Debug );

            // Binder memory references tracking
            // builder.AddFilter("Uno.UI.DataBinding.BinderReferenceHolder", LogLevel.Debug );

            // DevServer and HotReload related
            // builder.AddFilter("Uno.UI.RemoteControl", LogLevel.Information);

            // Debug JS interop
            // builder.AddFilter("Uno.Foundation.WebAssemblyRuntime", LogLevel.Debug );
        });

        global::Uno.Extensions.LogExtensionPoint.AmbientLoggerFactory = factory;

#if HAS_UNO
        global::Uno.UI.Adapter.Microsoft.Extensions.Logging.LoggingAdapter.Initialize();
#endif
#endif
    }
}
