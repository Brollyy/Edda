using NAudio.CoreAudioApi;

namespace Edda {
    public interface ISongPreviewUiAdapter {
        MMDevice GetPlaybackDevice();
        float GetSongVolume();
        int GetEditorAudioLatency();
        void SetPreviewPlaying(bool isPlaying);
        void SetPreviewButtonEnabled(bool isEnabled);
    }
}