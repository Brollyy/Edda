using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Automation;

namespace Edda.Avalonia.UI.Tests;

/// <summary>
/// UI automation driver that runs the real Avalonia application process and interacts
/// with controls through Windows UI Automation and physical mouse/keyboard input.
/// </summary>
public sealed class AvaloniaUIDriver {
    const string StartWindowId = "StartWindow";
    const string MainWindowId = "AppMainWindow";
    const string SettingsWindowId = "SettingsWindow";
    const string MainWindowFallbackTitle = "Edda";
    const string PickerCancelSentinel = "__EDDA_TEST_PICKER_CANCEL__";
    const string PickerQueueFileEnvironmentVariable = "EDDA_TEST_PICKER_QUEUE_FILE";
    const string DebugLogFileEnvironmentVariable = "EDDA_TEST_DEBUG_LOG_FILE";
    static readonly string[] MainWindowSentinelIds = {
        "btnSongPlayer",
        "sliderSongTempo",
        "txtSongName"
    };

    const uint KeyEventFKeyUp = 0x0002;
    const uint KeyEventFScancode = 0x0008;
    const uint MouseEventFLeftDown = 0x0002;
    const uint MouseEventFLeftUp = 0x0004;
    const uint MouseEventFRightDown = 0x0008;
    const uint MouseEventFRightUp = 0x0010;
    const byte VirtualKeyShift = 0x10;
    const byte VirtualKeyControl = 0x11;
    const byte VirtualKeyAlt = 0x12;

    readonly TimeSpan defaultTimeout = TimeSpan.FromSeconds(15);
    readonly Dictionary<string, string?> launchEnvironmentOverrides = new(StringComparer.OrdinalIgnoreCase);

    Process? appProcess;
    string? appLaunchRoot;
    string? exceptionLogFilePath;
    string? debugLogFilePath;
    string? pickerSelectionQueueFilePath;
    string? testProfileRoot;
    int? lastKnownMainWindowHandle;
    int? pendingMainWindowReplacementSourceHandle;

    public void SetLaunchEnvironmentVariable(string key, string? value) {
        if (string.IsNullOrWhiteSpace(key)) {
            throw new ArgumentException("Environment variable key cannot be empty.", nameof(key));
        }

        if (value == null) {
            launchEnvironmentOverrides.Remove(key);
        } else {
            launchEnvironmentOverrides[key] = value;
        }
    }

    public void Launch() {
        if (appProcess is { HasExited: false }) {
            return;
        }

        var appPath = ResolveAppExecutablePath();
        testProfileRoot = Path.Combine(Path.GetTempPath(), "Edda-AvaloniaUiTests", Guid.NewGuid().ToString("N"));
        var appDataRoot = Path.Combine(testProfileRoot, "AppData", "Roaming");
        var localAppDataRoot = Path.Combine(testProfileRoot, "AppData", "Local");
        appLaunchRoot = Path.Combine(testProfileRoot, "AppUnderTest");
        exceptionLogFilePath = Path.Combine(testProfileRoot, "exception.log");
        debugLogFilePath = Path.Combine(testProfileRoot, "debug.log");
        pickerSelectionQueueFilePath = Path.Combine(testProfileRoot, "picker-queue.txt");
        Directory.CreateDirectory(appDataRoot);
        Directory.CreateDirectory(localAppDataRoot);
        File.WriteAllText(pickerSelectionQueueFilePath, string.Empty);
        var isolatedAppPath = CopyAppToIsolatedRoot(appPath, appLaunchRoot);

        var startInfo = new ProcessStartInfo {
            UseShellExecute = false
        };

        if (isolatedAppPath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) {
            startInfo.FileName = "dotnet";
            startInfo.Arguments = $"\"{isolatedAppPath}\"";
        } else {
            startInfo.FileName = isolatedAppPath;
        }

        startInfo.WorkingDirectory = Path.GetDirectoryName(isolatedAppPath) ?? Directory.GetCurrentDirectory();
        startInfo.Environment["APPDATA"] = appDataRoot;
        startInfo.Environment["LOCALAPPDATA"] = localAppDataRoot;
        startInfo.Environment["EDDA_TEST_EXCEPTION_LOG_FILE"] = exceptionLogFilePath;
        startInfo.Environment[DebugLogFileEnvironmentVariable] = debugLogFilePath;
        startInfo.Environment[PickerQueueFileEnvironmentVariable] = pickerSelectionQueueFilePath;
        foreach (var (key, value) in launchEnvironmentOverrides) {
            startInfo.Environment[key] = value ?? string.Empty;
        }

        appProcess = Process.Start(startInfo) ?? throw new InvalidOperationException($"Failed to launch {appPath}.");
        try {
            appProcess.WaitForInputIdle((int)TimeSpan.FromSeconds(10).TotalMilliseconds);
        } catch {
            // Some environments/process startup paths do not expose input-idle state.
        }

        WaitForIdle();
        WaitForStartWindow();
    }

    public void Shutdown() {
        if (appProcess is { HasExited: false }) {
            try {
                var window = FindSettingsWindow() ?? FindMainWindow() ?? FindStartWindow();
                if (window != null) {
                    FocusWindow(window);
                    SendKeyboardShortcutCore("Alt+F4");
                    if (appProcess.WaitForExit((int)TimeSpan.FromSeconds(3).TotalMilliseconds)) {
                        appProcess.Dispose();
                        appProcess = null;
                    }
                }
            } catch {
                // Fall back to forceful cleanup below.
            }

            if (appProcess is { HasExited: false }) {
                appProcess.Kill(entireProcessTree: true);
                appProcess.WaitForExit((int)TimeSpan.FromSeconds(5).TotalMilliseconds);
            }
        }

        appProcess?.Dispose();
        appProcess = null;
        appLaunchRoot = null;
        exceptionLogFilePath = null;
        debugLogFilePath = null;
        launchEnvironmentOverrides.Clear();
        pickerSelectionQueueFilePath = null;
        lastKnownMainWindowHandle = null;
        pendingMainWindowReplacementSourceHandle = null;

        if (!string.IsNullOrWhiteSpace(testProfileRoot) && Directory.Exists(testProfileRoot)) {
            try {
                Directory.Delete(testProfileRoot, recursive: true);
            } catch {
                // Best effort cleanup for temporary profile data.
            }
        }

        testProfileRoot = null;
    }

    public void WaitForIdle(TimeSpan? timeout = null) {
        EnsureLaunched();

        var remaining = timeout ?? TimeSpan.FromSeconds(2);
        var deadline = DateTime.UtcNow + remaining;
        while (DateTime.UtcNow < deadline) {
            if (appProcess is { HasExited: true }) {
                throw new InvalidOperationException(WithDiagnostics("Avalonia process exited unexpectedly."));
            }

            try {
                var waitMs = Math.Max(1, (int)Math.Min(remaining.TotalMilliseconds, 250));
                if (appProcess?.WaitForInputIdle(waitMs) == true) {
                    Thread.Sleep(80);
                    return;
                }
            } catch {
                // Some environments/process states do not expose input-idle reliably.
            }

            Thread.Sleep(40);
            remaining = deadline - DateTime.UtcNow;
        }

        Thread.Sleep(80);
    }

