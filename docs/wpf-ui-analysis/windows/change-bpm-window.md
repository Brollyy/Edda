# ChangeBPMWindow

- Status: Complete
- Analysis level: Documented baseline from code and implemented behavior
- Last updated: 2026-03-24

## Source files

- `src/Edda.Wpf/Windows/ChangeBPMWindow.xaml`
- `src/Edda.Wpf/Windows/ChangeBPMWindow.xaml.cs`
- `src/Edda.Wpf/Windows/MainWindow.UIControls.cs` (entry point)
- `src/Edda.App/Classes/MapEditor/MapEditor.cs` (BPM-change propagation hooks)

## 1. Purpose

Timing editor window for per-difficulty BPM change markers.

It allows users to:

- view current BPM change list
- edit change beat, BPM, and grid division values
- add new BPM changes at current playback position
- delete selected BPM changes

## 2. Entry points and startup flow

### How the window is opened

- opened from MainWindow via `BtnChangeBPM_Click`
- single-instance behavior via `Helper.GetFirstWindow<ChangeBPMWindow>()`
- created with a list copy of current difficulty BPM changes:
  - `gridController.currentMapDifficultyBpmChanges?.ToList()`

### Constructor initialization flow

- stores `caller` reference and snapshot `globalBPM`
- binds DataGrid `ItemsSource` to local `List<BPMChange>`
- displays rounded global BPM in `lblGlobalBPM`

### External refresh path

- `RefreshBPMChanges()` calls `dataBPMChange.Items.Refresh()`
- MainWindow invokes this when map editor BPM changes are modified from other workflows

## 3. Named controls and roles

| Control name | Type | Role | Key handlers |
| --- | --- | --- | --- |
| `lblGlobalBPM` | `Label` | Displays global/base BPM reference | (constructor set) |
| `btnExit` | `Button` | Closes window | `btnExit_Click` |
| `dataBPMChange` | `DataGrid` | CRUD surface for BPM changes | `CellEditEnding`, `CurrentCellChanged`, `RowEditEnding`, `AddingNewItem`, `PreviewExecuted` |

DataGrid columns:

- `Global Beat` -> `globalBeat`
- `BPM` -> `BPM`
- `Beat Division` -> `gridDivision`

## 4. Expected initial state

- fixed-size window, centered on owner
- global BPM label shows current map global BPM rounded to 3 decimals
- DataGrid rows show BPM changes for the currently selected difficulty at open time
- no explicit sorting UI; list is sorted programmatically on row commit

## 5. Supported user actions

### Keyboard shortcuts

- no custom shortcuts implemented, but DataGrid delete behavior is intercepted via command routing (`DataGrid.DeleteCommand`)

### Mouse interactions

| Interaction | Target | Result |
| --- | --- | --- |
| edit cell | `dataBPMChange` | validates edited value on cell commit |
| commit row | `dataBPMChange` | sorts list, propagates to map difficulty, redraws editor |
| add new row | `dataBPMChange` new-item row | creates BPM change seeded from current playback beat/global BPM/grid division |
| delete selected rows | `dataBPMChange` | removes selected BPM changes and propagates |
| click `Exit` | `btnExit` | closes window |

### Drag and drop behavior

- No drag-and-drop handlers are implemented.

### Playback-related interactions

- no direct playback controls in this window
- new BPM change defaults are derived from current MainWindow playback position (`sliderSongProgress`)
- edits immediately affect editor grid timing display via `caller.DrawEditorGrid(false)`

## 6. Validation and state rules

### Input validation

Validation runs in `dataBPMChange_CellEditEnding` and cancels invalid edits with error dialogs.

| Column | Rule | Failure behavior |
| --- | --- | --- |
| `Global Beat` | non-negative number | error dialog + `CancelEdit()` |
| `BPM` | positive number | error dialog + `CancelEdit()` |
| `Beat Division` | integer in `1..Editor.GridDivisionMax` | error dialog + `CancelEdit()` |

### Edit/commit rules

- `RowEditEnding` explicitly commits edit, rebinds sorted list, propagates changes, and redraws editor
- `CurrentCellChanged` triggers grid redraw to reflect pending/committed timing context changes
- delete command handling marks command as handled after custom removal flow

## 7. Persistence behavior

### Propagation model

`propagateBPMChanges()` copies local list into current map difficulty:

- `mapDiff.bpmChanges = new(BPMChanges)`
- `mapDiff.MarkDirty()`

This updates in-memory map state immediately; final file persistence still depends on normal map save workflow in MainWindow.

### Add/Delete flows

- add new item creates `BPMChange` using:
  - beat from current playback position converted to global beat
  - current global BPM
  - current grid division from editor grid
- delete removes selected `BPMChange` items from local list before propagation

## 8. Dialogs and file flows

### Dialogs

- validation error dialogs for invalid cell values

### File flows

- none directly in this window
- all changes remain in map editor state until map save/export actions occur elsewhere

## 9. Dependencies and background work

### Key dependencies

- `MainWindow` caller reference
- `BPMChange` model (`IComparable` sorting by `globalBeat`)
- current map difficulty state via `caller.mapEditor.currentMapDifficulty`
- editor grid redraw path via `caller.DrawEditorGrid(false)`

### Background/async behavior

- no background threads/timers
- all edits and redraw triggers are synchronous on UI thread

## 10. Timing-sensitive behavior

- frequent redraws can happen during grid edit lifecycle (`CurrentCellChanged` + row commit)
- list rebind on row commit (`ItemsSource = null` then restored) may affect selection/edit continuity
- new-row default beat uses current slider position at creation time, so timing depends on playback state at that moment

## 11. Test mapping

### Candidate WPF UI baseline tests

- constructor binds rows and shows expected rounded global BPM
- validation by column rejects invalid values and preserves previous values
- row commit sorts BPM changes by global beat
- add-new-item seeds beat/BPM/grid division from caller state
- delete command removes selected rows and updates difficulty state
- propagation marks difficulty dirty and reflects immediately in editor redraw

### Candidate Avalonia parity tests

- same editable grid columns and validation semantics
- same add/delete commit behavior and sort order
- same propagation timing to current difficulty model
- same redraw/update behavior after edits

### Gaps/questions to verify in runtime pass

- UX impact of redraw frequency while editing large BPM-change lists
- edge behavior when multiple rows are selected and deleted during active cell edit
- whether global BPM label should live-update if global BPM changes while window stays open
