using Uno.UI.Hosting;

namespace DLSS.Swapper.UnoLinux;

internal class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        App.InitializeLogging();

        // Disable OpenGL on X11 to avoid EGL/GLX failures on NVIDIA + XWayland.
        // Falls back to X11SoftwareRenderer (Skia CPU-side). Remove once
        // the GL/Vulkan path is confirmed working on this hardware.
        Uno.UI.FeatureConfiguration.Rendering.UseOpenGLOnX11 = false;

        var host = UnoPlatformHostBuilder.Create()
            .App(() => new App())
            .UseX11()
            .UseLinuxFrameBuffer()
            .UseMacOS()
            .UseWin32()
            .Build();

        host.Run();
    }
}