    public void ClickButton(string id) {
        ClickElement(GetElement(id));
        WaitForIdle();
    }

    public void ClickWithinElement(string id, double xRatio, double yRatio) {
        var element = GetElement(id);
        FocusElementWindow(element);
        var point = ResolvePointInElement(element, xRatio, yRatio);

        SetCursorPos(point.x, point.y);
        Thread.Sleep(20);
        mouse_event(MouseEventFLeftDown, 0, 0, 0, UIntPtr.Zero);
        Thread.Sleep(20);
        mouse_event(MouseEventFLeftUp, 0, 0, 0, UIntPtr.Zero);
        WaitForIdle();
    }

    public string GetText(string id) {
        return ReadElementText(GetElement(id));
    }

    public bool IsVisible(string id) {
        var element = GetElementOrNull(id);
        if (element == null) {
            return false;
        }

        try {
            var bounds = element.Current.BoundingRectangle;
            return !element.Current.IsOffscreen && bounds.Width > 1 && bounds.Height > 1;
        } catch (ElementNotAvailableException) {
            return false;
        } catch (COMException) {
            return false;
        }
    }

    public bool IsEnabled(string id) {
        var element = GetElementOrNull(id);
        return element != null && element.Current.IsEnabled;
    }

    public void SelectMenuItem(string path) {
        var segments = path.Split('>', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length == 0) {
            throw new ArgumentException("Menu path must contain at least one segment.", nameof(path));
        }

        if (PathTriggersMainWindowReplacement(path)) {
            MarkMainWindowReplacementExpected();
        }

        var currentScope = FindMainWindow() ?? throw new InvalidOperationException("Main window is not available for menu interaction.");
        FocusWindow(currentScope);

        for (var i = 0; i < segments.Length; i++) {
            var segment = segments[i];
            var isLast = i == segments.Length - 1;
            var menuItem = WaitForElement(
                () => FindMenuItem(segment, currentScope),
                defaultTimeout,
                $"Menu item '{segment}'");

            if (isLast) {
                ClickElement(menuItem);
            } else if (menuItem.TryGetCurrentPattern(ExpandCollapsePattern.Pattern, out var expandObj)) {
                ((ExpandCollapsePattern)expandObj).Expand();
            } else {
                ClickElement(menuItem);
            }

            currentScope = menuItem;
            Thread.Sleep(60);
        }

        WaitForIdle();
    }

    public bool IsMenuItemVisible(string path) {
        EnsureLaunched();
        var menuItem = FindMenuItemByPath(path);
        try {
            if (menuItem == null) {
                return false;
            }

            var bounds = menuItem.Current.BoundingRectangle;
            return !menuItem.Current.IsOffscreen && bounds.Width > 1 && bounds.Height > 1;
        } finally {
            DismissOpenMenus();
        }
    }

    public bool IsMenuItemChecked(string path) {
        EnsureLaunched();
        var menuItem = WaitForElement(
            () => FindMenuItemByPath(path),
            defaultTimeout,
            $"Menu item '{path}'");

        try {
            if (menuItem.TryGetCurrentPattern(TogglePattern.Pattern, out var toggleObj)) {
                return ((TogglePattern)toggleObj).Current.ToggleState == ToggleState.On;
            }

            if (menuItem.TryGetCurrentPattern(SelectionItemPattern.Pattern, out var selectionObj)) {
                return ((SelectionItemPattern)selectionObj).Current.IsSelected;
            }

            var itemStatus = menuItem.Current.ItemStatus ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(itemStatus)) {
                var normalizedStatus = itemStatus.Trim().ToLowerInvariant();
                if (normalizedStatus is "checked" or "on" or "selected" or "true") {
                    return true;
                }

                if (normalizedStatus is "unchecked" or "off" or "unselected" or "false") {
                    return false;
                }
            }

            var helpText = menuItem.Current.HelpText ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(helpText)) {
                var normalizedHelpText = helpText.Trim().ToLowerInvariant();
                if (normalizedHelpText.Contains("checked", StringComparison.Ordinal) ||
                    normalizedHelpText.Contains(" on", StringComparison.Ordinal) ||
                    normalizedHelpText == "on" ||
                    normalizedHelpText == "true") {
                    return true;
                }

                if (normalizedHelpText.Contains("unchecked", StringComparison.Ordinal) ||
                    normalizedHelpText.Contains(" off", StringComparison.Ordinal) ||
                    normalizedHelpText == "off" ||
                    normalizedHelpText == "false") {
                    return false;
                }
            }

