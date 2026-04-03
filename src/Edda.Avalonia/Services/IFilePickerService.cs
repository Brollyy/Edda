using Avalonia.Controls;
using System.Threading.Tasks;

namespace Edda.Avalonia.Services;

public interface IFilePickerService {
    Task<string?> PickOpenMapFolderAsync(Window? owner);
    Task<string?> PickNewMapFolderAsync(Window? owner);
    Task<string?> PickSongFileAsync(Window? owner);
    Task<string?> PickCoverFileAsync(Window? owner);
    Task<string?> PickImportSimfileAsync(Window? owner);
    Task<string?> PickExportFolderAsync(Window? owner, string? initialDirectory);
    Task<string?> PickGameInstallFolderAsync(Window? owner, string? initialDirectory);
}
