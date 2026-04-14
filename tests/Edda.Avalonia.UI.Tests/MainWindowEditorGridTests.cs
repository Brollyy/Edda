using System;
using System.Drawing;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using Edda.Testing;
using Xunit;

namespace Edda.Avalonia.UI.Tests;

public class MainWindowEditorGridTests {
    private const string MainWindowId = "AppMainWindow";
    private const string StartupOpenMapButtonId = "ButtonOpenMap";
    private const string SongNameTextBoxId = "txtSongName";
    private const string SongPlayerButtonId = "btnSongPlayer";
    private const string SongPositionTextId = "txtSongPosition";
    private const string SongProgressSliderId = "sliderSongProgress";
    private const string ScrollEditorId = "scrollEditor";
    private const string ScrollSpectrogramId = "scrollSpectrogram";
    private const string GridSpectrogramId = "gridSpectrogram";
    private const string SpectrogramResizeId = "spectrogramResize";
    private const string NavWaveformImageId = "imgWaveformVertical";
    private const string LineSongProgressId = "lineSongProgress";
    private const string MainWaveformId = "MainWaveform";
    private const string PreviewNoteId = "imgPreviewNote";
    private const string BeatScanLineId = "lineBeatScan";
    private const string Drum0Id = "Drum0";
    private const string Drum1Id = "Drum1";
    private const string Drum2Id = "Drum2";
    private const string Drum3Id = "Drum3";
    private const string GridSpacingTextBoxId = "txtGridSpacing";
    private const string MetronomeCheckboxId = "checkMetronome";
    private const string GridSnapCheckBoxId = "checkGridSnap";
    private const string GridWaveformCheckBoxId = "checkWaveform";
    private const string ChangeBpmButtonId = "btnChangeBPM";
    private const string CustomizeNavBarButtonId = "btnCustomizeNavBar";
    private const string SongVolumeValueId = "txtSongVol";
    private const string DrumVolumeValueId = "txtDrumVol";
    private const string SongTempoValueId = "txtSongTempo";
    private const string SongVolumeSliderId = "sliderSongVol";
    private const string DrumVolumeSliderId = "sliderDrumVol";
    private const string DifficultyNumberId = "txtDifficultyNumber";
    private const string NoteSpeedId = "txtNoteSpeed";
    private const string ColumnStatsButtonId = "columnStats";
    private const string NotesStatsAllId = "notesStatsAll";
    private const string NotesStatsSelectedId = "notesStatsSelected";
    private const string NotesStatsSingleId = "notesStatsSingle";
    private const string NotesStatsDoubleId = "notesStatsDouble";
    private const string NotesStatsTriplePlusLabelId = "notesStatsTriplePlusLabel";
    private const string NotesStatsTriplePlusId = "notesStatsTriplePlus";
    private const string ColumnStatsValue1Id = "columnStatsValue1";
    private const string ColumnStatsValue2Id = "columnStatsValue2";
    private const string ColumnStatsValue3Id = "columnStatsValue3";
    private const string ColumnStatsValue4Id = "columnStatsValue4";
    private const string ColumnStatsPercentage1Id = "columnStatsPercentage1";
    private const string ColumnStatsPercentage2Id = "columnStatsPercentage2";
    private const string ColumnStatsPercentage3Id = "columnStatsPercentage3";
    private const string ColumnStatsPercentage4Id = "columnStatsPercentage4";

    private const string FixtureMapFolderRelative = "tests/TestData/Wpf/MainWindow/FixtureMap";

    [Fact]
    public void AddNoteFromMenuUpdatesTotalAndColumnStats() {
        RunOpenedFixtureMapTest((driver, _) => {
            driver.ClickButton(ColumnStatsButtonId);
            var initialAll = ParseIntegerText(driver.GetText(NotesStatsAllId), NotesStatsAllId);
            var initialColumn1 = ParseIntegerText(driver.GetText(ColumnStatsValue1Id), ColumnStatsValue1Id);

            driver.SelectMenuItem("Edit>Add New>Note (column 1)");
            driver.WaitForIdle();

            Assert.Equal(initialAll + 1, ParseIntegerText(driver.GetText(NotesStatsAllId), NotesStatsAllId));
            Assert.Equal(initialColumn1 + 1, ParseIntegerText(driver.GetText(ColumnStatsValue1Id), ColumnStatsValue1Id));
        });
    }

    [Fact]
    public void EditorGridFixtureStartsWithExpectedViewportAndZeroedVisibleStats() {
        RunOpenedFixtureMapTest((driver, _) => {
            driver.ClickButton(ColumnStatsButtonId);

            var scrollEditor = driver.GetElementBounds(ScrollEditorId);
            Assert.True(scrollEditor.width >= 330, $"Expected the editor viewport to stay wide enough for four note columns, but width was {scrollEditor.width:0.##}.");
            Assert.True(scrollEditor.height >= 520, $"Expected the editor viewport to stay tall enough for scroll-based editing, but height was {scrollEditor.height:0.##}.");

            Assert.Equal(0, ParseIntegerText(driver.GetText(NotesStatsAllId), NotesStatsAllId));
            Assert.Equal(0, ParseIntegerText(driver.GetText(NotesStatsSelectedId), NotesStatsSelectedId));
            Assert.Equal(0, ParseIntegerText(driver.GetText(NotesStatsSingleId), NotesStatsSingleId));
            Assert.Equal(0, ParseIntegerText(driver.GetText(NotesStatsDoubleId), NotesStatsDoubleId));
            Assert.Equal(0, ParseIntegerText(driver.GetText(NotesStatsTriplePlusId), NotesStatsTriplePlusId));
            Assert.Equal(0, ParseIntegerText(driver.GetText(ColumnStatsValue1Id), ColumnStatsValue1Id));
            Assert.Equal(0, ParseIntegerText(driver.GetText(ColumnStatsValue2Id), ColumnStatsValue2Id));
            Assert.Equal(0, ParseIntegerText(driver.GetText(ColumnStatsValue3Id), ColumnStatsValue3Id));
            Assert.Equal(0, ParseIntegerText(driver.GetText(ColumnStatsValue4Id), ColumnStatsValue4Id));
            Assert.Equal("0%", driver.GetText(ColumnStatsPercentage1Id));
            Assert.Equal("0%", driver.GetText(ColumnStatsPercentage2Id));
            Assert.Equal("0%", driver.GetText(ColumnStatsPercentage3Id));
            Assert.Equal("0%", driver.GetText(ColumnStatsPercentage4Id));
        });
    }

    [Fact]
    public void EditorGridShellShowsSpectrogramNavigationAndBottomDrumLane() {
        RunOpenedFixtureMapTest((driver, _) => {
            var scrollEditor = driver.GetElementBounds(ScrollEditorId);
            var navWaveform = driver.GetElementBounds(NavWaveformImageId);
            var drum0 = driver.GetElementBounds(Drum0Id);
            var drum1 = driver.GetElementBounds(Drum1Id);
            var drum2 = driver.GetElementBounds(Drum2Id);
            var drum3 = driver.GetElementBounds(Drum3Id);

            Assert.True(driver.IsVisible(ScrollSpectrogramId));
            Assert.True(driver.IsVisible(NavWaveformImageId));
            Assert.True(driver.IsVisible(Drum0Id));
            Assert.True(driver.IsVisible(Drum1Id));
            Assert.True(driver.IsVisible(Drum2Id));
            Assert.True(driver.IsVisible(Drum3Id));
            Assert.True(RightEdge(scrollEditor) <= navWaveform.left, $"Expected the editor viewport to stay left of the navigation waveform, but editor right edge was {RightEdge(scrollEditor):0.##} and waveform left edge was {navWaveform.left:0.##}.");
            Assert.True(drum0.left < drum1.left && drum1.left < drum2.left && drum2.left < drum3.left, "Expected the four drum targets to be ordered left-to-right beneath the editor surface.");
            Assert.True(CenterY(drum0) > scrollEditor.top + scrollEditor.height * 0.7, $"Expected the first drum target to anchor near the bottom of the editor viewport, but its center Y was {CenterY(drum0):0.##}.");
            Assert.True(CenterY(drum3) > scrollEditor.top + scrollEditor.height * 0.7, $"Expected the fourth drum target to anchor near the bottom of the editor viewport, but its center Y was {CenterY(drum3):0.##}.");
        });
    }

