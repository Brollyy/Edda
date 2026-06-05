using Avalonia.Controls;
using System;

namespace Edda.Avalonia.Services;

public interface IDialogService {
    void ShowError(Window? owner, string title, string message, Action? onDismissed);
    void ShowRecentMapRemovalConfirmation(Window? owner, string title, string message, Action<AppDialogResult> onCompleted);
    void ShowYesNoConfirmation(Window? owner, string title, string message, Action<AppDialogResult> onCompleted);
    void ShowYesNoCancelConfirmation(Window? owner, string title, string message, Action<AppDialogResult> onCompleted);
}