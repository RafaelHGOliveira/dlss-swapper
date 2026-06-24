using Avalonia.Controls;
using DLSS_Swapper.Core.Platform;
using DLSS_Swapper.Linux.Platform.Linux;
using DLSS_Swapper.Linux.ViewModels;

namespace DLSS_Swapper.Linux.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    public MainWindow(IGameLibraryFactory factory, LinuxDLLManager dllManager) : this()
    {
        DataContext = new MainWindowViewModel(factory, dllManager);
        Loaded += async (_, _) =>
        {
            if (DataContext is MainWindowViewModel vm)
                await vm.LoadGamesCommand.ExecuteAsync(null);
        };
    }
}
