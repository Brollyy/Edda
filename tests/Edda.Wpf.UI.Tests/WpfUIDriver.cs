using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Automation;

namespace Edda.Wpf.UI.Tests {
    /// <summary>
    /// UI automation driver that runs the real WPF application process and interacts
    /// with controls through Windows UI Automation.
    /// </summary>
    public sealed class WpfUIDriver {
        private const string StartWindowId = "StartWindow";
        private const string MainWindowId = "AppMainWindow";
        private const string MainWindowFallbackTitle = "Edda";
        private const string StartWindowOpenMapButtonId = "ButtonOpenMap";
        private static readonly string[] MainWindowSentinelIds = {
            "btnSongPlayer",
            "sliderSongProgress",
            "txtSongName"
        };
        private const string PickerCancelSentinel = "__EDDA_TEST_PICKER_CANCEL__";
        private const string PickerQueueFileEnvironmentVariable = "EDDA_TEST_PICKER_QUEUE_FILE";
        private static readonly string[] PickerDialogTitles = {
            "Select your map's containing folder",
            "Select an empty folder to store your map",
            "Select a folder to export the map to",
            "Select a song to map",
            "Select a simfile to import",
            "Select the folder that Ragnarock is installed in"
        };

        private const uint KeyEventFKeyUp = 0x0002;
        private const uint KeyEventFScancode = 0x0008;
        private const uint MouseEventFLeftDown = 0x0002;
        private const uint MouseEventFLeftUp = 0x0004;
        private const uint MouseEventFRightDown = 0x0008;
        private const uint MouseEventFRightUp = 0x0010;
        private const uint MouseEventFMiddleDown = 0x0020;
        private const uint MouseEventFMiddleUp = 0x0040;
        private const uint MouseEventFWheel = 0x0800;
        private const byte VirtualKeyShift = 0x10;
        private const byte VirtualKeyControl = 0x11;
        private const byte VirtualKeyAlt = 0x12;
        private const int CursorArrowId = 32512;
        private const int CursorHandId = 32649;

        private readonly TimeSpan defaultTimeout = TimeSpan.FromSeconds(15);

        private Process? appProcess;
        private readonly Dictionary<string, string?> launchEnvironmentOverrides = new(StringComparer.OrdinalIgnoreCase);
        private string? pickerSelectionQueueFilePath;
        private string? testProfileRoot;

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
            testProfileRoot = Path.Combine(Path.GetTempPath(), "Edda-WpfUiTests", Guid.NewGuid().ToString("N"));
            var appDataRoot = Path.Combine(testProfileRoot, "AppData", "Roaming");
            var localAppDataRoot = Path.Combine(testProfileRoot, "AppData", "Local");
            pickerSelectionQueueFilePath = Path.Combine(testProfileRoot, "picker-queue.txt");
            Directory.CreateDirectory(appDataRoot);
            Directory.CreateDirectory(localAppDataRoot);
            File.WriteAllText(pickerSelectionQueueFilePath, string.Empty);

            var startInfo = new ProcessStartInfo {
                UseShellExecute = false
            };
            if (appPath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) {
                startInfo.FileName = "dotnet";
                startInfo.Arguments = $"\"{appPath}\"";
            } else {
                startInfo.FileName = appPath;
            }
            startInfo.WorkingDirectory = Path.GetDirectoryName(appPath) ?? Directory.GetCurrentDirectory();
            startInfo.Environment["APPDATA"] = appDataRoot;
            startInfo.Environment["LOCALAPPDATA"] = localAppDataRoot;
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
            _ = WaitForElement(FindStartWindow, defaultTimeout, "Start window");
        }

