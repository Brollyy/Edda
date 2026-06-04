# CustomizeNavBarWindow

- Status: Complete
- Analysis level: Documented baseline from code and implemented behavior
- Last updated: 2026-03-24

## Source files

- `src/Edda.Wpf/Windows/CustomizeNavBarWindow.xaml`
- `src/Edda.Wpf/Windows/CustomizeNavBarWindow.xaml.cs`
- `src/Edda.Wpf/Windows/MainWindow.UIControls.cs` (entry point)

## 1. Purpose

UI for configuring navigation bar visuals in MainWindow, including:

- element visibility toggles
- per-element colors (primary and secondary where applicable)
- shadow opacity for bookmarks and BPM-change markers
- quick reset to default visual presets

## 2. Entry points and startup flow

### How the window is opened

- opened from MainWindow via `BtnCustomizeNavBar_Click`
- uses single-instance window check (`Helper.GetFirstWindow<CustomizeNavBarWindow>()`)
- shown owned by MainWindow (`Owner = this`), non-modal

### Constructor initialization flow

- receives `MainWindow caller` and shared `UserSettingsManager`
- loads checkbox states and color/opacity settings from user settings
- wires debounced `ColorChanged` streams for each color picker (`Throttle(Editor.DrawDebounceInterval)`)
- enables/disables dependent controls based on toggle states
- sets `doneInit = true` only after setup to avoid writing settings during initial value assignment

## 3. Named controls and roles

| Control name | Type | Role | Key handlers |
| --- | --- | --- | --- |
| `btnSave` | `Button` | Closes window | `BtnSave_Click` |
| `CheckWaveform` | `CheckBox` | Show/hide nav waveform | `CheckWaveform_Click` |
| `ColorWaveform` | `PortableColorPicker` | Waveform color | debounced `ColorWaveform_ColorChanged` |
| `ButtonResetWaveform` | `Button` | Reset waveform color default | `ButtonResetWaveform_Click` |
| `CheckBookmark` | `CheckBox` | Show/hide bookmark markers | `CheckBookmark_Click` |
| `ColorBookmark` | `PortableColorPicker` | Bookmark marker and label colors | debounced `ColorBookmark_ColorChanged` |
| `SliderBookmarkShadowOpacity` | `Slider` | Bookmark label background shadow opacity | `SliderBookmarkShadowOpacity_ValueChanged`, `SliderBookmarkShadowOpacity_MouseDoubleClick` |
| `ButtonResetBookmark` | `Button` | Reset bookmark colors/shadow defaults | `ButtonResetBookmark_Click` |
| `CheckBPMChange` | `CheckBox` | Show/hide BPM change markers | `CheckBPMChange_Click` |
| `ColorBPMChange` | `PortableColorPicker` | BPM change marker and label colors | debounced `ColorBPMChange_ColorChanged` |
| `SliderBPMChangeShadowOpacity` | `Slider` | BPM change label shadow opacity | `SliderBPMChangeShadowOpacity_ValueChanged`, `SliderBPMChangeShadowOpacity_MouseDoubleClick` |
| `ButtonResetBPMChange` | `Button` | Reset BPM change colors/shadow defaults | `ButtonResetBPMChange_Click` |
| `CheckNote` | `CheckBox` | Show/hide nav notes | `CheckNote_Click` |
| `ColorNote` | `PortableColorPicker` | Nav note + selected-note colors | debounced `ColorNote_ColorChanged` |
| `ButtonResetNote` | `Button` | Reset nav note colors defaults | `ButtonResetNote_Click` |

## 4. Expected initial state

- all controls are pre-populated from user settings (or editor defaults fallback)
- each color picker/slider enable state depends on its visibility checkbox
- no immediate writes occur during initialization due `doneInit` guard
- window is fixed-size and centered on owner

## 5. Supported user actions

### Keyboard shortcuts

- No explicit keyboard shortcuts are implemented.

### Mouse interactions