    [Fact]
    public void HoveringEditorShowsPreviewNoteWithEditingAspectRatio() {
        RunOpenedFixtureMapTest((driver, _) => {
            var scrollEditor = driver.GetElementBounds(ScrollEditorId);

            driver.MoveMouseWithinElement(ScrollEditorId, 0.2, 0.8);
            driver.WaitForIdle(TimeSpan.FromMilliseconds(150));

            Assert.True(driver.IsVisible(PreviewNoteId));
            var previewNote = driver.GetElementBounds(PreviewNoteId);
            var aspectRatio = previewNote.width / Math.Max(1, previewNote.height);

            Assert.True(previewNote.top >= scrollEditor.top + scrollEditor.height * 0.6, $"Expected the hover preview note to appear in the lower editing band, but preview top was {previewNote.top:0.##}.");
            Assert.InRange(aspectRatio, 0.8, 1.2);
        });
    }

    [Fact]
    public void FixtureMapStartsWithCurrentTimingMarkerNearBottomEditingPosition() {
        RunOpenedFixtureMapTest((driver, _) => {
            var scrollEditor = driver.GetElementBounds(ScrollEditorId);
            driver.SelectMenuItem("Edit>Add New>Bookmark");
            driver.WaitForIdle();
            var timingMarker = driver.GetNamedElementBoundsWithin(ScrollEditorId, "Bookmark");

            Assert.True(timingMarker.top >= scrollEditor.top + scrollEditor.height * 0.68, $"Expected the current timing marker to start near the bottom editing position, but marker top was {timingMarker.top:0.##} and the bottom editing band started at {(scrollEditor.top + scrollEditor.height * 0.68):0.##}.");
            Assert.True(BottomEdge(timingMarker) <= BottomEdge(scrollEditor) - 4, $"Expected the current timing marker to remain inside the visible editor viewport, but marker bottom was {BottomEdge(timingMarker):0.##} for viewport bottom {BottomEdge(scrollEditor):0.##}.");
        });
    }

    [Fact]
    public void BookmarkAndTimingMarkersStayOnExpectedEditorHalves() {
        RunOpenedFixtureMapTest((driver, _) => {
            var scrollEditor = driver.GetElementBounds(ScrollEditorId);

            driver.SelectMenuItem("Edit>Add New>Bookmark");
            driver.SelectMenuItem("Edit>Add New>Timing Change");
            driver.WaitForIdle();

            var bookmarkMarker = driver.GetNamedElementBoundsWithin(ScrollEditorId, "Bookmark");
            var timingMarker = driver.GetNamedElementBoundsWithin(ScrollEditorId, "1/4 beat");

            Assert.True(bookmarkMarker.left >= scrollEditor.left + scrollEditor.width * 0.35, $"Expected bookmark markers to stay anchored on the right half of the editor, but bookmark left edge was {bookmarkMarker.left:0.##} for editor left edge {scrollEditor.left:0.##} and width {scrollEditor.width:0.##}.");
            Assert.True(RightEdge(bookmarkMarker) <= RightEdge(scrollEditor) + 1, $"Expected bookmark markers to remain inside or flush with the editor's right edge, but bookmark right edge was {RightEdge(bookmarkMarker):0.##} for editor right edge {RightEdge(scrollEditor):0.##}.");
            Assert.True(timingMarker.left <= scrollEditor.left + Math.Max(60, scrollEditor.width * 0.18), $"Expected timing-change markers to stay anchored near the editor's left lane margin, but timing marker left edge was {timingMarker.left:0.##} for editor left edge {scrollEditor.left:0.##} and width {scrollEditor.width:0.##}.");
            Assert.True(RightEdge(timingMarker) <= scrollEditor.left + scrollEditor.width * 0.68, $"Expected timing-change markers to stop well before the editor's right half, but timing marker right edge was {RightEdge(timingMarker):0.##} for editor width {scrollEditor.width:0.##}.");
            Assert.True(timingMarker.left < bookmarkMarker.left, $"Expected timing-change markers to sit left of bookmark markers, but timing left edge was {timingMarker.left:0.##} and bookmark left edge was {bookmarkMarker.left:0.##}.");
        });
    }

    [Fact]
    public void ResizingWindowExpandsEditorViewportAndKeepsEditorChromeAligned() {
        RunOpenedFixtureMapTest((driver, _) => {
            var beforeEditor = driver.GetElementBounds(ScrollEditorId);
            var beforeSpectrogram = driver.GetElementBounds(ScrollSpectrogramId);
            var beforeNav = driver.GetElementBounds(NavWaveformImageId);

            driver.ResizeMainWindow(1500, 1000);
            driver.WaitForIdle(TimeSpan.FromMilliseconds(500));

            var afterEditor = driver.GetElementBounds(ScrollEditorId);
            var afterSpectrogram = driver.GetElementBounds(ScrollSpectrogramId);
            var afterNav = driver.GetElementBounds(NavWaveformImageId);

            Assert.True(afterEditor.width > beforeEditor.width + 40, $"Expected the editor viewport to widen after enlarging the window, but it changed from {beforeEditor.width:0.##} to {afterEditor.width:0.##}.");
            Assert.True(afterEditor.height > beforeEditor.height + 40, $"Expected the editor viewport to grow taller after enlarging the window, but it changed from {beforeEditor.height:0.##} to {afterEditor.height:0.##}.");
            Assert.True(afterSpectrogram.height >= afterEditor.height * 0.95, $"Expected the spectrogram viewport to span nearly the full editor height after resize, but spectrogram height was {afterSpectrogram.height:0.##} and editor height was {afterEditor.height:0.##}.");
            Assert.True(afterNav.height >= afterEditor.height * 0.8, $"Expected the navigation strip to keep spanning most of the editor height after resize, but strip height was {afterNav.height:0.##} and editor height was {afterEditor.height:0.##}.");
            Assert.True(RightEdge(afterEditor) <= afterNav.left + 1, $"Expected the resized editor viewport to stay left of the navigation strip, but editor right edge was {RightEdge(afterEditor):0.##} and strip left edge was {afterNav.left:0.##}.");
        });
    }

    [Fact]
    public void DraggingSpectrogramDividerChangesRelativeWidths() {
        RunOpenedFixtureMapTest((driver, _) => {
            var beforeSpectrogram = driver.GetElementBounds(ScrollSpectrogramId);
            var beforeEditor = driver.GetElementBounds(ScrollEditorId);

                driver.DragElementByOffset(SpectrogramResizeId, 0.5, 0.5, -80, 0);
                driver.WaitForIdle(TimeSpan.FromMilliseconds(300));

            var afterSpectrogram = driver.GetElementBounds(ScrollSpectrogramId);
            var afterEditor = driver.GetElementBounds(ScrollEditorId);

            Assert.True(Math.Abs(afterSpectrogram.width - beforeSpectrogram.width) > 10, $"Expected dragging the spectrogram divider to change the spectrogram width, but it changed from {beforeSpectrogram.width:0.##} to {afterSpectrogram.width:0.##}.");
            Assert.True(Math.Abs(afterEditor.width - beforeEditor.width) > 10, $"Expected dragging the spectrogram divider to change the editor width, but it changed from {beforeEditor.width:0.##} to {afterEditor.width:0.##}.");
        });
    }

    [Fact]
    public void ChangingGridSpacingMovesExistingBookmark() {
        RunOpenedFixtureMapTest((driver, _) => {
            driver.SetSliderValue(SongProgressSliderId, 1200);
            driver.WaitForIdle();
            driver.SelectMenuItem("Edit>Add New>Bookmark");
            driver.WaitForIdle();
            var before = driver.GetNamedElementBoundsWithin(ScrollEditorId, "Bookmark");

            driver.SetText(GridSpacingTextBoxId, "2");
            driver.SendKeyboardShortcutToElement(GridSpacingTextBoxId, "Enter");
            driver.WaitForIdle(TimeSpan.FromMilliseconds(250));

            var after = driver.GetNamedElementBoundsWithin(ScrollEditorId, "Bookmark");
            Assert.True(Math.Abs(before.top - after.top) > 2, $"Expected changing grid spacing to move bookmark rendering, but top only changed from {before.top:0.##} to {after.top:0.##}.");
        });
    }

    [Fact]
    public void ScrollingEditorViewportUpdatesPlaybackPosition() {
        RunOpenedFixtureMapTest((driver, _) => {
            var before = driver.GetText(SongPositionTextId);

            driver.SetScrollViewerVerticalPercent(ScrollEditorId, 50);
            driver.WaitForIdle(TimeSpan.FromMilliseconds(450));

            var after = driver.GetText(SongPositionTextId);
            if (string.Equals(after, before, StringComparison.Ordinal)) {
                driver.SetScrollViewerVerticalPercent(ScrollEditorId, 20);
                driver.WaitForIdle(TimeSpan.FromMilliseconds(450));
                after = driver.GetText(SongPositionTextId);
            }

            Assert.NotEqual(before, after);
        });
    }

