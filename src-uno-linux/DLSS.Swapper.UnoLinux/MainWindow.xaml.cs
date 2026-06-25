namespace DLSS.Swapper.UnoLinux;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        ContentFrame.Navigate(typeof(Pages.GameGridPage));
    }
}
