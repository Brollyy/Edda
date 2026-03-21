# Platform-Agnostic UI Test Harness

To safely migrate the Edda UI from WPF to Avalonia, a **platform-agnostic UI test harness** must validate that both implementations behave identically.

The harness acts as the specification of UI behavior.

## Core idea

Tests interact with the UI through a **UI driver interface** rather than directly referencing framework controls.

Example:

```
driver.ClickButton("btnLoadSong")
driver.SetText("txtSongBPM", "120")
driver.OpenMenu("File")
driver.SelectMenuItem("Import Map")
```

The same tests must run against:

- WPF UI implementation
- Avalonia UI implementation

## UI Driver contract

Suggested interface:

```
public interface IUIDriver
{
    void ClickButton(string id);

    void SetText(string id, string value);

    string GetText(string id);

    bool IsVisible(string id);

    bool IsEnabled(string id);

    void SelectDropdown(string id, string value);

    void ToggleCheckbox(string id, bool value);

    void Drag(string sourceId, string targetId);

    void SendKeyboardShortcut(string shortcut);

    void WaitForIdle();
}
```

Each platform implements an adapter:

- `WpfUIDriver`
- `AvaloniaUIDriver`

## Test harness structure

Recommended layout:

```
tests/

Edda.UI.Harness/
    UIDriver.cs
    UIScenario.cs
    ScreenDefinitions/

Edda.Wpf.UI.Tests/
    WpfUIDriver.cs

Edda.Avalonia.UI.Tests/
    AvaloniaUIDriver.cs
```

## Scenario-based tests

Tests should describe user scenarios rather than individual control interactions.

Example scenario:

```
LoadSongScenario

1 open file dialog
2 select song
3 waveform appears
4 timeline initialized
5 playback enabled
```

These scenarios become the acceptance criteria for the migration.

## Coverage expectations

The harness must eventually cover:

- editor startup
- song loading
- waveform generation
- map editing interactions
- timeline navigation
- drum playback
- settings editing
- file import/export
- autosave
- keyboard shortcuts

## Control identification

All UI elements used by the harness must have stable identifiers.

Example:

```
<Button x:Name="btnLoadSong" />
<TextBox x:Name="txtSongBPM" />
```

Avalonia UI must preserve the same identifiers.

## Success criteria

Migration of a screen is considered complete when:

1. All harness tests for that screen pass.
2. The Avalonia driver behaves identically to the WPF driver.
3. No behavior regressions are detected.

This harness ensures that migration is validated against **behavior**, not just appearance.
