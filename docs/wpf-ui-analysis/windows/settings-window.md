# SettingsWindow

- Status: Complete
- Analysis level: Documented baseline from code and implemented behavior
- Last updated: 2026-03-24

## Source files

- `src/Edda.Wpf/Windows/SettingsWindow.xaml`
- `src/Edda.Wpf/Windows/SettingsWindow.xaml.cs`
- `src/Edda.Wpf/Windows/MainWindow.MenuItems.cs` (entry point)
- `src/Edda.Wpf/Windows/MainWindow.xaml.cs` (settings application path)

## 1. Purpose

Global settings editor for app-wide behavior and editor defaults:

- editor defaults (mapper, note speed, grid spacing, paste behavior)
- audio output and playback defaults
- spectrogram rendering options
- Discord/update toggles
- map save location behavior

## 2. Entry points and startup flow

### How the window is opened

- opened from MainWindow via Tools -> Settings (`MenuItemSettings_Click`)
- uses single-instance behavior through `ShowUniqueWindow(() => new SettingsWindow(this, userSettings))`
- window is modal-like in ownership (`Owner = MainWindow`) but shown non-blocking

### Constructor initialization flow

- receives `MainWindow caller` and shared `UserSettingsManager`
- sets `doneInit = false` to avoid applying settings during initial control population
- initializes dynamic combo sources:
  - playback devices
  - drum samples from `Resources`
  - note paste behavior values
  - spectrogram enum/colormap options
- populates all controls from stored settings
- applies UI visibility toggles:
  - spectrogram options section show/hide
  - map path show/hide based on save location mode
- sets `doneInit = true` after initialization finishes

## 3. Named controls and roles

| Control name | Type | Role | Key handlers |
| --- | --- | --- | --- |
| `btnSave` | `Button` | Closes settings window | `BtnSave_Click` |
| `CheckAutosave` | `CheckBox` | Enable/disable autosave | `CheckAutosave_Click` |
| `CheckShowSpectrogram` | `CheckBox` | Toggle spectrogram feature globally | `CheckShowSpectrogram_Click` |
| `txtDefaultMapper` | `TextBox` | Default mapper name | `TxtDefaultMapper_LostFocus` |
| `txtDefaultNoteSpeed` | `TextBox` | Default note speed | `TxtDefaultNoteSpeed_LostFocus` |
| `txtDefaultGridSpacing` | `TextBox` | Default grid spacing | `TxtDefaultGridSpacing_LostFocus` |
| `comboNotePasteBehavior` | `ComboBox` | Controls note paste alignment mode | `ComboNotePasteBehavior_SelectionChanged` |
| `comboPlaybackDevice` | `ComboBox` | Select playback output device | `ComboPlaybackDevice_SelectionChanged` |
| `txtAudioLatency` | `TextBox` | Editor audio latency (ms) | `TxtAudioLatency_LostFocus` |
| `comboDrumSample` | `ComboBox` | Select note sample set | `ComboDrumSample_SelectionChanged` |
| `checkPanNotes` | `CheckBox` | Toggle note pan behavior | `checkPanNotes_Click` |
| `sliderSongVol` | `Slider` | Default song volume | `SliderSongVol_ValueChanged`, `sliderSongVol_MouseLeftButtonUp`, `sliderSongVol_DragCompleted` |
| `sliderDrumVol` | `Slider` | Default note volume | `SliderDrumVol_ValueChanged`, `sliderDrumVol_MouseLeftButtonUp`, `sliderDrumVol_DragCompleted` |
| `comboSpectrogramQuality` | `ComboBox` | Spectrogram quality mode | `ComboSpectrogramQuality_SelectionChanged` |
| `comboSpectrogramType` | `ComboBox` | Spectrogram frequency scale | `ComboSpectrogramType_SelectionChanged` |
| `txtSpectrogramFrequency` | `TextBox` | Spectrogram max frequency | `TxtSpectrogramFrequency_LostFocus`, `TxtSpectrogramFrequency_KeyDown` |
| `comboSpectrogramColormap` | `ComboBox` | Spectrogram color map | `ComboSpectrogramColormap_SelectionChanged` |
| `checkSpectrogramFlipped` | `CheckBox` | Flip spectrogram image | `checkSpectrogramFlipped_Click` |
| `checkSpectrogramChunking` | `CheckBox` | Enable spectrogram chunk rendering | `checkSpectrogramChunking_Click` |
| `checkSpectrogramCache` | `CheckBox` | Cache spectrogram images in map folder | `checkSpectrogramCache_Click` |
| `checkDiscord` | `CheckBox` | Toggle Discord rich presence | `CheckDiscord_Click` |
| `checkStartupUpdate` | `CheckBox` | Toggle startup update check | `CheckStartupUpdate_Click` |
| `comboMapSaveFolder` | `ComboBox` | Choose Documents vs Game Install save mode | `comboMapSaveFolder_SelectionChanged` |
| `txtMapSaveFolderPath` | `TextBlock` | Displays/opens game install path picker | `txtMapSaveFolderPath_MouseLeftButtonUp` |

## 4. Expected initial state

- controls load values from persisted settings immediately in constructor
- spectrogram options section is visible only when `CheckShowSpectrogram` is checked
- map path link is hidden in Documents mode and visible in Game Install mode
- playback devices list includes `Default` when available
- playback device combo disables if no devices are available
- settings are not written during initialization due `doneInit` guard

