using System;
using System.IO;
using Edda.Classes.MapEditorNS;
using Edda.Const;

namespace Edda.Avalonia.Services;

internal sealed class AvaloniaOpenAlSongPreviewController : IDisposable {
    readonly OpenAlAudioEngine audioEngine;
    readonly ISongPreviewUiAdapter uiAdapter;
    string? previewPath;
    OpenAlBuffer? previewBuffer;
    OpenAlSource? previewSource;
    bool previewIsPlaying;

    public AvaloniaOpenAlSongPreviewController(OpenAlAudioEngine audioEngine, ISongPreviewUiAdapter uiAdapter) {
        this.audioEngine = audioEngine;
        this.uiAdapter = uiAdapter;
    }

    public void Dispose() {
        UnloadPreview();
    }

    public void LoadPreview(MapEditor mapEditor) {
        previewPath = Path.Combine(mapEditor.mapFolder, BeatmapDefaults.PreviewFilename);
        if (File.Exists(previewPath)) {
            previewBuffer?.Dispose();
            previewBuffer = audioEngine.LoadVorbisBuffer(previewPath);
            previewSource ??= audioEngine.CreateSource();
            EnablePreviewButton();
        } else {
            UnloadPreview();
        }
    }

    public void UnloadPreview() {
        StopPreview();
        previewPath = null;
        previewBuffer?.Dispose();
        previewBuffer = null;
        previewSource?.Dispose();
        previewSource = null;
        DisablePreviewButton();
    }

    public void Restart(MapEditor mapEditor, bool songIsPlaying) {
        UnloadPreview();
        LoadPreview(mapEditor);
        if (songIsPlaying) {
            DisablePreviewButton();
        }
    }

    public void TogglePreview() {
        if (previewIsPlaying) {
            StopPreview();
        } else {
            PlayPreview();
        }
    }

    public void StopPreview() {
        if (!previewIsPlaying && previewSource == null) {
            return;
        }

        previewIsPlaying = false;
        uiAdapter.SetPreviewPlaying(false);
        previewSource?.Stop();
    }

    public void UpdateVolume() {
        if (previewIsPlaying) {
            StopPreview();
            PlayPreview();
        }
    }

    public void EnablePreviewButton() {
        if (!string.IsNullOrWhiteSpace(previewPath) && File.Exists(previewPath)) {
            uiAdapter.SetPreviewButtonEnabled(true);
        }
    }

    public void DisablePreviewButton() {
        uiAdapter.SetPreviewButtonEnabled(false);
    }

    void PlayPreview() {
        if (string.IsNullOrWhiteSpace(previewPath) || !File.Exists(previewPath) || previewBuffer == null) {
            DisablePreviewButton();
            return;
        }

        var editorAudioLatency = Math.Max(0, -uiAdapter.GetEditorAudioLatency());
        previewSource ??= audioEngine.CreateSource();
        if (previewSource == null) {
            DisablePreviewButton();
            return;
        }

        previewSource.Play(previewBuffer, volume: uiAdapter.GetSongVolume(), startSeconds: editorAudioLatency / 1000.0);
        previewIsPlaying = true;
        uiAdapter.SetPreviewPlaying(true);
    }
}
