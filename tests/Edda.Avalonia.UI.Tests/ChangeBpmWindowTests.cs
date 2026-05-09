using System;
using System.Globalization;
using System.Drawing;
using Xunit;

namespace Edda.Avalonia.UI.Tests;

public class ChangeBpmWindowTests {
    const string SongBpmTextBoxId = "txtSongBPM";
    const string SongProgressSliderId = "sliderSongProgress";
    const string GridDivisionTextBoxId = "txtGridDivision";
    const string ChangeBpmButtonId = "btnChangeBPM";
    const string ScrollEditorId = "scrollEditor";

    const string ChangeBpmGlobalValueId = "lblGlobalBPM";
    const string ChangeBpmGridId = "dataBPMChange";
    const string ChangeBpmExitButtonId = "btnExit";

    [Fact]
    public void ChangeBpmWindowDisplaysCurrentGlobalBpm() {
        AvaloniaWindowTestHarness.RunOpenedFixtureMapTest((driver, _) => {
            var expectedBpm = driver.GetText(SongBpmTextBoxId);

            driver.ClickButton(ChangeBpmButtonId);
            driver.WaitForIdle();

            Assert.Equal(expectedBpm, driver.GetText(ChangeBpmGlobalValueId));
            Assert.True(driver.IsVisible(ChangeBpmGridId));
        });
    }

    [Fact]
    public void ChangeBpmGridShowsRowsForTimingChangesCreatedInMainWindow() {
        AvaloniaWindowTestHarness.RunOpenedFixtureMapTest((driver, _) => {
            AddTimingChangeAt(driver, 2_000);
            AddTimingChangeAt(driver, 8_000);

            driver.ClickButton(ChangeBpmButtonId);
            driver.WaitForIdle();

            Assert.True(driver.GetDataGridRowCount(ChangeBpmGridId) >= 2);
        });
    }

    [Fact]
    public void NewTimingChangeSeedsBeatBpmAndGridDivisionFromMainWindowState() {
        AvaloniaWindowTestHarness.RunOpenedFixtureMapTest((driver, _) => {
            const double targetProgressMs = 30_000;
            const int targetDivision = 7;

            driver.SetSliderValue(SongProgressSliderId, targetProgressMs);
            driver.SetText(GridDivisionTextBoxId, targetDivision.ToString(CultureInfo.InvariantCulture));
            CommitTextboxEdits(driver);
            var actualProgressMs = driver.GetSliderValue(SongProgressSliderId);
            var expectedBpm = ParseDoubleCell(driver.GetText(SongBpmTextBoxId));
            var expectedBeat = Math.Round(actualProgressMs / 60000.0 * expectedBpm, 3);

            driver.SelectMenuItem("Edit>Add New>Timing Change");
            driver.WaitForIdle();
            driver.ClickButton(ChangeBpmButtonId);
            driver.WaitForIdle();
            Assert.True(driver.GetDataGridRowCount(ChangeBpmGridId) > 0);

            var rowIndex = driver.GetDataGridRowCount(ChangeBpmGridId) - 1;
            var createdBeat = ParseDoubleCell(driver.GetDataGridCellText(ChangeBpmGridId, rowIndex, 0));
            var createdBpm = ParseDoubleCell(driver.GetDataGridCellText(ChangeBpmGridId, rowIndex, 1));
            var createdDivision = ParseDoubleCell(driver.GetDataGridCellText(ChangeBpmGridId, rowIndex, 2));

            Assert.InRange(Math.Abs(createdBeat - expectedBeat), 0, 0.01);
            Assert.InRange(Math.Abs(createdBpm - expectedBpm), 0, 0.01);
            Assert.Equal(targetDivision, (int)Math.Round(createdDivision));
        });
    }

    [Fact]
    public void ChangeBpmGridEditsBpmValueAndPersistsAfterReopen() {
        AvaloniaWindowTestHarness.RunOpenedFixtureMapTest((driver, _) => {
            AddTimingChangeAt(driver, 2_000);

            driver.ClickButton(ChangeBpmButtonId);
            driver.WaitForIdle();
            Assert.True(driver.GetDataGridRowCount(ChangeBpmGridId) > 0);

            driver.SetDataGridCellText(ChangeBpmGridId, 0, 1, "150");
            driver.WaitForIdle();
            Assert.InRange(Math.Abs(ParseDoubleCell(driver.GetDataGridCellText(ChangeBpmGridId, 0, 1)) - 150), 0, 0.01);

            driver.ClickButton(ChangeBpmExitButtonId);
            driver.WaitForIdle();
            driver.ClickButton(ChangeBpmButtonId);
            driver.WaitForIdle();
            Assert.InRange(Math.Abs(ParseDoubleCell(driver.GetDataGridCellText(ChangeBpmGridId, 0, 1)) - 150), 0, 0.01);
        });
    }

