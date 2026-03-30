using Edda.Const;
using NAudio.CoreAudioApi;
using NAudio.Vorbis;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using SoundTouch.Net.NAudioSupport;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Edda {
    public class SongPreviewController : IDisposable {
        readonly ISongPreviewUiAdapter uiAdapter;

        // audio playback state
        SampleChannel previewChannel;
        VorbisWaveReader previewStream;
        SoundTouchWaveStream previewTempoStream;
        WasapiOut previewPlayer;
        CancellationTokenSource previewPlaybackCancellationTokenSource;
        bool previewIsPlaying;

        // constructor
        public SongPreviewController(ISongPreviewUiAdapter uiAdapter) {
            this.uiAdapter = uiAdapter;
        }

        public void Dispose() {
            // Clear the most memory-heavy components
            UnloadPreviewPlayer();
            UnloadPreviewStream();
            UnloadPreviewTempoStream();

            previewPlaybackCancellationTokenSource?.Cancel();
            previewPlaybackCancellationTokenSource?.Dispose();
            previewPlaybackCancellationTokenSource = null;

            previewIsPlaying = false;
            previewChannel = null;
        }

        private void UnloadPreviewPlayer() {
            var oldPreviewPlayer = previewPlayer;
            previewPlayer = null;
            oldPreviewPlayer?.Stop();
            oldPreviewPlayer?.Dispose();
        }

        private void UnloadPreviewStream() {
            var oldPreviewStream = previewStream;
            previewStream = null;
            oldPreviewStream?.Dispose();
        }

        private void UnloadPreviewTempoStream() {
            var oldPreviewTempoStream = previewTempoStream;
            previewTempoStream = null;
            oldPreviewTempoStream?.Dispose();
        }

        public void LoadPreview(MapEditor mapEditor) {
            var previewPath = Path.Combine(mapEditor.mapFolder, BeatmapDefaults.PreviewFilename);
            try {
                previewStream = new VorbisWaveReader(previewPath);
                previewTempoStream = new SoundTouchWaveStream(previewStream);
                previewChannel = new SampleChannel(previewTempoStream);
                UpdateVolume();
                InitPreviewPlayer();
                EnablePreviewButton();
            } catch (Exception) {
                UnloadPreview();
            }
        }

        private void InitPreviewPlayer() {
            var device = uiAdapter.GetPlaybackDevice();
            if (device != null) {
                previewPlayer = new WasapiOut(device, AudioClientShareMode.Shared, true, Audio.WASAPILatencyTarget);
                previewPlayer.Init(previewChannel);

                // subscribe to playbackstopped
                previewPlayer.PlaybackStopped += (sender, args) => { StopPreview(); };
            } else {
                previewPlayer = null;
            }
        }

        public void UnloadPreview() {
            UnloadPreviewPlayer();
            UnloadPreviewStream();
            UnloadPreviewTempoStream();
            previewChannel = null;
            previewIsPlaying = false;
            uiAdapter.SetPreviewPlaying(false);
            DisablePreviewButton();
        }

        private void PlayPreview() {
            previewIsPlaying = true;
            uiAdapter.SetPreviewPlaying(true);

            // set seek position for preview on start
            previewStream.CurrentTime = TimeSpan.Zero;

            var editorAudioLatency = uiAdapter.GetEditorAudioLatency();

            // play the preview
            if (editorAudioLatency == 0 || previewTempoStream.CurrentTime > new TimeSpan(0, 0, 0, 0, editorAudioLatency)) {
                previewTempoStream.CurrentTime = previewTempoStream.CurrentTime - new TimeSpan(0, 0, 0, 0, editorAudioLatency);
                previewPlayer?.Play();
            } else {
                previewTempoStream.CurrentTime = new TimeSpan(0);
                var oldPreviewPlaybackCancellationTokenSource = previewPlaybackCancellationTokenSource;
                previewPlaybackCancellationTokenSource = new();
                oldPreviewPlaybackCancellationTokenSource?.Dispose();
                Task.Delay(new TimeSpan(0, 0, 0, 0, editorAudioLatency)).ContinueWith(o => {
                    if (previewPlaybackCancellationTokenSource != null && !previewPlaybackCancellationTokenSource.IsCancellationRequested) {
                        previewPlayer?.Play();
                    }
                });
            }
        }

        public void StopPreview() {
            if (!previewIsPlaying) {
                return;
            }
            previewIsPlaying = false;
            uiAdapter.SetPreviewPlaying(false);

            previewPlayer?.Stop();
        }

        public void TogglePreview() {
            if (!previewIsPlaying) {
                PlayPreview();
            } else {
                StopPreview();
            }
        }

        public void Restart(MapEditor mapEditor, bool songIsPlaying) {
            UnloadPreview();
            LoadPreview(mapEditor);
            if (songIsPlaying) {
                DisablePreviewButton();
            }
        }

        public void UpdateVolume() {
            if (previewChannel != null) {
                previewChannel.Volume = uiAdapter.GetSongVolume();
            }
        }

        public void EnablePreviewButton() {
            if (previewPlayer != null) {
                uiAdapter.SetPreviewButtonEnabled(true);
            }
        }

        public void DisablePreviewButton() {
            uiAdapter.SetPreviewButtonEnabled(false);
        }
    }
}