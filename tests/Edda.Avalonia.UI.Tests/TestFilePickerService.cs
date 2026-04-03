using Edda.Avalonia.Services;
using Avalonia.Controls;
using System.Threading.Tasks;

namespace Edda.Avalonia.UI.Tests;

internal sealed class TestFilePickerService : IFilePickerService {
    readonly Queue<string?> selections = new();

    public void Enqueue(params string?[] paths) {
        foreach (var path in paths) {
            selections.Enqueue(path);
        }
    }

    public Task<string?> PickOpenMapFolderAsync(Window? owner) {
        return Task.FromResult(Dequeue());
    }

    public Task<string?> PickNewMapFolderAsync(Window? owner) {
        return Task.FromResult(Dequeue());
    }

    public Task<string?> PickSongFileAsync(Window? owner) {
        return Task.FromResult(Dequeue());
    }

    public Task<string?> PickCoverFileAsync(Window? owner) {
        return Task.FromResult(Dequeue());
    }

    public Task<string?> PickImportSimfileAsync(Window? owner) {
        return Task.FromResult(Dequeue());
    }

    public Task<string?> PickExportFolderAsync(Window? owner, string? initialDirectory) {
        return Task.FromResult(Dequeue());
    }

    public Task<string?> PickGameInstallFolderAsync(Window? owner, string? initialDirectory) {
        return Task.FromResult(Dequeue());
    }

    string? Dequeue() {
        if (selections.Count == 0) {
            throw new InvalidOperationException("No queued file picker selection is available.");
        }

        return selections.Dequeue();
    }
}
