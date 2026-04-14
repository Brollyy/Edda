using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;
using System;

namespace Edda.Avalonia;

public sealed partial class App : global::Avalonia.Application {
    public static Func<IClassicDesktopStyleApplicationLifetime?, AppSession>? SessionFactory { get; set; }

    public AppSession? Session { get; private set; }

    public override void Initialize() {
        Styles.Add(new FluentTheme());
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
