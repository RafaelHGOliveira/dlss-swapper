using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DLSS.Swapper.UnoLinux.Pages;

public sealed partial class SettingsPage : Page
{
    public UnoSettingsPageModel ViewModel { get; } = new();

    public SettingsPage()
    {
        InitializeComponent();
        DataContext = ViewModel;
    }

    private void OnRemoveIgnoredPathClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: string path })
        {
            ViewModel.RemoveIgnoredPath(path);
        }
    }
}
