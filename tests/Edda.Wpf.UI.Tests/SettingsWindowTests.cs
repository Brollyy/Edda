using System.IO;
using Xunit;

namespace Edda.Wpf.UI.Tests {
    public class SettingsWindowTests {
        private const string SettingsWindowTitle = "Settings";
        private const string SettingsAutosaveCheckboxId = "CheckAutosave";
        private const string SettingsDefaultMapperId = "txtDefaultMapper";
        private const string SettingsDefaultNoteSpeedId = "txtDefaultNoteSpeed";
        private const string SettingsDefaultGridSpacingId = "txtDefaultGridSpacing";
        private const string SettingsAudioLatencyId = "txtAudioLatency";
        private const string SettingsPlaybackDeviceComboId = "comboPlaybackDevice";
        private const string SettingsDrumSampleComboId = "comboDrumSample";
        private const string SettingsPanNotesCheckboxId = "checkPanNotes";
        private const string SettingsShowSpectrogramCheckboxId = "CheckShowSpectrogram";
        private const string SettingsSpectrogramOptionsId = "spectrogramOptions";
        private const string SettingsSpectrogramFrequencyId = "txtSpectrogramFrequency";
        private const string SettingsDiscordCheckboxId = "checkDiscord";
        private const string SettingsStartupUpdateCheckboxId = "checkStartupUpdate";
        private const string SettingsMapSaveComboId = "comboMapSaveFolder";
        private const string SettingsMapSavePathTextId = "txtMapSaveFolderPath";
        private const string SettingsSaveButtonId = "btnSave";

        private const string SongPlayerButtonId = "btnSongPlayer";
        private const string SongTempoSliderId = "sliderSongTempo";
        private const string MainWindowTitle = "Edda";

        [Fact]
        public void SettingsWindowAutosaveTogglePersistsAcrossReopen() {
            WpfWindowTestHarness.RunOpenedFixtureMapTest((driver, _) => {
                driver.SelectMenuItem("Tools>Settings");
                driver.WaitForIdle();

                var initialAutosave = driver.IsChecked(SettingsAutosaveCheckboxId);
                driver.ToggleCheckbox(SettingsAutosaveCheckboxId, !initialAutosave);
                driver.WaitForIdle();
                driver.ClickButton(SettingsSaveButtonId);
                driver.WaitForIdle();

                driver.SelectMenuItem("Tools>Settings");
                driver.WaitForIdle();
                Assert.Equal(!initialAutosave, driver.IsChecked(SettingsAutosaveCheckboxId));
                driver.ClickButton(SettingsSaveButtonId);
                driver.WaitForIdle();
            });
        }

        [Fact]
        public void SettingsWindowSpectrogramToggleUpdatesOptionsVisibility() {
            WpfWindowTestHarness.RunOpenedFixtureMapTest((driver, _) => {
                driver.SelectMenuItem("Tools>Settings");
                driver.WaitForIdle();

                driver.ToggleCheckbox(SettingsShowSpectrogramCheckboxId, false);
                driver.WaitForIdle();
                Assert.False(driver.IsVisible(SettingsSpectrogramOptionsId));

                driver.ToggleCheckbox(SettingsShowSpectrogramCheckboxId, true);
                driver.WaitForIdle();
                Assert.True(driver.IsVisible(SettingsSpectrogramOptionsId));

                driver.ClickButton(SettingsSaveButtonId);
                driver.WaitForIdle();
            });
        }

        [Fact]
        public void SettingsWindowInvalidNumericInputsShowErrorAndRevertToPreviousValue() {
            WpfWindowTestHarness.RunOpenedFixtureMapTest((driver, _) => {
                OpenSettings(driver);
                driver.ToggleCheckbox(SettingsShowSpectrogramCheckboxId, true);
                driver.WaitForIdle();

                AssertInvalidTextInputRestored(driver, SettingsDefaultNoteSpeedId, "fast");
                AssertInvalidTextInputRestored(driver, SettingsDefaultGridSpacingId, "wide");
                AssertInvalidTextInputRestored(driver, SettingsAudioLatencyId, "laggy");
                AssertInvalidTextInputRestored(driver, SettingsSpectrogramFrequencyId, "999999");
            });
        }

