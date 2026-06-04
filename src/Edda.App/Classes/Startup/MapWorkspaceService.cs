using System;
using System.IO;
using Edda.Const;

namespace Edda.Startup;

#nullable enable

public sealed class MapWorkspaceService {
    readonly ISongAudioMetadataReader audioMetadataReader;

    public MapWorkspaceService(ISongAudioMetadataReader audioMetadataReader) {
        this.audioMetadataReader = audioMetadataReader;
    }

    public MapDocumentSummary CreateNewMap(string mapFolder, string songFilePath) {
        if (string.IsNullOrWhiteSpace(mapFolder)) {
            throw new ArgumentException("A target map folder is required.", nameof(mapFolder));
        }

        if (string.IsNullOrWhiteSpace(songFilePath)) {
            throw new ArgumentException("A source song path is required.", nameof(songFilePath));
        }

        Directory.CreateDirectory(mapFolder);

        var duration = audioMetadataReader.GetDuration(songFilePath);
        if (duration.TotalHours >= 1) {
            throw new InvalidDataException("Songs over 1 hour in duration are not supported.");
        }

        var beatmap = new RagnarockMap(mapFolder, makeNew: true);
        var songFileName = Helper.SanitiseSongFileName(songFilePath);
        var destinationSongPath = Path.Combine(mapFolder, songFileName);

        beatmap.SetValue("_songApproximativeDuration", (int)duration.TotalSeconds + 1);
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

        var resolvedMapFolder = RagnarockMap.ResolveExistingPathCasing(mapFolder);
        var beatmap = new RagnarockMap(resolvedMapFolder, makeNew: false);
        return new MapDocumentSummary(
            resolvedMapFolder,
            (string?)beatmap.GetValue("_songName") ?? string.Empty,
            (string?)beatmap.GetValue("_songFilename") ?? BeatmapDefaults.SongFilename
        );
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

    private void ApplyVorbisMetadata(RagnarockMap beatmap, string songFilePath) {
        var tags = audioMetadataReader.ReadTags(songFilePath);
        if (!string.IsNullOrWhiteSpace(tags.Artist)) {
            beatmap.SetValue("_songAuthorName", tags.Artist);
        }

        if (!string.IsNullOrWhiteSpace(tags.Title)) {
            beatmap.SetValue("_songName", tags.Title);
        }
    }
}
