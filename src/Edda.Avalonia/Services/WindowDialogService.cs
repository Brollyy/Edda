using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using System;

namespace Edda.Avalonia.Services;

public sealed class WindowDialogService : IDialogService {
    public void ShowError(Window? owner, string title, string message, Action? onDismissed) {
        ShowDialog(
            owner,
            title,
            message,
            [new DialogButton("OK", AppDialogResult.Ok)],
            AppDialogResult.Ok,
            result => {
                if (result == AppDialogResult.Ok) {
                    onDismissed?.Invoke();
                }
            }
        );
    }

    public void ShowRecentMapRemovalConfirmation(Window? owner, string title, string message, Action<AppDialogResult> onCompleted) {
        ShowDialog(
            owner,
            title,
            message,
            [
                new DialogButton("Yes", AppDialogResult.Yes),
                new DialogButton("No", AppDialogResult.No),
                new DialogButton("Cancel", AppDialogResult.Cancel)
            ],
            AppDialogResult.Cancel,
            onCompleted
        );
    }

    public void ShowYesNoConfirmation(Window? owner, string title, string message, Action<AppDialogResult> onCompleted) {
        ShowDialog(
            owner,
            title,
            message,
            [
                new DialogButton("Yes", AppDialogResult.Yes),
                new DialogButton("No", AppDialogResult.No)
            ],
            AppDialogResult.No,
            onCompleted
        );
    }

    public void ShowYesNoCancelConfirmation(Window? owner, string title, string message, Action<AppDialogResult> onCompleted) {
        ShowDialog(
            owner,
            title,
            message,
            [
                new DialogButton("Yes", AppDialogResult.Yes),
                new DialogButton("No", AppDialogResult.No),
                new DialogButton("Cancel", AppDialogResult.Cancel)
            ],
            AppDialogResult.Cancel,
            onCompleted
        );
    }

    static void ShowDialog(Window? owner, string title, string message, DialogButton[] buttons, AppDialogResult closeResult, Action<AppDialogResult> onCompleted) {
        var dialog = new Window {
            Title = title,
            Width = 420,
            CanResize = false,
            WindowStartupLocation = owner != null ? WindowStartupLocation.CenterOwner : WindowStartupLocation.CenterScreen,
            SizeToContent = SizeToContent.Height
        };
        AutomationHelper.SetAutomationId(dialog, $"Dialog{title.Replace(" ", string.Empty)}");

        var root = new StackPanel {
            Margin = new Thickness(20),
            Spacing = 16
        };
        root.Children.Add(new TextBlock {
            Text = message,
            TextWrapping = global::Avalonia.Media.TextWrapping.Wrap
        });

        var buttonPanel = new StackPanel {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        root.Children.Add(buttonPanel);

        var resolved = false;
        void Resolve(AppDialogResult result) {
            if (resolved) {
                return;
            }

            resolved = true;
            onCompleted(result);
            dialog.Close();
        }

        foreach (var button in buttons) {
            var dialogButton = AutomationHelper.WithAutomationId(new Button {
                Content = button.Label,
                MinWidth = 84
            }, $"DialogButton{button.Result}");
            dialogButton.Click += (_, _) => Resolve(button.Result);
            buttonPanel.Children.Add(dialogButton);
        }

        dialog.Closed += (_, _) => {
            if (!resolved) {
                resolved = true;
                onCompleted(closeResult);
            }
        };

        dialog.Content = root;
        if (owner != null) {
            dialog.Show(owner);
        } else {
            dialog.Show();
        }
    }

    readonly record struct DialogButton(string Label, AppDialogResult Result);
}
