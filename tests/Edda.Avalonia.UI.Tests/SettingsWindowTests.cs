using System.IO;
using Xunit;

namespace Edda.Avalonia.UI.Tests;

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

    [Fact]
    public void SettingsWindowAutosaveTogglePersistsAcrossReopen() {
        AvaloniaWindowTestHarness.RunOpenedFixtureMapTest((driver, _) => {
            OpenSettings(driver);

            var initialAutosave = driver.IsChecked(SettingsAutosaveCheckboxId);
            driver.ToggleCheckbox(SettingsAutosaveCheckboxId, !initialAutosave);
            driver.WaitForIdle();
            driver.ClickButton(SettingsSaveButtonId);
            driver.WaitForIdle();

            OpenSettings(driver);
            Assert.Equal(!initialAutosave, driver.IsChecked(SettingsAutosaveCheckboxId));
            driver.ClickButton(SettingsSaveButtonId);
            driver.WaitForIdle();
        });
    }

    [Fact]
    public void SettingsWindowSpectrogramToggleUpdatesOptionsVisibility() {
        AvaloniaWindowTestHarness.RunOpenedFixtureMapTest((driver, _) => {
            OpenSettings(driver);

            driver.ToggleCheckbox(SettingsShowSpectrogramCheckboxId, false);
            driver.WaitForIdle();
            Assert.False(driver.IsVisible(SettingsSpectrogramFrequencyId));

            driver.ToggleCheckbox(SettingsShowSpectrogramCheckboxId, true);
            driver.WaitForIdle();
            Assert.True(driver.IsVisible(SettingsSpectrogramFrequencyId));

            driver.ClickButton(SettingsSaveButtonId);
            driver.WaitForIdle();
        });
    }

    [Fact]
    public void SettingsWindowInvalidNumericInputsShowErrorAndRevertToPreviousValue() {
        AvaloniaWindowTestHarness.RunOpenedFixtureMapTest((driver, _) => {
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
        AvaloniaWindowTestHarness.RunOpenedFixtureMapTest((driver, _) => {
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
        AvaloniaWindowTestHarness.RunOpenedFixtureMapTest((driver, _) => {
            var installFolder = AvaloniaWindowTestHarness.CreateTempOutputFolder("game-install");
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
                AvaloniaWindowTestHarness.SafeDeleteDirectory(installFolder);
            }
        });
    }

    [Fact]
    public void SettingsWindowGameInstallPickerCancelRevertsToDocumentsMode() {
        AvaloniaWindowTestHarness.RunOpenedFixtureMapTest((driver, _) => {
            OpenSettings(driver);

            driver.SetTestPickerCancellation();
            driver.SelectDropdown(SettingsMapSaveComboId, "Game Install");
            driver.WaitForIdle();

            Assert.Equal("Documents", driver.GetSelectedValue(SettingsMapSaveComboId));
            Assert.False(driver.IsVisible(SettingsMapSavePathTextId));
        });
    }

    [Fact]
    public void SettingsWindowReSelectingGameInstallModeReopensPickerAndUpdatesPath() {
        AvaloniaWindowTestHarness.RunOpenedFixtureMapTest((driver, _) => {
            var firstInstallFolder = AvaloniaWindowTestHarness.CreateTempOutputFolder("game-install-first");
            var secondInstallFolder = AvaloniaWindowTestHarness.CreateTempOutputFolder("game-install-second");
            try {
                OpenSettings(driver);

                driver.SetTestFileSelection(firstInstallFolder);
                driver.SelectDropdown(SettingsMapSaveComboId, "Game Install");
                driver.WaitForIdle();
                Assert.Equal(firstInstallFolder, driver.GetText(SettingsMapSavePathTextId));

                driver.SelectDropdown(SettingsMapSaveComboId, "Documents");
                driver.WaitForIdle();
                driver.SetTestFileSelection(secondInstallFolder);
                driver.SelectDropdown(SettingsMapSaveComboId, "Game Install");
                driver.WaitForIdle();
                Assert.Equal(secondInstallFolder, driver.GetText(SettingsMapSavePathTextId));
            } finally {
                AvaloniaWindowTestHarness.SafeDeleteDirectory(firstInstallFolder);
                AvaloniaWindowTestHarness.SafeDeleteDirectory(secondInstallFolder);
            }
        });
    }

    [Fact]
    public void SettingsWindowDiscordAndUpdateTogglesPersistAcrossReopen() {
        AvaloniaWindowTestHarness.RunOpenedFixtureMapTest((driver, _) => {
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
        AvaloniaWindowTestHarness.RunOpenedFixtureMapTest((driver, _) => {
            driver.ClickWithinElement(SongPlayerButtonId, 0.5, 0.5);
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
        AvaloniaWindowTestHarness.RunOpenedFixtureMapTest((driver, _) => {
            driver.ClickWithinElement(SongPlayerButtonId, 0.5, 0.5);
            driver.WaitForIdle();
            Assert.False(driver.IsEnabled(SongTempoSliderId));

            OpenSettings(driver);
            var changedDrumSample = driver.TrySelectDifferentDropdownValue(SettingsDrumSampleComboId);
            var selectedDrumSample = driver.GetSelectedValue(SettingsDrumSampleComboId);
            if (changedDrumSample) {
                Assert.True(driver.IsEnabled(SongTempoSliderId));
            }

            var initialPanNotes = driver.IsChecked(SettingsPanNotesCheckboxId);
            driver.ClickButton(SettingsSaveButtonId);
            driver.WaitForIdle();

            OpenSettings(driver);
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

    static void OpenSettings(AvaloniaUIDriver driver) {
        driver.SelectMenuItem("Tools>Settings");
        driver.WaitForSettingsWindow();
        driver.WaitForIdle();
    }

    static void AssertInvalidTextInputRestored(AvaloniaUIDriver driver, string controlId, string invalidValue) {
        var previousValue = driver.GetText(controlId);
        driver.SetText(controlId, invalidValue);
        driver.SendKeyboardShortcutToWindow("Tab", SettingsWindowTitle);
        driver.TryInvokeCommand("DialogResult.Ok");
        driver.WaitForIdle();
        Assert.Equal(previousValue, driver.GetText(controlId));
    }
}
