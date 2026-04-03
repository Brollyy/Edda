using Avalonia.Controls;
using Edda.Avalonia.Services;

namespace Edda.Avalonia.UI.Tests;

internal sealed class TestDialogService : IDialogService {
    PendingDialog? pendingDialog;

    public void ShowError(Window? owner, string title, string message, Action? onDismissed) {
        pendingDialog = new PendingDialog(result => {
            if (result != AppDialogResult.Ok) {
                throw new InvalidOperationException($"Expected DialogResult.Ok for error dialog '{title}'.");
            }

            onDismissed?.Invoke();
        });
    }

    public void ShowRecentMapRemovalConfirmation(Window? owner, string title, string message, Action<AppDialogResult> onCompleted) {
        pendingDialog = new PendingDialog(onCompleted);
    }

    public void ShowYesNoConfirmation(Window? owner, string title, string message, Action<AppDialogResult> onCompleted) {
        pendingDialog = new PendingDialog(onCompleted);
    }

    public void ShowYesNoCancelConfirmation(Window? owner, string title, string message, Action<AppDialogResult> onCompleted) {
        pendingDialog = new PendingDialog(onCompleted);
    }

    public void Resolve(string commandId) {
        if (pendingDialog == null) {
            throw new InvalidOperationException("There is no pending dialog to resolve.");
        }

        var callback = pendingDialog.Callback;
        pendingDialog = null;
        callback(ParseResult(commandId));
    }

    static AppDialogResult ParseResult(string commandId) {
        return commandId switch {
            "DialogResult.Ok" => AppDialogResult.Ok,
            "DialogResult.Yes" => AppDialogResult.Yes,
            "DialogResult.No" => AppDialogResult.No,
            "DialogResult.Cancel" => AppDialogResult.Cancel,
            _ => throw new InvalidOperationException($"Unsupported dialog command '{commandId}'.")
        };
    }

    sealed class PendingDialog {
        public PendingDialog(Action<AppDialogResult> callback) {
            Callback = callback;
        }

        public Action<AppDialogResult> Callback { get; }
    }
}
