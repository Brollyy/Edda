using Edda.UI.Harness;

namespace Edda.Avalonia.UI.Tests;

public class AvaloniaUIDriver : IUIDriver
{
    public void Launch() { }

    public void Shutdown() { }

    public void WaitForIdle() { }

    public void ClickButton(string id) { }

    public void SetText(string id, string value) { }

    public string GetText(string id) => string.Empty;

    public bool IsVisible(string id) => false;

    public bool IsEnabled(string id) => false;

    public bool IsChecked(string id) => false;

    public void ToggleCheckbox(string id, bool value) { }

    public string GetSelectedValue(string id) => string.Empty;

    public void SelectDropdown(string id, string value) { }

    public void OpenMenu(string id) { }

    public void SelectMenuItem(string path) { }

    public void SendKeyboardShortcut(string shortcut) { }

    public void Drag(string sourceId, string targetId) { }

    public void InvokeCommand(string commandId) { }

    public void SetTestFileSelection(string path) { }

    public void AssertNotificationContains(string text) { }
}
