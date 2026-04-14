using System;
using System.IO;
using System.Linq;
using System.Windows.Automation;
using Xunit;

namespace Edda.Avalonia.UI.Tests;

public class StartWindowTests {
    private const string StartWindowId = "StartWindow";
    private const string MainWindowId = "AppMainWindow";

    private const string StartupOpenMapButtonId = "ButtonOpenMap";
    private const string StartupNewMapButtonId = "ButtonNewMap";
    private const string StartupImportMapButtonId = "ButtonImportMap";
    private const string StartupExitButtonId = "ButtonExit";
    private const string StartupVersionTextId = "TxtVersionNumber";
    private const string StartupRecentMapsListId = "ListViewRecentMaps";

    private const string SongNameTextBoxId = "txtSongName";
    private const string SongFileNameTextId = "txtSongFileName";
    private const string FixtureMapFolderRelative = "tests/TestData/Wpf/MainWindow/FixtureMap";

    [Fact]
    public void StartWindowShowsVersionAndPrimaryActions() {
        var driver = new AvaloniaUIDriver();
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
    public void StartWindowFreshProfileShowsExpectedTextAndNoPlaceholderRecentMaps() {
        var driver = new AvaloniaUIDriver();
        try {
            driver.Launch();
            driver.WaitForIdle();

            var versionText = driver.GetText(StartupVersionTextId);
            Assert.True(versionText.StartsWith("version ", StringComparison.OrdinalIgnoreCase), $"Expected version text to start with 'version ', but got '{versionText}'.");
            Assert.DoesNotContain("X.X.X", versionText);
            Assert.Equal(0, driver.GetListItemCount(StartupRecentMapsListId));

            Assert.True(driver.ContainsText("Edda"));
            Assert.True(driver.ContainsText("New Map"));
            Assert.True(driver.ContainsText("Create a new map"));
            Assert.True(driver.ContainsText("Import Map"));
            Assert.True(driver.ContainsText("Import StepMania simfiles"));
            Assert.True(driver.ContainsText("Open Map"));
            Assert.True(driver.ContainsText("Continue working on an existing map"));
            Assert.True(driver.ContainsText("Recent Maps"));
            Assert.False(driver.ContainsText("C:/SongPath/SongPath"));
        } finally {
            driver.Shutdown();
        }
    }

    [Fact]
    public void StartWindowPrimaryActionsAndRecentMapsAreArrangedForLaunchFlow() {
        var driver = new AvaloniaUIDriver();
        try {
            driver.Launch();
            driver.WaitForIdle();

            var startWindow = driver.GetElementBounds(StartWindowId);
            var exitButton = driver.GetElementBounds(StartupExitButtonId);
            var versionText = driver.GetElementBounds(StartupVersionTextId);
            var newMapButton = driver.GetElementBounds(StartupNewMapButtonId);
            var importMapButton = driver.GetElementBounds(StartupImportMapButtonId);
            var openMapButton = driver.GetElementBounds(StartupOpenMapButtonId);
            var recentMapsList = driver.GetElementBounds(StartupRecentMapsListId);

            Assert.True(CenterX(versionText) < newMapButton.left, $"Expected version text to sit in the left column, but version center {CenterX(versionText):0.##} was not left of the New Map button {newMapButton.left:0.##}.");
            Assert.True(Math.Abs(CenterY(newMapButton) - CenterY(importMapButton)) < 25, $"Expected New Map and Import Map buttons to share a row, but centers were {CenterY(newMapButton):0.##} and {CenterY(importMapButton):0.##}.");
            Assert.True(RightEdge(newMapButton) <= importMapButton.left + 15, $"Expected New Map and Import Map buttons to sit side-by-side without overlap, but New Map right edge {RightEdge(newMapButton):0.##} exceeded Import Map left edge {importMapButton.left:0.##}.");
            Assert.True(openMapButton.top >= Math.Max(BottomEdge(newMapButton), BottomEdge(importMapButton)) - 10, $"Expected Open Map button to sit below the top action row, but its top edge was {openMapButton.top:0.##}.");
            Assert.True(openMapButton.width > newMapButton.width * 1.5, $"Expected Open Map button to be the full-width primary action, but widths were Open={openMapButton.width:0.##}, New={newMapButton.width:0.##}.");
            Assert.True(openMapButton.width > importMapButton.width * 1.5, $"Expected Open Map button to be wider than Import Map, but widths were Open={openMapButton.width:0.##}, Import={importMapButton.width:0.##}.");
            Assert.True(recentMapsList.top >= BottomEdge(openMapButton) - 10, $"Expected Recent Maps list to sit below Open Map, but list top {recentMapsList.top:0.##} and Open Map bottom {BottomEdge(openMapButton):0.##} did not line up.");
            Assert.True(exitButton.top <= startWindow.top + 40, $"Expected Exit button near the top edge, but top was {exitButton.top:0.##} for window top {startWindow.top:0.##}.");
            Assert.True(RightEdge(exitButton) >= RightEdge(startWindow) - 40, $"Expected Exit button near the right edge, but right edge was {RightEdge(exitButton):0.##} for window right edge {RightEdge(startWindow):0.##}.");
        } finally {
            driver.Shutdown();
        }
    }

    [Fact]
    public void StartWindowShowsBrandLogoAndActionIcons() {
        var driver = new AvaloniaUIDriver();
        try {
            driver.Launch();
            driver.WaitForIdle();

            var versionText = driver.GetElementBounds(StartupVersionTextId);
            var newMapButton = driver.GetElementBounds(StartupNewMapButtonId);
            var importMapButton = driver.GetElementBounds(StartupImportMapButtonId);
            var openMapButton = driver.GetElementBounds(StartupOpenMapButtonId);
            var images = driver.GetVisibleDescendantBoundsWithin(StartWindowId, ControlType.Image);
            var largestImage = images.OrderByDescending(bounds => bounds.width * bounds.height).FirstOrDefault();

            Assert.True(images.Count >= 4, $"Expected StartWindow branding and actions to expose at least four visible images, but found {images.Count}.");
            Assert.True(largestImage.width >= 80 && largestImage.height >= 80, $"Expected largest StartWindow image to be logo-sized, but it was {largestImage.width:0.##}x{largestImage.height:0.##}.");
            Assert.True(BottomEdge(largestImage) <= versionText.top + 10, $"Expected brand logo to sit above version text, but logo bottom was {BottomEdge(largestImage):0.##} and version top was {versionText.top:0.##}.");
            Assert.True(RightEdge(largestImage) < newMapButton.left, $"Expected brand logo to stay in left column, but logo right edge was {RightEdge(largestImage):0.##} and New Map left edge was {newMapButton.left:0.##}.");
            Assert.True(images.Any(image => CenterX(image) >= newMapButton.left && CenterX(image) <= RightEdge(newMapButton)), "Expected New Map button to include a visible icon.");
            Assert.True(images.Any(image => CenterX(image) >= importMapButton.left && CenterX(image) <= RightEdge(importMapButton)), "Expected Import Map button to include a visible icon.");
            Assert.True(images.Any(image => CenterX(image) >= openMapButton.left && CenterX(image) <= RightEdge(openMapButton)), "Expected Open Map button to include a visible icon.");
        } finally {
            driver.Shutdown();
        }
    }

    [Fact]
    public void StartWindowOpenMapButtonLoadsMainWindowForSelectedFolder() {
        var driver = new AvaloniaUIDriver();
        string? mapFolder = null;

        try {
            mapFolder = AvaloniaWindowTestHarness.CreateFixtureMapCopy();
            driver.Launch();
            driver.WaitForIdle();

            driver.SetTestFileSelection(mapFolder);
            driver.ClickButton(StartupOpenMapButtonId);
            driver.WaitForMainWindow();

            Assert.True(driver.IsVisible(MainWindowId));
            Assert.False(driver.IsVisible(StartWindowId));
        } finally {
            driver.Shutdown();
            AvaloniaWindowTestHarness.SafeDeleteDirectory(mapFolder);
        }
    }

    [Fact]
    public void StartWindowExitButtonTerminatesProcess() {
        var driver = new AvaloniaUIDriver();
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
        var driver = new AvaloniaUIDriver();
        try {
            driver.Launch();
            driver.WaitForIdle();

            var before = driver.GetElementBounds(StartWindowId);
            driver.DragWithinElement(StartWindowId, 0.35, 0.05, 0.75, 0.05);
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
        var driver = new AvaloniaUIDriver();
        string? newMapFolder = null;
        string? pickerSourceFolder = null;

        try {
            newMapFolder = AvaloniaWindowTestHarness.CreateAlphabeticMapFolder("start-new-map-picker");
            pickerSourceFolder = AvaloniaWindowTestHarness.CreateTempOutputFolder("start-new-map-song-source");

            var sourceSongPath = Path.Combine(AvaloniaWindowTestHarness.GetRepositoryRoot(), FixtureMapFolderRelative, "song.ogg");
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
            AvaloniaWindowTestHarness.SafeDeleteDirectory(newMapFolder);
            AvaloniaWindowTestHarness.SafeDeleteDirectory(pickerSourceFolder);
        }
    }

    [Fact]
    public void StartWindowImportMapButtonCreatesAndLoadsConvertedMap() {
        var driver = new AvaloniaUIDriver();
        string? importTargetFolder = null;
        string? importSourceFolder = null;

        try {
            importTargetFolder = AvaloniaWindowTestHarness.CreateAlphabeticMapFolder("start-import-map-picker");
            var importFixture = AvaloniaWindowTestHarness.CreateStepManiaImportFixture("Start Import Song");
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
            AvaloniaWindowTestHarness.SafeDeleteDirectory(importTargetFolder);
            AvaloniaWindowTestHarness.SafeDeleteDirectory(importSourceFolder);
        }
    }

    [Fact]
    public void StartWindowRecentMapLeftClickLoadsMap() {
        var driver = new AvaloniaUIDriver();
        string? mapFolder = null;

        try {
            mapFolder = AvaloniaWindowTestHarness.CreateFixtureMapCopy();
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
            AvaloniaWindowTestHarness.SafeDeleteDirectory(mapFolder);
        }
    }

    [Fact]
    public void StartWindowRecentMapRightClickRespectsNoAndYesConfirmation() {
        var driver = new AvaloniaUIDriver();
        string? mapFolder = null;

        try {
            mapFolder = AvaloniaWindowTestHarness.CreateFixtureMapCopy();
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
            AvaloniaWindowTestHarness.SafeDeleteDirectory(mapFolder);
        }
    }

    [Fact]
    public void StartWindowFailedRecentOpenRemovesStaleEntry() {
        var driver = new AvaloniaUIDriver();
        string? mapFolder = null;
        string? staleMapPath = null;

        try {
            mapFolder = AvaloniaWindowTestHarness.CreateFixtureMapCopy();
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

            AvaloniaWindowTestHarness.SafeDeleteDirectory(staleMapPath);
            mapFolder = null;

            driver.ClickListItemContainingText(StartupRecentMapsListId, staleMapPath);
            driver.InvokeCommand("DialogResult.Ok");
            driver.WaitForStartWindow();

            Assert.True(driver.IsVisible(StartWindowId));
            Assert.False(driver.ContainsText(staleMapPath));
        } finally {
            driver.Shutdown();
            AvaloniaWindowTestHarness.SafeDeleteDirectory(mapFolder);
        }
    }

    static double CenterX((double left, double top, double width, double height) bounds) {
        return bounds.left + bounds.width / 2;
    }

    static double CenterY((double left, double top, double width, double height) bounds) {
        return bounds.top + bounds.height / 2;
    }

    static double RightEdge((double left, double top, double width, double height) bounds) {
        return bounds.left + bounds.width;
    }

    static double BottomEdge((double left, double top, double width, double height) bounds) {
        return bounds.top + bounds.height;
    }
}