        [Fact]
        public void SettingsWindowSpectrogramFrequencyCommitsOnEnterKey() {
            WpfWindowTestHarness.RunOpenedFixtureMapTest((driver, _) => {
                OpenSettings(driver);
                driver.ToggleCheckbox(SettingsShowSpectrogramCheckboxId, true);
                driver.WaitForIdle();

                var currentValue = driver.GetText(SettingsSpectrogramFrequencyId);
                var nextValue = currentValue == "12000" ? "11000" : "12000";
                driver.SetText(SettingsSpectrogramFrequencyId, nextValue);
                driver.SendKeyboardShortcutToWindow("Enter", SettingsWindowTitle);
                driver.WaitForIdle();

                Assert.Equal(nextValue, driver.GetText(SettingsSpectrogramFrequencyId));
            });
        }

        [Fact]
        public void SettingsWindowGameInstallPickerUpdatesPathAndCreatesMapFolder() {
            WpfWindowTestHarness.RunOpenedFixtureMapTest((driver, _) => {
                var installFolder = WpfWindowTestHarness.CreateTempOutputFolder("game-install");
                try {
                    OpenSettings(driver);

                    driver.SetTestFileSelection(installFolder);
                    driver.SelectDropdown(SettingsMapSaveComboId, "Game Install");
                    driver.WaitForIdle();

                    Assert.True(driver.IsVisible(SettingsMapSavePathTextId));
                    Assert.Equal(installFolder, driver.GetText(SettingsMapSavePathTextId));
                    Assert.True(Directory.Exists(Path.Combine(installFolder, "Ragnarock", "CustomSongs")));

                    driver.ClickButton(SettingsSaveButtonId);
                    driver.WaitForIdle();
                } finally {
                    WpfWindowTestHarness.SafeDeleteDirectory(installFolder);
                }
            });
        }

        [Fact]
        public void SettingsWindowGameInstallPickerCancelRevertsToDocumentsMode() {
            WpfWindowTestHarness.RunOpenedFixtureMapTest((driver, _) => {
                OpenSettings(driver);

                driver.SetTestPickerCancellation();
                driver.SelectDropdown(SettingsMapSaveComboId, "Game Install");
                driver.WaitForIdle();

                Assert.Equal("Documents", driver.GetSelectedValue(SettingsMapSaveComboId));
                Assert.False(driver.IsVisible(SettingsMapSavePathTextId));
            });
        }

        [Fact]
        public void SettingsWindowMapSavePathTextReopensPickerWhenUsingGameInstallMode() {
            WpfWindowTestHarness.RunOpenedFixtureMapTest((driver, _) => {
                var firstInstallFolder = WpfWindowTestHarness.CreateTempOutputFolder("game-install-first");
                var secondInstallFolder = WpfWindowTestHarness.CreateTempOutputFolder("game-install-second");
                try {
                    OpenSettings(driver);

                    driver.SetTestFileSelection(firstInstallFolder);
                    driver.SelectDropdown(SettingsMapSaveComboId, "Game Install");
                    driver.WaitForIdle();
                    Assert.Equal(firstInstallFolder, driver.GetText(SettingsMapSavePathTextId));

                    driver.SetTestFileSelection(secondInstallFolder);
                    driver.ClickButton(SettingsMapSavePathTextId);
                    driver.WaitForIdle();
                    Assert.Equal(secondInstallFolder, driver.GetText(SettingsMapSavePathTextId));
                } finally {
                    WpfWindowTestHarness.SafeDeleteDirectory(firstInstallFolder);
                    WpfWindowTestHarness.SafeDeleteDirectory(secondInstallFolder);
                }
            });
        }

