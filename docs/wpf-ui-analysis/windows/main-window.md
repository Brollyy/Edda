# MainWindow

- Status: Complete
- Analysis level: Documented baseline from code and implemented behavior
- Last updated: 2026-03-24

## Source files

- `src/Edda.Wpf/Windows/MainWindow.xaml`
- `src/Edda.Wpf/Windows/MainWindow.xaml.cs`
- `src/Edda.Wpf/Windows/MainWindow.UIControls.cs`
- `src/Edda.Wpf/Windows/MainWindow.MenuItems.cs`
- `src/Edda.Wpf/Windows/MainWindow.GridControls.cs`
- `src/Edda.Wpf/Classes/Adapters/Editor/EditorGridController.cs`

## 1. Purpose

Primary editor screen for full map authoring flow:

- map metadata editing (song, artist, mapper, environment, explicit flag)
- audio assignment and playback control
- note/bookmark/BPM-change editing on timeline grid
- difficulty management and stats inspection
- import/save/export and utility windows (timing editor, nav customization, predictor)

## 2. Entry points and startup flow

### How the window is opened

- App startup lands on `StartWindow` (`src/Edda.Wpf/App.xaml`), then `StartWindow` opens `MainWindow` and immediately calls one of:
  - `InitNewMap(newMapFolder)`
  - `InitImportMap(importMapFolder)`
  - `InitOpenMap(mapFolder)`
- Inside `MainWindow`, File menu / Ctrl shortcuts can re-run create/import/open flows.
- `Close Map` closes `MainWindow` and returns to `StartWindow` (`returnToStartMenuOnClose = true`).

### Constructor and baseline state

`MainWindow()`:

- initializes UI
- disables map-dependent controls (`DisableUI()`)
- configures autosave timer
- creates `EditorGridController` and `SongPreviewController`
- loads and validates settings (`InitSettings()` + `LoadSettingsFile()`)
- registers audio device change listener
- initializes drummer/metronome
- sets nav preview line styling and environment list
- wires debounce handlers for expensive resize redraw operations

### Map initialization paths

- `InitNewMap`
  - optional cleanup of previous map state
  - prompts for `.ogg`
  - creates new `MapEditor`
  - loads song and metadata-derived defaults
  - updates recent maps and Discord presence
- `InitImportMap`
  - prompts for `.sm`/`.ssc`
  - converts to Ragnarock format
  - saves imported map then opens it via `InitOpenMap`
- `InitOpenMap`
  - creates `MapEditor`
  - initializes grid controller, song, preview, cover image, and full UI
  - updates recent maps and Discord presence
  - performs delayed redraw thread (memory-use workaround)

## 3. Named controls and roles

High-impact controls grouped by region.

