# BPMCalcWindow

- Status: Complete
- Analysis level: Documented baseline from code and implemented behavior
- Last updated: 2026-03-24

## Source files

- `src/Edda.Wpf/Windows/BPMCalcWindow.xaml`
- `src/Edda.Wpf/Windows/BPMCalcWindow.xaml.cs`
- `src/Edda.Wpf/Windows/MainWindow.MenuItems.cs` (entry point)

## 1. Purpose

Small tap-tempo utility to estimate BPM from repeated key presses.

Displays:

- number of timing inputs captured
- rounded average BPM
- unrounded average BPM (2 decimal places)

## 2. Entry points and startup flow

### How the window is opened

- opened from MainWindow Tools -> BPM Finder (`MenuItemBpmFinder_Click`)
- uses MainWindow `ShowUniqueWindow(() => new BPMCalcWindow())`
- single-instance behavior while open

### Constructor setup

- initializes `Stopwatch`
- initializes `List<long> intervalSamples`
- UI starts at `0` inputs / `0 BPM`

## 3. Named controls and roles

| Control name | Type | Role | Key handlers |
| --- | --- | --- | --- |
| `lblInputCounter` | `Label` | Shows input count | updated in `Window_KeyDown`, reset in `BtnReset_Click` |
| `lblAvgBPM` | `Label` | Shows rounded average BPM | updated in `CalculateBPM` |
| `lblUnroundedAvgBPM` | `Label` | Shows precise BPM in parentheses | updated in `CalculateBPM` |
| `btnReset` | `Button` | Clears all captured samples and restarts session | `BtnReset_Click` |

Window-level input:

- `KeyDown="Window_KeyDown"` captures tap input globally in the window

## 4. Expected initial state

- stopwatch not running
- no interval samples
- counter label = `0`
- avg label = `0`
- unrounded avg label = `(0.00)`

## 5. Supported user actions

### Keyboard shortcuts

- no dedicated shortcut map; any key press in window contributes to tap sequence

### Mouse interactions

| Interaction | Target | Result |
| --- | --- | --- |
| click `Reset` | `btnReset` | clears samples, counters, and display values; resets stopwatch |

### Drag and drop behavior

- No drag-and-drop behavior is implemented.

### Playback-related interactions

- none (no audio playback in this window)

## 6. Validation and state rules

### Input handling rules

- first key press starts stopwatch only (does not increment input counter)
- each subsequent key press:
  - adds elapsed interval sample (`now - prevTime`)
  - increments input counter
  - updates displayed BPM values
- BPM is computed from sorted sample list mean interval (`60000 / avgInterval`)

### Notes/quirks

- all keys are accepted (including modifiers/navigation keys if pressed while focused)
- repeated keydown events from key repeat can influence results
- sorting samples before averaging is unnecessary for mean but currently harmless

## 7. Persistence behavior

- no settings are persisted
- state is in-memory only for current window lifetime

## 8. Dialogs and file flows

- no dialogs
- no file I/O/import/export

## 9. Dependencies and background work

### Key dependencies

- `System.Diagnostics.Stopwatch`
- in-memory `List<long>` for interval capture

### Background/async behavior

- none; all logic is synchronous on UI thread

## 10. Timing-sensitive behavior

- accuracy depends on key event timing and UI/input latency
- first sample uses time between first and second key presses
- very short or inconsistent tapping intervals can produce unstable BPM output

## 11. Test mapping

### Candidate WPF UI baseline tests

- initial UI state values are correct on open
- first key press starts capture but does not increment `lblInputCounter`
- second and later key presses increment counter and update BPM labels
- reset returns UI and internal counters/samples to defaults
- BPM output format stays consistent (`0.` rounding for main label, `0.00` in parentheses)

### Candidate Avalonia parity tests

- same any-key tap capture behavior
- same first-tap/no-count rule
- same mean-based BPM formula and formatting
- same reset semantics

### Gaps/questions to verify in runtime pass

- whether excluding modifier keys would improve UX consistency
- whether key repeat should be ignored for cleaner tapping input
- practical accuracy/precision on different hardware/keyboards
