using Xunit;

namespace Edda.Wpf.UI.Tests {
    public class CustomizeNavBarWindowTests {
        private const string CustomizeNavBarButtonId = "btnCustomizeNavBar";

        private const string CustomizeNavCheckWaveformId = "CheckWaveform";
        private const string CustomizeNavCheckBookmarkId = "CheckBookmark";
        private const string CustomizeNavCheckBpmChangeId = "CheckBPMChange";
        private const string CustomizeNavCheckNoteId = "CheckNote";
        private const string CustomizeNavColorWaveformId = "ColorWaveform";
        private const string CustomizeNavColorBookmarkId = "ColorBookmark";
        private const string CustomizeNavBookmarkShadowSliderId = "SliderBookmarkShadowOpacity";
        private const string CustomizeNavColorBpmChangeId = "ColorBPMChange";
        private const string CustomizeNavBpmShadowSliderId = "SliderBPMChangeShadowOpacity";
        private const string CustomizeNavColorNoteId = "ColorNote";
        private const string CustomizeNavResetBookmarkId = "ButtonResetBookmark";
        private const string CustomizeNavResetBpmChangeId = "ButtonResetBPMChange";
        private const string CustomizeNavSaveButtonId = "btnSave";

        private const string NavWaveformImageId = "imgWaveformVertical";
        private const string NavBookmarksCanvasId = "canvasBookmarks";
        private const string NavBpmChangesCanvasId = "canvasTimingChanges";
        private const string NavNotesCanvasId = "canvasNavNotes";

        [Fact]
        public void CustomizeNavBarInitialToggleStateMatchesDefaultVisibility() {
            WpfWindowTestHarness.RunOpenedFixtureMapTest((driver, _) => {
                driver.ClickButton(CustomizeNavBarButtonId);
                driver.WaitForIdle();

                Assert.True(driver.IsChecked(CustomizeNavCheckWaveformId));
                Assert.True(driver.IsChecked(CustomizeNavCheckBookmarkId));
                Assert.False(driver.IsChecked(CustomizeNavCheckBpmChangeId));
                Assert.False(driver.IsChecked(CustomizeNavCheckNoteId));
            });
        }

        [Fact]
        public void CustomizeNavBarToggleStateControlsDependentInputEnablement() {
            WpfWindowTestHarness.RunOpenedFixtureMapTest((driver, _) => {
                driver.ClickButton(CustomizeNavBarButtonId);
                driver.WaitForIdle();

                Assert.True(driver.IsEnabled(CustomizeNavColorWaveformId));
                Assert.True(driver.IsEnabled(CustomizeNavColorBookmarkId));
                Assert.True(driver.IsEnabled(CustomizeNavBookmarkShadowSliderId));
                Assert.False(driver.IsEnabled(CustomizeNavColorBpmChangeId));
                Assert.False(driver.IsEnabled(CustomizeNavBpmShadowSliderId));
                Assert.False(driver.IsEnabled(CustomizeNavColorNoteId));

                driver.ToggleCheckbox(CustomizeNavCheckBookmarkId, false);
                driver.ToggleCheckbox(CustomizeNavCheckBpmChangeId, true);
                driver.ToggleCheckbox(CustomizeNavCheckNoteId, true);
                driver.WaitForIdle();

                Assert.False(driver.IsEnabled(CustomizeNavColorBookmarkId));
                Assert.False(driver.IsEnabled(CustomizeNavBookmarkShadowSliderId));
                Assert.True(driver.IsEnabled(CustomizeNavColorBpmChangeId));
                Assert.True(driver.IsEnabled(CustomizeNavBpmShadowSliderId));
                Assert.True(driver.IsEnabled(CustomizeNavColorNoteId));
            });
        }

