# AGENTS.md

## Project Overview

Edda is a desktop editor for mapping songs to levels for the VR rhythm game Ragnarock.

This repository is an Avalonia rewrite of the original Edda application. The current application is cross-platform .NET/Avalonia code; the WPF application has been removed.

The repository currently contains:
- An Avalonia desktop application
- Shared application services
- Core map editing, audio processing, and conversion logic
- Application assets and embedded resources

---

## Repository Structure

Key directories:

- `src/Edda.Avalonia/` — Avalonia desktop UI, windows, platform services, and app entry point
- `src/Edda.App/` — shared application-level services and adapters
- `src/Edda.Core/` — core logic, utilities, map converters, audio processing, constants, and resources

Important entry points:

- `src/Edda.Avalonia/Program.cs`
- `src/Edda.Avalonia/App.cs`
- `src/Edda.Avalonia/Windows/MainWindow.cs`
- `src/Edda.Core/Classes/EddaConstants.cs`

---

## Build Instructions

Restore dependencies:

```bash
dotnet restore
```

Build the Avalonia application:

```bash
dotnet build src/Edda.Avalonia/Edda.Avalonia.csproj
```

If a solution file exists and only includes current projects, building the solution is also acceptable:

```bash
dotnet build *.sln
```

---

## Platform Considerations

- Keep shared logic in `src/Edda.Core/` platform-independent where possible.
- Keep application orchestration and service wiring in `src/Edda.App/`.
- Keep UI behavior and Avalonia-specific code isolated to `src/Edda.Avalonia/`.
- Do not introduce Windows-only dependencies unless they are isolated behind platform-specific services.

---

## Editing Guidelines

When making changes:

- Keep diffs small and focused.
- Avoid renaming files unless necessary.
- Follow the existing code style in surrounding files.
- Do not introduce new packages unless needed.
- Prefer shared logic changes in `src/Edda.Core/` or `src/Edda.App/` when behavior is not UI-specific.

---

## Validation

After making changes:

1. Run `dotnet build src/Edda.Avalonia/Edda.Avalonia.csproj`.
2. Ensure no compilation errors occur.
3. For UI changes, verify the affected Avalonia window or workflow compiles.

---

## Summary Format For Pull Requests

When submitting changes, summarize work as:

Changed:
- files modified

Validation:
- commands run

Notes:
- any limitations or environment constraints
