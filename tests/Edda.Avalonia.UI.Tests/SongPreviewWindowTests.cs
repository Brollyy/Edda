using System.IO;
using Xunit;

namespace Edda.Avalonia.UI.Tests;

public class SongPreviewWindowTests {
    const string OpenPreviewButtonId = "btnMakePreview";
    const string SongProgressSliderId = "sliderSongProgress";
    const string SongPreviewWindowTitle = "Song Preview";
    const string PreviewGenerateButtonId = "btnGenerate";

    const string PreviewStartMinId = "TxtStartTimeMin";
    const string PreviewStartSecId = "TxtStartTimeSec";
    const string PreviewEndMinId = "TxtEndTimeMin";
    const string PreviewEndSecId = "TxtEndTimeSec";
    const string PreviewFadeInId = "TxtFadeInDuration";
    const string PreviewFadeOutId = "TxtFadeOutDuration";

    [Fact]
    public void SongPreviewWindowShowsSeededTimeAndFadeFields() {
        AvaloniaWindowTestHarness.RunOpenedFixtureMapTest((driver, _) => {
            driver.SetSliderValue(SongProgressSliderId, 30_000);
            driver.WaitForIdle();
            var actualProgressMs = driver.GetSliderValue(SongProgressSliderId);
            OpenPreview(driver);

            Assert.True(driver.IsVisible(PreviewGenerateButtonId));
            Assert.Equal(((int)(actualProgressMs / 1000) / 60).ToString(), driver.GetText(PreviewStartMinId));
            Assert.Equal(((int)(actualProgressMs / 1000) % 60).ToString(), driver.GetText(PreviewStartSecId));
            Assert.False(string.IsNullOrWhiteSpace(driver.GetText(PreviewEndMinId)));
            Assert.False(string.IsNullOrWhiteSpace(driver.GetText(PreviewEndSecId)));
            Assert.False(string.IsNullOrWhiteSpace(driver.GetText(PreviewFadeInId)));
            Assert.False(string.IsNullOrWhiteSpace(driver.GetText(PreviewFadeOutId)));
        });
    }

    [Fact]
    public void SongPreviewWindowAdjustsStartWhenEndIsMovedBeforeStart() {
        AvaloniaWindowTestHarness.RunOpenedFixtureMapTest((driver, _) => {
            OpenPreview(driver);

            driver.SetText(PreviewStartMinId, "0");
            driver.SetText(PreviewStartSecId, "30");
            driver.SetText(PreviewEndMinId, "0");
            driver.SetText(PreviewEndSecId, "10");
            driver.SetText(PreviewFadeInId, driver.GetText(PreviewFadeInId));
            driver.WaitForIdle();

            Assert.Equal(driver.GetText(PreviewEndMinId), driver.GetText(PreviewStartMinId));
            Assert.Equal(driver.GetText(PreviewEndSecId), driver.GetText(PreviewStartSecId));
        });
    }

    [Fact]
    public void SongPreviewWindowAdjustsEndWhenStartIsMovedAfterEnd() {
        AvaloniaWindowTestHarness.RunOpenedFixtureMapTest((driver, _) => {
            OpenPreview(driver);

            driver.SetText(PreviewEndMinId, "0");
            driver.SetText(PreviewEndSecId, "10");
            driver.SetText(PreviewStartMinId, "0");
            driver.SetText(PreviewStartSecId, "30");
            driver.SetText(PreviewFadeOutId, driver.GetText(PreviewFadeOutId));
            driver.WaitForIdle();

            Assert.Equal(driver.GetText(PreviewStartMinId), driver.GetText(PreviewEndMinId));
            Assert.Equal(driver.GetText(PreviewStartSecId), driver.GetText(PreviewEndSecId));
        });
    }

