using System;
using System.Collections.Generic;
using System.IO;
using NAudio.Vorbis;
using NAudio.Wave;
using OpenTK.Audio.OpenAL;

namespace Edda.Avalonia.Services;

internal sealed class OpenAlAudioEngine : IDisposable {
    internal static readonly object SyncRoot = new();
    static ALContext activeContext;
    readonly ALDevice device;
    readonly ALContext context;

    public OpenAlAudioEngine() {
        device = ALC.OpenDevice(null);
        if (device == ALDevice.Null) {
            throw new InvalidOperationException("OpenAL could not open the default playback device.");
        }

        context = ALC.CreateContext(device, (int[])null!);
        if (context == ALContext.Null || !ALC.MakeContextCurrent(context)) {
            ALC.CloseDevice(device);
            throw new InvalidOperationException("OpenAL could not create a playback context.");
        }

        activeContext = context;
    }

    public OpenAlBuffer LoadVorbisBuffer(string filePath) {
        using var reader = new VorbisWaveReader(filePath);
        var samples = ReadAllSamples(reader, out var sampleRate, out var channels);
        return CreateBuffer(samples, sampleRate, channels);
    }

    public OpenAlBuffer LoadWaveBuffer(string filePath) {
        using var reader = new AudioFileReader(filePath);
        var samples = ReadAllSamples(reader, out var sampleRate, out var channels);
        return CreateBuffer(samples, sampleRate, channels);
    }

    public OpenAlSource CreateSource() {
        lock (SyncRoot) {
            EnsureCurrentContext();
            return new OpenAlSource(AL.GenSource());
        }
    }

    public OpenAlStreamingSource CreateStreamingSource() {
        lock (SyncRoot) {
            EnsureCurrentContext();
            return new OpenAlStreamingSource(AL.GenSource());
        }
    }

    public void Dispose() {
        lock (SyncRoot) {
            ALC.MakeContextCurrent(ALContext.Null);
            if (activeContext == context) {
                activeContext = ALContext.Null;
            }
            if (context != ALContext.Null) {
                ALC.DestroyContext(context);
            }
            if (device != ALDevice.Null) {
                ALC.CloseDevice(device);
            }
        }
    }

    static OpenAlBuffer CreateBuffer(short[] samples, int sampleRate, int channels) {
        lock (SyncRoot) {
            EnsureCurrentContext();
            var buffer = AL.GenBuffer();
            var format = channels == 1 ? ALFormat.Mono16 : ALFormat.Stereo16;
            AL.BufferData(buffer, format, samples, sampleRate);
            return new OpenAlBuffer(buffer);
        }
    }

    internal static void EnsureCurrentContext() {
        if (activeContext != ALContext.Null) {
            ALC.MakeContextCurrent(activeContext);
        }
    }

    static short[] ReadAllSamples(ISampleProvider sampleProvider, out int sampleRate, out int channels) {
        sampleRate = sampleProvider.WaveFormat.SampleRate;
        channels = Math.Clamp(sampleProvider.WaveFormat.Channels, 1, 2);
        var output = new List<short>();
        var buffer = new float[sampleRate * channels];
        int read;
        while ((read = sampleProvider.Read(buffer, 0, buffer.Length)) > 0) {
            for (var i = 0; i < read; i++) {
                var sample = Math.Clamp(buffer[i], -1f, 1f);
                output.Add((short)Math.Round(sample * short.MaxValue));
            }
        }

        return output.ToArray();
    }
}

internal sealed class OpenAlBuffer : IDisposable {
    public OpenAlBuffer(int id) {
        Id = id;
    }

    public int Id { get; }

    public void Dispose() {
        lock (OpenAlAudioEngine.SyncRoot) {
            OpenAlAudioEngine.EnsureCurrentContext();
            AL.DeleteBuffer(Id);
        }
    }
}

internal sealed class OpenAlSource : IDisposable {
    public OpenAlSource(int id) {
        Id = id;
    }

    public int Id { get; }

    public bool IsPlaying {
        get {
            lock (OpenAlAudioEngine.SyncRoot) {
                OpenAlAudioEngine.EnsureCurrentContext();
                return AL.GetSource(Id, ALGetSourcei.SourceState) == (int)ALSourceState.Playing;
            }
        }
    }

    public void Play(OpenAlBuffer buffer, double volume = 1, double pitch = 1, float pan = 0, double startSeconds = 0) {
        lock (OpenAlAudioEngine.SyncRoot) {
            OpenAlAudioEngine.EnsureCurrentContext();
            StopCore();
            AL.Source(Id, ALSourcei.Buffer, buffer.Id);
            SetVolumeCore(volume);
            AL.Source(Id, ALSourcef.Pitch, (float)Math.Clamp(pitch, 0.1, 2.0));
            AL.Source(Id, ALSourcef.RolloffFactor, 0);
            AL.Source(Id, ALSource3f.Position, Math.Clamp(pan, -1, 1), 0, -1);
            if (startSeconds > 0) {
                AL.Source(Id, ALSourcef.SecOffset, (float)startSeconds);
            }
            AL.SourcePlay(Id);
        }
    }

    public void Stop() {
        lock (OpenAlAudioEngine.SyncRoot) {
            OpenAlAudioEngine.EnsureCurrentContext();
            StopCore();
        }
    }

    public void SetVolume(double volume) {
        lock (OpenAlAudioEngine.SyncRoot) {
            OpenAlAudioEngine.EnsureCurrentContext();
            SetVolumeCore(volume);
        }
    }

    void SetVolumeCore(double volume) {
        AL.Source(Id, ALSourcef.Gain, (float)Math.Clamp(volume, 0, 4));
    }

    void StopCore() {
        AL.SourceStop(Id);
    }

    public void Dispose() {
        lock (OpenAlAudioEngine.SyncRoot) {
            OpenAlAudioEngine.EnsureCurrentContext();
            StopCore();
            AL.DeleteSource(Id);
        }
    }
}