| Control name | Type | Role | Key handlers |
| --- | --- | --- | --- |
| `MenuItemNewMap` | `MenuItem` | Start new map workflow | `MenuItemNewMap_Click` |
| `MenuItemOpenMap` | `MenuItem` | Open existing map folder | `MenuItemOpenMap_Click` |
| `MenuItemImportMap` | `MenuItem` | Import StepMania map | `MenuItemImportMap_Click` |
| `MenuItemSaveMap` | `MenuItem` | Backup + save current map | `MenuItemSaveMap_Click` |
| `MenuItemExportMap` | `MenuItem` | Export map as zip package | `MenuItemExportMap_Click` |
| `MenuItemCloseMap` | `MenuItem` | Close editor and return to start screen | `MenuItemCloseMap_Click` |
| `btnSongPlayer` | `Button` | Play/pause main song playback | `BtnSongPlayer_Click` |
| `sliderSongProgress` | `Slider` | Seek/playback position and sync anchor for grid/nav | `SliderSongProgress_ValueChanged` |
| `txtSongName` | `TextBox` | Song title in map metadata | `TxtSongName_TextChanged`, `TxtSongName_LostFocus` |
| `txtArtistName` | `TextBox` | Song artist metadata | `TxtArtistName_TextChanged`, `TxtArtistName_LostFocus` |
| `txtMapperName` | `TextBox` | Level author metadata | `TxtMapperName_TextChanged`, `TxtMapperName_LostFocus` |
| `txtSongBPM` | `TextBox` | Global BPM editing and optional retime trigger | `TxtSongBPM_LostFocus`, `TxtSongBPM_KeyDown` |
| `comboEnvironment` | `ComboBox` | Environment metadata selection | `ComboEnvironment_SelectionChanged` |
| `btnPickSong` | `Button` | Replace song file | `BtnPickSong_Click` |
| `btnMakePreview` | `Button` | Open preview generation dialog | `BtnMakePreview_Click` |
| `btnPlayPreview` | `Button` | Toggle preview playback | `BtnPlayPreview_Click` |
| `btnPickCover` | `Button` | Replace/assign cover image | `BtnPickCover_Click` |
| `btnChangeDifficulty0..2` | `Button` | Switch active difficulty | `BtnChangeDifficulty*_Click` |
| `btnAddDifficulty` | `Button` | Add difficulty (optionally copy markers) | `BtnAddDifficulty_Click` |
| `btnDeleteDifficulty` | `Button` | Delete active difficulty | `BtnDeleteDifficulty_Click` |
| `txtDifficultyNumber` | `TextBox` | Difficulty rank value | `TxtDifficultyNumber_LostFocus`, `TxtDifficultyNumber_KeyDown` |
| `txtNoteSpeed` | `TextBox` | Note speed value | `TxtNoteSpeed_LostFocus` |
| `txtDistMedal0..2` | `TextBox` | Medal distance settings | `TxtDistMedal*_GotFocus`, `TxtDistMedal*_LostFocus` |
| `btnChangeBPM` | `Button` | Open timing change editor window | `BtnChangeBPM_Click` |
| `btnCustomizeNavBar` | `Button` | Open nav-bar customization window | `BtnCustomizeNavBar_Click` |
| `sliderSongVol` | `Slider` | Song playback volume | `SliderSongVol_ValueChanged` |
| `sliderDrumVol` | `Slider` | Note hit sample volume | `SliderDrumVol_ValueChanged` |
| `sliderSongTempo` | `Slider` | Playback tempo multiplier | `sliderSongTempo_ValueChanged`, `sliderSongTempo_MouseDoubleClick` |
| `checkMetronome` | `CheckBox` | Enable metronome playback | `CheckMetronome_Click` |
| `checkGridSnap` | `CheckBox` | Toggle snap-to-grid behavior | `CheckGridSnap_Click` |
| `txtGridDivision` | `TextBox` | Grid beat subdivision | `TxtGridDivision_LostFocus`, `TxtGridDivision_KeyDown` |
| `txtGridSpacing` | `TextBox` | Grid vertical spacing | `TxtGridSpacing_LostFocus`, `TxtGridSpacing_KeyDown` |
| `checkWaveform` | `CheckBox` | Toggle main waveform overlay | `CheckWaveform_Click` |
| `borderNavWaveform` | `Border` | Right-side navigation waveform and marker lane | `BorderNavWaveform_*` mouse handlers |
| `scrollEditor` | `ScrollViewer` | Main editing viewport and mouse input hub | `ScrollEditor_*`, `scrollEditor_*` handlers |
| `gridSpectrogram` / `scrollSpectrogram` | `Grid` / `ScrollViewer` | Spectrogram panel with synced scroll | `ScrollSpectrogram_*` |

## 4. Expected initial state

### Before map is loaded

- map editor controls are disabled via `DisableUI()`.
- playback controls and editing surface are disabled.
- menu bar remains available.
- settings are still loaded and applied (spectrogram, nav element visibility, audio prefs).

### After map is loaded (`InitUI` path)

- map metadata fields populated from `info.dat`.
- song file loaded, duration/progress controls initialized.
- cover image loaded (or cleared if missing).
- active difficulty set to index `0` by default.
- difficulty buttons reflect current count (1-3).
- grid spacing/division and snap behavior initialized.
- waveform/spectrogram/nav overlays drawn.
- stats and optional difficulty prediction are visible according to settings/feature support.

## 5. Supported user actions

### Keyboard shortcuts

| Shortcut | Scope | Effect |
| --- | --- | --- |
| `Ctrl+N` | Global | New map flow |
| `Ctrl+O` | Global | Open map flow |
| `Ctrl+S` | Global | Backup and save |
| `Ctrl+I` | Global | Import map flow |
| `Ctrl+E` | Global | Export map |
| `Ctrl+W` | Global | Close map and return to start window |
| `Ctrl+[` | Global | Toggle left sidebar |
| `Ctrl+]` | Global | Toggle right sidebar |
| `Ctrl+A` | Editor-ready state | Select all notes |
| `Ctrl+C` | Editor-ready state | Copy selected notes |
| `Ctrl+X` | Editor-ready state | Cut selected notes |
| `Ctrl+V` | Editor-ready state | Paste at beat with column offset |
| `Ctrl+Shift+V` | Editor-ready state | Paste on mouse column |
| `Ctrl+Z` | Editor-ready state | Undo |
| `Ctrl+Y` or `Ctrl+Shift+Z` | Editor-ready state | Redo |
| `Ctrl+M` | Editor-ready state | Mirror selection |
| `Ctrl+B` | Editor-ready state | Add bookmark |
| `Ctrl+T` | Editor-ready state | Add timing change |
| `Ctrl+Shift+T` | Editor-ready state | Add grid-snapped timing change |
| `Ctrl+Q` | Editor-ready state | Quantize selection |
| `Ctrl+G` | Editor-ready state | Toggle snap-to-grid |
| `1` / `2` / `3` / `4` | Grid interaction state | Add note in lane 1-4 and play drum sample |
| `Delete` | Editor-ready state | Remove selected notes |
| `Escape` | Editor-ready state | Unselect all notes |
| `Shift+Up` / `Shift+Down` | Editor-ready state | Move selection by beat step |
| `Ctrl+Up` / `Ctrl+Down` | Editor-ready state | Move selection by grid step |
| `Shift+Left/Right` or `Ctrl+Left/Right` | Editor-ready state | Move selection by column |
| `Space` | Non-textbox focus | Toggle play/pause |
| `Ctrl+MouseWheel` | Grid/nav hover state | Adjust grid division (global or local timing-change segment) |