        public void Shutdown() {
            if (appProcess is { HasExited: false }) {
                try {
                    var mainWindow = FindMainWindow() ?? FindStartWindow();
                    if (mainWindow != null) {
                        var hwnd = new IntPtr(mainWindow.Current.NativeWindowHandle);
                        if (hwnd != IntPtr.Zero) {
                            SetForegroundWindow(hwnd);
                            SendKeyboardShortcut("Alt+F4");
                            if (appProcess.WaitForExit((int)TimeSpan.FromSeconds(3).TotalMilliseconds)) {
                                appProcess.Dispose();
                                appProcess = null;
                            }
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
            launchEnvironmentOverrides.Clear();
            pickerSelectionQueueFilePath = null;

            if (!string.IsNullOrWhiteSpace(testProfileRoot) && Directory.Exists(testProfileRoot)) {
                try {
                    Directory.Delete(testProfileRoot, true);
                } catch {
                    // Best effort cleanup for temporary test profile.
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
                    throw new InvalidOperationException("Edda process exited unexpectedly.");
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

        public bool IsProcessRunning() {
            return appProcess is { HasExited: false };
        }

        public bool WaitForExit(TimeSpan timeout) {
            if (appProcess == null) {
                return true;
            }

            return appProcess.WaitForExit((int)timeout.TotalMilliseconds);
        }

        public void ClickButton(string id) {
            var element = GetElement(id);
            ClickElement(element);
            TryHandlePendingPickerDialogs();
            WaitForIdle();
        }

        public void SetText(string id, string value) {
            var element = GetElement(id);
            FocusElementWindow(element);
            TrySetFocus(element);

            if (element.TryGetCurrentPattern(ValuePattern.Pattern, out var valuePatternObj)) {
                ((ValuePattern)valuePatternObj).SetValue(value);
                return;
            }

            var bounds = element.Current.BoundingRectangle;
            var x = (int)(bounds.Left + bounds.Width / 2);
            var y = (int)(bounds.Top + bounds.Height / 2);
            PerformLeftDoubleClick(x, y);
            Thread.Sleep(40);
            SendCombo(VirtualKeyControl, 0x41); // Ctrl+A
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

        public string GetText(string id) {
            var element = GetElement(id);
            if (element.TryGetCurrentPattern(ValuePattern.Pattern, out var valuePatternObj)) {
                return ((ValuePattern)valuePatternObj).Current.Value ?? string.Empty;
            }

            return element.Current.Name ?? string.Empty;
        }

        public (double left, double top, double width, double height) GetElementBounds(string id) {
            var element = GetElement(id);
            var bounds = element.Current.BoundingRectangle;
            return (bounds.Left, bounds.Top, bounds.Width, bounds.Height);
        }

        public bool IsVisible(string id) {
            var element = GetElementOrNull(id);
            if (element == null) {
                return false;
            }

            var bounds = element.Current.BoundingRectangle;
            return !element.Current.IsOffscreen && bounds.Width > 1 && bounds.Height > 1;
        }

        public bool IsEnabled(string id) {
            var element = GetElementOrNull(id);
            return element != null && element.Current.IsEnabled;
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

        public void ToggleCheckbox(string id, bool value) {
            var element = GetElement(id);
            FocusElementWindow(element);
            TrySetFocus(element);
            if (!element.TryGetCurrentPattern(TogglePattern.Pattern, out var toggleObj)) {
                throw new InvalidOperationException($"Element '{id}' does not support toggle interaction.");
            }

            var toggle = (TogglePattern)toggleObj;
            var current = toggle.Current.ToggleState == ToggleState.On;
            if (current == value) {
                return;
            }

            FocusElementWindow(element);
            TrySetFocus(element);
            SendKey(0x20); // Space
            WaitForIdle();
            if (WaitUntil(() => IsChecked(id) == value, TimeSpan.FromSeconds(1))) {
                return;
            }

            for (var attempt = 0; attempt < 2; attempt++) {
                var bounds = element.Current.BoundingRectangle;
                var x = (int)(bounds.Left + bounds.Width / 2);
                var y = (int)(bounds.Top + bounds.Height / 2);

                SetCursorPos(x, y);
                Thread.Sleep(20);
                mouse_event(MouseEventFLeftDown, 0, 0, 0, UIntPtr.Zero);
                Thread.Sleep(20);
                mouse_event(MouseEventFLeftUp, 0, 0, 0, UIntPtr.Zero);
                WaitForIdle();

                if (WaitUntil(() => IsChecked(id) == value, TimeSpan.FromSeconds(1))) {
                    return;
                }
            }

            toggle.Toggle();
            WaitForIdle();
            if (WaitUntil(() => IsChecked(id) == value, TimeSpan.FromSeconds(1))) {
                return;
            }

            throw new InvalidOperationException($"Checkbox '{id}' did not reach the requested state '{value}'.");
        }

        public string GetSelectedValue(string id) {
            var element = GetElement(id);
            if (!element.TryGetCurrentPattern(SelectionPattern.Pattern, out var selectionObj)) {
                throw new InvalidOperationException($"Element '{id}' does not support selection.");
            }

            var selected = ((SelectionPattern)selectionObj).Current.GetSelection();
            return selected.Length > 0 ? selected[0].Current.Name ?? string.Empty : string.Empty;
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
            Thread.Sleep(80);

            var item = WaitForElement(
                () => FindElementByName(value, ControlType.ListItem) ?? FindElementByName(value, ControlType.MenuItem),
                defaultTimeout,
                $"Dropdown item '{value}'"
            );

            PhysicalClickElement(item);

            TryHandlePendingPickerDialogs();
            WaitForIdle();
        }

        public bool TrySelectDifferentDropdownValue(string id) {
            var combo = GetElement(id);
            var currentValue = GetSelectedValue(id);

            if (combo.TryGetCurrentPattern(ExpandCollapsePattern.Pattern, out var expandObj)) {
                ((ExpandCollapsePattern)expandObj).Expand();
            }
            Thread.Sleep(80);

            var optionNames = new List<string>();
            var localItems = combo.FindAll(
                TreeScope.Descendants,
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ListItem)
            );
            foreach (AutomationElement item in localItems) {
                var name = item.Current.Name ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(name) && !optionNames.Contains(name, StringComparer.Ordinal)) {
                    optionNames.Add(name);
                }
            }

            if (optionNames.Count == 0) {
                foreach (var window in GetProcessWindows()) {
                    var windowItems = window.FindAll(
                        TreeScope.Descendants,
                        new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ListItem)
                    );
                    foreach (AutomationElement item in windowItems) {
                        var name = item.Current.Name ?? string.Empty;
                        if (!string.IsNullOrWhiteSpace(name) && !optionNames.Contains(name, StringComparer.Ordinal)) {
                            optionNames.Add(name);
                        }
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
                $"Dropdown item '{nextValue}'"
            );

            if (itemToSelect.TryGetCurrentPattern(SelectionItemPattern.Pattern, out var selectionObj)) {
                ((SelectionItemPattern)selectionObj).Select();
            } else {
                ClickElement(itemToSelect);
            }

            if (combo.TryGetCurrentPattern(ExpandCollapsePattern.Pattern, out expandObj)) {
                ((ExpandCollapsePattern)expandObj).Collapse();
            }

            TryHandlePendingPickerDialogs();
            WaitForIdle();
            return true;
        }

        public void ClickElementByName(string name) {
            var element = WaitForElement(
                () => FindElementByName(name),
                defaultTimeout,
                $"Element named '{name}'"
            );
            ClickElement(element);
            TryHandlePendingPickerDialogs();
            WaitForIdle();
        }

        public void DoubleClickElementByName(string name) {
            var element = WaitForElement(
                () => FindElementByName(name),
                defaultTimeout,
                $"Element named '{name}'"
            );
            FocusElementWindow(element);
            var bounds = element.Current.BoundingRectangle;
            var x = (int)(bounds.Left + bounds.Width / 2);
            var y = (int)(bounds.Top + bounds.Height / 2);
            PerformLeftDoubleClick(x, y);
            WaitForIdle();
        }

        public void ClickListItemContainingText(string listAutomationId, string text) {
            var list = GetElement(listAutomationId);
            var item = WaitForElement(
                () => FindListItemContainingText(list, text),
                defaultTimeout,
                $"List item containing '{text}' in '{listAutomationId}'"
            );
            FocusElementWindow(item);
            TrySetFocus(item);

            var bounds = item.Current.BoundingRectangle;
            var x = (int)(bounds.Left + bounds.Width / 2);
            var y = (int)(bounds.Top + bounds.Height / 2);

            SetCursorPos(x, y);
            Thread.Sleep(20);
            mouse_event(MouseEventFLeftDown, 0, 0, 0, UIntPtr.Zero);
            Thread.Sleep(20);
            mouse_event(MouseEventFLeftUp, 0, 0, 0, UIntPtr.Zero);
            WaitForIdle();
        }

        public void RightClickElementByName(string name) {
            var element = WaitForElement(
                () => FindElementByName(name),
                defaultTimeout,
                $"Element named '{name}'"
            );

            FocusElementWindow(element);
            var bounds = element.Current.BoundingRectangle;
            var x = (int)(bounds.Left + bounds.Width / 2);
            var y = (int)(bounds.Top + bounds.Height / 2);

            SetCursorPos(x, y);
            Thread.Sleep(20);
            mouse_event(MouseEventFRightDown, 0, 0, 0, UIntPtr.Zero);
            Thread.Sleep(20);
            mouse_event(MouseEventFRightUp, 0, 0, 0, UIntPtr.Zero);
            WaitForIdle();
        }

        public void RightClickListItemContainingText(string listAutomationId, string text) {
            var list = GetElement(listAutomationId);
            var item = WaitForElement(
                () => FindListItemContainingText(list, text),
                defaultTimeout,
                $"List item containing '{text}' in '{listAutomationId}'"
            );

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

        public int CountWindowsByTitle(string title) {
            EnsureLaunched();
            if (string.IsNullOrWhiteSpace(title)) {
                return 0;
            }

            return GetProcessWindows().Count(window =>
                string.Equals(window.Current.Name ?? string.Empty, title, StringComparison.OrdinalIgnoreCase));
        }

        public string GetWindowDebugSummary() {
            EnsureLaunched();

            var windows = GetProcessWindows()
                .Select(window => {
                    var title = window.Current.Name ?? string.Empty;
                    var automationId = window.Current.AutomationId ?? string.Empty;
                    var handle = window.Current.NativeWindowHandle;
                    var offscreen = window.Current.IsOffscreen;
                    return $"Window(title='{title}', id='{automationId}', hwnd={handle}, offscreen={offscreen}, descendants=[{DescribeWindowDescendants(window)}])";
                });

            return string.Join("; ", windows);
        }

        public string GetTestLog() {
            if (appProcess is not { HasExited: true }) {
                return string.Empty;
            }

            try {
                return $"Edda process exited with code {appProcess.ExitCode}.";
            } catch {
                return string.Empty;
            }
        }

        public bool ContainsText(string text) {
            EnsureLaunched();
            return FindTextContaining(text) != null;
        }

        public int GetListItemCount(string listAutomationId) {
            var list = GetElement(listAutomationId);
            var items = list.FindAll(
                TreeScope.Descendants,
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ListItem)
            );
            return items.Count;
        }

        public int GetDataGridRowCount(string dataGridId) {
            var dataGrid = GetElement(dataGridId);
            return GetDataGridRows(dataGrid).Count;
        }

        public void SelectDataGridRow(string dataGridId, int rowIndex) {
            var dataGrid = GetElement(dataGridId);
            var rows = GetDataGridRows(dataGrid);
            if (rowIndex < 0 || rowIndex >= rows.Count) {
                throw new ArgumentOutOfRangeException(nameof(rowIndex), $"DataGrid row index {rowIndex} is out of range.");
            }

            var row = rows[rowIndex];
            FocusElementWindow(row);
            if (row.TryGetCurrentPattern(SelectionItemPattern.Pattern, out var selectionObj)) {
                ((SelectionItemPattern)selectionObj).Select();
            } else {
                ClickElement(row);
            }
            WaitForIdle();
        }

        public string GetDataGridCellText(string dataGridId, int rowIndex, int columnIndex) {
            var cell = GetDataGridCell(dataGridId, rowIndex, columnIndex);
            return ReadElementText(cell);
        }

        public void SetDataGridCellText(string dataGridId, int rowIndex, int columnIndex, string value) {
            var cell = GetDataGridCell(dataGridId, rowIndex, columnIndex);
            FocusElementWindow(cell);
            ClickElement(cell);
            Thread.Sleep(40);

            if (cell.TryGetCurrentPattern(ValuePattern.Pattern, out var valuePatternObj)) {
                ((ValuePattern)valuePatternObj).SetValue(value);
                SendKey(0x0D); // Enter
                WaitForIdle();
                return;
            }

            var bounds = cell.Current.BoundingRectangle;
            var x = (int)(bounds.Left + bounds.Width / 2);
            var y = (int)(bounds.Top + bounds.Height / 2);
            PerformLeftDoubleClick(x, y);
            Thread.Sleep(40);
            SendCombo(VirtualKeyControl, 0x41); // Ctrl+A
            SendText(value);
            SendKey(0x0D); // Enter
            WaitForIdle();
        }

        public bool TryAddDataGridNewItemRow(string dataGridId, params string[] cellValues) {
            var dataGrid = GetElement(dataGridId);
            var initialRowCount = GetDataGridRows(dataGrid).Count;
            FocusElementWindow(dataGrid);

            var bounds = dataGrid.Current.BoundingRectangle;
            var x = (int)(bounds.Left + bounds.Width * 0.25);
            var y = (int)(bounds.Top + bounds.Height * 0.95);
            SetCursorPos(x, y);
            Thread.Sleep(20);
            mouse_event(MouseEventFLeftDown, 0, 0, 0, UIntPtr.Zero);
            Thread.Sleep(20);
            mouse_event(MouseEventFLeftUp, 0, 0, 0, UIntPtr.Zero);
            Thread.Sleep(80);

            foreach (var value in cellValues) {
                SendText(value);
                SendKey(0x09); // Tab
                Thread.Sleep(30);
            }
            SendKey(0x0D); // Enter
            WaitForIdle();

            return WaitUntil(() => GetDataGridRows(dataGrid).Count > initialRowCount, TimeSpan.FromSeconds(2));
        }

        public void SendKeyboardShortcutToWindow(string shortcut, string windowTitle) {
            EnsureLaunched();
            var window = WaitForElement(
                () => FindWindowByTitle(windowTitle),
                defaultTimeout,
                $"Window titled '{windowTitle}'"
            );
            FocusWindow(window);
            SendKeyboardShortcutCore(shortcut);
            TryHandlePendingPickerDialogs();
        }

        public void OpenMenu(string id) {
            ClickButton(id);
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
                $"Menu item '{path}'"
            );

            try {
                if (menuItem.TryGetCurrentPattern(TogglePattern.Pattern, out var toggleObj)) {
                    return ((TogglePattern)toggleObj).Current.ToggleState == ToggleState.On;
                }

                if (menuItem.TryGetCurrentPattern(SelectionItemPattern.Pattern, out var selectionObj)) {
                    return ((SelectionItemPattern)selectionObj).Current.IsSelected;
                }

                return false;
            } finally {
                DismissOpenMenus();
            }
        }

        public void SelectMenuItem(string path) {
            var segments = path.Split('>', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (segments.Length == 0) {
                throw new ArgumentException("Menu path must contain at least one segment.", nameof(path));
            }

            AutomationElement? currentScope = FindMainWindow();
            if (currentScope == null) {
                throw new InvalidOperationException("Main window is not available for menu interaction.");
            }

            for (var i = 0; i < segments.Length; i++) {
                var segment = segments[i];
                var isLast = i == segments.Length - 1;

                var scope = currentScope;
                var menuItem = WaitForElement(
                    () => FindMenuItem(segment, scope),
                    defaultTimeout,
                    $"Menu item '{segment}'"
                );

                if (isLast) {
                    ClickElement(menuItem);
                } else if (menuItem.TryGetCurrentPattern(ExpandCollapsePattern.Pattern, out var expandObj)) {
                    ((ExpandCollapsePattern)expandObj).Expand();
                } else {
                    ClickElement(menuItem);
                }

                currentScope = menuItem;
            }

            TryHandlePendingPickerDialogs();
            WaitForIdle();
        }

        public void SendKeyboardShortcut(string shortcut) {
            EnsureLaunched();
            FocusAppWindow();
            SendKeyboardShortcutCore(shortcut);
            TryHandlePendingPickerDialogs();
        }

        public void SendKeyboardShortcutToElement(string id, string shortcut) {
            var element = GetElement(id);
            FocusElementWindow(element);
            TrySetFocus(element);
            SendKeyboardShortcutCore(shortcut);
            TryHandlePendingPickerDialogs();
        }

        public void MoveMouseWithinElement(string elementId, double xRatio, double yRatio) {
            var element = GetElement(elementId);
            FocusElementWindow(element);
            var point = ResolvePointInElement(element, xRatio, yRatio);
            SetCursorPos(point.x + 8, point.y + 8);
            Thread.Sleep(20);
            SetCursorPos(point.x, point.y);
            Thread.Sleep(40);
        }

        public string GetCurrentCursorKind() {
            EnsureLaunched();
            var cursorInfo = new CursorInfo { cbSize = (uint)Marshal.SizeOf<CursorInfo>() };
            if (!GetCursorInfo(out cursorInfo) || (cursorInfo.flags & 0x1) == 0) {
                return "Unknown";
            }

            var handCursor = LoadCursor(IntPtr.Zero, CursorHandId);
            if (cursorInfo.hCursor == handCursor) {
                return "Hand";
            }

            var arrowCursor = LoadCursor(IntPtr.Zero, CursorArrowId);
            if (cursorInfo.hCursor == arrowCursor) {
                return "Arrow";
            }

            return "Other";
        }

        public void MouseWheelWithinElement(string elementId, double xRatio, double yRatio, int delta, bool holdControl = false) {
            var element = GetElement(elementId);
            FocusElementWindow(element);
            var point = ResolvePointInElement(element, xRatio, yRatio);
            SetCursorPos(point.x, point.y);
            Thread.Sleep(25);

            if (holdControl) {
                keybd_event(VirtualKeyControl, 0, 0, UIntPtr.Zero);
            }

            mouse_event(MouseEventFWheel, 0, 0, unchecked((uint)delta), UIntPtr.Zero);
            Thread.Sleep(25);

            if (holdControl) {
                keybd_event(VirtualKeyControl, 0, KeyEventFKeyUp, UIntPtr.Zero);
            }

            WaitForIdle();
        }

        public void ClickWithinElement(string elementId, double xRatio, double yRatio) {
            var element = GetElement(elementId);
            FocusElementWindow(element);
            var point = ResolvePointInElement(element, xRatio, yRatio);

            SetCursorPos(point.x, point.y);
            Thread.Sleep(20);
            mouse_event(MouseEventFLeftDown, 0, 0, 0, UIntPtr.Zero);
            Thread.Sleep(20);
            mouse_event(MouseEventFLeftUp, 0, 0, 0, UIntPtr.Zero);
            WaitForIdle();
        }

        public void RightClickWithinElement(string elementId, double xRatio, double yRatio) {
            var element = GetElement(elementId);
            FocusElementWindow(element);
            var point = ResolvePointInElement(element, xRatio, yRatio);

            SetCursorPos(point.x, point.y);
            Thread.Sleep(20);
            mouse_event(MouseEventFRightDown, 0, 0, 0, UIntPtr.Zero);
            Thread.Sleep(20);
            mouse_event(MouseEventFRightUp, 0, 0, 0, UIntPtr.Zero);
            WaitForIdle();
        }

        public void MiddleClickWithinElement(string elementId, double xRatio, double yRatio) {
            var element = GetElement(elementId);
            FocusElementWindow(element);
            TrySetFocus(element);
            var point = ResolvePointInElement(element, xRatio, yRatio);

            SetCursorPos(point.x + 4, point.y + 4);
            Thread.Sleep(20);
            SetCursorPos(point.x, point.y);
            Thread.Sleep(20);
            mouse_event(MouseEventFMiddleDown, 0, 0, 0, UIntPtr.Zero);
            Thread.Sleep(60);
            mouse_event(MouseEventFMiddleUp, 0, 0, 0, UIntPtr.Zero);
            WaitForIdle();
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

        public void DragNamedElementWithin(string containerId, string elementName, double targetXRatio, double targetYRatio) {
            var container = GetElement(containerId);
            var namedElement = WaitForElement(
                () => FindNamedDescendant(container, elementName),
                defaultTimeout,
                $"Element named '{elementName}' inside '{containerId}'"
            );

            FocusElementWindow(container);
            var sourceBounds = namedElement.Current.BoundingRectangle;
            var sourceX = (int)(sourceBounds.Left + sourceBounds.Width / 2);
            var sourceY = (int)(sourceBounds.Top + sourceBounds.Height / 2);
            var target = ResolvePointInElement(container, targetXRatio, targetYRatio);

            SetCursorPos(sourceX, sourceY);
            Thread.Sleep(20);
            mouse_event(MouseEventFLeftDown, 0, 0, 0, UIntPtr.Zero);
            Thread.Sleep(20);
            SetCursorPos(target.x, target.y);
            Thread.Sleep(40);
            mouse_event(MouseEventFLeftUp, 0, 0, 0, UIntPtr.Zero);
            WaitForIdle();
        }

        public void DragWithinElement(string elementId, double startXRatio, double startYRatio, double endXRatio, double endYRatio) {
            var element = GetElement(elementId);
            FocusElementWindow(element);
            var start = ResolvePointInElement(element, startXRatio, startYRatio);
            var end = ResolvePointInElement(element, endXRatio, endYRatio);

            SetCursorPos(start.x, start.y);
            Thread.Sleep(20);
            mouse_event(MouseEventFLeftDown, 0, 0, 0, UIntPtr.Zero);
            Thread.Sleep(20);
            SetCursorPos(end.x, end.y);
            Thread.Sleep(40);
            mouse_event(MouseEventFLeftUp, 0, 0, 0, UIntPtr.Zero);
            WaitForIdle();
        }

        public void Drag(string sourceId, string targetId) {
            var source = GetElement(sourceId);
            var target = GetElement(targetId);
            FocusElementWindow(source);

            var sourceRect = source.Current.BoundingRectangle;
            var targetRect = target.Current.BoundingRectangle;
            var sourceX = (int)(sourceRect.Left + sourceRect.Width / 2);
            var sourceY = (int)(sourceRect.Top + sourceRect.Height / 2);
            var targetX = (int)(targetRect.Left + targetRect.Width / 2);
            var targetY = (int)(targetRect.Top + targetRect.Height / 2);

            SetCursorPos(sourceX, sourceY);
            Thread.Sleep(20);
            mouse_event(MouseEventFLeftDown, 0, 0, 0, UIntPtr.Zero);
            Thread.Sleep(20);
            SetCursorPos(targetX, targetY);
            Thread.Sleep(40);
            mouse_event(MouseEventFLeftUp, 0, 0, 0, UIntPtr.Zero);
            WaitForIdle();
        }

        public void InvokeCommand(string commandId) {
            EnsureLaunched();

            var normalized = commandId.Trim();
            string buttonName = normalized switch {
                "DialogResult.Yes" => "Yes",
                "DialogResult.No" => "No",
                "DialogResult.Cancel" => "Cancel",
                "DialogResult.Ok" => "OK",
                _ => string.Empty
            };

            if (string.IsNullOrEmpty(buttonName)) {
                return;
            }

            var button = WaitForElement(
                () => FindDialogButton(buttonName),
                defaultTimeout,
                $"Dialog button '{buttonName}'"
            );

            ClickElement(button);
            TryHandlePendingPickerDialogs();
            WaitForIdle();
        }

        public bool TryInvokeCommand(string commandId, TimeSpan? timeout = null) {
            EnsureLaunched();

            var normalized = commandId.Trim();
            string buttonName = normalized switch {
                "DialogResult.Yes" => "Yes",
                "DialogResult.No" => "No",
                "DialogResult.Cancel" => "Cancel",
                "DialogResult.Ok" => "OK",
                _ => string.Empty
            };

            if (string.IsNullOrEmpty(buttonName)) {
                return false;
            }

            var button = WaitUntil(
                () => FindDialogButton(buttonName) != null,
                timeout ?? TimeSpan.FromSeconds(2));
            if (!button) {
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

        public void SetTestFileSelections(params string[] paths) {
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
                _ = WaitForElement(FindMainWindow, timeout ?? defaultTimeout, "main window");
            } catch (Exception ex) when (ex is TimeoutException or InvalidOperationException) {
                throw new TimeoutException(WithDiagnostics(ex.Message));
            }
        }

        public void WaitForStartWindow(TimeSpan? timeout = null) {
            try {
                _ = WaitForElement(FindStartWindow, timeout ?? defaultTimeout, "start window");
            } catch (Exception ex) when (ex is TimeoutException or InvalidOperationException) {
                throw new TimeoutException(WithDiagnostics(ex.Message));
            }
        }

        public void AssertNotificationContains(string text) {
            EnsureLaunched();
            var found = WaitUntil(() => FindTextContaining(text) != null, TimeSpan.FromSeconds(3));
            if (!found) {
                throw new InvalidOperationException($"Did not find UI text containing '{text}'.");
            }
        }

        private AutomationElement GetElement(string id) {
            return WaitForElement(() => GetElementOrNull(id), defaultTimeout, $"Element '{id}'");
        }

        private AutomationElement? GetElementOrNull(string id) {
            EnsureLaunched();

            return id switch {
                StartWindowId => FindStartWindow(),
                MainWindowId => FindMainWindow(),
                _ => FindElementByAutomationId(id)
            };
        }

        private AutomationElement? FindStartWindow() {
            foreach (var window in GetProcessWindowsInSearchOrder()) {
                if (FindByAutomationIdWithin(window, StartWindowOpenMapButtonId) != null) {
                    return window;
                }
            }

            return null;
        }

        private AutomationElement? FindMainWindow() {
            foreach (var window in GetProcessWindowsInSearchOrder()) {
                if (string.Equals(window.Current.Name, MainWindowFallbackTitle, StringComparison.OrdinalIgnoreCase)) {
                    return window;
                }

                if (FindByAutomationIdWithin(window, MainWindowId) != null) {
                    return window;
                }

                if (MainWindowSentinelIds.Any(id => FindByAutomationIdWithin(window, id) != null)) {
                    return window;
                }
            }

            return null;
        }

        private AutomationElement? FindElementByAutomationId(string automationId) {
            foreach (var window in GetProcessWindowsInSearchOrder()) {
                var inWindow = FindByAutomationIdWithin(window, automationId);
                if (inWindow != null) {
                    return inWindow;
                }
            }

            return null;
        }

        private AutomationElement? FindByAutomationIdWithin(AutomationElement scope, string automationId) {
            if (string.Equals(scope.Current.AutomationId, automationId, StringComparison.Ordinal)) {
                return scope;
            }

            var condition = new PropertyCondition(AutomationElement.AutomationIdProperty, automationId);
            return scope.FindFirst(TreeScope.Descendants, condition);
        }

        private AutomationElement? FindElementByName(string name, ControlType controlType) {
            foreach (var window in GetProcessWindowsInSearchOrder()) {
                var condition = new AndCondition(
                    new PropertyCondition(AutomationElement.ControlTypeProperty, controlType),
                    new PropertyCondition(AutomationElement.NameProperty, name)
                );
                var element = window.FindFirst(TreeScope.Descendants, condition);
                if (element != null) {
                    return element;
                }
            }

            return null;
        }

        private AutomationElement? FindElementByName(string name) {
            foreach (var window in GetProcessWindowsInSearchOrder()) {
                var condition = new PropertyCondition(AutomationElement.NameProperty, name);
                var element = window.FindFirst(TreeScope.Descendants, condition);
                if (element != null) {
                    return element;
                }
            }

            return null;
        }

        private AutomationElement? FindMenuItemByPath(string path) {
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

        private AutomationElement? FindWindowByTitle(string title) {
            return GetProcessWindowsInSearchOrder().FirstOrDefault(window =>
                string.Equals(window.Current.Name ?? string.Empty, title, StringComparison.OrdinalIgnoreCase));
        }

        private AutomationElement? FindMenuItem(string menuLabel, AutomationElement scope) {
            var target = NormalizeUiLabel(menuLabel);
            var allMenuItems = scope.FindAll(
                TreeScope.Descendants,
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.MenuItem)
            );

            foreach (AutomationElement item in allMenuItems) {
                if (NormalizeUiLabel(item.Current.Name) == target) {
                    return item;
                }
            }

            // fallback: search globally in case submenu is in a popup tree
            foreach (var window in GetProcessWindows()) {
                var items = window.FindAll(
                    TreeScope.Descendants,
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.MenuItem)
                );
                foreach (AutomationElement item in items) {
                    if (NormalizeUiLabel(item.Current.Name) == target) {
                        return item;
                    }
                }
            }

            return null;
        }

        private AutomationElement? FindDialogButton(string buttonName) {
            foreach (var window in GetProcessWindowsInSearchOrder()) {
                var condition = new AndCondition(
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button),
                    new PropertyCondition(AutomationElement.NameProperty, buttonName)
                );
                var button = window.FindFirst(TreeScope.Descendants, condition);
                if (button != null) {
                    return button;
                }
            }

            return null;
        }

        private AutomationElement? FindTextContaining(string text) {
            foreach (var window in GetProcessWindowsInSearchOrder()) {
                var texts = window.FindAll(
                    TreeScope.Descendants,
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Text)
                );

                foreach (AutomationElement textElement in texts) {
                    if ((textElement.Current.Name ?? string.Empty).Contains(text, StringComparison.OrdinalIgnoreCase)) {
                        return textElement;
                    }
                }
            }

            return null;
        }

        private IEnumerable<AutomationElement> GetProcessWindowsInSearchOrder() {
            var foregroundHandle = GetForegroundWindow();
            return GetProcessWindows()
                .OrderBy(window => new IntPtr(window.Current.NativeWindowHandle) != foregroundHandle)
                .ThenBy(window => window.Current.IsOffscreen)
                .ThenByDescending(window => window.Current.NativeWindowHandle);
        }

        private IReadOnlyList<AutomationElement> GetProcessWindows() {
            EnsureLaunched();
            if (appProcess is null || appProcess.HasExited) {
                return Array.Empty<AutomationElement>();
            }

            var condition = new AndCondition(
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Window),
                new PropertyCondition(AutomationElement.ProcessIdProperty, appProcess.Id)
            );

            var results = new List<AutomationElement>();
            var seen = new HashSet<int>();
            var desktopChildren = AutomationElement.RootElement.FindAll(TreeScope.Children, Condition.TrueCondition);
            foreach (AutomationElement child in desktopChildren) {
                TryAddWindow(child, results, seen);

                try {
                    // Native file/folder pickers can be nested under intermediate automation containers
                    // instead of appearing as direct desktop children.
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

        private void ClickElement(AutomationElement element) {
            FocusElementWindow(element);
            TrySetFocus(element);

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
                return;
            }

            var bounds = element.Current.BoundingRectangle;
            var x = (int)(bounds.Left + bounds.Width / 2);
            var y = (int)(bounds.Top + bounds.Height / 2);
            SetCursorPos(x, y);
            Thread.Sleep(20);
            mouse_event(MouseEventFLeftDown, 0, 0, 0, UIntPtr.Zero);
            Thread.Sleep(20);
            mouse_event(MouseEventFLeftUp, 0, 0, 0, UIntPtr.Zero);
        }

        private static void PhysicalClickElement(AutomationElement element) {
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

        private static void TrySetFocus(AutomationElement element) {
            try {
                element.SetFocus();
            } catch {
                // Some controls do not support focus through UI Automation.
            }
        }

        private static AutomationElement? FindNamedDescendant(AutomationElement scope, string name) {
            var condition = new PropertyCondition(AutomationElement.NameProperty, name);
            return scope.FindFirst(TreeScope.Descendants, condition);
        }

        private static AutomationElement? FindListItemContainingText(AutomationElement list, string text) {
            var allDescendants = list.FindAll(TreeScope.Descendants, Condition.TrueCondition);
            foreach (AutomationElement element in allDescendants) {
                if (!string.Equals(element.Current.Name ?? string.Empty, text, StringComparison.Ordinal)) {
                    continue;
                }

                var current = element;
                while (current != null) {
                    if (Equals(current.Current.ControlType, ControlType.ListItem)) {
                        return current;
                    }

                    current = TreeWalker.ControlViewWalker.GetParent(current);
                }
            }

            return null;
        }

        private AutomationElement GetDataGridCell(string dataGridId, int rowIndex, int columnIndex) {
            var dataGrid = GetElement(dataGridId);
            var rows = GetDataGridRows(dataGrid);
            if (rowIndex < 0 || rowIndex >= rows.Count) {
                throw new ArgumentOutOfRangeException(nameof(rowIndex), $"DataGrid row index {rowIndex} is out of range.");
            }
            if (columnIndex < 0) {
                throw new ArgumentOutOfRangeException(nameof(columnIndex), "DataGrid column index must be non-negative.");
            }

            var row = rows[rowIndex];
            var rowCells = row.FindAll(TreeScope.Descendants, Condition.TrueCondition)
                .Cast<AutomationElement>()
                .Where(element =>
                    element.TryGetCurrentPattern(GridItemPattern.Pattern, out var gridObj) &&
                    ((GridItemPattern)gridObj).Current.Row == rowIndex &&
                    ((GridItemPattern)gridObj).Current.Column == columnIndex
                )
                .OrderBy(element => element.Current.BoundingRectangle.Left)
                .ToList();
            if (rowCells.Count > 0) {
                return rowCells[0];
            }

            var fallbackCell = dataGrid.FindAll(TreeScope.Descendants, Condition.TrueCondition)
                .Cast<AutomationElement>()
                .FirstOrDefault(element =>
                    element.TryGetCurrentPattern(GridItemPattern.Pattern, out var gridObj) &&
                    ((GridItemPattern)gridObj).Current.Row == rowIndex &&
                    ((GridItemPattern)gridObj).Current.Column == columnIndex
                );

            if (fallbackCell != null) {
                return fallbackCell;
            }

            throw new InvalidOperationException($"Could not find DataGrid cell [{rowIndex}, {columnIndex}] in '{dataGridId}'.");
        }

        private static IReadOnlyList<AutomationElement> GetDataGridRows(AutomationElement dataGrid) {
            var rowCondition = new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.DataItem);
            var rows = dataGrid.FindAll(TreeScope.Descendants, rowCondition)
                .Cast<AutomationElement>()
                .Where(row => {
                    var name = row.Current.Name ?? string.Empty;
                    return !name.Contains("NewItemPlaceholder", StringComparison.OrdinalIgnoreCase);
                })
                .OrderBy(row => row.Current.BoundingRectangle.Top)
                .ToList();

            return rows;
        }

        private static string ReadElementText(AutomationElement element) {
            if (element.TryGetCurrentPattern(ValuePattern.Pattern, out var valueObj)) {
                return ((ValuePattern)valueObj).Current.Value ?? string.Empty;
            }

            var text = element.FindFirst(
                TreeScope.Descendants,
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Text)
            );
            if (text != null && !string.IsNullOrWhiteSpace(text.Current.Name)) {
                return text.Current.Name;
            }

            return element.Current.Name ?? string.Empty;
        }

        private void EnsureLaunched() {
            if (appProcess == null) {
                throw new InvalidOperationException("Driver is not launched. Call Launch() first.");
            }

            if (appProcess.HasExited) {
                var testLog = GetTestLog();
                throw new InvalidOperationException(
                    string.IsNullOrWhiteSpace(testLog)
                        ? "Driver is not launched. Call Launch() first."
                        : $"Driver is not launched. Call Launch() first.{Environment.NewLine}{testLog}");
            }
        }

        private void FocusAppWindow() {
            var window = FindMainWindow() ?? FindStartWindow() ?? GetProcessWindows().FirstOrDefault();
            if (window == null) {
                return;
            }

            FocusWindow(window);
        }

        private void FocusElementWindow(AutomationElement element) {
            var window = FindOwningWindow(element);
            if (window != null) {
                ActivateWindow(window);
                return;
            }

            FocusAppWindow();
        }

        private static AutomationElement? FindOwningWindow(AutomationElement element) {
            try {
                var walker = TreeWalker.ControlViewWalker;
                var current = element;
                while (current != null) {
                    if (Equals(current.Current.ControlType, ControlType.Window)) {
                        return current;
                    }
                    current = walker.GetParent(current);
                }
            } catch {
                // Best effort lookup; fallback handled by caller.
            }

            return null;
        }

        private static void ActivateWindow(AutomationElement window) {
            var hwnd = new IntPtr(window.Current.NativeWindowHandle);
            if (hwnd == IntPtr.Zero) {
                return;
            }

            TrySetFocus(window);
            SetForegroundWindow(hwnd);
            Thread.Sleep(60);
        }

        private static void FocusWindow(AutomationElement window) {
            ActivateWindow(window);

            var cursorInfo = new CursorInfo { cbSize = (uint)Marshal.SizeOf<CursorInfo>() };
            var hasCursorPosition = GetCursorInfo(out cursorInfo);
            var bounds = window.Current.BoundingRectangle;
            if (bounds.Width > 60 && bounds.Height > 20) {
                var x = (int)(bounds.Left + Math.Min(120, Math.Max(40, bounds.Width * 0.25)));
                var y = (int)(bounds.Top + Math.Min(24, Math.Max(8, bounds.Height * 0.05)));
                SetCursorPos(x, y);
                Thread.Sleep(20);
                mouse_event(MouseEventFLeftDown, 0, 0, 0, UIntPtr.Zero);
                Thread.Sleep(20);
                mouse_event(MouseEventFLeftUp, 0, 0, 0, UIntPtr.Zero);
                Thread.Sleep(40);

                if (hasCursorPosition) {
                    SetCursorPos(cursorInfo.ptScreenPos.x, cursorInfo.ptScreenPos.y);
                    Thread.Sleep(20);
                }
            }
        }

        private string WithDiagnostics(string message) {
            var diagnostics = GetDiagnostics();
            return string.IsNullOrWhiteSpace(diagnostics)
                ? message
                : $"{message}{Environment.NewLine}{diagnostics}";
        }

        private string GetDiagnostics() {
            if (appProcess is not { HasExited: false }) {
                return GetTestLog();
            }

            try {
                var summary = GetWindowDebugSummary();
                return string.IsNullOrWhiteSpace(summary) ? GetTestLog() : summary;
            } catch {
                return GetTestLog();
            }
        }

        private AutomationElement WaitForElement(Func<AutomationElement?> resolver, TimeSpan timeout, string description) {
            var deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline) {
                try {
                    var element = resolver();
                    if (element != null) {
                        return element;
                    }
                } catch (COMException) {
                    // Window trees can churn during transitions; retry within the timeout.
                } catch (ElementNotAvailableException) {
                    // The automation element disappeared between polls; retry.
                }

                Thread.Sleep(50);
            }

            throw new TimeoutException($"Timed out waiting for {description}.");
        }

        private bool WaitUntil(Func<bool> condition, TimeSpan timeout) {
            var deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline) {
                try {
                    if (condition()) {
                        return true;
                    }
                } catch (COMException) {
                    // UI Automation can transiently fail while windows are opening or closing.
                } catch (ElementNotAvailableException) {
                    // Treat disappearing automation elements as a transient poll failure.
                }

                Thread.Sleep(40);
            }

            return false;
        }

        private void TryHandlePendingPickerDialogs() {
            // The application consumes queued picker selections directly through
            // EDDA_TEST_PICKER_QUEUE_FILE, so no external UI Automation is needed here.
        }

        private AutomationElement? FindPickerDialogWindow() {
            foreach (var window in GetProcessWindows()) {
                if (!Equals(window.Current.ControlType, ControlType.Window)) {
                    continue;
                }

                var windowName = window.Current.Name ?? string.Empty;
                if (PickerDialogTitles.Any(title => string.Equals(title, windowName, StringComparison.OrdinalIgnoreCase))) {
                    return window;
                }
            }

            return null;
        }

        private void SelectPathInDialog(AutomationElement dialogWindow, string path) {
            FocusWindow(dialogWindow);

            // Focus address/path bar.
            SendCombo(VirtualKeyControl, 0x4C); // Ctrl+L
            Thread.Sleep(80);
            SendCombo(VirtualKeyControl, 0x41); // Ctrl+A
            Thread.Sleep(40);

            SendText(path);
            SendKey(0x0D); // Enter
            Thread.Sleep(250);

            if (WaitUntil(() => IsDialogClosed(dialogWindow), TimeSpan.FromSeconds(3))) {
                return;
            }

            // File pickers are more reliable when we also populate the explicit "File name" field.
            var fileNameField = FindDialogFileNameField(dialogWindow);
            if (fileNameField != null) {
                SetDialogFieldText(fileNameField, path);
                SendKey(0x0D); // Enter
                Thread.Sleep(200);

                if (WaitUntil(() => IsDialogClosed(dialogWindow), TimeSpan.FromSeconds(3))) {
                    return;
                }
            }

            // Try native accelerator for "Open".
            SendCombo(VirtualKeyAlt, 0x4F); // Alt+O
            if (WaitUntil(() => IsDialogClosed(dialogWindow), TimeSpan.FromSeconds(3))) {
                return;
            }

            // Fallback to clicking a likely confirm button.
            var confirmButton = FindDialogConfirmButton(dialogWindow);
            if (confirmButton != null) {
                ClickElement(confirmButton);
            }

            if (!WaitUntil(() => IsDialogClosed(dialogWindow), TimeSpan.FromSeconds(3))) {
                throw new InvalidOperationException(
                    $"Picker dialog '{dialogWindow.Current.Name}' did not close after selecting '{path}'. {DescribeDialog(dialogWindow)}"
                );
            }
        }

        private void CancelDialog(AutomationElement dialogWindow) {
            FocusWindow(dialogWindow);

            SendKey(0x1B); // Escape
            if (WaitUntil(() => IsDialogClosed(dialogWindow), TimeSpan.FromSeconds(2))) {
                return;
            }

            var cancelButton = FindDialogCancelButton(dialogWindow);
            if (cancelButton != null) {
                ClickElement(cancelButton);
            }

            if (!WaitUntil(() => IsDialogClosed(dialogWindow), TimeSpan.FromSeconds(3))) {
                throw new InvalidOperationException(
                    $"Picker dialog '{dialogWindow.Current.Name}' did not close after cancellation. {DescribeDialog(dialogWindow)}"
                );
            }
        }

        private void SetDialogFieldText(AutomationElement field, string value) {
            TrySetFocus(field);

            if (field.TryGetCurrentPattern(ValuePattern.Pattern, out var valuePatternObj)) {
                ((ValuePattern)valuePatternObj).SetValue(value);
                return;
            }

            var bounds = field.Current.BoundingRectangle;
            var x = (int)(bounds.Left + bounds.Width / 2);
            var y = (int)(bounds.Top + bounds.Height / 2);
            SetCursorPos(x, y);
            Thread.Sleep(20);
            mouse_event(MouseEventFLeftDown, 0, 0, 0, UIntPtr.Zero);
            Thread.Sleep(20);
            mouse_event(MouseEventFLeftUp, 0, 0, 0, UIntPtr.Zero);
            Thread.Sleep(40);
            SendCombo(VirtualKeyControl, 0x41); // Ctrl+A
            Thread.Sleep(40);
            SendText(value);
        }

        private static AutomationElement? FindDialogFileNameField(AutomationElement dialogWindow) {
            var descendants = dialogWindow.FindAll(TreeScope.Descendants, Condition.TrueCondition);
            foreach (AutomationElement element in descendants) {
                var isTextEntry = Equals(element.Current.ControlType, ControlType.Edit) ||
                    Equals(element.Current.ControlType, ControlType.ComboBox);
                if (!isTextEntry) {
                    continue;
                }

                var normalizedName = NormalizeUiLabel(element.Current.Name);
                var automationId = element.Current.AutomationId ?? string.Empty;
                if (automationId == "1148" || normalizedName is "filename" or "nazwapliku") {
                    return element;
                }
            }

            return null;
        }

        private static AutomationElement? FindDialogConfirmButton(AutomationElement dialogWindow) {
            var buttons = dialogWindow.FindAll(
                TreeScope.Descendants,
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button)
            );

            var fallbackCandidates = new List<AutomationElement>();
            foreach (AutomationElement button in buttons) {
                var normalized = NormalizeUiLabel(button.Current.Name);
                if (normalized is "open" or "ok" or "select" or "selectfolder" or "choose" or "choosefolder" or "otworz" or "wybierz" or "wybierzfolder" or "zapisz" or "save") {
                    return button;
                }

                var bounds = button.Current.BoundingRectangle;
                if (!button.Current.IsEnabled || bounds.Width < 40 || bounds.Height < 20) {
                    continue;
                }

                if (normalized is "cancel" or "close" or "anuluj" or "zamknij" or "help" or "pomoc" or "nowyfolder" or "newfolder") {
                    continue;
                }

                fallbackCandidates.Add(button);
            }

            return fallbackCandidates
                .OrderByDescending(button => button.Current.BoundingRectangle.Bottom)
                .ThenByDescending(button => button.Current.BoundingRectangle.Right)
                .FirstOrDefault();
        }

        private static AutomationElement? FindDialogCancelButton(AutomationElement dialogWindow) {
            var buttons = dialogWindow.FindAll(
                TreeScope.Descendants,
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button)
            );

            foreach (AutomationElement button in buttons) {
                var normalized = NormalizeUiLabel(button.Current.Name);
                if (normalized is "cancel" or "close" or "anuluj" or "zamknij") {
                    return button;
                }
            }

            return null;
        }

        private static string NormalizeUiLabel(string? value) {
            if (string.IsNullOrWhiteSpace(value)) {
                return string.Empty;
            }

            return new string(value.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
        }

        private static bool IsDialogClosed(AutomationElement dialogWindow) {
            try {
                var handle = dialogWindow.Current.NativeWindowHandle;
                return handle == 0 || dialogWindow.Current.IsOffscreen;
            } catch (ElementNotAvailableException) {
                return true;
            } catch (COMException) {
                return true;
            }
        }

        private static string DescribeDialog(AutomationElement dialogWindow) {
            var interestingControls = dialogWindow.FindAll(TreeScope.Descendants, Condition.TrueCondition)
                .Cast<AutomationElement>()
                .Where(element =>
                    Equals(element.Current.ControlType, ControlType.Button) ||
                    Equals(element.Current.ControlType, ControlType.Edit) ||
                    Equals(element.Current.ControlType, ControlType.ComboBox))
                .Select(element => {
                    var controlType = element.Current.ControlType?.ProgrammaticName ?? "unknown";
                    var name = element.Current.Name ?? string.Empty;
                    var automationId = element.Current.AutomationId ?? string.Empty;
                    return $"{controlType}('{name}', id='{automationId}')";
                })
                .Distinct(StringComparer.Ordinal)
                .Take(12);

            return $"Visible controls: {string.Join(", ", interestingControls)}";
        }

        private static string DescribeWindowDescendants(AutomationElement window) {
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
                        .Take(20)
                        .Select(element => $"{element.controlType}('{element.name}', id='{element.id}')"));
            } catch (COMException) {
                return "descendants unavailable";
            } catch (ElementNotAvailableException) {
                return "descendants unavailable";
            }
        }

        private void DismissOpenMenus() {
            try {
                FocusAppWindow();
                SendKey(0x1B); // Escape
                Thread.Sleep(40);
            } catch {
                // Best effort cleanup after menu inspection.
            }
        }

        private static void SendKeyboardShortcutCore(string shortcut) {
            var normalized = shortcut.Trim().Replace(" ", string.Empty).ToLowerInvariant();
            switch (normalized) {
                case "ctrl+g":
                    SendCombo(0x11, 0x47); // Ctrl + G
                    break;
                case "ctrl+s":
                    SendCombo(0x11, 0x53); // Ctrl + S
                    break;
                case "ctrl+b":
                    SendCombo(0x11, 0x42); // Ctrl + B
                    break;
                case "ctrl+t":
                    SendCombo(0x11, 0x54); // Ctrl + T
                    break;
                case "ctrl+shift+t":
                    SendCombo(VirtualKeyControl, VirtualKeyShift, 0x54); // Ctrl + Shift + T
                    break;
                case "ctrl+a":
                    SendCombo(0x11, 0x41); // Ctrl + A
                    break;
                case "ctrl+z":
                    SendCombo(0x11, 0x5A); // Ctrl + Z
                    break;
                case "ctrl+y":
                    SendCombo(0x11, 0x59); // Ctrl + Y
                    break;
                case "ctrl+shift+z":
                    SendCombo(VirtualKeyControl, VirtualKeyShift, 0x5A); // Ctrl + Shift + Z
                    break;
                case "ctrl+n":
                    SendCombo(0x11, 0x4E); // Ctrl + N
                    break;
                case "ctrl+o":
                    SendCombo(0x11, 0x4F); // Ctrl + O
                    break;
                case "ctrl+i":
                    SendCombo(0x11, 0x49); // Ctrl + I
                    break;
                case "ctrl+e":
                    SendCombo(0x11, 0x45); // Ctrl + E
                    break;
                case "ctrl+w":
                    SendCombo(0x11, 0x57); // Ctrl + W
                    break;
                case "ctrl+[":
                    SendComboUsingScanCode(0x11, 0xDB); // Ctrl + [
                    break;
                case "ctrl+]":
                    SendComboUsingScanCode(0x11, 0xDD); // Ctrl + ]
                    break;
                case "alt+f4":
                    SendCombo(0x12, 0x73); // Alt + F4
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

        private static void SendCombo(byte modifier, byte key) {
            var scanCode = (byte)MapVirtualKey(key, 0);
            keybd_event(modifier, 0, 0, UIntPtr.Zero);
            Thread.Sleep(10);
            keybd_event(0, scanCode, KeyEventFScancode, UIntPtr.Zero);
            Thread.Sleep(10);
            keybd_event(0, scanCode, KeyEventFScancode | KeyEventFKeyUp, UIntPtr.Zero);
            Thread.Sleep(10);
            keybd_event(modifier, 0, KeyEventFKeyUp, UIntPtr.Zero);
        }

        private static void SendComboUsingScanCode(byte modifier, byte key) {
            var scanCode = (byte)MapVirtualKey(key, 0);
            keybd_event(modifier, 0, 0, UIntPtr.Zero);
            keybd_event(0, scanCode, KeyEventFScancode, UIntPtr.Zero);
            keybd_event(0, scanCode, KeyEventFScancode | KeyEventFKeyUp, UIntPtr.Zero);
            keybd_event(modifier, 0, KeyEventFKeyUp, UIntPtr.Zero);
        }

        private static void SendCombo(byte firstModifier, byte secondModifier, byte key) {
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

        private static void SendKey(byte key) {
            keybd_event(key, 0, 0, UIntPtr.Zero);
            keybd_event(key, 0, KeyEventFKeyUp, UIntPtr.Zero);
        }

        private static void SendText(string text) {
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

        private static double ClampRatio(double ratio) {
            if (double.IsNaN(ratio)) {
                return 0;
            }

            return Math.Max(0, Math.Min(1, ratio));
        }

        private static (int x, int y) ResolvePointInElement(AutomationElement element, double xRatio, double yRatio) {
            var bounds = element.Current.BoundingRectangle;
            var x = (int)(bounds.Left + bounds.Width * ClampRatio(xRatio));
            var y = (int)(bounds.Top + bounds.Height * ClampRatio(yRatio));
            return (x, y);
        }

        private static void PerformLeftDoubleClick(int x, int y) {
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

        private void WritePickerSelections(params string[] selections) {
            if (string.IsNullOrWhiteSpace(pickerSelectionQueueFilePath)) {
                throw new InvalidOperationException("Picker selection queue file is not initialized.");
            }

            File.WriteAllLines(pickerSelectionQueueFilePath, selections);
        }

        private static string ResolveAppExecutablePath() {
            var envPath = Environment.GetEnvironmentVariable("EDDA_WPF_EXE");
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
                    Path.Combine(repoRoot, "src", "Edda.Wpf", "bin", "Debug", "net9.0-windows", "win-x64", "Edda.exe"),
                    Path.Combine(repoRoot, "src", "Edda.Wpf", "bin", "Release", "net9.0-windows", "win-x64", "Edda.exe"),
                    Path.Combine(repoRoot, "src", "Edda.Wpf", "bin", "Debug", "net9.0-windows", "win-x64", "Edda.dll"),
                    Path.Combine(repoRoot, "src", "Edda.Wpf", "bin", "Release", "net9.0-windows", "win-x64", "Edda.dll"),
                    Path.Combine(repoRoot, "src", "Edda.Wpf", "bin", "Debug", "net8.0-windows", "win-x64", "Edda.exe"),
                    Path.Combine(repoRoot, "src", "Edda.Wpf", "bin", "Release", "net8.0-windows", "win-x64", "Edda.exe"),
                    Path.Combine(repoRoot, "src", "Edda.Wpf", "bin", "Debug", "net8.0-windows", "win-x64", "Edda.dll"),
                    Path.Combine(repoRoot, "src", "Edda.Wpf", "bin", "Release", "net8.0-windows", "win-x64", "Edda.dll")
                };

                var match = candidates.FirstOrDefault(File.Exists);
                if (!string.IsNullOrWhiteSpace(match)) {
                    return match;
                }
            }

            throw new FileNotFoundException("Could not locate Edda WPF binary. Set EDDA_WPF_EXE to the full Edda.exe or Edda.dll path.");
        }

        private static string? FindRepositoryRoot(string startPath) {
            var directory = new DirectoryInfo(startPath);
            while (directory != null) {
                if (File.Exists(Path.Combine(directory.FullName, "RagnarockEditor.sln"))) {
                    return directory.FullName;
                }
                directory = directory.Parent;
            }

            return null;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Point {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct CursorInfo {
            public uint cbSize;
            public uint flags;
            public IntPtr hCursor;
            public Point ptScreenPos;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        [DllImport("user32.dll")]
        private static extern uint MapVirtualKey(uint uCode, uint uMapType);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern short VkKeyScan(char ch);

        [DllImport("user32.dll")]
        private static extern bool SetCursorPos(int x, int y);

        [DllImport("user32.dll")]
        private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetCursorInfo(out CursorInfo pci);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr LoadCursor(IntPtr hInstance, int lpCursorName);
    }
}