        [Fact]
        public void SettingsWindowDiscordAndUpdateTogglesPersistAcrossReopen() {
            WpfWindowTestHarness.RunOpenedFixtureMapTest((driver, _) => {
                OpenSettings(driver);

                var initialDiscord = driver.IsChecked(SettingsDiscordCheckboxId);
                var initialUpdate = driver.IsChecked(SettingsStartupUpdateCheckboxId);
                driver.ToggleCheckbox(SettingsDiscordCheckboxId, !initialDiscord);
                driver.ToggleCheckbox(SettingsStartupUpdateCheckboxId, !initialUpdate);
                driver.ClickButton(SettingsSaveButtonId);
                driver.WaitForIdle();

                OpenSettings(driver);
                Assert.Equal(!initialDiscord, driver.IsChecked(SettingsDiscordCheckboxId));
                Assert.Equal(!initialUpdate, driver.IsChecked(SettingsStartupUpdateCheckboxId));
            });
        }

        [Fact]
        public void SettingsWindowPlaybackDeviceSelectionPausesPlaybackAndPersists() {
            WpfWindowTestHarness.RunOpenedFixtureMapTest((driver, _) => {
                driver.ClickButton(SongPlayerButtonId);
                driver.WaitForIdle();
                Assert.False(driver.IsEnabled(SongTempoSliderId));

                OpenSettings(driver);
                var changedPlaybackDevice = driver.TrySelectDifferentDropdownValue(SettingsPlaybackDeviceComboId);
                if (!changedPlaybackDevice) {
                    return;
                }

                var selectedPlaybackDevice = driver.GetSelectedValue(SettingsPlaybackDeviceComboId);
                Assert.True(driver.IsEnabled(SongTempoSliderId));

                driver.ClickButton(SettingsSaveButtonId);
                driver.WaitForIdle();
                OpenSettings(driver);
                Assert.Equal(selectedPlaybackDevice, driver.GetSelectedValue(SettingsPlaybackDeviceComboId));
            });
        }

        [Fact]
        public void SettingsWindowDrumSampleAndPanPausePlaybackAndPersist() {
            WpfWindowTestHarness.RunOpenedFixtureMapTest((driver, _) => {
                driver.ClickButton(SongPlayerButtonId);
                driver.WaitForIdle();
                Assert.False(driver.IsEnabled(SongTempoSliderId));

                OpenSettings(driver);
                var changedDrumSample = driver.TrySelectDifferentDropdownValue(SettingsDrumSampleComboId);
                var selectedDrumSample = driver.GetSelectedValue(SettingsDrumSampleComboId);
                if (changedDrumSample) {
                    Assert.True(driver.IsEnabled(SongTempoSliderId));
                }

                driver.SendKeyboardShortcutToWindow("Space", MainWindowTitle);
                driver.WaitForIdle();
                Assert.False(driver.IsEnabled(SongTempoSliderId));

                var initialPanNotes = driver.IsChecked(SettingsPanNotesCheckboxId);
                driver.ToggleCheckbox(SettingsPanNotesCheckboxId, !initialPanNotes);
                driver.WaitForIdle();
                Assert.True(driver.IsEnabled(SongTempoSliderId));

                driver.ClickButton(SettingsSaveButtonId);
                driver.WaitForIdle();
                OpenSettings(driver);
                Assert.Equal(selectedDrumSample, driver.GetSelectedValue(SettingsDrumSampleComboId));
                Assert.Equal(!initialPanNotes, driver.IsChecked(SettingsPanNotesCheckboxId));
            });
        }

        private static void OpenSettings(WpfUIDriver driver) {
            driver.SelectMenuItem("Tools>Settings");
            driver.WaitForIdle();
        }

        private static void AssertInvalidTextInputRestored(WpfUIDriver driver, string controlId, string invalidValue) {
            var previousValue = driver.GetText(controlId);
            driver.SetText(controlId, invalidValue);
            driver.SetText(SettingsDefaultMapperId, driver.GetText(SettingsDefaultMapperId));
            driver.InvokeCommand("DialogResult.Ok");
            driver.WaitForIdle();
            Assert.Equal(previousValue, driver.GetText(controlId));
        }
    }
}
