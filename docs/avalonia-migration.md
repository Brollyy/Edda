# Avalonia Migration Scaffold for Edda

This document provides a structured path for migrating Edda from WPF to Avalonia without attempting a risky one-shot rewrite.

## Goals

- Preserve current editor behavior.
- Move non-UI logic out of WPF-specific code.
- Create a clear separation between application logic and presentation.
- Introduce an Avalonia UI incrementally.
- Keep the existing WPF application usable during the migration.

## Current state

The current repository is centered around:

- `Windows/` for WPF views and code-behind
- `Classes/` for editor logic, audio, helper utilities, converters, and settings
- NAudio-based audio playback and editor interaction logic

## Target architecture

Suggested long-term structure:

- `src/Edda.Core/`
  - editor state
  - domain models
  - shared services
  - file/map conversion logic
- `src/Edda.App/`
  - application services
  - commands
  - abstractions for dialogs, clipboard, file picking, and notifications
- `src/Edda.Avalonia/`
  - Avalonia views
  - view models
  - Avalonia-specific adapters
- `src/Edda.Wpf/`
  - optional landing place for legacy WPF UI during migration

This repo does not need to be reorganized immediately. The scaffold exists to guide future changes.

## Migration strategy

### Phase 1: Inventory and isolate

1. Identify WPF-specific code in `Windows/`.
2. Identify reusable logic in `Classes/`.
3. Introduce view models for the highest-value windows.
4. Replace direct UI logic in code-behind with calls into view models or services.

### Phase 2: Extract shared services

Extract interfaces for:

- file dialogs
- message dialogs
- settings access
- clipboard
- dispatcher / UI-thread invocation
- audio output selection, if UI-triggered

Keep concrete WPF implementations in the legacy UI layer.

### Phase 3: Introduce Avalonia shell

Create a minimal Avalonia application that can host:

- application startup
- a placeholder main window
- one migrated settings or utility screen

### Phase 4: Incremental screen migration

Recommended order:

1. `SettingsWindow`
2. simple dialogs and utility windows
3. editor-adjacent panels with mostly form controls
4. `MainWindow`

### Phase 5: Remove WPF dependencies from shared flow

As screens move to Avalonia:

- stop referencing WPF types from extracted logic
- move remaining shared logic into framework-agnostic classes
- keep platform-specific implementations behind interfaces

## High-priority extraction candidates

The following areas are likely good first targets:

- settings editing flow
- map metadata editing
- validation and parsing helpers
- non-visual editor commands
- import / conversion workflows

These are typically easier to migrate than the full editor canvas.

## Avalonia-specific notes

When porting WPF UI to Avalonia:

- do not assume XAML is drop-in compatible
- replace WPF-specific APIs intentionally
- move logic out of code-behind first
- avoid porting `Dispatcher` usage directly without an abstraction
- treat drag-and-drop, clipboard, and dialogs as platform services

## Validation guidance

For migration PRs, prefer this order:

1. unit-test shared extracted logic
2. build the current WPF application
3. build the new Avalonia project once introduced
4. verify migrated workflows manually

## Definition of done for each migrated screen

A screen is considered migrated when:

- its behavior is represented by framework-agnostic view models or services
- Avalonia UI exists for the workflow
- no new WPF-only logic is added to the shared path
- validation notes are recorded in the PR summary
