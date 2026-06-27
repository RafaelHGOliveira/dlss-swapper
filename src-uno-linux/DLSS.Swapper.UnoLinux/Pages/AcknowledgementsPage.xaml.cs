using Microsoft.UI.Xaml.Controls;

namespace DLSS.Swapper.UnoLinux.Pages;

public sealed partial class AcknowledgementsPage : Page
{
    public UnoAcknowledgementsPageModel ViewModel { get; } = new();

    public AcknowledgementsPage()
    {
        InitializeComponent();
        DataContext = ViewModel;
    }
}
