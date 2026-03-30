using NAudio.CoreAudioApi;

namespace Edda {
    public class WpfSongPreviewUiAdapter : ISongPreviewUiAdapter {
        readonly MainWindow mainWindow;

        public WpfSongPreviewUiAdapter(MainWindow mainWindow) {
            this.mainWindow = mainWindow;
        }

        public MMDevice GetPlaybackDevice() {
            return mainWindow.playbackDevice;
        }

        public float GetSongVolume() {
            return (float)mainWindow.sliderSongVol.Value;
        }

        public int GetEditorAudioLatency() {
            return mainWindow.editorAudioLatency;
        }

        public void SetPreviewPlaying(bool isPlaying) {
            mainWindow.btnPlayPreview.Tag = isPlaying ? 1 : 0;
            mainWindow.imgPreviewButton.Source = Helper.BitmapGenerator(isPlaying ? "stopButton.png" : "playButton.png");
        }

        public void SetPreviewButtonEnabled(bool isEnabled) {
            mainWindow.btnPlayPreview.IsEnabled = isEnabled;
            mainWindow.imgPreviewButton.Opacity = isEnabled ? 1 : 0.5;
        }
    }
}