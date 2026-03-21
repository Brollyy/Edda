# Avalonia Migration Workplan

This document tracks concrete steps toward introducing an Avalonia UI for Edda.

## Stage 0 — Preparation

Tasks:

- Audit WPF-only dependencies
- Identify logic in `Windows/` that should move to shared services
- Document UI windows and their responsibilities

Output:

- migration inventory
- prioritized screen list

## Stage 1 — Core extraction

Tasks:

- Introduce view models for:
  - Settings
  - audio device selection
  - map metadata

- Move non-UI logic from code-behind into:

```
Classes/ViewModels/
Classes/Services/
```

Goal:

Allow both WPF and Avalonia UIs to consume the same logic.

## Stage 2 — Avalonia project introduction

Create:

```
src/Edda.Avalonia/
```

Minimal project responsibilities:

- Application bootstrap
- Main window shell
- dependency wiring

No editor logic should live here.

## Stage 3 — First migrated screen

Recommended first migration:

- Settings window

Why:

- mostly forms
- minimal rendering logic

Steps:

1. Extract view model
2. Bind WPF UI to the new view model
3. Create Avalonia view bound to the same view model

## Stage 4 — Shared services stabilization

Introduce abstractions for:

- dialogs
- file picking
- UI dispatcher
- clipboard

Example interface:

```
IFileDialogService
IMessageDialogService
IClipboardService
IUIDispatcher
```

## Stage 5 — Editor migration

Once the service layer stabilizes:

- move editor interaction logic into shared components
- gradually migrate editor UI elements

The main editor window should be the last screen migrated.

## Tracking progress

Recommended issue labels:

- `avalonia-migration`
- `ui-refactor`
- `viewmodel-extraction`

Example milestone:

```
Avalonia Migration Phase 1
```

Tracks extraction of shared logic and view models.
