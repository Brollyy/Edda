# WPF Split and Analysis Plan

Before starting the WPF → Avalonia migration, the current WPF user interface should be isolated into its own explicit project boundary and analyzed as the baseline implementation.

## Why this step is required

A migration without a stable baseline makes it too easy for agents to:

- regress behavior silently
- miss edge-case interactions
- change UI semantics while preserving only superficial layout
- port screens without proving behavioral equivalence

The WPF UI must therefore become the reference implementation against which future Avalonia behavior is validated.

## Pre-migration rule

No production screen should be migrated until all of the following are true:

1. The legacy WPF UI is isolated behind a dedicated project boundary.
2. The screen and its behaviors have been inventoried.
3. Baseline behavior is covered by WPF UI tests in `tests/Edda.Wpf.UI.Tests/`.
4. Matching Avalonia tests exist (or are tracked) in `tests/Edda.Avalonia.UI.Tests/` for migrated features.

## Recommended project split

Target structure:

- `src/Edda.Core/`
  - domain models
  - editor logic
  - map conversion logic
  - framework-agnostic services
- `src/Edda.App/`
  - application services
  - commands
  - framework-agnostic view models
  - UI contracts
- `src/Edda.Wpf/`
  - current WPF windows
  - WPF resources
  - WPF-specific adapters
- `src/Edda.Avalonia/`
  - Avalonia views
  - Avalonia resources
  - Avalonia-specific adapters
- `tests/Edda.Wpf.UI.Tests/`
  - WPF UI test suite and driver
- `tests/Edda.Avalonia.UI.Tests/`
  - Avalonia UI test suite and driver

## Required analysis pass

Before any screen is ported, perform an analysis pass over the WPF UI and record:

- all windows and dialogs
- all named controls and their roles
- startup flows
- keyboard shortcuts
- mouse interactions
- drag and drop behaviors
- playback-related interactions
- validation behavior
- visibility and enabled/disabled rules
- settings persistence behavior
- error and confirmation dialogs
- file import/export flows

## Analysis output

The analysis pass should produce a living inventory for each screen with:

- screen purpose
- required controls
- expected initial state
- supported user actions
- observable outcomes
- dependencies on services or background work
- known timing-sensitive behavior

## Migration gate

The first Avalonia PR should not be a visual port.

The first implementation PR should instead:

1. carve out the WPF project boundary
2. add baseline WPF UI tests for the targeted functionality
3. scaffold separate Avalonia tests for the same functionality
4. capture baseline expectations from the WPF application

Only after this gate is complete should screen-by-screen migration begin.
