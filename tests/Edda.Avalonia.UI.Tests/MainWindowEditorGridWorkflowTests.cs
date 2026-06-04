using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using Xunit;

namespace Edda.Avalonia.UI.Tests;

public class MainWindowEditorGridWorkflowTests {
    private const string StartupOpenMapButtonId = "ButtonOpenMap";
    private const string SongProgressSliderId = "sliderSongProgress";
    private const string GridDivisionTextBoxId = "txtGridDivision";
    private const string ScrollEditorId = "scrollEditor";
    private const string ScrollEditorHoldIconId = "scrollEditorHoldIcon";
    private const string NotesStatsAllId = "notesStatsAll";
    private const string NotesStatsSelectedId = "notesStatsSelected";

    private const string FixtureMapFolderRelative = "tests/TestData/Wpf/MainWindow/FixtureMap";

    [Fact]
    public void BookmarkWorkflowViaMenuPersistsToDifficultyFile() {
        RunOpenedFixtureMapTest((driver, mapFolder) => {
            var difficultyPath = GetPrimaryDifficultyMapPath(mapFolder);
            var initialCount = GetBookmarksCount(difficultyPath);

            driver.SelectMenuItem("Edit>Add New>Bookmark");
            driver.WaitForIdle();
            driver.SendKeyboardShortcut("Ctrl+S");
            driver.WaitForIdle();

            Assert.Equal(initialCount + 1, GetBookmarksCount(difficultyPath));
        });
    }

    [Fact]
    public void BookmarkWorkflowViaShortcutPersistsToDifficultyFile() {
        RunOpenedFixtureMapTest((driver, mapFolder) => {
            var difficultyPath = GetPrimaryDifficultyMapPath(mapFolder);
            var initialCount = GetBookmarksCount(difficultyPath);

            driver.SendKeyboardShortcut("Ctrl+B");
            driver.WaitForIdle();
            driver.SendKeyboardShortcut("Ctrl+S");
            driver.WaitForIdle();

            Assert.Equal(initialCount + 1, GetBookmarksCount(difficultyPath));
        });
    }

    [Fact]
    public void TimingChangeWorkflowViaMenuAndShortcutPersistsToDifficultyFile() {
        RunOpenedFixtureMapTest((driver, mapFolder) => {
            var difficultyPath = GetPrimaryDifficultyMapPath(mapFolder);
            var initialCount = GetBpmChangesCount(difficultyPath);

            driver.SelectMenuItem("Edit>Add New>Timing Change");
            driver.WaitForIdle();
            driver.MoveMouseWithinElement(ScrollEditorId, 0.5, 0.75);
            driver.WaitForIdle();
            driver.SendKeyboardShortcut("Ctrl+Shift+T");
            driver.WaitForIdle();
            driver.SendKeyboardShortcut("Ctrl+S");
            driver.WaitForIdle();

            Assert.Equal(initialCount + 2, GetBpmChangesCount(difficultyPath));
        });
    }

    [Fact]
    public void DragSelectionSelectsNotesAndEscapeClearsSelection() {
        RunOpenedFixtureMapTest((driver, _) => {
            driver.ClickWithinElement(ScrollEditorId, 0.2, 0.8);
            driver.ClickWithinElement(ScrollEditorId, 0.45, 0.6);
            driver.ClickWithinElement(ScrollEditorId, 0.7, 0.4);
            driver.WaitForIdle();

            Assert.Equal(0, ParseIntegerText(driver.GetText(NotesStatsSelectedId), NotesStatsSelectedId));
            Assert.True(ParseIntegerText(driver.GetText(NotesStatsAllId), NotesStatsAllId) > 0);

            driver.DragWithinElement(ScrollEditorId, 0.15, 0.85, 0.75, 0.35);
            driver.WaitForIdle();

            var selectedAfterDrag = ParseIntegerText(driver.GetText(NotesStatsSelectedId), NotesStatsSelectedId);
            Assert.True(selectedAfterDrag > 0);

            driver.SendKeyboardShortcut("Escape");
            driver.WaitForIdle();
            Assert.Equal(0, ParseIntegerText(driver.GetText(NotesStatsSelectedId), NotesStatsSelectedId));
        });
    }

