namespace Edda {
    public interface ISongPreviewUiAdapter {
        float GetSongVolume();
        int GetEditorAudioLatency();
        void SetPreviewPlaying(bool isPlaying);
        void SetPreviewButtonEnabled(bool isEnabled);
    }
}