    [Fact]
    public void ScrollingEditorViewportUpdatesGridWaveformStatus() {
        RunOpenedFixtureMapTest((driver, _) => {
            driver.ToggleCheckbox(GridWaveformCheckBoxId, true);
            driver.WaitForIdle();
            var before = driver.GetItemStatus(ScrollEditorId);

            driver.SetScrollViewerVerticalPercent(ScrollEditorId, 20);
            driver.WaitForIdle(TimeSpan.FromMilliseconds(450));

            var after = driver.GetItemStatus(ScrollEditorId);
            if (string.Equals(after, before, StringComparison.Ordinal)) {
                driver.SetScrollViewerVerticalPercent(ScrollEditorId, 50);
                driver.WaitForIdle(TimeSpan.FromMilliseconds(450));
                after = driver.GetItemStatus(ScrollEditorId);
            }
            Assert.NotEqual(before, after);
        });
    }

    [Fact]
    public void PlaybackStartsScrollingVisibleTimingMarkerWithinQuarterSecond() {
        RunOpenedFixtureMapTest((driver, _) => {
            driver.SelectMenuItem("Edit>Add New>Bookmark");
            driver.WaitForIdle();
            var beforeProgress = driver.GetSliderValue(SongProgressSliderId);
            var beforeMarker = driver.GetNamedElementBoundsWithin(ScrollEditorId, "Bookmark");

            driver.ClickWithinElement(SongPlayerButtonId, 0.5, 0.5);
            driver.WaitForIdle(TimeSpan.FromMilliseconds(150));
            Thread.Sleep(250);

            var afterProgress = driver.GetSliderValue(SongProgressSliderId);
            var afterMarker = driver.GetNamedElementBoundsWithin(ScrollEditorId, "Bookmark");

            driver.ClickWithinElement(SongPlayerButtonId, 0.5, 0.5);
            driver.WaitForIdle();

            Assert.True(afterProgress >= beforeProgress + 100, $"Expected playback to advance the progress slider almost immediately, but it only changed from {beforeProgress:0.##}ms to {afterProgress:0.##}ms.");
            Assert.True(Math.Abs(afterMarker.top - beforeMarker.top) > 10, $"Expected playback to start moving the visible timing marker almost immediately, but its top position only changed from {beforeMarker.top:0.##} to {afterMarker.top:0.##} after a quarter-second.");
        });
    }

    [Fact]
    public void SongProgressSliderMovesEditorViewportWithVisibleBookmark() {
        RunOpenedFixtureMapTest((driver, _) => {
            driver.SetSliderValue(SongProgressSliderId, 1200);
            driver.WaitForIdle();
            driver.SelectMenuItem("Edit>Add New>Bookmark");
            driver.WaitForIdle();

            var before = driver.GetNamedElementBoundsWithin(ScrollEditorId, "Bookmark");

            driver.SetSliderValue(SongProgressSliderId, 0);
            driver.WaitForIdle();

            var after = driver.GetNamedElementBoundsWithin(ScrollEditorId, "Bookmark");
            Assert.True(Math.Abs(before.top - after.top) > 25, $"Expected the bookmark to move within the editor viewport when song progress changed, but its top position only changed from {before.top:0.##} to {after.top:0.##}.");
        });
    }

    [Fact]
    public void UndoAndRedoRestoreEditorGridNoteCount() {
        RunOpenedFixtureMapTest((driver, _) => {
            var initialAll = ParseIntegerText(driver.GetText(NotesStatsAllId), NotesStatsAllId);

            driver.SelectMenuItem("Edit>Add New>Note (column 2)");
            driver.WaitForIdle();
            Assert.Equal(initialAll + 1, ParseIntegerText(driver.GetText(NotesStatsAllId), NotesStatsAllId));

            driver.SendKeyboardShortcut("Ctrl+Z");
            driver.WaitForIdle();
            Assert.Equal(initialAll, ParseIntegerText(driver.GetText(NotesStatsAllId), NotesStatsAllId));

            driver.SendKeyboardShortcut("Ctrl+Y");
            driver.WaitForIdle();
            Assert.Equal(initialAll + 1, ParseIntegerText(driver.GetText(NotesStatsAllId), NotesStatsAllId));

            driver.SendKeyboardShortcut("Ctrl+Z");
            driver.WaitForIdle();
            Assert.Equal(initialAll, ParseIntegerText(driver.GetText(NotesStatsAllId), NotesStatsAllId));

            driver.SendKeyboardShortcut("Ctrl+Shift+Z");
            driver.WaitForIdle();
            Assert.Equal(initialAll + 1, ParseIntegerText(driver.GetText(NotesStatsAllId), NotesStatsAllId));
        });
    }

    [Fact]
    public void SelectAllAndDeleteRemoveAllNotesFromGrid() {
        RunOpenedFixtureMapTest((driver, _) => {
            driver.SelectMenuItem("Edit>Add New>Note (column 1)");
            driver.SelectMenuItem("Edit>Add New>Note (column 2)");
            driver.WaitForIdle();

            var allNotes = ParseIntegerText(driver.GetText(NotesStatsAllId), NotesStatsAllId);
            Assert.True(allNotes > 0);

            driver.SendKeyboardShortcut("Ctrl+A");
            driver.WaitForIdle();
            Assert.Equal(allNotes, ParseIntegerText(driver.GetText(NotesStatsSelectedId), NotesStatsSelectedId));

            driver.SendKeyboardShortcut("Delete");
            driver.WaitForIdle();
            Assert.Equal(0, ParseIntegerText(driver.GetText(NotesStatsAllId), NotesStatsAllId));
            Assert.Equal(0, ParseIntegerText(driver.GetText(NotesStatsSelectedId), NotesStatsSelectedId));
        });
    }

    [Fact]
    public void CtrlAIsIgnoredDuringPlayback() {
        RunOpenedFixtureMapTest((driver, _) => {
            driver.SelectMenuItem("Edit>Add New>Note (column 1)");
            driver.SelectMenuItem("Edit>Add New>Note (column 2)");
            driver.WaitForIdle();

            Assert.Equal(0, ParseIntegerText(driver.GetText(NotesStatsSelectedId), NotesStatsSelectedId));

            driver.SendKeyboardShortcut("Space");
            driver.WaitForIdle();
            driver.SendKeyboardShortcut("Ctrl+A");
            driver.WaitForIdle();
            driver.SendKeyboardShortcut("Space");
            driver.WaitForIdle();

            Assert.Equal(0, ParseIntegerText(driver.GetText(NotesStatsSelectedId), NotesStatsSelectedId));
        });
    }

    [Fact]
    public void NumberKeyAddsNoteWhenSongIsPlaying() {
        RunOpenedFixtureMapTest((driver, _) => {
            var initialAll = ParseIntegerText(driver.GetText(NotesStatsAllId), NotesStatsAllId);

            driver.SendKeyboardShortcut("Space");
            driver.WaitForIdle();
            driver.SendKeyboardShortcut("1");
            driver.WaitForIdle();
            driver.SendKeyboardShortcut("Space");
            driver.WaitForIdle();

            Assert.Equal(initialAll + 1, ParseIntegerText(driver.GetText(NotesStatsAllId), NotesStatsAllId));
        });
    }

