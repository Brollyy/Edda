using Avalonia.Controls;
using Avalonia.Platform.Storage;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Edda.Avalonia.Services;

public sealed class StorageProviderFilePickerService : IFilePickerService {
    const string TestPickerQueueFileEnvironmentVariable = "EDDA_TEST_PICKER_QUEUE_FILE";
    const string TestPickerCancelSentinel = "__EDDA_TEST_PICKER_CANCEL__";

    public Task<string?> PickOpenMapFolderAsync(Window? owner) {
        return PickFolderAsync(owner, "Select your map's containing folder");
    }

    public Task<string?> PickNewMapFolderAsync(Window? owner) {
        return PickFolderAsync(owner, "Select an empty folder to store your map");
    }

    public Task<string?> PickSongFileAsync(Window? owner) {
        return PickFileAsync(
            owner,
            "Select a song to map",
            [
                new FilePickerFileType("OGG Vorbis") {
                    Patterns = ["*.ogg"]
                }
            ]
        );
    }

    public Task<string?> PickCoverFileAsync(Window? owner) {
        return PickFileAsync(
            owner,
            "Select a cover image",
            [
                new FilePickerFileType("Cover image") {
                    Patterns = ["*.jpg", "*.jpeg", "*.jfif"]
                }
            ]
        );
    }

    public Task<string?> PickImportSimfileAsync(Window? owner) {
        return PickFileAsync(
            owner,
            "Select a simfile to import",
            [
                new FilePickerFileType("StepMania simfile") {
                    Patterns = ["*.sm", "*.ssc"]
                }
            ]
        );
    }

    public Task<string?> PickExportFolderAsync(Window? owner, string? initialDirectory) {
        return PickFolderAsync(owner, "Select an export destination folder");
    }

    public Task<string?> PickGameInstallFolderAsync(Window? owner, string? initialDirectory) {
        return PickFolderAsync(owner, "Select your Ragnarock install folder");
    }

    static async Task<string?> PickFolderAsync(Window? owner, string title) {
        if (TryDequeueTestPickerSelection(out var queuedSelection)) {
            return queuedSelection;
        }

        if (owner == null) {
            return null;
        }

        var folders = await owner.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions {
            Title = title,
            AllowMultiple = false
        });
        return folders.FirstOrDefault()?.TryGetLocalPath();
    }

    static async Task<string?> PickFileAsync(Window? owner, string title, IReadOnlyList<FilePickerFileType> filters) {
        if (TryDequeueTestPickerSelection(out var queuedSelection)) {
            return queuedSelection;
        }

        if (owner == null) {
            return null;
        }

        var files = await owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = filters
        });
        return files.FirstOrDefault()?.TryGetLocalPath();
    }

    static bool TryDequeueTestPickerSelection(out string? selection) {
        selection = null;

        var queueFilePath = Environment.GetEnvironmentVariable(TestPickerQueueFileEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(queueFilePath) || !File.Exists(queueFilePath)) {
            return false;
        }

        for (var attempt = 0; attempt < 10; attempt++) {
            try {
                var lines = File.ReadAllLines(queueFilePath)
                    .Where(line => !string.IsNullOrWhiteSpace(line))
                    .ToList();
                if (lines.Count == 0) {
                    return false;
                }

                selection = lines[0];
                File.WriteAllLines(queueFilePath, lines.Skip(1));
                if (string.Equals(selection, TestPickerCancelSentinel, StringComparison.Ordinal)) {
                    selection = null;
                }

                return true;
            } catch (IOException) {
                Thread.Sleep(20);
            }
        }

        throw new IOException($"Unable to read queued picker selections from '{queueFilePath}'.");
    }
}
