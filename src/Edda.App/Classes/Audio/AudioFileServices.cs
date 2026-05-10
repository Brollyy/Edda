using System;

namespace Edda;

#nullable enable

public sealed record SongAudioTags(string? Artist, string? Title);

public sealed record SpectrogramAudioSamples(int SampleRate, int BitsPerSample, long SourceLengthBytes, float[] Samples);

public interface ISongAudioMetadataReader {
    TimeSpan GetDuration(string songFilePath);
    SongAudioTags ReadTags(string songFilePath);
}

public interface ISpectrogramAudioReader {
    SpectrogramAudioSamples ReadSamples(string filePath);
}