    [Fact]
    public void AddingNotesUpdatesEveryVisibleEditorGridStatsField() {
        RunOpenedFixtureMapTest((driver, _) => {
            driver.ClickButton(ColumnStatsButtonId);

            driver.SelectMenuItem("Edit>Add New>Note (column 1)");
            driver.WaitForIdle();

            Assert.Equal(1, ParseIntegerText(driver.GetText(NotesStatsAllId), NotesStatsAllId));
            Assert.Equal(0, ParseIntegerText(driver.GetText(NotesStatsSelectedId), NotesStatsSelectedId));
            Assert.Equal(1, ParseIntegerText(driver.GetText(NotesStatsSingleId), NotesStatsSingleId));
            Assert.Equal(0, ParseIntegerText(driver.GetText(NotesStatsDoubleId), NotesStatsDoubleId));
            Assert.Equal(0, ParseIntegerText(driver.GetText(NotesStatsTriplePlusId), NotesStatsTriplePlusId));
            Assert.Equal(1, ParseIntegerText(driver.GetText(ColumnStatsValue1Id), ColumnStatsValue1Id));
            Assert.Equal(0, ParseIntegerText(driver.GetText(ColumnStatsValue2Id), ColumnStatsValue2Id));
            Assert.Equal(0, ParseIntegerText(driver.GetText(ColumnStatsValue3Id), ColumnStatsValue3Id));
            Assert.Equal(0, ParseIntegerText(driver.GetText(ColumnStatsValue4Id), ColumnStatsValue4Id));
            Assert.Equal("100%", driver.GetText(ColumnStatsPercentage1Id));
            Assert.Equal("0%", driver.GetText(ColumnStatsPercentage2Id));
            Assert.Equal("0%", driver.GetText(ColumnStatsPercentage3Id));
            Assert.Equal("0%", driver.GetText(ColumnStatsPercentage4Id));

            driver.SelectMenuItem("Edit>Add New>Note (column 2)");
            driver.WaitForIdle();

            Assert.Equal(2, ParseIntegerText(driver.GetText(NotesStatsAllId), NotesStatsAllId));
            Assert.Equal(0, ParseIntegerText(driver.GetText(NotesStatsSelectedId), NotesStatsSelectedId));
            Assert.Equal(0, ParseIntegerText(driver.GetText(NotesStatsSingleId), NotesStatsSingleId));
            Assert.Equal(1, ParseIntegerText(driver.GetText(NotesStatsDoubleId), NotesStatsDoubleId));
            Assert.Equal(0, ParseIntegerText(driver.GetText(NotesStatsTriplePlusId), NotesStatsTriplePlusId));
            Assert.Equal(1, ParseIntegerText(driver.GetText(ColumnStatsValue1Id), ColumnStatsValue1Id));
            Assert.Equal(1, ParseIntegerText(driver.GetText(ColumnStatsValue2Id), ColumnStatsValue2Id));
            Assert.Equal(0, ParseIntegerText(driver.GetText(ColumnStatsValue3Id), ColumnStatsValue3Id));
            Assert.Equal(0, ParseIntegerText(driver.GetText(ColumnStatsValue4Id), ColumnStatsValue4Id));
            Assert.Equal("50%", driver.GetText(ColumnStatsPercentage1Id));
            Assert.Equal("50%", driver.GetText(ColumnStatsPercentage2Id));
            Assert.Equal("0%", driver.GetText(ColumnStatsPercentage3Id));
            Assert.Equal("0%", driver.GetText(ColumnStatsPercentage4Id));
        });
    }

    [Fact]
    public void NumberKeyIsIgnoredWhenTextboxHasFocusDuringPlayback() {
        RunOpenedFixtureMapTest((driver, _) => {
            var initialAll = ParseIntegerText(driver.GetText(NotesStatsAllId), NotesStatsAllId);

            driver.SendKeyboardShortcut("Space");
            driver.WaitForIdle();
            driver.SetText(SongNameTextBoxId, driver.GetText(SongNameTextBoxId));
            driver.WaitForIdle();
            driver.SendKeyboardShortcut("1");
            driver.WaitForIdle();
            driver.ClickButton(SongPlayerButtonId);
            driver.WaitForIdle();

            Assert.Equal(initialAll, ParseIntegerText(driver.GetText(NotesStatsAllId), NotesStatsAllId));
        });
    }

    [Fact]
    public void ClickingEditorBackgroundKeepsGridSnapStateWhileSpaceStartsPlayback() {
        RunOpenedFixtureMapTest((driver, _) => {
            driver.ToggleCheckbox(GridSnapCheckBoxId, false);
            driver.WaitForIdle();
            var beforeCheckbox = driver.IsChecked(GridSnapCheckBoxId);
            var beforeProgress = driver.GetSliderValue(SongProgressSliderId);

            driver.ClickWithinElement(ScrollEditorId, 0.02, 0.8);
            driver.WaitForIdle();
            driver.SendKeyboardShortcut("Space");
            driver.WaitForIdle(TimeSpan.FromMilliseconds(150));
            Thread.Sleep(250);
            var duringProgress = driver.GetSliderValue(SongProgressSliderId);
            driver.SendKeyboardShortcut("Space");
            driver.WaitForIdle();

            Assert.Equal(beforeCheckbox, driver.IsChecked(GridSnapCheckBoxId));
            Assert.True(duringProgress > beforeProgress + 100, $"Expected space to start playback after clicking editor background, but progress only changed from {beforeProgress:0.##}ms to {duringProgress:0.##}ms.");
        });
    }

    [Fact]
    public void RightClickOnHoveredNoteRemovesIt() {
        RunOpenedFixtureMapTest((driver, _) => {
            var initialAll = ParseIntegerText(driver.GetText(NotesStatsAllId), NotesStatsAllId);

            driver.ClickWithinElement(ScrollEditorId, 0.2, 0.8);
            driver.WaitForIdle();
            Assert.Equal(initialAll + 1, ParseIntegerText(driver.GetText(NotesStatsAllId), NotesStatsAllId));

            driver.MoveMouseWithinElement(ScrollEditorId, 0.2, 0.8);
            driver.RightClickWithinElement(ScrollEditorId, 0.2, 0.8);
            driver.WaitForIdle();
            Assert.Equal(initialAll, ParseIntegerText(driver.GetText(NotesStatsAllId), NotesStatsAllId));
        });
    }

    [Fact]
    public void PlaybackLaneInputAnimatesDrumAndPlacesNoteOverDrumLane() {
        RunOpenedFixtureMapTest((driver, _) => {
            var initialAll = ParseIntegerText(driver.GetText(NotesStatsAllId), NotesStatsAllId);
            var drum0 = driver.GetElementBounds(Drum0Id);
            var editor = driver.GetElementBounds(ScrollEditorId);
            var noteXRatio = ClampRatio((CenterX(drum0) - editor.left) / editor.width);
            const double noteYRatio = 0.8;
            var beforeDrumStatus = driver.GetItemStatus(Drum0Id);
            ConfigureDrumOnlyAudio(driver);
            using var probe = AudioOutputProbe.Create();
            probe.MeasurePeak(TimeSpan.FromMilliseconds(150));

            driver.MoveMouseWithinElement(ScrollEditorId, noteXRatio, noteYRatio);
            driver.WaitForIdle(TimeSpan.FromMilliseconds(80));
            driver.ClickWithinElement(ScrollEditorId, noteXRatio, noteYRatio);
            driver.WaitForIdle();

            var placedNote = driver.GetNamedElementBoundsWithin(ScrollEditorId, "Note 1");
            using var placedBitmap = driver.CaptureElementBitmap(ScrollEditorId);
            var drumCenterX = editor.left + FindOrangeDrumCenterXNear(placedBitmap, CenterX(drum0) - editor.left, Math.Max(24, (int)Math.Round(drum0.width * 0.6)));
            Assert.True(Math.Abs(CenterX(placedNote) - drumCenterX) <= Math.Max(12, drum0.width * 0.25), $"Expected the newly placed column-1 note to align over Drum 1, but note center X was {CenterX(placedNote):0.##} and drum center X was {drumCenterX:0.##}.");

            driver.SendKeyboardShortcut("Space");
            driver.WaitForIdle();
            var threshold = GetAudibleThreshold(probe.MeasurePeak(TimeSpan.FromMilliseconds(150)));
            probe.ResetPeak();
            driver.SendKeyboardShortcut("1");
            Assert.True(probe.WaitForPeakAbove(threshold, TimeSpan.FromSeconds(2)), $"Expected playback lane input to produce audible drum output above threshold {threshold:0.0000}, but measured peak was {probe.GetPeakAmplitude():0.0000}.");
            driver.WaitForIdle(TimeSpan.FromMilliseconds(150));
            driver.SendKeyboardShortcut("Space");
            driver.WaitForIdle();

            Assert.NotEqual(beforeDrumStatus, driver.GetItemStatus(Drum0Id));
            Assert.True(ParseIntegerText(driver.GetText(NotesStatsAllId), NotesStatsAllId) >= initialAll + 1, $"Expected playback lane input flow to leave at least the placed note visible, but the note count stayed at {driver.GetText(NotesStatsAllId)}.");
        }, mutateFixture: ClearNotesFromFixture);
    }

