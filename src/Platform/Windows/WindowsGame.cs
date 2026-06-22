using DLSS_Swapper.Core.Data;
using System;
using System.Threading.Tasks;

namespace DLSS_Swapper.Platform.Windows;

/// <summary>
/// Abstract base for all Windows game types. Overrides the four platform hooks
/// in GameBase with the actual WinUI / WinTrust implementations.
/// </summary>
public abstract class WindowsGame : GameBase
{
    protected override void RunOnUIThread(Action action)
        => App.CurrentApp.RunOnUIThread(action);

    protected override Task RunOnUIThreadAsync(Func<Task> action)
        => App.CurrentApp.RunOnUIThreadAsync(action);

    protected override bool IsAdminUser()
        => App.CurrentApp.IsAdminUser();

    protected override bool VerifySignature(string dllPath)
        => WinTrust.VerifyEmbeddedSignature(dllPath);
}
