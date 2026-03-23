# UI Migration Test Strategy

For now, the migration effort uses two independent UI test projects instead of a shared scenario harness:

- `tests/Edda.Wpf.UI.Tests`
- `tests/Edda.Avalonia.UI.Tests`

## Why this approach

The WPF and Avalonia UI layers will not be identical during migration. Keeping tests separate avoids forcing a shared test contract too early and allows each project to evolve at its own pace.

## Current testing model

- WPF tests are the baseline checks against the existing application behavior.
- Avalonia tests are separate and will be written/reworked as each migrated feature lands.
- Assertions and control lookup details can differ between the two projects.

Each project owns its own driver class:

- `WpfUIDriver`
- `AvaloniaUIDriver`

## Current scaffold status

- Both projects are configured as xUnit test projects and run via `dotnet test`.
- Each project includes startup-focused tests:
  - one active wiring test (`DriverCanBeCreated`)
  - skipped behavioral tests that become active once the corresponding driver is implemented

## Commands

```bash
dotnet test tests/Edda.Wpf.UI.Tests
dotnet test tests/Edda.Avalonia.UI.Tests
```

Notes:

- WPF test execution requires Windows desktop runtime support.
- On non-Windows environments, WPF tests may build but not execute.
