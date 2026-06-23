using Avalonia;
using DLSS_Swapper.Linux;

AppBuilder.Configure<App>()
    .UsePlatformDetect()
    .StartWithClassicDesktopLifetime(args);
