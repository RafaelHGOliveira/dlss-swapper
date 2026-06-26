using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using DLSS_Swapper;

namespace DLSS.Swapper.UnoLinux.Pages;

public sealed partial class InitialLoadingPage : Page
{
    public InitialLoadingPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    async void OnLoaded(object sender, RoutedEventArgs e)
    {
        var app = (App)Application.Current;
        try
        {
            StatusText.Text = "Loading game database...";
            await app.Database.InitializeAsync();
            StatusText.Text = "Loading DLL manifest...";
            await app.DllManager.LoadManifestAsync();
        }
        catch (System.Exception ex)
        {
            Logger.Error(ex, "Async init failed");
        }
        app.MainWindowRef.NavigateTo(typeof(GameGridPage));
    }
}