    [Fact]
    public void SongPreviewWindowInvalidFadeValueShowsErrorAndReverts() {
        AvaloniaWindowTestHarness.RunOpenedFixtureMapTest((driver, _) => {
            OpenPreview(driver);

            var previousValue = driver.GetText(PreviewFadeInId);
            driver.SetText(PreviewFadeInId, "-1");
            driver.SendKeyboardShortcutToWindow("Tab", SongPreviewWindowTitle);
            driver.TryInvokeCommand("DialogResult.Ok");
            driver.WaitForIdle();

            Assert.Equal(previousValue, driver.GetText(PreviewFadeInId));
        });
    }

    [Fact]
    public void SongPreviewWindowInvalidTimeValueShowsErrorAndReverts() {
        AvaloniaWindowTestHarness.RunOpenedFixtureMapTest((driver, _) => {
            OpenPreview(driver);

            var previousValue = driver.GetText(PreviewStartSecId);
            driver.SetText(PreviewStartSecId, "oops");
            driver.SendKeyboardShortcutToWindow("Tab", SongPreviewWindowTitle);
            driver.TryInvokeCommand("DialogResult.Ok");
            driver.WaitForIdle();

            Assert.Equal(previousValue, driver.GetText(PreviewStartSecId));
        });
    }

    [Fact]
    public void SongPreviewWindowLongDurationWarningCanCancelGeneration() {
        AvaloniaWindowTestHarness.RunOpenedFixtureMapTest((driver, mapFolder) => {
            OpenPreview(driver);
            var previewPath = Path.Combine(mapFolder, "preview.ogg");
            if (File.Exists(previewPath)) {
                File.Delete(previewPath);
            }

            driver.SetText(PreviewStartMinId, "0");
            driver.SetText(PreviewStartSecId, "0");
            driver.SetText(PreviewEndMinId, "0");
            driver.SetText(PreviewEndSecId, "30");
            driver.SetText(PreviewFadeInId, driver.GetText(PreviewFadeInId));
            driver.WaitForIdle();

            driver.ClickButton(PreviewGenerateButtonId);
            driver.InvokeCommand("DialogResult.No");
            driver.WaitForIdle();

            Assert.False(File.Exists(previewPath));
            Assert.True(driver.IsEnabled(PreviewGenerateButtonId));
        });
    }

    [Fact]
    public void SongPreviewWindowGenerateSuccessCreatesPreviewAndReenablesButton() {
        AvaloniaWindowTestHarness.RunOpenedFixtureMapTest((driver, mapFolder) => {
            OpenPreview(driver);
            var previewPath = Path.Combine(mapFolder, "preview.ogg");
            if (File.Exists(previewPath)) {
                File.Delete(previewPath);
            }

            driver.SetText(PreviewStartMinId, "0");
            driver.SetText(PreviewStartSecId, "0");
            driver.SetText(PreviewEndMinId, "0");
            driver.SetText(PreviewEndSecId, "5");
            driver.SetText(PreviewFadeInId, "1");
            driver.SetText(PreviewFadeOutId, "1");
            driver.WaitForIdle();

            driver.ClickButton(PreviewGenerateButtonId);
            driver.InvokeCommand("DialogResult.Ok");
            driver.WaitForIdle();

            Assert.True(File.Exists(previewPath));
            Assert.True(driver.IsEnabled(PreviewGenerateButtonId));
        });
    }

    [Fact]
    public void SongPreviewWindowIsSingleInstanceFromMainWindowButton() {
        AvaloniaWindowTestHarness.RunOpenedFixtureMapTest((driver, _) => {
            OpenPreview(driver);
            Assert.Equal(1, driver.CountWindowsByTitle(SongPreviewWindowTitle));

            driver.ClickButton(OpenPreviewButtonId);
            driver.WaitForIdle();
            Assert.Equal(1, driver.CountWindowsByTitle(SongPreviewWindowTitle));
        });
    }

    static void OpenPreview(AvaloniaUIDriver driver) {
        driver.ClickButton(OpenPreviewButtonId);
        driver.WaitForIdle();
    }
}
