using System.IO;
using Xunit;

namespace Edda.Wpf.UI.Tests {
    public class SongPreviewWindowTests {
        private const string OpenPreviewButtonId = "btnMakePreview";
        private const string SongProgressSliderId = "sliderSongProgress";
        private const string SongPreviewWindowTitle = "Song Preview";
        private const string PreviewGenerateButtonId = "btnGenerate";

        private const string PreviewStartMinId = "TxtStartTimeMin";
        private const string PreviewStartSecId = "TxtStartTimeSec";
        private const string PreviewEndMinId = "TxtEndTimeMin";
        private const string PreviewEndSecId = "TxtEndTimeSec";
        private const string PreviewFadeInId = "TxtFadeInDuration";
        private const string PreviewFadeOutId = "TxtFadeOutDuration";

        [Fact]
        public void SongPreviewWindowShowsSeededTimeAndFadeFields() {
            WpfWindowTestHarness.RunOpenedFixtureMapTest((driver, _) => {
                driver.SetSliderValue(SongProgressSliderId, 30_000);
                driver.WaitForIdle();
                OpenPreview(driver);

                Assert.True(driver.IsVisible(PreviewGenerateButtonId));
                Assert.Equal("0", driver.GetText(PreviewStartMinId));
                Assert.Equal("30", driver.GetText(PreviewStartSecId));
                Assert.False(string.IsNullOrWhiteSpace(driver.GetText(PreviewEndMinId)));
                Assert.False(string.IsNullOrWhiteSpace(driver.GetText(PreviewEndSecId)));
                Assert.False(string.IsNullOrWhiteSpace(driver.GetText(PreviewFadeInId)));
                Assert.False(string.IsNullOrWhiteSpace(driver.GetText(PreviewFadeOutId)));
            });
        }

        [Fact]
        public void SongPreviewWindowAdjustsStartWhenEndIsMovedBeforeStart() {
            WpfWindowTestHarness.RunOpenedFixtureMapTest((driver, _) => {
                OpenPreview(driver);

                driver.SetText(PreviewStartMinId, "0");
                driver.SetText(PreviewStartSecId, "30");
                driver.SetText(PreviewEndMinId, "0");
                driver.SetText(PreviewEndSecId, "10");
                // Move focus so end-time lost-focus logic runs.
                driver.SetText(PreviewFadeInId, driver.GetText(PreviewFadeInId));
                driver.WaitForIdle();

                Assert.Equal(driver.GetText(PreviewEndMinId), driver.GetText(PreviewStartMinId));
                Assert.Equal(driver.GetText(PreviewEndSecId), driver.GetText(PreviewStartSecId));
            });
        }

        [Fact]
        public void SongPreviewWindowAdjustsEndWhenStartIsMovedAfterEnd() {
            WpfWindowTestHarness.RunOpenedFixtureMapTest((driver, _) => {
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
            WpfWindowTestHarness.RunOpenedFixtureMapTest((driver, _) => {
                OpenPreview(driver);

                var previousValue = driver.GetText(PreviewFadeInId);
                driver.SetText(PreviewFadeInId, "-1");
                driver.SetText(PreviewFadeOutId, driver.GetText(PreviewFadeOutId));
                driver.InvokeCommand("DialogResult.Ok");
                driver.WaitForIdle();

                Assert.Equal(previousValue, driver.GetText(PreviewFadeInId));
            });
        }

        [Fact]
        public void SongPreviewWindowInvalidTimeValueShowsErrorAndReverts() {
            WpfWindowTestHarness.RunOpenedFixtureMapTest((driver, _) => {
                OpenPreview(driver);

                var previousValue = driver.GetText(PreviewStartSecId);
                driver.SetText(PreviewStartSecId, "oops");
                driver.SetText(PreviewFadeInId, driver.GetText(PreviewFadeInId));
                driver.InvokeCommand("DialogResult.Ok");
                driver.WaitForIdle();

                Assert.Equal(previousValue, driver.GetText(PreviewStartSecId));
            });
        }

        [Fact]
        public void SongPreviewWindowLongDurationWarningCanCancelGeneration() {
            WpfWindowTestHarness.RunOpenedFixtureMapTest((driver, mapFolder) => {
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
            WpfWindowTestHarness.RunOpenedFixtureMapTest((driver, mapFolder) => {
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
            WpfWindowTestHarness.RunOpenedFixtureMapTest((driver, _) => {
                OpenPreview(driver);
                Assert.Equal(1, driver.CountWindowsByTitle(SongPreviewWindowTitle));

                driver.ClickButton(OpenPreviewButtonId);
                driver.WaitForIdle();
                Assert.Equal(1, driver.CountWindowsByTitle(SongPreviewWindowTitle));
            });
        }

        private static void OpenPreview(WpfUIDriver driver) {
            driver.ClickButton(OpenPreviewButtonId);
            driver.WaitForIdle();
        }
    }
}