### Mouse interactions

| Interaction | Target | Result |
| --- | --- | --- |
| Click/drag | `sliderSongProgress` | Seek song and synchronize grid/nav position |
| Move/click-drag | `borderNavWaveform` | Preview time/beat and scrub timeline |
| Left click | `scrollEditor`/grid | Select existing note or place new note |
| Left-drag | `scrollEditor`/grid | Box-select notes |
| Right click | `scrollEditor`/grid | Remove hovered note |
| Drag marker | grid bookmark/BPM marker | Reposition bookmark or BPM change |
| Middle-click hold + move | `scrollEditor` | Hold-scroll mode with visual indicator |
| Double click | `sliderSongTempo` | Reset song tempo to default |
| Click | `btnAddDifficulty` | Add difficulty (with copy prompt) |
| Click | `btnDeleteDifficulty` | Delete difficulty (with confirmation) |
| Click | `btnPickSong` / `btnPickCover` | Open file picker and replace assets |

### Drag and drop behavior

- No OS-level drag-and-drop handlers are implemented for files/controls.
- Internal drag behavior exists for:
  - note box selection on editor grid
  - bookmark/BPM marker repositioning
  - nav waveform scrubbing by click-drag

### Playback-related interactions

| Action | Trigger | Observable outcome |
| --- | --- | --- |
| Main song play/pause | `btnSongPlayer`, `Space` | Progress slider animates; note/beat scanning starts/stops |
| Preview playback toggle | `btnPlayPreview` | Preview clip starts/stops independently of main song |
| Preview generation | `btnMakePreview` | Opens `SongPreviewWindow` seeded with current song position |
| Tempo change | `sliderSongTempo` | Audio playback tempo, note scanner tempo, beat scanner tempo update |
| Song volume change | `sliderSongVol` | Song channel volume changes; text updates as `%` |
| Drum volume change | `sliderDrumVol` | Drummer volume changes; text updates as `%` |
| Metronome toggle | `checkMetronome` | Beat tick output enabled/disabled |

## 6. Validation and state rules

### Input validation

| Field | Rule | Failure behavior |
| --- | --- | --- |
| `txtSongBPM` | positive number | error dialog; value reverts to previous |
| `txtSongOffset` | numeric | error dialog; value reverts to previous |
| `txtDifficultyNumber` | integer in configured min/max range | error dialog; value reverts to previous |
| `txtNoteSpeed` | positive number | error dialog; value reverts to previous |
| `txtDistMedal0..2` | non-negative integer or empty/Auto (=0) | error dialog; value reverts to previous |
| `txtGridSpacing` | numeric | error dialog; value reverts to previous |
| `txtGridDivision` | integer 1..`Editor.GridDivisionMax` | error dialog; value reverts to previous |
| Song file load | valid `.ogg`, duration < 1 hour | error dialog and abort |
| Cover image load | file must exist; non-square prompts optional crop | warning/error dialogs and safe fallback |

### Enable/disable and visibility rules

- `DisableUI()` is applied before map load; `EnableUI()` after successful map initialization.
- During active playback, controls that affect timeline consistency are disabled (BPM edits, difficulty switching, grid editing input, progress slider, nav bar interaction, tempo slider, nav customization).
- Difficulty switch buttons are visible only up to `numDifficulties`; add/remove buttons are constrained (`max 3`, `min 1`).
- `difficultyPrediction` label visibility depends on user setting and predictor feature support.
- `MenuItemClearCache` visibility depends on both spectrogram enabled and spectrogram cache enabled.
- Nav overlays (`waveform`, `bookmarks`, `BPM changes`, `notes`) are shown/hidden by persisted user settings.

## 7. Persistence behavior

### Map data persistence

- map metadata fields write directly via `mapEditor.SetMapValue(...)` as user edits.
- map difficulty values and custom editor values (grid spacing/division) persist via `SetMapValue` / map editor APIs.
- explicit save uses `SaveBeatmap()`; normal Save command uses `BackupAndSaveBeatmap()`.
- autosave timer periodically calls `SaveBeatmap()` when enabled.

