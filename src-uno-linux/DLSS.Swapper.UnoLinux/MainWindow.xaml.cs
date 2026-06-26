using Microsoft.UI.Xaml.Controls;

namespace DLSS.Swapper.UnoLinux;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        ContentFrame.Navigate(typeof(Pages.InitialLoadingPage));
    }

    internal void NavigateTo(System.Type pageType) => ContentFrame.Navigate(pageType);

    private void MainNavigationView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is not NavigationViewItem item)
            return;

        var tag = item.Tag as string;
        System.Type? pageType = tag switch
        {
            "GameGridPage" => typeof(Pages.GameGridPage),
            // LibraryPage, SettingsPage, AcknowledgementsPage not yet implemented
            _ => null,
        };

        if (pageType is not null)
            ContentFrame.Navigate(pageType);
    }
}
