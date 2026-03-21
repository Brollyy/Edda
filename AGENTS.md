# AGENTS.md

## Project overview

Edda is a desktop editor for mapping songs to levels for the VR rhythm game Ragnarock. The application is a Windows desktop application built with .NET and WPF.

The repository currently contains:
- A WPF desktop application (MainWindow.xaml, Windows/*)
- Audio processing components using NAudio
- Mapping and conversion utilities

Future work may include migration toward cross-platform UI frameworks (e.g. Avalonia), but current builds target Windows.

---

## Repository structure

Key directories:

- `Classes/` — core logic, utilities, map converters, audio playback
- `Windows/` — WPF UI (MainWindow.xaml, SettingsWindow.xaml, etc.)
- `Resources/` — application assets
- `Const/` — constants and settings keys

Important entry points:

- `Windows/MainWindow.xaml`
- `Classes/EddaConstants.cs`

---

## Build instructions

Preferred commands:

Restore dependencies:

```bash
dotnet restore
```

Build the project:

```bash
dotnet build
```

If a solution file exists, prefer building the solution:

```bash
dotnet build *.sln
```

---

## Platform considerations

- The application currently depends on Windows-only frameworks.
- WPF UI code lives under `Windows/`.
- Non-UI code in `Classes/` should remain platform-independent where possible.

When modifying code:

1. Prefer editing shared logic in `Classes/` rather than UI code when possible.
2. Avoid introducing new Windows-only dependencies in shared components.
3. Keep UI behavior changes isolated to `Windows/`.

---

## Editing guidelines

When making changes:

- Keep diffs small and focused.
- Avoid renaming files unless necessary.
- Follow the existing code style in surrounding files.
- Do not introduce new packages unless needed.

---

## Validation

After making changes:

1. Run `dotnet build`
2. Ensure no compilation errors occur.
3. Confirm UI-related changes compile with the WPF project.

If running in a non-Windows environment:

- Only validate compilation of non-WPF logic where possible.

---

## Summary format for pull requests

When submitting changes, summarize work as:

Changed:
- files modified

Validation:
- commands run

Notes:
- any limitations or environment constraints
