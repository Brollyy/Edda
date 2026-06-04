using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Vorbis;
using NAudio.Wave;
using OpenTK.Audio.OpenAL;
using SoundTouch.Net.NAudioSupport;

namespace Edda.Avalonia.Services;

internal sealed class OpenAlStreamingSource : IDisposable {
    const int BufferCount = 4;
    const int BufferDurationMilliseconds = 80;
    const int StreamPumpDelayMilliseconds = 15;

    readonly int id;
    readonly int[] buffers;
    CancellationTokenSource? playbackCancellation;
    Task? playbackTask;
    WaveStream? activeStream;
    ISampleProvider? activeSampleProvider;
    float[] sampleBuffer = [];
    bool disposed;

    public OpenAlStreamingSource(int id) {
        this.id = id;
        buffers = AL.GenBuffers(BufferCount);
    }

    public bool PlayVorbis(string filePath, double tempo, double volume, double startSeconds) {
        if (disposed || string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)) {
            return false;
        }

        Stop();

        var sourceStream = new VorbisWaveReader(filePath);
        var tempoStream = new SoundTouchWaveStream(sourceStream) {
            Tempo = Math.Clamp(tempo, 0.1, 2.0)
        };
        tempoStream.CurrentTime = TimeSpan.FromSeconds(Math.Max(0, startSeconds));

        activeStream = tempoStream;
        activeSampleProvider = tempoStream.ToSampleProvider();
        sampleBuffer = new float[Math.Max(1024, activeSampleProvider.WaveFormat.SampleRate * BufferDurationMilliseconds / 1000) * activeSampleProvider.WaveFormat.Channels];

        playbackCancellation = new CancellationTokenSource();
        var queuedBuffers = QueueInitialBuffers(volume);
        if (queuedBuffers == 0) {
            Stop();
            return false;
        }

        playbackTask = Task.Run(() => PumpPlayback(playbackCancellation.Token));
        return true;
    }

    public void Stop() {
        var cancellation = playbackCancellation;
        var task = playbackTask;
        playbackCancellation = null;
        playbackTask = null;

        if (cancellation != null) {
            cancellation.Cancel();
            try {
                task?.Wait(TimeSpan.FromMilliseconds(250));
            } catch (AggregateException) {
            }
            cancellation.Dispose();
        }

        lock (OpenAlAudioEngine.SyncRoot) {
            OpenAlAudioEngine.EnsureCurrentContext();
            AL.SourceStop(id);
            UnqueueAllBuffers();
        }

        activeSampleProvider = null;
        activeStream?.Dispose();
        activeStream = null;
    }

    public void SetVolume(double volume) {
        lock (OpenAlAudioEngine.SyncRoot) {
            OpenAlAudioEngine.EnsureCurrentContext();
            AL.Source(id, ALSourcef.Gain, (float)Math.Clamp(volume, 0, 4));
        }
    }

    public void Dispose() {
        if (disposed) {
            return;
        }

        disposed = true;
        Stop();
        lock (OpenAlAudioEngine.SyncRoot) {
            OpenAlAudioEngine.EnsureCurrentContext();
            AL.DeleteBuffers(buffers);
            AL.DeleteSource(id);
        }
    }

    int QueueInitialBuffers(double volume) {
        lock (OpenAlAudioEngine.SyncRoot) {
            OpenAlAudioEngine.EnsureCurrentContext();
            AL.Source(id, ALSourcef.Gain, (float)Math.Clamp(volume, 0, 4));
            AL.Source(id, ALSourcef.Pitch, 1f);
            AL.Source(id, ALSourcef.RolloffFactor, 0);
            AL.Source(id, ALSource3f.Position, 0, 0, -1);

            var queued = 0;
            foreach (var buffer in buffers) {
                if (!FillBuffer(buffer)) {
                    break;
                }

                AL.SourceQueueBuffer(id, buffer);
                queued++;
            }

            if (queued > 0) {
                AL.SourcePlay(id);
            }

            return queued;
        }
    }

    void PumpPlayback(CancellationToken cancellationToken) {
        while (!cancellationToken.IsCancellationRequested) {
            var reachedEnd = false;
            lock (OpenAlAudioEngine.SyncRoot) {
                OpenAlAudioEngine.EnsureCurrentContext();
                var processed = AL.GetSource(id, ALGetSourcei.BuffersProcessed);
                while (processed-- > 0) {
                    var buffer = AL.SourceUnqueueBuffer(id);
                    if (FillBuffer(buffer)) {
                        AL.SourceQueueBuffer(id, buffer);
                    } else {
                        reachedEnd = true;
                    }
                }

                var queued = AL.GetSource(id, ALGetSourcei.BuffersQueued);
                if (queued == 0 && reachedEnd) {
                    return;
                }

                if (queued > 0 && AL.GetSource(id, ALGetSourcei.SourceState) != (int)ALSourceState.Playing) {
                    AL.SourcePlay(id);
                }
            }

            Thread.Sleep(StreamPumpDelayMilliseconds);
        }
    }

    bool FillBuffer(int buffer) {
        if (activeSampleProvider == null) {
            return false;
        }

        var read = activeSampleProvider.Read(sampleBuffer, 0, sampleBuffer.Length);
        if (read <= 0) {
            return false;
        }

        var samples = new short[read];
        for (var i = 0; i < read; i++) {
            samples[i] = (short)Math.Round(Math.Clamp(sampleBuffer[i], -1f, 1f) * short.MaxValue);
        }

        var channels = Math.Clamp(activeSampleProvider.WaveFormat.Channels, 1, 2);
        var format = channels == 1 ? ALFormat.Mono16 : ALFormat.Stereo16;
        AL.BufferData(buffer, format, samples, activeSampleProvider.WaveFormat.SampleRate);
        return true;
    }

    void UnqueueAllBuffers() {
        var queued = AL.GetSource(id, ALGetSourcei.BuffersQueued);
        while (queued-- > 0) {
            AL.SourceUnqueueBuffer(id);
        }
    }
}