## 5. Supported user actions

### Keyboard shortcuts

| Shortcut | Scope | Effect |
| --- | --- | --- |
| `Enter` | `txtSpectrogramFrequency` focused | Commits frequency validation/update (same as lost-focus) |

No other explicit keyboard shortcuts are implemented in this window.

### Mouse interactions

| Interaction | Target | Result |
| --- | --- | --- |
| Click | checkboxes | Toggle boolean settings and apply immediately |
| Select | combo boxes | Change enum/device/path mode values and apply |
| Drag/release | volume sliders | Update displayed percent and persist volume defaults |
| Click | `txtMapSaveFolderPath` | Re-open game install folder picker when in Game Install mode |
| Click | `btnSave` | Close settings window |

### Drag and drop behavior

- No drag-and-drop handlers are implemented.

### Playback-related interactions

| Action | Trigger | Observable outcome |
| --- | --- | --- |
| Change output device | `comboPlaybackDevice` | Calls `caller.UpdatePlaybackDevice(...)`, reinitializes playback target |
| Change audio latency | `txtAudioLatency` | Stores latency and pauses song on caller |
| Change drum sample | `comboDrumSample` | Stores sample, pauses playback, restarts drummer |
| Toggle pan notes | `checkPanNotes` | Stores setting, pauses playback, restarts drummer |
| Change default volumes | `sliderSongVol` / `sliderDrumVol` | Persists defaults and reapplies caller settings |
| Spectrogram setting changes | spectrogram controls | Calls `caller.LoadSettingsFile(true)`, reloading spectrogram behavior |

## 6. Validation and state rules

### Input validation

| Field | Rule | Failure behavior |
| --- | --- | --- |
| `txtDefaultNoteSpeed` | numeric | error dialog; revert to previous |
| `txtDefaultGridSpacing` | numeric | error dialog; revert to previous |
| `txtAudioLatency` | numeric | error dialog; revert to previous |
| `txtSpectrogramFrequency` | integer in `Editor.Spectrogram.MinFreq..MaxFreq` | error dialog; revert to previous |

### Enable/disable and visibility rules

- spectrogram options UI toggles with `CheckShowSpectrogram`
- map save path link visibility toggles by `comboMapSaveFolder` mode
- playback device combo disables if zero available endpoints
- `doneInit` prevents update side effects while constructor is still populating controls

## 7. Persistence behavior

### Persistence mechanism

- most control handlers write a setting key and call `UpdateSettings()`
- `UpdateSettings()` performs:
  - `userSettings.Write()`
  - `caller.LoadSettingsFile(true)` to apply changes immediately in MainWindow

### Settings scope examples

- editor defaults: mapper, default note speed/grid spacing, paste behavior
- playback defaults: device, latency, drum sample, pan, song/note default volumes
- spectrogram: enabled/cache/type/quality/frequency/colormap/flipped/chunking
- misc: Discord RPC, update check, map save location mode/path, autosave

### Map save location specifics

- selecting Game Install opens folder picker
- canceling picker reverts selection to Documents and resets path default
- selected install path is persisted and custom-song subfolder is created if missing

## 8. Dialogs and file flows

### Dialogs

- error dialogs for invalid numeric inputs
- folder picker dialog for Ragnarock install path (`CommonOpenFileDialog`)

### File/folder side effects

- `PickGameFolder()` creates `Program.GameInstallRelativeMapFolder` under selected install path if needed
- drum sample list is discovered by scanning `Program.ResourcesPath` for matching files (`*1.wav`/`*1.mp3`)

## 9. Dependencies and background work

### Key dependencies

- `MainWindow` caller reference for immediate runtime updates
- shared `UserSettingsManager`
- `MMDevice` list from caller for audio endpoint selection
- `Spectrogram.Colormap` names for colormap options
- `CommonOpenFileDialog` for game install path selection

### Background/async behavior

- no dedicated background threads/timers in this window
- all changes are applied synchronously via `UpdateSettings()`

## 10. Timing-sensitive behavior

- most settings apply immediately and trigger `caller.LoadSettingsFile(true)`, which can trigger expensive redraw/reload paths in MainWindow
- `doneInit` is timing-sensitive for correctness; without it, initialization would fire selection handlers too early
- slider-driven updates can invoke frequent apply calls while dragging (especially note volume path)

## 11. Test mapping

### Candidate WPF UI baseline tests

- constructor populates controls from persisted settings without mutating values during init
- spectrogram section visibility toggles with `CheckShowSpectrogram`
- invalid numeric entries show error and revert values
- changing playback device calls caller update and persists ID
- changing drum sample/pan triggers caller pause + drummer restart
- map save location Game Install path picker:
  - cancel reverts to Documents
  - confirm persists path and shows clickable path text
- autosave/Discord/update toggles persist and reflect immediately in caller

### Candidate Avalonia parity tests

- same settings grouping and immediate-apply behavior
- same validation constraints and fallback behavior
- same visibility rules for spectrogram options and map path row
- same device-selection semantics including default-device option
- same map save location folder-picker flow

### Gaps/questions to verify in runtime pass

- UX impact of frequent `UpdateSettings()` calls while dragging volume sliders
- behavior when resources directory has no matching drum sample files
- behavior when playback devices change while settings window is open