    [Fact]
    public void ChangeBpmGridEditsGlobalBeatAndGridDivisionValues() {
        AvaloniaWindowTestHarness.RunOpenedFixtureMapTest((driver, _) => {
            AddTimingChangeAt(driver, 2_000);

            driver.ClickButton(ChangeBpmButtonId);
            driver.WaitForIdle();
            Assert.True(driver.GetDataGridRowCount(ChangeBpmGridId) > 0);

            driver.SetDataGridCellText(ChangeBpmGridId, 0, 0, "1.5");
            driver.WaitForIdle();
            Assert.InRange(Math.Abs(ParseDoubleCell(driver.GetDataGridCellText(ChangeBpmGridId, 0, 0)) - 1.5), 0, 0.01);

            driver.SetDataGridCellText(ChangeBpmGridId, 0, 2, "8");
            driver.WaitForIdle();
            Assert.Equal(8, (int)Math.Round(ParseDoubleCell(driver.GetDataGridCellText(ChangeBpmGridId, 0, 2))));
        });
    }

    [Fact]
    public void ChangeBpmDeleteRemovesSelectedRowsAndPersistsAfterReopen() {
        AvaloniaWindowTestHarness.RunOpenedFixtureMapTest((driver, _) => {
            AddTimingChangeAt(driver, 2_000);
            AddTimingChangeAt(driver, 8_000);

            driver.ClickButton(ChangeBpmButtonId);
            driver.WaitForIdle();
            var initialRows = driver.GetDataGridRowCount(ChangeBpmGridId);
            Assert.True(initialRows >= 2);

            driver.SelectDataGridRow(ChangeBpmGridId, 0);
            driver.SendKeyboardShortcutToElement(ChangeBpmGridId, "Delete");
            driver.WaitForIdle();
            Assert.Equal(initialRows - 1, driver.GetDataGridRowCount(ChangeBpmGridId));

            driver.ClickButton(ChangeBpmExitButtonId);
            driver.WaitForIdle();
            driver.ClickButton(ChangeBpmButtonId);
            driver.WaitForIdle();
            Assert.Equal(initialRows - 1, driver.GetDataGridRowCount(ChangeBpmGridId));
        });
    }

    [Fact]
    public void ChangeBpmRowCommitSortsByGlobalBeatAndPersistsAfterReopen() {
        AvaloniaWindowTestHarness.RunOpenedFixtureMapTest((driver, _) => {
            AddTimingChangeAt(driver, 2_000);
            AddTimingChangeAt(driver, 8_000);

            driver.ClickButton(ChangeBpmButtonId);
            driver.WaitForIdle();
            Assert.True(driver.GetDataGridRowCount(ChangeBpmGridId) >= 2);

            driver.SetDataGridCellText(ChangeBpmGridId, 0, 0, "12");
            driver.WaitForIdle();
            driver.SetDataGridCellText(ChangeBpmGridId, 1, 0, "1");
            driver.WaitForIdle();

            AssertRowsSortedByGlobalBeat(driver);

            driver.ClickButton(ChangeBpmExitButtonId);
            driver.WaitForIdle();
            driver.ClickButton(ChangeBpmButtonId);
            driver.WaitForIdle();

            AssertRowsSortedByGlobalBeat(driver);
        });
    }

    [Fact]
    public void ChangeBpmWindowAddsNewRowsFromWindowAndPersistsAfterReopen() {
        AvaloniaWindowTestHarness.RunOpenedFixtureMapTest((driver, _) => {
            driver.ClickButton(ChangeBpmButtonId);
            driver.WaitForIdle();
            var initialRows = driver.GetDataGridRowCount(ChangeBpmGridId);

            Assert.True(driver.TryAddDataGridNewItemRow(ChangeBpmGridId, "3", "180", "6"));
            Assert.Equal(initialRows + 1, driver.GetDataGridRowCount(ChangeBpmGridId));

            driver.ClickButton(ChangeBpmExitButtonId);
            driver.WaitForIdle();
            driver.ClickButton(ChangeBpmButtonId);
            driver.WaitForIdle();

            Assert.Equal(initialRows + 1, driver.GetDataGridRowCount(ChangeBpmGridId));
        });
    }