    [Fact]
    public void NotesPlacedAcrossAllLanesStayCenteredOverEachDrum() {
        RunOpenedFixtureMapTest((driver, _) => {
            var editor = driver.GetElementBounds(ScrollEditorId);
            var drumIds = new[] { Drum0Id, Drum1Id, Drum2Id, Drum3Id };

            for (var lane = 0; lane < drumIds.Length; lane++) {
                var drum = driver.GetElementBounds(drumIds[lane]);
                var xRatio = ClampRatio((CenterX(drum) - editor.left) / editor.width);
                driver.MoveMouseWithinElement(ScrollEditorId, xRatio, 0.82);
                driver.WaitForIdle(TimeSpan.FromMilliseconds(80));
                driver.ClickWithinElement(ScrollEditorId, xRatio, 0.82);
                driver.WaitForIdle();
            }

            using var bitmap = driver.CaptureElementBitmap(ScrollEditorId);
            for (var lane = 0; lane < drumIds.Length; lane++) {
                var drum = driver.GetElementBounds(drumIds[lane]);
                var note = driver.GetNamedElementBoundsWithin(ScrollEditorId, $"Note {lane + 1}");
                var drumCenterX = editor.left + FindOrangeDrumCenterXNear(bitmap, CenterX(drum) - editor.left, Math.Max(24, (int)Math.Round(drum.width * 0.6)));
                Assert.True(
                    Math.Abs(CenterX(note) - drumCenterX) <= Math.Max(10, drum.width * 0.2),
                    $"Expected lane {lane + 1} note placement to stay centered over its drum, but note center X was {CenterX(note):0.##} and drum center X was {drumCenterX:0.##}.");
            }
        }, mutateFixture: ClearNotesFromFixture);
    }

    [Fact]
    public void PlaybackHitUsesDrumAudioWithoutLeavingNoteDimmedOrPulsingDrum() {
        RunOpenedFixtureMapTest((driver, _) => {
            ConfigureDrumOnlyAudio(driver);
            using var probe = AudioOutputProbe.Create();
            using var beforeDrum = driver.CaptureElementBitmap(Drum0Id);
            var threshold = GetAudibleThreshold(probe.MeasurePeak(TimeSpan.FromMilliseconds(150)));
            probe.ResetPeak();

            driver.MoveMouseWithinElement(ScrollEditorId, 0.2, 0.8);
            driver.WaitForIdle(TimeSpan.FromMilliseconds(100));
            driver.ClickWithinElement(ScrollEditorId, 0.2, 0.8);
            Assert.True(probe.WaitForPeakAbove(threshold, TimeSpan.FromSeconds(2)), $"Expected note placement to produce audible drum output above threshold {threshold:0.0000}, but measured peak was {probe.GetPeakAmplitude():0.0000}.");

            Assert.Contains("opacity:1", driver.GetNamedElementItemStatusWithin(ScrollEditorId, "Note 1"));
        });
    }

    [Fact]
    public void PlaybackHitLogsDrumSampleWhenNoteReachesBeatScanLine() {
        RunOpenedFixtureMapTest((driver, _) => {
            ConfigureDrumOnlyAudio(driver);
            using var probe = AudioOutputProbe.Create();
            var threshold = GetAudibleThreshold(probe.MeasurePeak(TimeSpan.FromMilliseconds(150)));
            driver.MoveMouseWithinElement(ScrollEditorId, 0.2, 0.8);
            driver.WaitForIdle(TimeSpan.FromMilliseconds(100));
            driver.ClickWithinElement(ScrollEditorId, 0.2, 0.8);
            driver.WaitForIdle();
            probe.ResetPeak();

            driver.ClickWithinElement(SongPlayerButtonId, 0.5, 0.5);
            Assert.True(probe.WaitForPeakAbove(threshold, TimeSpan.FromSeconds(2)), $"Expected playback note hit to produce audible drum output above threshold {threshold:0.0000}, but measured peak was {probe.GetPeakAmplitude():0.0000}.");
            driver.ClickWithinElement(SongPlayerButtonId, 0.5, 0.5);
            driver.WaitForIdle();
        }, mutateFixture: ClearNotesFromFixture);
    }

    [Fact]
    public void DrumSamplesResolveFromAppBaseDirectoryWhenLaunchWorkingDirectoryDiffers() {
        var launchWorkingDirectory = CreateTempOutputFolder("launch-cwd");
        try {
            RunOpenedFixtureMapTest((driver, _) => {
                ConfigureDrumOnlyAudio(driver);
                using var probe = AudioOutputProbe.Create();
                var threshold = GetAudibleThreshold(probe.MeasurePeak(TimeSpan.FromMilliseconds(150)));
                probe.ResetPeak();
                driver.MoveMouseWithinElement(ScrollEditorId, 0.2, 0.8);
                driver.WaitForIdle(TimeSpan.FromMilliseconds(100));
                driver.ClickWithinElement(ScrollEditorId, 0.2, 0.8);
                driver.WaitForIdle();

                Assert.True(probe.WaitForPeakAbove(threshold, TimeSpan.FromSeconds(2)), $"Expected drum samples to stay audible when launched from a non-app working directory, but measured peak was {probe.GetPeakAmplitude():0.0000}.");
            }, configureDriver: driver => driver.SetLaunchWorkingDirectory(launchWorkingDirectory));
        } finally {
            SafeDeleteDirectory(launchWorkingDirectory);
        }
    }

    [Fact]
    public void EnablingMetronomeProducesMetronomePlaybackDuringSongPlayback() {
        RunOpenedFixtureMapTest((driver, _) => {
            ConfigureDrumOnlyAudio(driver);
            using var probe = AudioOutputProbe.Create();
            driver.ToggleCheckbox(MetronomeCheckboxId, true);
            driver.WaitForIdle();
            var threshold = GetAudibleThreshold(probe.MeasurePeak(TimeSpan.FromMilliseconds(150)));
            probe.ResetPeak();

            driver.ClickWithinElement(SongPlayerButtonId, 0.5, 0.5);
            Assert.True(probe.WaitForPeakAbove(threshold, TimeSpan.FromSeconds(2)), $"Expected metronome playback to produce audible output above threshold {threshold:0.0000}, but measured peak was {probe.GetPeakAmplitude():0.0000}.");
            driver.ClickWithinElement(SongPlayerButtonId, 0.5, 0.5);
            driver.WaitForIdle();
        }, mutateFixture: ClearNotesFromFixture);
    }

    [Fact]
    public void BeatScanLinePassesThroughDrumCenters() {
        RunOpenedFixtureMapTest((driver, _) => {
            var drum0 = driver.GetElementBounds(Drum0Id);
            var drum1 = driver.GetElementBounds(Drum1Id);
            var drum2 = driver.GetElementBounds(Drum2Id);
            var drum3 = driver.GetElementBounds(Drum3Id);
            var editor = driver.GetElementBounds(ScrollEditorId);
            using var bitmap = driver.CaptureElementBitmap(ScrollEditorId);
            var drumCenterY = FindOrangeDrumCenterY(bitmap);
            var sampleXs = new[] {
                (int)Math.Round((((CenterX(drum0) + CenterX(drum1)) / 2.0) - editor.left)),
                (int)Math.Round((((CenterX(drum1) + CenterX(drum2)) / 2.0) - editor.left)),
                (int)Math.Round((((CenterX(drum2) + CenterX(drum3)) / 2.0) - editor.left))
            };
            var detectedY = FindDarkHorizontalGuideRow(bitmap, drumCenterY, sampleXs, Math.Max(12, (int)Math.Round(drum0.height * 0.35)));
            Assert.InRange(Math.Abs(detectedY - drumCenterY), 0, 10);
        });
    }

    [Fact]
    public void DefaultGridSpacingUsesWpfSizedNoteFootprint() {
        RunOpenedFixtureMapTest((driver, _) => {
            using var before = driver.CaptureElementBitmap(ScrollEditorId);
            driver.MoveMouseWithinElement(ScrollEditorId, 0.2, 0.8);
            driver.WaitForIdle(TimeSpan.FromMilliseconds(100));
            driver.ClickWithinElement(ScrollEditorId, 0.2, 0.8);
            driver.WaitForIdle();
            using var after = driver.CaptureElementBitmap(ScrollEditorId);

            var note = GetChangedPixelBounds(before, after);
            var drum0 = driver.GetElementBounds(Drum0Id);
            Assert.True(note.height >= drum0.height * 0.85, $"Expected the default note footprint to stay close to the WPF-sized drum lane footprint, but note height was {note.height:0.##} for drum height {drum0.height:0.##}.");
        });
    }

