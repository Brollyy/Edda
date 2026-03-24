using Edda.Classes.MapEditorNS.NoteNS;
using System;

public interface INoteScannerUiAdapter {
    void InvokeOnUiThread(Action action);
    void AnimateDrum(int column);
    void AnimateNote(Note note);
}
