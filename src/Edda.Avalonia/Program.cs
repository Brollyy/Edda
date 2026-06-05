using Avalonia;
using Avalonia.Fonts.Inter;
using System;

namespace Edda.Avalonia;

public static class Program {
    [STAThread]
    public static int Main(string[] args) {
        return BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp() {
        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
    }
}