    [Fact]
    public void ChangeBpmWindowShowsTimingColumnsAndFooterAction() {
        AvaloniaWindowTestHarness.RunOpenedFixtureMapTest((driver, _) => {
            driver.ClickButton(ChangeBpmButtonId);
            driver.WaitForIdle();

            Assert.True(driver.ContainsText("Timing Changes:"));
            Assert.True(driver.ContainsText("Global Beat"));
            Assert.True(driver.ContainsText("BPM"));
            Assert.True(driver.ContainsText("Beat Division"));
            Assert.True(driver.IsVisible(ChangeBpmExitButtonId));
        });
    }

    [Fact]
    public void ChangeBpmEditsRefreshMainEditorGridWhileWindowStaysOpen() {
        AvaloniaWindowTestHarness.RunOpenedFixtureMapTest((driver, _) => {
            AddTimingChangeAt(driver, 2_000);
            driver.ClickButton(ChangeBpmButtonId);
            driver.WaitForIdle();

            using var before = driver.CaptureElementBitmap(ScrollEditorId);
            driver.SetDataGridCellText(ChangeBpmGridId, 0, 2, "8");
            driver.WaitForIdle();
            using var after = driver.CaptureElementBitmap(ScrollEditorId);

            var diff = GetMeanAbsoluteRgbDifference(before, after);
            Assert.True(diff > 1.25, $"Expected Change BPM edits to refresh main editor grid before closing the window, but mean RGB difference was only {diff:0.##}.");
        });
    }

    [Fact]
    public void ChangeBpmWindowExitClosesWindow() {
        AvaloniaWindowTestHarness.RunOpenedFixtureMapTest((driver, _) => {
            driver.ClickButton(ChangeBpmButtonId);
            driver.WaitForIdle();
            Assert.True(driver.IsVisible(ChangeBpmExitButtonId));

            driver.ClickButton(ChangeBpmExitButtonId);
            driver.WaitForIdle();
            Assert.False(driver.IsVisible(ChangeBpmExitButtonId));
        });
    }

    static void AddTimingChangeAt(AvaloniaUIDriver driver, double songProgressMilliseconds) {
        driver.SetSliderValue(SongProgressSliderId, songProgressMilliseconds);
        driver.WaitForIdle();
        driver.SelectMenuItem("Edit>Add New>Timing Change");
    }

    static void AssertRowsSortedByGlobalBeat(AvaloniaUIDriver driver) {
        var firstBeat = ParseDoubleCell(driver.GetDataGridCellText(ChangeBpmGridId, 0, 0));
        var secondBeat = ParseDoubleCell(driver.GetDataGridCellText(ChangeBpmGridId, 1, 0));
        Assert.True(firstBeat <= secondBeat, $"Expected sorted beats but got {firstBeat} and {secondBeat}.");
    }

    static double GetMeanAbsoluteRgbDifference(Bitmap before, Bitmap after) {
        var width = Math.Min(before.Width, after.Width);
        var height = Math.Min(before.Height, after.Height);
        double total = 0;
        var samples = 0;

        for (var y = 0; y < height; y++) {
            for (var x = 0; x < width; x++) {
                var left = before.GetPixel(x, y);
                var right = after.GetPixel(x, y);
                total += (Math.Abs(left.R - right.R) + Math.Abs(left.G - right.G) + Math.Abs(left.B - right.B)) / 3.0;
                samples++;
            }
        }

        return samples == 0 ? 0 : total / samples;
    }

    static double ParseDoubleCell(string value) {
        if (double.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out var parsed)) {
            return parsed;
        }

        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed)) {
            return parsed;
        }

        throw new InvalidOperationException($"Could not parse DataGrid cell value '{value}' as double.");
    }

    static void CommitTextboxEdits(AvaloniaUIDriver driver) {
        driver.SendKeyboardShortcut("Enter");
        driver.SendKeyboardShortcut("Tab");
        driver.WaitForIdle();
    }
}
