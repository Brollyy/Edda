# StartWindow

- Status: Complete
- Analysis level: Documented baseline from code and implemented behavior
- Last updated: 2026-03-24

## Source files

- `src/Edda.Wpf/Windows/StartWindow.xaml`
- `src/Edda.Wpf/Windows/StartWindow.xaml.cs`
- `src/Edda.Wpf/App.xaml` (startup URI)

## 1. Purpose

Application landing screen for starting mapping workflows:

- create a new map
- import StepMania map
- open existing map
- re-open a map from recent list

## 2. Entry points and startup flow

### How the window is opened

- app startup URI is `Windows/StartWindow.xaml` (`App.xaml`)
- also reopened when MainWindow closes with `Close Map` path or open/import failure recovery

### Constructor initialization flow

- initializes UI
- sets displayed version text from `Program.DisplayVersionString`
- populates recent map list from `RecentOpenedFolders`
- attempts to apply Windows 11 rounded corners via `DwmSetWindowAttribute`
  - failure is silently logged to console

### Launch-to-main transitions

- `New Map`: choose folder -> close StartWindow -> show MainWindow -> `InitNewMap`
- `Import Map`: choose folder -> close StartWindow -> show MainWindow -> `InitImportMap`
- `Open Map`: choose folder or recent map path -> close StartWindow -> show MainWindow -> `InitOpenMap`
- in open failure path: remove stale recent entry, reopen StartWindow, close failed MainWindow

## 3. Named controls and roles

| Control name | Type | Role | Key handlers |
| --- | --- | --- | --- |
| `ButtonExit` | `Button` | Hard exits app process | `ButtonExit_Click` |
| `ButtonNewMap` | `Button` | Starts create-new-map workflow | `ButtonNewMap_Click` |
| `ButtonImportMap` | `Button` | Starts import-map workflow | `ButtonImportMap_Click` |
| `ButtonOpenMap` | `Button` | Starts open-map folder workflow | `ButtonOpenMap_Click` |
| `ListViewRecentMaps` | `ListView` | Hosts dynamically generated recent map entries | populated via `PopulateRecentlyOpenedMaps` |
| `TxtVersionNumber` | `TextBlock` | Shows display version string | constructor assignment |
| `InvisibleTitleBar` / window root | layout/mouse surface | custom draggable borderless chrome | `Window_MouseLeftButtonDown` |

Dynamic recent-map item behavior (`CreateRecentMapItem`):

- left click on item -> opens selected map path
- right click on item -> confirm/remove entry from recents

## 4. Expected initial state

- borderless transparent window with custom gradient background
- version text populated
- recent maps list rebuilt from persisted recents
- each recent entry shows map name (or `Untitled Map`) and path
- window draggable from root mouse surface (custom chrome)

## 5. Supported user actions

### Keyboard shortcuts

- no explicit keyboard shortcuts implemented

### Mouse interactions

| Interaction | Target | Result |
| --- | --- | --- |
| click | `ButtonNewMap` | opens folder chooser and launches MainWindow new-map init |
| click | `ButtonImportMap` | opens folder chooser and launches MainWindow import init |
| click | `ButtonOpenMap` | opens map-folder chooser and launches MainWindow open init |
| left click | recent map item | opens selected recent map |
| right click | recent map item | removal confirmation dialog and optional list removal |
| click | `ButtonExit` | calls `Environment.Exit(0)` |
| mouse drag | window surface | calls `DragMove()` for borderless window move |

### Drag and drop behavior

- no drag-and-drop handlers are implemented

### Playback-related interactions

- none (startup/navigation window only)

## 6. Validation and state rules

- open/import/new flows are no-op when folder picker returns null
- recent removal only applied after explicit `Yes` confirmation
- map open/import errors show dialogs and trigger recovery path
- stale recent entries are pruned automatically when opening that path fails

## 7. Persistence behavior

### Recents behavior

- reads recents from shared app `RecentOpenedFolders`
- right-click removal updates in-memory list and persists (`RecentMaps.Write()`)
- failed open removes invalid/stale recent entry and persists update

### Other persistence

- no user settings edited directly in this window
- version value is runtime display only

## 8. Dialogs and file flows

### Dialogs

- confirm dialog for removing recent map item
- error dialogs for import/open failures

### File/folder flows

- folder pickers invoked via helper methods:
  - `Helper.ChooseNewMapFolder()`
  - `Helper.ChooseOpenMapFolder()`
- no direct file parsing in StartWindow itself; delegated to MainWindow init methods

## 9. Dependencies and background work

### Key dependencies

- app-level `RecentOpenedFolders`
- helper folder chooser methods
- MainWindow init entrypoints (`InitNewMap`, `InitImportMap`, `InitOpenMap`)
- Win32 DWM interop for rounded corners

### Background/async behavior

- none; all operations run synchronously in UI event handlers

## 10. Timing-sensitive behavior

- MainWindow must be shown before calling init methods (explicit code comment), otherwise WPF load-time behavior may break flows
- transition sequence intentionally closes StartWindow before initializing MainWindow workflows

## 11. Test mapping

### Candidate WPF UI baseline tests

- constructor populates version and recent map list from persisted recents
- clicking each primary action button routes to correct MainWindow init path
- recent item left click opens map; right click removal respects confirmation choice
- failed recent open removes stale entry and reopens StartWindow
- borderless drag path (`Window_MouseLeftButtonDown`) allows window movement
- exit button terminates app process path

### Candidate Avalonia parity tests

- same startup role and action routing
- same recent-item interaction semantics (open/remove)
- same stale-recent cleanup on open failure
- same version display behavior and startup branding intent

### Gaps/questions to verify in runtime pass

- behavior of `Environment.Exit(0)` on unsaved work in other windows/process states
- rounded-corner interop fallback expectations on non-Windows or older Windows versions
- whether startup window should keep custom chrome in Avalonia migration
