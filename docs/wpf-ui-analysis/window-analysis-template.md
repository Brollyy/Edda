# Window Analysis Template

Use this template for each WPF window to capture baseline behavior before migration.

## Metadata

- Window:
- Status: Scaffolded / In progress / Complete
- Source files:
- Related services or shared classes:

## 1. Purpose

- Primary use case:
- User goal:

## 2. Entry points and startup flow

- How the window is opened:
- Required preconditions:
- Initial focus behavior:

## 3. Named controls and roles

| Control name | Type | Role | Key events/handlers | Notes |
| --- | --- | --- | --- | --- |
| | | | | |

## 4. Expected initial state

- Default values:
- Visibility/enabled state:
- Data loaded on open:

## 5. Supported user actions

### Keyboard shortcuts

| Shortcut | Scope | Result | Notes |
| --- | --- | --- | --- |
| | | | |

### Mouse interactions

| Interaction | Target | Result | Notes |
| --- | --- | --- | --- |
| | | | |

### Drag and drop behavior

| Source/target | Allowed data | Result | Notes |
| --- | --- | --- | --- |
| | | | |

### Playback-related interactions

| Action | Trigger | Result | Notes |
| --- | --- | --- | --- |
| | | | |

## 6. Validation and state rules

- Input validation behavior:
- Error messages:
- Enable/disable logic:

## 7. Persistence behavior

- Settings read on load:
- Settings persisted on close/save:
- Side effects:

## 8. Dialogs and file flows

- File pickers:
- Confirmation dialogs:
- Error dialogs:
- Import/export behavior:

## 9. Dependencies and background work

- Services used:
- Async/background operations:
- UI thread assumptions:

## 10. Timing-sensitive behavior

- Playback timing dependencies:
- Debounce/throttle/repaint assumptions:
- Race-condition risks:

## 11. Test mapping

- Candidate WPF UI tests:
- Candidate Avalonia parity tests:
- Gaps/questions:
