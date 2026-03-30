using Edda;
using Edda.Classes.MapEditorNS.NoteNS;
using Edda.Classes.MapEditorNS.Stats;
using System.Collections.Generic;
using System.Windows;

#nullable enable

internal sealed class WpfMapEditorUiAdapter : IMapEditorUiAdapter {
    readonly MainWindow mainWindow;

    EditorGridController GridController => mainWindow.gridController;

    public WpfMapEditorUiAdapter(MainWindow mainWindow) {
        this.mainWindow = mainWindow;
    }

    public string GetUserSetting(string key) {
        return mainWindow.GetUserSetting(key);
    }

    public bool IsShiftKeyDown => mainWindow.shiftKeyDown;

    public void UpdateDifficultyButtons() {
        mainWindow.UpdateDifficultyButtons();
    }

    public void DrawEditorGrid(bool redrawWaveform = true) {
        mainWindow.DrawEditorGrid(redrawWaveform);
    }

    public void RefreshBPMChanges() {
        mainWindow.RefreshBPMChanges();
    }

    public void RefreshDiscordPresence() {
        mainWindow.RefreshDiscordPresence();
    }

    public void SetMapStats(MapStats stats) {
        mainWindow.SetMapStats(stats);
    }

    public void DrawNotes(IEnumerable<Note> notes) {
        GridController.DrawNotes(notes);
    }

    public void DrawNavNotes(IEnumerable<Note> notes) {
        GridController.DrawNavNotes(notes);
    }

    public void UndrawNotes(IEnumerable<Note> notes) {
        GridController.UndrawNotes(notes);
    }

    public void UndrawNavNotes(IEnumerable<Note> notes) {
        GridController.UndrawNavNotes(notes);
    }

    public void HighlightNotes(IEnumerable<Note> notes) {
        GridController.HighlightNotes(notes);
    }

    public void HighlightNavNotes(IEnumerable<Note> notes) {
        GridController.HighlightNavNotes(notes);
    }

    public void HighlightAllNotes() {
        GridController.HighlightAllNotes();
    }

    public void HighlightAllNavNotes() {
        GridController.HighlightAllNavNotes();
    }

    public void UnhighlightNotes(IEnumerable<Note> notes) {
        GridController.UnhighlightNotes(notes);
    }

    public void UnhighlightNavNotes(IEnumerable<Note> notes) {
        GridController.UnhighlightNavNotes(notes);
    }

    public void UnhighlightAllNotes() {
        GridController.UnhighlightAllNotes();
    }

    public void UnhighlightAllNavNotes() {
        GridController.UnhighlightAllNavNotes();
    }

    public void SetClipboardText(string text) {
        Clipboard.SetText(text);
    }

    public string? GetClipboardText() {
        return Clipboard.ContainsText() ? Clipboard.GetText() : null;
    }
}