using System;
using System.Globalization;
using Xunit;

namespace Edda.Wpf.UI.Tests {
    public class ChangeBpmWindowTests {
        private const string SongBpmTextBoxId = "txtSongBPM";
        private const string SongProgressSliderId = "sliderSongProgress";
        private const string GridDivisionTextBoxId = "txtGridDivision";
        private const string ChangeBpmButtonId = "btnChangeBPM";

        private const string ChangeBpmGlobalValueId = "lblGlobalBPM";
        private const string ChangeBpmGridId = "dataBPMChange";
        private const string ChangeBpmExitButtonId = "btnExit";

        [Fact]
        public void ChangeBpmWindowDisplaysCurrentGlobalBpm() {
            WpfWindowTestHarness.RunOpenedFixtureMapTest((driver, _) => {
                var expectedBpm = driver.GetText(SongBpmTextBoxId);

                driver.ClickButton(ChangeBpmButtonId);
                driver.WaitForIdle();

                Assert.Equal(expectedBpm, driver.GetText(ChangeBpmGlobalValueId));
                Assert.True(driver.IsVisible(ChangeBpmGridId));
            });
        }

        [Fact]
        public void ChangeBpmGridShowsRowsForTimingChangesCreatedInMainWindow() {
            WpfWindowTestHarness.RunOpenedFixtureMapTest((driver, _) => {
                AddTimingChangeAt(driver, 2_000);
                AddTimingChangeAt(driver, 8_000);

                driver.ClickButton(ChangeBpmButtonId);
                driver.WaitForIdle();

                Assert.True(driver.GetDataGridRowCount(ChangeBpmGridId) >= 2);
            });
        }

        [Fact]
        public void ChangeBpmAddRowSeedsBeatBpmAndGridDivisionFromMainWindowState() {
            WpfWindowTestHarness.RunOpenedFixtureMapTest((driver, _) => {
                const double targetProgressMs = 30_000;
                const int targetDivision = 7;

                driver.SetSliderValue(SongProgressSliderId, targetProgressMs);
                driver.SetText(GridDivisionTextBoxId, targetDivision.ToString(CultureInfo.InvariantCulture));
                CommitTextboxEdits(driver);
                var expectedBpm = ParseDoubleCell(driver.GetText(SongBpmTextBoxId));
                var expectedBeat = Math.Round(targetProgressMs / 60000.0 * expectedBpm, 3);

                driver.ClickButton(ChangeBpmButtonId);
                driver.WaitForIdle();
                var initialRows = driver.GetDataGridRowCount(ChangeBpmGridId);

                var created = driver.TryAddDataGridNewItemRow(ChangeBpmGridId);
                Assert.True(created, "Expected to create a new BPM change row from DataGrid placeholder.");

                var finalRows = driver.GetDataGridRowCount(ChangeBpmGridId);
                Assert.Equal(initialRows + 1, finalRows);

                var rowIndex = finalRows - 1;
                var createdBeat = ParseDoubleCell(driver.GetDataGridCellText(ChangeBpmGridId, rowIndex, 0));
                var createdBpm = ParseDoubleCell(driver.GetDataGridCellText(ChangeBpmGridId, rowIndex, 1));
                var createdDivision = ParseDoubleCell(driver.GetDataGridCellText(ChangeBpmGridId, rowIndex, 2));

                Assert.InRange(Math.Abs(createdBeat - expectedBeat), 0, 0.01);
                Assert.InRange(Math.Abs(createdBpm - expectedBpm), 0, 0.01);
                Assert.Equal(targetDivision, (int)Math.Round(createdDivision));
            });
        }

        [Fact]
        public void ChangeBpmGridRejectsInvalidBpmValueAndKeepsPreviousValue() {
            WpfWindowTestHarness.RunOpenedFixtureMapTest((driver, _) => {
                AddTimingChangeAt(driver, 2_000);

                driver.ClickButton(ChangeBpmButtonId);
                driver.WaitForIdle();
                Assert.True(driver.GetDataGridRowCount(ChangeBpmGridId) > 0);

                var previousBpm = driver.GetDataGridCellText(ChangeBpmGridId, 0, 1);
                driver.SetDataGridCellText(ChangeBpmGridId, 0, 1, "-1");
                driver.InvokeCommand("DialogResult.Ok");
                driver.WaitForIdle();

                Assert.Equal(previousBpm, driver.GetDataGridCellText(ChangeBpmGridId, 0, 1));
            });
        }

