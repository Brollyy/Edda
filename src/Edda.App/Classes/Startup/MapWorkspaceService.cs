using System;
using System.IO;
using Edda.Const;
using NAudio.Vorbis;

namespace Edda.Startup;

#nullable enable

public sealed class MapWorkspaceService {
    public MapDocumentSummary CreateNewMap(string mapFolder, string songFilePath) {
        if (string.IsNullOrWhiteSpace(mapFolder)) {
            throw new ArgumentException("A target map folder is required.", nameof(mapFolder));
        }

        if (string.IsNullOrWhiteSpace(songFilePath)) {
            throw new ArgumentException("A source song path is required.", nameof(songFilePath));
        }

        Directory.CreateDirectory(mapFolder);

        using var vorbisStream = TryOpenVorbis(songFilePath);
        if (vorbisStream.TotalTime.TotalHours >= 1) {
            throw new InvalidDataException("Songs over 1 hour in duration are not supported.");
        }

        var beatmap = new RagnarockMap(mapFolder, makeNew: true);
        var songFileName = Helper.SanitiseSongFileName(songFilePath);
        var destinationSongPath = Path.Combine(mapFolder, songFileName);

        beatmap.SetValue("_songApproximativeDuration", (int)vorbisStream.TotalTime.TotalSeconds + 1);
        beatmap.SetValue("_songFilename", songFileName);

        CopyIfNeeded(songFilePath, destinationSongPath);
        ApplyVorbisMetadata(beatmap, destinationSongPath);
        beatmap.SaveToFile();

        return OpenMap(mapFolder);
    }

    public MapDocumentSummary ImportMap(string mapFolder, string simfilePath) {
        if (string.IsNullOrWhiteSpace(mapFolder)) {
            throw new ArgumentException("A target map folder is required.", nameof(mapFolder));
        }

        if (string.IsNullOrWhiteSpace(simfilePath)) {
            throw new ArgumentException("A simfile path is required.", nameof(simfilePath));
        }

        Directory.CreateDirectory(mapFolder);

        var beatmap = new RagnarockMap(mapFolder, makeNew: true);
        var converter = new StepManiaMapConverter();
        converter.Convert(simfilePath, beatmap);
        beatmap.SaveToFile();

        return OpenMap(mapFolder);
    }

    public MapDocumentSummary OpenMap(string mapFolder) {
        if (string.IsNullOrWhiteSpace(mapFolder)) {
            throw new ArgumentException("A map folder is required.", nameof(mapFolder));
        }

        var beatmap = new RagnarockMap(mapFolder, makeNew: false);
        return new MapDocumentSummary(
            Path.GetFullPath(mapFolder),
            (string?)beatmap.GetValue("_songName") ?? string.Empty,
            (string?)beatmap.GetValue("_songFilename") ?? BeatmapDefaults.SongFilename
        );
    }

    private static VorbisWaveReader TryOpenVorbis(string songFilePath) {
        try {
            return new VorbisWaveReader(songFilePath);
        } catch (Exception ex) {
            throw new InvalidDataException("The .ogg file is corrupted.", ex);
        }
    }

    private static void CopyIfNeeded(string sourceSongPath, string destinationSongPath) {
        var sourceFullPath = Path.GetFullPath(sourceSongPath);
        var destinationFullPath = Path.GetFullPath(destinationSongPath);
        if (string.Equals(sourceFullPath, destinationFullPath, StringComparison.OrdinalIgnoreCase)) {
            return;
        }

        if (File.Exists(destinationFullPath)) {
            File.Delete(destinationFullPath);
        }

        File.Copy(sourceFullPath, destinationFullPath, overwrite: true);
    }

    private static void ApplyVorbisMetadata(RagnarockMap beatmap, string songFilePath) {
        using var tagReader = new VorbisSampleProvider(File.OpenRead(songFilePath), closeOnDispose: true);
        var tags = tagReader.Tags;
        if (!string.IsNullOrWhiteSpace(tags.Artist)) {
            beatmap.SetValue("_songAuthorName", tags.Artist);
        }

        if (!string.IsNullOrWhiteSpace(tags.Title)) {
            beatmap.SetValue("_songName", tags.Title);
        }
    }
}
