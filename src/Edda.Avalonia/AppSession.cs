using Edda.Startup;
using Edda.Settings;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using CoreProgram = Edda.Const.Program;
using Edda.Avalonia.Services;
using Edda.Avalonia.Windows;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Edda.Avalonia;

public sealed class AppSession {
    readonly IClassicDesktopStyleApplicationLifetime? lifetime;
    readonly IFilePickerService filePickerService;
    readonly IDialogService dialogService;
    readonly MapWorkspaceService mapWorkspaceService;

    public global::RecentOpenedFolders RecentMaps { get; }
    public UserSettingsManager UserSettings { get; }
    public StartWindow? StartWindow { get; private set; }
    public MainWindow? MainWindow { get; private set; }
    public SettingsWindow? SettingsWindow { get; private set; }
    public bool IsShutdownRequested { get; private set; }

    public AppSession(
        global::RecentOpenedFolders recentMaps,
        UserSettingsManager userSettings,
        MapWorkspaceService mapWorkspaceService,
        IFilePickerService filePickerService,
        IDialogService dialogService,
        IClassicDesktopStyleApplicationLifetime? lifetime = null
    ) {
        RecentMaps = recentMaps;
        UserSettings = userSettings;
        this.mapWorkspaceService = mapWorkspaceService;
        this.filePickerService = filePickerService;
        this.dialogService = dialogService;
        this.lifetime = lifetime;
        SettingsBootstrapper.EnsureDefaults(UserSettings);
    }

    public static AppSession CreateDefault(IClassicDesktopStyleApplicationLifetime? lifetime) {
        Directory.CreateDirectory(CoreProgram.ProgramDataDir);
        var userSettings = new UserSettingsManager(CoreProgram.SettingsFile);
        SettingsBootstrapper.EnsureDefaults(userSettings);

        return new AppSession(
            new global::RecentOpenedFolders(CoreProgram.RecentOpenedMapsFile, CoreProgram.MaxRecentOpenedMaps),
            userSettings,
            new MapWorkspaceService(new NAudioAudioFileServices()),
            new StorageProviderFilePickerService(userSettings),
            new WindowDialogService(),
            lifetime
        );
    }

    public void Launch() {
        ShowStartWindow();
    }

    public void RequestExit() {
        ShutdownCore(shutdownLifetime: true);
    }

    public void CloseForSessionReset() {
        ShutdownCore(shutdownLifetime: false);
    }

    void ShutdownCore(bool shutdownLifetime) {
        IsShutdownRequested = true;
        CloseWindow(SettingsWindow);
        CloseWindow(MainWindow);
        CloseWindow(StartWindow);
        SettingsWindow = null;
        MainWindow = null;
        StartWindow = null;
        if (shutdownLifetime) {
            lifetime?.Shutdown();
        }
    }

    public async void OpenMapFromPicker(StartWindow owner) {
        var folder = await filePickerService.PickOpenMapFolderAsync(owner);
        OpenMap(owner, folder);
    }

    public async void OpenMapFromPicker(MainWindow owner) {
        var folder = await filePickerService.PickOpenMapFolderAsync(owner);
        OpenMap(owner, folder);
    }

    public void OpenRecentMap(StartWindow owner, string folder) {
        OpenMap(owner, folder);
    }

    public async void CreateNewMap(StartWindow owner) {
        var mapFolder = await filePickerService.PickNewMapFolderAsync(owner);
        if (string.IsNullOrWhiteSpace(mapFolder)) {
            return;
        }

        var songFile = await filePickerService.PickSongFileAsync(owner);
        if (string.IsNullOrWhiteSpace(songFile)) {
            return;
        }

        try {
            var summary = mapWorkspaceService.CreateNewMap(mapFolder, songFile);
            AddRecentMap(summary);
            ShowMainWindow(summary);
        } catch (Exception ex) {
            dialogService.ShowError(owner, "Error", $"An error occured while creating the map:\n{ex.Message}.", onDismissed: null);
        }
    }

    public async void CreateNewMap(MainWindow owner) {
        var mapFolder = await filePickerService.PickNewMapFolderAsync(owner);
        if (string.IsNullOrWhiteSpace(mapFolder)) {
            return;
        }

        var songFile = await filePickerService.PickSongFileAsync(owner);
        if (string.IsNullOrWhiteSpace(songFile)) {
            return;
        }

        try {
            var summary = mapWorkspaceService.CreateNewMap(mapFolder, songFile);
            AddRecentMap(summary);
            ShowMainWindow(summary);
        } catch (Exception ex) {
            dialogService.ShowError(owner, "Error", $"An error occured while creating the map:\n{ex.Message}.", onDismissed: null);
        }
    }

    public async void ImportMap(StartWindow owner) {
        var mapFolder = await filePickerService.PickNewMapFolderAsync(owner);
        if (string.IsNullOrWhiteSpace(mapFolder)) {
            return;
        }

        var simfile = await filePickerService.PickImportSimfileAsync(owner);
        if (string.IsNullOrWhiteSpace(simfile)) {
            return;
        }

        try {
            var summary = mapWorkspaceService.ImportMap(mapFolder, simfile);
            AddRecentMap(summary);
            ShowMainWindow(summary);
        } catch (Exception ex) {
            dialogService.ShowError(owner, "Error", $"An error occured while importing the simfile:\n{ex.Message}.", onDismissed: null);
        }
    }

