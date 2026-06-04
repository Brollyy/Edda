using System;
using System.IO;
using Edda.Const;

namespace Edda.Avalonia.Services;

internal sealed class AvaloniaOpenAlAudioCuePlayer : IAudioCuePlayer {
    const int NumChannels = 4;
    const double CueGain = 2.0;
    readonly OpenAlAudioEngine audioEngine;
    readonly int streams;
    readonly int uniqueSamples;
    readonly string basePath;
    readonly OpenAlBuffer[] sampleBuffers;
    readonly OpenAlSource[] sources;
    double volume;
    int lastPlayedStream;

    public AvaloniaOpenAlAudioCuePlayer(OpenAlAudioEngine audioEngine, string basePath, int streams, bool isEnabled, bool isPanned, double defaultVolume) {
        this.audioEngine = audioEngine;
        this.basePath = basePath;
        this.streams = Math.Max(1, streams);
        this.isEnabled = isEnabled;
        this.isPanned = isPanned;
        volume = Math.Clamp(defaultVolume, 0, 1);

        while (File.Exists(GetFilePath(basePath, uniqueSamples + 1))) {
            uniqueSamples++;
        }
        if (uniqueSamples < 1) {
            throw new FileNotFoundException($"Couldn't find the file {GetFilePath(basePath, uniqueSamples + 1)}");
        }

        sampleBuffers = new OpenAlBuffer[uniqueSamples];
        for (var i = 0; i < uniqueSamples; i++) {
            sampleBuffers[i] = audioEngine.LoadWaveBuffer(GetFilePath(basePath, i + 1));
        }

        sources = new OpenAlSource[this.streams];
        for (var i = 0; i < sources.Length; i++) {
            sources[i] = audioEngine.CreateSource();
        }
    }

    public bool isEnabled { get; set; }
    public bool isPanned { get; set; }

    public bool Play() {
        return Play(0);
    }

    public bool Play(int channel) {
        if (!isEnabled) {
            return true;
        }

        var source = FindAvailableSource();
        if (source == null) {
            return false;
        }

        lastPlayedStream++;
        var sampleIndex = lastPlayedStream % NumChannels % uniqueSamples;
        source.Play(sampleBuffers[sampleIndex], volume: GetEffectiveVolume(), pan: ResolvePan(channel));
        return true;
    }

    public void ChangeVolume(double vol) {
        volume = Math.Clamp(Math.Abs(vol), 0, 1);
        foreach (var source in sources) {
            source.SetVolume(GetEffectiveVolume());
        }
    }

    public void Dispose() {
        foreach (var source in sources) {
            source.Dispose();
        }
        foreach (var sampleBuffer in sampleBuffers) {
            sampleBuffer.Dispose();
        }
    }

    OpenAlSource? FindAvailableSource() {
        foreach (var source in sources) {
            if (!source.IsPlaying) {
                return source;
            }
        }

        return null;
    }

    float ResolvePan(int channel) {
        if (!isPanned || basePath == "mmatick") {
            return 0;
        }

        return basePath == "bassdrum"
            ? -1
            : (float)(channel * 2.0 * Audio.MaxPanDistance / (NumChannels - 1) - Audio.MaxPanDistance);
    }

    double GetEffectiveVolume() {
        return volume * CueGain;
    }

    static string GetFilePath(string basePath, int sampleNumber) {
        var relativePath = Path.Combine("Resources", $"{basePath}{sampleNumber}.wav");
        var appBasePath = Path.Combine(AppContext.BaseDirectory, relativePath);
        if (File.Exists(appBasePath)) {
            return appBasePath;
        }

        var workingDirectoryPath = Path.GetFullPath(relativePath);
        if (File.Exists(workingDirectoryPath)) {
            return workingDirectoryPath;
        }

        return appBasePath;
    }
}