        [Fact]
        public void CustomizeNavBarToggleChangesApplyToMainWindowLayers() {
            WpfWindowTestHarness.RunOpenedFixtureMapTest((driver, _) => {
                driver.SelectMenuItem("Edit>Add New>Timing Change");
                driver.SelectMenuItem("Edit>Add New>Note (column 1)");
                driver.WaitForIdle();

                driver.ClickButton(CustomizeNavBarButtonId);
                driver.WaitForIdle();

                driver.ToggleCheckbox(CustomizeNavCheckWaveformId, false);
                driver.ToggleCheckbox(CustomizeNavCheckBookmarkId, false);
                driver.ToggleCheckbox(CustomizeNavCheckBpmChangeId, true);
                driver.ToggleCheckbox(CustomizeNavCheckNoteId, true);
                driver.WaitForIdle();
                driver.ClickButton(CustomizeNavSaveButtonId);
                driver.WaitForIdle();

                Assert.False(driver.IsVisible(NavWaveformImageId));
                Assert.False(driver.IsVisible(NavBookmarksCanvasId));
            });
        }

        [Fact]
        public void CustomizeNavBarShadowSlidersAcceptManualValues() {
            WpfWindowTestHarness.RunOpenedFixtureMapTest((driver, _) => {
                driver.ClickButton(CustomizeNavBarButtonId);
                driver.WaitForIdle();

                driver.ToggleCheckbox(CustomizeNavCheckBookmarkId, true);
                driver.ToggleCheckbox(CustomizeNavCheckBpmChangeId, true);
                driver.SetSliderValue(CustomizeNavBookmarkShadowSliderId, 0.12);
                driver.SetSliderValue(CustomizeNavBpmShadowSliderId, 0.83);
                driver.WaitForIdle();

                driver.WaitForIdle();

                Assert.InRange(driver.GetSliderValue(CustomizeNavBookmarkShadowSliderId), 0.11, 0.13);
                Assert.InRange(driver.GetSliderValue(CustomizeNavBpmShadowSliderId), 0.82, 0.84);
            });
        }

        [Fact]
        public void CustomizeNavBarResetButtonsRestoreShadowDefaults() {
            WpfWindowTestHarness.RunOpenedFixtureMapTest((driver, _) => {
                driver.ClickButton(CustomizeNavBarButtonId);
                driver.WaitForIdle();

                driver.ToggleCheckbox(CustomizeNavCheckBookmarkId, true);
                driver.ToggleCheckbox(CustomizeNavCheckBpmChangeId, true);
                driver.SetSliderValue(CustomizeNavBookmarkShadowSliderId, 0.2);
                driver.SetSliderValue(CustomizeNavBpmShadowSliderId, 0.8);
                driver.WaitForIdle();

                driver.ClickButton(CustomizeNavResetBookmarkId);
                driver.ClickButton(CustomizeNavResetBpmChangeId);
                driver.WaitForIdle();

                Assert.InRange(driver.GetSliderValue(CustomizeNavBookmarkShadowSliderId), 0.49, 0.51);
                Assert.InRange(driver.GetSliderValue(CustomizeNavBpmShadowSliderId), 0.49, 0.51);
            });
        }

        [Fact]
        public void CustomizeNavBarChangesPersistAcrossWindowReopen() {
            WpfWindowTestHarness.RunOpenedFixtureMapTest((driver, _) => {
                driver.ClickButton(CustomizeNavBarButtonId);
                driver.WaitForIdle();

                driver.ToggleCheckbox(CustomizeNavCheckWaveformId, false);
                driver.ToggleCheckbox(CustomizeNavCheckBookmarkId, false);
                driver.ToggleCheckbox(CustomizeNavCheckBpmChangeId, true);
                driver.ToggleCheckbox(CustomizeNavCheckNoteId, true);
                driver.SetSliderValue(CustomizeNavBpmShadowSliderId, 0.77);
                driver.WaitForIdle();
                driver.ClickButton(CustomizeNavSaveButtonId);
                driver.WaitForIdle();

                driver.ClickButton(CustomizeNavBarButtonId);
                driver.WaitForIdle();

                Assert.False(driver.IsChecked(CustomizeNavCheckWaveformId));
                Assert.False(driver.IsChecked(CustomizeNavCheckBookmarkId));
                Assert.True(driver.IsChecked(CustomizeNavCheckBpmChangeId));
                Assert.True(driver.IsChecked(CustomizeNavCheckNoteId));
                Assert.InRange(driver.GetSliderValue(CustomizeNavBpmShadowSliderId), 0.75, 0.79);
            });
        }
    }
}
