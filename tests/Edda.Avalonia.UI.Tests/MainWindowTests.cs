using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using Xunit;

namespace Edda.Avalonia.UI.Tests;

    public class MainWindowTests {
        private const string StartWindowId = "StartWindow";
        private const string StartupOpenMapButtonId = "ButtonOpenMap";
        private const string MainWindowId = "AppMainWindow";
        private const string LeftSidebarSentinelId = "txtSongName";
        private const string RightSidebarSentinelId = "txtDifficultyNumber";

        private const string SongNameTextBoxId = "txtSongName";
        private const string ArtistNameTextBoxId = "txtArtistName";
        private const string MapperNameTextBoxId = "txtMapperName";
        private const string SongFileNameTextId = "txtSongFileName";
        private const string CoverFileNameTextId = "txtCoverFileName";
        private const string PickSongButtonId = "btnPickSong";
        private const string PickCoverButtonId = "btnPickCover";
        private const string SongBpmTextBoxId = "txtSongBPM";
        private const string SongTempoTextId = "txtSongTempo";
        private const string EnvironmentComboBoxId = "comboEnvironment";
        private const string ExplicitCheckboxId = "checkExplicitContent";
        private const string MetronomeCheckboxId = "checkMetronome";
        private const string WaveformCheckboxId = "checkWaveform";

        private const string SongPlayerButtonId = "btnSongPlayer";
        private const string SongProgressSliderId = "sliderSongProgress";
        private const string SongVolumeSliderId = "sliderSongVol";
        private const string DrumVolumeSliderId = "sliderDrumVol";
        private const string SongVolumeTextId = "txtSongVol";
        private const string DrumVolumeTextId = "txtDrumVol";
        private const string SongTempoSliderId = "sliderSongTempo";
        private const string SongPositionTextId = "txtSongPosition";
        private const string SelectedBeatLabelId = "lblSelectedBeat";
        private const string PreviewPlayButtonId = "btnPlayPreview";
        private const string DifficultyPredictionLabelId = "difficultyPrediction";

        private const string NavWaveformImageId = "imgWaveformVertical";
        private const string NavBookmarksCanvasId = "canvasBookmarks";
        private const string NavBpmChangesCanvasId = "canvasTimingChanges";
        private const string NavNotesCanvasId = "canvasNavNotes";

        private const string ClearCacheMenuItemId = "MenuItemClearCache";
        private const string SettingsShowSpectrogramCheckboxId = "CheckShowSpectrogram";
        private const string SettingsSaveButtonId = "btnSave";

        private const string GridSnapCheckboxId = "checkGridSnap";
        private const string SnapToGridMenuItemId = "MenuItemSnapToGrid";
        private const string GridDivisionTextBoxId = "txtGridDivision";
        private const string GridSpacingTextBoxId = "txtGridSpacing";
        private const string NavWaveformId = "borderNavWaveform";
        private const string NavWaveformDragTargetId = "canvasNavInputBox";

        private const string DifficultyButton0Id = "btnChangeDifficulty0";
        private const string DifficultyButton1Id = "btnChangeDifficulty1";
        private const string DifficultyButton2Id = "btnChangeDifficulty2";
        private const string AddDifficultyButtonId = "btnAddDifficulty";
        private const string DeleteDifficultyButtonId = "btnDeleteDifficulty";
        private const string DifficultyNumberTextBoxId = "txtDifficultyNumber";
        private const string NoteSpeedTextBoxId = "txtNoteSpeed";
        private const string BronzeMedalDistanceTextBoxId = "txtDistMedal0";
        private const string SilverMedalDistanceTextBoxId = "txtDistMedal1";
        private const string GoldMedalDistanceTextBoxId = "txtDistMedal2";

        private const string ChangeBpmButtonId = "btnChangeBPM";
        private const string CustomizeNavBarButtonId = "btnCustomizeNavBar";
        private const string CreatePreviewButtonId = "btnMakePreview";

        private const string BpmWindowSentinelId = "lblAvgBPM";
        private const string PredictorWindowSentinelId = "btnPredict";
        private const string SettingsWindowSentinelId = "CheckAutosave";
        private const string AboutWindowSentinelId = "TxtGithubLink";
        private const string ChangeBpmWindowSentinelId = "dataBPMChange";
        private const string CustomizeNavBarWindowSentinelId = "ColorWaveform";
        private const string SongPreviewWindowSentinelId = "btnGenerate";

        private const string DialogYesCommandId = "DialogResult.Yes";
        private const string DialogNoCommandId = "DialogResult.No";
        private const string DialogCancelCommandId = "DialogResult.Cancel";

        private const string FixtureMapFolderRelative = "tests/TestData/Wpf/MainWindow/FixtureMap";
        private const string AutosaveFolderName = "autosaves";
        private const string CacheFolderName = "cache";

        [Fact]
        public void MainWindowIsVisibleAfterOpeningMap() {
            RunOpenedFixtureMapTestLocal((driver, _) => { Assert.True(driver.IsVisible(MainWindowId)); });
        }

        [Fact]
        public void MapLoadEnablesCoreMetadataPlaybackAndGridControls() {
            RunOpenedFixtureMapTestLocal((driver, _) => {
                Assert.True(driver.IsEnabled(SongNameTextBoxId));
                Assert.True(driver.IsEnabled(ArtistNameTextBoxId));
                Assert.True(driver.IsEnabled(MapperNameTextBoxId));
                Assert.True(driver.IsEnabled(SongBpmTextBoxId));
                Assert.True(driver.IsEnabled(SongPlayerButtonId));
                Assert.True(driver.IsEnabled(SongProgressSliderId));
                Assert.True(driver.IsEnabled(SongVolumeSliderId));
                Assert.True(driver.IsEnabled(DrumVolumeSliderId));
                Assert.True(driver.IsEnabled(MetronomeCheckboxId));
                Assert.True(driver.IsEnabled(GridSnapCheckboxId));
                Assert.True(driver.IsEnabled(GridDivisionTextBoxId));
                Assert.True(driver.IsEnabled(GridSpacingTextBoxId));
            });
        }

        [Fact]
        public void MapLoadPopulatesMetadataAndEnvironmentSelection() {
            RunOpenedFixtureMapTestLocal((driver, _) => {
                Assert.False(string.IsNullOrWhiteSpace(driver.GetText(SongNameTextBoxId)));
                Assert.False(string.IsNullOrWhiteSpace(driver.GetText(SongBpmTextBoxId)));
                Assert.False(string.IsNullOrWhiteSpace(driver.GetSelectedValue(EnvironmentComboBoxId)));
            });
        }

        [Fact]
        public void FixtureMapValuesAreLoadedIntoMainWindowFields() {
            RunOpenedFixtureMapTestLocal((driver, _) => {
                Assert.Equal("Fixture Song", driver.GetText(SongNameTextBoxId));
                Assert.Equal("120", driver.GetText(SongBpmTextBoxId));
                Assert.Equal("Midgard", driver.GetSelectedValue(EnvironmentComboBoxId));
                Assert.False(driver.IsChecked(ExplicitCheckboxId));
                Assert.Equal("song.ogg", driver.GetText(SongFileNameTextId));
                Assert.Equal("cover.jpg", driver.GetText(CoverFileNameTextId));
            });
        }

        [Fact]
        public void FixtureDifficultyValuesAreLoadedIntoDifficultyFields() {
            RunOpenedFixtureMapTestLocal((driver, _) => {
                Assert.Equal("3", driver.GetText(DifficultyNumberTextBoxId));
                Assert.Equal("12", driver.GetText(NoteSpeedTextBoxId));
                Assert.Equal("Auto", driver.GetText(BronzeMedalDistanceTextBoxId));
                Assert.Equal("Auto", driver.GetText(SilverMedalDistanceTextBoxId));
                Assert.Equal("Auto", driver.GetText(GoldMedalDistanceTextBoxId));
                Assert.Equal("1", driver.GetText(GridSpacingTextBoxId));
                Assert.Equal("4", driver.GetText(GridDivisionTextBoxId));
            });
        }

        [Fact]
        public void SingleDifficultyFixtureShowsExpectedDifficultyButtonState() {
            RunOpenedFixtureMapTestLocal((driver, _) => {
                Assert.True(driver.IsVisible(DifficultyButton0Id));
                Assert.False(driver.IsVisible(DifficultyButton1Id));
                Assert.False(driver.IsVisible(DifficultyButton2Id));
                Assert.True(driver.IsEnabled(AddDifficultyButtonId));
                Assert.False(driver.IsEnabled(DeleteDifficultyButtonId));
            });
        }

        [Fact]
        public void PlaybackViaButtonDisablesAndReenablesTimelineAndDifficultyControls() {
            RunOpenedFixtureMapTestLocal((driver, _) => {
                var previewButtonInitiallyEnabled = driver.IsEnabled(PreviewPlayButtonId);

                driver.ClickWithinElement(SongPlayerButtonId, 0.5, 0.5);
                driver.WaitForIdle();
                Assert.False(driver.IsEnabled(SongTempoSliderId));
                Assert.False(driver.IsEnabled(SongProgressSliderId));
                Assert.False(driver.IsEnabled(DifficultyButton0Id));
                Assert.False(driver.IsEnabled(PreviewPlayButtonId));

                driver.ClickWithinElement(SongPlayerButtonId, 0.5, 0.5);
                driver.WaitForIdle();
                Assert.True(driver.IsEnabled(SongTempoSliderId));
                Assert.True(driver.IsEnabled(SongProgressSliderId));
                Assert.True(driver.IsEnabled(DifficultyButton0Id));
                Assert.True(driver.IsEnabled(AddDifficultyButtonId));
                Assert.Equal(previewButtonInitiallyEnabled, driver.IsEnabled(PreviewPlayButtonId));
            });
        }

        [Fact]
        public void PlaybackViaSpaceShortcutDisablesAndReenablesTimelineControls() {
            RunOpenedFixtureMapTestLocal((driver, _) => {
                driver.SendKeyboardShortcut("Space");
                driver.WaitForIdle();
                Assert.False(driver.IsEnabled(SongTempoSliderId));
                Assert.False(driver.IsEnabled(SongProgressSliderId));

                driver.SendKeyboardShortcut("Space");
                driver.WaitForIdle();
                Assert.True(driver.IsEnabled(SongTempoSliderId));
                Assert.True(driver.IsEnabled(SongProgressSliderId));
            });
        }

        [Fact]
        public void SpaceShortcutIsIgnoredWhenTextboxHasFocus() {
            RunOpenedFixtureMapTestLocal((driver, _) => {
                var currentSongName = driver.GetText(SongNameTextBoxId);
                driver.SetText(SongNameTextBoxId, currentSongName);
                driver.WaitForIdle();
                Assert.True(driver.IsEnabled(SongTempoSliderId));

                driver.SendKeyboardShortcut("Space");
                driver.WaitForIdle();

                Assert.True(driver.IsEnabled(SongTempoSliderId));
                Assert.True(driver.IsEnabled(SongProgressSliderId));
            });
        }

        [Fact]
        public void CtrlGTogglesSnapAndKeepsCheckboxAndMenuInSync() {
            RunOpenedFixtureMapTestLocal((driver, _) => {
                var initialSnapState = driver.IsChecked(GridSnapCheckboxId);
                driver.SendKeyboardShortcut("Ctrl+G");
                driver.WaitForIdle();
                Assert.Equal(!initialSnapState, driver.IsChecked(GridSnapCheckboxId));
                Assert.Equal(driver.IsChecked(GridSnapCheckboxId), driver.IsMenuItemChecked("Edit>Snap Notes to Grid"));

                driver.SendKeyboardShortcut("Ctrl+G");
                driver.WaitForIdle();
                Assert.Equal(initialSnapState, driver.IsChecked(GridSnapCheckboxId));
                Assert.Equal(driver.IsChecked(GridSnapCheckboxId), driver.IsMenuItemChecked("Edit>Snap Notes to Grid"));
            });
        }

        [Fact]
        public void SnapMenuToggleKeepsCheckboxAndMenuInSync() {
            RunOpenedFixtureMapTestLocal((driver, _) => {
                var initialSnapState = driver.IsChecked(GridSnapCheckboxId);
                driver.SelectMenuItem("Edit>Snap Notes to Grid");
                driver.WaitForIdle();
                Assert.Equal(!initialSnapState, driver.IsChecked(GridSnapCheckboxId));
                Assert.Equal(driver.IsChecked(GridSnapCheckboxId), driver.IsMenuItemChecked("Edit>Snap Notes to Grid"));

                driver.SelectMenuItem("Edit>Snap Notes to Grid");
                driver.WaitForIdle();
                Assert.Equal(initialSnapState, driver.IsChecked(GridSnapCheckboxId));
                Assert.Equal(driver.IsChecked(GridSnapCheckboxId), driver.IsMenuItemChecked("Edit>Snap Notes to Grid"));
            });
        }

        [Fact]
        public void GridSnapCheckboxToggleKeepsCheckboxAndMenuInSync() {
            RunOpenedFixtureMapTestLocal((driver, _) => {
                var initialSnapState = driver.IsChecked(GridSnapCheckboxId);
                driver.ToggleCheckbox(GridSnapCheckboxId, !initialSnapState);
                driver.WaitForIdle();
                Assert.Equal(!initialSnapState, driver.IsChecked(GridSnapCheckboxId));
                Assert.Equal(driver.IsChecked(GridSnapCheckboxId), driver.IsMenuItemChecked("Edit>Snap Notes to Grid"));
            });
        }

        [Fact]
        public void CtrlLeftBracketTogglesLeftSidebarVisibility() {
            RunOpenedFixtureMapTestLocal((driver, _) => {
                var initialState = driver.IsVisible(LeftSidebarSentinelId);
                driver.SendKeyboardShortcut("Ctrl+[");
                driver.WaitForIdle();
                Assert.Equal(!initialState, driver.IsVisible(LeftSidebarSentinelId));

                driver.SendKeyboardShortcut("Ctrl+[");
                driver.WaitForIdle();
                Assert.Equal(initialState, driver.IsVisible(LeftSidebarSentinelId));
            });
        }

        [Fact]
        public void CtrlRightBracketTogglesRightSidebarVisibility() {
            RunOpenedFixtureMapTestLocal((driver, _) => {
                var initialState = driver.IsVisible(RightSidebarSentinelId);
                driver.SendKeyboardShortcut("Ctrl+]");
                driver.WaitForIdle();
                Assert.Equal(!initialState, driver.IsVisible(RightSidebarSentinelId));

                driver.SendKeyboardShortcut("Ctrl+]");
                driver.WaitForIdle();
                Assert.Equal(initialState, driver.IsVisible(RightSidebarSentinelId));
            });
        }

        [Theory]
        [InlineData("View>Toggle Left Sidebar", LeftSidebarSentinelId)]
        [InlineData("View>Toggle Right Sidebar", RightSidebarSentinelId)]
        public void ViewMenuToggleActionsChangeSidebarVisibility(string menuPath, string sidebarSentinelId) {
            RunOpenedFixtureMapTestLocal((driver, _) => {
                var initialState = driver.IsVisible(sidebarSentinelId);
                driver.SelectMenuItem(menuPath);
                driver.WaitForIdle();
                Assert.Equal(!initialState, driver.IsVisible(sidebarSentinelId));

                driver.SelectMenuItem(menuPath);
                driver.WaitForIdle();
                Assert.Equal(initialState, driver.IsVisible(sidebarSentinelId));
            });
        }

        [Fact]
        public void AddDifficultyCreatesAdditionalDifficultySlot() {
            RunOpenedFixtureMapTestLocal((driver, _) => {
                driver.ClickButton(AddDifficultyButtonId);
                driver.InvokeCommand(DialogNoCommandId);
                driver.WaitForIdle();

                Assert.True(driver.IsVisible(DifficultyButton1Id));
                Assert.True(driver.IsEnabled(DeleteDifficultyButtonId));
            });
        }

        [Fact]
        public void DeleteDifficultyReturnsToSingleDifficultyState() {
            RunOpenedFixtureMapTestLocal((driver, _) => {
                driver.ClickButton(AddDifficultyButtonId);
                driver.InvokeCommand(DialogNoCommandId);
                driver.WaitForIdle();

                driver.ClickButton(DeleteDifficultyButtonId);
                driver.InvokeCommand(DialogYesCommandId);
                driver.WaitForIdle();

                Assert.True(driver.IsVisible(DifficultyButton0Id));
                Assert.False(driver.IsVisible(DifficultyButton1Id));
                Assert.False(driver.IsVisible(DifficultyButton2Id));
                Assert.False(driver.IsEnabled(DeleteDifficultyButtonId));
            });
        }

        [Fact]
        public void DifficultyButtonsUpdateAtMinimumAndMaximumDifficultyCounts() {
            RunOpenedFixtureMapTestLocal((driver, _) => {
                driver.ClickButton(AddDifficultyButtonId);
                driver.InvokeCommand(DialogNoCommandId);
                driver.WaitForIdle();
                driver.ClickButton(AddDifficultyButtonId);
                driver.InvokeCommand(DialogNoCommandId);
                driver.WaitForIdle();

                Assert.True(driver.IsVisible(DifficultyButton0Id));
                Assert.True(driver.IsVisible(DifficultyButton1Id));
                Assert.True(driver.IsVisible(DifficultyButton2Id));
                Assert.False(driver.IsEnabled(AddDifficultyButtonId));
                Assert.True(driver.IsEnabled(DeleteDifficultyButtonId));

                driver.ClickButton(DeleteDifficultyButtonId);
                driver.InvokeCommand(DialogYesCommandId);
                driver.WaitForIdle();
                driver.ClickButton(DeleteDifficultyButtonId);
                driver.InvokeCommand(DialogYesCommandId);
                driver.WaitForIdle();

                Assert.True(driver.IsVisible(DifficultyButton0Id));
                Assert.False(driver.IsVisible(DifficultyButton1Id));
                Assert.False(driver.IsVisible(DifficultyButton2Id));
                Assert.True(driver.IsEnabled(AddDifficultyButtonId));
                Assert.False(driver.IsEnabled(DeleteDifficultyButtonId));
            });
        }

        [Theory]
        [InlineData(SongBpmTextBoxId, "0")]
        [InlineData(DifficultyNumberTextBoxId, "-1")]
        [InlineData(DifficultyNumberTextBoxId, "999")]
        [InlineData(NoteSpeedTextBoxId, "0")]
        [InlineData(BronzeMedalDistanceTextBoxId, "-1")]
        [InlineData(SilverMedalDistanceTextBoxId, "-1")]
        [InlineData(GoldMedalDistanceTextBoxId, "-1")]
        [InlineData(GridSpacingTextBoxId, "abc")]
        [InlineData(GridDivisionTextBoxId, "0")]
        public void InvalidNumericInputRestoresPreviousValue(string controlId, string invalidValue) {
            RunOpenedFixtureMapTestLocal((driver, _) => { AssertInvalidTextInputRestored(driver, controlId, invalidValue); });
        }

        [Fact]
        public void BpmChangeCancelKeepsPreviousValue() {
            RunOpenedFixtureMapTestLocal((driver, _) => {
                var previousBpm = driver.GetText(SongBpmTextBoxId);
                var newBpm = BuildNewBpmValue(previousBpm);

                driver.SetText(SongBpmTextBoxId, newBpm);
                driver.SendKeyboardShortcut("Enter");
                driver.InvokeCommand(DialogCancelCommandId);
                driver.WaitForIdle();

                Assert.Equal(previousBpm, driver.GetText(SongBpmTextBoxId));
            });
        }

        [Fact]
        public void BpmChangeWithoutRetimeKeepsNewValue() {
            RunOpenedFixtureMapTestLocal((driver, _) => {
                var newBpm = BuildNewBpmValue(driver.GetText(SongBpmTextBoxId));

                driver.SetText(SongBpmTextBoxId, newBpm);
                driver.SendKeyboardShortcut("Enter");
                driver.InvokeCommand(DialogNoCommandId);
                driver.WaitForIdle();

                Assert.Equal(newBpm, driver.GetText(SongBpmTextBoxId));
            });
        }

        [Fact]
        public void BpmChangeWithRetimeKeepsNewValue() {
            RunOpenedFixtureMapTestLocal((driver, _) => {
                var newBpm = BuildNewBpmValue(driver.GetText(SongBpmTextBoxId));

                driver.SetText(SongBpmTextBoxId, newBpm);
                driver.SendKeyboardShortcut("Enter");
                driver.InvokeCommand(DialogYesCommandId);
                driver.WaitForIdle();

                Assert.Equal(newBpm, driver.GetText(SongBpmTextBoxId));
            });
        }

        [Fact]
        public void ExplicitCheckboxCanBeToggled() {
            RunOpenedFixtureMapTestLocal((driver, _) => {
                var initial = driver.IsChecked(ExplicitCheckboxId);
                driver.ToggleCheckbox(ExplicitCheckboxId, !initial);
                driver.WaitForIdle();
                Assert.Equal(!initial, driver.IsChecked(ExplicitCheckboxId));
            });
        }

        [Fact]
        public void WaveformCheckboxCanBeToggled() {
            RunOpenedFixtureMapTestLocal((driver, _) => {
                var initial = driver.IsChecked(WaveformCheckboxId);
                driver.ToggleCheckbox(WaveformCheckboxId, !initial);
                driver.WaitForIdle();
                Assert.Equal(!initial, driver.IsChecked(WaveformCheckboxId));
            });
        }

        [Fact]
        public void MetronomeCheckboxCanBeToggled() {
            RunOpenedFixtureMapTestLocal((driver, _) => {
                var initial = driver.IsChecked(MetronomeCheckboxId);
                driver.ToggleCheckbox(MetronomeCheckboxId, !initial);
                driver.WaitForIdle();
                Assert.Equal(!initial, driver.IsChecked(MetronomeCheckboxId));
            });
        }

        [Fact]
        public void VolumeSlidersUpdateDisplayedPercentages() {
            RunOpenedFixtureMapTestLocal((driver, _) => {
                driver.SetSliderValue(SongVolumeSliderId, 0.25);
                driver.SetSliderValue(DrumVolumeSliderId, 0.65);
                driver.WaitForIdle();

                Assert.Equal("25%", driver.GetText(SongVolumeTextId));
                Assert.Equal("65%", driver.GetText(DrumVolumeTextId));
            });
        }

        [Fact]
        public void SongTempoSliderUpdatesDisplayedTempoText() {
            RunOpenedFixtureMapTestLocal((driver, _) => {
                driver.SetSliderValue(SongTempoSliderId, 1.5);
                driver.WaitForIdle();
                AssertTempoText(driver, 1.5);
            });
        }

        [Fact]
        public void DoubleClickingSongTempoSliderResetsDefaultTempo() {
            RunOpenedFixtureMapTestLocal((driver, _) => {
                driver.SetSliderValue(SongTempoSliderId, 1.6);
                driver.WaitForIdle();
                AssertTempoText(driver, 1.6);

                driver.DoubleClickElement(SongTempoSliderId);
                driver.WaitForIdle();

                AssertTempoText(driver, 1.0);
            });
        }

        [Fact]
        public void SongProgressSliderUpdatesDisplayedSongPosition() {
            RunOpenedFixtureMapTestLocal((driver, _) => {
                var initialPosition = driver.GetText(SongPositionTextId);
                driver.SetSliderValue(SongProgressSliderId, 3000);
                driver.WaitForIdle();
                Assert.NotEqual(initialPosition, driver.GetText(SongPositionTextId));
            });
        }

        [Fact]
        public void EnvironmentSelectionCanBeChanged() {
            RunOpenedFixtureMapTestLocal((driver, _) => {
                driver.SelectDropdown(EnvironmentComboBoxId, "Helheim");
                driver.WaitForIdle();
                Assert.Equal("Helheim", driver.GetSelectedValue(EnvironmentComboBoxId));
            });
        }

        [Fact]
        public void EnvironmentSelectionPersistsAfterSaveAndReopen() {
            var driver = new AvaloniaUIDriver();
            string? mapFolder = null;

            try {
                mapFolder = LaunchAndOpenFixtureMapLocal(driver);
                driver.SelectDropdown(EnvironmentComboBoxId, "Helheim");
                driver.WaitForIdle();
                driver.SendKeyboardShortcut("Ctrl+S");
                driver.WaitForIdle();

                driver.SendKeyboardShortcut("Ctrl+W");
                driver.WaitForStartWindow();
                Assert.True(driver.IsVisible(StartWindowId));

                driver.SetTestFileSelection(mapFolder);
                driver.ClickButton(StartupOpenMapButtonId);
                driver.WaitForMainWindow();
                Assert.Equal("Helheim", driver.GetSelectedValue(EnvironmentComboBoxId));
            } finally {
                driver.Shutdown();
                SafeDeleteDirectoryLocal(mapFolder);
            }
        }

        [Fact]
        public void OpenMapMenuLoadsSelectedMapFolder() {
            var driver = new AvaloniaUIDriver();
            string? firstMapFolder = null;
            string? secondMapFolder = null;

            try {
                firstMapFolder = CreateFixtureMapCopyLocal();
                secondMapFolder = CreateFixtureMapCopyLocal();
                LaunchAndOpenMapLocal(driver, firstMapFolder);

                var editedSongName = $"OpenSource {Guid.NewGuid():N}";
                driver.SetText(SongNameTextBoxId, editedSongName);
                CommitTextboxEdits(driver);
                driver.SendKeyboardShortcut("Ctrl+S");
                driver.WaitForIdle();
                Assert.Equal(editedSongName, driver.GetText(SongNameTextBoxId));

                driver.SetTestFileSelection(secondMapFolder);
                driver.SelectMenuItem("File>Open Map");
                driver.WaitForMainWindow();
                Assert.Equal("Fixture Song", driver.GetText(SongNameTextBoxId));
            } finally {
                driver.Shutdown();
                SafeDeleteDirectoryLocal(firstMapFolder);
                SafeDeleteDirectoryLocal(secondMapFolder);
            }
        }

        [Fact]
        public void CtrlOShortcutLoadsSelectedMapFolder() {
            var driver = new AvaloniaUIDriver();
            string? firstMapFolder = null;
            string? secondMapFolder = null;

            try {
                firstMapFolder = CreateFixtureMapCopyLocal();
                secondMapFolder = CreateFixtureMapCopyLocal();
                LaunchAndOpenMapLocal(driver, firstMapFolder);

                var editedSongName = $"OpenShortcut {Guid.NewGuid():N}";
                driver.SetText(SongNameTextBoxId, editedSongName);
                CommitTextboxEdits(driver);
                driver.SendKeyboardShortcut("Ctrl+S");
                driver.WaitForIdle();
                Assert.Equal(editedSongName, driver.GetText(SongNameTextBoxId));

                driver.SetTestFileSelection(secondMapFolder);
                driver.SendKeyboardShortcut("Ctrl+O");
                driver.WaitForMainWindow();
                Assert.Equal("Fixture Song", driver.GetText(SongNameTextBoxId));
            } finally {
                driver.Shutdown();
                SafeDeleteDirectoryLocal(firstMapFolder);
                SafeDeleteDirectoryLocal(secondMapFolder);
            }
        }

        [Fact]
        public void CtrlOWithUnsavedChangesAndCancelKeepsCurrentMapOpen() {
            RunOpenedFixtureMapTestLocal((driver, _) => {
                var editedSongName = $"KeepOpen {Guid.NewGuid():N}";
                driver.SetText(SongNameTextBoxId, editedSongName);
                CommitTextboxEdits(driver);

                driver.SendKeyboardShortcut("Ctrl+O");
                driver.InvokeCommand(DialogCancelCommandId);
                driver.WaitForMainWindow();

                Assert.True(driver.IsVisible(MainWindowId));
                Assert.Equal(editedSongName, driver.GetText(SongNameTextBoxId));
            });
        }

        [Fact]
        public void PickSongButtonLoadsSelectedOggIntoCurrentMap() {
            RunOpenedFixtureMapTestLocal((driver, mapFolder) => {
                var pickerSourceFolder = CreateTempOutputFolderLocal("pick-song-source");
                try {
                    var sourceSongPath = Path.Combine(GetRepositoryRootLocal(), FixtureMapFolderRelative, "song.ogg");
                    var pickedSongName = "pickedsong.ogg";
                    var pickedSongPath = Path.Combine(pickerSourceFolder, pickedSongName);
                    var expectedSongFileName = "song.ogg";
                    File.Copy(sourceSongPath, pickedSongPath, overwrite: true);

                    driver.SetTestFileSelection(pickedSongPath);
                    driver.ClickButton(PickSongButtonId);
                    driver.WaitForIdle();

                    Assert.Equal(expectedSongFileName, driver.GetText(SongFileNameTextId));
                    Assert.True(File.Exists(Path.Combine(mapFolder, expectedSongFileName)));
                } finally {
                    SafeDeleteDirectoryLocal(pickerSourceFolder);
                }
            });
        }

        [Fact]
        public void PickCoverButtonLoadsSelectedJpegIntoCurrentMap() {
            RunOpenedFixtureMapTestLocal((driver, mapFolder) => {
                var pickerSourceFolder = CreateTempOutputFolderLocal("pick-cover-source");
                try {
                    var sourceCoverPath = Path.Combine(GetRepositoryRootLocal(), FixtureMapFolderRelative, "cover.jpg");
                    var pickedCoverName = "pickedcover.jpg";
                    var pickedCoverPath = Path.Combine(pickerSourceFolder, pickedCoverName);
                    var expectedCoverFileName = "cover.jpg";
                    File.Copy(sourceCoverPath, pickedCoverPath, overwrite: true);

                    driver.SetTestFileSelection(pickedCoverPath);
                    driver.ClickButton(PickCoverButtonId);
                    _ = driver.TryInvokeCommand(DialogNoCommandId);
                    driver.WaitForIdle();

                    Assert.Equal(expectedCoverFileName, driver.GetText(CoverFileNameTextId));
                    Assert.True(File.Exists(Path.Combine(mapFolder, expectedCoverFileName)));
                } finally {
                    SafeDeleteDirectoryLocal(pickerSourceFolder);
                }
            });
        }

        [Fact]
        public void NewMapMenuPickerFlowCreatesAndLoadsMapFromSelectedSong() {
            var driver = new AvaloniaUIDriver();
            string? initialMapFolder = null;
            string? newMapFolder = null;
            string? pickerSourceFolder = null;

            try {
                initialMapFolder = CreateFixtureMapCopyLocal();
                LaunchAndOpenMapLocal(driver, initialMapFolder);
                newMapFolder = CreateAlphabeticMapFolderLocal("new-map-picker");
                pickerSourceFolder = CreateTempOutputFolderLocal("new-map-song-source");

                var sourceSongPath = Path.Combine(GetRepositoryRootLocal(), FixtureMapFolderRelative, "song.ogg");
                var pickedSongName = "newmapsong.ogg";
                var pickedSongPath = Path.Combine(pickerSourceFolder, pickedSongName);
                File.Copy(sourceSongPath, pickedSongPath, overwrite: true);

                driver.SetTestFileSelections(newMapFolder, pickedSongPath);
                driver.SelectMenuItem("File>New Map");
                driver.WaitForMainWindow();

                Assert.True(driver.IsVisible(MainWindowId));
                Assert.Equal("song.ogg", driver.GetText(SongFileNameTextId));
                Assert.True(File.Exists(Path.Combine(newMapFolder, "info.dat")));
                Assert.True(File.Exists(Path.Combine(newMapFolder, "song.ogg")));
            } finally {
                driver.Shutdown();
                SafeDeleteDirectoryLocal(initialMapFolder);
                SafeDeleteDirectoryLocal(newMapFolder);
                SafeDeleteDirectoryLocal(pickerSourceFolder);
            }
        }

        [Fact]
        public void ImportMapMenuPickerFlowCreatesAndLoadsConvertedMap() {
            var driver = new AvaloniaUIDriver();
            string? initialMapFolder = null;
            string? importTargetFolder = null;
            string? importSourceFolder = null;

            try {
                initialMapFolder = CreateFixtureMapCopyLocal();
                LaunchAndOpenMapLocal(driver, initialMapFolder);
                importTargetFolder = CreateAlphabeticMapFolderLocal("import-map-picker");
                var importFixture = CreateStepManiaImportFixtureLocal();
                importSourceFolder = importFixture.fixtureFolder;

                driver.SetTestFileSelections(importTargetFolder, importFixture.simfilePath);
                driver.SelectMenuItem("File>Import Map");
                driver.WaitForMainWindow();

                Assert.True(driver.IsVisible(MainWindowId));
                Assert.Equal("Picker Import Song", driver.GetText(SongNameTextBoxId));
                Assert.True(File.Exists(Path.Combine(importTargetFolder, "info.dat")));
                Assert.True(File.Exists(Path.Combine(importTargetFolder, "song.ogg")));
            } finally {
                driver.Shutdown();
                SafeDeleteDirectoryLocal(initialMapFolder);
                SafeDeleteDirectoryLocal(importTargetFolder);
                SafeDeleteDirectoryLocal(importSourceFolder);
            }
        }

        [Fact]
        public void CtrlSCreatesAutosaveSnapshot() {
            RunOpenedFixtureMapTestLocal((driver, mapFolder) => {
                var autosaveFolder = Path.Combine(mapFolder, AutosaveFolderName);
                if (Directory.Exists(autosaveFolder)) {
                    Directory.Delete(autosaveFolder, true);
                }

                driver.SendKeyboardShortcut("Ctrl+S");
                driver.WaitForIdle();

                Assert.True(Directory.Exists(autosaveFolder));
                Assert.NotEmpty(Directory.GetDirectories(autosaveFolder));
            });
        }

        [Fact]
        public void SaveMenuCreatesAutosaveSnapshot() {
            RunOpenedFixtureMapTestLocal((driver, mapFolder) => {
                var autosaveFolder = Path.Combine(mapFolder, AutosaveFolderName);
                if (Directory.Exists(autosaveFolder)) {
                    Directory.Delete(autosaveFolder, true);
                }

                driver.SelectMenuItem("File>Save Map");
                driver.WaitForIdle();

                Assert.True(Directory.Exists(autosaveFolder));
                Assert.NotEmpty(Directory.GetDirectories(autosaveFolder));
            });
        }

        [Fact]
        public void ClearCacheMenuDeletesCacheFolderWhenConfirmed() {
            RunOpenedFixtureMapTestLocal((driver, mapFolder) => {
                var cacheFolder = Path.Combine(mapFolder, CacheFolderName);
                Directory.CreateDirectory(cacheFolder);
                File.WriteAllText(Path.Combine(cacheFolder, "dummy.cache"), "fixture");

                driver.SelectMenuItem("Tools>Clear Cache");
                driver.InvokeCommand(DialogYesCommandId);
                driver.WaitForIdle();

                Assert.False(Directory.Exists(cacheFolder));
            });
        }

        [Fact]
        public void ClearCacheMenuDoesNotDeleteCacheFolderWhenCancelled() {
            RunOpenedFixtureMapTestLocal((driver, mapFolder) => {
                var cacheFolder = Path.Combine(mapFolder, CacheFolderName);
                Directory.CreateDirectory(cacheFolder);
                File.WriteAllText(Path.Combine(cacheFolder, "dummy.cache"), "fixture");

                driver.SelectMenuItem("Tools>Clear Cache");
                driver.InvokeCommand(DialogCancelCommandId);
                driver.WaitForIdle();

                Assert.True(Directory.Exists(cacheFolder));
            });
        }

        [Fact]
        public void DefaultSettingsApplyExpectedMenuAndOverlayVisibility() {
            RunOpenedFixtureMapTestLocal((driver, _) => {
                Assert.True(driver.IsMenuItemVisible("Tools>Clear Cache"));
                Assert.True(driver.IsVisible(NavWaveformImageId));
                Assert.False(driver.IsVisible(NavBpmChangesCanvasId));
                Assert.False(driver.IsVisible(NavNotesCanvasId));
                Assert.False(driver.IsVisible(DifficultyPredictionLabelId));
            });
        }

        [Fact]
        public void DisablingSpectrogramInSettingsHidesClearCacheMenuItem() {
            RunOpenedFixtureMapTestLocal((driver, _) => {
                Assert.True(driver.IsMenuItemVisible("Tools>Clear Cache"));

                driver.SelectMenuItem("Tools>Settings");
                driver.WaitForIdle();
                driver.ToggleCheckbox(SettingsShowSpectrogramCheckboxId, false);
                driver.WaitForIdle();
                driver.ClickButton(SettingsSaveButtonId);
                driver.WaitForIdle();

                Assert.False(driver.IsMenuItemVisible("Tools>Clear Cache"));
            });
        }

        [Fact]
        public void ExportMenuCreatesZipPackageInSelectedFolder() {
            RunOpenedFixtureMapTestLocal((driver, _) => {
                var exportFolder = CreateTempOutputFolderLocal("export-menu");

                try {
                    driver.SetTestFileSelection(exportFolder);
                    driver.SelectMenuItem("File>Export Map");
                    driver.WaitForIdle();

                    Assert.NotEmpty(Directory.GetFiles(exportFolder, "*.zip"));
                } finally {
                    SafeDeleteDirectoryLocal(exportFolder);
                }
            });
        }

        [Fact]
        public void CtrlECreatesZipPackageInSelectedFolder() {
            RunOpenedFixtureMapTestLocal((driver, _) => {
                var exportFolder = CreateTempOutputFolderLocal("export-shortcut");

                try {
                    driver.SetTestFileSelection(exportFolder);
                    driver.SendKeyboardShortcut("Ctrl+E");
                    driver.WaitForIdle();

                    Assert.NotEmpty(Directory.GetFiles(exportFolder, "*.zip"));
                } finally {
                    SafeDeleteDirectoryLocal(exportFolder);
                }
            });
        }

        [Fact]
        public void ExportIncludesInfoSongCoverAndAllDifficultyFiles() {
            RunOpenedFixtureMapTestLocal((driver, mapFolder) => {
                driver.ClickButton(AddDifficultyButtonId);
                driver.InvokeCommand(DialogNoCommandId);
                driver.WaitForIdle();
                driver.ClickButton(AddDifficultyButtonId);
                driver.InvokeCommand(DialogNoCommandId);
                driver.WaitForIdle();

                driver.SendKeyboardShortcut("Ctrl+S");
                driver.WaitForIdle();

                var exportFolder = CreateTempOutputFolderLocal("export-contents");
                try {
                    driver.SetTestFileSelection(exportFolder);
                    driver.SelectMenuItem("File>Export Map");
                    driver.WaitForIdle();

                    var zipPath = Directory.GetFiles(exportFolder, "*.zip").Single();
                    var expectedFiles = BuildExpectedExportEntries(mapFolder);
                    var actualEntries = ReadZipEntries(zipPath);

                    foreach (var expectedFile in expectedFiles) {
                        Assert.Contains(expectedFile, actualEntries);
                    }
                } finally {
                    SafeDeleteDirectoryLocal(exportFolder);
                }
            });
        }

        [Fact]
        public void SongNamePersistsAfterSaveAndReopen() {
            var driver = new AvaloniaUIDriver();
            string? mapFolder = null;

            try {
                mapFolder = LaunchAndOpenFixtureMapLocal(driver);
                var updatedSongName = $"MainWindow Test {Guid.NewGuid():N}";

                driver.SetText(SongNameTextBoxId, updatedSongName);
                CommitTextboxEdits(driver);
                driver.SendKeyboardShortcut("Ctrl+S");
                driver.WaitForIdle();

                driver.SendKeyboardShortcut("Ctrl+W");
                driver.WaitForStartWindow();
                Assert.True(driver.IsVisible(StartWindowId));

                driver.SetTestFileSelection(mapFolder);
                driver.ClickButton(StartupOpenMapButtonId);
                driver.WaitForMainWindow();

                Assert.Equal(updatedSongName, driver.GetText(SongNameTextBoxId));
            } finally {
                driver.Shutdown();
                SafeDeleteDirectoryLocal(mapFolder);
            }
        }

        [Fact]
        public void ArtistAndMapperNamesPersistAfterSaveAndReopen() {
            var driver = new AvaloniaUIDriver();
            string? mapFolder = null;

            try {
                mapFolder = LaunchAndOpenFixtureMapLocal(driver);
                var updatedArtist = $"Artist {Guid.NewGuid():N}";
                var updatedMapper = $"Mapper {Guid.NewGuid():N}";

                driver.SetText(ArtistNameTextBoxId, updatedArtist);
                CommitTextboxEdits(driver);
                driver.SetText(MapperNameTextBoxId, updatedMapper);
                CommitTextboxEdits(driver);
                driver.SendKeyboardShortcut("Ctrl+S");
                driver.WaitForIdle();

                driver.SendKeyboardShortcut("Ctrl+W");
                driver.WaitForStartWindow();
                Assert.True(driver.IsVisible(StartWindowId));

                driver.SetTestFileSelection(mapFolder);
                driver.ClickButton(StartupOpenMapButtonId);
                driver.WaitForMainWindow();

                Assert.Equal(updatedArtist, driver.GetText(ArtistNameTextBoxId));
                Assert.Equal(updatedMapper, driver.GetText(MapperNameTextBoxId));
            } finally {
                driver.Shutdown();
                SafeDeleteDirectoryLocal(mapFolder);
            }
        }

        [Fact]
        public void CtrlWWithoutUnsavedChangesReturnsToStartWindow() {
            RunOpenedFixtureMapTestLocal((driver, _) => {
                driver.SendKeyboardShortcut("Ctrl+W");
                driver.WaitForStartWindow();
                Assert.True(driver.IsVisible(StartWindowId));
            });
        }

        [Fact]
        public void CtrlWWithUnsavedChangesAndCancelKeepsMainWindowOpen() {
            RunOpenedFixtureMapTestLocal((driver, _) => {
                MarkCurrentMapDirty(driver);
                driver.SendKeyboardShortcut("Ctrl+W");
                driver.InvokeCommand(DialogCancelCommandId);
                driver.WaitForMainWindow();
                Assert.True(driver.IsVisible(MainWindowId));
            });
        }

        [Fact]
        public void CtrlWWithUnsavedChangesAndDontSaveReturnsToStartWindow() {
            RunOpenedFixtureMapTestLocal((driver, _) => {
                MarkCurrentMapDirty(driver);
                driver.SendKeyboardShortcut("Ctrl+W");
                driver.InvokeCommand(DialogNoCommandId);
                driver.WaitForStartWindow();
                Assert.True(driver.IsVisible(StartWindowId));
            });
        }

        [Fact]
        public void CtrlWWithUnsavedChangesAndSavePersistsChanges() {
            var driver = new AvaloniaUIDriver();
            string? mapFolder = null;

            try {
                mapFolder = LaunchAndOpenFixtureMapLocal(driver);
                var updatedSongName = $"SavedViaClose {Guid.NewGuid():N}";
                driver.SetText(SongNameTextBoxId, updatedSongName);
                CommitTextboxEdits(driver);

                driver.SendKeyboardShortcut("Ctrl+W");
                driver.InvokeCommand(DialogYesCommandId);
                driver.WaitForStartWindow();
                Assert.True(driver.IsVisible(StartWindowId));

                driver.SetTestFileSelection(mapFolder);
                driver.ClickButton(StartupOpenMapButtonId);
                driver.WaitForMainWindow();
                Assert.Equal(updatedSongName, driver.GetText(SongNameTextBoxId));
            } finally {
                driver.Shutdown();
                SafeDeleteDirectoryLocal(mapFolder);
            }
        }

        [Fact]
        public void CloseMapMenuWithoutUnsavedChangesReturnsToStartWindow() {
            RunOpenedFixtureMapTestLocal((driver, _) => {
                driver.SelectMenuItem("File>Close Map");
                driver.WaitForStartWindow();
                Assert.True(driver.IsVisible(StartWindowId));
            });
        }

        [Fact]
        public void CloseMapMenuWithUnsavedChangesAndCancelKeepsMainWindowOpen() {
            RunOpenedFixtureMapTestLocal((driver, _) => {
                MarkCurrentMapDirty(driver);
                driver.SelectMenuItem("File>Close Map");
                driver.InvokeCommand(DialogCancelCommandId);
                driver.WaitForMainWindow();
                Assert.True(driver.IsVisible(MainWindowId));
            });
        }

        [Theory]
        [InlineData("Tools>BPM Finder", BpmWindowSentinelId)]
        [InlineData("Tools>Difficulty Predictor", PredictorWindowSentinelId)]
        [InlineData("Tools>Settings", SettingsWindowSentinelId)]
        [InlineData("Help>About Edda", AboutWindowSentinelId)]
        public void MenuItemsOpenExpectedAuxiliaryWindows(string menuPath, string sentinelControlId) {
            RunOpenedFixtureMapTestLocal((driver, _) => {
                driver.SelectMenuItem(menuPath);
                driver.WaitForIdle();
                Assert.True(driver.IsVisible(sentinelControlId));
            });
        }

        [Theory]
        [InlineData(ChangeBpmButtonId, ChangeBpmWindowSentinelId)]
        [InlineData(CustomizeNavBarButtonId, CustomizeNavBarWindowSentinelId)]
        [InlineData(CreatePreviewButtonId, SongPreviewWindowSentinelId)]
        public void MainWindowButtonsOpenExpectedAuxiliaryWindows(string buttonId, string sentinelControlId) {
            RunOpenedFixtureMapTestLocal((driver, _) => {
                driver.ClickButton(buttonId);
                driver.WaitForIdle();
                Assert.True(driver.IsVisible(sentinelControlId));
            });
        }

        [Fact]
        public void DraggingNavWaveformUpdatesSongPositionText() {
            RunOpenedFixtureMapTestLocal((driver, _) => {
                var initialPosition = driver.GetText(SongPositionTextId);
                driver.DragWithinElement(NavWaveformImageId, 0.5, 0.2, 0.5, 0.8);
                driver.WaitForIdle();
                Assert.NotEqual(initialPosition, driver.GetText(SongPositionTextId));

                var selectedBeatText = driver.GetText(SelectedBeatLabelId);
                Assert.Contains("Time:", selectedBeatText);
                Assert.Contains("Global Beat:", selectedBeatText);
            });
        }

        private static HashSet<string> BuildExpectedExportEntries(string mapFolder) {
            var expected = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "info.dat" };
            var infoPath = Path.Combine(mapFolder, "info.dat");
            using var doc = JsonDocument.Parse(File.ReadAllText(infoPath));
            var root = doc.RootElement;

            if (root.TryGetProperty("_songFilename", out var songFile) && songFile.ValueKind == JsonValueKind.String) {
                var songName = songFile.GetString();
                if (!string.IsNullOrWhiteSpace(songName)) {
                    expected.Add(songName);
                }
            }
            if (root.TryGetProperty("_coverImageFilename", out var coverFile) && coverFile.ValueKind == JsonValueKind.String) {
                var coverName = coverFile.GetString();
                if (!string.IsNullOrWhiteSpace(coverName)) {
                    expected.Add(coverName);
                }
            }
            if (root.TryGetProperty("_beatmapFilenames", out var beatmapFiles) && beatmapFiles.ValueKind == JsonValueKind.Array) {
                foreach (var beatmapFile in beatmapFiles.EnumerateArray()) {
                    if (beatmapFile.ValueKind != JsonValueKind.String) {
                        continue;
                    }
                    var beatmapName = beatmapFile.GetString();
                    if (!string.IsNullOrWhiteSpace(beatmapName)) {
                        expected.Add(beatmapName);
                    }
                }
            }

            var previewFile = Path.Combine(mapFolder, "preview.ogg");
            if (File.Exists(previewFile)) {
                expected.Add("preview.ogg");
            }

            return expected;
        }

        private static HashSet<string> ReadZipEntries(string zipPath) {
            using var archive = ZipFile.OpenRead(zipPath);
            return archive.Entries
                .Select(entry => Path.GetFileName(entry.FullName))
                .Where(entryName => !string.IsNullOrWhiteSpace(entryName))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        private static void RunOpenedFixtureMapTestLocal(Action<AvaloniaUIDriver, string> testBody) {
            var driver = new AvaloniaUIDriver();
            string? mapFolder = null;

            try {
                mapFolder = LaunchAndOpenFixtureMapLocal(driver);
                testBody(driver, mapFolder);
            } finally {
                driver.Shutdown();
                SafeDeleteDirectoryLocal(mapFolder);
            }
        }

        private static string LaunchAndOpenFixtureMapLocal(AvaloniaUIDriver driver) {
            var fixtureCopy = CreateFixtureMapCopyLocal();
            LaunchAndOpenMapLocal(driver, fixtureCopy);
            return fixtureCopy;
        }

        private static void LaunchAndOpenMapLocal(AvaloniaUIDriver driver, string mapFolder) {
            driver.Launch();
            driver.WaitForIdle();
            driver.SetTestFileSelection(mapFolder);
            driver.ClickButton(StartupOpenMapButtonId);
            driver.WaitForMainWindow();
        }

        private static string CreateFixtureMapCopyLocal() {
            var fixtureSourcePath = Path.Combine(GetRepositoryRootLocal(), FixtureMapFolderRelative);
            Assert.True(Directory.Exists(fixtureSourcePath), $"MainWindow fixture map folder was not found: {fixtureSourcePath}");

            var fixtureCopyPath = CreateTempOutputFolderLocal("fixture");
            CopyDirectoryRecursively(fixtureSourcePath, fixtureCopyPath);
            return fixtureCopyPath;
        }

        private static string CreateTempOutputFolderLocal(string tag) {
            var outputPath = Path.Combine(Path.GetTempPath(), "Edda-WpfMainWindowTests", tag, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(outputPath);
            return outputPath;
        }

        private static string CreateAlphabeticMapFolderLocal(string tag) {
            var mapFolder = Path.Combine(CreateTempOutputFolderLocal(tag), "PickerFlowMap");
            Directory.CreateDirectory(mapFolder);
            return mapFolder;
        }

        private static (string fixtureFolder, string simfilePath) CreateStepManiaImportFixtureLocal() {
            var fixtureFolder = CreateTempOutputFolderLocal("import-source");
            var songSourcePath = Path.Combine(GetRepositoryRootLocal(), FixtureMapFolderRelative, "song.ogg");
            var songFileName = "importsong.ogg";
            var songTargetPath = Path.Combine(fixtureFolder, songFileName);
            File.Copy(songSourcePath, songTargetPath, overwrite: true);

            var simfilePath = Path.Combine(fixtureFolder, "picker-import.sm");
            File.WriteAllText(simfilePath, BuildStepManiaSimfile("Picker Import Song", songFileName));
            return (fixtureFolder, simfilePath);
        }

        private static string BuildStepManiaSimfile(string title, string songFileName) {
            return
                $"#TITLE:{title};\n" +
                "#SUBTITLE:;\n" +
                "#ARTIST:Picker Artist;\n" +
                "#CREDIT:Picker Mapper;\n" +
                $"#MUSIC:{songFileName};\n" +
                "#OFFSET:0.000;\n" +
                "#BPMS:0.000=120.000;\n" +
                "#NOTES:\n" +
                "     dance-single:\n" +
                "     :\n" +
                "     Easy:\n" +
                "     3:\n" +
                "     0.000,0.000,0.000,0.000,0.000:\n" +
                "0000\n" +
                "1000\n" +
                "0000\n" +
                "0000\n" +
                ",\n" +
                "0000\n" +
                "0000\n" +
                "0100\n" +
                "0000\n" +
                ";\n";
        }

        private static string GetRepositoryRootLocal() {
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

        private static void SafeDeleteDirectoryLocal(string? directoryPath) {
            if (string.IsNullOrWhiteSpace(directoryPath) || !Directory.Exists(directoryPath)) {
                return;
            }

            try {
                Directory.Delete(directoryPath, true);
            } catch {
                // Best effort cleanup for temporary test paths.
            }
        }

        private static void AssertInvalidTextInputRestored(AvaloniaUIDriver driver, string controlId, string invalidValue) {
            var previousValue = driver.GetText(controlId);
            driver.SetText(controlId, invalidValue);
            CommitTextboxEdits(driver);
            _ = driver.TryInvokeCommand("DialogResult.Ok");
            driver.WaitForIdle();
            Assert.Equal(previousValue, driver.GetText(controlId));
        }

        private static void AssertTempoText(AvaloniaUIDriver driver, double expectedTempo) {
            var tempoText = driver.GetText(SongTempoTextId);
            Assert.EndsWith("x", tempoText, StringComparison.OrdinalIgnoreCase);

            var numericText = tempoText[..^1];
            var parsed = double.TryParse(numericText, NumberStyles.Float, CultureInfo.CurrentCulture, out var actualTempo) ||
                         double.TryParse(numericText, NumberStyles.Float, CultureInfo.InvariantCulture, out actualTempo);

            Assert.True(parsed, $"Could not parse tempo label '{tempoText}'.");
            Assert.Equal(expectedTempo, actualTempo, 2);
        }

        private static void CommitTextboxEdits(AvaloniaUIDriver driver) {
            driver.SendKeyboardShortcut("Enter");
            driver.SendKeyboardShortcut("Tab");
            driver.WaitForIdle();
        }

        private static string BuildNewBpmValue(string currentBpmText) {
            if (!double.TryParse(currentBpmText, NumberStyles.Float, CultureInfo.InvariantCulture, out var bpm) &&
                !double.TryParse(currentBpmText, out bpm)) {
                bpm = 120;
            }

            return (bpm + 7).ToString(CultureInfo.InvariantCulture);
        }

        private static void MarkCurrentMapDirty(AvaloniaUIDriver driver) {
            driver.SetText(SongNameTextBoxId, $"{driver.GetText(SongNameTextBoxId)}*");
            CommitTextboxEdits(driver);
        }
    }