            return false;
        } finally {
            DismissOpenMenus();
        }
    }

    public void ToggleCheckbox(string id, bool isChecked) {
        var element = GetElement(id);
        FocusElementWindow(element);
        TrySetFocus(element);

        if (IsChecked(id) == isChecked) {
            return;
        }

        SendKeyboardShortcutCore("Space");
        WaitForIdle();
        if (WaitUntil(() => IsChecked(id) == isChecked, TimeSpan.FromSeconds(1))) {
            return;
        }

        ClickElement(element);
        WaitForIdle();
        if (WaitUntil(() => IsChecked(id) == isChecked, TimeSpan.FromSeconds(1))) {
            return;
        }

        if (element.TryGetCurrentPattern(TogglePattern.Pattern, out var toggleObj)) {
            ((TogglePattern)toggleObj).Toggle();
            WaitForIdle();
        }

        if (!WaitUntil(() => IsChecked(id) == isChecked, TimeSpan.FromSeconds(1))) {
            throw new InvalidOperationException($"Checkbox '{id}' did not reach the requested state '{isChecked}'.");
        }
    }

    public bool IsChecked(string id) {
        var element = GetElement(id);
        if (element.TryGetCurrentPattern(TogglePattern.Pattern, out var toggleObj)) {
            return ((TogglePattern)toggleObj).Current.ToggleState == ToggleState.On;
        }

        if (element.TryGetCurrentPattern(SelectionItemPattern.Pattern, out var selectionObj)) {
            return ((SelectionItemPattern)selectionObj).Current.IsSelected;
        }

        return false;
    }

    public void SetText(string id, string value) {
        var element = GetElement(id);
        FocusElementWindow(element);
        TrySetFocus(element);

        if (element.TryGetCurrentPattern(ValuePattern.Pattern, out var valuePatternObj) && element.Current.IsKeyboardFocusable) {
            ((ValuePattern)valuePatternObj).SetValue(value);
            return;
        }

        var bounds = element.Current.BoundingRectangle;
        var x = (int)(bounds.Left + bounds.Width / 2);
        var y = (int)(bounds.Top + bounds.Height / 2);
        PerformLeftDoubleClick(x, y);
        Thread.Sleep(40);
        SendCombo(VirtualKeyControl, 0x41);
        Thread.Sleep(40);
        SendText(value);
    }

    public void SetSliderValue(string id, double value) {
        var element = GetElement(id);
        FocusElementWindow(element);
        TrySetFocus(element);
        if (!element.TryGetCurrentPattern(RangeValuePattern.Pattern, out var rangePatternObj)) {
            throw new InvalidOperationException($"Element '{id}' does not support range input.");
        }

        var range = (RangeValuePattern)rangePatternObj;
        var clamped = Math.Max(range.Current.Minimum, Math.Min(range.Current.Maximum, value));
        range.SetValue(clamped);
        WaitForIdle();
    }

    public double GetSliderValue(string id) {
        var element = GetElement(id);
        if (!element.TryGetCurrentPattern(RangeValuePattern.Pattern, out var rangePatternObj)) {
            throw new InvalidOperationException($"Element '{id}' does not support range value lookup.");
        }

        return ((RangeValuePattern)rangePatternObj).Current.Value;
    }

    public void DoubleClickElement(string id) {
        var element = GetElement(id);
        FocusElementWindow(element);
        var bounds = element.Current.BoundingRectangle;
        var x = (int)(bounds.Left + bounds.Width / 2);
        var y = (int)(bounds.Top + bounds.Height / 2);

        if (Equals(element.Current.ControlType, ControlType.Slider) &&
            element.TryGetCurrentPattern(RangeValuePattern.Pattern, out var rangeObj)) {
            var range = (RangeValuePattern)rangeObj;
            var minimum = range.Current.Minimum;
            var maximum = range.Current.Maximum;
            if (maximum > minimum) {
                var ratio = ClampRatio((range.Current.Value - minimum) / (maximum - minimum));
                ratio = Math.Max(0.05, Math.Min(0.95, ratio));
                x = (int)(bounds.Left + bounds.Width * ratio);
            }
        }

        PerformLeftDoubleClick(x, y);
        WaitForIdle();
    }

    public void SelectDropdown(string id, string value) {
        var combo = GetElement(id);
        FocusElementWindow(combo);
        TrySetFocus(combo);

        if (combo.TryGetCurrentPattern(ExpandCollapsePattern.Pattern, out var expandObj)) {
            ((ExpandCollapsePattern)expandObj).Expand();
        } else {
            ClickElement(combo);
        }

        Thread.Sleep(100);

        var item = WaitForElement(
            () => FindElementByName(value, ControlType.ListItem) ?? FindElementByName(value, ControlType.MenuItem),
            defaultTimeout,
            $"Dropdown item '{value}'");

        ClickElement(item);
        WaitForIdle();
    }

    public bool TrySelectDifferentDropdownValue(string id) {
        var combo = GetElement(id);
        FocusElementWindow(combo);
        TrySetFocus(combo);

        var currentValue = GetSelectedValue(id);
        if (combo.TryGetCurrentPattern(ExpandCollapsePattern.Pattern, out var expandObj)) {
            ((ExpandCollapsePattern)expandObj).Expand();
        }

        Thread.Sleep(100);

        var optionNames = new List<string>();
        foreach (var scope in EnumerateComboSearchScopes(combo)) {
            var items = scope.FindAll(
                TreeScope.Descendants,
                new OrCondition(
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ListItem),
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.MenuItem)));

            foreach (AutomationElement item in items) {
                var name = item.Current.Name ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(name) && !optionNames.Contains(name, StringComparer.Ordinal)) {
                    optionNames.Add(name);
                }
            }
        }

        var nextValue = optionNames.FirstOrDefault(name => !string.Equals(name, currentValue, StringComparison.Ordinal));
        if (string.IsNullOrWhiteSpace(nextValue)) {
            if (combo.TryGetCurrentPattern(ExpandCollapsePattern.Pattern, out expandObj)) {
                ((ExpandCollapsePattern)expandObj).Collapse();
            }

            WaitForIdle();
            return false;
        }

        var itemToSelect = WaitForElement(
            () => FindElementByName(nextValue, ControlType.ListItem) ?? FindElementByName(nextValue, ControlType.MenuItem),
            defaultTimeout,
            $"Dropdown item '{nextValue}'");

        ClickElement(itemToSelect);
        WaitForIdle();
        return true;
    }

    public string GetSelectedValue(string id) {
        var element = GetElement(id);
        if (element.TryGetCurrentPattern(SelectionPattern.Pattern, out var selectionObj)) {
            var selected = ((SelectionPattern)selectionObj).Current.GetSelection();
            if (selected.Length > 0) {
                return selected[0].Current.Name ?? string.Empty;
            }
        }

        var text = element.FindFirst(
            TreeScope.Descendants,
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Text));
        if (text != null && !string.IsNullOrWhiteSpace(text.Current.Name)) {
            return text.Current.Name;
        }

        return element.Current.Name ?? string.Empty;
    }

    public void SendKeyboardShortcut(string shortcut) {
        EnsureLaunched();
        if (ShortcutTriggersMainWindowReplacement(shortcut)) {
            MarkMainWindowReplacementExpected();
        }
        FocusAppWindow();
        SendKeyboardShortcutCore(shortcut);
        WaitForIdle();
    }

    public void SendKeyboardShortcutToWindow(string shortcut, string windowTitle) {
        EnsureLaunched();
        var window = WaitForElement(
            () => FindWindowByTitle(windowTitle),
            defaultTimeout,
            $"Window titled '{windowTitle}'");

        FocusWindow(window);
        SendKeyboardShortcutCore(shortcut);
        WaitForIdle();
    }

    public void ClickListItemContainingText(string listAutomationId, string text) {
        var list = GetElement(listAutomationId);
        var item = WaitForElement(
            () => FindListItemContainingText(list, text),
            defaultTimeout,
            $"List item containing '{text}' in '{listAutomationId}'");

        PhysicalClickElement(item);
        WaitForIdle();
    }

    public void RightClickListItemContainingText(string listAutomationId, string text) {
        var list = GetElement(listAutomationId);
        var item = WaitForElement(
            () => FindListItemContainingText(list, text),
            defaultTimeout,
            $"List item containing '{text}' in '{listAutomationId}'");

        FocusElementWindow(item);
        var bounds = item.Current.BoundingRectangle;
        var x = (int)(bounds.Left + bounds.Width / 2);
        var y = (int)(bounds.Top + bounds.Height / 2);
        SetCursorPos(x, y);
        Thread.Sleep(20);
        mouse_event(MouseEventFRightDown, 0, 0, 0, UIntPtr.Zero);
        Thread.Sleep(20);
        mouse_event(MouseEventFRightUp, 0, 0, 0, UIntPtr.Zero);
        WaitForIdle();
    }

    public int GetListItemCount(string id) {
        var list = GetElement(id);
        var count = 0;
        var walker = TreeWalker.ControlViewWalker;
        var child = walker.GetFirstChild(list);
        while (child != null) {
            if (IsMeaningfulElement(child)) {
                count++;
            }

            child = walker.GetNextSibling(child);
        }

        return count;
    }

    public bool ContainsText(string text) {
        return FindTextContaining(text) != null;
    }

    public void InvokeCommand(string commandId) {
        var buttonName = ResolveDialogButtonName(commandId);
        if (string.IsNullOrWhiteSpace(buttonName)) {
            return;
        }

        var button = WaitForElement(
            () => FindDialogButton(buttonName),
            defaultTimeout,
            $"Dialog button '{buttonName}'");

        ClickElement(button);
        WaitForIdle();

        if (string.Equals(commandId, "DialogResult.Cancel", StringComparison.Ordinal) &&
            pendingMainWindowReplacementSourceHandle != null &&
            FindMainWindow() is AutomationElement currentMainWindow) {
            pendingMainWindowReplacementSourceHandle = null;
            lastKnownMainWindowHandle = currentMainWindow.Current.NativeWindowHandle;
        }
    }

    public bool TryInvokeCommand(string commandId) {
        var buttonName = ResolveDialogButtonName(commandId);
        if (string.IsNullOrWhiteSpace(buttonName)) {
            return false;
        }

        if (!WaitUntil(() => FindDialogButton(buttonName) != null, TimeSpan.FromSeconds(2))) {
            return false;
        }

        InvokeCommand(commandId);
        return true;
    }

    public void SetTestFileSelection(string path) {
        EnsureLaunched();
        if (string.IsNullOrWhiteSpace(path)) {
            throw new ArgumentException("Test file selection path cannot be empty.", nameof(path));
        }

        WritePickerSelections(Path.GetFullPath(path));
    }

    public void SetTestFileSelections(params string?[] paths) {
        EnsureLaunched();
        if (paths == null || paths.Length == 0) {
            throw new ArgumentException("At least one file or folder path is required.", nameof(paths));
        }

        var normalizedPaths = paths.Select(path => {
            if (string.IsNullOrWhiteSpace(path)) {
                throw new ArgumentException("Test file selection paths cannot be empty.", nameof(paths));
            }

            return Path.GetFullPath(path);
        }).ToArray();

        WritePickerSelections(normalizedPaths);
    }

    public void SetTestPickerCancellation() {
        EnsureLaunched();
        WritePickerSelections(PickerCancelSentinel);
    }

    public void WaitForMainWindow(TimeSpan? timeout = null) {
        try {
            var effectiveTimeout = timeout ?? defaultTimeout;
            AutomationElement window;
            if (pendingMainWindowReplacementSourceHandle is int previousHandle) {
                window = WaitForElement(
                    () => FindMainWindowReplacing(previousHandle),
                    effectiveTimeout,
                    "replacement main window");
            } else {
                window = WaitForElement(() => FindMainWindow(), effectiveTimeout, "main window");
            }

            lastKnownMainWindowHandle = window.Current.NativeWindowHandle;
            pendingMainWindowReplacementSourceHandle = null;
        } catch (Exception ex) when (ex is TimeoutException or InvalidOperationException) {
            throw new TimeoutException(WithDiagnostics(ex.Message, includeDebugLog: true));
        }
    }

    public void WaitForStartWindow(TimeSpan? timeout = null) {
        try {
            _ = WaitForElement(FindStartWindow, timeout ?? defaultTimeout, "start window");
        } catch (Exception ex) when (ex is TimeoutException or InvalidOperationException) {
            throw new TimeoutException(WithDiagnostics(ex.Message));
        }
    }

    public void WaitForSettingsWindow(TimeSpan? timeout = null) {
        try {
            _ = WaitForElement(FindSettingsWindow, timeout ?? defaultTimeout, "settings window");
        } catch (Exception ex) when (ex is TimeoutException or InvalidOperationException) {
            throw new TimeoutException(WithDiagnostics(ex.Message));
        }
    }

    public bool WaitForExit(TimeSpan timeout) {
        if (appProcess == null) {
            return true;
        }

        return appProcess.WaitForExit((int)timeout.TotalMilliseconds);
    }

    public bool IsProcessRunning() {
        return appProcess is { HasExited: false };
    }

    public (double left, double top, double width, double height) GetElementBounds(string id) {
        var element = GetElement(id);
        var bounds = element.Current.BoundingRectangle;
        return (bounds.Left, bounds.Top, bounds.Width, bounds.Height);
    }

    public void DragWithinElement(string id, double startXRatio, double startYRatio, double endXRatio, double endYRatio) {
        var element = GetElement(id);
        FocusElementWindow(element);

        var start = ResolvePointInElement(element, startXRatio, startYRatio);
        var end = ResolvePointInElement(element, endXRatio, endYRatio);

        SetCursorPos(start.x, start.y);
        Thread.Sleep(20);
        mouse_event(MouseEventFLeftDown, 0, 0, 0, UIntPtr.Zero);
        Thread.Sleep(40);

        const int stepCount = 12;
        for (var step = 1; step <= stepCount; step++) {
            var progress = step / (double)stepCount;
            var x = (int)Math.Round(start.x + ((end.x - start.x) * progress));
            var y = (int)Math.Round(start.y + ((end.y - start.y) * progress));
            SetCursorPos(x, y);
            Thread.Sleep(25);
        }

        Thread.Sleep(40);
        mouse_event(MouseEventFLeftUp, 0, 0, 0, UIntPtr.Zero);
        WaitForIdle();
    }

    AutomationElement GetElement(string id) {
        return WaitForElement(() => GetElementOrNull(id), defaultTimeout, $"Element '{id}'");
    }

    AutomationElement? GetElementOrNull(string id) {
        EnsureLaunched();

        return id switch {
            StartWindowId => FindStartWindow(),
            MainWindowId => FindMainWindow(),
            SettingsWindowId => FindSettingsWindow(),
            _ => FindElementByAutomationId(id)
        };
    }

    AutomationElement? FindStartWindow() {
        foreach (var window in GetProcessWindowsInSearchOrder()) {
            if (string.Equals(window.Current.AutomationId, StartWindowId, StringComparison.Ordinal)) {
                return window;
            }

            if (FindByAutomationIdWithin(window, "ButtonOpenMap") != null) {
                return window;
            }
        }

        return null;
    }

    AutomationElement? FindMainWindow() {
        return FindMainWindowCore(lastKnownMainWindowHandle);
    }

    AutomationElement? FindMainWindowReplacing(int previousHandle) {
        return FindMainWindowCore(
            preferredHandle: lastKnownMainWindowHandle,
            disallowedHandle: previousHandle);
    }

    AutomationElement? FindMainWindowCore(int? preferredHandle = null, int? disallowedHandle = null) {
        var candidates = GetProcessWindowsInSearchOrder()
            .Where(IsMainWindowCandidate)
            .ToList();

        if (preferredHandle is int preferred) {
            var preferredWindow = candidates.FirstOrDefault(window => window.Current.NativeWindowHandle == preferred);
            if (preferredWindow != null && preferredWindow.Current.NativeWindowHandle != disallowedHandle) {
                return preferredWindow;
            }
        }

        return candidates.FirstOrDefault(window => window.Current.NativeWindowHandle != disallowedHandle);
    }

    AutomationElement? FindSettingsWindow() {
        foreach (var window in GetProcessWindowsInSearchOrder()) {
            if (string.Equals(window.Current.AutomationId, SettingsWindowId, StringComparison.Ordinal)) {
                return window;
            }

            if (string.Equals(window.Current.Name ?? string.Empty, "Settings", StringComparison.OrdinalIgnoreCase)) {
                return window;
            }
        }

        return null;
    }

    AutomationElement? FindElementByAutomationId(string automationId) {
        foreach (var window in GetProcessWindowsInSearchOrder()) {
            var inWindow = FindByAutomationIdWithin(window, automationId);
            if (inWindow != null) {
                return inWindow;
            }
        }

        return null;
    }

    static AutomationElement? FindByAutomationIdWithin(AutomationElement scope, string automationId) {
        if (string.Equals(scope.Current.AutomationId, automationId, StringComparison.Ordinal)) {
            return scope;
        }

        var condition = new PropertyCondition(AutomationElement.AutomationIdProperty, automationId);
        return scope.FindFirst(TreeScope.Descendants, condition);
    }

    AutomationElement? FindElementByName(string name, ControlType controlType) {
        foreach (var window in GetProcessWindowsInSearchOrder()) {
            var condition = new AndCondition(
                new PropertyCondition(AutomationElement.ControlTypeProperty, controlType),
                new PropertyCondition(AutomationElement.NameProperty, name));
            var element = window.FindFirst(TreeScope.Descendants, condition);
            if (element != null) {
                return element;
            }
        }

        return null;
    }

    AutomationElement? FindWindowByTitle(string title) {
        return GetProcessWindowsInSearchOrder().FirstOrDefault(window =>
            string.Equals(window.Current.Name ?? string.Empty, title, StringComparison.OrdinalIgnoreCase));
    }

    AutomationElement? FindMenuItem(string menuLabel, AutomationElement scope) {
        var target = NormalizeUiLabel(menuLabel);
        var allMenuItems = scope.FindAll(
            TreeScope.Descendants,
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.MenuItem));

        foreach (AutomationElement item in allMenuItems) {
            if (NormalizeUiLabel(item.Current.Name) == target) {
                return item;
            }
        }

        foreach (var window in GetProcessWindows()) {
            var items = window.FindAll(
                TreeScope.Descendants,
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.MenuItem));
            foreach (AutomationElement item in items) {
                if (NormalizeUiLabel(item.Current.Name) == target) {
                    return item;
                }
            }
        }

        return null;
    }

    AutomationElement? FindMenuItemByPath(string path) {
        var segments = path.Split('>', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length == 0) {
            return null;
        }

        var mainWindow = FindMainWindow();
        if (mainWindow == null) {
            return null;
        }

        FocusWindow(mainWindow);
        AutomationElement currentScope = mainWindow;
        for (var i = 0; i < segments.Length; i++) {
            var segment = segments[i];
            var menuItem = FindMenuItem(segment, currentScope);
            if (menuItem == null) {
                return null;
            }

            if (i == segments.Length - 1) {
                return menuItem;
            }

            if (menuItem.TryGetCurrentPattern(ExpandCollapsePattern.Pattern, out var expandObj)) {
                ((ExpandCollapsePattern)expandObj).Expand();
            } else {
                ClickElement(menuItem);
            }

            currentScope = menuItem;
            Thread.Sleep(40);
        }

        return null;
    }

    AutomationElement? FindDialogButton(string buttonName) {
        foreach (var window in GetDialogWindowsInSearchOrder()) {
            var byId = FindByAutomationIdWithin(window, ResolveDialogButtonAutomationId(buttonName));
            if (byId != null) {
                return byId;
            }

            var condition = new AndCondition(
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button),
                new PropertyCondition(AutomationElement.NameProperty, buttonName));
            var button = window.FindFirst(TreeScope.Descendants, condition);
            if (button != null) {
                return button;
            }
        }

        return null;
    }

    AutomationElement? FindTextContaining(string text) {
        foreach (var window in GetProcessWindowsInSearchOrder()) {
            var descendants = window.FindAll(TreeScope.Descendants, Condition.TrueCondition);
            foreach (AutomationElement descendant in descendants) {
                if ((descendant.Current.Name ?? string.Empty).Contains(text, StringComparison.OrdinalIgnoreCase)) {
                    return descendant;
                }

                if (descendant.TryGetCurrentPattern(ValuePattern.Pattern, out var valueObj) &&
                    (((ValuePattern)valueObj).Current.Value ?? string.Empty).Contains(text, StringComparison.OrdinalIgnoreCase)) {
                    return descendant;
                }
            }
        }

        return null;
    }

    AutomationElement? FindListItemContainingText(AutomationElement list, string text) {
        if ((list.Current.Name ?? string.Empty).Contains(text, StringComparison.OrdinalIgnoreCase)) {
            return list;
        }

        var descendants = list.FindAll(TreeScope.Descendants, Condition.TrueCondition);
        foreach (AutomationElement descendant in descendants) {
            var name = descendant.Current.Name ?? string.Empty;
            if (!name.Contains(text, StringComparison.OrdinalIgnoreCase)) {
                continue;
            }

            var current = descendant;
            AutomationElement? candidate = descendant;
            while (current != null) {
                var parent = SafeGetParent(current);
                if (parent == null) {
                    break;
                }

                if (AreSameElement(parent, list)) {
                    return candidate;
                }

                candidate = current;
                current = parent;
            }

            return candidate;
        }

        return null;
    }

    static AutomationElement? SafeGetParent(AutomationElement element) {
        try {
            return TreeWalker.ControlViewWalker.GetParent(element);
        } catch (COMException) {
            return null;
        } catch (ElementNotAvailableException) {
            return null;
        }
    }

    static bool AreSameElement(AutomationElement first, AutomationElement second) {
        try {
            return Automation.Compare(first, second);
        } catch {
            try {
                return first.GetRuntimeId().SequenceEqual(second.GetRuntimeId());
            } catch {
                return false;
            }
        }
    }

    IEnumerable<AutomationElement> GetProcessWindowsInSearchOrder() {
        var foregroundHandle = GetForegroundWindow();
        return GetProcessWindows()
            .OrderBy(window => new IntPtr(window.Current.NativeWindowHandle) != foregroundHandle)
            .ThenBy(window => window.Current.IsOffscreen)
            .ThenByDescending(window => window.Current.NativeWindowHandle);
    }

    IEnumerable<AutomationElement> GetDialogWindowsInSearchOrder() {
        return GetProcessWindowsInSearchOrder()
            .Where(window => {
                var id = window.Current.AutomationId ?? string.Empty;
                var title = window.Current.Name ?? string.Empty;
                return id.StartsWith("Dialog", StringComparison.Ordinal) ||
                    !string.Equals(title, "Edda", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(title, "Settings", StringComparison.OrdinalIgnoreCase);
            });
    }

    IReadOnlyList<AutomationElement> GetProcessWindows() {
        if (appProcess is null || appProcess.HasExited) {
            return Array.Empty<AutomationElement>();
        }

        var condition = new AndCondition(
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Window),
            new PropertyCondition(AutomationElement.ProcessIdProperty, appProcess.Id));

        var results = new List<AutomationElement>();
        var seen = new HashSet<int>();
        var desktopChildren = AutomationElement.RootElement.FindAll(TreeScope.Children, Condition.TrueCondition);
        foreach (AutomationElement child in desktopChildren) {
            TryAddWindow(child, results, seen);

            try {
                var nestedWindows = child.FindAll(TreeScope.Descendants, condition);
                foreach (AutomationElement nestedWindow in nestedWindows) {
                    TryAddWindow(nestedWindow, results, seen);
                }
            } catch (COMException) {
                // Some desktop child trees do not respond well to descendant enumeration.
            }
        }

        return results;

        void TryAddWindow(AutomationElement window, ICollection<AutomationElement> collection, ISet<int> handles) {
            try {
                if (!Equals(window.Current.ControlType, ControlType.Window) || window.Current.ProcessId != appProcess.Id) {
                    return;
                }

                var handle = window.Current.NativeWindowHandle;
                if (handle != 0 && !handles.Add(handle)) {
                    return;
                }

                collection.Add(window);
            } catch (COMException) {
                // The window disappeared while we were enumerating it.
            } catch (ElementNotAvailableException) {
                // The window disappeared while we were enumerating it.
            }
        }
    }

    void ClickElement(AutomationElement element) {
        FocusElementWindow(element);
        TrySetFocus(element);

        var bounds = element.Current.BoundingRectangle;
        if (bounds.Width > 1 && bounds.Height > 1) {
            PhysicalClickElement(element);
            return;
        }

        if (element.TryGetCurrentPattern(InvokePattern.Pattern, out var invokeObj)) {
            ((InvokePattern)invokeObj).Invoke();
            return;
        }

        if (element.TryGetCurrentPattern(SelectionItemPattern.Pattern, out var selectionObj)) {
            ((SelectionItemPattern)selectionObj).Select();
            return;
        }

        if (element.TryGetCurrentPattern(ExpandCollapsePattern.Pattern, out var expandObj)) {
            var expand = (ExpandCollapsePattern)expandObj;
            if (expand.Current.ExpandCollapseState == ExpandCollapseState.Collapsed) {
                expand.Expand();
            } else {
                expand.Collapse();
            }
        }
    }

    void FocusAppWindow() {
        var window = FindSettingsWindow() ?? FindMainWindow() ?? FindStartWindow() ?? GetProcessWindows().FirstOrDefault();
        if (window != null) {
            FocusWindow(window);
        }
    }

    void FocusElementWindow(AutomationElement element) {
        var current = element;
        while (current != null && !Equals(current.Current.ControlType, ControlType.Window)) {
            current = SafeGetParent(current);
        }

        if (current != null) {
            FocusWindow(current);
        }
    }

    static void FocusWindow(AutomationElement window) {
        try {
            var hwnd = new IntPtr(window.Current.NativeWindowHandle);
            if (hwnd != IntPtr.Zero) {
                SetForegroundWindow(hwnd);
                Thread.Sleep(40);
            }
        } catch {
            // Best effort.
        }

        TrySetFocus(window);
    }

    static void PhysicalClickElement(AutomationElement element) {
        TrySetFocus(element);
        var bounds = element.Current.BoundingRectangle;
        var x = (int)(bounds.Left + bounds.Width / 2);
        var y = (int)(bounds.Top + bounds.Height / 2);
        SetCursorPos(x, y);
        Thread.Sleep(20);
        mouse_event(MouseEventFLeftDown, 0, 0, 0, UIntPtr.Zero);
        Thread.Sleep(20);
        mouse_event(MouseEventFLeftUp, 0, 0, 0, UIntPtr.Zero);
    }

    static void TrySetFocus(AutomationElement element) {
        try {
            element.SetFocus();
        } catch {
            // Some controls do not support focus through UI Automation.
        }
    }

    static string ReadElementText(AutomationElement element) {
        if (element.TryGetCurrentPattern(ValuePattern.Pattern, out var valueObj)) {
            return ((ValuePattern)valueObj).Current.Value ?? string.Empty;
        }

        var text = element.FindFirst(
            TreeScope.Descendants,
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Text));
        if (text != null && !string.IsNullOrWhiteSpace(text.Current.Name)) {
            return text.Current.Name;
        }

        return element.Current.Name ?? string.Empty;
    }

    static bool IsMeaningfulElement(AutomationElement element) {
        try {
            var bounds = element.Current.BoundingRectangle;
            return bounds.Width > 1 && bounds.Height > 1;
        } catch {
            return false;
        }
    }

    IEnumerable<AutomationElement> EnumerateComboSearchScopes(AutomationElement combo) {
        yield return combo;

        foreach (var window in GetProcessWindows()) {
            yield return window;
        }
    }

    void EnsureLaunched() {
        if (appProcess == null) {
            throw new InvalidOperationException("Driver is not launched. Call Launch() first.");
        }

        if (appProcess.HasExited) {
            throw new InvalidOperationException("Driver is not launched because the Avalonia process has already exited.");
        }
    }

    string WithDiagnostics(string message) {
        return $"{message}{Environment.NewLine}{GetProcessDebugSummary()}{Environment.NewLine}{GetWindowDebugSummary()}{Environment.NewLine}{GetExceptionLogSummary()}";
    }

    string WithDiagnostics(string message, bool includeDebugLog) {
        if (!includeDebugLog) {
            return WithDiagnostics(message);
        }

        return $"{message}{Environment.NewLine}{GetProcessDebugSummary()}{Environment.NewLine}{GetWindowDebugSummary()}{Environment.NewLine}{GetExceptionLogSummary()}{Environment.NewLine}{GetDebugLogSummary()}";
    }

    string GetProcessDebugSummary() {
        if (appProcess == null) {
            return "Avalonia process: not launched";
        }

        try {
            if (!appProcess.HasExited) {
                return $"Avalonia process: running (pid {appProcess.Id})";
            }

            return $"Avalonia process: exited with code {appProcess.ExitCode}";
        } catch (InvalidOperationException) {
            return "Avalonia process: unavailable";
        }
    }

    string GetWindowDebugSummary() {
        var windows = GetProcessWindows()
            .Select(window => {
                var title = window.Current.Name ?? string.Empty;
                var automationId = window.Current.AutomationId ?? string.Empty;
                var handle = window.Current.NativeWindowHandle;
                var offscreen = window.Current.IsOffscreen;
                return $"Window(title='{title}', id='{automationId}', hwnd={handle}, offscreen={offscreen}, descendants=[{DescribeWindowDescendants(window)}])";
            });

        return $"Visible Avalonia windows: {string.Join(" | ", windows)}";
    }

    string GetExceptionLogSummary() {
        if (string.IsNullOrWhiteSpace(exceptionLogFilePath) || !File.Exists(exceptionLogFilePath)) {
            return "Unhandled exception log: empty";
        }

        try {
            var contents = File.ReadAllText(exceptionLogFilePath);
            if (string.IsNullOrWhiteSpace(contents)) {
                return "Unhandled exception log: empty";
            }

            return $"Unhandled exception log:{Environment.NewLine}{contents}";
        } catch (Exception ex) {
            return $"Unhandled exception log unavailable: {ex.Message}";
        }
    }

    string GetDebugLogSummary() {
        if (string.IsNullOrWhiteSpace(debugLogFilePath) || !File.Exists(debugLogFilePath)) {
            return "Debug log: empty";
        }

        try {
            var contents = File.ReadAllText(debugLogFilePath);
            if (string.IsNullOrWhiteSpace(contents)) {
                return "Debug log: empty";
            }

            return $"Debug log:{Environment.NewLine}{contents}";
        } catch (Exception ex) {
            return $"Debug log unavailable: {ex.Message}";
        }
    }

    static string DescribeWindowDescendants(AutomationElement window) {
        try {
            return string.Join(", ",
                window.FindAll(TreeScope.Descendants, Condition.TrueCondition)
                    .Cast<AutomationElement>()
                    .Select(element => {
                        var id = element.Current.AutomationId ?? string.Empty;
                        var name = element.Current.Name ?? string.Empty;
                        var controlType = element.Current.ControlType?.ProgrammaticName ?? "unknown";
                        return (id, name, controlType);
                    })
                    .Where(element => !string.IsNullOrWhiteSpace(element.id) || !string.IsNullOrWhiteSpace(element.name))
                    .Take(30)
                    .Select(element => $"{element.controlType}('{element.name}', id='{element.id}')"));
        } catch (COMException) {
            return "descendants unavailable";
        } catch (ElementNotAvailableException) {
            return "descendants unavailable";
        }
    }

    void DismissOpenMenus() {
        try {
            FocusAppWindow();
            SendKey(0x1B);
            Thread.Sleep(40);
        } catch {
            // Best effort cleanup after menu inspection.
        }
    }

    static string ResolveDialogButtonName(string commandId) {
        return commandId.Trim() switch {
            "DialogResult.Yes" => "Yes",
            "DialogResult.No" => "No",
            "DialogResult.Cancel" => "Cancel",
            "DialogResult.Ok" => "OK",
            _ => string.Empty
        };
    }

    static string ResolveDialogButtonAutomationId(string buttonName) {
        return buttonName switch {
            "Yes" => "DialogButtonYes",
            "No" => "DialogButtonNo",
            "Cancel" => "DialogButtonCancel",
            "OK" => "DialogButtonOk",
            _ => string.Empty
        };
    }

    static string NormalizeUiLabel(string? value) {
        if (string.IsNullOrWhiteSpace(value)) {
            return string.Empty;
        }

        return new string(value.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
    }

    void MarkMainWindowReplacementExpected() {
        var currentMainWindow = FindMainWindow();
        if (currentMainWindow != null) {
            pendingMainWindowReplacementSourceHandle = currentMainWindow.Current.NativeWindowHandle;
            lastKnownMainWindowHandle = currentMainWindow.Current.NativeWindowHandle;
        }
    }

    static bool PathTriggersMainWindowReplacement(string path) {
        return path switch {
            "File>Open Map" => true,
            "File>New Map" => true,
            "File>Import Map" => true,
            _ => false
        };
    }

    static bool ShortcutTriggersMainWindowReplacement(string shortcut) {
        var normalized = shortcut.Trim().Replace(" ", string.Empty).ToLowerInvariant();
        return normalized is "ctrl+o" or "ctrl+n" or "ctrl+i";
    }

    static bool IsMainWindowCandidate(AutomationElement window) {
        if (string.Equals(window.Current.AutomationId, MainWindowId, StringComparison.Ordinal)) {
            return true;
        }

        if (string.Equals(window.Current.Name ?? string.Empty, MainWindowFallbackTitle, StringComparison.OrdinalIgnoreCase) &&
            MainWindowSentinelIds.Any(id => FindByAutomationIdWithin(window, id) != null)) {
            return true;
        }

        return MainWindowSentinelIds.Any(id => FindByAutomationIdWithin(window, id) != null);
    }

    static AutomationElement WaitForElement(Func<AutomationElement?> finder, TimeSpan timeout, string description) {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline) {
            var element = finder();
            if (element != null) {
                return element;
            }

            Thread.Sleep(50);
        }

        throw new TimeoutException($"Timed out waiting for {description}.");
    }

    static bool WaitUntil(Func<bool> condition, TimeSpan timeout) {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline) {
            if (condition()) {
                return true;
            }

            Thread.Sleep(50);
        }

        return false;
    }

    void WritePickerSelections(params string[] selections) {
        if (string.IsNullOrWhiteSpace(pickerSelectionQueueFilePath)) {
            throw new InvalidOperationException("Picker selection queue file is not initialized.");
        }

        File.WriteAllLines(pickerSelectionQueueFilePath, selections);
    }

    static string ResolveAppExecutablePath() {
        var envPath = Environment.GetEnvironmentVariable("EDDA_AVALONIA_EXE");
        if (!string.IsNullOrWhiteSpace(envPath) && File.Exists(envPath) &&
            (envPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) || envPath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))) {
            return envPath;
        }

        var rootsToProbe = new List<string> { Directory.GetCurrentDirectory(), AppContext.BaseDirectory };
        foreach (var root in rootsToProbe) {
            var repoRoot = FindRepositoryRoot(root);
            if (repoRoot == null) {
                continue;
            }

            var candidates = new[] {
                Path.Combine(repoRoot, "src", "Edda.Avalonia", "bin", "Debug", "net9.0", "Edda.Avalonia.exe"),
                Path.Combine(repoRoot, "src", "Edda.Avalonia", "bin", "Release", "net9.0", "Edda.Avalonia.exe"),
                Path.Combine(repoRoot, "src", "Edda.Avalonia", "bin", "Debug", "net9.0", "Edda.Avalonia.dll"),
                Path.Combine(repoRoot, "src", "Edda.Avalonia", "bin", "Release", "net9.0", "Edda.Avalonia.dll"),
                Path.Combine(repoRoot, "src", "Edda.Avalonia", "bin", "Debug", "net8.0-windows", "Edda.Avalonia.exe"),
                Path.Combine(repoRoot, "src", "Edda.Avalonia", "bin", "Release", "net8.0-windows", "Edda.Avalonia.exe"),
                Path.Combine(repoRoot, "src", "Edda.Avalonia", "bin", "Debug", "net8.0-windows", "Edda.Avalonia.dll"),
                Path.Combine(repoRoot, "src", "Edda.Avalonia", "bin", "Release", "net8.0-windows", "Edda.Avalonia.dll")
            };

            var match = candidates.FirstOrDefault(File.Exists);
            if (!string.IsNullOrWhiteSpace(match)) {
                return match;
            }
        }

        throw new FileNotFoundException("Could not locate Edda Avalonia binary. Set EDDA_AVALONIA_EXE to the full Edda.Avalonia.exe or Edda.Avalonia.dll path.");
    }

    static string CopyAppToIsolatedRoot(string appPath, string launchRoot) {
        var sourceDirectory = Path.GetDirectoryName(appPath) ?? throw new DirectoryNotFoundException($"Could not determine app directory for '{appPath}'.");
        Directory.CreateDirectory(launchRoot);

        foreach (var sourceFilePath in Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories)) {
            var relativePath = Path.GetRelativePath(sourceDirectory, sourceFilePath);
            var destinationFilePath = Path.Combine(launchRoot, relativePath);
            var destinationDirectory = Path.GetDirectoryName(destinationFilePath);
            if (!string.IsNullOrWhiteSpace(destinationDirectory)) {
                Directory.CreateDirectory(destinationDirectory);
            }

            File.Copy(sourceFilePath, destinationFilePath, overwrite: true);
        }

        return Path.Combine(launchRoot, Path.GetFileName(appPath));
    }

    static string? FindRepositoryRoot(string startPath) {
        var directory = new DirectoryInfo(startPath);
        while (directory != null) {
            if (File.Exists(Path.Combine(directory.FullName, "RagnarockEditor.sln"))) {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return null;
    }

    static double ClampRatio(double ratio) {
        if (double.IsNaN(ratio)) {
            return 0;
        }

        return Math.Max(0, Math.Min(1, ratio));
    }

    static (int x, int y) ResolvePointInElement(AutomationElement element, double xRatio, double yRatio) {
        var bounds = element.Current.BoundingRectangle;
        var x = (int)(bounds.Left + bounds.Width * ClampRatio(xRatio));
        var y = (int)(bounds.Top + bounds.Height * ClampRatio(yRatio));
        return (x, y);
    }

    static void PerformLeftDoubleClick(int x, int y) {
        SetCursorPos(x, y);
        Thread.Sleep(20);
        mouse_event(MouseEventFLeftDown, 0, 0, 0, UIntPtr.Zero);
        Thread.Sleep(20);
        mouse_event(MouseEventFLeftUp, 0, 0, 0, UIntPtr.Zero);
        Thread.Sleep(60);
        mouse_event(MouseEventFLeftDown, 0, 0, 0, UIntPtr.Zero);
        Thread.Sleep(20);
        mouse_event(MouseEventFLeftUp, 0, 0, 0, UIntPtr.Zero);
        Thread.Sleep(40);
    }

    static void SendKeyboardShortcutCore(string shortcut) {
        var normalized = shortcut.Trim().Replace(" ", string.Empty).ToLowerInvariant();
        switch (normalized) {
            case "ctrl+g":
                SendCombo(VirtualKeyControl, 0x47);
                break;
            case "ctrl+s":
                SendCombo(VirtualKeyControl, 0x53);
                break;
            case "ctrl+n":
                SendCombo(VirtualKeyControl, 0x4E);
                break;
            case "ctrl+o":
                SendCombo(VirtualKeyControl, 0x4F);
                break;
            case "ctrl+i":
                SendCombo(VirtualKeyControl, 0x49);
                break;
            case "ctrl+e":
                SendCombo(VirtualKeyControl, 0x45);
                break;
            case "ctrl+w":
                SendCombo(VirtualKeyControl, 0x57);
                break;
            case "ctrl+[":
                SendComboUsingScanCode(VirtualKeyControl, 0xDB);
                break;
            case "ctrl+]":
                SendComboUsingScanCode(VirtualKeyControl, 0xDD);
                break;
            case "ctrl+a":
                SendCombo(VirtualKeyControl, 0x41);
                break;
            case "alt+f4":
                SendCombo(VirtualKeyAlt, 0x73);
                break;
            case "space":
                SendKey(0x20);
                break;
            case "enter":
                SendKey(0x0D);
                break;
            case "tab":
                SendKey(0x09);
                break;
            case "escape":
                SendKey(0x1B);
                break;
            default:
                throw new NotSupportedException($"Unsupported keyboard shortcut '{shortcut}'.");
        }
    }

    static void SendCombo(byte modifier, byte key) {
        var scanCode = (byte)MapVirtualKey(key, 0);
        keybd_event(modifier, 0, 0, UIntPtr.Zero);
        Thread.Sleep(10);
        keybd_event(0, scanCode, KeyEventFScancode, UIntPtr.Zero);
        Thread.Sleep(10);
        keybd_event(0, scanCode, KeyEventFScancode | KeyEventFKeyUp, UIntPtr.Zero);
        Thread.Sleep(10);
        keybd_event(modifier, 0, KeyEventFKeyUp, UIntPtr.Zero);
    }

    static void SendComboUsingScanCode(byte modifier, byte key) {
        var scanCode = (byte)MapVirtualKey(key, 0);
        keybd_event(modifier, 0, 0, UIntPtr.Zero);
        keybd_event(0, scanCode, KeyEventFScancode, UIntPtr.Zero);
        keybd_event(0, scanCode, KeyEventFScancode | KeyEventFKeyUp, UIntPtr.Zero);
        keybd_event(modifier, 0, KeyEventFKeyUp, UIntPtr.Zero);
    }

    static void SendKey(byte key) {
        keybd_event(key, 0, 0, UIntPtr.Zero);
        keybd_event(key, 0, KeyEventFKeyUp, UIntPtr.Zero);
    }

    static void SendText(string text) {
        foreach (var ch in text) {
            var vkInfo = VkKeyScan(ch);
            if (vkInfo == -1) {
                continue;
            }

            var key = (byte)(vkInfo & 0xFF);
            var modifiers = (byte)((vkInfo >> 8) & 0xFF);

            if ((modifiers & 1) != 0) {
                keybd_event(VirtualKeyShift, 0, 0, UIntPtr.Zero);
            }
            if ((modifiers & 2) != 0) {
                keybd_event(VirtualKeyControl, 0, 0, UIntPtr.Zero);
            }
            if ((modifiers & 4) != 0) {
                keybd_event(VirtualKeyAlt, 0, 0, UIntPtr.Zero);
            }

            keybd_event(key, 0, 0, UIntPtr.Zero);
            keybd_event(key, 0, KeyEventFKeyUp, UIntPtr.Zero);

            if ((modifiers & 4) != 0) {
                keybd_event(VirtualKeyAlt, 0, KeyEventFKeyUp, UIntPtr.Zero);
            }
            if ((modifiers & 2) != 0) {
                keybd_event(VirtualKeyControl, 0, KeyEventFKeyUp, UIntPtr.Zero);
            }
            if ((modifiers & 1) != 0) {
                keybd_event(VirtualKeyShift, 0, KeyEventFKeyUp, UIntPtr.Zero);
            }
        }
    }

    [DllImport("user32.dll")]
    static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    [DllImport("user32.dll")]
    static extern uint MapVirtualKey(uint uCode, uint uMapType);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    static extern short VkKeyScan(char ch);

    [DllImport("user32.dll")]
    static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);
}