    [Fact]
    public void CtrlMouseWheelOverGridUpdatesGlobalGridDivision() {
        RunOpenedFixtureMapTest((driver, _) => {
            var initialDivision = ParseIntegerText(driver.GetText(GridDivisionTextBoxId), GridDivisionTextBoxId);

            driver.MoveMouseWithinElement(ScrollEditorId, 0.5, 0.9);
            driver.MouseWheelWithinElement(ScrollEditorId, 0.5, 0.9, 120, holdControl: true);
            driver.WaitForIdle();
            Assert.Equal(initialDivision + 1, ParseIntegerText(driver.GetText(GridDivisionTextBoxId), GridDivisionTextBoxId));

            driver.MouseWheelWithinElement(ScrollEditorId, 0.5, 0.9, -120, holdControl: true);
            driver.WaitForIdle();
            Assert.Equal(initialDivision, ParseIntegerText(driver.GetText(GridDivisionTextBoxId), GridDivisionTextBoxId));
        });
    }

    [Fact]
    public void CtrlMouseWheelOverTimingSegmentUpdatesLocalGridDivisionOnly() {
        RunOpenedFixtureMapTest((driver, mapFolder) => {
            var difficultyPath = GetPrimaryDifficultyMapPath(mapFolder);
            var globalDivision = ParseIntegerText(driver.GetText(GridDivisionTextBoxId), GridDivisionTextBoxId);

            driver.SetSliderValue(SongProgressSliderId, 1000);
            driver.WaitForIdle();
            driver.SelectMenuItem("Edit>Add New>Timing Change");
            driver.WaitForIdle();
            driver.SendKeyboardShortcut("Ctrl+S");
            driver.WaitForIdle();

            var initialLocalDivisions = GetBpmChangeGridDivisions(difficultyPath);
            Assert.Single(initialLocalDivisions);
            Assert.Equal(globalDivision, initialLocalDivisions[0]);

            driver.MoveMouseWithinElement(ScrollEditorId, 0.5, 0.9);
            driver.MouseWheelWithinElement(ScrollEditorId, 0.5, 0.9, 120, holdControl: true);
            driver.WaitForIdle();
            driver.SendKeyboardShortcut("Ctrl+S");
            driver.WaitForIdle();

            var updatedLocalDivisions = GetBpmChangeGridDivisions(difficultyPath);
            Assert.Single(updatedLocalDivisions);
            Assert.Equal(globalDivision + 1, updatedLocalDivisions[0]);
            Assert.Equal(globalDivision, ParseIntegerText(driver.GetText(GridDivisionTextBoxId), GridDivisionTextBoxId));
        });
    }

    [Fact]
    public void DraggingBookmarkMarkerPersistsUpdatedBookmarkBeat() {
        RunOpenedFixtureMapTest((driver, mapFolder) => {
            var difficultyPath = GetPrimaryDifficultyMapPath(mapFolder);

            driver.SetSliderValue(SongProgressSliderId, 1200);
            driver.WaitForIdle();
            driver.SelectMenuItem("Edit>Add New>Bookmark");
            driver.WaitForIdle();
            driver.SendKeyboardShortcut("Ctrl+S");
            driver.WaitForIdle();

            var initialBookmarkTimes = GetBookmarkTimes(difficultyPath);
            Assert.Single(initialBookmarkTimes);

            driver.DragNamedElementWithin(ScrollEditorId, "Bookmark", 0.5, 0.2);
            driver.WaitForIdle();
            driver.SendKeyboardShortcut("Ctrl+S");
            driver.WaitForIdle();

            var updatedBookmarkTimes = GetBookmarkTimes(difficultyPath);
            Assert.Single(updatedBookmarkTimes);
            Assert.True(Math.Abs(updatedBookmarkTimes[0] - initialBookmarkTimes[0]) > 0.01);
        });
    }

