# SongPreviewWindow

- Status: Complete
- Analysis level: Documented baseline from code and implemented behavior
- Last updated: 2026-03-24

## Source files

- `src/Edda.Wpf/Windows/SongPreviewWindow.xaml`
- `src/Edda.Wpf/Windows/SongPreviewWindow.xaml.cs`
- `src/Edda.Wpf/Windows/MainWindow.UIControls.cs` (entry point and lifecycle hooks)

## 1. Purpose

Utility window for generating `preview.ogg` from the current song file using selectable:

- preview start time
- preview end time
- fade-in duration
- fade-out duration

## 2. Entry points and startup flow

### How the window is opened

- opened from MainWindow via `BtnMakePreview_Click`
- created as single-instance window (`Helper.GetFirstWindow<SongPreviewWindow>()`)
- owner set to MainWindow and shown non-modally

### Startup flow and defaults

Constructor inputs: `songFolder`, `songURL`, suggested `startMin/startSec`.

On open:

- reads full song duration from `VorbisWaveReader(songURL)`
- stores song end minute/second bounds
- initializes `startMin/startSec` from caller (typically current playback position)
- initializes `endMin/endSec` to `start + Audio.MaxPreviewLength`
- sets fade defaults from `Audio.DefaultPreviewFadeIn/Audio.DefaultPreviewFadeOut`
- writes initial values into text fields via `UpdateTextFields()`

### Interaction with MainWindow preview playback

- when this window opens, MainWindow unloads preview playback (`songPreviewController?.UnloadPreview()`)
- when this window closes, MainWindow restarts preview subsystem (`songPreviewController?.Restart(...)`)

## 3. Named controls and roles

| Control name | Type | Role | Key handlers |
| --- | --- | --- | --- |
| `btnGenerate` | `Button` | Generates preview file | `BtnGenerate_Click` |
| `TxtStartTimeMin` | `TextBox` | Start minute | `TxtStartTimeMin_GotFocus`, `TxtStartTimeMin_LostFocus` |
| `TxtStartTimeSec` | `TextBox` | Start second | `TxtStartTimeSec_GotFocus`, `TxtStartTimeSec_LostFocus` |
| `TxtEndTimeMin` | `TextBox` | End minute | `TxtEndTimeMin_GotFocus`, `TxtEndTimeMin_LostFocus` |
| `TxtEndTimeSec` | `TextBox` | End second | `TxtEndTimeSec_GotFocus`, `TxtEndTimeSec_LostFocus` |
| `TxtFadeInDuration` | `TextBox` | Fade-in duration (sec) | `TxtFadeInDuration_GotFocus`, `TxtFadeInDuration_LostFocus` |
| `TxtFadeOutDuration` | `TextBox` | Fade-out duration (sec) | `TxtFadeOutDuration_GotFocus`, `TxtFadeOutDuration_LostFocus` |

## 4. Expected initial state

- fields are pre-filled with constructor-derived defaults
- focus on any time/duration textbox selects all text (`GotFocus -> SelectAll()`)
- generate button is enabled
- no preview file is written until `Create Preview` is clicked

## 5. Supported user actions

### Keyboard shortcuts

- No explicit keyboard shortcuts are implemented.

### Mouse interactions

| Interaction | Target | Result |
| --- | --- | --- |
| Focus textbox | any time/duration field | auto-selects full value |
| Leave textbox | any time/duration field | validates/clamps and rewrites canonical value |
| Click `Create Preview` | `btnGenerate` | runs ffmpeg preview generation flow |

### Drag and drop behavior

- No drag-and-drop handlers are implemented.

### Playback-related interactions

- This window does not directly play audio.
- It produces the preview asset consumed by MainWindow preview playback (`preview.ogg`).

## 6. Validation and state rules

### Input validation

| Field group | Rule | Failure behavior |
| --- | --- | --- |
| fade in/out | non-negative integer; clamped to total song seconds | error dialog on invalid parse; keep previous value |
| start/end fields | integer expected; intended range 0..59 | error dialog for non-integers; value rewritten |
| start/end ordering | start must be <= end | if invalid after editing start, end is forced to start; if invalid after editing end, start is forced to end |
| preview duration | if longer than `Audio.MaxPreviewLength` | warning dialog asks user whether to continue |

### Current implementation quirks to preserve/verify

- range check in start/end lost-focus handlers uses current state (`startMin`, `startSec`, etc.) rather than parsed `temp`, so bounds enforcement can be weaker than intended.
- default end time (`start + max preview length`) is not clamped at constructor time to song length.

## 7. Persistence behavior

- no user setting persistence in this window
- writes output file directly to map folder:
  - `Path.Combine(songFolder, BeatmapDefaults.PreviewFilename)`
- generation action disables `btnGenerate` during ffmpeg run and re-enables afterward

## 8. Dialogs and file flows

### Dialogs

- invalid input error dialogs
- duration warning confirmation dialog (`Continue anyway?`)
- success dialog when preview creation returns exit code `0`
- error dialog when ffmpeg returns non-zero exit code

### File flow

- input: existing map song file (`songURL`)
- output: preview file in map folder (`preview.ogg`)
- conversion path uses `Helper.FFmpeg(...)` with `-ss/-to` trim and dual `afade` filters

## 9. Dependencies and background work

### Key dependencies

- `NAudio.Vorbis.VorbisWaveReader` for reading total song duration
- `Helper.FFmpeg(...)` wrapper for preview encoding
- `Audio` constants (`MaxPreviewLength`, default fade durations)
- `BeatmapDefaults.PreviewFilename` output naming

### Background/async behavior

- ffmpeg process is invoked synchronously from button handler
- UI feedback for long operation is limited to temporary button disablement

## 10. Timing-sensitive behavior

- preview trim/fade timing is computed from minute/second fields and passed directly into ffmpeg command args
- fade values near/above preview length can materially change output (especially fade-out start calculation)
- manual runtime verification needed for edge cases:
  - start/end near song boundaries
  - long fades relative to clip length
  - duration-over-limit override path

## 11. Test mapping

### Candidate WPF UI baseline tests

- constructor seeds fields correctly from provided start timestamp
- focus selection behavior for all text fields
- invalid fade values show error and revert
- start/end order auto-correction behavior after invalid range edits
- long duration prompt appears and gates generation
- generation success/failure dialogs map to ffmpeg exit code
- button disable/re-enable wraps generation call

### Candidate Avalonia parity tests

- same field model and focus/commit behavior
- same validation + warning workflow
- same output path and file naming (`preview.ogg`)
- same duration override confirmation behavior

### Gaps/questions to verify in runtime pass

- actual ffmpeg fade timing semantics relative to trimmed clip
- behavior when `songURL` is missing/unreadable at runtime
- whether start/end range enforcement should stay as-is or be fixed behind explicit migration notes