        [Fact]
        public void ChangeBpmGridRejectsInvalidGlobalBeatAndGridDivisionValues() {
            WpfWindowTestHarness.RunOpenedFixtureMapTest((driver, _) => {
                AddTimingChangeAt(driver, 2_000);

                driver.ClickButton(ChangeBpmButtonId);
                driver.WaitForIdle();
                Assert.True(driver.GetDataGridRowCount(ChangeBpmGridId) > 0);

                var previousBeat = driver.GetDataGridCellText(ChangeBpmGridId, 0, 0);
                driver.SetDataGridCellText(ChangeBpmGridId, 0, 0, "-1");
                driver.InvokeCommand("DialogResult.Ok");
                driver.WaitForIdle();
                Assert.Equal(previousBeat, driver.GetDataGridCellText(ChangeBpmGridId, 0, 0));

                var previousDivision = driver.GetDataGridCellText(ChangeBpmGridId, 0, 2);
                driver.SetDataGridCellText(ChangeBpmGridId, 0, 2, "0");
                driver.InvokeCommand("DialogResult.Ok");
                driver.WaitForIdle();
                Assert.Equal(previousDivision, driver.GetDataGridCellText(ChangeBpmGridId, 0, 2));
            });
        }

        [Fact]
        public void ChangeBpmDeleteRemovesSelectedRowsAndPersistsAfterReopen() {
            WpfWindowTestHarness.RunOpenedFixtureMapTest((driver, _) => {
                AddTimingChangeAt(driver, 2_000);
                AddTimingChangeAt(driver, 8_000);

                driver.ClickButton(ChangeBpmButtonId);
                driver.WaitForIdle();
                var initialRows = driver.GetDataGridRowCount(ChangeBpmGridId);
                Assert.True(initialRows >= 2);

                driver.SelectDataGridRow(ChangeBpmGridId, 0);
                driver.SendKeyboardShortcut("Delete");
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
            WpfWindowTestHarness.RunOpenedFixtureMapTest((driver, _) => {
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
        public void ChangeBpmWindowExitClosesWindow() {
            WpfWindowTestHarness.RunOpenedFixtureMapTest((driver, _) => {
                driver.ClickButton(ChangeBpmButtonId);
                driver.WaitForIdle();
                Assert.True(driver.IsVisible(ChangeBpmExitButtonId));

                driver.ClickButton(ChangeBpmExitButtonId);
                driver.WaitForIdle();
                Assert.False(driver.IsVisible(ChangeBpmExitButtonId));
            });
        }

        private static void AddTimingChangeAt(WpfUIDriver driver, double songProgressMilliseconds) {
            driver.SetSliderValue(SongProgressSliderId, songProgressMilliseconds);
            driver.WaitForIdle();
            driver.SendKeyboardShortcut("Ctrl+T");
            driver.WaitForIdle();
        }

        private static void AssertRowsSortedByGlobalBeat(WpfUIDriver driver) {
            var firstBeat = ParseDoubleCell(driver.GetDataGridCellText(ChangeBpmGridId, 0, 0));
            var secondBeat = ParseDoubleCell(driver.GetDataGridCellText(ChangeBpmGridId, 1, 0));
            Assert.True(firstBeat <= secondBeat, $"Expected sorted beats but got {firstBeat} and {secondBeat}.");
        }

        private static double ParseDoubleCell(string value) {
            if (double.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out var parsed)) {
                return parsed;
            }

            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed)) {
                return parsed;
            }

            throw new InvalidOperationException($"Could not parse DataGrid cell value '{value}' as double.");
        }

        private static void CommitTextboxEdits(WpfUIDriver driver) {
            driver.SendKeyboardShortcut("Enter");
            driver.SendKeyboardShortcut("Tab");
            driver.WaitForIdle();
        }
    }
}
