using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using System;

namespace Edda.Avalonia;

public sealed partial class App : global::Avalonia.Application {
    public static Func<IClassicDesktopStyleApplicationLifetime?, AppSession>? SessionFactory { get; set; }

    public AppSession? Session { get; private set; }

    public override void Initialize() {
        AvaloniaXamlLoader.Load(this);
        RequestedThemeVariant = global::Avalonia.Styling.ThemeVariant.Light;
    }

    public override void OnFrameworkInitializationCompleted() {
        ResetSession(SessionFactory ?? AppSession.CreateDefault);
        base.OnFrameworkInitializationCompleted();
    }

    public void ResetSession(Func<IClassicDesktopStyleApplicationLifetime?, AppSession> sessionFactory) {
        Session?.CloseForSessionReset();
        Session = sessionFactory(ApplicationLifetime as IClassicDesktopStyleApplicationLifetime);
        Session.Launch();
    }
}