| Interaction | Target | Result |
| --- | --- | --- |
| Click | visibility checkboxes | Persist enable state and redraw affected nav layer |
| Change color | color pickers | Persist color setting(s) and redraw affected nav layer (debounced) |
| Drag slider | shadow sliders | Persist opacity and redraw relevant labels |
| Double click slider | shadow sliders | Reset opacity to editor default constant |
| Click reset | reset buttons | Restore defaults for section values |
| Click `OK` | `btnSave` | Close window |

### Drag and drop behavior

- No drag-and-drop handlers are implemented.

### Playback-related interactions

- No direct playback controls.
- Changes affect visual overlays rendered during normal editor playback/editing.

## 6. Validation and state rules

### Input validation

- No free-form text inputs.
- Slider bounds enforce `0..1` opacity range.
- Color pickers constrain values to valid color types.

### Enable/disable and visibility rules

- waveform color picker enabled only if waveform toggle is on
- bookmark color picker + bookmark shadow slider enabled only if bookmarks toggle is on
- BPM-change color picker + BPM-change shadow slider enabled only if BPM changes toggle is on
- note color picker enabled only if note toggle is on

## 7. Persistence behavior

### Persistence mechanism

- all interactions persist through `userSettings.SetValueForKey(...)`
- `UpdateSettings()` writes file and calls `caller.LoadSettingsFile()`
- specialized update paths redraw only affected layer(s):
  - waveform -> `DrawNavWaveform()`
  - bookmarks -> `DrawNavBookmarks()`
  - BPM changes -> `DrawNavBPMChanges()`
  - notes -> clear + `DrawNavNotes()` + `HighlightNavNotes()`

### Keys affected

- visibility: `EnableNavWaveform`, `EnableNavBookmarks`, `EnableNavBPMChanges`, `EnableNavNotes`
- colors: `NavWaveformColor`, `NavBookmarkColor`, `NavBookmarkNameColor`, `NavBPMChangeColor`, `NavBPMChangeLabelColor`, `NavNoteColor`, `NavSelectedNoteColor`
- shadow: `NavBookmarkShadowOpacity`, `NavBPMChangeShadowOpacity`

## 8. Dialogs and file flows

- No dialogs are shown in this window.
- No file import/export actions occur here.

## 9. Dependencies and background work

### Key dependencies

- `MainWindow` caller (for immediate redraw + settings reload)
- shared `UserSettingsManager`
- `Editor` constants for default visual values
- `ColorPicker.PortableColorPicker` component

### Background/async behavior

- color change handlers are debounced via Rx (`Observable.FromEventPattern(...).Throttle(...)`)
- callbacks marshal to UI thread with `ObserveOn(SynchronizationContext.Current)` + `Dispatcher.Invoke`

## 10. Timing-sensitive behavior

- debounce interval intentionally reduces expensive redraw churn during rapid color dragging
- settings and redraw order is important: write/apply first, then redraw target layer
- reset buttons may trigger both direct setter effects and color-changed events, so redraw may occur more than once for a single user action

## 11. Test mapping

### Candidate WPF UI baseline tests

- constructor loads saved states and does not mutate settings during init
- each visibility toggle updates user settings and target layer enable state
- each color picker updates corresponding setting key(s) after debounce
- each shadow slider updates opacity settings; double click resets to defaults
- reset buttons restore default values and are reflected in nav rendering
- note update path keeps selected note highlights after redraw

### Candidate Avalonia parity tests

- same section grouping (waveform/bookmark/BPM-change/note)
- same per-section enable/disable semantics
- same key mapping for persisted settings
- same default-reset behavior and slider bounds
- same debounced color update strategy (or equivalent UX behavior)

### Gaps/questions to verify in runtime pass

- whether reset actions produce redundant redraw passes noticeable on low-end systems
- if throttled color changes ever miss final selected value under fast interaction
- visual parity of color picker primary/secondary mapping in future Avalonia implementation
