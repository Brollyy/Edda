using System;
using System.IO;
using Xunit;

namespace Edda.Wpf.UI.Tests {
    public class StartWindowTests {
        private const string StartWindowId = "StartWindow";
        private const string MainWindowId = "AppMainWindow";

        private const string StartupOpenMapButtonId = "ButtonOpenMap";
        private const string StartupNewMapButtonId = "ButtonNewMap";
        private const string StartupImportMapButtonId = "ButtonImportMap";
        private const string StartupExitButtonId = "ButtonExit";
        private const string StartupDragSurfaceId = "InvisibleTitleBar";
        private const string StartupVersionTextId = "TxtVersionNumber";
        private const string StartupRecentMapsListId = "ListViewRecentMaps";

        private const string SongNameTextBoxId = "txtSongName";
        private const string SongFileNameTextId = "txtSongFileName";
        private const string FixtureMapFolderRelative = "tests/TestData/Wpf/MainWindow/FixtureMap";

        [Fact]
        public void StartWindowShowsVersionAndPrimaryActions() {
            var driver = new WpfUIDriver();
            try {
                driver.Launch();
                driver.WaitForIdle();

                Assert.True(driver.IsVisible(StartWindowId));
                Assert.False(string.IsNullOrWhiteSpace(driver.GetText(StartupVersionTextId)));
                Assert.True(driver.IsEnabled(StartupOpenMapButtonId));
                Assert.True(driver.IsEnabled(StartupNewMapButtonId));
                Assert.True(driver.IsEnabled(StartupImportMapButtonId));
            } finally {
                driver.Shutdown();
            }
        }

        [Fact]
        public void StartWindowOpenMapButtonLoadsMainWindowForSelectedFolder() {
            var driver = new WpfUIDriver();
            string? mapFolder = null;

            try {
                mapFolder = WpfWindowTestHarness.CreateFixtureMapCopy();
                driver.Launch();
                driver.WaitForIdle();

                driver.SetTestFileSelection(mapFolder);
                driver.ClickButton(StartupOpenMapButtonId);
                driver.WaitForMainWindow();

                Assert.True(driver.IsVisible(MainWindowId), $"{driver.GetWindowDebugSummary()}{Environment.NewLine}{driver.GetTestLog()}");
                Assert.False(driver.IsVisible(StartWindowId));
            } finally {
                driver.Shutdown();
                WpfWindowTestHarness.SafeDeleteDirectory(mapFolder);
            }
        }

        [Fact]
        public void StartWindowExitButtonTerminatesProcess() {
            var driver = new WpfUIDriver();
            try {
                driver.Launch();
                driver.WaitForIdle();

                try {
                    driver.ClickButton(StartupExitButtonId);
                } catch (InvalidOperationException ex) when (ex.Message.Contains("exited unexpectedly", StringComparison.OrdinalIgnoreCase)) {
                    // Expected when app exits during post-click idle wait.
                }

                Assert.True(driver.WaitForExit(TimeSpan.FromSeconds(3)));
                Assert.False(driver.IsProcessRunning());
            } finally {
                driver.Shutdown();
            }
        }

        [Fact]
        public void StartWindowDragSurfaceMovesWindow() {
            var driver = new WpfUIDriver();
            try {
                driver.Launch();
                driver.WaitForIdle();

                var before = driver.GetElementBounds(StartWindowId);
                driver.DragWithinElement(StartWindowId, 0.5, 0.05, 0.8, 0.2);
                driver.WaitForIdle();
                var after = driver.GetElementBounds(StartWindowId);

                var movedX = Math.Abs(after.left - before.left);
                var movedY = Math.Abs(after.top - before.top);
                Assert.True(movedX > 5 || movedY > 5, $"Expected StartWindow to move, but delta was ({movedX:0.##}, {movedY:0.##}).");
            } finally {
                driver.Shutdown();
            }
        }

        [Fact]
        public void StartWindowNewMapButtonCreatesMapFromSelectedSong() {
            var driver = new WpfUIDriver();
            string? newMapFolder = null;
            string? pickerSourceFolder = null;

            try {
                newMapFolder = WpfWindowTestHarness.CreateAlphabeticMapFolder("start-new-map-picker");
                pickerSourceFolder = WpfWindowTestHarness.CreateTempOutputFolder("start-new-map-song-source");

                var sourceSongPath = Path.Combine(WpfWindowTestHarness.GetRepositoryRoot(), FixtureMapFolderRelative, "song.ogg");
                var pickedSongName = "song.ogg";
                var pickedSongPath = Path.Combine(pickerSourceFolder, pickedSongName);
                File.Copy(sourceSongPath, pickedSongPath, overwrite: true);

                driver.Launch();
                driver.WaitForIdle();
                driver.SetTestFileSelections(newMapFolder, pickedSongPath);
                driver.ClickButton(StartupNewMapButtonId);
                driver.WaitForMainWindow();

                Assert.True(driver.IsVisible(MainWindowId));
                Assert.Equal(pickedSongName, driver.GetText(SongFileNameTextId));
                Assert.True(File.Exists(Path.Combine(newMapFolder, "info.dat")));
                Assert.True(File.Exists(Path.Combine(newMapFolder, pickedSongName)));
            } finally {
                driver.Shutdown();
                WpfWindowTestHarness.SafeDeleteDirectory(newMapFolder);
                WpfWindowTestHarness.SafeDeleteDirectory(pickerSourceFolder);
            }
        }