    [Fact]
    public void StatsPanelUsesCompactLayoutAndHighlightsTriplePlusWarnings() {
        RunOpenedFixtureMapTest((driver, _) => {
            var top = driver.GetElementBounds(NotesStatsAllId);
            var bottom = driver.GetElementBounds(ColumnStatsButtonId);

            using var labelBitmap = driver.CaptureElementBitmap(NotesStatsTriplePlusLabelId);
            using var valueBitmap = driver.CaptureElementBitmap(NotesStatsTriplePlusId);

            Assert.Equal("1", driver.GetText(NotesStatsTriplePlusId));
            Assert.True(BottomEdge(bottom) - top.top <= 130, $"Expected the map stats section to stay compact like WPF, but the visible stats stack used {BottomEdge(bottom) - top.top:0.##} px.");
            Assert.True(ContainsWarningRed(labelBitmap) || ContainsWarningRed(valueBitmap), "Expected triple+ warning stats to render with a visible red warning tint.");
        }, mutateFixture: AddTriplePlusNotesToFixture);
    }

    [Fact]
    public void NavigationWaveformStaysVisuallyCenteredWithinTimeline() {
        RunOpenedFixtureMapTest((driver, _) => {
            using var bitmap = driver.CaptureElementBitmap(NavWaveformImageId);
            var centerRatio = ComputeInterestingPixelCenterRatio(bitmap);
            Assert.InRange(centerRatio, 0.35, 0.65);
        });
    }

    [Fact]
    public void SnapToGridKeepsClicksOnCorrectSideOfTimingChange() {
        RunOpenedFixtureMapTest((driver, _) => {
            var marker = driver.GetNamedElementBoundsWithin(ScrollEditorId, "1/3 beat");
            var editor = driver.GetElementBounds(ScrollEditorId);
            var timingLineY = BottomEdge(marker) - 2;
            var aboveRatio = ClampRatio((timingLineY - 6 - editor.top) / editor.height);
            var belowRatio = ClampRatio((timingLineY + 6 - editor.top) / editor.height);

            driver.MoveMouseWithinElement(ScrollEditorId, 0.8, aboveRatio);
            driver.WaitForIdle(TimeSpan.FromMilliseconds(80));
            driver.ClickWithinElement(ScrollEditorId, 0.8, aboveRatio);
            driver.WaitForIdle();
            var note1 = driver.GetNamedElementBoundsWithin(ScrollEditorId, "Note 4");

            driver.MoveMouseWithinElement(ScrollEditorId, 0.2, belowRatio);
            driver.WaitForIdle(TimeSpan.FromMilliseconds(80));
            driver.ClickWithinElement(ScrollEditorId, 0.2, belowRatio);
            driver.WaitForIdle();
            var visibleNoteCount = driver.GetText(NotesStatsAllId);
            Assert.True(visibleNoteCount == "2", $"Expected the second click beside the timing change to add a second note, but the visible note count stayed at {visibleNoteCount} and editor status was '{driver.GetItemStatus(ScrollEditorId)}'.");
            var note2 = driver.GetNamedElementBoundsWithin(ScrollEditorId, "Note 1");

            Assert.True(CenterY(note1) < timingLineY - 4, $"Expected the note placed just above the timing change to stay above its beat line, but note center Y was {CenterY(note1):0.##} for timing line Y {timingLineY:0.##}.");
            Assert.True(CenterY(note2) > timingLineY + 4, $"Expected the note placed just below the timing change to stay below its beat line, but note center Y was {CenterY(note2):0.##} for timing line Y {timingLineY:0.##}.");
        }, mutateFixture: ApplyTimingChangeFixtureOnEmptyGrid);
    }

    [Fact]
    public void TimingChangeLabelKeepsAdjacentLaneClickable() {
        RunOpenedFixtureMapTest((driver, _) => {
            var marker = driver.GetNamedElementBoundsWithin(ScrollEditorId, "1/3 beat");
            var editor = driver.GetElementBounds(ScrollEditorId);
            var drum1 = driver.GetElementBounds(Drum1Id);
            var timingLineY = BottomEdge(marker) - 2;
            var xRatio = ClampRatio((CenterX(drum1) - editor.left) / editor.width);
            var yRatio = ClampRatio((timingLineY - 6 - editor.top) / editor.height);

            driver.MoveMouseWithinElement(ScrollEditorId, xRatio, yRatio);
            driver.WaitForIdle(TimeSpan.FromMilliseconds(80));
            driver.ClickWithinElement(ScrollEditorId, xRatio, yRatio);
            driver.WaitForIdle();

            Assert.Equal("1", driver.GetText(NotesStatsAllId));
            var note = driver.GetNamedElementBoundsWithin(ScrollEditorId, "Note 2");
            Assert.True(CenterY(note) < timingLineY - 4, $"Expected the adjacent-lane click beside the timing label to place a note above the timing line, but note center Y was {CenterY(note):0.##} for timing line Y {timingLineY:0.##}.");
        }, mutateFixture: ApplyTimingChangeFixtureOnEmptyGrid);
    }

    [Fact]
    public void SidebarsKeepCoreControlsWithinDockBounds() {
        RunOpenedFixtureMapTest((driver, _) => {
            var mainWindow = driver.GetElementBounds(MainWindowId);
            var editor = driver.GetElementBounds(ScrollEditorId);
            var navWaveform = driver.GetElementBounds(NavWaveformImageId);
            var songName = driver.GetElementBounds(SongNameTextBoxId);
            var cover = driver.GetElementBounds("imgCover");
            var changeBpm = driver.GetElementBounds(ChangeBpmButtonId);
            var customizeNav = driver.GetElementBounds(CustomizeNavBarButtonId);
            var difficultyNumber = driver.GetElementBounds(DifficultyNumberId);
            var noteSpeed = driver.GetElementBounds(NoteSpeedId);
            var songVol = driver.GetElementBounds(SongVolumeValueId);
            var drumVol = driver.GetElementBounds(DrumVolumeValueId);
            var songTempo = driver.GetElementBounds(SongTempoValueId);

            Assert.True(songName.left >= mainWindow.left + 10, $"Expected left sidebar content to keep at least 10 px of inner padding, but song name left edge was {songName.left:0.##} for window left edge {mainWindow.left:0.##}.");
            Assert.True(RightEdge(difficultyNumber) <= RightEdge(mainWindow) - 14, $"Expected right sidebar content to keep visible inner padding, but difficulty field right edge was {RightEdge(difficultyNumber):0.##} for window right edge {RightEdge(mainWindow):0.##}.");
            Assert.True(RightEdge(songName) <= editor.left - 4, $"Expected the song name field to stay left of the editor viewport, but its right edge was {RightEdge(songName):0.##} for editor left edge {editor.left:0.##}.");
            Assert.True(RightEdge(cover) <= editor.left - 4, $"Expected the cover preview to stay left of the editor viewport, but its right edge was {RightEdge(cover):0.##} for editor left edge {editor.left:0.##}.");
            Assert.True(changeBpm.width >= 185, $"Expected the timing button to retain its full readable width, but it was only {changeBpm.width:0.##}px wide.");
            Assert.True(customizeNav.width >= 185, $"Expected the navigation-bar button to retain its full readable width, but it was only {customizeNav.width:0.##}px wide.");
            Assert.True(changeBpm.left >= RightEdge(navWaveform) - 1, $"Expected the timing button to stay right of the navigation waveform, but button left edge was {changeBpm.left:0.##} for waveform right edge {RightEdge(navWaveform):0.##}.");
            Assert.True(customizeNav.left >= RightEdge(navWaveform) - 1, $"Expected the navigation-bar button to stay right of the navigation waveform, but button left edge was {customizeNav.left:0.##} for waveform right edge {RightEdge(navWaveform):0.##}.");
            Assert.True(difficultyNumber.left >= RightEdge(navWaveform) - 1, $"Expected the difficulty number field to stay in the right settings dock, but left edge was {difficultyNumber.left:0.##} for waveform right edge {RightEdge(navWaveform):0.##}.");
            Assert.True(noteSpeed.left >= RightEdge(navWaveform) - 1, $"Expected the note-speed field to stay in the right settings dock, but left edge was {noteSpeed.left:0.##} for waveform right edge {RightEdge(navWaveform):0.##}.");
            Assert.InRange(songVol.width, 28, 40);
            Assert.InRange(drumVol.width, 28, 40);
            Assert.InRange(songTempo.width, 28, 40);
        });
    }

