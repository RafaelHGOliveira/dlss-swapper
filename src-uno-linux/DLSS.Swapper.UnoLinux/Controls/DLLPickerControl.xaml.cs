using DLSS_Swapper.Core.Data;
using DLSS_Swapper.Data;
using DLSS_Swapper.Linux.Platform.Linux;
using Microsoft.UI.Xaml.Controls;

namespace DLSS.Swapper.UnoLinux.Controls;

public sealed partial class DLLPickerControl : UserControl
{
    public DLLPickerControlModel ViewModel { get; private set; }

    public DLLPickerControl(GameBase game, GameAssetType assetType, LinuxDLLManager dllManager)
    {
        InitializeComponent();
        ViewModel = new DLLPickerControlModel(game, assetType, dllManager);
    }

    void OnErrorInfoBarClosed(InfoBar sender, InfoBarClosedEventArgs e) =>
        ViewModel.ClearError();
}
