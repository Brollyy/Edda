# DifficultyPredictorWindow

- Status: Complete
- Analysis level: Documented baseline from code and implemented behavior
- Last updated: 2026-03-24

## Source files

- `src/Edda.Wpf/Windows/DifficultyPredictorWindow.xaml`
- `src/Edda.Wpf/Windows/DifficultyPredictorWindow.xaml.cs`
- `src/Edda.Wpf/Windows/MainWindow.MenuItems.cs` (entry point)
- `src/Edda.Wpf/Windows/MainWindow.xaml.cs` (live map-stats predictor path)
- `src/Edda.App/Classes/MapEditor/Stats/IDifficultyPredictor.cs` (feature contract)

## 1. Purpose

Manual prediction utility for estimating difficulty ranks across the current map's difficulty set.

Supports:

- selecting predictor algorithm
- optionally showing precise decimal predictions
- toggling whether predictions appear live in MainWindow map stats
- one-click recalculation of all loaded difficulties

## 2. Entry points and startup flow

### How the window is opened

- opened from MainWindow Tools -> Difficulty Predictor (`MenuItemDifficultyPredictor_Click`)
- uses MainWindow single-instance helper (`ShowUniqueWindow`)
- owner set to MainWindow

### Constructor initialization flow

- stores `MainWindow` and shared `UserSettingsManager` references
- initializes two settings toggles:
  - `DifficultyPredictorShowPrecise`
  - `DifficultyPredictorShowInMapStats`
- loads selected algorithm from settings and checks matching radio button
- sets `windowLoaded = true` after initial control setup to prevent early setting writes from constructor-triggered events

### Predictor binding lifecycle

- algorithm radio changes write setting + call `UpdateSettings()`
- `UpdateSettings()` writes settings and calls `mainWindow.LoadSettingsFile(true)`
- MainWindow maps algorithm key to predictor singleton and updates live map-stats label behavior

## 3. Named controls and roles

| Control name | Type | Role | Key handlers |
| --- | --- | --- | --- |
| `PKBeamAlgoRadioButton` | `RadioButton` | Select PKBeam model | `PKBeamAlgoRadioButton_Checked` |
| `NytildeAlgoRadioButton` | `RadioButton` | Select Nytilde model | `NytildeAlgoRadioButton_Checked` |
| `MelchiorAlgoRadioButton` | `RadioButton` | Select Melchior scorer | `MelchiorAlgoRadioButton_Checked` |
| `CheckShowPreciseValues` | `CheckBox` | Toggle decimal precision display | `CheckShowPreciseValues_Click` |
| `CheckShowInMapStats` | `CheckBox` | Toggle live prediction in MainWindow stats area | `CheckShowInMapStats_Click` |
| `btnPredict` | `Button` | Executes prediction for all map difficulties | `BtnPredict_Click` |
| `PanelPredictionResults` | `StackPanel` | Hosts prediction result UI | shown/hidden in `BtnPredict_Click` |
| `PanelPredictionWarning` | `TextBlock` | Warning for uncertain predictions (`???`) | visibility set in `BtnPredict_Click` |
| `btnDifficulty0..2` | `Button` | Visual containers for predicted ranks | enabled/disabled in `BtnPredict_Click` |
| `lblDifficultyRank1..3` | `Label` | Predicted values per difficulty slot | updated in `BtnPredict_Click` |

## 4. Expected initial state

- algorithm radio reflects persisted algorithm key
- precise/map-stats checkboxes reflect persisted settings
- prediction results panel hidden (`PanelPredictionResults.Visibility = Hidden`)
- warning text hidden
- no prediction values shown until user clicks `Predict`

## 5. Supported user actions

### Keyboard shortcuts

- no explicit keyboard shortcuts are implemented

### Mouse interactions

| Interaction | Target | Result |
| --- | --- | --- |
| click radio button | algorithm options | updates selected predictor setting and MainWindow predictor binding |
| click checkbox | precise/map-stats options | persists setting and refreshes MainWindow live prediction behavior |
| click `Predict` | `btnPredict` | computes and renders predictions for available difficulties |
| hover info icons | algorithm descriptions | shows tooltip explanation and caveats |

### Drag and drop behavior

- No drag-and-drop behavior is implemented.

### Playback-related interactions

- no direct playback controls
- optional side effect: map-stats prediction label in MainWindow updates as settings/algorithm change

## 6. Validation and state rules

### Prediction rendering rules

- prediction button starts by disabling all difficulty result buttons and hiding results/warnings
- iterates current map difficulties (`for i < mapEditor.numDifficulties`), enabling result slot buttons as needed
- if predictor returns value:
  - label color = `DifficultyPrediction.Colour`
  - value rounded to integer or 2 decimals based on precise toggle + predictor feature support
- if predictor returns null and algorithm is not `AlwaysPredict`:
  - label color = warning color
  - label text = `???`
  - warning text shown
- otherwise fallback value `0`

### Settings state rules

- checkbox/radio changes persist immediately and call `mainWindow.UpdateDifficultyPrediction()`
- `windowLoaded` guard prevents constructor radio initialization from immediately rewriting settings
- `CheckShowPreciseValues.IsEnabled` is recalculated on algorithm change from predictor feature flags

### Known edge case

- `BtnPredict_Click` assumes `mainWindow.mapEditor` is non-null; if no map is loaded and user clicks Predict, this can throw at `mapEditor.numDifficulties`.

## 7. Persistence behavior

### Settings keys written

- `DifficultyPredictorAlgorithm`
- `DifficultyPredictorShowPrecise`
- `DifficultyPredictorShowInMapStats`

### Apply path

- `UpdateSettings()`:
  - `userSettings.Write()`
  - `mainWindow.LoadSettingsFile(true)` (rebinding predictor and related UI behavior)

No file import/export is performed in this window.

## 8. Dialogs and file flows

- No dialogs are shown by this window.
- No direct file I/O.

## 9. Dependencies and background work

### Key dependencies

- `MainWindow` for current map data and live UI updates
- shared `UserSettingsManager`
- `IDifficultyPredictor` implementations (`PKBeam`, `Nytilde`, `Melchior`)
- predictor feature flags (`PreciseFloat`, `AlwaysPredict`, `RealTime`)

### Background/async behavior

- none; prediction and UI updates run synchronously on UI thread

## 10. Timing-sensitive behavior

- prediction runs on button click in UI thread; heavier models may momentarily block UI
- map-stats live label updates depend on immediate settings reload path in MainWindow
- warning visibility can change per-click depending on model output and map completeness/state

## 11. Test mapping

### Candidate WPF UI baseline tests

- constructor reflects persisted algorithm and toggle settings
- radio changes persist algorithm key and update MainWindow predictor binding
- precise toggle affects label formatting only when predictor supports `PreciseFloat`
- map-stats toggle updates MainWindow difficulty label visibility behavior
- predict action populates result labels/buttons for current difficulty count
- null prediction path shows `???` and warning text
- safe behavior expectation when no map is loaded (document current failure or add guard test)

### Candidate Avalonia parity tests

- same algorithm selection/toggle persistence semantics
- same result rendering rules (colors, precision, warning handling)
- same live integration behavior with main map-stats prediction label
- same handling for unsupported/uncertain predictions

### Gaps/questions to verify in runtime pass

- runtime UX/performance of model inference on larger maps
- whether no-map `Predict` should be guarded with user-facing message
- whether `CheckShowPreciseValues.IsEnabled` should follow `PreciseFloat` feature explicitly
