# AboutWindow

- Status: Complete
- Analysis level: Documented baseline from code and implemented behavior
- Last updated: 2026-03-24

## Source files

- `src/Edda.Wpf/Windows/AboutWindow.xaml`
- `src/Edda.Wpf/Windows/AboutWindow.xaml.cs`
- `src/Edda.Wpf/Windows/MainWindow.MenuItems.cs` (entry point)

## 1. Purpose

Informational window presenting:

- current app display version
- project GitHub link
- community website link

## 2. Entry points and startup flow

### How the window is opened

- opened from MainWindow Help -> About Edda (`MenuItemAboutPage_Click`)
- uses MainWindow `ShowUniqueWindow(() => new AboutWindow())`
- single-instance behavior while open

### Constructor initialization flow

- initializes UI
- sets `TxtVersionNumber.Text` from `Program.DisplayVersionString`

## 3. Named controls and roles

| Control name | Type | Role | Key handlers |
| --- | --- | --- | --- |
| `TxtVersionNumber` | `TextBlock` | Displays app version | constructor assignment |
| `TxtGithubLink` | `TextBlock` | Clickable GitHub URL | `TxtGithubLink_MouseLeftButtonDown`, hover enter/leave |
| `TxtRagnacustomsLink` | `TextBlock` | Clickable Ragnacustoms URL | `TxtRagnacustomsLink_MouseLeftButtonDown`, hover enter/leave |

## 4. Expected initial state

- fixed-size, non-resizable window
- version text populated to current display version
- both links styled as underlined hot-track text
- default cursor changes to hand only while hovering a link

## 5. Supported user actions

### Keyboard shortcuts

- no explicit keyboard shortcuts are implemented

### Mouse interactions

| Interaction | Target | Result |
| --- | --- | --- |
| left click | `TxtGithubLink` | opens GitHub URL via `Helper.OpenWebUrl(...)` |
| left click | `TxtRagnacustomsLink` | opens Ragnacustoms URL via `Helper.OpenWebUrl(...)` |
| hover enter/leave | both links | toggles window cursor between hand and default |

### Drag and drop behavior

- No drag-and-drop behavior is implemented.

### Playback-related interactions

- none

## 6. Validation and state rules

- no user-editable inputs
- no data validation rules
- link actions depend on `Helper.OpenWebUrl` and OS browser handling

## 7. Persistence behavior

- no settings or map data persistence
- read-only informational content, except runtime version text substitution

## 8. Dialogs and file flows

- no dialogs shown by this window
- no direct file I/O/import/export
- external side effect: launches web URLs in system browser

## 9. Dependencies and background work

### Key dependencies

- `Program.DisplayVersionString` for version display
- `Helper.OpenWebUrl(...)` for external link launch

### Background/async behavior

- none; all handlers run synchronously on UI thread

## 10. Timing-sensitive behavior

- none identified; simple event-driven UI

## 11. Test mapping

### Candidate WPF UI baseline tests

- constructor sets version label to current display version string
- clicking each link triggers URL open action with expected text value
- cursor changes to hand on link hover and reverts on leave
- only one About window instance opens from MainWindow helper path

### Candidate Avalonia parity tests

- same version content and static layout intent
- same clickable links and cursor affordance
- same single-instance behavior from Help menu

### Gaps/questions to verify in runtime pass

- URL launch behavior when default browser is unavailable/restricted
- whether About window should be owner-centered vs screen-centered in migrated UI