        [Fact]
        public void StartWindowImportMapButtonCreatesAndLoadsConvertedMap() {
            var driver = new WpfUIDriver();
            string? importTargetFolder = null;
            string? importSourceFolder = null;

            try {
                importTargetFolder = WpfWindowTestHarness.CreateAlphabeticMapFolder("start-import-map-picker");
                var importFixture = WpfWindowTestHarness.CreateStepManiaImportFixture("Start Import Song");
                importSourceFolder = importFixture.fixtureFolder;

                driver.Launch();
                driver.WaitForIdle();
                driver.SetTestFileSelections(importTargetFolder, importFixture.simfilePath);
                driver.ClickButton(StartupImportMapButtonId);
                driver.WaitForMainWindow();

                Assert.True(driver.IsVisible(MainWindowId));
                Assert.Equal("Start Import Song", driver.GetText(SongNameTextBoxId));
                Assert.True(File.Exists(Path.Combine(importTargetFolder, "info.dat")));
                Assert.True(File.Exists(Path.Combine(importTargetFolder, "song.ogg")));
            } finally {
                driver.Shutdown();
                WpfWindowTestHarness.SafeDeleteDirectory(importTargetFolder);
                WpfWindowTestHarness.SafeDeleteDirectory(importSourceFolder);
            }
        }

        [Fact]
        public void StartWindowRecentMapLeftClickLoadsMap() {
            var driver = new WpfUIDriver();
            string? mapFolder = null;

            try {
                mapFolder = WpfWindowTestHarness.CreateFixtureMapCopy();
                driver.Launch();
                driver.WaitForIdle();
                driver.SetTestFileSelection(mapFolder);
                driver.ClickButton(StartupOpenMapButtonId);
                driver.WaitForMainWindow();

                driver.SendKeyboardShortcut("Ctrl+W");
                driver.WaitForStartWindow();
                Assert.True(driver.IsVisible(StartWindowId));
                Assert.True(driver.ContainsText(mapFolder));

                driver.ClickListItemContainingText(StartupRecentMapsListId, mapFolder);
                driver.WaitForMainWindow();
                Assert.True(driver.IsVisible(MainWindowId));
                Assert.False(driver.IsVisible(StartWindowId));
            } finally {
                driver.Shutdown();
                WpfWindowTestHarness.SafeDeleteDirectory(mapFolder);
            }
        }

        [Fact]
        public void StartWindowRecentMapRightClickRespectsNoAndYesConfirmation() {
            var driver = new WpfUIDriver();
            string? mapFolder = null;

            try {
                mapFolder = WpfWindowTestHarness.CreateFixtureMapCopy();
                driver.Launch();
                driver.WaitForIdle();
                driver.SetTestFileSelection(mapFolder);
                driver.ClickButton(StartupOpenMapButtonId);
                driver.WaitForMainWindow();

                driver.SendKeyboardShortcut("Ctrl+W");
                driver.WaitForStartWindow();
                Assert.True(driver.IsVisible(StartWindowId));
                Assert.True(driver.ContainsText(mapFolder));
                var initialCount = driver.GetListItemCount(StartupRecentMapsListId);

                driver.RightClickListItemContainingText(StartupRecentMapsListId, mapFolder);
                driver.InvokeCommand("DialogResult.No");
                Assert.True(driver.ContainsText(mapFolder));
                Assert.Equal(initialCount, driver.GetListItemCount(StartupRecentMapsListId));

                driver.RightClickListItemContainingText(StartupRecentMapsListId, mapFolder);
                driver.InvokeCommand("DialogResult.Yes");
                Assert.False(driver.ContainsText(mapFolder));
                Assert.Equal(initialCount - 1, driver.GetListItemCount(StartupRecentMapsListId));
            } finally {
                driver.Shutdown();
                WpfWindowTestHarness.SafeDeleteDirectory(mapFolder);
            }
        }

        [Fact]
        public void StartWindowFailedRecentOpenRemovesStaleEntry() {
            var driver = new WpfUIDriver();
            string? mapFolder = null;
            string? staleMapPath = null;

            try {
                mapFolder = WpfWindowTestHarness.CreateFixtureMapCopy();
                staleMapPath = Path.GetFullPath(mapFolder);
                driver.Launch();
                driver.WaitForIdle();
                driver.SetTestFileSelection(mapFolder);
                driver.ClickButton(StartupOpenMapButtonId);
                driver.WaitForMainWindow();

                driver.SendKeyboardShortcut("Ctrl+W");
                driver.WaitForStartWindow();
                Assert.True(driver.IsVisible(StartWindowId));
                Assert.True(driver.ContainsText(staleMapPath));

                WpfWindowTestHarness.SafeDeleteDirectory(staleMapPath);
                mapFolder = null;

                driver.ClickListItemContainingText(StartupRecentMapsListId, staleMapPath);
                driver.InvokeCommand("DialogResult.Ok");
                driver.WaitForStartWindow();

                Assert.True(driver.IsVisible(StartWindowId));
                Assert.False(driver.ContainsText(staleMapPath));
            } finally {
                driver.Shutdown();
                WpfWindowTestHarness.SafeDeleteDirectory(mapFolder);
            }
        }
    }
}