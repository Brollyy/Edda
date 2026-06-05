using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Edda.Const;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CoreProgram = Edda.Const.Program;

namespace Edda.Avalonia.Services;

public sealed class StorageProviderFilePickerService : IFilePickerService {
    const string TestPickerQueueFileEnvironmentVariable = "EDDA_TEST_PICKER_QUEUE_FILE";
    const string TestPickerCancelSentinel = "__EDDA_TEST_PICKER_CANCEL__";

    readonly UserSettingsManager userSettings;

    public StorageProviderFilePickerService(UserSettingsManager userSettings) {
        this.userSettings = userSettings;
    }

    public Task<string?> PickOpenMapFolderAsync(Window? owner) {
        return PickFolderAsync(owner, "Select your map's containing folder", ResolveMapSaveDirectory());
    }

    public Task<string?> PickNewMapFolderAsync(Window? owner) {
        return PickFolderAsync(owner, "Select an empty folder to store your map", ResolveMapSaveDirectory());
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
        return PickFolderAsync(owner, "Select an export destination folder", initialDirectory);
    }

    public Task<string?> PickGameInstallFolderAsync(Window? owner, string? initialDirectory) {
        return PickFolderAsync(owner, "Select your Ragnarock install folder", initialDirectory);
    }

    static async Task<string?> PickFolderAsync(Window? owner, string title, string? initialDirectory) {
        if (TryDequeueTestPickerSelection(out var queuedSelection)) {
            return queuedSelection;
        }

        if (owner == null) {
            return null;
        }

        var folders = await owner.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions {
            Title = title,
            AllowMultiple = false,
            SuggestedStartLocation = await GetSuggestedStartLocationAsync(owner, initialDirectory)
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

    string ResolveMapSaveDirectory() {
        var configuredDirectory = userSettings.GetValueForKey(UserSettingsKey.MapSaveLocationIndex) == "1"
            ? Path.Combine(userSettings.GetValueForKey(UserSettingsKey.MapSaveLocationPath) ?? string.Empty, CoreProgram.GameInstallRelativeMapFolder)
            : Helper.DefaultRagnarockMapPath();

        if (string.IsNullOrWhiteSpace(configuredDirectory)) {
            configuredDirectory = CoreProgram.DocumentsMapFolder;
        }

        try {
            Directory.CreateDirectory(configuredDirectory);
            return configuredDirectory;
        } catch {
            return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        }
    }

    static async Task<IStorageFolder?> GetSuggestedStartLocationAsync(Window owner, string? initialDirectory) {
        var existingDirectory = ResolveExistingDirectory(initialDirectory);
        return existingDirectory == null
            ? null
            : await owner.StorageProvider.TryGetFolderFromPathAsync(existingDirectory);
    }

    static string? ResolveExistingDirectory(string? path) {
        if (string.IsNullOrWhiteSpace(path)) {
            return null;
        }

        var candidate = Path.GetFullPath(path);
        if (File.Exists(candidate)) {
            candidate = Path.GetDirectoryName(candidate);
        }

        while (!string.IsNullOrWhiteSpace(candidate) && !Directory.Exists(candidate)) {
            candidate = Directory.GetParent(candidate)?.FullName;
        }

        return string.IsNullOrWhiteSpace(candidate) ? null : candidate;
    }
}