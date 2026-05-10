using Edda.Classes.MapEditorNS.NoteNS;
using System.Collections.Generic;

public class NoteScanner : AudioScanner {
    readonly INoteScannerUiAdapter uiAdapter;
    List<Note> notesPlayed;
    public bool playedLateNote { get; set; }
    public NoteScanner(INoteScannerUiAdapter uiAdapter, IAudioCuePlayer parallelAudioPlayer, double tempo) : base(parallelAudioPlayer, tempo) {
        this.uiAdapter = uiAdapter;
        this.playedLateNote = false;
    }

    public override void Dispose() {
        base.Dispose();
        notesPlayed = null;
    }

    protected override void OnNoteScanBegin() {
        notesPlayed = new List<Note>();
    }
    protected override void OnNoteScanLateHit(Note n) {
        notesPlayed.Add(n);
        playedLateNote = true;
    }
    protected override void OnNoteScanHit(Note n) {
        notesPlayed.Add(n);
    }
    protected override void OnNoteScanFinish() {
        foreach (Note n in notesPlayed) {
            uiAdapter.InvokeOnUiThread(() => {
                uiAdapter.AnimateDrum(n.col);
                uiAdapter.AnimateNote(n);
            });
        }
    }
}
