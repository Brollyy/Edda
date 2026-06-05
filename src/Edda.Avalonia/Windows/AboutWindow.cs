using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Edda.Const;
using System;
using EddaProgram = Edda.Const.Program;

namespace Edda.Avalonia.Windows;

internal sealed class AboutWindow : Window {
    public AboutWindow() {
        Title = "About Edda";
        Width = 420;
        Height = 300;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        AutomationHelper.SetAutomationId(this, "AboutWindow");
        Content = BuildRoot();
    }

    Control BuildRoot() {
        var root = new DockPanel();

        var header = new Border {
            Padding = new Thickness(20),
            Background = new LinearGradientBrush {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                GradientStops = new GradientStops {
                    new GradientStop(Color.Parse("#F7F9FD"), 0),
                    new GradientStop(Color.Parse("#DCE5F5"), 1)
                }
            }
        };
        DockPanel.SetDock(header, Dock.Top);
        header.Child = new StackPanel {
            Spacing = 6,
            Children = {
                new TextBlock {
                    Text = "Edda",
                    FontSize = 42,
                    FontWeight = FontWeight.Bold,
                    Foreground = new SolidColorBrush(Color.Parse("#002668"))
                },
                AutomationHelper.WithAutomationId(new TextBlock {
                    Name = "TxtVersionNumber",
                    Text = $"version {EddaProgram.DisplayVersionString}",
                    FontSize = 18,
                    FontWeight = FontWeight.SemiBold
                }, "TxtVersionNumber")
            }
        };
        root.Children.Add(header);

        var body = new StackPanel {
            Margin = new Thickness(20),
            Spacing = 10
        };
        body.Children.Add(new TextBlock {
            Text = "Edda is an open-source project hosted on GitHub.",
            TextWrapping = TextWrapping.Wrap
        });
        body.Children.Add(CreateLinkButton("TxtGithubLink", EddaProgram.RepositoryURL));
        body.Children.Add(new TextBlock {
            Text = "Join the Ragnacustoms community to discuss Edda, mapping, custom songs and more.",
            TextWrapping = TextWrapping.Wrap
        });
        body.Children.Add(CreateLinkButton("TxtRagnacustomsLink", "https://ragnacustoms.com/"));

        root.Children.Add(body);
        return root;
    }

    static Button CreateLinkButton(string automationId, string url) {
        var button = AutomationHelper.WithAutomationId(new Button {
            Name = automationId,
            Content = url,
            HorizontalAlignment = HorizontalAlignment.Left,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0),
            Foreground = Brushes.DodgerBlue
        }, automationId);
        button.Click += (_, _) => OpenUrl(url);
        return button;
    }

    static void OpenUrl(string url) {
        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("EDDA_TEST_PICKER_QUEUE_FILE"))) {
            return;
        }

        try {
            Helper.OpenWebUrl(url);
        } catch {
            // Ignore failures to launch an external browser.
        }
    }
}