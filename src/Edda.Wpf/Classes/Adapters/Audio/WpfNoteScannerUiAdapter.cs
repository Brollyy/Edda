using Edda.Classes.MapEditorNS.NoteNS;
using System;

namespace Edda {
    public class WpfNoteScannerUiAdapter : INoteScannerUiAdapter {
        readonly MainWindow mainWindow;

        public WpfNoteScannerUiAdapter(MainWindow mainWindow) {
            this.mainWindow = mainWindow;
        }

        public void InvokeOnUiThread(Action action) {
            mainWindow.Dispatcher.Invoke(action);
        }

        public void AnimateDrum(int column) {
            mainWindow.AnimateDrum(column);
        }

        public void AnimateNote(Note note) {
            mainWindow.AnimateNote(note);
        }
    }
}
