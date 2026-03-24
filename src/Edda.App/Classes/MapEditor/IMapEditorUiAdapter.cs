using Edda.Classes.MapEditorNS.NoteNS;
using Edda.Classes.MapEditorNS.Stats;
using System.Collections.Generic;

#nullable enable

public interface IMapEditorUiAdapter {
    string GetUserSetting(string key);
    bool IsShiftKeyDown { get; }

    void UpdateDifficultyButtons();
    void DrawEditorGrid(bool redrawWaveform = true);
    void RefreshBPMChanges();
    void RefreshDiscordPresence();
    void SetMapStats(MapStats stats);

    void DrawNotes(IEnumerable<Note> notes);
    void DrawNavNotes(IEnumerable<Note> notes);
    void UndrawNotes(IEnumerable<Note> notes);
    void UndrawNavNotes(IEnumerable<Note> notes);
    void HighlightNotes(IEnumerable<Note> notes);
    void HighlightNavNotes(IEnumerable<Note> notes);
    void HighlightAllNotes();
    void HighlightAllNavNotes();
    void UnhighlightNotes(IEnumerable<Note> notes);
    void UnhighlightNavNotes(IEnumerable<Note> notes);
    void UnhighlightAllNotes();
    void UnhighlightAllNavNotes();

    void SetClipboardText(string text);
    string? GetClipboardText();
}
