namespace Edda.UI.Harness;

public interface IUIDriver
{
    void Launch();

    void Shutdown();

    void WaitForIdle();

    void ClickButton(string id);

    void SetText(string id, string value);

    string GetText(string id);

    bool IsVisible(string id);

    bool IsEnabled(string id);

    bool IsChecked(string id);

    void ToggleCheckbox(string id, bool value);

    string GetSelectedValue(string id);

    void SelectDropdown(string id, string value);

    void OpenMenu(string id);

    void SelectMenuItem(string path);

    void SendKeyboardShortcut(string shortcut);

    void Drag(string sourceId, string targetId);

    void InvokeCommand(string commandId);

    void SetTestFileSelection(string path);

    void AssertNotificationContains(string text);
}
