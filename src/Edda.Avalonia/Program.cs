using Avalonia;
using Avalonia.Fonts.Inter;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Edda.Avalonia;

public static class Program {
    const string ExceptionLogPathEnvironmentVariable = "EDDA_TEST_EXCEPTION_LOG_FILE";

    [STAThread]
    public static int Main(string[] args) {
        RegisterTestExceptionLogging();
        return BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp() {
        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
    }

    static void RegisterTestExceptionLogging() {
        AppDomain.CurrentDomain.UnhandledException += (_, eventArgs) => {
            WriteUnhandledExceptionLog(eventArgs.ExceptionObject as Exception ?? new Exception("Unknown unhandled exception."));
        };

        TaskScheduler.UnobservedTaskException += (_, eventArgs) => {
            WriteUnhandledExceptionLog(eventArgs.Exception);
        };
    }

    static void WriteUnhandledExceptionLog(Exception exception) {
        var path = Environment.GetEnvironmentVariable(ExceptionLogPathEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(path)) {
            return;
        }

        try {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory)) {
                Directory.CreateDirectory(directory);
            }

            var builder = new StringBuilder();
            builder.AppendLine($"[{DateTime.UtcNow:O}]");
            builder.AppendLine(exception.ToString());
            builder.AppendLine();
            File.AppendAllText(path, builder.ToString());
        } catch {
            // Best effort diagnostic logging for launched UI tests.
        }
    }
}
