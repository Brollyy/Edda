using NAudio.Vorbis;
using System;
using System.IO;

namespace Edda;

internal sealed class NAudioAudioFileServices : ISongAudioMetadataReader, ISpectrogramAudioReader {
    public TimeSpan GetDuration(string songFilePath) {
        try {
            using var reader = new VorbisWaveReader(songFilePath);
            return reader.TotalTime;
        } catch (Exception ex) {
            throw new InvalidDataException("The .ogg file is corrupted.", ex);
        }
    }

    public SongAudioTags ReadTags(string songFilePath) {
        using var tagReader = new VorbisSampleProvider(File.OpenRead(songFilePath), closeOnDispose: true);
        return new SongAudioTags(tagReader.Tags.Artist, tagReader.Tags.Title);
    }

    public SpectrogramAudioSamples ReadSamples(string filePath) {
        using var reader = new VorbisWaveReader(filePath);
        var bytesPerSample = Math.Max(1, reader.WaveFormat.BitsPerSample / 8);
        var sampleCount = reader.Length / bytesPerSample;
        var audioBuffer = new float[sampleCount];
        reader.Read(audioBuffer, 0, (int)sampleCount);
        return new SpectrogramAudioSamples(reader.WaveFormat.SampleRate, reader.WaveFormat.BitsPerSample, reader.Length, audioBuffer);
    }
}