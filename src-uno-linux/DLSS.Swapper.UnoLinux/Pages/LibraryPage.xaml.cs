using DLSS_Swapper.Data;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DLSS.Swapper.UnoLinux.Pages;

public sealed partial class LibraryPage : Page
{
    public UnoLibraryPageModel ViewModel { get; } = new();

    // Ordered to match PivotItem index declared in LibraryPage.xaml
    private static readonly GameAssetType[] TypeOrder =
    [
        GameAssetType.DLSS,
        GameAssetType.DLSS_G,
        GameAssetType.DLSS_D,
        GameAssetType.FSR_31_DX12,
        GameAssetType.FSR_31_VK,
        GameAssetType.XeSS,
        GameAssetType.XeLL,
        GameAssetType.XeSS_FG,
        GameAssetType.XeSS_DX11,
    ];

    public LibraryPage()
    {
        InitializeComponent();
        DataContext = ViewModel;
    }

    private void OnPivotSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not Pivot pivot) return;
        int idx = pivot.SelectedIndex;
        if (idx >= 0 && idx < TypeOrder.Length)
            ViewModel.SelectedType = TypeOrder[idx];
    }

    private void OnDownloadClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: DLLRecord record })
            ViewModel.DownloadRecord(record);
    }

    private void OnDeleteClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: DLLRecord record })
            ViewModel.DeleteRecord(record);
    }

    private void OnOpenFolderClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: DLLRecord record })
            ViewModel.OpenFolder(record);
    }
}