    [Fact]
    public void DraggingTimingChangeMarkerPersistsUpdatedBeat() {
        RunOpenedFixtureMapTest((driver, mapFolder) => {
            var difficultyPath = GetPrimaryDifficultyMapPath(mapFolder);
            var globalDivision = ParseIntegerText(driver.GetText(GridDivisionTextBoxId), GridDivisionTextBoxId);

            driver.SetSliderValue(SongProgressSliderId, 1400);
            driver.WaitForIdle();
            driver.SelectMenuItem("Edit>Add New>Timing Change");
            driver.WaitForIdle();
            driver.SendKeyboardShortcut("Ctrl+S");
            driver.WaitForIdle();

            var initialBpmTimes = GetBpmChangeTimes(difficultyPath);
            Assert.Single(initialBpmTimes);

            driver.DragNamedElementWithin(ScrollEditorId, $"1/{globalDivision} beat", 0.5, 0.2);
            driver.WaitForIdle();
            driver.SendKeyboardShortcut("Ctrl+S");
            driver.WaitForIdle();

            var updatedBpmTimes = GetBpmChangeTimes(difficultyPath);
            Assert.Single(updatedBpmTimes);
            Assert.True(Math.Abs(updatedBpmTimes[0] - initialBpmTimes[0]) > 0.01);
        });
    }

    [Fact]
    public void EditorHoldScrollModeIndicatorStartsHidden() {
        RunOpenedFixtureMapTest((driver, _) => {
            Assert.False(driver.IsVisible(ScrollEditorHoldIconId));
        });
    }

    private static void RunOpenedFixtureMapTest(Action<AvaloniaUIDriver, string> testBody) {
        var driver = new AvaloniaUIDriver();
        string? mapFolder = null;

        try {
            mapFolder = LaunchAndOpenFixtureMap(driver);
            testBody(driver, mapFolder);
        } finally {
            driver.Shutdown();
            SafeDeleteDirectory(mapFolder);
        }
    }

    private static string LaunchAndOpenFixtureMap(AvaloniaUIDriver driver) {
        var fixtureCopy = CreateFixtureMapCopy();

        driver.Launch();
        driver.WaitForIdle();
        driver.SetTestFileSelection(fixtureCopy);
        driver.ClickButton(StartupOpenMapButtonId);
        driver.WaitForMainWindow();

        return fixtureCopy;
    }

    private static string CreateFixtureMapCopy() {
        var fixtureSourcePath = Path.Combine(GetRepositoryRoot(), FixtureMapFolderRelative);
        Assert.True(Directory.Exists(fixtureSourcePath), $"MainWindow fixture map folder was not found: {fixtureSourcePath}");

        var fixtureCopyPath = CreateTempOutputFolder("grid-workflow-fixture");
        CopyDirectoryRecursively(fixtureSourcePath, fixtureCopyPath);
        return fixtureCopyPath;
    }

    private static string CreateTempOutputFolder(string tag) {
        var outputPath = Path.Combine(Path.GetTempPath(), "Edda-AvaloniaEditorGridWorkflowTests", tag, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outputPath);
        return outputPath;
    }

    private static string GetRepositoryRoot() {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null) {
            if (File.Exists(Path.Combine(current.FullName, "RagnarockEditor.sln"))) {
                return current.FullName;
            }
            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root (RagnarockEditor.sln).");
    }

    private static void CopyDirectoryRecursively(string sourcePath, string destinationPath) {
        foreach (var sourceDirectory in Directory.GetDirectories(sourcePath, "*", SearchOption.AllDirectories)) {
            var relativePath = Path.GetRelativePath(sourcePath, sourceDirectory);
            Directory.CreateDirectory(Path.Combine(destinationPath, relativePath));
        }

        foreach (var sourceFilePath in Directory.GetFiles(sourcePath, "*", SearchOption.AllDirectories)) {
            var relativePath = Path.GetRelativePath(sourcePath, sourceFilePath);
            var destinationFilePath = Path.Combine(destinationPath, relativePath);
            var destinationDirectory = Path.GetDirectoryName(destinationFilePath);
            if (!string.IsNullOrWhiteSpace(destinationDirectory)) {
                Directory.CreateDirectory(destinationDirectory);
            }
            File.Copy(sourceFilePath, destinationFilePath, overwrite: true);
        }
    }

    private static void SafeDeleteDirectory(string? directoryPath) {
        if (string.IsNullOrWhiteSpace(directoryPath) || !Directory.Exists(directoryPath)) {
            return;
        }

        try {
            Directory.Delete(directoryPath, true);
        } catch {
            // Best effort cleanup for temporary test paths.
        }
    }