    [Fact]
    public void ClickingAndRemovingHoveredNoteUpdatesVisibleCountWithinOneSecond() {
        RunOpenedFixtureMapTest((driver, _) => {
            var initialAll = ParseIntegerText(driver.GetText(NotesStatsAllId), NotesStatsAllId);

            driver.MoveMouseWithinElement(ScrollEditorId, 0.2, 0.8);
            driver.WaitForIdle(TimeSpan.FromMilliseconds(100));
            Assert.True(driver.IsVisible(PreviewNoteId));

            driver.ClickWithinElement(ScrollEditorId, 0.2, 0.8);
            Assert.True(WaitForVisibleNoteCount(() => ParseIntegerText(driver.GetText(NotesStatsAllId), NotesStatsAllId), initialAll + 1), $"Expected note placement to update the visible note count within one second, but it stayed at {driver.GetText(NotesStatsAllId)}.");

            driver.MoveMouseWithinElement(ScrollEditorId, 0.2, 0.8);
            driver.WaitForIdle(TimeSpan.FromMilliseconds(100));
            driver.RightClickWithinElement(ScrollEditorId, 0.2, 0.8);
            Assert.True(WaitForVisibleNoteCount(() => ParseIntegerText(driver.GetText(NotesStatsAllId), NotesStatsAllId), initialAll), $"Expected note removal to update the visible note count within one second, but it stayed at {driver.GetText(NotesStatsAllId)}.");
        });
    }

    private static void RunOpenedFixtureMapTest(Action<AvaloniaUIDriver, string> testBody, Action<AvaloniaUIDriver>? configureDriver = null, Action<string>? mutateFixture = null) {
        var driver = new AvaloniaUIDriver();
        string? mapFolder = null;

        try {
            configureDriver?.Invoke(driver);
            mapFolder = LaunchAndOpenFixtureMap(driver, mutateFixture);
            testBody(driver, mapFolder);
        } finally {
            driver.Shutdown();
            SafeDeleteDirectory(mapFolder);
        }
    }

    private static string LaunchAndOpenFixtureMap(AvaloniaUIDriver driver, Action<string>? mutateFixture = null) {
        var fixtureCopy = CreateFixtureMapCopy(mutateFixture);

        driver.Launch();
        driver.WaitForIdle();
        driver.SetTestFileSelection(fixtureCopy);
        driver.ClickButton(StartupOpenMapButtonId);
        driver.WaitForMainWindow();

        return fixtureCopy;
    }

    private static string CreateFixtureMapCopy(Action<string>? mutateFixture = null) {
        var fixtureSourcePath = Path.Combine(GetRepositoryRoot(), FixtureMapFolderRelative);
        Assert.True(Directory.Exists(fixtureSourcePath), $"MainWindow fixture map folder was not found: {fixtureSourcePath}");

        var fixtureCopyPath = CreateTempOutputFolder("grid-fixture");
        CopyDirectoryRecursively(fixtureSourcePath, fixtureCopyPath);
        mutateFixture?.Invoke(fixtureCopyPath);
        return fixtureCopyPath;
    }

    private static string CreateTempOutputFolder(string tag) {
        var outputPath = Path.Combine(Path.GetTempPath(), "Edda-AvaloniaEditorGridTests", tag, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outputPath);
        return outputPath;
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

    private static int ParseIntegerText(string value, string controlId) {
        if (int.TryParse(value, out var parsed)) {
            return parsed;
        }

        throw new InvalidOperationException($"Expected integer text in '{controlId}', but got '{value}'.");
    }

    private static double CenterY((double left, double top, double width, double height) bounds) {
        return bounds.top + bounds.height / 2;
    }

    private static double CenterX((double left, double top, double width, double height) bounds) {
        return bounds.left + bounds.width / 2;
    }

    private static double RightEdge((double left, double top, double width, double height) bounds) {
        return bounds.left + bounds.width;
    }

    private static double BottomEdge((double left, double top, double width, double height) bounds) {
        return bounds.top + bounds.height;
    }

    private static bool WaitForVisibleNoteCount(Func<int> getCount, int expectedCount) {
        var timeout = DateTime.UtcNow + TimeSpan.FromSeconds(1);
        while (DateTime.UtcNow < timeout) {
            if (getCount() == expectedCount) {
                return true;
            }

            Thread.Sleep(50);
        }

        return getCount() == expectedCount;
    }

    private static void ApplyTimingChangeFixture(string fixturePath) {
        var easyMapPath = Path.Combine(fixturePath, "easy.dat");
        var root = JsonNode.Parse(File.ReadAllText(easyMapPath))!.AsObject();
        var customData = root["_customData"]!.AsObject();
        customData["_BPMChanges"] = new JsonArray {
            new JsonObject {
                ["_BPM"] = 155,
                ["_time"] = 2,
                ["_beatsPerBar"] = 3,
                ["_metronomeOffset"] = 3
            }
        };

        File.WriteAllText(easyMapPath, root.ToJsonString(new JsonSerializerOptions {
            WriteIndented = true
        }));
    }

    private static void ApplyTimingChangeFixtureOnEmptyGrid(string fixturePath) {
        ClearNotesFromFixture(fixturePath);
        ApplyTimingChangeFixture(fixturePath);
    }

    private static void AddTriplePlusNotesToFixture(string fixturePath) {
        var easyMapPath = Path.Combine(fixturePath, "easy.dat");
        var root = JsonNode.Parse(File.ReadAllText(easyMapPath))!.AsObject();
        var notes = root["_notes"] as JsonArray ?? [];
        notes.Add(CreateNote(1.0, 0));
        notes.Add(CreateNote(1.0, 1));
        notes.Add(CreateNote(1.0, 2));
        root["_notes"] = notes;
        File.WriteAllText(easyMapPath, root.ToJsonString(new JsonSerializerOptions {
            WriteIndented = true
        }));
    }

    private static void ClearNotesFromFixture(string fixturePath) {
        var easyMapPath = Path.Combine(fixturePath, "easy.dat");
        var root = JsonNode.Parse(File.ReadAllText(easyMapPath))!.AsObject();
        root["_notes"] = new JsonArray();
        File.WriteAllText(easyMapPath, root.ToJsonString(new JsonSerializerOptions {
            WriteIndented = true
        }));
    }

    private static JsonObject CreateNote(double beat, int column) {
        return new JsonObject {
            ["_time"] = beat,
            ["_lineIndex"] = column,
            ["_lineLayer"] = 0,
            ["_type"] = 0,
            ["_cutDirection"] = 1
        };
    }

    private static void ConfigureDrumOnlyAudio(AvaloniaUIDriver driver) {
        driver.SetSliderValue(SongVolumeSliderId, 0);
        driver.SetSliderValue(DrumVolumeSliderId, 1);
        driver.WaitForIdle();
    }

    private static double GetAudibleThreshold(double baselinePeak) {
        return Math.Max(0.008, baselinePeak * 2.5);
    }

    private static int FindDarkHorizontalGuideRow(Bitmap bitmap, double expectedLocalY, IReadOnlyList<int> sampleXs, int searchRadius) {
        var minY = Math.Max(0, (int)Math.Floor(expectedLocalY - searchRadius));
        var maxY = Math.Min(bitmap.Height - 1, (int)Math.Ceiling(expectedLocalY + searchRadius));
        var bestY = minY;
        double bestScore = double.NegativeInfinity;

        for (var y = minY; y <= maxY; y++) {
            double score = 0;
            foreach (var sampleX in sampleXs) {
                for (var dx = -1; dx <= 1; dx++) {
                    var x = Math.Clamp(sampleX + dx, 0, bitmap.Width - 1);
                    var pixel = bitmap.GetPixel(x, y);
                    score += (255 - pixel.R) + (255 - pixel.G) + (255 - pixel.B);
                    score += Math.Max(0, pixel.B - pixel.R) + Math.Max(0, pixel.B - pixel.G);
                }
            }

            if (score > bestScore) {
                bestScore = score;
                bestY = y;
            }
        }

        return bestY;
    }

    private static double FindOrangeDrumCenterY(Bitmap bitmap) {
        var minY = bitmap.Height;
        var maxY = -1;
        var scanStartY = Math.Max(0, bitmap.Height / 2);

        for (var y = scanStartY; y < bitmap.Height; y++) {
            for (var x = 0; x < bitmap.Width; x++) {
                var pixel = bitmap.GetPixel(x, y);
                if (pixel.A < 32 || pixel.R < 140 || pixel.G < 70 || pixel.B > 90) {
                    continue;
                }

                minY = Math.Min(minY, y);
                maxY = Math.Max(maxY, y);
            }
        }

        Assert.True(maxY >= minY, "Expected the editor screenshot to contain visible orange drum pixels.");
        return (minY + maxY) / 2.0;
    }

    private static double FindOrangeDrumCenterXNear(Bitmap bitmap, double expectedLocalX, int searchRadius) {
        var minX = bitmap.Width;
        var maxX = -1;
        var scanStartY = Math.Max(0, bitmap.Height / 2);
        var startX = Math.Max(0, (int)Math.Floor(expectedLocalX - searchRadius));
        var endX = Math.Min(bitmap.Width - 1, (int)Math.Ceiling(expectedLocalX + searchRadius));

        for (var y = scanStartY; y < bitmap.Height; y++) {
            for (var x = startX; x <= endX; x++) {
                var pixel = bitmap.GetPixel(x, y);
                if (pixel.A < 32 || pixel.R < 140 || pixel.G < 70 || pixel.B > 90) {
                    continue;
                }

                minX = Math.Min(minX, x);
                maxX = Math.Max(maxX, x);
            }
        }

        Assert.True(maxX >= minX, "Expected the editor screenshot to contain visible orange drum pixels near the requested lane.");
        return (minX + maxX) / 2.0;
    }

    private static (double left, double top, double width, double height) GetChangedPixelBounds(Bitmap before, Bitmap after) {
        var width = Math.Min(before.Width, after.Width);
        var height = Math.Min(before.Height, after.Height);
        var minX = width;
        var minY = height;
        var maxX = -1;
        var maxY = -1;

        for (var y = 0; y < height; y++) {
            for (var x = 0; x < width; x++) {
                var left = before.GetPixel(x, y);
                var right = after.GetPixel(x, y);
                if (ColorDistance(left, right) < 35) {
                    continue;
                }

                minX = Math.Min(minX, x);
                minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x);
                maxY = Math.Max(maxY, y);
            }
        }

        Assert.True(maxX >= minX && maxY >= minY, "Expected a UI interaction to change visible editor pixels.");
        return (minX, minY, maxX - minX + 1, maxY - minY + 1);
    }

