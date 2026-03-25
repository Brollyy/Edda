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
        private static readonly string[] PickerDialogTitles = {
            "Select your map's containing folder",
            "Select an empty folder to store your map",
            "Select a folder to export the map to",
            "Select a song to map",
            "Select a simfile to import"
        };

        private const uint KeyEventFKeyUp = 0x0002;
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

        private readonly TimeSpan defaultTimeout = TimeSpan.FromSeconds(15);

        private Process? appProcess;
        private readonly Queue<string> pendingFileSelections = new();
        private string? testProfileRoot;

        public void Launch() {
            if (appProcess is { HasExited: false }) {
                return;
            }

            var appPath = ResolveAppExecutablePath();
            testProfileRoot = Path.Combine(Path.GetTempPath(), "Edda-WpfUiTests", Guid.NewGuid().ToString("N"));
            var appDataRoot = Path.Combine(testProfileRoot, "AppData", "Roaming");
            var localAppDataRoot = Path.Combine(testProfileRoot, "AppData", "Local");
            Directory.CreateDirectory(appDataRoot);
            Directory.CreateDirectory(localAppDataRoot);

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
            pendingFileSelections.Clear();

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

            var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromMilliseconds(250));
            while (DateTime.UtcNow < deadline) {
                if (appProcess is { HasExited: true }) {
                    throw new InvalidOperationException("Edda process exited unexpectedly.");
                }
                Thread.Sleep(30);
            }
        }

        public void ClickButton(string id) {
            var element = GetElement(id);
            ClickElement(element);
            TryHandlePendingPickerDialogs();
            WaitForIdle();
        }

        public void SetText(string id, string value) {
            var element = GetElement(id);
            FocusAppWindow();
            TrySetFocus(element);

            if (element.TryGetCurrentPattern(ValuePattern.Pattern, out var valuePatternObj)) {
                ((ValuePattern)valuePatternObj).SetValue(value);
                return;
            }

            throw new InvalidOperationException($"Element '{id}' does not support text input.");
        }

        public void SetSliderValue(string id, double value) {
            var element = GetElement(id);
            FocusAppWindow();
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

        public bool IsVisible(string id) {
            var element = GetElementOrNull(id);
            return element != null && !element.Current.IsOffscreen;
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
            FocusAppWindow();
            TrySetFocus(element);
            if (!element.TryGetCurrentPattern(TogglePattern.Pattern, out var toggleObj)) {
                throw new InvalidOperationException($"Element '{id}' does not support toggle interaction.");
            }

            var toggle = (TogglePattern)toggleObj;
            var current = toggle.Current.ToggleState == ToggleState.On;
            if (current == value) {
                return;
            }

            toggle.Toggle();
            WaitForIdle();
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
            if (combo.TryGetCurrentPattern(ExpandCollapsePattern.Pattern, out var expandObj)) {
                ((ExpandCollapsePattern)expandObj).Expand();
            }

            var item = WaitForElement(
                () => FindElementByName(value, ControlType.ListItem) ?? FindElementByName(value, ControlType.MenuItem),
                defaultTimeout,
                $"Dropdown item '{value}'"
            );

            if (item.TryGetCurrentPattern(SelectionItemPattern.Pattern, out var selectionObj)) {
                ((SelectionItemPattern)selectionObj).Select();
            } else {
                ClickElement(item);
            }

            if (combo.TryGetCurrentPattern(ExpandCollapsePattern.Pattern, out expandObj)) {
                ((ExpandCollapsePattern)expandObj).Collapse();
            }
        }

        public void OpenMenu(string id) {
            ClickButton(id);
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
                    SendCombo(0x11, 0xDB); // Ctrl + [
                    break;
                case "ctrl+]":
                    SendCombo(0x11, 0xDD); // Ctrl + ]
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

            TryHandlePendingPickerDialogs();
        }

        public void MoveMouseWithinElement(string elementId, double xRatio, double yRatio) {
            var element = GetElement(elementId);
            FocusAppWindow();
            var point = ResolvePointInElement(element, xRatio, yRatio);
            SetCursorPos(point.x, point.y);
            Thread.Sleep(25);
        }

        public void MouseWheelWithinElement(string elementId, double xRatio, double yRatio, int delta, bool holdControl = false) {
            var element = GetElement(elementId);
            FocusAppWindow();
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
            FocusAppWindow();
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
            FocusAppWindow();
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
            FocusAppWindow();
            var point = ResolvePointInElement(element, xRatio, yRatio);

            SetCursorPos(point.x, point.y);
            Thread.Sleep(20);
            mouse_event(MouseEventFMiddleDown, 0, 0, 0, UIntPtr.Zero);
            Thread.Sleep(20);
            mouse_event(MouseEventFMiddleUp, 0, 0, 0, UIntPtr.Zero);
            WaitForIdle();
        }

        public void DoubleClickElement(string id) {
            var element = GetElement(id);
            FocusAppWindow();
            var bounds = element.Current.BoundingRectangle;
            var x = (int)(bounds.Left + bounds.Width / 2);
            var y = (int)(bounds.Top + bounds.Height / 2);
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

            FocusAppWindow();
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
            FocusAppWindow();
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
            FocusAppWindow();

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

        public void SetTestFileSelection(string path) {
            EnsureLaunched();
            if (string.IsNullOrWhiteSpace(path)) {
                throw new ArgumentException("Test file selection path cannot be empty.", nameof(path));
            }

            pendingFileSelections.Clear();
            pendingFileSelections.Enqueue(Path.GetFullPath(path));
        }

        public void SetTestFileSelections(params string[] paths) {
            EnsureLaunched();
            if (paths == null || paths.Length == 0) {
                throw new ArgumentException("At least one file or folder path is required.", nameof(paths));
            }

            pendingFileSelections.Clear();
            foreach (var path in paths) {
                if (string.IsNullOrWhiteSpace(path)) {
                    throw new ArgumentException("Test file selection paths cannot be empty.", nameof(paths));
                }
                pendingFileSelections.Enqueue(Path.GetFullPath(path));
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
            foreach (var window in GetProcessWindows()) {
                if (FindByAutomationIdWithin(window, StartWindowOpenMapButtonId) != null) {
                    return window;
                }
            }

            return null;
        }

        private AutomationElement? FindMainWindow() {
            foreach (var window in GetProcessWindows()) {
                if (string.Equals(window.Current.Name, MainWindowFallbackTitle, StringComparison.OrdinalIgnoreCase)) {
                    return window;
                }

                if (FindByAutomationIdWithin(window, MainWindowId) != null) {
                    return window;
                }
            }

            return null;
        }

        private AutomationElement? FindElementByAutomationId(string automationId) {
            foreach (var window in GetProcessWindows()) {
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
            foreach (var window in GetProcessWindows()) {
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
            foreach (var window in GetProcessWindows()) {
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
            foreach (var window in GetProcessWindows()) {
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

        private IReadOnlyList<AutomationElement> GetProcessWindows() {
            EnsureLaunched();
            if (appProcess is null || appProcess.HasExited) {
                return Array.Empty<AutomationElement>();
            }

            var condition = new AndCondition(
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Window),
                new PropertyCondition(AutomationElement.ProcessIdProperty, appProcess.Id)
            );

            var windowCollection = AutomationElement.RootElement.FindAll(TreeScope.Children, condition);
            return windowCollection.Cast<AutomationElement>().ToList();
        }

        private void ClickElement(AutomationElement element) {
            FocusAppWindow();
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

            throw new InvalidOperationException($"Unable to click UI element '{element.Current.AutomationId}'.");
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

        private void EnsureLaunched() {
            if (appProcess == null || appProcess.HasExited) {
                throw new InvalidOperationException("Driver is not launched. Call Launch() first.");
            }
        }

        private void FocusAppWindow() {
            var window = FindMainWindow() ?? FindStartWindow() ?? GetProcessWindows().FirstOrDefault();
            if (window == null) {
                return;
            }

            FocusWindow(window);
        }

        private static void FocusWindow(AutomationElement window) {
            var hwnd = new IntPtr(window.Current.NativeWindowHandle);
            if (hwnd == IntPtr.Zero) {
                return;
            }

            SetForegroundWindow(hwnd);
            Thread.Sleep(30);
        }

        private AutomationElement WaitForElement(Func<AutomationElement?> resolver, TimeSpan timeout, string description) {
            var deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline) {
                var element = resolver();
                if (element != null) {
                    return element;
                }

                Thread.Sleep(50);
            }

            throw new TimeoutException($"Timed out waiting for {description}.");
        }

        private bool WaitUntil(Func<bool> condition, TimeSpan timeout) {
            var deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline) {
                if (condition()) {
                    return true;
                }

                Thread.Sleep(40);
            }

            return false;
        }

        private void TryHandlePendingPickerDialogs() {
            if (pendingFileSelections.Count == 0) {
                return;
            }

            while (pendingFileSelections.Count > 0) {
                var dialog = WaitForElement(
                    FindPickerDialogWindow,
                    TimeSpan.FromSeconds(6),
                    "file/folder picker dialog"
                );

                SelectPathInDialog(dialog, pendingFileSelections.Dequeue());
            }
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

            SendText(path);
            SendKey(0x0D); // Enter
            Thread.Sleep(120);

            // Try native accelerator for "Open".
            SendCombo(VirtualKeyAlt, 0x4F); // Alt+O

            if (WaitUntil(() => FindPickerDialogWindow() == null, TimeSpan.FromSeconds(3))) {
                return;
            }

            // Fallback to clicking a likely confirm button.
            var confirmButton = FindDialogConfirmButton(dialogWindow);
            if (confirmButton != null) {
                ClickElement(confirmButton);
            }

            _ = WaitUntil(() => FindPickerDialogWindow() == null, TimeSpan.FromSeconds(3));
        }

        private static AutomationElement? FindDialogConfirmButton(AutomationElement dialogWindow) {
            var buttons = dialogWindow.FindAll(
                TreeScope.Descendants,
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button)
            );

            foreach (AutomationElement button in buttons) {
                var normalized = NormalizeUiLabel(button.Current.Name);
                if (normalized is "open" or "ok" or "select" or "selectfolder") {
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

        private static void SendCombo(byte modifier, byte key) {
            keybd_event(modifier, 0, 0, UIntPtr.Zero);
            keybd_event(key, 0, 0, UIntPtr.Zero);
            keybd_event(key, 0, KeyEventFKeyUp, UIntPtr.Zero);
            keybd_event(modifier, 0, KeyEventFKeyUp, UIntPtr.Zero);
        }

        private static void SendCombo(byte firstModifier, byte secondModifier, byte key) {
            keybd_event(firstModifier, 0, 0, UIntPtr.Zero);
            keybd_event(secondModifier, 0, 0, UIntPtr.Zero);
            keybd_event(key, 0, 0, UIntPtr.Zero);
            keybd_event(key, 0, KeyEventFKeyUp, UIntPtr.Zero);
            keybd_event(secondModifier, 0, KeyEventFKeyUp, UIntPtr.Zero);
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
            mouse_event(MouseEventFLeftUp, 0, 0, 0, UIntPtr.Zero);
            Thread.Sleep(60);
            mouse_event(MouseEventFLeftDown, 0, 0, 0, UIntPtr.Zero);
            mouse_event(MouseEventFLeftUp, 0, 0, 0, UIntPtr.Zero);
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

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern short VkKeyScan(char ch);

        [DllImport("user32.dll")]
        private static extern bool SetCursorPos(int x, int y);

        [DllImport("user32.dll")]
        private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);
    }
}