    private static string GetPrimaryDifficultyMapPath(string mapFolder) {
        var infoPath = Path.Combine(mapFolder, "info.dat");
        if (!File.Exists(infoPath)) {
            return Path.Combine(mapFolder, "easy.dat");
        }

        using var doc = JsonDocument.Parse(File.ReadAllText(infoPath));
        if (doc.RootElement.TryGetProperty("_beatmapFilenames", out var filenames) &&
            filenames.ValueKind == JsonValueKind.Array &&
            filenames.GetArrayLength() > 0 &&
            filenames[0].ValueKind == JsonValueKind.String) {
            var fileName = filenames[0].GetString();
            if (!string.IsNullOrWhiteSpace(fileName)) {
                return Path.Combine(mapFolder, fileName);
            }
        }

        return Path.Combine(mapFolder, "easy.dat");
    }

    private static int GetBookmarksCount(string difficultyPath) {
        return ReadJsonArrayCount(difficultyPath, "_customData", "_bookmarks");
    }

    private static int GetBpmChangesCount(string difficultyPath) {
        return ReadJsonArrayCount(difficultyPath, "_customData", "_BPMChanges");
    }

    private static List<double> GetBookmarkTimes(string difficultyPath) {
        return ReadCustomDataDoubleValues(difficultyPath, "_bookmarks", "_time");
    }

    private static List<double> GetBpmChangeTimes(string difficultyPath) {
        return ReadCustomDataDoubleValues(difficultyPath, "_BPMChanges", "_time");
    }

    private static List<int> GetBpmChangeGridDivisions(string difficultyPath) {
        return ReadCustomDataIntValues(difficultyPath, "_BPMChanges", "_beatsPerBar");
    }

    private static int ReadJsonArrayCount(string filePath, params string[] path) {
        using var doc = JsonDocument.Parse(File.ReadAllText(filePath));
        var element = doc.RootElement;
        foreach (var segment in path) {
            if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(segment, out element)) {
                return 0;
            }
        }

        return element.ValueKind == JsonValueKind.Array ? element.GetArrayLength() : 0;
    }

    private static List<double> ReadCustomDataDoubleValues(string filePath, string arrayProperty, string valueProperty) {
        using var doc = JsonDocument.Parse(File.ReadAllText(filePath));
        if (!TryGetCustomDataArray(doc.RootElement, arrayProperty, out var array)) {
            return new List<double>();
        }

        var values = new List<double>();
        foreach (var item in array.EnumerateArray()) {
            if (item.ValueKind != JsonValueKind.Object || !item.TryGetProperty(valueProperty, out var valueElement)) {
                continue;
            }

            if (TryReadDouble(valueElement, out var value)) {
                values.Add(value);
            }
        }

        return values;
    }

    private static List<int> ReadCustomDataIntValues(string filePath, string arrayProperty, string valueProperty) {
        using var doc = JsonDocument.Parse(File.ReadAllText(filePath));
        if (!TryGetCustomDataArray(doc.RootElement, arrayProperty, out var array)) {
            return new List<int>();
        }

        var values = new List<int>();
        foreach (var item in array.EnumerateArray()) {
            if (item.ValueKind != JsonValueKind.Object || !item.TryGetProperty(valueProperty, out var valueElement)) {
                continue;
            }

            if (TryReadInt(valueElement, out var value)) {
                values.Add(value);
            }
        }

        return values;
    }

    private static bool TryGetCustomDataArray(JsonElement root, string arrayProperty, out JsonElement array) {
        array = default;
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("_customData", out var customData) ||
            customData.ValueKind != JsonValueKind.Object) {
            return false;
        }

        return customData.TryGetProperty(arrayProperty, out array) && array.ValueKind == JsonValueKind.Array;
    }

    private static bool TryReadDouble(JsonElement element, out double value) {
        if (element.ValueKind == JsonValueKind.Number) {
            return element.TryGetDouble(out value);
        }

        if (element.ValueKind == JsonValueKind.String) {
            var text = element.GetString();
            return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) ||
                double.TryParse(text, out value);
        }

        value = default;
        return false;
    }

    private static bool TryReadInt(JsonElement element, out int value) {
        if (element.ValueKind == JsonValueKind.Number) {
            return element.TryGetInt32(out value);
        }

        if (element.ValueKind == JsonValueKind.String) {
            var text = element.GetString();
            return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value) ||
                int.TryParse(text, out value);
        }

        value = default;
        return false;
    }

    private static int ParseIntegerText(string value, string controlId) {
        if (int.TryParse(value, out var parsed)) {
            return parsed;
        }

        throw new InvalidOperationException($"Expected integer text in '{controlId}', but got '{value}'.");
    }
}