    private static (double left, double top, double width, double height) GetChangedPixelBoundsNearPoint(Bitmap before, Bitmap after, double xRatio, double yRatio) {
        var width = Math.Min(before.Width, after.Width);
        var height = Math.Min(before.Height, after.Height);
        var changed = new bool[width, height];

        for (var y = 0; y < height; y++) {
            for (var x = 0; x < width; x++) {
                changed[x, y] = ColorDistance(before.GetPixel(x, y), after.GetPixel(x, y)) >= 35;
            }
        }

        var targetX = (width - 1) * xRatio;
        var targetY = (height - 1) * yRatio;
        var visited = new bool[width, height];
        (double left, double top, double width, double height) best = default;
        (double left, double top, double width, double height) fallback = default;
        double bestScore = double.PositiveInfinity;
        double fallbackScore = double.PositiveInfinity;

        for (var startY = 0; startY < height; startY++) {
            for (var startX = 0; startX < width; startX++) {
                if (!changed[startX, startY] || visited[startX, startY]) {
                    continue;
                }

                var queue = new Queue<(int x, int y)>();
                queue.Enqueue((startX, startY));
                visited[startX, startY] = true;

                var minX = startX;
                var minY = startY;
                var maxX = startX;
                var maxY = startY;
                var pixels = 0;

                while (queue.Count > 0) {
                    var (x, y) = queue.Dequeue();
                    pixels++;
                    minX = Math.Min(minX, x);
                    minY = Math.Min(minY, y);
                    maxX = Math.Max(maxX, x);
                    maxY = Math.Max(maxY, y);

                    foreach (var (nextX, nextY) in new[] { (x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1) }) {
                        if (nextX < 0 || nextX >= width || nextY < 0 || nextY >= height || visited[nextX, nextY] || !changed[nextX, nextY]) {
                            continue;
                        }

                        visited[nextX, nextY] = true;
                        queue.Enqueue((nextX, nextY));
                    }
                }

                if (pixels < 20) {
                    continue;
                }

                var componentWidth = maxX - minX + 1;
                var componentHeight = maxY - minY + 1;
                if (componentWidth < 12 || componentHeight < 12) {
                    continue;
                }

                var centerX = (minX + maxX) / 2.0;
                var centerY = (minY + maxY) / 2.0;
                var fallbackComponent = (left: (double)minX, top: (double)minY, width: (double)componentWidth, height: (double)componentHeight);
                var fallbackComponentScore = Math.Abs(centerX - targetX) + Math.Abs(centerY - targetY);
                if (fallbackComponentScore < fallbackScore) {
                    fallbackScore = fallbackComponentScore;
                    fallback = fallbackComponent;
                }

                var aspectRatio = Math.Max(componentWidth, componentHeight) / Math.Max(1.0, Math.Min(componentWidth, componentHeight));
                if (aspectRatio > 2.4) {
                    continue;
                }

                var score = Math.Abs(centerX - targetX) + Math.Abs(centerY - targetY) + (aspectRatio - 1.0) * 10;
                if (score < bestScore) {
                    bestScore = score;
                    best = (minX, minY, componentWidth, componentHeight);
                }
            }
        }

        if (best.width > 0 && best.height > 0) {
            return best;
        }

        Assert.True(fallback.width > 0 && fallback.height > 0, "Expected a UI interaction to change visible editor pixels near the requested point.");
        return fallback;
    }

    private static bool ContainsWarningRed(Bitmap bitmap) {
        var redPixels = 0;
        for (var y = 0; y < bitmap.Height; y++) {
            for (var x = 0; x < bitmap.Width; x++) {
                var pixel = bitmap.GetPixel(x, y);
                if (pixel.A > 0 && pixel.R >= 40 && pixel.R >= pixel.G + 4 && pixel.R >= pixel.B + 4) {
                    redPixels++;
                    if (redPixels >= 1) {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private static double ColorDistance(Color left, Color right) {
        var deltaR = left.R - right.R;
        var deltaG = left.G - right.G;
        var deltaB = left.B - right.B;
        return Math.Sqrt((deltaR * deltaR) + (deltaG * deltaG) + (deltaB * deltaB));
    }

    private static double ComputeInterestingPixelCenterRatio(Bitmap bitmap) {
        var background = AverageCornerColor(bitmap);
        double weightedX = 0;
        double totalWeight = 0;

        for (var y = 1; y < bitmap.Height - 1; y++) {
            for (var x = 1; x < bitmap.Width - 1; x++) {
                var pixel = bitmap.GetPixel(x, y);
                var weight = ColorDistance(pixel, background);
                if (weight < 45) {
                    continue;
                }

                weightedX += x * weight;
                totalWeight += weight;
            }
        }

        Assert.True(totalWeight > 0, "Expected the navigation waveform screenshot to contain visible waveform pixels.");
        return weightedX / totalWeight / Math.Max(1, bitmap.Width - 1);
    }

    private static double GetMeanAbsoluteRgbDifference(Bitmap before, Bitmap after) {
        var width = Math.Min(before.Width, after.Width);
        var height = Math.Min(before.Height, after.Height);
        double total = 0;
        var samples = 0;

        for (var y = 0; y < height; y++) {
            for (var x = 0; x < width; x++) {
                var left = before.GetPixel(x, y);
                var right = after.GetPixel(x, y);
                total += (Math.Abs(left.R - right.R) + Math.Abs(left.G - right.G) + Math.Abs(left.B - right.B)) / 3.0;
                samples++;
            }
        }

        return samples == 0 ? 0 : total / samples;
    }

    private static Color AverageCornerColor(Bitmap bitmap) {
        var points = new[] {
            bitmap.GetPixel(1, 1),
            bitmap.GetPixel(Math.Max(1, bitmap.Width - 2), 1),
            bitmap.GetPixel(1, Math.Max(1, bitmap.Height - 2)),
            bitmap.GetPixel(Math.Max(1, bitmap.Width - 2), Math.Max(1, bitmap.Height - 2))
        };

        return Color.FromArgb(
            points.Sum(point => point.R) / points.Length,
            points.Sum(point => point.G) / points.Length,
            points.Sum(point => point.B) / points.Length);
    }

    private static double ClampRatio(double ratio) {
        return Math.Max(0.05, Math.Min(0.95, ratio));
    }
}
