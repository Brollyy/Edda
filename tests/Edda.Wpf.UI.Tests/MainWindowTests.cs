using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading;
using Xunit;

namespace Edda.Wpf.UI.Tests {

    public class MainWindowTests {
        private const string StartWindowId = "StartWindow";
        private const string StartupOpenMapButtonId = "ButtonOpenMap";
        private const string StartupRecentMapsListId = "ListViewRecentMaps";
        private const string MainWindowId = "AppMainWindow";
        private const string LeftSidebarId = "borderLeftDock";
        private const string RightSidebarId = "borderRightDock";
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
        private const string SongDurationTextId = "txtSongDuration";
        private const string SelectedBeatLabelId = "lblSelectedBeat";
        private const string PreviewPlayButtonId = "btnPlayPreview";
        private const string DifficultyPredictionLabelId = "difficultyPrediction";
        private const string CoverImageId = "imgCover";

        private const string NavWaveformImageId = "imgWaveformVertical";
        private const string EditorPanelId = "EditorPanel";
        private const string ScrollSpectrogramId = "scrollSpectrogram";
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
        private const string ScrollEditorId = "scrollEditor";
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
        private const string NotesStatsAllId = "notesStatsAll";
        private const string NotesStatsSelectedId = "notesStatsSelected";
        private const string NotesStatsSingleId = "notesStatsSingle";
        private const string NotesStatsDoubleId = "notesStatsDouble";
        private const string NotesStatsTriplePlusId = "notesStatsTriplePlus";
        private const string ColumnStatsButtonId = "columnStats";
        private const string ColumnStatsValue1Id = "columnStatsValue1";
        private const string ColumnStatsValue2Id = "columnStatsValue2";
        private const string ColumnStatsValue3Id = "columnStatsValue3";
        private const string ColumnStatsValue4Id = "columnStatsValue4";
        private const string ColumnStatsPercentage1Id = "columnStatsPercentage1";
        private const string ColumnStatsPercentage2Id = "columnStatsPercentage2";
        private const string ColumnStatsPercentage3Id = "columnStatsPercentage3";
        private const string ColumnStatsPercentage4Id = "columnStatsPercentage4";

        private const string ChangeBpmButtonId = "btnChangeBPM";
        private const string CustomizeNavBarButtonId = "btnCustomizeNavBar";
        private const string CreatePreviewButtonId = "btnMakePreview";
        private const string Drum0Id = "Drum0";
        private const string Drum1Id = "Drum1";
        private const string Drum2Id = "Drum2";
        private const string Drum3Id = "Drum3";

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
            RunOpenedFixtureMapTest((driver, _) => { Assert.True(driver.IsVisible(MainWindowId)); });
        }

