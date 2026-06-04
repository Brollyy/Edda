# WPF UI Analysis Inventory

This folder contains the scaffold for the WPF UI baseline analysis described in `docs/wpf-split-and-analysis-plan.md`.

The goal of this scaffold phase is to establish a stable inventory and placeholders for all windows before writing detailed behavior notes.

## Scope

The current WPF analysis scope includes these windows:

- `StartWindow`
- `MainWindow`
- `SettingsWindow`
- `CustomizeNavBarWindow`
- `SongPreviewWindow`
- `ChangeBPMWindow`
- `BPMCalcWindow`
- `DifficultyPredictorWindow`
- `AboutWindow`

## Scaffold contents

- `docs/wpf-ui-analysis/window-feature-matrix.md` - high-level feature matrix by window
- `docs/wpf-ui-analysis/window-analysis-template.md` - reusable per-window analysis template
- `docs/wpf-ui-analysis/windows/*.md` - per-window scaffold records

## Status legend

- `Scaffolded` - structure created, detailed behavior not yet documented
- `In progress` - details are being added and validated against code
- `Complete` - behavior inventory is populated and ready for migration/test planning

## Window inventory

| Window | Source files | Record | Status |
| --- | --- | --- | --- |
| `StartWindow` | `src/Edda.Wpf/Windows/StartWindow.xaml`, `src/Edda.Wpf/Windows/StartWindow.xaml.cs` | `docs/wpf-ui-analysis/windows/start-window.md` | Complete |
| `MainWindow` | `src/Edda.Wpf/Windows/MainWindow.xaml`, `src/Edda.Wpf/Windows/MainWindow.xaml.cs`, `src/Edda.Wpf/Windows/MainWindow.UIControls.cs`, `src/Edda.Wpf/Windows/MainWindow.MenuItems.cs`, `src/Edda.Wpf/Windows/MainWindow.GridControls.cs` | `docs/wpf-ui-analysis/windows/main-window.md` | Complete |
| `SettingsWindow` | `src/Edda.Wpf/Windows/SettingsWindow.xaml`, `src/Edda.Wpf/Windows/SettingsWindow.xaml.cs` | `docs/wpf-ui-analysis/windows/settings-window.md` | Complete |
| `CustomizeNavBarWindow` | `src/Edda.Wpf/Windows/CustomizeNavBarWindow.xaml`, `src/Edda.Wpf/Windows/CustomizeNavBarWindow.xaml.cs` | `docs/wpf-ui-analysis/windows/customize-nav-bar-window.md` | Complete |
| `SongPreviewWindow` | `src/Edda.Wpf/Windows/SongPreviewWindow.xaml`, `src/Edda.Wpf/Windows/SongPreviewWindow.xaml.cs` | `docs/wpf-ui-analysis/windows/song-preview-window.md` | Complete |
| `ChangeBPMWindow` | `src/Edda.Wpf/Windows/ChangeBPMWindow.xaml`, `src/Edda.Wpf/Windows/ChangeBPMWindow.xaml.cs` | `docs/wpf-ui-analysis/windows/change-bpm-window.md` | Complete |
| `BPMCalcWindow` | `src/Edda.Wpf/Windows/BPMCalcWindow.xaml`, `src/Edda.Wpf/Windows/BPMCalcWindow.xaml.cs` | `docs/wpf-ui-analysis/windows/bpm-calc-window.md` | Complete |
| `DifficultyPredictorWindow` | `src/Edda.Wpf/Windows/DifficultyPredictorWindow.xaml`, `src/Edda.Wpf/Windows/DifficultyPredictorWindow.xaml.cs` | `docs/wpf-ui-analysis/windows/difficulty-predictor-window.md` | Complete |
| `AboutWindow` | `src/Edda.Wpf/Windows/AboutWindow.xaml`, `src/Edda.Wpf/Windows/AboutWindow.xaml.cs` | `docs/wpf-ui-analysis/windows/about-window.md` | Complete |

## Next step

All per-window WPF records are now documented; use these baselines to map expectations into `tests/Edda.Wpf.UI.Tests/` and migration parity plans.