### Backup behavior

- save backup folder: `<map>/Backups` (via `Program.BackupPath`).
- backup naming includes timestamp.
- capped backup count (`Program.MaxBackups`) with oldest removal.

### Settings persistence and side effects

- `ValidateSettingsFile()` backfills missing/invalid settings.
- `LoadSettingsFile()` applies settings to runtime behavior (spectrogram options, predictors, audio latency/device, Discord RPC, autosave, nav overlays).
- settings windows opened from `MainWindow` reuse shared `UserSettingsManager` instance.

### Recents and presence

- recent map entries update on open/new/import and song-name text edits.
- Discord rich presence updates after map load and when map stats/song name change.

## 8. Dialogs and file flows

### File and folder pickers

- New map folder chooser (`Helper.ChooseNewMapFolder`).
- Open map folder chooser (`Helper.ChooseOpenMapFolder`).
- Import simfile picker (`.sm`, `.ssc`).
- Song picker (`.ogg`).
- Cover picker (`.jpg`, `.jpeg`, `.jfif`).
- Export destination folder picker (`CommonOpenFileDialog`).

### Confirmations and warnings

- unsaved changes prompt on close/switch operations
- optional note/marker retiming when BPM changes
- add difficulty prompt (copy bookmarks/BPM changes)
- delete difficulty confirmation (destructive)
- clear spectrogram cache confirmation
- non-square cover image crop prompt
- update-check informational/failure dialogs

### Import/export behavior

- import converts StepMania chart(s) into Ragnarock map format before opening.
- export writes `info.dat`, selected difficulty files, song file, cover file, and optional `preview.ogg` into a zip package.

### Utility windows opened from MainWindow

- `BPMCalcWindow`
- `DifficultyPredictorWindow`
- `ChangeBPMWindow`
- `CustomizeNavBarWindow`
- `SettingsWindow`
- `AboutWindow`

All use single-instance behavior via `ShowUniqueWindow` (or explicit `GetFirstWindow` checks).

## 9. Dependencies and background work

### Key dependencies

- `MapEditor` for map state mutations and serialization
- `EditorGridController` for waveform/grid rendering and grid interactions
- `SongPreviewController` for preview clip workflow
- `UserSettingsManager` for persisted user settings
- `RecentOpenedFolders` for recents
- `DiscordClient` for rich presence
- NAudio stack (`WasapiOut`, `VorbisWaveReader`, `SampleChannel`, `SoundTouchWaveStream`) for audio playback
- `ParallelAudioPlayer` for drummer/metronome

### Background/async behavior

- autosave timer (`System.Timers.Timer`)
- resize redraw debounce streams via Rx `Throttle`
- delayed redraw thread in `InitOpenMap` (500 ms wait)
- delayed audio start using `Task.Delay` when latency compensation requires it
- deferred `editorIsLoaded` activation using `Dispatcher.BeginInvoke(...ContextIdle)`

## 10. Timing-sensitive behavior

Known timing-sensitive or synchronization-sensitive points:

- playback progress uses `DoubleAnimation`; inline note states this can introduce about `0.1s` desync.
- note and beat scanners start at `slider position - editorAudioLatency`.
- when latency offset is larger than current time, playback start is deferred asynchronously.
- grid, nav waveform, spectrogram, and song progress are cross-synchronized (multiple event paths update each other).
- heavy redraw paths are debounce-throttled to avoid resize storms.
- one delayed redraw is intentionally performed after open to reduce WPF memory pressure.

## 11. Test mapping

### Candidate WPF UI baseline tests

- map load initializes controls and enables disabled-at-start controls.
- keyboard shortcut routing (global + editor-specific) including focus guards.
- play/pause disables/enables editor controls correctly.
- BPM edit prompt branches:
  - cancel keeps old BPM
  - no updates BPM only
  - yes retimes notes/markers
- difficulty add/delete/switch updates button visibility and active-state styling.
- grid snap toggle stays in sync between checkbox and menu item.
- nav waveform mouse scrub updates selected time/beat display and song position.
- invalid numeric inputs show dialogs and restore previous values.
- export creates zip with expected files for current difficulty count.

### Candidate Avalonia parity tests

- same shortcuts and focus rules.
- same enable/disable policy during playback.
- same validation and revert behavior for all numeric fields.
- same difficulty lifecycle constraints and UI states.
- same import/export file inclusion logic.
- same settings-driven visibility rules (spectrogram/nav overlays/prediction label).

### Gaps/questions to verify in runtime pass

- measured real desync characteristics under tempo changes and non-zero audio latency.
- behavior when no active playback device is available at runtime.
- whether any editor shortcuts should be blocked when map is not yet initialized.