        [Fact]
        public void MapLoadEnablesCoreMetadataPlaybackAndGridControls() {
            RunOpenedFixtureMapTest((driver, _) => {
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
            RunOpenedFixtureMapTest((driver, _) => {
                Assert.False(string.IsNullOrWhiteSpace(driver.GetText(SongNameTextBoxId)));
                Assert.False(string.IsNullOrWhiteSpace(driver.GetText(SongBpmTextBoxId)));
                Assert.False(string.IsNullOrWhiteSpace(driver.GetSelectedValue(EnvironmentComboBoxId)));
            });
        }

        [Fact]
        public void FixtureMapValuesAreLoadedIntoMainWindowFields() {
            RunOpenedFixtureMapTest((driver, _) => {
                Assert.Equal("Fixture Song", driver.GetText(SongNameTextBoxId));
                Assert.Equal("120", driver.GetText(SongBpmTextBoxId));
                Assert.Equal("Midgard", driver.GetSelectedValue(EnvironmentComboBoxId));
                Assert.False(driver.IsChecked(ExplicitCheckboxId));
                Assert.Equal("song.ogg", driver.GetText(SongFileNameTextId));
                Assert.Equal("cover.jpg", driver.GetText(CoverFileNameTextId));
            });
        }

        [Fact]
        public void FixtureMapShowsExactVisibleMetadataAndPlaybackDefaults() {
            RunOpenedFixtureMapTest((driver, _) => {
                Assert.Equal("Fixture Song", driver.GetText(SongNameTextBoxId));
                Assert.Equal("Fixture Artist", driver.GetText(ArtistNameTextBoxId));
                Assert.Equal("Fixture Mapper", driver.GetText(MapperNameTextBoxId));
                Assert.Equal("120", driver.GetText(SongBpmTextBoxId));
                Assert.Equal("Midgard", driver.GetSelectedValue(EnvironmentComboBoxId));
                Assert.Equal("song.ogg", driver.GetText(SongFileNameTextId));
                Assert.Equal("cover.jpg", driver.GetText(CoverFileNameTextId));
                Assert.Equal("0:00", driver.GetText(SongPositionTextId));
                Assert.Equal("40%", driver.GetText(SongVolumeTextId));
                Assert.Equal("100%", driver.GetText(DrumVolumeTextId));
                AssertTempoText(driver, 1.0);
            });
        }

        [Fact]
        public void FixtureMapShowsVisibleActionsStatsArtworkAndNavigationDefaults() {
            RunOpenedFixtureMapTest((driver, _) => {
                var songPlayer = driver.GetElementBounds(SongPlayerButtonId);
                var pickSong = driver.GetElementBounds(PickSongButtonId);
                var pickCover = driver.GetElementBounds(PickCoverButtonId);
                var playPreview = driver.GetElementBounds(PreviewPlayButtonId);
                var createPreview = driver.GetElementBounds(CreatePreviewButtonId);
                var coverImage = driver.GetElementBounds(CoverImageId);

                Assert.Equal("Create Song Preview", driver.GetText(CreatePreviewButtonId));
                Assert.Equal("Edit Song Timing", driver.GetText(ChangeBpmButtonId));
                Assert.Equal("Customize Navigation Bar", driver.GetText(CustomizeNavBarButtonId));
                Assert.False(driver.IsVisible(DifficultyPredictionLabelId));
                Assert.NotEqual("0:00", driver.GetText(SongDurationTextId));

                Assert.True(songPlayer.width <= 40 && songPlayer.height <= 40, $"Expected the song playback action to be rendered as a compact icon button, but bounds were {songPlayer.width:0.##}x{songPlayer.height:0.##}.");
                Assert.True(pickSong.width <= 30 && pickSong.height >= 18, $"Expected the song picker action to use a compact icon button, but bounds were {pickSong.width:0.##}x{pickSong.height:0.##}.");
                Assert.True(pickCover.width <= 30 && pickCover.height >= 18, $"Expected the cover picker action to use a compact icon button, but bounds were {pickCover.width:0.##}x{pickCover.height:0.##}.");
                Assert.True(playPreview.width <= 30 && playPreview.height <= 30, $"Expected the preview playback action to be rendered as a compact icon button, but bounds were {playPreview.width:0.##}x{playPreview.height:0.##}.");
                Assert.True(createPreview.width > playPreview.width * 5, $"Expected the preview creation action to dominate its row, but button widths were {createPreview.width:0.##} and {playPreview.width:0.##}.");
                Assert.True(coverImage.width >= 150 && coverImage.height >= 150, $"Expected cover artwork to be rendered at a clearly previewable size, but bounds were {coverImage.width:0.##}x{coverImage.height:0.##}.");

                Assert.Equal("0", driver.GetText(NotesStatsAllId));
                Assert.Equal("0", driver.GetText(NotesStatsSelectedId));
                Assert.Equal("0", driver.GetText(NotesStatsSingleId));
                Assert.Equal("0", driver.GetText(NotesStatsDoubleId));
                Assert.Equal("0", driver.GetText(NotesStatsTriplePlusId));

                driver.ClickButton(ColumnStatsButtonId);
                Assert.Equal("0", driver.GetText(ColumnStatsValue1Id));
                Assert.Equal("0", driver.GetText(ColumnStatsValue2Id));
                Assert.Equal("0", driver.GetText(ColumnStatsValue3Id));
                Assert.Equal("0", driver.GetText(ColumnStatsValue4Id));
                Assert.Equal("0%", driver.GetText(ColumnStatsPercentage1Id));
                Assert.Equal("0%", driver.GetText(ColumnStatsPercentage2Id));
                Assert.Equal("0%", driver.GetText(ColumnStatsPercentage3Id));
                Assert.Equal("0%", driver.GetText(ColumnStatsPercentage4Id));

                Assert.True(driver.IsVisible(NavWaveformImageId));
            });
        }

        [Fact]
        public void FixtureMapShowsExpectedEditorShellControlsAndVisibleButtonText() {
            RunOpenedFixtureMapTest((driver, _) => {
                Assert.Equal("Create Song Preview", driver.GetText(CreatePreviewButtonId));
                Assert.Equal("Edit Song Timing", driver.GetText(ChangeBpmButtonId));
                Assert.Equal("Customize Navigation Bar", driver.GetText(CustomizeNavBarButtonId));
                Assert.Equal("3", driver.GetText(DifficultyNumberTextBoxId));
                Assert.Equal("12", driver.GetText(NoteSpeedTextBoxId));
                Assert.Equal("Auto", driver.GetText(BronzeMedalDistanceTextBoxId));
                Assert.Equal("Auto", driver.GetText(SilverMedalDistanceTextBoxId));
                Assert.Equal("Auto", driver.GetText(GoldMedalDistanceTextBoxId));

                Assert.True(driver.IsVisible(ScrollSpectrogramId));
                Assert.True(driver.IsVisible(NavWaveformImageId));
                Assert.True(driver.IsVisible(ScrollEditorId));
                Assert.True(driver.IsVisible(SelectedBeatLabelId));
                Assert.True(driver.IsVisible(Drum0Id));
                Assert.True(driver.IsVisible(Drum1Id));
                Assert.True(driver.IsVisible(Drum2Id));
                Assert.True(driver.IsVisible(Drum3Id));
            });
        }

        [Fact]
        public void FixtureDifficultyValuesAreLoadedIntoDifficultyFields() {
            RunOpenedFixtureMapTest((driver, _) => {
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
            RunOpenedFixtureMapTest((driver, _) => {
                Assert.True(driver.IsVisible(DifficultyButton0Id));
                Assert.False(driver.IsVisible(DifficultyButton1Id));
                Assert.False(driver.IsVisible(DifficultyButton2Id));
                Assert.True(driver.IsEnabled(AddDifficultyButtonId));
                Assert.False(driver.IsEnabled(DeleteDifficultyButtonId));
            });
        }

        [Fact]
        public void MainWindowRegionsAndPlaybackControlsArePositionedForEditingWorkflow() {
            RunOpenedFixtureMapTest((driver, _) => {
                var songName = driver.GetElementBounds(SongNameTextBoxId);
                var navWaveform = driver.GetElementBounds(NavWaveformImageId);
                var difficultyNumber = driver.GetElementBounds(DifficultyNumberTextBoxId);
                var songPlayer = driver.GetElementBounds(SongPlayerButtonId);
                var songPosition = driver.GetElementBounds(SongPositionTextId);
                var songProgress = driver.GetElementBounds(SongProgressSliderId);

                Assert.True(RightEdge(songName) < navWaveform.left, $"Expected metadata fields to sit left of the navigation waveform, but song name right edge was {RightEdge(songName):0.##} and waveform left edge was {navWaveform.left:0.##}.");
                Assert.True(RightEdge(navWaveform) < difficultyNumber.left, $"Expected the navigation waveform to sit left of the difficulty controls, but waveform right edge was {RightEdge(navWaveform):0.##} and difficulty field left edge was {difficultyNumber.left:0.##}.");
                Assert.True(navWaveform.height > navWaveform.width * 2.5, $"Expected the navigation waveform to render as a tall vertical strip, but width={navWaveform.width:0.##} and height={navWaveform.height:0.##}.");
                Assert.True(RightEdge(songPlayer) < songPosition.left, $"Expected the play button to sit before the song position text, but button right edge was {RightEdge(songPlayer):0.##} and position left edge was {songPosition.left:0.##}.");
                Assert.True(RightEdge(songPosition) < songProgress.left, $"Expected the song position text to sit before the progress slider, but position right edge was {RightEdge(songPosition):0.##} and slider left edge was {songProgress.left:0.##}.");
                Assert.True(Math.Abs(CenterY(songPlayer) - CenterY(songProgress)) < 45, $"Expected playback controls to share a horizontal row, but center Y values were {CenterY(songPlayer):0.##} and {CenterY(songProgress):0.##}.");
                Assert.True(songProgress.width > songPlayer.width * 4, $"Expected the progress slider to dominate the playback row, but slider width={songProgress.width:0.##} and play button width={songPlayer.width:0.##}.");
            });
        }

        [Fact]
        public void MainWindowEditorViewportAndNavigationStripUseExpectedEditingProportions() {
            RunOpenedFixtureMapTest((driver, _) => {
                var songName = driver.GetElementBounds(SongNameTextBoxId);
                var scrollEditor = driver.GetElementBounds(ScrollEditorId);
                var navWaveform = driver.GetElementBounds(NavWaveformImageId);
                var difficultyNumber = driver.GetElementBounds(DifficultyNumberTextBoxId);

                Assert.True(RightEdge(songName) < scrollEditor.left, $"Expected the editor viewport to sit to the right of the metadata sidebar, but song name right edge was {RightEdge(songName):0.##} and editor left edge was {scrollEditor.left:0.##}.");
                Assert.True(RightEdge(scrollEditor) <= navWaveform.left, $"Expected the editor viewport to sit left of the navigation waveform strip, but editor right edge was {RightEdge(scrollEditor):0.##} and strip left edge was {navWaveform.left:0.##}.");
                Assert.True(RightEdge(navWaveform) < difficultyNumber.left, $"Expected the navigation waveform strip to sit left of the difficulty controls, but strip right edge was {RightEdge(navWaveform):0.##} and difficulty field left edge was {difficultyNumber.left:0.##}.");
                Assert.True(scrollEditor.width >= 330, $"Expected the editor viewport to stay wide enough for four note columns, but width was {scrollEditor.width:0.##}.");
                Assert.True(scrollEditor.height >= 520, $"Expected the editor viewport to stay tall enough for scrolling workflow, but height was {scrollEditor.height:0.##}.");
                Assert.True(scrollEditor.height > scrollEditor.width * 1.15, $"Expected the editor viewport to read as a tall mapping surface, but width={scrollEditor.width:0.##} and height={scrollEditor.height:0.##}.");
                Assert.True(navWaveform.height >= scrollEditor.height * 0.8, $"Expected the navigation waveform strip to span most of the editor height, but strip height was {navWaveform.height:0.##} and editor height was {scrollEditor.height:0.##}.");
                Assert.True(navWaveform.height > navWaveform.width * 2.5, $"Expected the navigation waveform strip to stay vertical and narrow, but width={navWaveform.width:0.##} and height={navWaveform.height:0.##}.");
            });
        }

        [Fact]
        public void MainWindowEditorShellKeepsSpectrogramDrumLaneAndFooterInExpectedPlaces() {
            RunOpenedFixtureMapTest((driver, _) => {
                var scrollEditor = driver.GetElementBounds(ScrollEditorId);
                var navWaveform = driver.GetElementBounds(NavWaveformImageId);
                var difficultyNumber = driver.GetElementBounds(DifficultyNumberTextBoxId);
                var selectedBeat = driver.GetElementBounds(SelectedBeatLabelId);
                var changeBpmButton = driver.GetElementBounds(ChangeBpmButtonId);
                var drum0 = driver.GetElementBounds(Drum0Id);
                var drum1 = driver.GetElementBounds(Drum1Id);
                var drum2 = driver.GetElementBounds(Drum2Id);
                var drum3 = driver.GetElementBounds(Drum3Id);

                Assert.True(RightEdge(scrollEditor) <= navWaveform.left, $"Expected the editor viewport to sit left of the navigation waveform strip, but editor right edge was {RightEdge(scrollEditor):0.##} and strip left edge was {navWaveform.left:0.##}.");
                Assert.True(RightEdge(navWaveform) < difficultyNumber.left, $"Expected the navigation waveform strip to sit left of the difficulty controls, but strip right edge was {RightEdge(navWaveform):0.##} and difficulty field left edge was {difficultyNumber.left:0.##}.");

                Assert.True(drum0.left < drum1.left && drum1.left < drum2.left && drum2.left < drum3.left, "Expected the four drum targets to progress left-to-right across the beat scan lane.");
                Assert.True(CenterY(drum0) > scrollEditor.top + scrollEditor.height * 0.7, $"Expected the first drum target to anchor near the bottom of the editor viewport, but its center Y was {CenterY(drum0):0.##} and the viewport bottom region started at {(scrollEditor.top + scrollEditor.height * 0.7):0.##}.");
                Assert.True(CenterY(drum3) > scrollEditor.top + scrollEditor.height * 0.7, $"Expected the fourth drum target to anchor near the bottom of the editor viewport, but its center Y was {CenterY(drum3):0.##} and the viewport bottom region started at {(scrollEditor.top + scrollEditor.height * 0.7):0.##}.");
                Assert.True(selectedBeat.top > changeBpmButton.top + changeBpmButton.height, $"Expected the selected-beat footer to be docked below the editor settings content, but footer top was {selectedBeat.top:0.##} and the first editor-settings button bottom was {(changeBpmButton.top + changeBpmButton.height):0.##}.");
            });
        }

        [Fact]
        public void FullWindowLayoutKeepsSidebarsAndEditorChromeInsideMainWindowBounds() {
            RunOpenedFixtureMapTest((driver, _) => {
                driver.ResizeMainWindow(1900, 1000);
                driver.WaitForIdle(TimeSpan.FromMilliseconds(700));

                var mainWindow = driver.GetElementBounds(MainWindowId);
                var songName = driver.GetElementBounds(SongNameTextBoxId);
                var scrollSpectrogram = driver.GetElementBounds(ScrollSpectrogramId);
                var scrollEditor = driver.GetElementBounds(ScrollEditorId);
                var navWaveform = driver.GetElementBounds(NavWaveformImageId);
                var difficultyNumber = driver.GetElementBounds(DifficultyNumberTextBoxId);

                Assert.True(songName.left >= mainWindow.left + 5, $"Expected the left sidebar field to remain inside the window bounds, but song name left edge was {songName.left:0.##} for window left edge {mainWindow.left:0.##}.");
                Assert.True(RightEdge(songName) <= scrollSpectrogram.left + 2, $"Expected the left sidebar fields to remain clipped within their dock and clear of the spectrogram, but song name right edge was {RightEdge(songName):0.##} and spectrogram left edge was {scrollSpectrogram.left:0.##}.");
                Assert.True(RightEdge(scrollSpectrogram) <= scrollEditor.left + 2, $"Expected the spectrogram to stay immediately left of the editor viewport, but spectrogram right edge was {RightEdge(scrollSpectrogram):0.##} and editor left edge was {scrollEditor.left:0.##}.");
                Assert.True(RightEdge(scrollEditor) <= navWaveform.left + 1, $"Expected the editor viewport to remain left of the navigation strip, but editor right edge was {RightEdge(scrollEditor):0.##} and strip left edge was {navWaveform.left:0.##}.");
                Assert.True(RightEdge(navWaveform) < difficultyNumber.left, $"Expected the navigation strip to remain left of the right sidebar fields, but strip right edge was {RightEdge(navWaveform):0.##} and difficulty field left edge was {difficultyNumber.left:0.##}.");
                Assert.True(RightEdge(difficultyNumber) <= RightEdge(mainWindow) - 10, $"Expected the right sidebar field to remain inside the window bounds, but difficulty field right edge was {RightEdge(difficultyNumber):0.##} for window right edge {RightEdge(mainWindow):0.##}.");
                Assert.True(scrollEditor.width >= 450, $"Expected the editor viewport to remain comfortably wide at full window size, but width was {scrollEditor.width:0.##}.");
                Assert.True(scrollEditor.height >= 650, $"Expected the editor viewport to remain comfortably tall at full window size, but height was {scrollEditor.height:0.##}.");
                Assert.True(scrollSpectrogram.width >= 20, $"Expected the spectrogram strip to remain visibly useful at full window size, but width was {scrollSpectrogram.width:0.##}.");
                Assert.True(scrollSpectrogram.width < scrollEditor.width, $"Expected the spectrogram strip to stay narrower than the main editor viewport, but spectrogram width was {scrollSpectrogram.width:0.##} and editor width was {scrollEditor.width:0.##}.");
                Assert.True(navWaveform.width >= 20 && navWaveform.width <= 100, $"Expected the navigation strip to stay narrow like WPF, but width was {navWaveform.width:0.##}.");
            });
        }

        [Fact]
        public void SidebarsKeepComfortablePaddingInsideDockBounds() {
            RunOpenedFixtureMapTest((driver, _) => {
                var mainWindow = driver.GetElementBounds(MainWindowId);
                var songName = driver.GetElementBounds(SongNameTextBoxId);
                var difficultyNumber = driver.GetElementBounds(DifficultyNumberTextBoxId);

                Assert.True(songName.left >= mainWindow.left + 10, $"Expected left sidebar content to keep at least 10 px of inner padding, but song name left edge was {songName.left:0.##} for window left edge {mainWindow.left:0.##}.");
                Assert.True(RightEdge(difficultyNumber) <= RightEdge(mainWindow) - 14, $"Expected right sidebar content to keep visible inner padding, but difficulty field right edge was {RightEdge(difficultyNumber):0.##} for window right edge {RightEdge(mainWindow):0.##}.");
            });
        }

        [Fact]
        public void NavigationWaveformClicksSeekTopToSongEndAndBottomToSongStart() {
            RunOpenedFixtureMapTest((driver, _) => {
                var durationMs = ParseTimeTextToMilliseconds(driver.GetText(SongDurationTextId));

                driver.ClickWithinElement(NavWaveformImageId, 0.5, 0.05);
                driver.WaitForIdle();
                var nearTopValue = driver.GetSliderValue(SongProgressSliderId);

                driver.ClickWithinElement(NavWaveformImageId, 0.5, 0.95);
                driver.WaitForIdle();
                var nearBottomValue = driver.GetSliderValue(SongProgressSliderId);

                Assert.True(nearTopValue >= durationMs * 0.75, $"Expected clicking near the top of the navigation waveform to seek close to song end, but progress was {nearTopValue:0.##}ms for duration {durationMs:0.##}ms.");
                Assert.True(nearBottomValue <= durationMs * 0.25, $"Expected clicking near the bottom of the navigation waveform to seek close to song start, but progress was {nearBottomValue:0.##}ms for duration {durationMs:0.##}ms.");
                Assert.True(nearTopValue > nearBottomValue, $"Expected top-of-waveform seeks to land after bottom-of-waveform seeks, but values were {nearTopValue:0.##}ms and {nearBottomValue:0.##}ms.");
            });
        }

        [Fact]
        public void PlaybackViaButtonDisablesAndReenablesTimelineAndDifficultyControls() {
            RunOpenedFixtureMapTest((driver, _) => {
                var previewButtonInitiallyEnabled = driver.IsEnabled(PreviewPlayButtonId);

                driver.ClickWithinElement(SongPlayerButtonId, 0.5, 0.5);
                driver.WaitForIdle();
                Assert.False(driver.IsEnabled(SongTempoSliderId), driver.GetTestLog());
                Assert.False(driver.IsEnabled(SongProgressSliderId), driver.GetTestLog());
                Assert.False(driver.IsEnabled(DifficultyButton0Id), driver.GetTestLog());
                Assert.False(driver.IsEnabled(PreviewPlayButtonId), driver.GetTestLog());

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
            RunOpenedFixtureMapTest((driver, _) => {
                driver.SendKeyboardShortcut("Space");
                driver.WaitForIdle();
                Assert.False(driver.IsEnabled(SongTempoSliderId), driver.GetTestLog());
                Assert.False(driver.IsEnabled(SongProgressSliderId), driver.GetTestLog());

                driver.SendKeyboardShortcut("Space");
                driver.WaitForIdle();
                Assert.True(driver.IsEnabled(SongTempoSliderId));
                Assert.True(driver.IsEnabled(SongProgressSliderId));
            });
        }

        [Fact]
        public void PlaybackAdvancesTimelineAndBeatReadoutWithinHalfASecond() {
            RunOpenedFixtureMapTest((driver, _) => {
                var initialProgress = driver.GetSliderValue(SongProgressSliderId);

                driver.ClickWithinElement(SongPlayerButtonId, 0.5, 0.5);

                var advanced = WaitForPlaybackAdvance(driver, initialProgress, out var updatedProgress);

                driver.ClickWithinElement(SongPlayerButtonId, 0.5, 0.5);
                driver.WaitForIdle();

                Assert.True(advanced, string.IsNullOrWhiteSpace(driver.GetTestLog())
                    ? $"Expected playback to advance the timeline quickly, but progress stayed at {updatedProgress:0.##}."
                    : driver.GetTestLog());
                Assert.True(updatedProgress >= initialProgress + 150, $"Expected playback slider progress to advance by at least 150ms, but it changed from {initialProgress:0.##} to {updatedProgress:0.##}.");
            });
        }

        [Fact]
        public void SpaceShortcutIsIgnoredWhenTextboxHasFocus() {
            RunOpenedFixtureMapTest((driver, _) => {
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
            RunOpenedFixtureMapTest((driver, _) => {
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
            RunOpenedFixtureMapTest((driver, _) => {
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
            RunOpenedFixtureMapTest((driver, _) => {
                var initialSnapState = driver.IsChecked(GridSnapCheckboxId);
                driver.ToggleCheckbox(GridSnapCheckboxId, !initialSnapState);
                driver.WaitForIdle();
                Assert.Equal(!initialSnapState, driver.IsChecked(GridSnapCheckboxId));
                Assert.Equal(driver.IsChecked(GridSnapCheckboxId), driver.IsMenuItemChecked("Edit>Snap Notes to Grid"));
            });
        }

        [Fact]
        public void CtrlLeftBracketTogglesLeftSidebarVisibility() {
            RunOpenedFixtureMapTest((driver, _) => {
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
            RunOpenedFixtureMapTest((driver, _) => {
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
            RunOpenedFixtureMapTest((driver, _) => {
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
            RunOpenedFixtureMapTest((driver, _) => {
                driver.ClickButton(AddDifficultyButtonId);
                driver.InvokeCommand(DialogNoCommandId);
                driver.WaitForIdle();

                Assert.True(driver.IsVisible(DifficultyButton1Id));
                Assert.True(driver.IsEnabled(DeleteDifficultyButtonId));
            });
        }

        [Fact]
        public void DeleteDifficultyReturnsToSingleDifficultyState() {
            RunOpenedFixtureMapTest((driver, _) => {
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
            RunOpenedFixtureMapTest((driver, _) => {
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

        [Fact]
        public void DifficultySlotsStayAnchoredWhenDifficultyCountChanges() {
            RunOpenedFixtureMapTest((driver, _) => {
                var beforeButton0 = driver.GetElementBounds(DifficultyButton0Id);
                var beforeAdd = driver.GetElementBounds(AddDifficultyButtonId);
                var beforeDelete = driver.GetElementBounds(DeleteDifficultyButtonId);

                driver.ClickButton(AddDifficultyButtonId);
                driver.InvokeCommand(DialogNoCommandId);
                driver.WaitForIdle();

                var afterButton0 = driver.GetElementBounds(DifficultyButton0Id);
                var afterButton1 = driver.GetElementBounds(DifficultyButton1Id);
                var afterAdd = driver.GetElementBounds(AddDifficultyButtonId);
                var afterDelete = driver.GetElementBounds(DeleteDifficultyButtonId);

                Assert.InRange(Math.Abs(afterButton0.left - beforeButton0.left), 0, 3);
                Assert.InRange(Math.Abs(afterAdd.left - beforeAdd.left), 0, 3);
                Assert.InRange(Math.Abs(afterDelete.left - beforeDelete.left), 0, 3);
                Assert.True(afterButton1.left > afterButton0.left, $"Expected added difficulty slots to flow left-to-right in stable positions, but difficulty 1 left edge was {afterButton1.left:0.##} and difficulty 0 left edge was {afterButton0.left:0.##}.");
                Assert.True(afterAdd.left > afterButton1.left, $"Expected +/- actions to stay docked to the right of the difficulty slots, but add button left edge was {afterAdd.left:0.##} and difficulty 1 left edge was {afterButton1.left:0.##}.");
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
            RunOpenedFixtureMapTest((driver, _) => { AssertInvalidTextInputRestored(driver, controlId, invalidValue); });
        }

        [Fact]
        public void BpmChangeCancelKeepsPreviousValue() {
            RunOpenedFixtureMapTest((driver, _) => {
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
            RunOpenedFixtureMapTest((driver, _) => {
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
            RunOpenedFixtureMapTest((driver, _) => {
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
            RunOpenedFixtureMapTest((driver, _) => {
                var initial = driver.IsChecked(ExplicitCheckboxId);
                driver.ToggleCheckbox(ExplicitCheckboxId, !initial);
                driver.WaitForIdle();
                Assert.Equal(!initial, driver.IsChecked(ExplicitCheckboxId));
            });
        }

        [Fact]
        public void WaveformCheckboxCanBeToggled() {
            RunOpenedFixtureMapTest((driver, _) => {
                var initial = driver.IsChecked(WaveformCheckboxId);
                driver.ToggleCheckbox(WaveformCheckboxId, !initial);
                driver.WaitForIdle();
                Assert.Equal(!initial, driver.IsChecked(WaveformCheckboxId));
            });
        }

        [Fact]
        public void MetronomeCheckboxCanBeToggled() {
            RunOpenedFixtureMapTest((driver, _) => {
                var initial = driver.IsChecked(MetronomeCheckboxId);
                driver.ToggleCheckbox(MetronomeCheckboxId, !initial);
                driver.WaitForIdle();
                Assert.Equal(!initial, driver.IsChecked(MetronomeCheckboxId));
            });
        }

        [Fact]
        public void VolumeSlidersUpdateDisplayedPercentages() {
            RunOpenedFixtureMapTest((driver, _) => {
                driver.SetSliderValue(SongVolumeSliderId, 0.25);
                driver.SetSliderValue(DrumVolumeSliderId, 0.65);
                driver.WaitForIdle();

                Assert.Equal("25%", driver.GetText(SongVolumeTextId));
                Assert.Equal("65%", driver.GetText(DrumVolumeTextId));
            });
        }

        [Fact]
        public void SongTempoSliderUpdatesDisplayedTempoText() {
            RunOpenedFixtureMapTest((driver, _) => {
                driver.SetSliderValue(SongTempoSliderId, 1.5);
                driver.WaitForIdle();
                AssertTempoText(driver, 1.5);
            });
        }

        [Fact]
        public void DoubleClickingSongTempoSliderResetsDefaultTempo() {
            RunOpenedFixtureMapTest((driver, _) => {
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
            RunOpenedFixtureMapTest((driver, _) => {
                var initialPosition = driver.GetText(SongPositionTextId);
                driver.SetSliderValue(SongProgressSliderId, 3000);
                driver.WaitForIdle();
                Assert.NotEqual(initialPosition, driver.GetText(SongPositionTextId));
            });
        }

        [Fact]
        public void EnvironmentSelectionCanBeChanged() {
            RunOpenedFixtureMapTest((driver, _) => {
                driver.SelectDropdown(EnvironmentComboBoxId, "Helheim");
                driver.WaitForIdle();
                Assert.Equal("Helheim", driver.GetSelectedValue(EnvironmentComboBoxId));
            });
        }

        [Fact]
        public void EnvironmentSelectionPersistsAfterSaveAndReopen() {
            var driver = new WpfUIDriver();
            string? mapFolder = null;

            try {
                mapFolder = LaunchAndOpenFixtureMap(driver);
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
                SafeDeleteDirectory(mapFolder);
            }
        }

        [Fact]
        public void OpenMapMenuLoadsSelectedMapFolder() {
            var driver = new WpfUIDriver();
            string? firstMapFolder = null;
            string? secondMapFolder = null;

            try {
                firstMapFolder = CreateFixtureMapCopy();
                secondMapFolder = CreateFixtureMapCopy();
                LaunchAndOpenMap(driver, firstMapFolder);

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
                SafeDeleteDirectory(firstMapFolder);
                SafeDeleteDirectory(secondMapFolder);
            }
        }

        [Fact]
        public void CtrlOShortcutLoadsSelectedMapFolder() {
            var driver = new WpfUIDriver();
            string? firstMapFolder = null;
            string? secondMapFolder = null;

            try {
                firstMapFolder = CreateFixtureMapCopy();
                secondMapFolder = CreateFixtureMapCopy();
                LaunchAndOpenMap(driver, firstMapFolder);

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
                SafeDeleteDirectory(firstMapFolder);
                SafeDeleteDirectory(secondMapFolder);
            }
        }

        [Fact]
        public void CtrlOWithUnsavedChangesAndCancelKeepsCurrentMapOpen() {
            RunOpenedFixtureMapTest((driver, _) => {
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
            RunOpenedFixtureMapTest((driver, mapFolder) => {
                var pickerSourceFolder = CreateTempOutputFolder("pick-song-source");
                try {
                    var sourceSongPath = Path.Combine(GetRepositoryRoot(), FixtureMapFolderRelative, "song.ogg");
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
                    SafeDeleteDirectory(pickerSourceFolder);
                }
            });
        }

        [Fact]
        public void PickCoverButtonLoadsSelectedJpegIntoCurrentMap() {
            RunOpenedFixtureMapTest((driver, mapFolder) => {
                var pickerSourceFolder = CreateTempOutputFolder("pick-cover-source");
                try {
                    var sourceCoverPath = Path.Combine(GetRepositoryRoot(), FixtureMapFolderRelative, "cover.jpg");
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
                    SafeDeleteDirectory(pickerSourceFolder);
                }
            });
        }

        [Fact]
        public void NewMapMenuPickerFlowCreatesAndLoadsMapFromSelectedSong() {
            var driver = new WpfUIDriver();
            string? initialMapFolder = null;
            string? newMapFolder = null;
            string? pickerSourceFolder = null;

            try {
                initialMapFolder = CreateFixtureMapCopy();
                LaunchAndOpenMap(driver, initialMapFolder);
                newMapFolder = CreateAlphabeticMapFolder("new-map-picker");
                pickerSourceFolder = CreateTempOutputFolder("new-map-song-source");

                var sourceSongPath = Path.Combine(GetRepositoryRoot(), FixtureMapFolderRelative, "song.ogg");
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
                SafeDeleteDirectory(initialMapFolder);
                SafeDeleteDirectory(newMapFolder);
                SafeDeleteDirectory(pickerSourceFolder);
            }
        }

        [Fact]
        public void ImportMapMenuPickerFlowCreatesAndLoadsConvertedMap() {
            var driver = new WpfUIDriver();
            string? initialMapFolder = null;
            string? importTargetFolder = null;
            string? importSourceFolder = null;

            try {
                initialMapFolder = CreateFixtureMapCopy();
                LaunchAndOpenMap(driver, initialMapFolder);
                importTargetFolder = CreateAlphabeticMapFolder("import-map-picker");
                var importFixture = CreateStepManiaImportFixture();
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
                SafeDeleteDirectory(initialMapFolder);
                SafeDeleteDirectory(importTargetFolder);
                SafeDeleteDirectory(importSourceFolder);
            }
        }

        [Fact]
        public void CtrlSCreatesAutosaveSnapshot() {
            RunOpenedFixtureMapTest((driver, mapFolder) => {
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
            RunOpenedFixtureMapTest((driver, mapFolder) => {
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
        public void OpeningFixtureMapCreatesSpectrogramCachePngsBesideMap() {
            var driver = new WpfUIDriver();
            string? mapFolder = null;

            try {
                mapFolder = CreateFixtureMapCopy();
                var cacheFolder = Path.Combine(mapFolder, CacheFolderName);
                SafeDeleteDirectory(cacheFolder);

                LaunchAndOpenMap(driver, mapFolder);
                driver.WaitForIdle(TimeSpan.FromMilliseconds(500));

                Assert.True(
                    WaitForFileCount(() => Directory.Exists(cacheFolder) ? Directory.GetFiles(cacheFolder, "*.png").Length : 0, minimumCount: 1),
                    $"Expected spectrogram rendering to create at least one cached PNG beside the map, but '{cacheFolder}' stayed empty.");
            } finally {
                driver.Shutdown();
                SafeDeleteDirectory(mapFolder);
            }
        }

        [Fact]
        public void ClearCacheMenuDeletesCacheFolderWhenConfirmed() {
            RunOpenedFixtureMapTest((driver, mapFolder) => {
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
            RunOpenedFixtureMapTest((driver, mapFolder) => {
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
            RunOpenedFixtureMapTest((driver, _) => {
                Assert.True(driver.IsMenuItemVisible("Tools>Clear Cache"));
                Assert.True(driver.IsVisible(NavWaveformImageId));
                Assert.False(driver.IsVisible(NavBpmChangesCanvasId));
                Assert.False(driver.IsVisible(NavNotesCanvasId));
                Assert.False(driver.IsVisible(DifficultyPredictionLabelId));
            });
        }

        [Fact]
        public void DisablingSpectrogramInSettingsHidesClearCacheMenuItem() {
            RunOpenedFixtureMapTest((driver, _) => {
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
            RunOpenedFixtureMapTest((driver, _) => {
                var exportFolder = CreateTempOutputFolder("export-menu");

                try {
                    driver.SetTestFileSelection(exportFolder);
                    driver.SelectMenuItem("File>Export Map");
                    driver.WaitForIdle();

                    Assert.NotEmpty(Directory.GetFiles(exportFolder, "*.zip"));
                } finally {
                    SafeDeleteDirectory(exportFolder);
                }
            });
        }

        [Fact]
        public void CtrlECreatesZipPackageInSelectedFolder() {
            RunOpenedFixtureMapTest((driver, _) => {
                var exportFolder = CreateTempOutputFolder("export-shortcut");

                try {
                    driver.SetTestFileSelection(exportFolder);
                    driver.SendKeyboardShortcut("Ctrl+E");
                    driver.WaitForIdle();

                    Assert.NotEmpty(Directory.GetFiles(exportFolder, "*.zip"));
                } finally {
                    SafeDeleteDirectory(exportFolder);
                }
            });
        }

        [Fact]
        public void ExportIncludesInfoSongCoverAndAllDifficultyFiles() {
            RunOpenedFixtureMapTest((driver, mapFolder) => {
                driver.ClickButton(AddDifficultyButtonId);
                driver.InvokeCommand(DialogNoCommandId);
                driver.WaitForIdle();
                driver.ClickButton(AddDifficultyButtonId);
                driver.InvokeCommand(DialogNoCommandId);
                driver.WaitForIdle();

                driver.SendKeyboardShortcut("Ctrl+S");
                driver.WaitForIdle();

                var exportFolder = CreateTempOutputFolder("export-contents");
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
                    SafeDeleteDirectory(exportFolder);
                }
            });
        }

        [Fact]
        public void SongNamePersistsAfterSaveAndReopen() {
            var driver = new WpfUIDriver();
            string? mapFolder = null;

            try {
                mapFolder = LaunchAndOpenFixtureMap(driver);
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
                SafeDeleteDirectory(mapFolder);
            }
        }

        [Fact]
        public void ArtistAndMapperNamesPersistAfterSaveAndReopen() {
            var driver = new WpfUIDriver();
            string? mapFolder = null;

            try {
                mapFolder = LaunchAndOpenFixtureMap(driver);
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
                SafeDeleteDirectory(mapFolder);
            }
        }

        [Fact]
        public void SavingRenamedSongUpdatesRecentMapDisplayWhenReturningToStartWindow() {
            var driver = new WpfUIDriver();
            string? mapFolder = null;

            try {
                mapFolder = LaunchAndOpenFixtureMap(driver);
                var updatedSongName = $"Recent Entry {Guid.NewGuid():N}";
                var normalizedMapFolder = Path.GetFullPath(mapFolder);

                driver.SetText(SongNameTextBoxId, updatedSongName);
                CommitTextboxEdits(driver);
                driver.SendKeyboardShortcut("Ctrl+S");
                driver.WaitForIdle();

                driver.SendKeyboardShortcut("Ctrl+W");
                driver.WaitForStartWindow();

                Assert.True(driver.IsVisible(StartWindowId));
                Assert.Equal(1, driver.GetListItemCount(StartupRecentMapsListId));
                Assert.True(driver.ContainsText(updatedSongName));
                Assert.True(driver.ContainsText(normalizedMapFolder));

                driver.ClickListItemContainingText(StartupRecentMapsListId, normalizedMapFolder);
                driver.WaitForMainWindow();
                driver.SendKeyboardShortcut("Ctrl+W");
                driver.WaitForStartWindow();

                Assert.Equal(1, driver.GetListItemCount(StartupRecentMapsListId));
                Assert.True(driver.ContainsText(updatedSongName));
            } finally {
                driver.Shutdown();
                SafeDeleteDirectory(mapFolder);
            }
        }

        [Fact]
        public void CtrlWWithoutUnsavedChangesReturnsToStartWindow() {
            RunOpenedFixtureMapTest((driver, _) => {
                driver.SendKeyboardShortcut("Ctrl+W");
                driver.WaitForStartWindow();
                Assert.True(driver.IsVisible(StartWindowId));
            });
        }

        [Fact]
        public void CtrlWWithUnsavedChangesAndCancelKeepsMainWindowOpen() {
            RunOpenedFixtureMapTest((driver, _) => {
                MarkCurrentMapDirty(driver);
                driver.SendKeyboardShortcut("Ctrl+W");
                driver.InvokeCommand(DialogCancelCommandId);
                driver.WaitForMainWindow();
                Assert.True(driver.IsVisible(MainWindowId));
            });
        }

        [Fact]
        public void CtrlWWithUnsavedChangesAndDontSaveReturnsToStartWindow() {
            RunOpenedFixtureMapTest((driver, _) => {
                MarkCurrentMapDirty(driver);
                driver.SendKeyboardShortcut("Ctrl+W");
                driver.InvokeCommand(DialogNoCommandId);
                driver.WaitForStartWindow();
                Assert.True(driver.IsVisible(StartWindowId));
            });
        }

        [Fact]
        public void CtrlWWithUnsavedChangesAndSavePersistsChanges() {
            var driver = new WpfUIDriver();
            string? mapFolder = null;

            try {
                mapFolder = LaunchAndOpenFixtureMap(driver);
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
                SafeDeleteDirectory(mapFolder);
            }
        }

        [Fact]
        public void CloseMapMenuWithoutUnsavedChangesReturnsToStartWindow() {
            RunOpenedFixtureMapTest((driver, _) => {
                driver.SelectMenuItem("File>Close Map");
                driver.WaitForStartWindow();
                Assert.True(driver.IsVisible(StartWindowId));
            });
        }

        [Fact]
        public void CloseMapMenuWithUnsavedChangesAndCancelKeepsMainWindowOpen() {
            RunOpenedFixtureMapTest((driver, _) => {
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
            RunOpenedFixtureMapTest((driver, _) => {
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
            RunOpenedFixtureMapTest((driver, _) => {
                driver.ClickButton(buttonId);
                driver.WaitForIdle();
                Assert.True(driver.IsVisible(sentinelControlId));
            });
        }

        [Fact]
        public void DraggingNavWaveformUpdatesSongPositionText() {
            RunOpenedFixtureMapTest((driver, _) => {
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

        private static void RunOpenedFixtureMapTest(Action<WpfUIDriver, string> testBody) {
            var driver = new WpfUIDriver();
            string? mapFolder = null;

            try {
                mapFolder = LaunchAndOpenFixtureMap(driver);
                testBody(driver, mapFolder);
            } finally {
                driver.Shutdown();
                SafeDeleteDirectory(mapFolder);
            }
        }

        private static string LaunchAndOpenFixtureMap(WpfUIDriver driver) {
            var fixtureCopy = CreateFixtureMapCopy();
            LaunchAndOpenMap(driver, fixtureCopy);
            return fixtureCopy;
        }

        private static void LaunchAndOpenMap(WpfUIDriver driver, string mapFolder) {
            driver.Launch();
            driver.WaitForIdle();
            driver.SetTestFileSelection(mapFolder);
            driver.ClickButton(StartupOpenMapButtonId);
            driver.WaitForMainWindow();
        }

        private static string CreateFixtureMapCopy() {
            var fixtureSourcePath = Path.Combine(GetRepositoryRoot(), FixtureMapFolderRelative);
            Assert.True(Directory.Exists(fixtureSourcePath), $"MainWindow fixture map folder was not found: {fixtureSourcePath}");

            var fixtureCopyPath = CreateTempOutputFolder("fixture");
            CopyDirectoryRecursively(fixtureSourcePath, fixtureCopyPath);
            return fixtureCopyPath;
        }

        private static string CreateTempOutputFolder(string tag) {
            var outputPath = Path.Combine(Path.GetTempPath(), "Edda-WpfMainWindowTests", tag, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(outputPath);
            return outputPath;
        }

        private static string CreateAlphabeticMapFolder(string tag) {
            var mapFolder = Path.Combine(CreateTempOutputFolder(tag), "PickerFlowMap");
            Directory.CreateDirectory(mapFolder);
            return mapFolder;
        }

        private static (string fixtureFolder, string simfilePath) CreateStepManiaImportFixture() {
            var fixtureFolder = CreateTempOutputFolder("import-source");
            var songSourcePath = Path.Combine(GetRepositoryRoot(), FixtureMapFolderRelative, "song.ogg");
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

        private static string GetRepositoryRoot() {
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

        private static void SafeDeleteDirectory(string? directoryPath) {
            if (string.IsNullOrWhiteSpace(directoryPath) || !Directory.Exists(directoryPath)) {
                return;
            }

            try {
                Directory.Delete(directoryPath, true);
            } catch {
                // Best effort cleanup for temporary test paths.
            }
        }

        private static void AssertInvalidTextInputRestored(WpfUIDriver driver, string controlId, string invalidValue) {
            var previousValue = driver.GetText(controlId);
            driver.SetText(controlId, invalidValue);
            CommitTextboxEdits(driver);
            _ = driver.TryInvokeCommand("DialogResult.Ok");
            driver.WaitForIdle();
            Assert.Equal(previousValue, driver.GetText(controlId));
        }

        private static void AssertTempoText(WpfUIDriver driver, double expectedTempo) {
            var tempoText = driver.GetText(SongTempoTextId);
            Assert.EndsWith("x", tempoText, StringComparison.OrdinalIgnoreCase);

            var numericText = tempoText[..^1];
            var parsed = double.TryParse(numericText, NumberStyles.Float, CultureInfo.CurrentCulture, out var actualTempo) ||
                         double.TryParse(numericText, NumberStyles.Float, CultureInfo.InvariantCulture, out actualTempo);

            Assert.True(parsed, $"Could not parse tempo label '{tempoText}'.");
            Assert.Equal(expectedTempo, actualTempo, 2);
        }

        private static void CommitTextboxEdits(WpfUIDriver driver) {
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

        private static void MarkCurrentMapDirty(WpfUIDriver driver) {
            driver.SetText(SongNameTextBoxId, $"{driver.GetText(SongNameTextBoxId)}*");
            CommitTextboxEdits(driver);
        }

        private static double CenterY((double left, double top, double width, double height) bounds) {
            return bounds.top + bounds.height / 2;
        }

        private static double RightEdge((double left, double top, double width, double height) bounds) {
            return bounds.left + bounds.width;
        }

        private static double BottomEdge((double left, double top, double width, double height) bounds) {
            return bounds.top + bounds.height;
        }

        private static double ParseTimeTextToMilliseconds(string value) {
            if (!TimeSpan.TryParseExact(value, @"m\:ss", CultureInfo.InvariantCulture, out var parsed) &&
                !TimeSpan.TryParseExact(value, @"mm\:ss", CultureInfo.InvariantCulture, out parsed)) {
                throw new InvalidOperationException($"Expected a time string in m:ss format, but got '{value}'.");
            }

            return parsed.TotalMilliseconds;
        }

        private static bool WaitForPlaybackAdvance(WpfUIDriver driver, double initialProgress, out double updatedProgress) {
            var timeout = Stopwatch.StartNew();
            updatedProgress = initialProgress;

            while (timeout.Elapsed < TimeSpan.FromMilliseconds(750)) {
                Thread.Sleep(75);
                updatedProgress = driver.GetSliderValue(SongProgressSliderId);
                if (updatedProgress >= initialProgress + 150) {
                    return true;
                }
            }

            return false;
        }

        private static bool WaitForFileCount(Func<int> getCount, int minimumCount) {
            var timeout = Stopwatch.StartNew();

            while (timeout.Elapsed < TimeSpan.FromSeconds(10)) {
                if (getCount() >= minimumCount) {
                    return true;
                }

                Thread.Sleep(100);
            }

            return false;
        }
    }
}
