using Avalonia.Controls;
using DLSS_Swapper.Core.Interfaces;
using DLSS_Swapper.Core.Platform;

namespace DLSS_Swapper.Linux.Views;

public partial class MainWindow : Window
{
    public MainWindow(IGameLibraryFactory gameFactory, IGameManager gameManager)
    {
        InitializeComponent();
    }
}
