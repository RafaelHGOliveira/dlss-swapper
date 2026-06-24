// Stub — Task 5 implements this
using Avalonia.Controls;
using DLSS_Swapper.Core.Data;
using DLSS_Swapper.Linux.Platform.Linux;

namespace DLSS_Swapper.Linux.Views;

public partial class SwapDialog : Window
{
    public SwapDialog()
    {
        InitializeComponent();
    }

    public SwapDialog(GameBase game, LinuxDLLManager dllManager) : this()
    {
    }
}
