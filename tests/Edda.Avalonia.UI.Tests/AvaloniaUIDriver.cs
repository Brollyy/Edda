using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
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
    const string SerialExecutionMutexName = @"Global\Edda.UiTests.SerialExecution";
    const string PickerCancelSentinel = "__EDDA_TEST_PICKER_CANCEL__";
    const uint WmClose = 0x0010;
    const string PickerQueueFileEnvironmentVariable = "EDDA_TEST_PICKER_QUEUE_FILE";
    const string DebugLogFileEnvironmentVariable = "EDDA_TEST_DEBUG_LOG_FILE";
    const string KeepProfileEnvironmentVariable = "EDDA_TEST_KEEP_PROFILE";
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
    const uint MouseEventFWheel = 0x0800;
    const int InputMouse = 0;
    const byte VirtualKeyShift = 0x10;
    const byte VirtualKeyControl = 0x11;
    const byte VirtualKeyAlt = 0x12;

    readonly TimeSpan defaultTimeout = TimeSpan.FromSeconds(15);
    readonly Dictionary<string, string?> launchEnvironmentOverrides = new(StringComparer.OrdinalIgnoreCase);
    string? launchWorkingDirectory;
    readonly Dictionary<string, AutomationElement> elementCache = new(StringComparer.Ordinal);

    Process? appProcess;
    Mutex? serialExecutionMutex;
    bool ownsSerialExecutionMutex;
    string? appLaunchRoot;
    string? exceptionLogFilePath;
    string? debugLogFilePath;
    string? driverLogFilePath;
    string? standardOutputLogFilePath;
    string? standardErrorLogFilePath;
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

    public void SetLaunchWorkingDirectory(string? path) {
        launchWorkingDirectory = string.IsNullOrWhiteSpace(path) ? null : path;
    }

    public void Launch() {
        if (appProcess is { HasExited: false }) {
            return;
        }

        AcquireSerialExecutionLock();
        var appPath = ResolveAppExecutablePath();
        CleanupStaleProcesses(Path.GetFileNameWithoutExtension(appPath));
        testProfileRoot = Path.Combine(Path.GetTempPath(), "Edda-AvaloniaUiTests", Guid.NewGuid().ToString("N"));
        var appDataRoot = Path.Combine(testProfileRoot, "AppData", "Roaming");
        var localAppDataRoot = Path.Combine(testProfileRoot, "AppData", "Local");
        appLaunchRoot = Path.Combine(testProfileRoot, "AppUnderTest");
        exceptionLogFilePath = Path.Combine(testProfileRoot, "exception.log");
        debugLogFilePath = Path.Combine(testProfileRoot, "debug.log");
        driverLogFilePath = Path.Combine(testProfileRoot, "driver.log");
        standardOutputLogFilePath = Path.Combine(testProfileRoot, "stdout.log");
        standardErrorLogFilePath = Path.Combine(testProfileRoot, "stderr.log");
        pickerSelectionQueueFilePath = Path.Combine(testProfileRoot, "picker-queue.txt");
        Directory.CreateDirectory(appDataRoot);
        Directory.CreateDirectory(localAppDataRoot);
        File.WriteAllText(pickerSelectionQueueFilePath, string.Empty);
        File.WriteAllText(driverLogFilePath, string.Empty);
        File.WriteAllText(standardOutputLogFilePath, string.Empty);
        File.WriteAllText(standardErrorLogFilePath, string.Empty);
        var isolatedAppPath = CopyAppToIsolatedRoot(appPath, appLaunchRoot);

        var startInfo = new ProcessStartInfo {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        if (isolatedAppPath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) {
            startInfo.FileName = "dotnet";
            startInfo.Arguments = $"\"{isolatedAppPath}\"";
        } else {
            startInfo.FileName = isolatedAppPath;
        }

        startInfo.WorkingDirectory = launchWorkingDirectory ?? Path.GetDirectoryName(isolatedAppPath) ?? Directory.GetCurrentDirectory();
        startInfo.Environment["APPDATA"] = appDataRoot;
        startInfo.Environment["LOCALAPPDATA"] = localAppDataRoot;
        startInfo.Environment["EDDA_TEST_EXCEPTION_LOG_FILE"] = exceptionLogFilePath;
        startInfo.Environment[DebugLogFileEnvironmentVariable] = debugLogFilePath;
        startInfo.Environment[PickerQueueFileEnvironmentVariable] = pickerSelectionQueueFilePath;
        foreach (var (key, value) in launchEnvironmentOverrides) {
            startInfo.Environment[key] = value ?? string.Empty;
        }

        try {
            appProcess = Process.Start(startInfo) ?? throw new InvalidOperationException($"Failed to launch {appPath}.");
            appProcess.OutputDataReceived += (_, eventArgs) => AppendProcessLogLine(standardOutputLogFilePath, eventArgs.Data);
            appProcess.ErrorDataReceived += (_, eventArgs) => AppendProcessLogLine(standardErrorLogFilePath, eventArgs.Data);
            appProcess.BeginOutputReadLine();
            appProcess.BeginErrorReadLine();
            elementCache.Clear();
            try {
                appProcess.WaitForInputIdle((int)TimeSpan.FromSeconds(10).TotalMilliseconds);
            } catch {
                // Some environments/process startup paths do not expose input-idle state.
            }

            WaitForIdle();
            WaitForStartWindow();
        } catch {
            ReleaseSerialExecutionLock();
            throw;
        }
    }

    public void Shutdown() {
        if (appProcess is { HasExited: false }) {
            try {
                foreach (var hwnd in GetProcessWindowHandles()) {
                    PostMessage(hwnd, WmClose, IntPtr.Zero, IntPtr.Zero);
                }

                if (appProcess.WaitForExit((int)TimeSpan.FromSeconds(3).TotalMilliseconds)) {
                    appProcess.Dispose();
                    appProcess = null;
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
        driverLogFilePath = null;
        standardOutputLogFilePath = null;
        standardErrorLogFilePath = null;
        launchEnvironmentOverrides.Clear();
        launchWorkingDirectory = null;
        pickerSelectionQueueFilePath = null;
        lastKnownMainWindowHandle = null;
        pendingMainWindowReplacementSourceHandle = null;
        elementCache.Clear();

        var keepProfile = string.Equals(Environment.GetEnvironmentVariable(KeepProfileEnvironmentVariable), "1", StringComparison.Ordinal);
        if (!keepProfile && !string.IsNullOrWhiteSpace(testProfileRoot) && Directory.Exists(testProfileRoot)) {
            try {
                Directory.Delete(testProfileRoot, recursive: true);
            } catch {
                // Best effort cleanup for temporary profile data.
            }
        }

        testProfileRoot = null;
        ReleaseSerialExecutionLock();
    }

    IntPtr[] GetProcessWindowHandles() {
        var handles = new List<IntPtr>();
        if (appProcess == null) {
            return handles.ToArray();
        }

        try {
            if (appProcess.MainWindowHandle != IntPtr.Zero) {
                handles.Add(appProcess.MainWindowHandle);
            }
        } catch {
            // Fall back to automation-discovered windows below.
        }

        try {
            foreach (var window in GetProcessWindows()) {
                try {
                    var hwnd = new IntPtr(window.Current.NativeWindowHandle);
                    if (hwnd != IntPtr.Zero && !handles.Contains(hwnd)) {
                        handles.Add(hwnd);
                    }
                } catch {
                    // Ignore windows that disappear while the app is closing.
                }
            }
        } catch {
            // Best effort enumeration only.
        }

        return handles.ToArray();
    }

    void AcquireSerialExecutionLock() {
        if (ownsSerialExecutionMutex) {
            return;
        }

        serialExecutionMutex ??= new Mutex(false, SerialExecutionMutexName);
        try {
            if (!serialExecutionMutex.WaitOne(TimeSpan.FromMinutes(5))) {
                throw new TimeoutException("Timed out waiting for exclusive UI test execution lock.");
            }
        } catch (AbandonedMutexException) {
            // Treat abandoned ownership as acquired so the next test can proceed.
        }

        ownsSerialExecutionMutex = true;
    }

    void ReleaseSerialExecutionLock() {
        if (!ownsSerialExecutionMutex) {
            serialExecutionMutex?.Dispose();
            serialExecutionMutex = null;
            return;
        }

        try {
            serialExecutionMutex?.ReleaseMutex();
        } catch (ApplicationException) {
            // Best effort cleanup if the mutex is no longer owned.
        } finally {
            ownsSerialExecutionMutex = false;
            serialExecutionMutex?.Dispose();
            serialExecutionMutex = null;
        }
    }

    public void WaitForIdle(TimeSpan? timeout = null) {
        EnsureLaunched();

        var start = DateTime.UtcNow;
        var settleDelay = timeout ?? TimeSpan.FromMilliseconds(220);
        while (DateTime.UtcNow - start < settleDelay) {
            if (appProcess is { HasExited: true }) {
                throw new InvalidOperationException(WithDiagnostics("Avalonia process exited unexpectedly."));
            }

            var remaining = settleDelay - (DateTime.UtcNow - start);
            var sleepMilliseconds = (int)Math.Max(10, Math.Min(40, remaining.TotalMilliseconds));
            Thread.Sleep(sleepMilliseconds);
        }

        if (appProcess is { HasExited: true }) {
            throw new InvalidOperationException(WithDiagnostics("Avalonia process exited unexpectedly."));
        }

        WriteDriverLog($"WaitForIdle completed in {(DateTime.UtcNow - start).TotalMilliseconds:0}ms");
    }

    public void ClickButton(string id) {
        ClickElement(GetElement(id));
        WaitForIdle();
    }

    public void ClickWithinElement(string id, double xRatio, double yRatio) {
        var element = GetElement(id);
        var interactionElement = ResolvePointInteractionElement(element);
        PrepareWindowForPointInput(interactionElement);
        TrySetFocus(interactionElement);
        var point = ResolvePointInElement(interactionElement, xRatio, yRatio);
        WriteDriverLog($"ClickWithinElement id='{id}' interactionId='{TryGetCurrentAutomationId(interactionElement)}' rawBounds={DescribeBounds(interactionElement.Current.BoundingRectangle)} visibleBounds={DescribeBounds(ResolveVisibleBounds(interactionElement))} point=({point.x},{point.y}) hit={DescribeElementAtScreenPoint(point.x, point.y)}");

        SetCursorPos(point.x + 8, point.y + 8);
        Thread.Sleep(20);
        SetCursorPos(point.x, point.y);
        Thread.Sleep(20);
        SendMouseButton(MouseEventFLeftDown);
        Thread.Sleep(20);
        SendMouseButton(MouseEventFLeftUp);
        Thread.Sleep(20);
        SetCursorPos(point.x + 1, point.y);
        Thread.Sleep(20);
        SetCursorPos(point.x, point.y);
    }

    public void RightClickWithinElement(string id, double xRatio, double yRatio) {
        var element = GetElement(id);
        var interactionElement = ResolvePointInteractionElement(element);
        PrepareWindowForPointInput(interactionElement);
        TrySetFocus(interactionElement);
        var point = ResolvePointInElement(interactionElement, xRatio, yRatio);
        WriteDriverLog($"RightClickWithinElement id='{id}' interactionId='{TryGetCurrentAutomationId(interactionElement)}' rawBounds={DescribeBounds(interactionElement.Current.BoundingRectangle)} visibleBounds={DescribeBounds(ResolveVisibleBounds(interactionElement))} point=({point.x},{point.y}) hit={DescribeElementAtScreenPoint(point.x, point.y)}");

        SetCursorPos(point.x + 8, point.y + 8);
        Thread.Sleep(20);
        SetCursorPos(point.x, point.y);
        Thread.Sleep(20);
        SendMouseButton(MouseEventFRightDown);
        Thread.Sleep(20);
        SendMouseButton(MouseEventFRightUp);
        Thread.Sleep(20);
        SetCursorPos(point.x + 1, point.y);
        Thread.Sleep(20);
        SetCursorPos(point.x, point.y);
    }

    public void MoveMouseWithinElement(string id, double xRatio, double yRatio) {
        var element = GetElement(id);
        var interactionElement = ResolvePointInteractionElement(element);
        PrepareWindowForPointInput(interactionElement);
        TrySetFocus(interactionElement);
        var point = ResolvePointInElement(interactionElement, xRatio, yRatio);
        WriteDriverLog($"MoveMouseWithinElement id='{id}' interactionId='{TryGetCurrentAutomationId(interactionElement)}' rawBounds={DescribeBounds(interactionElement.Current.BoundingRectangle)} visibleBounds={DescribeBounds(ResolveVisibleBounds(interactionElement))} point=({point.x},{point.y}) hit={DescribeElementAtScreenPoint(point.x, point.y)}");
        SetCursorPos(point.x + 8, point.y + 8);
        Thread.Sleep(20);
        SetCursorPos(point.x, point.y);
        Thread.Sleep(40);
    }

    public void MouseWheelWithinElement(string id, double xRatio, double yRatio, int delta, bool holdControl = false) {
        var element = GetElement(id);
        var interactionElement = ResolvePointInteractionElement(element);
        PrepareWindowForPointInput(interactionElement);
        TrySetFocus(interactionElement);
        var point = ResolvePointInElement(interactionElement, xRatio, yRatio);
        WriteDriverLog($"MouseWheelWithinElement id='{id}' interactionId='{TryGetCurrentAutomationId(interactionElement)}' rawBounds={DescribeBounds(interactionElement.Current.BoundingRectangle)} visibleBounds={DescribeBounds(ResolveVisibleBounds(interactionElement))} point=({point.x},{point.y}) delta={delta} ctrl={holdControl} hit={DescribeElementAtScreenPoint(point.x, point.y)}");
        SetCursorPos(point.x + 8, point.y + 8);
        Thread.Sleep(20);
        SetCursorPos(point.x, point.y);
        Thread.Sleep(25);

        if (holdControl) {
            keybd_event(VirtualKeyControl, 0, 0, UIntPtr.Zero);
        }

        SendMouseWheel(delta);
        Thread.Sleep(25);

        if (holdControl) {
            keybd_event(VirtualKeyControl, 0, KeyEventFKeyUp, UIntPtr.Zero);
        }

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
        var startedAt = DateTime.UtcNow;
        var element = GetElementOrNull(id);
        var isEnabled = element != null && IsElementEnabled(element);
        if (id is "sliderSongTempo" or "sliderSongProgress" or "btnChangeDifficulty0" or "btnPlayPreview") {
            WriteDriverLog($"IsEnabled id='{id}' result={isEnabled} elapsedMs={(DateTime.UtcNow - startedAt).TotalMilliseconds:0}");
        }
        return isEnabled;
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
        DismissOpenMenus();

        var directAutomationId = ResolveMenuPathAutomationId(path);
        var directMenuItem = string.IsNullOrWhiteSpace(directAutomationId)
            ? null
            : FindElementByAutomationId(directAutomationId);
        if (directMenuItem != null) {
            ClickElement(directMenuItem);
            WaitForIdle();
            DismissOpenMenus();
            return;
        }

        for (var i = 0; i < segments.Length; i++) {
            var segment = segments[i];
            var isLast = i == segments.Length - 1;
            var menuItem = WaitForElement(
                () => FindMenuItem(segment, currentScope),
                defaultTimeout,
                $"Menu item '{segment}'");

            if (isLast) {
                ClickElement(menuItem);
            } else {
                ClickElement(menuItem);
                if (menuItem.TryGetCurrentPattern(ExpandCollapsePattern.Pattern, out var expandObj) &&
                    ((ExpandCollapsePattern)expandObj).Current.ExpandCollapseState == ExpandCollapseState.Collapsed) {
                    ((ExpandCollapsePattern)expandObj).Expand();
                }
            }

            currentScope = menuItem;
            Thread.Sleep(60);
        }

        WaitForIdle();
        DismissOpenMenus();
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

        ClickElement(element);
        WaitForIdle();
        if (WaitUntil(() => IsChecked(id) == isChecked, TimeSpan.FromSeconds(1))) {
            return;
        }

        if (element.TryGetCurrentPattern(TogglePattern.Pattern, out var toggleObj)) {
            ((TogglePattern)toggleObj).Toggle();
            WaitForIdle();
            if (WaitUntil(() => IsChecked(id) == isChecked, TimeSpan.FromSeconds(1))) {
                return;
            }
        }

        SendKeyboardShortcutCore("Space");
        WaitForIdle();
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
        SetText(element, value);
    }

    void SetText(AutomationElement element, string value) {
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
                var name = TryGetCurrentName(item);
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
                return TryGetCurrentName(selected[0]);
            }
        }

        var text = element.FindFirst(
            TreeScope.Descendants,
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Text));
        var textName = text != null ? TryGetCurrentName(text) : string.Empty;
        if (!string.IsNullOrWhiteSpace(textName)) {
            return textName;
        }

        return TryGetCurrentName(element);
    }

    public void SendKeyboardShortcut(string shortcut) {
        EnsureLaunched();
        if (ShortcutTriggersMainWindowReplacement(shortcut)) {
            MarkMainWindowReplacementExpected();
        }
        FocusAppWindow();
        SendKeyboardShortcutCore(shortcut);
    }

    public void SendKeyboardShortcutToWindow(string shortcut, string windowTitle) {
        EnsureLaunched();
        var window = WaitForElement(
            () => FindWindowByTitle(windowTitle),
            defaultTimeout,
            $"Window titled '{windowTitle}'");

        FocusWindow(window);
        SendKeyboardShortcutCore(shortcut);
    }

    public void SendKeyboardShortcutToElement(string id, string shortcut) {
        var element = GetElement(id);
        var keyboardTarget = GetKeyboardTarget(element, id) ?? element;
        FocusElementWindow(keyboardTarget);
        TrySetFocus(keyboardTarget);
        SendKeyboardShortcutCore(shortcut);
    }

    public int CountWindowsByTitle(string title) {
        EnsureLaunched();
        if (string.IsNullOrWhiteSpace(title)) {
            return 0;
        }

        return GetProcessWindows().Count(window =>
            string.Equals(TryGetCurrentName(window), title, StringComparison.OrdinalIgnoreCase));
    }

    public int GetDataGridRowCount(string dataGridId) {
        var dataGrid = GetElement(dataGridId);
        var rowIds = dataGrid.FindAll(TreeScope.Descendants, Condition.TrueCondition)
            .Cast<AutomationElement>()
            .Select(element => element.Current.AutomationId ?? string.Empty)
            .Where(automationId =>
                automationId.StartsWith($"{dataGridId}_Row", StringComparison.Ordinal) &&
                automationId.EndsWith("_Select", StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (rowIds.Count > 0) {
            return rowIds.Count;
        }

        return dataGrid.FindAll(TreeScope.Descendants, Condition.TrueCondition)
            .Cast<AutomationElement>()
            .Select(element => element.Current.AutomationId ?? string.Empty)
            .Where(automationId => automationId.StartsWith($"{dataGridId}_Cell", StringComparison.Ordinal))
            .Select(automationId => {
                var suffix = automationId[(dataGridId.Length + "_Cell".Length)..];
                var separatorIndex = suffix.IndexOf('_');
                return separatorIndex >= 0 ? suffix[..separatorIndex] : suffix;
            })
            .Distinct(StringComparer.Ordinal)
            .Count();
    }

    public void SelectDataGridRow(string dataGridId, int rowIndex) {
        ClickButton($"{dataGridId}_Row{rowIndex}_Select");
    }

    public string GetDataGridCellText(string dataGridId, int rowIndex, int columnIndex) {
        var cell = GetDataGridCellElement(dataGridId, rowIndex, columnIndex);
        return ReadElementText(cell);
    }

    public void SetDataGridCellText(string dataGridId, int rowIndex, int columnIndex, string value) {
        var cell = GetDataGridCellElement(dataGridId, rowIndex, columnIndex);
        SetText(cell, value);
        FocusElementWindow(cell);
        TrySetFocus(cell);
        SendKeyboardShortcutCore("Enter");
        WaitForIdle();
    }

    public bool TryAddDataGridNewItemRow(string dataGridId, params string[] cellValues) {
        var addButton = GetElementOrNull($"{dataGridId}_Add");
        if (addButton == null) {
            return false;
        }

        var initialRowCount = GetDataGridRowCount(dataGridId);
        ClickElement(addButton);
        WaitForIdle();

        var createdRowCount = GetDataGridRowCount(dataGridId);
        if (createdRowCount <= initialRowCount) {
            return false;
        }

        var rowIndex = createdRowCount - 1;
        for (var columnIndex = 0; columnIndex < cellValues.Length; columnIndex++) {
            SetDataGridCellText(dataGridId, rowIndex, columnIndex, cellValues[columnIndex]);
        }

        return true;
    }

    AutomationElement GetDataGridCellElement(string dataGridId, int rowIndex, int columnIndex) {
        try {
            return WaitForElement(
                () => FindDataGridCellElement(dataGridId, rowIndex, columnIndex),
                defaultTimeout,
                $"DataGrid cell '{dataGridId}[{rowIndex},{columnIndex}]'");
        } catch (TimeoutException ex) {
            throw new TimeoutException(WithDiagnostics(ex.Message, includeDebugLog: true));
        }
    }

    AutomationElement? FindDataGridCellElement(string dataGridId, int rowIndex, int columnIndex) {
        var exactId = $"{dataGridId}_Cell{rowIndex}_{columnIndex}";
        var exactMatch = GetElementOrNull(exactId);
        if (exactMatch != null) {
            return exactMatch;
        }

        if (!string.Equals(dataGridId, "dataBPMChange", StringComparison.Ordinal)) {
            return null;
        }

        var scope = FindWindowByTitle("Change BPM") ?? FindWindowByTitle("Timing Settings");
        if (scope == null) {
            return null;
        }

        try {
            var orderedEdits = scope.FindAll(
                    TreeScope.Descendants,
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit))
                .Cast<AutomationElement>()
                .Select(element => new {
                    Element = element,
                    Bounds = element.Current.BoundingRectangle
                })
                .Where(entry => entry.Bounds.Width > 1 && entry.Bounds.Height > 1)
                .OrderBy(entry => entry.Bounds.Top)
                .ThenBy(entry => entry.Bounds.Left)
                .ToList();

            if (orderedEdits.Count == 0) {
                return null;
            }

            var rows = new List<List<(AutomationElement element, System.Windows.Rect bounds)>>();
            foreach (var entry in orderedEdits) {
                if (rows.Count == 0 || Math.Abs(rows[^1][0].bounds.Top - entry.Bounds.Top) > 4) {
                    rows.Add([]);
                }

                rows[^1].Add((entry.Element, entry.Bounds));
            }

            if (rowIndex < 0 || rowIndex >= rows.Count) {
                return null;
            }

            var row = rows[rowIndex]
                .OrderBy(entry => entry.bounds.Left)
                .Select(entry => entry.element)
                .ToList();
            if (columnIndex < 0 || columnIndex >= row.Count) {
                return null;
            }

            return row[columnIndex];
        } catch (COMException) {
            return null;
        } catch (ElementNotAvailableException) {
            return null;
        }
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

            lastKnownMainWindowHandle = TryGetNativeWindowHandle(window);
            pendingMainWindowReplacementSourceHandle = null;
            elementCache.Clear();
            PrimeElementCache(
                window,
                "btnSongPlayer",
                "sliderSongProgress",
                "sliderSongTempo",
                "btnPlayPreview",
                "btnAddDifficulty",
                "btnChangeDifficulty0");
        } catch (Exception ex) when (ex is TimeoutException or InvalidOperationException) {
            throw new TimeoutException(WithDiagnostics(ex.Message, includeDebugLog: true));
        }
    }

    public void WaitForStartWindow(TimeSpan? timeout = null) {
        try {
            _ = WaitForElement(FindStartWindow, timeout ?? defaultTimeout, "start window");
            elementCache.Clear();
        } catch (Exception ex) when (ex is TimeoutException or InvalidOperationException) {
            throw new TimeoutException(WithDiagnostics(ex.Message));
        }
    }

    public void WaitForSettingsWindow(TimeSpan? timeout = null) {
        try {
            _ = WaitForElement(FindSettingsWindow, timeout ?? defaultTimeout, "settings window");
            elementCache.Clear();
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

    public IReadOnlyList<(double left, double top, double width, double height)> GetVisibleDescendantBoundsWithin(string containerId, ControlType controlType) {
        var container = GetElement(containerId);
        var matches = container.FindAll(
            TreeScope.Descendants,
            new PropertyCondition(AutomationElement.ControlTypeProperty, controlType));

        return matches
            .Cast<AutomationElement>()
            .Where(element => !element.Current.IsOffscreen && element.Current.BoundingRectangle.Width > 1 && element.Current.BoundingRectangle.Height > 1)
            .Select(element => {
                var bounds = element.Current.BoundingRectangle;
                return (bounds.Left, bounds.Top, bounds.Width, bounds.Height);
            })
            .ToList();
    }

    public Bitmap CaptureElementBitmap(string id) {
        var element = GetElement(id);
        return CaptureScreenRegion(ResolveVisibleBounds(element));
    }

    public string GetItemStatus(string id) {
        var element = GetElement(id);
        return element.Current.ItemStatus ?? string.Empty;
    }

    public (double left, double top, double width, double height) GetNamedElementBoundsWithin(string containerId, string elementName) {
        var container = GetElement(containerId);
        var element = WaitForElement(
            () => FindNamedDescendant(container, elementName),
            defaultTimeout,
            $"Element named '{elementName}' inside '{containerId}'");
        var bounds = ResolveVisibleBounds(element);
        return (bounds.Left, bounds.Top, bounds.Width, bounds.Height);
    }

    public string GetNamedElementItemStatusWithin(string containerId, string elementName) {
        var container = GetElement(containerId);
        var element = WaitForElement(
            () => FindNamedDescendant(container, elementName),
            defaultTimeout,
            $"Element named '{elementName}' inside '{containerId}'");
        return element.Current.ItemStatus ?? string.Empty;
    }

    public Bitmap CaptureNamedElementBitmapWithin(string containerId, string elementName) {
        var container = GetElement(containerId);
        var element = WaitForElement(
            () => FindNamedDescendant(container, elementName),
            defaultTimeout,
            $"Element named '{elementName}' inside '{containerId}'");
        return CaptureScreenRegion(ResolveVisibleBounds(element));
    }

    public void ResizeMainWindow(int width, int height) {
        var mainWindow = FindMainWindow() ?? throw new InvalidOperationException("Main window is not available for resize.");
        var nativeHandle = TryGetNativeWindowHandle(mainWindow);
        if (nativeHandle == 0) {
            throw new InvalidOperationException("Main window handle was not available for resize.");
        }

        var bounds = mainWindow.Current.BoundingRectangle;
        if (!MoveWindow(new IntPtr(nativeHandle), (int)bounds.Left, (int)bounds.Top, width, height, true)) {
            throw new InvalidOperationException("Could not resize the main window.");
        }

        elementCache.Clear();
        WaitForIdle(TimeSpan.FromMilliseconds(450));
    }

    public void SetScrollViewerVerticalPercent(string id, double verticalPercent) {
        var element = GetElement(id);
        FocusElementWindow(element);
        TrySetFocus(element);
        if (!element.TryGetCurrentPattern(ScrollPattern.Pattern, out var scrollObj)) {
            throw new InvalidOperationException($"Element '{id}' does not support scroll interaction.");
        }

        var scroll = (ScrollPattern)scrollObj;
        var horizontalPercent = scroll.Current.HorizontallyScrollable
            ? scroll.Current.HorizontalScrollPercent
            : ScrollPattern.NoScroll;
        scroll.SetScrollPercent(horizontalPercent, Math.Clamp(verticalPercent, 0, 100));
        WaitForIdle(TimeSpan.FromMilliseconds(350));
    }

    public void DragWithinElement(string id, double startXRatio, double startYRatio, double endXRatio, double endYRatio) {
        var element = GetElement(id);
        var interactionElement = ResolvePointInteractionElement(element);
        PrepareWindowForPointInput(interactionElement);
        TrySetFocus(interactionElement);

        var start = ResolvePointInElement(interactionElement, startXRatio, startYRatio);
        var end = ResolvePointInElement(interactionElement, endXRatio, endYRatio);
        WriteDriverLog($"DragWithinElement id='{id}' interactionId='{TryGetCurrentAutomationId(interactionElement)}' rawBounds={DescribeBounds(interactionElement.Current.BoundingRectangle)} visibleBounds={DescribeBounds(ResolveVisibleBounds(interactionElement))} start=({start.x},{start.y}) startHit={DescribeElementAtScreenPoint(start.x, start.y)} end=({end.x},{end.y}) endHit={DescribeElementAtScreenPoint(end.x, end.y)}");

        SetCursorPos(start.x + 8, start.y + 8);
        Thread.Sleep(20);
        SetCursorPos(start.x, start.y);
        Thread.Sleep(20);
        SendMouseButton(MouseEventFLeftDown);
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
        SendMouseButton(MouseEventFLeftUp);
        Thread.Sleep(20);
        SetCursorPos(end.x + 1, end.y);
        Thread.Sleep(20);
        SetCursorPos(end.x, end.y);
        WaitForIdle();
    }

    public void DragElementByOffset(string id, double startXRatio, double startYRatio, int deltaX, int deltaY) {
        var element = GetElement(id);
        var interactionElement = ResolvePointInteractionElement(element);
        PrepareWindowForPointInput(interactionElement);
        TrySetFocus(interactionElement);

        var start = ResolvePointInElement(interactionElement, startXRatio, startYRatio);
        var end = (x: start.x + deltaX, y: start.y + deltaY);
        WriteDriverLog($"DragElementByOffset id='{id}' interactionId='{TryGetCurrentAutomationId(interactionElement)}' start=({start.x},{start.y}) end=({end.x},{end.y})");

        SetCursorPos(start.x + 8, start.y + 8);
        Thread.Sleep(20);
        SetCursorPos(start.x, start.y);
        Thread.Sleep(20);
        SendMouseButton(MouseEventFLeftDown);
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
        SendMouseButton(MouseEventFLeftUp);
        Thread.Sleep(20);
        SetCursorPos(end.x, end.y);
        WaitForIdle();
    }

    public void DragNamedElementWithin(string containerId, string elementName, double targetXRatio, double targetYRatio) {
        var container = GetElement(containerId);
        var namedElement = WaitForElement(
            () => FindNamedDescendant(container, elementName),
            defaultTimeout,
            $"Element named '{elementName}' inside '{containerId}'");

        var interactionContainer = ResolvePointInteractionElement(container);
        PrepareWindowForPointInput(interactionContainer);
        TrySetFocus(interactionContainer);
        var sourceBounds = ResolveVisibleBounds(namedElement);
        var containerBounds = ResolveVisibleBounds(interactionContainer);
        var clippedSourceBounds = System.Windows.Rect.Intersect(sourceBounds, containerBounds);
        if (clippedSourceBounds.Width > 1 && clippedSourceBounds.Height > 1) {
            sourceBounds = clippedSourceBounds;
        }
        var sourceX = (int)(sourceBounds.Left + sourceBounds.Width / 2);
        var sourceY = (int)(sourceBounds.Top + sourceBounds.Height / 2);
        var target = ResolvePointInElement(interactionContainer, targetXRatio, targetYRatio);
        WriteDriverLog($"DragNamedElementWithin container='{containerId}' sourceName='{elementName}' containerRawBounds={DescribeBounds(interactionContainer.Current.BoundingRectangle)} containerVisibleBounds={DescribeBounds(containerBounds)} sourceBounds={DescribeBounds(sourceBounds)} source=({sourceX},{sourceY}) sourceHit={DescribeElementAtScreenPoint(sourceX, sourceY)} target=({target.x},{target.y}) targetHit={DescribeElementAtScreenPoint(target.x, target.y)}");

        SetCursorPos(sourceX + 8, sourceY + 8);
        Thread.Sleep(20);
        SetCursorPos(sourceX, sourceY);
        Thread.Sleep(20);
        SendMouseButton(MouseEventFLeftDown);
        Thread.Sleep(40);

        const int stepCount = 12;
        for (var step = 1; step <= stepCount; step++) {
            var progress = step / (double)stepCount;
            var x = (int)Math.Round(sourceX + ((target.x - sourceX) * progress));
            var y = (int)Math.Round(sourceY + ((target.y - sourceY) * progress));
            SetCursorPos(x, y);
            Thread.Sleep(25);
        }

        Thread.Sleep(40);
        SendMouseButton(MouseEventFLeftUp);
        Thread.Sleep(20);
        SetCursorPos(target.x + 1, target.y);
        Thread.Sleep(20);
        SetCursorPos(target.x, target.y);
        WaitForIdle();
    }

    AutomationElement GetElement(string id) {
        return WaitForElement(() => GetElementOrNull(id), defaultTimeout, $"Element '{id}'");
    }

    AutomationElement? GetElementOrNull(string id) {
        EnsureLaunched();

        if (ShouldCacheElementId(id) &&
            elementCache.TryGetValue(id, out var cachedElement) &&
            IsCachedElementUsable(cachedElement, id)) {
            return cachedElement;
        }

        var directMatch = id switch {
            StartWindowId => FindStartWindow(),
            MainWindowId => FindMainWindow(),
            SettingsWindowId => FindSettingsWindow(),
            _ => FindElementByAutomationId(id)
        };
        if (directMatch != null) {
            if (ShouldCacheElementId(id)) {
                elementCache[id] = directMatch;
            }
            return directMatch;
        }

        return FindCompositeElementContainer(id);
    }

    static AutomationElement? GetKeyboardTarget(AutomationElement element, string automationId) {
        return FindByAutomationIdWithin(element, $"{automationId}_KeyboardTarget");
    }

    AutomationElement? FindCompositeElementContainer(string automationId) {
        if (string.Equals(automationId, "dataBPMChange", StringComparison.Ordinal)) {
            var changeBpmWindow = FindWindowByTitle("Change BPM") ?? FindWindowByTitle("Timing Settings");
            if (changeBpmWindow != null) {
                return changeBpmWindow;
            }
        }

        foreach (var window in GetProcessWindowsInSearchOrder()) {
            if (FindByAutomationIdWithin(window, $"{automationId}_KeyboardTarget") != null) {
                return window;
            }

            try {
                var descendants = window.FindAll(TreeScope.Descendants, Condition.TrueCondition);
                foreach (AutomationElement descendant in descendants) {
                    var id = TryGetCurrentAutomationId(descendant);
                    if (id.StartsWith($"{automationId}_Row", StringComparison.Ordinal) ||
                        id.StartsWith($"{automationId}_Cell", StringComparison.Ordinal)) {
                        return window;
                    }
                }
            } catch (COMException) {
                // The window disappeared while we were enumerating it.
            } catch (ElementNotAvailableException) {
                // The window disappeared while we were enumerating it.
            }
        }

        return null;
    }

    AutomationElement? FindStartWindow() {
        foreach (var window in GetProcessWindowsInSearchOrder()) {
            if (string.Equals(TryGetCurrentAutomationId(window), StartWindowId, StringComparison.Ordinal)) {
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
            var preferredWindow = candidates.FirstOrDefault(window => TryGetNativeWindowHandle(window) == preferred);
            if (preferredWindow != null && TryGetNativeWindowHandle(preferredWindow) != disallowedHandle) {
                return preferredWindow;
            }
        }

        return candidates.FirstOrDefault(window => TryGetNativeWindowHandle(window) != disallowedHandle);
    }

    AutomationElement? FindSettingsWindow() {
        foreach (var window in GetProcessWindowsInSearchOrder()) {
            if (string.Equals(TryGetCurrentAutomationId(window), SettingsWindowId, StringComparison.Ordinal)) {
                return window;
            }

            if (string.Equals(TryGetCurrentName(window), "Settings", StringComparison.OrdinalIgnoreCase)) {
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
        try {
            if (string.Equals(TryGetCurrentAutomationId(scope), automationId, StringComparison.Ordinal)) {
                return scope;
            }

            var condition = new PropertyCondition(AutomationElement.AutomationIdProperty, automationId);
            return scope.FindFirst(TreeScope.Descendants, condition);
        } catch (COMException) {
            return null;
        } catch (ElementNotAvailableException) {
            return null;
        }
    }

    AutomationElement? FindElementByName(string name, ControlType controlType) {
        foreach (var window in GetProcessWindowsInSearchOrder()) {
            var condition = new AndCondition(
                new PropertyCondition(AutomationElement.ControlTypeProperty, controlType),
                new PropertyCondition(AutomationElement.NameProperty, name));
            try {
                var element = window.FindFirst(TreeScope.Descendants, condition);
                if (element != null) {
                    return element;
                }
            } catch (COMException) {
                // The window disappeared while we were enumerating it.
            } catch (ElementNotAvailableException) {
                // The window disappeared while we were enumerating it.
            }
        }

        return null;
    }

    AutomationElement? FindWindowByTitle(string title) {
        return GetProcessWindowsInSearchOrder().FirstOrDefault(window =>
            string.Equals(TryGetCurrentName(window), title, StringComparison.OrdinalIgnoreCase));
    }

    AutomationElement? FindMenuItem(string menuLabel, AutomationElement scope) {
        var target = NormalizeUiLabel(menuLabel);
        var allMenuItems = scope.FindAll(
            TreeScope.Descendants,
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.MenuItem));

        foreach (AutomationElement item in allMenuItems) {
            if (NormalizeUiLabel(TryGetCurrentName(item)) == target) {
                return item;
            }
        }

        foreach (var window in GetProcessWindows()) {
            var items = window.FindAll(
                TreeScope.Descendants,
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.MenuItem));
            foreach (AutomationElement item in items) {
                if (NormalizeUiLabel(TryGetCurrentName(item)) == target) {
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

            ClickElement(menuItem);
            if (menuItem.TryGetCurrentPattern(ExpandCollapsePattern.Pattern, out var expandObj) &&
                ((ExpandCollapsePattern)expandObj).Current.ExpandCollapseState == ExpandCollapseState.Collapsed) {
                ((ExpandCollapsePattern)expandObj).Expand();
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
                if (TryGetCurrentName(descendant).Contains(text, StringComparison.OrdinalIgnoreCase)) {
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
        if (TryGetCurrentName(list).Contains(text, StringComparison.OrdinalIgnoreCase)) {
            return list;
        }

        var descendants = list.FindAll(TreeScope.Descendants, Condition.TrueCondition);
        foreach (AutomationElement descendant in descendants) {
            var name = TryGetCurrentName(descendant);
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

    static AutomationElement? FindNamedDescendant(AutomationElement container, string elementName) {
        try {
            if (string.Equals(TryGetCurrentName(container), elementName, StringComparison.OrdinalIgnoreCase)) {
                return container;
            }

            var descendants = container.FindAll(TreeScope.Descendants, Condition.TrueCondition);
            foreach (AutomationElement descendant in descendants) {
                if (string.Equals(TryGetCurrentName(descendant), elementName, StringComparison.OrdinalIgnoreCase)) {
                    return descendant;
                }
            }
        } catch (COMException) {
            return null;
        } catch (ElementNotAvailableException) {
            return null;
        }

        return null;
    }

    static Bitmap CaptureScreenRegion(System.Windows.Rect bounds) {
        var width = Math.Max(1, (int)Math.Ceiling(bounds.Width));
        var height = Math.Max(1, (int)Math.Ceiling(bounds.Height));
        var bitmap = new Bitmap(width, height);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.CopyFromScreen(
            (int)Math.Floor(bounds.Left),
            (int)Math.Floor(bounds.Top),
            0,
            0,
            new Size(width, height));
        return bitmap;
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
            .OrderBy(window => new IntPtr(TryGetNativeWindowHandle(window)) != foregroundHandle)
            .ThenBy(window => TryGetIsOffscreen(window))
            .ThenByDescending(window => TryGetNativeWindowHandle(window));
    }

    IEnumerable<AutomationElement> GetDialogWindowsInSearchOrder() {
        return GetProcessWindowsInSearchOrder()
            .Where(window => {
                var id = TryGetCurrentAutomationId(window);
                var title = TryGetCurrentName(window);
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
        var window = FindOwningWindow(element);
        if (window != null) {
            FocusWindow(window);
        }
    }

    void PrepareWindowForPointInput(AutomationElement element) {
        var window = FindOwningWindow(element);
        if (window == null) {
            FocusElementWindow(element);
            return;
        }

        ActivateWindowForPointInput(window);
    }

    static AutomationElement? FindOwningWindow(AutomationElement element) {
        var current = element;
        while (current != null && !Equals(TryGetCurrentControlType(current), ControlType.Window)) {
            current = SafeGetParent(current);
        }

        return current;
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

    static void ActivateWindowForPointInput(AutomationElement window) {
        FocusWindow(window);

        try {
            var bounds = window.Current.BoundingRectangle;
            if (bounds.Width <= 60 || bounds.Height <= 20) {
                return;
            }

            var x = (int)(bounds.Left + Math.Min(120, Math.Max(40, bounds.Width * 0.25)));
            var y = (int)(bounds.Top + Math.Min(24, Math.Max(8, bounds.Height * 0.05)));
            SetCursorPos(x, y);
            Thread.Sleep(20);
            SendMouseButton(MouseEventFLeftDown);
            Thread.Sleep(20);
            SendMouseButton(MouseEventFLeftUp);
            Thread.Sleep(40);
        } catch {
            // Best effort activation for physical pointer input.
        }
    }

    AutomationElement ResolvePointInteractionElement(AutomationElement element) {
        try {
            if (string.Equals(TryGetCurrentAutomationId(element), "scrollEditor", StringComparison.Ordinal)) {
                var inputLayer = FindByAutomationIdWithin(element, "scrollEditorInputLayer");
                if (inputLayer != null) {
                    return inputLayer;
                }
            }
        } catch {
            // Fall back to the original element when the descendant lookup churns.
        }

        return element;
    }

    static void PhysicalClickElement(AutomationElement element) {
        TrySetFocus(element);
        var bounds = element.Current.BoundingRectangle;
        var x = (int)(bounds.Left + bounds.Width / 2);
        var y = (int)(bounds.Top + bounds.Height / 2);
        SetCursorPos(x, y);
        Thread.Sleep(20);
        SendMouseButton(MouseEventFLeftDown);
        Thread.Sleep(20);
        SendMouseButton(MouseEventFLeftUp);
    }

    static void SendMouseButton(uint flags) {
        SendMouseInput(flags, 0);
    }

    static void SendMouseWheel(int delta) {
        SendMouseInput(MouseEventFWheel, delta);
    }

    static void SendMouseInput(uint flags, int mouseData) {
        var inputs = new[] {
            new Input {
                type = InputMouse,
                U = new InputUnion {
                    mi = new MouseInput {
                        dwFlags = flags,
                        mouseData = mouseData
                    }
                }
            }
        };

        _ = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>());
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
        var textName = text != null ? TryGetCurrentName(text) : string.Empty;
        if (!string.IsNullOrWhiteSpace(textName)) {
            return textName;
        }

        return TryGetCurrentName(element);
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
        return $"{message}{Environment.NewLine}{GetProcessDebugSummary()}{Environment.NewLine}{GetWindowDebugSummary()}{Environment.NewLine}{GetExceptionLogSummary()}{Environment.NewLine}{GetStandardErrorLogSummary()}";
    }

    string WithDiagnostics(string message, bool includeDebugLog) {
        if (!includeDebugLog) {
            return WithDiagnostics(message);
        }

        return $"{message}{Environment.NewLine}{GetProcessDebugSummary()}{Environment.NewLine}{GetWindowDebugSummary()}{Environment.NewLine}{GetExceptionLogSummary()}{Environment.NewLine}{GetStandardOutputLogSummary()}{Environment.NewLine}{GetStandardErrorLogSummary()}{Environment.NewLine}{GetDebugLogSummary()}{Environment.NewLine}{GetDriverLogSummary()}";
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

    public string GetWindowDebugSummary() {
        var windows = GetProcessWindows()
            .Select(window => {
                var title = TryGetCurrentName(window);
                var automationId = TryGetCurrentAutomationId(window);
                var handle = TryGetNativeWindowHandle(window);
                var offscreen = TryGetIsOffscreen(window);
                return $"Window(title='{title}', id='{automationId}', hwnd={handle}, offscreen={offscreen}, descendants=[{DescribeWindowDescendants(window)}])";
            });

        return $"Visible Avalonia windows: {string.Join(" | ", windows)}";
    }

    public string GetTestLog() {
        var exceptionSummary = GetExceptionLogSummary();
        var debugSummary = GetDebugLogSummary();
        return $"{exceptionSummary}{Environment.NewLine}{debugSummary}".Trim();
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

    string GetDriverLogSummary() {
        if (string.IsNullOrWhiteSpace(driverLogFilePath) || !File.Exists(driverLogFilePath)) {
            return "Driver log: empty";
        }

        try {
            var contents = File.ReadAllText(driverLogFilePath);
            if (string.IsNullOrWhiteSpace(contents)) {
                return "Driver log: empty";
            }

            return $"Driver log:{Environment.NewLine}{contents}";
        } catch (Exception ex) {
            return $"Driver log unavailable: {ex.Message}";
        }
    }

    string GetStandardOutputLogSummary() {
        return GetProcessLogSummary(standardOutputLogFilePath, "Stdout log");
    }

    string GetStandardErrorLogSummary() {
        return GetProcessLogSummary(standardErrorLogFilePath, "Stderr log");
    }

    static string GetProcessLogSummary(string? filePath, string label) {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)) {
            return $"{label}: empty";
        }

        try {
            var contents = File.ReadAllText(filePath);
            if (string.IsNullOrWhiteSpace(contents)) {
                return $"{label}: empty";
            }

            return $"{label}:{Environment.NewLine}{contents}";
        } catch (Exception ex) {
            return $"{label} unavailable: {ex.Message}";
        }
    }

    static void AppendProcessLogLine(string? filePath, string? line) {
        if (string.IsNullOrWhiteSpace(filePath) || line == null) {
            return;
        }

        try {
            File.AppendAllText(filePath, $"{line}{Environment.NewLine}");
        } catch {
            // Best effort diagnostics only.
        }
    }

    void WriteDriverLog(string message) {
        if (string.IsNullOrWhiteSpace(driverLogFilePath)) {
            return;
        }

        try {
            File.AppendAllText(driverLogFilePath, $"[{DateTime.UtcNow:O}] {message}{Environment.NewLine}");
        } catch {
            // Best effort diagnostics only.
        }
    }

    static string DescribeElementAtScreenPoint(int x, int y) {
        try {
            var element = AutomationElement.FromPoint(new System.Windows.Point(x, y));
            if (element == null) {
                return "<null>";
            }

            var name = TryGetCurrentName(element);
            var id = TryGetCurrentAutomationId(element);
            var controlType = TryGetCurrentControlType(element)?.ProgrammaticName ?? "unknown";
            return $"name='{name}' id='{id}' type='{controlType}'";
        } catch (ElementNotAvailableException) {
            return "<not-available>";
        } catch (COMException ex) {
            return $"<com:{ex.HResult}>";
        }
    }

    static string DescribeBounds(System.Windows.Rect bounds) {
        return $"({bounds.Left:0.##},{bounds.Top:0.##},{bounds.Width:0.##},{bounds.Height:0.##})";
    }

    static string DescribeWindowDescendants(AutomationElement window) {
        try {
            return string.Join(", ",
                window.FindAll(TreeScope.Descendants, Condition.TrueCondition)
                    .Cast<AutomationElement>()
                    .Select(element => {
                        var id = TryGetCurrentAutomationId(element);
                        var name = TryGetCurrentName(element);
                        var controlType = TryGetCurrentControlType(element)?.ProgrammaticName ?? "unknown";
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

    static string TryGetCurrentAutomationId(AutomationElement element) {
        try {
            return element.Current.AutomationId ?? string.Empty;
        } catch (COMException) {
            return string.Empty;
        } catch (ElementNotAvailableException) {
            return string.Empty;
        }
    }

    static string TryGetCurrentName(AutomationElement element) {
        try {
            return element.Current.Name ?? string.Empty;
        } catch (COMException) {
            return string.Empty;
        } catch (ElementNotAvailableException) {
            return string.Empty;
        }
    }

    static ControlType? TryGetCurrentControlType(AutomationElement element) {
        try {
            return element.Current.ControlType;
        } catch (COMException) {
            return null;
        } catch (ElementNotAvailableException) {
            return null;
        }
    }

    static int TryGetNativeWindowHandle(AutomationElement element) {
        try {
            return element.Current.NativeWindowHandle;
        } catch (COMException) {
            return 0;
        } catch (ElementNotAvailableException) {
            return 0;
        }
    }

    static bool TryGetIsOffscreen(AutomationElement element) {
        try {
            return element.Current.IsOffscreen;
        } catch (COMException) {
            return true;
        } catch (ElementNotAvailableException) {
            return true;
        }
    }

    static bool IsElementEnabled(AutomationElement element) {
        try {
            if (!element.Current.IsEnabled) {
                return false;
            }

            var controlType = TryGetCurrentControlType(element);
            if ((Equals(controlType, ControlType.Button) || Equals(controlType, ControlType.Slider)) &&
                !element.Current.IsKeyboardFocusable) {
                return false;
            }

            if (Equals(controlType, ControlType.Slider) &&
                element.TryGetCurrentPattern(RangeValuePattern.Pattern, out var rangePatternObj) &&
                ((RangeValuePattern)rangePatternObj).Current.IsReadOnly) {
                return false;
            }

            return true;
        } catch (COMException) {
            return false;
        } catch (ElementNotAvailableException) {
            return false;
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

    string CaptureWindowTopologySnapshot() {
        try {
            return string.Join("|",
                GetProcessWindows()
                    .Select(window => $"{TryGetNativeWindowHandle(window)}:{TryGetCurrentAutomationId(window)}:{TryGetCurrentName(window)}:{TryGetIsOffscreen(window)}")
                    .OrderBy(entry => entry, StringComparer.Ordinal));
        } catch {
            return string.Empty;
        }
    }

    static bool IsCachedElementUsable(AutomationElement element, string expectedAutomationId) {
        try {
            return string.Equals(TryGetCurrentAutomationId(element), expectedAutomationId, StringComparison.Ordinal);
        } catch {
            return false;
        }
    }

    static bool ShouldCacheElementId(string automationId) {
        return automationId is
            "btnSongPlayer" or
            "sliderSongProgress" or
            "sliderSongTempo" or
            "btnPlayPreview" or
            "btnAddDifficulty" or
            "btnChangeDifficulty0";
    }

    void PrimeElementCache(AutomationElement scope, params string[] automationIds) {
        foreach (var automationId in automationIds) {
            if (string.IsNullOrWhiteSpace(automationId)) {
                continue;
            }

            var element = FindByAutomationIdWithin(scope, automationId);
            if (element != null) {
                elementCache[automationId] = element;
            }
        }
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

    static string ResolveMenuPathAutomationId(string path) {
        return path.Trim() switch {
            "Edit>Snap Notes to Grid" => "MenuItemSnapToGrid",
            "Tools>Settings" => "MenuItemSettings",
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
            pendingMainWindowReplacementSourceHandle = TryGetNativeWindowHandle(currentMainWindow);
            lastKnownMainWindowHandle = TryGetNativeWindowHandle(currentMainWindow);
        }
        elementCache.Clear();
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

    static void CleanupStaleProcesses(string? processName) {
        if (string.IsNullOrWhiteSpace(processName)) {
            return;
        }

        foreach (var process in Process.GetProcessesByName(processName)) {
            try {
                if (process.HasExited) {
                    continue;
                }

                process.Kill(entireProcessTree: true);
                process.WaitForExit((int)TimeSpan.FromSeconds(5).TotalMilliseconds);
            } catch {
                // Best effort cleanup before launching a new isolated app instance.
            } finally {
                process.Dispose();
            }
        }
    }

    static bool IsMainWindowCandidate(AutomationElement window) {
        if (string.Equals(TryGetCurrentAutomationId(window), MainWindowId, StringComparison.Ordinal)) {
            return true;
        }

        if (string.Equals(TryGetCurrentName(window), MainWindowFallbackTitle, StringComparison.OrdinalIgnoreCase) &&
            MainWindowSentinelIds.Any(id => FindByAutomationIdWithin(window, id) != null)) {
            return true;
        }

        return MainWindowSentinelIds.Any(id => FindByAutomationIdWithin(window, id) != null);
    }

    static AutomationElement WaitForElement(Func<AutomationElement?> finder, TimeSpan timeout, string description) {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline) {
            try {
                var element = finder();
                if (element != null) {
                    return element;
                }
            } catch (COMException) {
                // A window disappeared while we were enumerating the automation tree. Retry.
            } catch (ElementNotAvailableException) {
                // A window disappeared while we were enumerating the automation tree. Retry.
            }

            Thread.Sleep(50);
        }

        throw new TimeoutException($"Timed out waiting for {description}.");
    }

    static bool WaitUntil(Func<bool> condition, TimeSpan timeout) {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline) {
            try {
                if (condition()) {
                    return true;
                }
            } catch (COMException) {
                // A window disappeared while we were enumerating the automation tree. Retry.
            } catch (ElementNotAvailableException) {
                // A window disappeared while we were enumerating the automation tree. Retry.
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
        var bounds = ResolveVisibleBounds(element);
        var x = (int)(bounds.Left + bounds.Width * ClampRatio(xRatio));
        var y = (int)(bounds.Top + bounds.Height * ClampRatio(yRatio));
        return (x, y);
    }

    static System.Windows.Rect ResolveVisibleBounds(AutomationElement element) {
        var bounds = element.Current.BoundingRectangle;
        var current = element;
        while (true) {
            current = SafeGetParent(current);
            if (current == null) {
                break;
            }

            try {
                var parentBounds = current.Current.BoundingRectangle;
                if (parentBounds.Width > 1 && parentBounds.Height > 1) {
                    var intersected = System.Windows.Rect.Intersect(bounds, parentBounds);
                    if (intersected.Width > 1 && intersected.Height > 1) {
                        bounds = intersected;
                    }
                }

                if (Equals(TryGetCurrentControlType(current), ControlType.Window)) {
                    break;
                }
            } catch (ElementNotAvailableException) {
                break;
            } catch (COMException) {
                break;
            }
        }

        return bounds;
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
            case "ctrl+b":
                SendCombo(VirtualKeyControl, 0x42);
                break;
            case "ctrl+t":
                SendCombo(VirtualKeyControl, 0x54);
                break;
            case "ctrl+shift+t":
                SendCombo(VirtualKeyControl, VirtualKeyShift, 0x54);
                break;
            case "ctrl+z":
                SendCombo(VirtualKeyControl, 0x5A);
                break;
            case "ctrl+y":
                SendCombo(VirtualKeyControl, 0x59);
                break;
            case "ctrl+shift+z":
                SendCombo(VirtualKeyControl, VirtualKeyShift, 0x5A);
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
            case "delete":
                SendKey(0x2E);
                break;
            case "1":
                SendKey(0x31);
                break;
            case "2":
                SendKey(0x32);
                break;
            case "3":
                SendKey(0x33);
                break;
            case "4":
                SendKey(0x34);
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

    static void SendCombo(byte firstModifier, byte secondModifier, byte key) {
        var scanCode = (byte)MapVirtualKey(key, 0);
        keybd_event(firstModifier, 0, 0, UIntPtr.Zero);
        Thread.Sleep(10);
        keybd_event(secondModifier, 0, 0, UIntPtr.Zero);
        Thread.Sleep(10);
        keybd_event(0, scanCode, KeyEventFScancode, UIntPtr.Zero);
        Thread.Sleep(10);
        keybd_event(0, scanCode, KeyEventFScancode | KeyEventFKeyUp, UIntPtr.Zero);
        Thread.Sleep(10);
        keybd_event(secondModifier, 0, KeyEventFKeyUp, UIntPtr.Zero);
        Thread.Sleep(10);
        keybd_event(firstModifier, 0, KeyEventFKeyUp, UIntPtr.Zero);
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

    [DllImport("user32.dll", SetLastError = true)]
    static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    static extern bool MoveWindow(IntPtr hWnd, int x, int y, int nWidth, int nHeight, bool bRepaint);

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

    [DllImport("user32.dll", SetLastError = true)]
    static extern uint SendInput(uint nInputs, [In] Input[] pInputs, int cbSize);

    [StructLayout(LayoutKind.Sequential)]
    struct Input {
        public int type;
        public InputUnion U;
    }

    [StructLayout(LayoutKind.Explicit)]
    struct InputUnion {
        [FieldOffset(0)]
        public MouseInput mi;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct MouseInput {
        public int dx;
        public int dy;
        public int mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }
}