    public async void ImportMap(MainWindow owner) {
        var mapFolder = await filePickerService.PickNewMapFolderAsync(owner);
        if (string.IsNullOrWhiteSpace(mapFolder)) {
            return;
        }

        var simfile = await filePickerService.PickImportSimfileAsync(owner);
        if (string.IsNullOrWhiteSpace(simfile)) {
            return;
        }

        try {
            var summary = mapWorkspaceService.ImportMap(mapFolder, simfile);
            AddRecentMap(summary);
            ShowMainWindow(summary);
        } catch (Exception ex) {
            dialogService.ShowError(owner, "Error", $"An error occured while importing the simfile:\n{ex.Message}.", onDismissed: null);
        }
    }

    public void ConfirmRemoveRecentMap(StartWindow owner, string path) {
        dialogService.ShowRecentMapRemovalConfirmation(
            owner,
            "Confirm Removal",
            "Are you sure you want to remove this map from the list of recently opened maps?",
            result => {
                if (result != AppDialogResult.Yes) {
                    return;
                }

                RemoveRecentMap(path);
                owner.RefreshRecentMaps();
            }
        );
    }

    public void ReturnToStartWindow() {
        var previousSettingsWindow = SettingsWindow;
        var previousMainWindow = MainWindow;
        SettingsWindow = null;
        MainWindow = null;

        var startWindow = new StartWindow(this);
        StartWindow = startWindow;
        SetLifetimeMainWindow(startWindow);
        startWindow.Show();

        CloseWindow(previousSettingsWindow);
        CloseWindow(previousMainWindow);
    }

    public void ShowSettingsWindow(MainWindow owner) {
        if (SettingsWindow is { IsVisible: true }) {
            SettingsWindow.Activate();
            return;
        }

        var settingsWindow = new SettingsWindow(this, owner);
        SettingsWindow = settingsWindow;
        settingsWindow.Closed += (_, _) => {
            if (ReferenceEquals(SettingsWindow, settingsWindow)) {
                SettingsWindow = null;
            }
        };
        settingsWindow.Show(owner);
    }

    public Task<string?> PickGameInstallFolderAsync(Window? owner, string? initialDirectory) {
        return filePickerService.PickGameInstallFolderAsync(owner, initialDirectory);
    }

    public Task<string?> PickSongFileAsync(Window? owner) {
        return filePickerService.PickSongFileAsync(owner);
    }

    public Task<string?> PickCoverFileAsync(Window? owner) {
        return filePickerService.PickCoverFileAsync(owner);
    }

    public Task<string?> PickExportFolderAsync(Window? owner, string? initialDirectory) {
        return filePickerService.PickExportFolderAsync(owner, initialDirectory);
    }

    public void ShowError(Window? owner, string title, string message, Action? onDismissed) {
        dialogService.ShowError(owner, title, message, onDismissed);
    }

    public void ShowYesNoConfirmation(Window? owner, string title, string message, Action<AppDialogResult> onCompleted) {
        dialogService.ShowYesNoConfirmation(owner, title, message, onCompleted);
    }

    public void ShowYesNoCancelConfirmation(Window? owner, string title, string message, Action<AppDialogResult> onCompleted) {
        dialogService.ShowYesNoCancelConfirmation(owner, title, message, onCompleted);
    }

    void OpenMap(Window owner, string? folder) {
        if (string.IsNullOrWhiteSpace(folder)) {
            return;
        }

        try {
            var summary = mapWorkspaceService.OpenMap(folder);
            AddRecentMap(summary);
            ShowMainWindow(summary);
        } catch (Exception ex) {
            dialogService.ShowError(owner, "Error", $"An error occured while opening the map:\n{ex.Message}.", () => {
                RemoveRecentMap(folder);
                if (owner is StartWindow startWindow) {
                    startWindow.RefreshRecentMaps();
                }
            });
        }
    }

    void ShowStartWindow() {
        CloseWindow(SettingsWindow);
        SettingsWindow = null;

        if (StartWindow is { IsVisible: true }) {
            StartWindow.RefreshRecentMaps();
            return;
        }

        var startWindow = new StartWindow(this);
        StartWindow = startWindow;
        SetLifetimeMainWindow(startWindow);
        startWindow.Show();
    }

    void ShowMainWindow(MapDocumentSummary summary) {
        var previousSettingsWindow = SettingsWindow;
        var previousStartWindow = StartWindow;
        var previousMainWindow = MainWindow;
        SettingsWindow = null;
        var mainWindow = new MainWindow(this, summary);
        MainWindow = mainWindow;
        SetLifetimeMainWindow(mainWindow);
        mainWindow.Show();
        StartWindow = null;

        CloseWindow(previousSettingsWindow);
        CloseWindow(previousStartWindow);
        if (!ReferenceEquals(previousMainWindow, mainWindow)) {
            CloseWindow(previousMainWindow);
        }
    }

    void AddRecentMap(MapDocumentSummary summary) {
        RecentMaps.AddRecentlyOpened(summary.SongName, summary.MapFolder);
        RecentMaps.Write();
    }

    void RemoveRecentMap(string path) {
        RecentMaps.RemoveRecentlyOpened(path);
        RecentMaps.Write();
    }

    void SetLifetimeMainWindow(Window window) {
        if (lifetime != null) {
            lifetime.MainWindow = window;
        }
    }

    static void CloseWindow(Window? window) {
        if (window == null) {
            return;
        }

        if (Dispatcher.UIThread.CheckAccess()) {
            window.Close();
            return;
        }

        Dispatcher.UIThread.Post(window.Close);
    }
}
