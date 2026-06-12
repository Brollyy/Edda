using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Edda.Avalonia.Services;
using Edda.Classes.MapEditorNS;
using Edda.Classes.MapEditorNS.NoteNS;
using Edda.Classes.MapEditorNS.Stats;
using Edda.Const;
using Edda.Startup;
using NAudio.Vorbis;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AvaloniaColor = Avalonia.Media.Color;
using Button = Avalonia.Controls.Button;
using EddaProgram = Edda.Const.Program;
using Line = Avalonia.Controls.Shapes.Line;
using PixelPoint = Avalonia.PixelPoint;
using Rectangle = Avalonia.Controls.Shapes.Rectangle;
using TextBox = Avalonia.Controls.TextBox;

namespace Edda.Avalonia.Windows;

public sealed partial class MainWindow : Window {
    readonly AppSession appSession;
    readonly UserSettingsManager userSettings;
    readonly EditorUiAdapter mapEditorUiAdapter;
    readonly DispatcherTimer songPositionTimer;
    readonly List<PlaybackDeviceOption> playbackDevices = [];

    MapEditor? mapEditor;
    bool suppressControlEvents;
    bool textInputHasFocus;
    bool songIsPlaying;
    bool songPauseInProgress;
    bool shiftKeyDown;
    bool snapToGrid = true;
    bool navWaveformDragging;
    bool spectrogramResizeDragging;
    int difficultyPredictionUpdateSuspensionDepth;
    bool difficultyPredictionUpdatePending;
    Button? overlayPressedButton;
    double currentSongDurationSeconds;
    double currentSongPositionMilliseconds;
    int editorAudioLatency;
    int drumFeedbackSequence;
    int noteFeedbackSequence;
    const double SpectrogramResizeGripWidth = 8;
    const double SpectrogramDefaultWidth = 220;
    const double SidebarDockWidth = 225;
    const double PlaybackUiTextUpdateIntervalMilliseconds = 100;
    const double PlaybackProgressIndicatorEpsilon = 0.5;
    double spectrogramPreferredWidth = SpectrogramDefaultWidth;
    double spectrogramResizeDragOriginX;
    double spectrogramResizeDragOriginWidth;
    double lastPlaybackUiTextPositionMilliseconds = double.NaN;
    double lastSongProgressIndicatorY = double.NaN;
    double lastSongProgressAutomationPositionMilliseconds = double.NaN;

    OpenAlAudioEngine? audioEngine;
    AvaloniaOpenAlSongPreviewController? songPreviewController;
    CancellationTokenSource songPlaybackCancellationTokenSource = new();
    readonly Stopwatch playbackClock = new();
    double playbackStartMilliseconds;
    OpenAlStreamingSource? songPlayerSource;
    IAudioCuePlayer? drummer;
    IAudioCuePlayer? metronome;
    NoteScanner? noteScanner;
    BeatScanner? beatScanner;

    readonly Dictionary<string, Bitmap?> resourceBitmapCache = new(StringComparer.OrdinalIgnoreCase);

    MenuItem menuItemSnapToGrid = null!;
    MenuItem menuItemClearCache = null!;
    Border leftSidebar = null!;
    Border rightSidebar = null!;
    Grid gridSpectrogram = null!;
    Border borderSpectrogram = null!;
    Button spectrogramResize = null!;
    Border borderNavWaveform = null!;
    ScrollViewer scrollSpectrogram = null!;
    Canvas spectrogramCanvas = null!;
    Canvas mainWaveformCanvas = null!;
    Canvas navWaveformBackdrop = null!;
    Grid navWaveformVisualHost = null!;
    Line lineSongMouseover = null!;
    Line lineSongProgress = null!;
    Line lineBeatScan = null!;
    Canvas canvasBookmarkLabels = null!;
    Canvas canvasTimingChangeLabels = null!;
    global::Avalonia.Controls.Image drum0 = null!;
    global::Avalonia.Controls.Image drum1 = null!;
    global::Avalonia.Controls.Image drum2 = null!;
    global::Avalonia.Controls.Image drum3 = null!;

    Window? bpmFinderWindow;
    Window? predictorWindow;
    Window? aboutWindow;
    Window? changeBpmWindow;
    Window? customizeNavBarWindow;
    Window? songPreviewWindow;

    public string WindowId => "AppMainWindow";
    internal AppSession Session => appSession;
    internal UserSettingsManager UserSettings => userSettings;
    internal MapEditor? MapEditorInstance => mapEditor;
    internal string? CurrentMapFolder => mapEditor?.mapFolder;
    internal string? CurrentSongPath => mapEditor == null ? null : Path.Combine(mapEditor.mapFolder, GetMapString("_songFilename"));
    internal double CurrentSongPositionMilliseconds => currentSongPositionMilliseconds;
    internal double CurrentGlobalBpm => GetMapDouble("_beatsPerMinute");
    internal int CurrentGridDivision => Math.Max(1, GetMapInt("_editorGridDivision", RagnarockMapDifficulties.Current, custom: true));

    public TextBox TxtSongName { get; private set; } = null!;
    public TextBox TxtArtistName { get; private set; } = null!;
    public TextBox TxtMapperName { get; private set; } = null!;
    public TextBlock TxtSongFileName { get; private set; } = null!;
    public TextBlock TxtCoverFileName { get; private set; } = null!;
    public TextBox TxtSongBpm { get; private set; } = null!;
    public ComboBox ComboEnvironment { get; private set; } = null!;
    public CheckBox CheckExplicitContent { get; private set; } = null!;

    public Button BtnPickSong { get; private set; } = null!;
    public Button BtnPickCover { get; private set; } = null!;
    public Button BtnSongPlayer { get; private set; } = null!;
    public Button BtnPlayPreview { get; private set; } = null!;
    public Button BtnMakePreview { get; private set; } = null!;
    public Button BtnChangeBPM { get; private set; } = null!;
    public Button BtnCustomizeNavBar { get; private set; } = null!;
    public Slider SliderSongProgress { get; private set; } = null!;
    public Slider SliderSongTempo { get; private set; } = null!;
    public Slider SliderSongVol { get; private set; } = null!;
    public Slider SliderDrumVol { get; private set; } = null!;
    public TextBlock TxtSongTempo { get; private set; } = null!;
    public TextBlock TxtSongPosition { get; private set; } = null!;
    public TextBlock TxtSongDuration { get; private set; } = null!;
    public TextBlock TxtSongVol { get; private set; } = null!;
    public TextBlock TxtDrumVol { get; private set; } = null!;
    public CheckBox CheckMetronome { get; private set; } = null!;
    public CheckBox CheckWaveform { get; private set; } = null!;
    public CheckBox CheckGridSnap { get; private set; } = null!;
    public TextBox TxtGridDivision { get; private set; } = null!;
    public TextBox TxtGridSpacing { get; private set; } = null!;
    public TextBlock LblSelectedBeat { get; private set; } = null!;
    public TextBlock DifficultyPrediction { get; private set; } = null!;
    public Button ImgWaveformVertical { get; private set; } = null!;
    public Canvas CanvasBookmarks { get; private set; } = null!;
    public Canvas CanvasTimingChanges { get; private set; } = null!;
    public Canvas CanvasNavNotes { get; private set; } = null!;
    public Canvas CanvasNavInputBox { get; private set; } = null!;
    public global::Avalonia.Controls.Image ImgCover { get; private set; } = null!;

    Bitmap? coverPreviewBitmap;

    public Button BtnChangeDifficulty0 { get; private set; } = null!;
    public Button BtnChangeDifficulty1 { get; private set; } = null!;
    public Button BtnChangeDifficulty2 { get; private set; } = null!;
    public Button BtnAddDifficulty { get; private set; } = null!;
    public Button BtnDeleteDifficulty { get; private set; } = null!;
    public TextBox TxtDifficultyNumber { get; private set; } = null!;
    public TextBox TxtNoteSpeed { get; private set; } = null!;
    public TextBox TxtDistMedal0 { get; private set; } = null!;
    public TextBox TxtDistMedal1 { get; private set; } = null!;
    public TextBox TxtDistMedal2 { get; private set; } = null!;
    TextBlock lblDifficultyRank1 = null!;
    TextBlock lblDifficultyRank2 = null!;
    TextBlock lblDifficultyRank3 = null!;
    Border difficultySlot0 = null!;
    Border difficultySlot1 = null!;
    Border difficultySlot2 = null!;
    public IReadOnlyList<PlaybackDeviceOption> PlaybackDevices => playbackDevices;
    public string? PlaybackDeviceId { get; private set; }
    public bool PlayingOnDefaultDevice { get; private set; } = true;
    public bool DefaultDeviceAvailable => PlaybackDevices.Count > 0;

    public MainWindow(AppSession appSession, MapDocumentSummary summary) {
        this.appSession = appSession;
        userSettings = appSession.UserSettings;
        mapEditorUiAdapter = new EditorUiAdapter(this, userSettings);

        AutomationHelper.SetAutomationId(this, WindowId);
        Title = "Edda";
        Width = 1000;
        Height = 870;
        MinWidth = 1000;
        MinHeight = 450;
        CanResize = true;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        var iconPath = Path.Combine(AppContext.BaseDirectory, "Resources", "icon.png");
        if (File.Exists(iconPath)) {
            Icon = new WindowIcon(iconPath);
        }

        songPositionTimer = new DispatcherTimer {
            Interval = TimeSpan.FromMilliseconds(16)
        };
        songPositionTimer.Tick += (_, _) => {
            try {
                UpdatePlaybackPositionFromAudio();
            } catch (Exception) {
                PauseSong();
            }
        };
        try {
            audioEngine = new OpenAlAudioEngine();
            songPreviewController = new AvaloniaOpenAlSongPreviewController(audioEngine, new AvaloniaSongPreviewUiAdapter(this));
        } catch {
            audioEngine = null;
            songPreviewController = null;
        }

        Content = BuildRoot();
        PropertyChanged += OnMainWindowPropertyChanged;
        AddHandler(InputElement.PointerPressedEvent, OnWindowPointerTracePressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(InputElement.PointerMovedEvent, OnWindowPointerTraceMoved, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(InputElement.PointerReleasedEvent, OnWindowPointerTraceReleased, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(InputElement.PointerWheelChangedEvent, OnWindowPointerTraceWheelChanged, RoutingStrategies.Tunnel, handledEventsToo: true);
        Opened += (_, _) => {
            CenterAndConstrainToWorkingArea();
            DisableInactiveOverlayHitTesting();
            QueuePostLayoutEditorSync();
        };
        Closed += (_, _) => {
            DisposeWindowResources();
        };
        AddHandler(InputElement.KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(InputElement.KeyUpEvent, OnKeyUp, RoutingStrategies.Tunnel, handledEventsToo: true);

        LoadSettingsFile();
        LoadMap(summary.MapFolder);
        SetSnapToGrid(true);
        UpdatePlaybackUi();
    }

    void OnWindowPointerTracePressed(object? sender, PointerPressedEventArgs e) {
        var windowPosition = e.GetPosition(this);
        if (TryRouteOverlayPointerToNavWaveform(e.Source, windowPosition, onPressed: () => OnNavWaveformPointerPressed(ImgWaveformVertical, e))) {
            e.Handled = true;
            return;
        }

        if (TryRouteOverlayPointerToSpectrogramResize(e.Source, windowPosition, onPressed: () => OnSpectrogramResizePointerPressed(spectrogramResize, e))) {
            e.Handled = true;
            return;
        }

        if (TryHandleOverlayButtonPointerPressed(e.Source, windowPosition)) {
            e.Handled = true;
            return;
        }

        if (ShouldRouteWindowPointerToEditor(e.Source, windowPosition)) {
            OnScrollEditorPointerPressed(scrollEditorInputLayer, e);
        }
    }

    void OnWindowPointerTraceMoved(object? sender, PointerEventArgs e) {
        var windowPosition = e.GetPosition(this);
        if (TryRouteOverlayPointerToNavWaveform(e.Source, windowPosition, onPressed: null, onMoved: () => OnNavWaveformPointerMoved(ImgWaveformVertical, e))) {
            e.Handled = true;
            return;
        }

        if (!navWaveformDragging &&
            lineSongMouseover != null &&
            lineSongMouseover.Opacity > 0 &&
            ImgWaveformVertical != null &&
            !IsWindowPointWithinControl(ImgWaveformVertical, windowPosition)) {
            RefreshSongMouseoverIndicator(0, isVisible: false);
        }

        if (TryRouteOverlayPointerToSpectrogramResize(e.Source, windowPosition, onPressed: null, onMoved: () => OnSpectrogramResizePointerMoved(spectrogramResize, e))) {
            e.Handled = true;
            return;
        }

        if (ShouldRouteWindowPointerToEditor(e.Source, windowPosition, allowActiveGesture: true)) {
            OnScrollEditorPointerMoved(scrollEditorInputLayer, e);
        }
    }

    void OnWindowPointerTraceReleased(object? sender, PointerReleasedEventArgs e) {
        var windowPosition = e.GetPosition(this);
        if (TryRouteOverlayPointerToNavWaveform(e.Source, windowPosition, onPressed: null, onMoved: null, onReleased: () => OnNavWaveformPointerReleased(ImgWaveformVertical, e))) {
            e.Handled = true;
            return;
        }

        if (TryRouteOverlayPointerToSpectrogramResize(e.Source, windowPosition, onPressed: null, onMoved: null, onReleased: () => OnSpectrogramResizePointerReleased(spectrogramResize, e))) {
            e.Handled = true;
            return;
        }

        if (TryHandleOverlayButtonPointerReleased(e.Source, windowPosition)) {
            e.Handled = true;
            return;
        }

        if (ShouldRouteWindowPointerToEditor(e.Source, windowPosition, allowActiveGesture: true)) {
            OnScrollEditorPointerReleased(scrollEditorInputLayer, e);
        }
    }

    void OnWindowPointerTraceWheelChanged(object? sender, PointerWheelEventArgs e) {
        var windowPosition = e.GetPosition(this);
        if (ShouldRouteWindowPointerToEditor(e.Source, windowPosition, allowActiveGesture: true)) {
            OnScrollEditorPointerWheelChanged(scrollEditorInputLayer, e);
        }
    }

    static string DescribePointerSource(object? source) {
        if (source is StyledElement styledElement) {
            return AutomationProperties.GetAutomationId(styledElement) ??
                styledElement.Name ??
                styledElement.GetType().Name;
        }

        return source?.GetType().Name ?? "<null>";
    }

    bool ShouldRouteWindowPointerToEditor(object? source, Point windowPosition, bool allowActiveGesture = false) {
        if (scrollEditorInputLayer == null) {
            return false;
        }

        var sourceTypeName = source?.GetType().Name;
        if (!string.Equals(sourceTypeName, "LightDismissOverlayLayer", StringComparison.Ordinal) &&
            !(allowActiveGesture && editorPointerPressed)) {
            return false;
        }

        var viewportOrigin = scrollEditor.TranslatePoint(new Point(0, 0), this);
        if (!viewportOrigin.HasValue) {
            return false;
        }

        var viewportBounds = new Rect(viewportOrigin.Value, scrollEditor.Bounds.Size);
        return viewportBounds.Contains(windowPosition);
    }

    bool TryRouteOverlayPointerToNavWaveform(
        object? source,
        Point windowPosition,
        Action? onPressed,
        Action? onMoved = null,
        Action? onReleased = null) {
        if (ImgWaveformVertical == null || !string.Equals(source?.GetType().Name, "LightDismissOverlayLayer", StringComparison.Ordinal)) {
            return false;
        }

        if (!navWaveformDragging && !IsWindowPointWithinControl(ImgWaveformVertical, windowPosition)) {
            return false;
        }

        onPressed?.Invoke();
        onMoved?.Invoke();
        onReleased?.Invoke();
        return true;
    }

    bool TryRouteOverlayPointerToSpectrogramResize(
        object? source,
        Point windowPosition,
        Action? onPressed,
        Action? onMoved = null,
        Action? onReleased = null) {
        if (spectrogramResize == null || !string.Equals(source?.GetType().Name, "LightDismissOverlayLayer", StringComparison.Ordinal)) {
            return false;
        }

        if (!spectrogramResizeDragging && !IsWindowPointWithinControl(spectrogramResize, windowPosition)) {
            return false;
        }

        onPressed?.Invoke();
        onMoved?.Invoke();
        onReleased?.Invoke();
        return true;
    }

    bool TryHandleOverlayButtonPointerPressed(object? source, Point windowPosition) {
        if (!string.Equals(source?.GetType().Name, "LightDismissOverlayLayer", StringComparison.Ordinal)) {
            return false;
        }

        overlayPressedButton = ResolveUnderlyingButton(windowPosition);
        return overlayPressedButton != null;
    }

    bool TryHandleOverlayButtonPointerReleased(object? source, Point windowPosition) {
        if (!string.Equals(source?.GetType().Name, "LightDismissOverlayLayer", StringComparison.Ordinal)) {
            overlayPressedButton = null;
            return false;
        }

        var pressedButton = overlayPressedButton;
        overlayPressedButton = null;
        if (pressedButton == null) {
            return false;
        }

        var releasedButton = ResolveUnderlyingButton(windowPosition);
        if (!ReferenceEquals(pressedButton, releasedButton)) {
            return false;
        }

        pressedButton.Focus();
        pressedButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        return true;
    }

    Button? ResolveUnderlyingButton(Point windowPosition) {
        return this.GetVisualDescendants()
            .OfType<Button>()
            .Where(button =>
                !ReferenceEquals(button, ImgWaveformVertical) &&
                button.IsVisible &&
                button.IsEnabled &&
                button.IsHitTestVisible &&
                button.TranslatePoint(new Point(0, 0), this) is Point origin &&
                new Rect(origin, button.Bounds.Size).Contains(windowPosition))
            .OrderBy(button => button.Bounds.Width * button.Bounds.Height)
            .FirstOrDefault();
    }

    bool IsWindowPointWithinControl(Control control, Point windowPosition) {
        var origin = control.TranslatePoint(new Point(0, 0), this);
        return origin.HasValue && new Rect(origin.Value, control.Bounds.Size).Contains(windowPosition);
    }

    void DisableInactiveOverlayHitTesting() {
        Dispatcher.UIThread.Post(() => {
            foreach (var inputElement in this.GetVisualDescendants().OfType<InputElement>()) {
                if (!string.Equals(inputElement.GetType().Name, "LightDismissOverlayLayer", StringComparison.Ordinal)) {
                    continue;
                }

                inputElement.IsHitTestVisible = false;
            }
        }, DispatcherPriority.Loaded);
    }

    void CenterAndConstrainToWorkingArea() {
        var screen = Screens.ScreenFromWindow(this) ?? Screens.Primary;
        if (screen == null) {
            return;
        }

        var workingArea = screen.WorkingArea;
        var scaling = Math.Max(1, RenderScaling);
        var maxWidth = Math.Max(960, (workingArea.Width - 32) / scaling);
        var maxHeight = Math.Max(620, (workingArea.Height - 32) / scaling);
        var constrainedWidth = Math.Min(Width, maxWidth);
        var constrainedHeight = Math.Min(Height, maxHeight);

        MinWidth = Math.Min(MinWidth, constrainedWidth);
        MinHeight = Math.Min(MinHeight, constrainedHeight);
        Width = constrainedWidth;
        Height = constrainedHeight;

        var widthPixels = (int)Math.Round(constrainedWidth * scaling);
        var heightPixels = (int)Math.Round(constrainedHeight * scaling);
        var centeredX = workingArea.X + Math.Max(0, (workingArea.Width - widthPixels) / 2);
        var centeredY = workingArea.Y + Math.Max(0, (workingArea.Height - heightPixels) / 2);
        Position = new PixelPoint(centeredX, centeredY);
    }

    void QueuePostLayoutEditorSync() {
        Dispatcher.UIThread.Post(() => {
            if (mapEditor?.currentMapDifficulty == null) {
                return;
            }

            ApplyEditorLayoutMetrics();
            RefreshEditorSurface();
            SetSongPosition(currentSongPositionMilliseconds, updateSlider: true, updateNavWaveform: false);
        }, DispatcherPriority.Loaded);
    }

    public void OpenSettings() {
        appSession.ShowSettingsWindow(this);
    }

    public void LoadSettingsFile(bool reloadWaveforms = false) {
        PlaybackDeviceId = null;
        PlayingOnDefaultDevice = true;
        _ = int.TryParse(userSettings.GetValueForKey(UserSettingsKey.EditorAudioLatency), NumberStyles.Integer, CultureInfo.InvariantCulture, out editorAudioLatency);

        suppressControlEvents = true;
        SliderSongVol.Value = GetSettingDouble(UserSettingsKey.DefaultSongVolume, DefaultUserSettings.DefaultSongVolume);
        SliderDrumVol.Value = GetSettingDouble(UserSettingsKey.DefaultNoteVolume, DefaultUserSettings.DefaultNoteVolume);
        CheckWaveform.IsChecked = !GetSettingBool(UserSettingsKey.EnableSpectrogram, DefaultUserSettings.EnableSpectrogram);
        suppressControlEvents = false;

        UpdateVolumeTexts();
        UpdateSongTempoText();

        var showSpectrogram = GetSettingBool(UserSettingsKey.EnableSpectrogram, DefaultUserSettings.EnableSpectrogram);
        var cacheSpectrogram = GetSettingBool(UserSettingsKey.SpectrogramCache, DefaultUserSettings.SpectrogramCache);
        menuItemClearCache.IsVisible = showSpectrogram && cacheSpectrogram;

        RefreshEditorDisplayPreferences();
        RefreshNavigationLayersFromSettings();
        UpdateDifficultyPrediction();
        UpdateAudioVolumes();
    }

    public void UpdatePlaybackDevice(string? newPlaybackDeviceId, bool isDefaultDevice) {
        PlaybackDeviceId = string.IsNullOrWhiteSpace(newPlaybackDeviceId) ? null : newPlaybackDeviceId;
        PlayingOnDefaultDevice = isDefaultDevice || string.IsNullOrWhiteSpace(newPlaybackDeviceId);
        PauseSong();
        ReinitializePlaybackDependencies();
    }

    public void PauseSong() {
        if (songPauseInProgress) {
            return;
        }

        songPauseInProgress = true;
        try {
            var finalPosition = currentSongPositionMilliseconds;
            if (songIsPlaying) {
                finalPosition = playbackStartMilliseconds + (playbackClock.Elapsed.TotalMilliseconds * Math.Max(Audio.MinSongTempo, SliderSongTempo.Value));
            }

            songPlaybackCancellationTokenSource.Cancel();
            songPositionTimer.Stop();
            playbackClock.Stop();
            ResetPlaybackCanvasScroll();
            songIsPlaying = false;
            StopSongPlayer();
            noteScanner?.Stop();
            beatScanner?.Stop();
            SetSongPosition(finalPosition, updateSlider: true, updateNavWaveform: false);
            UpdatePlaybackUi();
            songPreviewController?.EnablePreviewButton();
        } finally {
            songPauseInProgress = false;
        }
    }

    public void RestartDrummer() {
        var oldDrummer = drummer;
        drummer = CreateAudioCuePlayer(
            userSettings.GetValueForKey(UserSettingsKey.DrumSampleFile) ?? DefaultUserSettings.DrumSampleFile,
            Audio.NotePlaybackStreams,
            userSettings.GetBoolForKey(UserSettingsKey.PanDrumSounds),
            GetSettingDouble(UserSettingsKey.DefaultNoteVolume, DefaultUserSettings.DefaultNoteVolume)
        );
        oldDrummer?.Dispose();
        drummer?.ChangeVolume(SliderDrumVol.Value);

        if (noteScanner != null) {
            noteScanner.SetAudioPlayer(drummer);
        } else {
            noteScanner = new NoteScanner(new AvaloniaNoteScannerUiAdapter(this), drummer, SliderSongTempo.Value);
        }
    }

    Control BuildRoot() {
        var root = new DockPanel();
        var topPanel = AutomationHelper.WithAutomationId(new Border {
            Name = "TopPanel",
            BorderBrush = new SolidColorBrush(AvaloniaColor.Parse("#6D6E73")),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Background = new LinearGradientBrush {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                GradientStops = new GradientStops {
                    new GradientStop(AvaloniaColor.Parse("#F4F4F4"), 0),
                    new GradientStop(AvaloniaColor.Parse("#D9D9D9"), 1)
                }
            },
            Child = BuildMenu()
        }, "TopPanel");
        DockPanel.SetDock(topPanel, Dock.Top);
        root.Children.Add(topPanel);

        var playback = BuildBottomPlaybackPanel();
        DockPanel.SetDock(playback, Dock.Bottom);
        root.Children.Add(playback);

        var body = new DockPanel {
            Background = new SolidColorBrush(AvaloniaColor.Parse("#D7D7DA")),
            ClipToBounds = true
        };

        leftSidebar = AutomationHelper.WithAutomationId(new Border {
            Name = "borderLeftDock",
            Width = SidebarDockWidth,
            MinWidth = SidebarDockWidth,
            Background = new SolidColorBrush(AvaloniaColor.Parse("#D7D7DA")),
            BorderBrush = new SolidColorBrush(AvaloniaColor.Parse("#6D6E73")),
            BorderThickness = new Thickness(0, 0, 1, 0),
            ClipToBounds = true
        }, "borderLeftDock");
        leftSidebar.Child = BuildLeftSidebar();
        DockPanel.SetDock(leftSidebar, Dock.Left);
        body.Children.Add(leftSidebar);

        rightSidebar = AutomationHelper.WithAutomationId(new Border {
            Name = "borderRightDock",
            Width = SidebarDockWidth,
            MinWidth = SidebarDockWidth,
            Background = new SolidColorBrush(AvaloniaColor.Parse("#D7D7DA")),
            BorderBrush = new SolidColorBrush(AvaloniaColor.Parse("#6D6E73")),
            BorderThickness = new Thickness(1, 0, 0, 0),
            ClipToBounds = true
        }, "borderRightDock");
        rightSidebar.Child = BuildRightSidebar();
        DockPanel.SetDock(rightSidebar, Dock.Right);
        body.Children.Add(rightSidebar);

        var centerPanel = BuildCenterPanel();
        body.Children.Add(centerPanel);

        root.Children.Add(body);
        return root;
    }

    Menu BuildMenu() {
        var menu = AutomationHelper.WithAutomationId(new Menu(), "MainMenu");

        var fileMenu = BuildMenuItem("File");
        fileMenu.Items.Add(BuildMenuItem("New Map", "MenuItemNewMap", (_, _) => BeginCreateNewMap()));
        fileMenu.Items.Add(BuildMenuItem("Open Map", "MenuItemOpenMap", (_, _) => BeginOpenMap()));
        fileMenu.Items.Add(BuildMenuItem("Save Map", "MenuItemSaveMap", (_, _) => SaveMapWithBackup()));
        fileMenu.Items.Add(BuildMenuItem("Import Map", "MenuItemImportMap", (_, _) => BeginImportMap()));
        fileMenu.Items.Add(BuildMenuItem("Export Map", "MenuItemExportMap", (_, _) => ExportMap()));
        fileMenu.Items.Add(BuildMenuItem("Close Map", "MenuItemCloseMap", (_, _) => BeginCloseMap()));

        var editMenu = BuildMenuItem("Edit");
        editMenu.Items.Add(BuildMenuItem("Copy", "MenuItemCopy", (_, _) => CopySelection()));
        editMenu.Items.Add(BuildMenuItem("Cut", "MenuItemCut", (_, _) => CutSelection()));
        editMenu.Items.Add(BuildMenuItem("Paste", "MenuItemPaste", (_, _) => PasteClipboardAtEditor(pasteOnColumn: false)));
        editMenu.Items.Add(BuildMenuItem("Paste On Column", "MenuItemPasteOnColumn", (_, _) => PasteClipboardAtEditor(pasteOnColumn: true)));
        editMenu.Items.Add(BuildMenuItem("Mirror", "MenuItemMirror", (_, _) => MirrorSelection()));
        editMenu.Items.Add(BuildMenuItem("Quantize", "MenuItemQuantize", (_, _) => QuantizeSelection()));
        menuItemSnapToGrid = BuildMenuItem("Snap Notes to Grid", "MenuItemSnapToGrid", (_, _) => SetSnapToGrid(menuItemSnapToGrid.IsChecked));
        menuItemSnapToGrid.ToggleType = MenuItemToggleType.CheckBox;
        editMenu.Items.Add(menuItemSnapToGrid);
        var addNewMenu = BuildMenuItem("Add New");
        addNewMenu.Items.Add(BuildMenuItem("Bookmark", "MenuItemAddBookmark", (_, _) => AddBookmarkAtCurrentPosition()));
        addNewMenu.Items.Add(BuildMenuItem("Timing Change", "MenuItemAddTimingChange", (_, _) => AddTimingChangeAtCurrentPosition()));
        addNewMenu.Items.Add(BuildMenuItem("Note (column 1)", "MenuItemAddNoteColumn1", (_, _) => AddNoteAtCurrentPosition(0)));
        addNewMenu.Items.Add(BuildMenuItem("Note (column 2)", "MenuItemAddNoteColumn2", (_, _) => AddNoteAtCurrentPosition(1)));
        addNewMenu.Items.Add(BuildMenuItem("Note (column 3)", "MenuItemAddNoteColumn3", (_, _) => AddNoteAtCurrentPosition(2)));
        addNewMenu.Items.Add(BuildMenuItem("Note (column 4)", "MenuItemAddNoteColumn4", (_, _) => AddNoteAtCurrentPosition(3)));
        editMenu.Items.Add(addNewMenu);

        var viewMenu = BuildMenuItem("View");
        viewMenu.Items.Add(BuildMenuItem("Toggle Left Sidebar", "MenuItemToggleLeftBar", (_, _) => ToggleLeftSidebar()));
        viewMenu.Items.Add(BuildMenuItem("Toggle Right Sidebar", "MenuItemToggleRightBar", (_, _) => ToggleRightSidebar()));

        var toolsMenu = BuildMenuItem("Tools");
        menuItemClearCache = BuildMenuItem("Clear Cache", "MenuItemClearCache", (_, _) => BeginClearCache());
        toolsMenu.Items.Add(BuildMenuItem("BPM Finder", "MenuItemBpmFinder", (_, _) => OpenBpmFinderWindow()));
        toolsMenu.Items.Add(BuildMenuItem("Difficulty Predictor", "MenuItemDifficultyPredictor", (_, _) => OpenDifficultyPredictorWindow()));
        toolsMenu.Items.Add(menuItemClearCache);
        toolsMenu.Items.Add(BuildMenuItem("Settings", "MenuItemSettings", (_, _) => OpenSettings()));

        var helpMenu = BuildMenuItem("Help");
        helpMenu.Items.Add(BuildMenuItem("About Edda", "MenuItemAboutPage", (_, _) => OpenAboutWindow()));

        menu.Items.Add(fileMenu);
        menu.Items.Add(editMenu);
        menu.Items.Add(viewMenu);
        menu.Items.Add(toolsMenu);
        menu.Items.Add(helpMenu);
        return menu;
    }

    static MenuItem BuildMenuItem(string header, string? automationId = null, EventHandler<RoutedEventArgs>? onClick = null) {
        var menuItem = automationId == null
            ? new MenuItem { Header = header }
            : AutomationHelper.WithAutomationId(new MenuItem { Header = header }, automationId);
        if (onClick != null) {
            menuItem.Click += onClick;
        }

        return menuItem;
    }

    Control BuildLeftSidebar() {
        var panel = new StackPanel {
            Spacing = 0,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        panel.Children.Add(CreateSectionHeader("Map Settings", "lblMapSettingsHeader", new Thickness(5, 3, 0, 0)));

        TxtSongName = CreateTextBox("txtSongName");
        TxtSongName.TextChanged += (_, _) => {
            if (suppressControlEvents || mapEditor == null) {
                return;
            }

            var songName = TxtSongName.Text ?? string.Empty;
            if (string.Equals(songName, GetMapString("_songName"), StringComparison.Ordinal)) {
                return;
            }

            mapEditor.SetMapValue("_songName", JToken.FromObject(songName));
            UpdateRecentMapEntry();
        };

        TxtArtistName = CreateTextBox("txtArtistName");
        TxtArtistName.TextChanged += (_, _) => {
            if (suppressControlEvents || mapEditor == null) {
                return;
            }

            var artistName = TxtArtistName.Text ?? string.Empty;
            if (string.Equals(artistName, GetMapString("_songAuthorName"), StringComparison.Ordinal)) {
                return;
            }

            mapEditor.SetMapValue("_songAuthorName", JToken.FromObject(artistName));
        };

        TxtMapperName = CreateTextBox("txtMapperName");
        TxtMapperName.TextChanged += (_, _) => {
            if (suppressControlEvents || mapEditor == null) {
                return;
            }

            var mapperName = TxtMapperName.Text ?? string.Empty;
            if (string.Equals(mapperName, GetMapString("_levelAuthorName"), StringComparison.Ordinal)) {
                return;
            }

            mapEditor.SetMapValue("_levelAuthorName", JToken.FromObject(mapperName));
        };

        TxtSongBpm = CreateTextBox("txtSongBPM");
        TxtSongBpm.LostFocus += (_, _) => CommitSongBpm();
        TxtSongBpm.KeyDown += OnNumericTextBoxKeyDown;

        ComboEnvironment = AutomationHelper.WithAutomationId(new ComboBox {
            Name = "comboEnvironment",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            FontSize = 10.5
        }, "comboEnvironment");
        ComboEnvironment.SelectionChanged += (_, _) => {
            if (suppressControlEvents || mapEditor == null || ComboEnvironment.SelectedItem is not string environment) {
                return;
            }

            if (string.Equals(environment, GetMapString("_environmentName"), StringComparison.Ordinal)) {
                return;
            }

            mapEditor.SetMapValue("_environmentName", JToken.FromObject(environment));
        };

        CheckExplicitContent = CreateCheckBox("checkExplicitContent", (_, _) => {
            if (suppressControlEvents || mapEditor == null) {
                return;
            }

            var explicitContent = (CheckExplicitContent.IsChecked ?? false).ToString().ToLowerInvariant();
            if (string.Equals(explicitContent, GetMapString("_explicit"), StringComparison.OrdinalIgnoreCase)) {
                return;
            }

            mapEditor.SetMapValue("_explicit", JToken.FromObject(explicitContent));
        });

        TxtSongFileName = AutomationHelper.WithAutomationId(new TextBlock {
            Name = "txtSongFileName",
            Width = 115,
            FontSize = 11,
            FontStyle = FontStyle.Italic,
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Right,
            HorizontalAlignment = HorizontalAlignment.Stretch
        }, "txtSongFileName");

        TxtCoverFileName = AutomationHelper.WithAutomationId(new TextBlock {
            Name = "txtCoverFileName",
            Width = 115,
            FontSize = 11,
            FontStyle = FontStyle.Italic,
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Right,
            HorizontalAlignment = HorizontalAlignment.Stretch
        }, "txtCoverFileName");

        BtnPickSong = CreateIconButton("btnPickSong", "openMap.png", 20, 20, "Choose song file");
        BtnPickSong.Click += async (_, _) => {
            var selection = await appSession.PickSongFileAsync(this);
            if (!string.IsNullOrWhiteSpace(selection)) {
                ReplaceSong(selection);
            }
        };

        BtnPickCover = CreateIconButton("btnPickCover", "openMap.png", 20, 20, "Choose cover image");
        BtnPickCover.Click += async (_, _) => {
            var selection = await appSession.PickCoverFileAsync(this);
            if (!string.IsNullOrWhiteSpace(selection)) {
                ReplaceCover(selection);
            }
        };

        BtnMakePreview = AutomationHelper.WithAutomationId(new Button {
            Name = "btnMakePreview",
            Content = "Create Song Preview",
            HorizontalAlignment = HorizontalAlignment.Stretch
        }, "btnMakePreview");
        BtnMakePreview.Click += (_, _) => OpenSongPreviewWindow();

        BtnPlayPreview = CreateIconButton("btnPlayPreview", "playButton.png", 20, 20, "Play preview");
        BtnPlayPreview.Click += (_, _) => songPreviewController?.TogglePreview();

        ImgCover = AutomationHelper.WithAutomationId(new global::Avalonia.Controls.Image {
            Name = "imgCover",
            Width = 175,
            Height = 175,
            Stretch = Stretch.UniformToFill
        }, "imgCover");

        DifficultyPrediction = AutomationHelper.WithAutomationId(new TextBlock {
            Name = "difficultyPrediction",
            Text = "Difficulty: 0",
            IsVisible = false,
            FontSize = 11,
            Margin = new Thickness(0, 5, 5, 0),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
            TextAlignment = TextAlignment.Right
        }, "difficultyPrediction");

        var songTempoRow = new StackPanel {
            Orientation = Orientation.Horizontal,
            Spacing = 6
        };
        TxtSongBpm.Width = 40;
        TxtSongBpm.MinWidth = 40;
        songTempoRow.Children.Add(TxtSongBpm);
        songTempoRow.Children.Add(AutomationHelper.WithAutomationId(new TextBlock {
            Name = "lblSongTempoUnit",
            Text = "BPM",
            VerticalAlignment = VerticalAlignment.Center
        }, "lblSongTempoUnit"));

        panel.Children.Add(CreateField("Song Name", TxtSongName, new Thickness(10, 0, 5, 0)));
        panel.Children.Add(CreateField("Artist Name", TxtArtistName, new Thickness(10, 0, 5, 0)));
        panel.Children.Add(CreateField("Mapper Name", TxtMapperName, new Thickness(10, 0, 5, 0)));
        panel.Children.Add(CreateField("Environment", ComboEnvironment, new Thickness(10, 0, 5, 0)));
        panel.Children.Add(CreateField("Song Tempo", songTempoRow, new Thickness(10, 0, 5, 0)));
        panel.Children.Add(CreateField("Explicit Content", CheckExplicitContent, new Thickness(10, 0, 5, 0), labelFontSize: 11.5));
        panel.Children.Add(CreateSectionDivider());
        panel.Children.Add(CreateSectionHeader("File Info", "lblFileInfoHeader", new Thickness(5, 5, 0, 0)));
        panel.Children.Add(CreateFilePickerRow("Song File", TxtSongFileName, BtnPickSong));
        panel.Children.Add(CreatePreviewActionsRow());
        panel.Children.Add(CreateFilePickerRow("Image", TxtCoverFileName, BtnPickCover));
        panel.Children.Add(new Border {
            Margin = new Thickness(0, 3, 0, 0),
            Width = 175,
            Height = 175,
            HorizontalAlignment = HorizontalAlignment.Center,
            BorderBrush = new SolidColorBrush(AvaloniaColor.Parse("#6D6E73")),
            BorderThickness = new Thickness(1),
            Child = ImgCover
        });
        panel.Children.Add(CreateSectionDivider());
        panel.Children.Add(CreateStatsHeader());
        panel.Children.Add(BuildStatsPanel());

        return new ScrollViewer {
            Content = panel,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden,
            ClipToBounds = true
        };
    }

    Control BuildCenterPanel() {
        editorPanel = AutomationHelper.WithAutomationId(new Grid {
            Name = "EditorPanel",
            MinWidth = 300,
            ClipToBounds = true,
            Background = new SolidColorBrush(AvaloniaColor.Parse("#5E80B4")),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto")
        }, "EditorPanel");
        editorPanel.PropertyChanged += OnEditorLayoutContainerPropertyChanged;

        var backgroundTexture = GetResourceBitmap("waterTexture.png");
        if (backgroundTexture != null) {
            var backgroundImage = new global::Avalonia.Controls.Image {
                Source = backgroundTexture,
                Stretch = Stretch.Fill,
                Opacity = 0.4,
                IsHitTestVisible = false
            };
            Grid.SetColumnSpan(backgroundImage, 3);
            editorPanel.Children.Add(backgroundImage);
        }

        gridSpectrogram = AutomationHelper.WithAutomationId(new Grid {
            Name = "gridSpectrogram",
            HorizontalAlignment = HorizontalAlignment.Left,
            ColumnDefinitions = new ColumnDefinitions($"{spectrogramPreferredWidth.ToString(CultureInfo.InvariantCulture)},{SpectrogramResizeGripWidth.ToString(CultureInfo.InvariantCulture)}"),
            ClipToBounds = true
        }, "gridSpectrogram");
        gridSpectrogram.Width = spectrogramPreferredWidth + SpectrogramResizeGripWidth;
        gridSpectrogram.AddHandler(InputElement.PointerPressedEvent, OnSpectrogramResizePointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        gridSpectrogram.AddHandler(InputElement.PointerMovedEvent, OnSpectrogramResizePointerMoved, RoutingStrategies.Tunnel, handledEventsToo: true);
        gridSpectrogram.AddHandler(InputElement.PointerReleasedEvent, OnSpectrogramResizePointerReleased, RoutingStrategies.Tunnel, handledEventsToo: true);
        borderSpectrogram = AutomationHelper.WithAutomationId(new Border {
            Name = "borderSpectrogram",
            Background = new SolidColorBrush(AvaloniaColor.Parse("#030611")),
            BorderBrush = new SolidColorBrush(AvaloniaColor.Parse("#6D6E73")),
            BorderThickness = new Thickness(0, 0, 1, 0)
        }, "borderSpectrogram");
        spectrogramCanvas = AutomationHelper.WithAutomationId(new Canvas {
            Name = "panelSpectrogram",
            Width = 100,
            ClipToBounds = true
        }, "panelSpectrogram");
        spectrogramPlaybackScrollTransform = new TranslateTransform();
        spectrogramCanvas.RenderTransform = spectrogramPlaybackScrollTransform;
        scrollSpectrogram = AutomationHelper.WithAutomationId(new ScrollViewer {
            Name = "scrollSpectrogram",
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
            Content = spectrogramCanvas
        }, "scrollSpectrogram");
        scrollSpectrogram.PropertyChanged += OnEditorScrollViewerPropertyChanged;
        scrollSpectrogram.AddHandler(InputElement.PointerPressedEvent, OnSpectrogramResizePointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        scrollSpectrogram.AddHandler(InputElement.PointerMovedEvent, OnSpectrogramResizePointerMoved, RoutingStrategies.Tunnel, handledEventsToo: true);
        scrollSpectrogram.AddHandler(InputElement.PointerReleasedEvent, OnSpectrogramResizePointerReleased, RoutingStrategies.Tunnel, handledEventsToo: true);
        borderSpectrogram.Child = scrollSpectrogram;
        gridSpectrogram.Children.Add(borderSpectrogram);
        spectrogramResize = AutomationHelper.WithAutomationId(new Button {
            Name = "spectrogramResize",
            Width = SpectrogramResizeGripWidth,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Background = new SolidColorBrush(AvaloniaColor.Parse("#556D6E73")),
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0),
            Cursor = new Cursor(StandardCursorType.SizeWestEast)
        }, "spectrogramResize");
        Grid.SetColumn(spectrogramResize, 1);
        spectrogramResize.SetValue(Visual.ZIndexProperty, 5);
        spectrogramResize.AddHandler(InputElement.PointerPressedEvent, OnSpectrogramResizePointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        spectrogramResize.AddHandler(InputElement.PointerMovedEvent, OnSpectrogramResizePointerMoved, RoutingStrategies.Tunnel, handledEventsToo: true);
        spectrogramResize.AddHandler(InputElement.PointerReleasedEvent, OnSpectrogramResizePointerReleased, RoutingStrategies.Tunnel, handledEventsToo: true);
        gridSpectrogram.Children.Add(spectrogramResize);
        editorPanel.Children.Add(gridSpectrogram);

        editorViewport = new Grid {
            ClipToBounds = true,
            MinWidth = EditorSurfaceMinWidth,
            MaxWidth = EditorSurfaceMaxWidth,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        editorViewport.PropertyChanged += OnEditorLayoutContainerPropertyChanged;
        Grid.SetColumn(editorViewport, 1);

        editorMarginGrid = new Grid {
            Margin = new Thickness(EditorPanelSideMargin, 0, EditorPanelSideMargin, 0),
            ClipToBounds = true,
            IsHitTestVisible = false
        };
        for (var columnIndex = 0; columnIndex < 9; columnIndex++) {
            editorMarginGrid.ColumnDefinitions.Add(new ColumnDefinition(columnIndex % 2 == 0 ? 1 : 3, GridUnitType.Star));
        }

        editorMarginGrid.Children.Add(new Border {
            Background = new SolidColorBrush(AvaloniaColor.Parse("#14000000"))
        });
        for (var laneIndex = 0; laneIndex < 4; laneIndex++) {
            var laneShade = new Border {
                Background = new LinearGradientBrush {
                    StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                    GradientStops = new GradientStops {
                        new GradientStop(AvaloniaColor.Parse("#05000000"), 0),
                        new GradientStop(AvaloniaColor.Parse("#42000000"), 1)
                    }
                }
            };
            Grid.SetColumn(laneShade, laneIndex * 2 + 1);
            editorMarginGrid.Children.Add(laneShade);
        }
        editorViewport.Children.Add(editorMarginGrid);
        editorMarginGrid.SetValue(Visual.ZIndexProperty, 0);

        drumGrid = new Grid {
            Margin = new Thickness(EditorPanelSideMargin, 0, EditorPanelSideMargin, 0),
            VerticalAlignment = VerticalAlignment.Bottom,
            Height = 120,
            ClipToBounds = true,
            IsHitTestVisible = false
        };
        for (var columnIndex = 0; columnIndex < 9; columnIndex++) {
            drumGrid.ColumnDefinitions.Add(new ColumnDefinition(columnIndex % 2 == 0 ? 1 : 3, GridUnitType.Star));
        }

        lineBeatScan = AutomationHelper.WithAutomationId(new Line {
            Name = "lineBeatScan",
            StartPoint = new Point(0, 0),
            EndPoint = new Point(EditorSurfaceDefaultWidth, 0),
            Stroke = new SolidColorBrush(AvaloniaColor.Parse("#1C255F")),
            StrokeThickness = 2,
            VerticalAlignment = VerticalAlignment.Center
        }, "lineBeatScan");
        Grid.SetColumnSpan(lineBeatScan, 9);
        drumGrid.Children.Add(lineBeatScan);

        drum0 = CreateDrumImage("Drum0");
        drum1 = CreateDrumImage("Drum1");
        drum2 = CreateDrumImage("Drum2");
        drum3 = CreateDrumImage("Drum3");
        AddDrumToLane(drumGrid, drum0, 1);
        AddDrumToLane(drumGrid, drum1, 3);
        AddDrumToLane(drumGrid, drum2, 5);
        AddDrumToLane(drumGrid, drum3, 7);
        editorViewport.Children.Add(drumGrid);
        drumGrid.SetValue(Visual.ZIndexProperty, 1);

        var editorSurface = BuildEditorSurface();
        editorSurface.SetValue(Visual.ZIndexProperty, 2);
        editorViewport.Children.Add(editorSurface);

        editorPanel.Children.Add(editorViewport);

        borderNavWaveform = AutomationHelper.WithAutomationId(new Border {
            Name = "borderNavWaveform",
            Background = new SolidColorBrush(AvaloniaColor.Parse("#24000000")),
            BorderBrush = new SolidColorBrush(AvaloniaColor.Parse("#6D6E73")),
            BorderThickness = new Thickness(1, 0, 0, 0)
        }, "borderNavWaveform");
        Grid.SetColumn(borderNavWaveform, 2);

        ImgWaveformVertical = AutomationHelper.WithAutomationId(new Button {
            Name = "imgWaveformVertical",
            Width = 64,
            Padding = new Thickness(0),
            BorderThickness = new Thickness(0),
            Background = Brushes.Transparent,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        }, "imgWaveformVertical");
        ImgWaveformVertical.AddHandler(InputElement.PointerPressedEvent, OnNavWaveformPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        ImgWaveformVertical.AddHandler(InputElement.PointerMovedEvent, OnNavWaveformPointerMoved, RoutingStrategies.Tunnel, handledEventsToo: true);
        ImgWaveformVertical.AddHandler(InputElement.PointerReleasedEvent, OnNavWaveformPointerReleased, RoutingStrategies.Tunnel, handledEventsToo: true);
        ImgWaveformVertical.AddHandler(InputElement.PointerEnteredEvent, OnNavWaveformPointerEntered, RoutingStrategies.Tunnel, handledEventsToo: true);
        ImgWaveformVertical.AddHandler(InputElement.PointerExitedEvent, OnNavWaveformPointerExited, RoutingStrategies.Tunnel, handledEventsToo: true);

        navWaveformVisualHost = new Grid {
            Width = 64,
            ClipToBounds = true,
            Background = new SolidColorBrush(AvaloniaColor.Parse("#183F8A"))
        };

        var waveformTexture = GetResourceBitmap("waterTextureSmall.png");
        if (waveformTexture != null) {
            navWaveformVisualHost.Children.Add(new global::Avalonia.Controls.Image {
                Source = waveformTexture,
                Stretch = Stretch.Fill,
                Opacity = 0.18,
                IsHitTestVisible = false
            });
        }

        navWaveformBackdrop = new Canvas {
            IsHitTestVisible = false
        };
        CanvasNavNotes = AutomationHelper.WithAutomationId(new Canvas {
            Name = "canvasNavNotes",
            IsHitTestVisible = false
        }, "canvasNavNotes");
        CanvasBookmarks = AutomationHelper.WithAutomationId(new Canvas {
            Name = "canvasBookmarks",
            IsHitTestVisible = false
        }, "canvasBookmarks");
        CanvasTimingChanges = AutomationHelper.WithAutomationId(new Canvas {
            Name = "canvasTimingChanges",
            IsHitTestVisible = false
        }, "canvasTimingChanges");
        lineSongMouseover = AutomationHelper.WithAutomationId(new Line {
            Name = "lineSongMouseover",
            Stroke = Brushes.White,
            StrokeThickness = 1,
            Opacity = 0
        }, "lineSongMouseover");
        lineSongProgress = AutomationHelper.WithAutomationId(new Line {
            Name = "lineSongProgress",
            Stroke = new SolidColorBrush(AvaloniaColor.Parse("#001C70")),
            StrokeThickness = 1.5
        }, "lineSongProgress");
        canvasBookmarkLabels = AutomationHelper.WithAutomationId(new Canvas {
            Name = "canvasBookmarkLabels",
            IsHitTestVisible = false
        }, "canvasBookmarkLabels");
        canvasTimingChangeLabels = AutomationHelper.WithAutomationId(new Canvas {
            Name = "canvasTimingChangeLabels",
            IsHitTestVisible = false
        }, "canvasTimingChangeLabels");
        CanvasNavInputBox = AutomationHelper.WithAutomationId(new Canvas {
            Name = "canvasNavInputBox",
            Background = Brushes.Transparent
        }, "canvasNavInputBox");

        navWaveformVisualHost.Children.Add(navWaveformBackdrop);
        navWaveformVisualHost.Children.Add(CanvasNavNotes);
        navWaveformVisualHost.Children.Add(lineSongMouseover);
        navWaveformVisualHost.Children.Add(CanvasBookmarks);
        navWaveformVisualHost.Children.Add(CanvasTimingChanges);
        navWaveformVisualHost.Children.Add(lineSongProgress);
        navWaveformVisualHost.Children.Add(canvasBookmarkLabels);
        navWaveformVisualHost.Children.Add(canvasTimingChangeLabels);
        navWaveformVisualHost.Children.Add(CanvasNavInputBox);
        ImgWaveformVertical.Content = navWaveformVisualHost;
        borderNavWaveform.Child = ImgWaveformVertical;
        editorPanel.Children.Add(borderNavWaveform);

        return editorPanel;
    }

    Control BuildBottomPlaybackPanel() {
        var border = new Border {
            BorderBrush = new SolidColorBrush(AvaloniaColor.Parse("#6D6E73")),
            BorderThickness = new Thickness(0, 1, 0, 0),
            Background = new LinearGradientBrush {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                GradientStops = new GradientStops {
                    new GradientStop(AvaloniaColor.Parse("#F4F4F4"), 0),
                    new GradientStop(AvaloniaColor.Parse("#D9D9D9"), 1)
                }
            }
        };

        var playbackPanel = new Grid {
            Margin = new Thickness(12, 8),
            ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*,Auto")
        };

        BtnSongPlayer = CreateIconButton("btnSongPlayer", "playButton.png", 30, 30, "Play song");
        BtnSongPlayer.Click += (_, _) => ToggleSongPlayback();
        playbackPanel.Children.Add(BtnSongPlayer);

        TxtSongPosition = AutomationHelper.WithAutomationId(new TextBlock {
            Name = "txtSongPosition",
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0, 12, 0),
            Text = Helper.TimeFormat(0)
        }, "txtSongPosition");
        Grid.SetColumn(TxtSongPosition, 1);
        playbackPanel.Children.Add(TxtSongPosition);

        SliderSongProgress = AutomationHelper.WithAutomationId(new Slider {
            Name = "sliderSongProgress",
            Minimum = 0,
            Maximum = 1000,
            Value = 0
        }, "sliderSongProgress");
        SliderSongProgress.PropertyChanged += OnSongProgressSliderPropertyChanged;
        Grid.SetColumn(SliderSongProgress, 2);
        playbackPanel.Children.Add(SliderSongProgress);

        TxtSongDuration = AutomationHelper.WithAutomationId(new TextBlock {
            Name = "txtSongDuration",
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0, 0, 0),
            Text = Helper.TimeFormat(0)
        }, "txtSongDuration");
        Grid.SetColumn(TxtSongDuration, 3);
        playbackPanel.Children.Add(TxtSongDuration);

        border.Child = playbackPanel;
        return border;
    }

    Control BuildRightSidebar() {
        var panel = new StackPanel {
            Spacing = 0,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        panel.Children.Add(CreateSectionHeader("Difficulty Settings", "lblDifficultySettingsHeader", new Thickness(5, 3, 0, 5)));

        var difficultyButtons = new StackPanel {
            Orientation = Orientation.Horizontal,
            Spacing = 0
        };

        BtnChangeDifficulty0 = BuildDifficultyButton("btnChangeDifficulty0", 0);
        BtnChangeDifficulty1 = BuildDifficultyButton("btnChangeDifficulty1", 1);
        BtnChangeDifficulty2 = BuildDifficultyButton("btnChangeDifficulty2", 2);
        difficultySlot0 = CreateDifficultySlot(BtnChangeDifficulty0, "difficultySlot0");
        difficultySlot1 = CreateDifficultySlot(BtnChangeDifficulty1, "difficultySlot1");
        difficultySlot2 = CreateDifficultySlot(BtnChangeDifficulty2, "difficultySlot2");
        difficultyButtons.Children.Add(difficultySlot0);
        difficultyButtons.Children.Add(difficultySlot1);
        difficultyButtons.Children.Add(difficultySlot2);

        var difficultyActions = new StackPanel {
            Orientation = Orientation.Vertical,
            Spacing = 0,
            Margin = new Thickness(5, 0, 0, 0)
        };
        BtnAddDifficulty = CreateIconButton("btnAddDifficulty", "Plus.png", 20, 20, "Add difficulty");
        BtnAddDifficulty.Click += (_, _) => BeginAddDifficulty();
        BtnDeleteDifficulty = CreateIconButton("btnDeleteDifficulty", "Minus.png", 20, 20, "Delete difficulty");
        BtnDeleteDifficulty.Click += (_, _) => BeginDeleteDifficulty();
        difficultyActions.Children.Add(BtnAddDifficulty);
        difficultyActions.Children.Add(BtnDeleteDifficulty);

        var changeDifficultyPanel = new StackPanel {
            Orientation = Orientation.Vertical,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(10, 0, 5, 0)
        };
        changeDifficultyPanel.Children.Add(AutomationHelper.WithAutomationId(new TextBlock {
            Name = "lblChangeDifficulty",
            Text = "Change Difficulty",
            Margin = new Thickness(0, 0, 0, 5),
            HorizontalAlignment = HorizontalAlignment.Center
        }, "lblChangeDifficulty"));
        var difficultyRow = new Grid {
            Width = 190,
            HorizontalAlignment = HorizontalAlignment.Center,
            ColumnDefinitions = new ColumnDefinitions("Auto,Auto")
        };
        difficultyRow.Children.Add(difficultyButtons);
        Grid.SetColumn(difficultyActions, 1);
        difficultyRow.Children.Add(difficultyActions);
        changeDifficultyPanel.Children.Add(difficultyRow);
        panel.Children.Add(changeDifficultyPanel);
        panel.Children.Add(CreateSectionDivider(new Thickness(0, 15, 0, 5), 190));

        TxtDifficultyNumber = CreateTextBox("txtDifficultyNumber");
        TxtDifficultyNumber.Width = 30;
        TxtDifficultyNumber.MinWidth = 30;
        TxtDifficultyNumber.LostFocus += (_, _) => CommitDifficultyNumber();
        TxtDifficultyNumber.KeyDown += OnNumericTextBoxKeyDown;

        TxtNoteSpeed = CreateTextBox("txtNoteSpeed");
        TxtNoteSpeed.Width = 30;
        TxtNoteSpeed.MinWidth = 30;
        TxtNoteSpeed.LostFocus += (_, _) => CommitNoteSpeed();
        TxtNoteSpeed.KeyDown += OnNumericTextBoxKeyDown;

        TxtDistMedal0 = CreateTextBox("txtDistMedal0");
        TxtDistMedal0.Width = 50;
        TxtDistMedal0.MinWidth = 50;
        TxtDistMedal0.GotFocus += (_, _) => ClearAutoText(TxtDistMedal0);
        TxtDistMedal0.LostFocus += (_, _) => CommitMedalDistance(TxtDistMedal0, RagnarockScoreMedals.Bronze);
        TxtDistMedal0.KeyDown += OnNumericTextBoxKeyDown;

        TxtDistMedal1 = CreateTextBox("txtDistMedal1");
        TxtDistMedal1.Width = 50;
        TxtDistMedal1.MinWidth = 50;
        TxtDistMedal1.GotFocus += (_, _) => ClearAutoText(TxtDistMedal1);
        TxtDistMedal1.LostFocus += (_, _) => CommitMedalDistance(TxtDistMedal1, RagnarockScoreMedals.Silver);
        TxtDistMedal1.KeyDown += OnNumericTextBoxKeyDown;

        TxtDistMedal2 = CreateTextBox("txtDistMedal2");
        TxtDistMedal2.Width = 50;
        TxtDistMedal2.MinWidth = 50;
        TxtDistMedal2.GotFocus += (_, _) => ClearAutoText(TxtDistMedal2);
        TxtDistMedal2.LostFocus += (_, _) => CommitMedalDistance(TxtDistMedal2, RagnarockScoreMedals.Gold);
        TxtDistMedal2.KeyDown += OnNumericTextBoxKeyDown;

        panel.Children.Add(CreateField("Difficulty Level", TxtDifficultyNumber, new Thickness(10, 5, 5, 0), valueColumnWidth: 120));
        panel.Children.Add(CreateField("Note Speed", TxtNoteSpeed, new Thickness(10, 0, 5, 0), valueColumnWidth: 120));
        panel.Children.Add(CreateSubsectionHeader("Medal Distances", "lblMedalDistances", new Thickness(10, 3, 0, 0), 14));
        panel.Children.Add(CreateField("Bronze", CreateMeasurementFieldRow(TxtDistMedal0, "lblBronzeDistanceUnit"), new Thickness(10, 0, 5, 0), valueColumnWidth: 120));
        panel.Children.Add(CreateField("Silver", CreateMeasurementFieldRow(TxtDistMedal1, "lblSilverDistanceUnit"), new Thickness(10, 0, 5, 0), valueColumnWidth: 120));
        panel.Children.Add(CreateField("Gold", CreateMeasurementFieldRow(TxtDistMedal2, "lblGoldDistanceUnit"), new Thickness(10, 0, 5, 0), valueColumnWidth: 120));
        panel.Children.Add(CreateSectionDivider());
        panel.Children.Add(CreateSectionHeader("Editor Settings", "lblEditorSettingsHeader", new Thickness(5, 0, 0, 2)));
        panel.Children.Add(BuildEditorSettingsPanel());

        LblSelectedBeat = AutomationHelper.WithAutomationId(new TextBlock {
            Name = "lblSelectedBeat",
            FontSize = 11,
            FontWeight = FontWeight.Bold,
            Margin = new Thickness(5, 4, 0, 0)
        }, "lblSelectedBeat");

        var dock = new DockPanel {
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        var selectedBeatFooter = new Border {
            Height = 25,
            BorderBrush = new SolidColorBrush(AvaloniaColor.Parse("#6D6E73")),
            BorderThickness = new Thickness(0, 1, 0, 0),
            Child = LblSelectedBeat
        };
        DockPanel.SetDock(selectedBeatFooter, Dock.Bottom);
        dock.Children.Add(selectedBeatFooter);
        dock.Children.Add(new ScrollViewer {
            Content = panel,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden,
            ClipToBounds = true
        });
        dock.ClipToBounds = true;
        return dock;
    }

    Button BuildDifficultyButton(string automationId, int difficultyIndex) {
        var rankLabel = difficultyIndex switch {
            0 => lblDifficultyRank1 = CreateDifficultyRankLabel("lblDifficultyRank1"),
            1 => lblDifficultyRank2 = CreateDifficultyRankLabel("lblDifficultyRank2"),
            _ => lblDifficultyRank3 = CreateDifficultyRankLabel("lblDifficultyRank3")
        };
        var button = AutomationHelper.WithAutomationId(new Button {
            Name = automationId,
            Width = 55,
            Height = 40,
            Padding = new Thickness(0),
            Content = new Grid {
                Width = 50,
                Children = {
                    CreateResourceImage($"difficulty{difficultyIndex + 1}.png", 32),
                    rankLabel
                }
            }
        }, automationId);
        AutomationProperties.SetName(button, $"Difficulty {difficultyIndex + 1}");
        button.Click += (_, _) => SelectDifficulty(difficultyIndex);
        return button;
    }

    static TextBlock CreateDifficultyRankLabel(string automationId) {
        return AutomationHelper.WithAutomationId(new TextBlock {
            Name = automationId,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 2, 1),
            FontSize = 12,
            FontWeight = FontWeight.Bold,
            IsHitTestVisible = false
        }, automationId);
    }

    static Border CreateDifficultySlot(Button button, string automationId) {
        return AutomationHelper.WithAutomationId(new Border {
            Name = automationId,
            Width = 55,
            Height = 40,
            Child = button
        }, automationId);
    }

    Control BuildEditorSettingsPanel() {
        var panel = new StackPanel {
            Spacing = 0
        };

        Control CreateSliderValueRow(Slider slider, TextBlock valueText) {
            var row = new Grid {
                ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                ColumnSpacing = 5,
                ClipToBounds = true,
                Margin = new Thickness(0, 0, 10, 0)
            };
            slider.HorizontalAlignment = HorizontalAlignment.Stretch;
            slider.MinWidth = 0;
            valueText.Margin = new Thickness(0);
            Grid.SetColumn(slider, 0);
            row.Children.Add(slider);
            Grid.SetColumn(valueText, 1);
            row.Children.Add(valueText);
            return row;
        }

        SliderSongVol = AutomationHelper.WithAutomationId(new Slider {
            Name = "sliderSongVol",
            Minimum = 0,
            Maximum = 1,
            Value = DefaultUserSettings.DefaultSongVolume
        }, "sliderSongVol");
        SliderSongVol.PropertyChanged += (_, e) => {
            if (e.Property == RangeBase.ValueProperty) {
                UpdateVolumeTexts();
                UpdateAudioVolumes();
            }
        };

        TxtSongVol = AutomationHelper.WithAutomationId(new TextBlock {
            Name = "txtSongVol",
            Width = 34,
            MinWidth = 34,
            FontSize = 10.5,
            TextAlignment = TextAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        }, "txtSongVol");

        SliderDrumVol = AutomationHelper.WithAutomationId(new Slider {
            Name = "sliderDrumVol",
            Minimum = 0,
            Maximum = 1,
            Value = DefaultUserSettings.DefaultNoteVolume
        }, "sliderDrumVol");
        SliderDrumVol.PropertyChanged += (_, e) => {
            if (e.Property == RangeBase.ValueProperty) {
                UpdateVolumeTexts();
                UpdateAudioVolumes();
            }
        };

        TxtDrumVol = AutomationHelper.WithAutomationId(new TextBlock {
            Name = "txtDrumVol",
            Width = 34,
            MinWidth = 34,
            FontSize = 10.5,
            TextAlignment = TextAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        }, "txtDrumVol");

        SliderSongTempo = AutomationHelper.WithAutomationId(new Slider {
            Name = "sliderSongTempo",
            Minimum = Audio.MinSongTempo,
            Maximum = Audio.MaxSongTempo,
            Value = Audio.DefaultSongTempo
        }, "sliderSongTempo");
        SliderSongTempo.PropertyChanged += OnSongTempoSliderPropertyChanged;
        SliderSongTempo.PointerPressed += OnSongTempoSliderPointerPressed;
        SliderSongTempo.DoubleTapped += OnSongTempoSliderDoubleTapped;

        TxtSongTempo = AutomationHelper.WithAutomationId(new TextBlock {
            Name = "txtSongTempo",
            Width = 34,
            MinWidth = 34,
            FontSize = 10.5,
            TextAlignment = TextAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        }, "txtSongTempo");

        CheckMetronome = CreateCheckBox("checkMetronome", (_, _) => {
            if (!suppressControlEvents && metronome != null) {
                metronome.isEnabled = CheckMetronome.IsChecked ?? false;
            }
        });
        CheckWaveform = CreateCheckBox("checkWaveform", (_, _) => RefreshEditorDisplayPreferences());
        CheckGridSnap = CreateCheckBox("checkGridSnap", (_, _) => {
            if (!suppressControlEvents) {
                SetSnapToGrid(CheckGridSnap.IsChecked ?? false);
            }
        });

        TxtGridDivision = CreateTextBox("txtGridDivision");
        TxtGridDivision.MinWidth = 36;
        TxtGridDivision.LostFocus += (_, _) => CommitGridDivision();
        TxtGridDivision.KeyDown += OnNumericTextBoxKeyDown;

        TxtGridSpacing = CreateTextBox("txtGridSpacing");
        TxtGridSpacing.MinWidth = 36;
        TxtGridSpacing.LostFocus += (_, _) => CommitGridSpacing();
        TxtGridSpacing.KeyDown += OnNumericTextBoxKeyDown;

        BtnChangeBPM = AutomationHelper.WithAutomationId(new Button {
            Name = "btnChangeBPM",
            Content = "Edit Song Timing",
            Width = 199,
            HorizontalAlignment = HorizontalAlignment.Center
        }, "btnChangeBPM");
        BtnChangeBPM.Click += (_, _) => OpenChangeBpmWindow();

        BtnCustomizeNavBar = AutomationHelper.WithAutomationId(new Button {
            Name = "btnCustomizeNavBar",
            Content = "Customize Navigation Bar",
            Width = 199,
            HorizontalAlignment = HorizontalAlignment.Center
        }, "btnCustomizeNavBar");
        BtnCustomizeNavBar.Click += (_, _) => OpenCustomizeNavBarWindow();

        var gridDivisionRow = new StackPanel {
            Orientation = Orientation.Horizontal,
            Spacing = 4
        };
        gridDivisionRow.Children.Add(new TextBlock {
            Text = "1/",
            VerticalAlignment = VerticalAlignment.Center
        });
        gridDivisionRow.Children.Add(TxtGridDivision);

        panel.Children.Add(new Border {
            Margin = new Thickness(0, 0, 0, 5),
            Padding = new Thickness(0, 5, 0, 10),
            Child = new StackPanel {
                Children = {
                    BtnChangeBPM,
                    new Border { Height = 5, Background = Brushes.Transparent },
                    BtnCustomizeNavBar
                }
            }
        });
        panel.Children.Add(CreateSubsectionHeader("Playback", "lblPlaybackHeader", new Thickness(10, 0, 0, 5), 14));
        panel.Children.Add(CreateField("Song Volume", CreateSliderValueRow(SliderSongVol, TxtSongVol), new Thickness(10, 0, 5, 0), valueColumnWidth: 115));
        panel.Children.Add(CreateField("Note Volume", CreateSliderValueRow(SliderDrumVol, TxtDrumVol), new Thickness(10, 0, 5, 0), valueColumnWidth: 115));
        panel.Children.Add(CreateField("Song Speed", CreateSliderValueRow(SliderSongTempo, TxtSongTempo), new Thickness(10, 0, 5, 0), valueColumnWidth: 115));
        panel.Children.Add(CreateField("Metronome", CheckMetronome, new Thickness(10, 0, 5, 0), valueColumnWidth: 115));
        panel.Children.Add(CreateSubsectionHeader("Editing Grid", "lblEditingGridHeader", new Thickness(10, 3, 0, 0), 14));
        panel.Children.Add(CreateField("Snap to Grid", CheckGridSnap, new Thickness(10, 0, 5, 0), valueColumnWidth: 115));
        panel.Children.Add(CreateField("Beat Division", gridDivisionRow, new Thickness(10, 0, 5, 0), valueColumnWidth: 115));
        panel.Children.Add(CreateField("Grid Spacing", TxtGridSpacing, new Thickness(10, 0, 5, 0), valueColumnWidth: 115));
        panel.Children.Add(CreateField("Grid Waveform", CheckWaveform, new Thickness(10, 0, 5, 0), labelFontSize: 11, valueColumnWidth: 115));

        return panel;
    }

    static TextBlock CreateSectionHeader(string title, string? automationId = null, Thickness? margin = null) {
        var textBlock = new TextBlock {
            Text = title,
            FontSize = 16,
            FontWeight = FontWeight.Bold,
            FontFamily = new FontFamily("Bahnschrift"),
            Foreground = new SolidColorBrush(AvaloniaColor.Parse("#002668")),
            Margin = margin ?? default
        };
        return automationId == null ? textBlock : AutomationHelper.WithAutomationId(textBlock, automationId);
    }

    static TextBlock CreateSubsectionHeader(string title, string? automationId = null, Thickness? margin = null, double fontSize = 13) {
        var textBlock = new TextBlock {
            Text = title,
            FontSize = fontSize,
            FontWeight = FontWeight.Bold,
            FontFamily = new FontFamily("Bahnschrift"),
            Foreground = Brushes.Black,
            Margin = margin ?? new Thickness(0, 0, 0, 5)
        };
        return automationId == null ? textBlock : AutomationHelper.WithAutomationId(textBlock, automationId);
    }

    static Border CreateSectionDivider(Thickness? margin = null, double? width = null) {
        return new Border {
            Height = 1,
            Width = width ?? double.NaN,
            HorizontalAlignment = width.HasValue ? HorizontalAlignment.Center : HorizontalAlignment.Stretch,
            Background = new SolidColorBrush(AvaloniaColor.Parse("#6D6E73")),
            Margin = margin ?? new Thickness(0, 10, 0, 5)
        };
    }

    Control CreatePreviewActionsRow() {
        var row = new Grid {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            ColumnSpacing = 5,
            Margin = new Thickness(10, 5, 5, 5)
        };
        row.Children.Add(BtnMakePreview);
        Grid.SetColumn(BtnPlayPreview, 1);
        row.Children.Add(BtnPlayPreview);
        return row;
    }

    Control CreateStatsHeader() {
        var header = new Grid {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            ColumnSpacing = 8
        };
        header.Children.Add(CreateSectionHeader("Map Stats", "lblMapStatsHeader", new Thickness(5, 5, 0, 0)));
        Grid.SetColumn(DifficultyPrediction, 1);
        header.Children.Add(DifficultyPrediction);
        return header;
    }

    Control CreateFilePickerRow(string label, TextBlock fileName, Button actionButton) {
        var row = new Grid {
            ColumnDefinitions = new ColumnDefinitions("*,115,Auto"),
            ColumnSpacing = 5,
            Margin = new Thickness(10, 0, 5, 0),
            ClipToBounds = true
        };
        row.Children.Add(new TextBlock {
            Text = label,
            FontSize = 10.5,
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.NoWrap
        });
        Grid.SetColumn(fileName, 1);
        row.Children.Add(fileName);
        Grid.SetColumn(actionButton, 2);
        row.Children.Add(actionButton);
        return row;
    }

    Button CreateIconButton(string automationId, string resourceFileName, double width, double height, string accessibleName) {
        var button = AutomationHelper.WithAutomationId(new Button {
            Name = automationId,
            Width = width,
            Height = height,
            Padding = new Thickness(2),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center
        }, automationId);
        SetButtonIcon(button, resourceFileName, accessibleName, Math.Max(14, Math.Min(width, height) - 4));
        return button;
    }

    void SetButtonIcon(Button button, string resourceFileName, string accessibleName, double iconSize) {
        button.Content = CreateResourceImage(resourceFileName, iconSize);
        AutomationProperties.SetName(button, accessibleName);
    }

    global::Avalonia.Controls.Image CreateResourceImage(string resourceFileName, double size) {
        return new global::Avalonia.Controls.Image {
            Source = GetResourceBitmap(resourceFileName),
            Width = size,
            Height = size,
            Stretch = Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
    }

    Bitmap? GetResourceBitmap(string resourceFileName) {
        if (resourceBitmapCache.TryGetValue(resourceFileName, out var cachedBitmap)) {
            return cachedBitmap;
        }

        var loadedBitmap = TryLoadBitmap(Path.Combine(AppContext.BaseDirectory, "Resources", resourceFileName));
        resourceBitmapCache[resourceFileName] = loadedBitmap;
        return loadedBitmap;
    }

    static Bitmap? TryLoadBitmap(string path) {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) {
            return null;
        }

        try {
            using var stream = File.OpenRead(path);
            return new Bitmap(stream);
        } catch {
            return null;
        }
    }

    TextBox CreateTextBox(string automationId) {
        var textBox = AutomationHelper.WithAutomationId(new TextBox {
            Name = automationId,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            MinHeight = 22,
            Padding = new Thickness(3, 1),
            FontSize = 10.5
        }, automationId);
        textBox.GotFocus += (_, _) => textInputHasFocus = true;
        textBox.LostFocus += (_, _) => textInputHasFocus = false;
        return textBox;
    }

    CheckBox CreateCheckBox(string automationId, EventHandler<RoutedEventArgs> onChanged) {
        var checkBox = AutomationHelper.WithAutomationId(new CheckBox {
            Name = automationId,
            VerticalAlignment = VerticalAlignment.Center,
            Focusable = false
        }, automationId);
        checkBox.IsCheckedChanged += onChanged;
        return checkBox;
    }

    static Control CreateField(string label, Control value, Thickness? margin = null, double? labelFontSize = null, double valueColumnWidth = 120) {
        var panel = new Grid {
            ColumnDefinitions = new ColumnDefinitions($"*,{valueColumnWidth}"),
            ColumnSpacing = 0,
            Margin = margin ?? new Thickness(10, 0, 5, 0),
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        panel.Children.Add(new TextBlock {
            Text = label,
            FontSize = labelFontSize ?? 10.75,
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.NoWrap
        });
        Grid.SetColumn(value, 1);
        panel.Children.Add(value);
        return panel;
    }

    Control CreateMeasurementFieldRow(TextBox valueBox, string automationId) {
        var row = new StackPanel {
            Orientation = Orientation.Horizontal,
            Spacing = 5
        };
        row.Children.Add(valueBox);
        row.Children.Add(AutomationHelper.WithAutomationId(new TextBlock {
            Name = automationId,
            Text = "m",
            Width = 10,
            VerticalAlignment = VerticalAlignment.Center
        }, automationId));
        return row;
    }

    global::Avalonia.Controls.Image CreateDrumImage(string automationId) {
        return AutomationHelper.WithAutomationId(new global::Avalonia.Controls.Image {
            Name = automationId,
            Source = GetResourceBitmap("drum.png"),
            Height = 72,
            Stretch = Stretch.Uniform,
            VerticalAlignment = VerticalAlignment.Bottom
        }, automationId);
    }

    static void AddDrumToLane(Grid drumGrid, global::Avalonia.Controls.Image drumImage, int column) {
        Grid.SetColumn(drumImage, column);
        drumGrid.Children.Add(drumImage);
    }

    void LoadMap(string mapFolder) {
        SuspendDifficultyPredictionUpdates();
        try {
            PauseSong();
            mapEditor?.Dispose();
            mapEditor = new MapEditor(mapEditorUiAdapter, mapFolder, makeNewMap: false);
            mapEditor.SelectDifficulty(0);
            var mapDirtyState = mapEditor.needsSave;

            suppressControlEvents = true;

            PopulateEnvironmentOptions();
            TxtSongName.Text = GetMapString("_songName");
            TxtArtistName.Text = GetMapString("_songAuthorName");
            TxtMapperName.Text = GetMapString("_levelAuthorName");
            TxtSongBpm.Text = FormatNumber(GetMapDouble("_beatsPerMinute"));
            ComboEnvironment.SelectedItem = GetMapString("_environmentName");
            CheckExplicitContent.IsChecked = GetMapBoolString("_explicit");
            TxtSongFileName.Text = GetMapString("_songFilename");
            TxtCoverFileName.Text = FormatCoverFileName(GetMapString("_coverImageFilename"));
            LoadDifficultyIntoControls(0);
            UpdateCoverPreview();

            LoadSongAudio();
            mapEditor.GlobalBPM = GetMapDouble("_beatsPerMinute");
            mapEditor.SongDuration = currentSongDurationSeconds;
            SliderSongProgress.Maximum = currentSongDurationSeconds * 1000;
            SetSongPosition(0, updateSlider: true, updateNavWaveform: false);

            suppressControlEvents = false;
            RefreshDifficultyButtons();
            RefreshEditorSurface();
            QueuePostLayoutEditorSync();
            mapEditor.needsSave = mapDirtyState;
            UpdateDifficultyPrediction();
            songPreviewController?.Restart(mapEditor, songIsPlaying);
            RestartDrummer();
            RestartMetronome();
        } finally {
            ResumeDifficultyPredictionUpdates();
        }
    }

    void PopulateEnvironmentOptions() {
        ComboEnvironment.Items.Clear();
        foreach (var environment in BeatmapDefaults.EnvironmentNames) {
            ComboEnvironment.Items.Add(environment);
        }
    }

    void LoadDifficultyIntoControls(int difficultyIndex) {
        if (mapEditor == null) {
            return;
        }

        mapEditor.SelectDifficulty(difficultyIndex);
        TxtDifficultyNumber.Text = GetMapString("_difficultyRank", (RagnarockMapDifficulties)difficultyIndex);
        TxtNoteSpeed.Text = GetMapString("_noteJumpMovementSpeed", (RagnarockMapDifficulties)difficultyIndex);
        TxtGridSpacing.Text = GetMapString("_editorGridSpacing", (RagnarockMapDifficulties)difficultyIndex, custom: true);
        TxtGridDivision.Text = GetMapString("_editorGridDivision", (RagnarockMapDifficulties)difficultyIndex, custom: true);
        mapEditor.defaultGridDivision = CurrentGridDivision;
        TxtDistMedal0.Text = FormatMedalDistance(mapEditor.GetMedalDistance(RagnarockScoreMedals.Bronze, (RagnarockMapDifficulties)difficultyIndex));
        TxtDistMedal1.Text = FormatMedalDistance(mapEditor.GetMedalDistance(RagnarockScoreMedals.Silver, (RagnarockMapDifficulties)difficultyIndex));
        TxtDistMedal2.Text = FormatMedalDistance(mapEditor.GetMedalDistance(RagnarockScoreMedals.Gold, (RagnarockMapDifficulties)difficultyIndex));
        RefreshEditorSurface();
        UpdateDifficultyPrediction();
    }

    void RefreshDifficultyButtons() {
        if (mapEditor == null) {
            return;
        }

        var count = mapEditor.numDifficulties;
        var selectedDifficultyIndex = mapEditor.currentDifficultyIndex;
        var difficultyButtons = new[] {
            BtnChangeDifficulty0,
            BtnChangeDifficulty1,
            BtnChangeDifficulty2
        };
        var difficultyLabels = new[] {
            lblDifficultyRank1,
            lblDifficultyRank2,
            lblDifficultyRank3
        };
        var selectedBrush = new SolidColorBrush(AvaloniaColor.Parse(Editor.Difficulty.SelectedColour));
        var defaultBrush = new SolidColorBrush(AvaloniaColor.Parse("#F0F0F0"));
        for (var difficultyIndex = 0; difficultyIndex < difficultyButtons.Length; difficultyIndex++) {
            var button = difficultyButtons[difficultyIndex];
            var isAvailable = difficultyIndex < count;
            var isSelected = difficultyIndex == selectedDifficultyIndex;
            button.IsVisible = isAvailable;
            button.Opacity = 1;
            button.IsHitTestVisible = !songIsPlaying && isAvailable && !isSelected;
            button.IsEnabled = !songIsPlaying && isAvailable;
            button.Focusable = button.IsEnabled;
            button.Background = isSelected ? selectedBrush : defaultBrush;
            difficultyLabels[difficultyIndex].Text = isAvailable
                ? GetMapString("_difficultyRank", (RagnarockMapDifficulties)difficultyIndex)
                : string.Empty;
            difficultyLabels[difficultyIndex].IsVisible = isAvailable;
        }

        BtnAddDifficulty.IsEnabled = !songIsPlaying && count < 3;
        BtnAddDifficulty.Focusable = BtnAddDifficulty.IsEnabled;
        BtnDeleteDifficulty.IsEnabled = !songIsPlaying && count > 1;
        BtnDeleteDifficulty.Focusable = BtnDeleteDifficulty.IsEnabled;
    }

    void ToggleSongPlayback() {
        if (songIsPlaying) {
            PauseSong();
            return;
        }

        PlaySong();
    }

    void UpdatePlaybackUi() {
        SetButtonIcon(BtnSongPlayer, songIsPlaying ? "pauseButton.png" : "playButton.png", songIsPlaying ? "Pause song" : "Play song", 18);
        TxtSongBpm.IsEnabled = !songIsPlaying;
        BtnChangeBPM.IsEnabled = !songIsPlaying;
        SliderSongTempo.IsEnabled = !songIsPlaying;
        SliderSongTempo.Focusable = !songIsPlaying;
        SliderSongProgress.IsEnabled = !songIsPlaying;
        SliderSongProgress.Focusable = !songIsPlaying;
        if (mapEditor != null) {
            RefreshDifficultyButtons();
        } else {
            BtnAddDifficulty.IsEnabled = false;
            BtnAddDifficulty.Focusable = false;
            BtnDeleteDifficulty.IsEnabled = false;
            BtnDeleteDifficulty.Focusable = false;
            BtnChangeDifficulty0.IsEnabled = false;
            BtnChangeDifficulty0.Focusable = false;
            BtnChangeDifficulty1.IsEnabled = false;
            BtnChangeDifficulty1.Focusable = false;
            BtnChangeDifficulty2.IsEnabled = false;
            BtnChangeDifficulty2.Focusable = false;
        }
        BtnPlayPreview.IsEnabled = !songIsPlaying;
        BtnPlayPreview.Focusable = BtnPlayPreview.IsEnabled;
        BtnCustomizeNavBar.IsEnabled = !songIsPlaying;
        BtnCustomizeNavBar.Focusable = BtnCustomizeNavBar.IsEnabled;
        BtnMakePreview.IsEnabled = !songIsPlaying;
        BtnMakePreview.Focusable = BtnMakePreview.IsEnabled;
        if (scrollEditor != null) {
            scrollEditor.IsEnabled = true;
            scrollEditor.IsHitTestVisible = !songIsPlaying;
            scrollEditor.Focusable = !songIsPlaying;
        }
        UpdateSpectrogramInterpolationMode();
        if (songIsPlaying) {
            HideEditorHoverVisuals();
        }
    }

    void OnSongProgressSliderPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e) {
        if (e.Property == RangeBase.ValueProperty && !suppressControlEvents) {
            SetSongPosition(SliderSongProgress.Value, updateSlider: false, updateNavWaveform: false);
        }
    }

    void OnSongTempoSliderPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e) {
        if (e.Property == RangeBase.ValueProperty) {
            noteScanner?.SetTempo(SliderSongTempo.Value);
            beatScanner?.SetTempo(SliderSongTempo.Value);
            UpdateSongTempoText();
        }
    }

    void OnSongTempoSliderPointerPressed(object? sender, PointerPressedEventArgs e) {
        if (e.ClickCount == 2) {
            ResetSongTempoToDefault();
            e.Handled = true;
        }
    }

    void OnSongTempoSliderDoubleTapped(object? sender, TappedEventArgs e) {
        ResetSongTempoToDefault();
        e.Handled = true;
    }

    void ResetSongTempoToDefault() {
        suppressControlEvents = true;
        SliderSongTempo.Value = Audio.DefaultSongTempo;
        suppressControlEvents = false;
        UpdateSongTempoText();
    }

    void UpdateSongTempoText() {
        TxtSongTempo.Text = $"{SliderSongTempo.Value:0.##}x";
    }

    void UpdateVolumeTexts() {
        TxtSongVol.Text = $"{Math.Round(SliderSongVol.Value * 100):0}%";
        TxtDrumVol.Text = $"{Math.Round(SliderDrumVol.Value * 100):0}%";
    }

    void SetSongPosition(double milliseconds, bool updateSlider = true, bool updateNavWaveform = true, bool updateEditorScroll = true) {
        var clamped = Math.Max(0, Math.Min(SliderSongProgress.Maximum, milliseconds));
        currentSongPositionMilliseconds = clamped;

        suppressControlEvents = true;
        if (updateSlider) {
            SliderSongProgress.Value = clamped;
        }
        suppressControlEvents = false;

        var seconds = clamped / 1000.0;
        var bpm = Math.Max(1, GetMapDouble("_beatsPerMinute"));
        var beat = seconds * bpm / 60.0;
        if (ShouldUpdatePlaybackText(clamped)) {
            TxtSongPosition.Text = Helper.TimeFormat(seconds);
            LblSelectedBeat.Text = $"Time: {Helper.TimeFormat(seconds)} | Global Beat: {beat:0.##}";
            lastPlaybackUiTextPositionMilliseconds = clamped;
        }
        RefreshSongProgressIndicator();
        if (updateEditorScroll) {
            SyncEditorScrollToCurrentBeat();
        }
    }

    bool ShouldUpdatePlaybackText(double milliseconds) {
        if (!songIsPlaying ||
            double.IsNaN(lastPlaybackUiTextPositionMilliseconds) ||
            Math.Abs(milliseconds - lastPlaybackUiTextPositionMilliseconds) >= PlaybackUiTextUpdateIntervalMilliseconds) {
            return true;
        }

        return milliseconds >= SliderSongProgress.Maximum - 10;
    }

    void OnNavWaveformPointerPressed(object? sender, PointerPressedEventArgs e) {
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control) &&
            e.GetCurrentPoint(ImgWaveformVertical).Properties.PointerUpdateKind == PointerUpdateKind.LeftButtonPressed &&
            TrySelectNotesForNavMarker(e.GetPosition(ImgWaveformVertical))) {
            e.Handled = true;
            return;
        }

        navWaveformDragging = true;
        e.Pointer.Capture(ImgWaveformVertical);
        RefreshSongMouseoverIndicator(e.GetPosition(ImgWaveformVertical).Y, isVisible: true);
        UpdateSongPositionFromNavWaveform(e);
        e.Handled = true;
    }

    void OnNavWaveformPointerMoved(object? sender, PointerEventArgs e) {
        RefreshSongMouseoverIndicator(e.GetPosition(ImgWaveformVertical).Y, isVisible: true);
        if (!navWaveformDragging) {
            return;
        }

        UpdateSongPositionFromNavWaveform(e);
        e.Handled = true;
    }

    void OnNavWaveformPointerReleased(object? sender, PointerReleasedEventArgs e) {
        if (!navWaveformDragging) {
            return;
        }

        navWaveformDragging = false;
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    void OnNavWaveformPointerEntered(object? sender, PointerEventArgs e) {
        RefreshSongMouseoverIndicator(e.GetPosition(ImgWaveformVertical).Y, isVisible: true);
    }

    void OnNavWaveformPointerExited(object? sender, PointerEventArgs e) {
        navWaveformDragging = false;
        RefreshSongMouseoverIndicator(0, isVisible: false);
    }

    void UpdateSongPositionFromNavWaveform(PointerEventArgs e) {
        var position = e.GetPosition(ImgWaveformVertical);
        var ratio = ImgWaveformVertical.Bounds.Height <= 0
            ? 0
            : 1 - Math.Clamp(position.Y / ImgWaveformVertical.Bounds.Height, 0, 1);
        SetSongPosition(ratio * SliderSongProgress.Maximum, updateSlider: true, updateNavWaveform: false);
    }

    bool TrySelectNotesForNavMarker(Point position) {
        if (mapEditor?.currentMapDifficulty == null || ImgWaveformVertical == null) {
            return false;
        }

        var height = GetNavWaveformHeight();
        if (height <= 0) {
            return false;
        }

        const double bookmarkHitPadding = 8;
        const double timingChangeTopPadding = 19;
        const double timingChangeBottomPadding = 8;
        double? nearestDistance = null;
        Bookmark? nearestBookmark = null;
        BPMChange? nearestBpmChange = null;

        foreach (var bookmark in mapEditor.currentMapDifficulty.bookmarks) {
            var y = BeatToNavY(bookmark.beat, height);
            var distance = Math.Abs(position.Y - y);
            if (distance <= bookmarkHitPadding && IsNearest(distance)) {
                nearestDistance = distance;
                nearestBookmark = bookmark;
                nearestBpmChange = null;
            }
        }

        foreach (var bpmChange in mapEditor.currentMapDifficulty.bpmChanges) {
            var y = BeatToNavY(bpmChange.globalBeat, height);
            if (position.Y < y - timingChangeTopPadding || position.Y > y + timingChangeBottomPadding) {
                continue;
            }

            var distance = Math.Abs(position.Y - y);
            if (IsNearest(distance)) {
                nearestDistance = distance;
                nearestBookmark = null;
                nearestBpmChange = bpmChange;
            }
        }

        if (nearestBookmark != null) {
            mapEditor.SelectNotesInBookmark(nearestBookmark);
            return true;
        }

        if (nearestBpmChange != null) {
            mapEditor.SelectNotesInBPMChange(nearestBpmChange);
            return true;
        }

        return false;

        bool IsNearest(double distance) {
            return !nearestDistance.HasValue || distance < nearestDistance.Value;
        }
    }

    void BeginAddDifficulty() {
        if (mapEditor == null || mapEditor.numDifficulties >= 3) {
            return;
        }

        appSession.ShowYesNoCancelConfirmation(
            this,
            "Add Difficulty",
            "Do you want to copy bookmarks and BPM changes from the current difficulty?",
            result => {
                if (result == AppDialogResult.Cancel) {
                    return;
                }

                PauseSong();
                mapEditor.CreateDifficulty(copyCurrentMarkers: result == AppDialogResult.Yes);
                mapEditor.SelectDifficulty(mapEditor.numDifficulties - 1);
                mapEditor.SortDifficulties();

                suppressControlEvents = true;
                LoadDifficultyIntoControls(mapEditor.currentDifficultyIndex);
                suppressControlEvents = false;
                RefreshDifficultyButtons();
            }
        );
    }

    void BeginDeleteDifficulty() {
        if (mapEditor == null || mapEditor.numDifficulties <= 1) {
            return;
        }

        appSession.ShowYesNoConfirmation(
            this,
            "Delete Difficulty",
            "Do you want to delete the current difficulty?",
            result => {
                if (result != AppDialogResult.Yes || mapEditor == null) {
                    return;
                }

                var selectedDifficultyStillValid = mapEditor.DeleteDifficulty();
                if (!selectedDifficultyStillValid) {
                    mapEditor.SelectDifficulty(Math.Max(0, mapEditor.numDifficulties - 1));
                }

                suppressControlEvents = true;
                LoadDifficultyIntoControls(mapEditor.currentDifficultyIndex);
                suppressControlEvents = false;
                RefreshDifficultyButtons();
            }
        );
    }

    void SelectDifficulty(int difficultyIndex) {
        if (mapEditor == null || difficultyIndex < 0 || difficultyIndex >= mapEditor.numDifficulties) {
            return;
        }

        PauseSong();
        suppressControlEvents = true;
        LoadDifficultyIntoControls(difficultyIndex);
        suppressControlEvents = false;
        RefreshDifficultyButtons();
        UpdateDifficultyPrediction();
    }

    void CommitSongBpm() {
        if (mapEditor == null) {
            return;
        }

        var previousBpm = GetMapDouble("_beatsPerMinute");
        if (!TryParseDouble(TxtSongBpm.Text, out var bpm) || bpm <= 0) {
            appSession.ShowError(this, "Error", "The BPM must be a positive number.", () => {
                TxtSongBpm.Text = FormatNumber(previousBpm);
            });
            return;
        }

        if (Math.Abs(bpm - previousBpm) < 0.0001) {
            TxtSongBpm.Text = FormatNumber(previousBpm);
            return;
        }

        appSession.ShowYesNoCancelConfirmation(
            this,
            "BPM Change",
            "Would you like to convert all notes and markers so that they remain at the same time?",
            result => {
                if (result == AppDialogResult.Cancel || mapEditor == null) {
                    TxtSongBpm.Text = FormatNumber(previousBpm);
                    return;
                }

                if (result == AppDialogResult.Yes) {
                    mapEditor.RetimeNotesAndMarkers(bpm, previousBpm);
                }

                mapEditor.GlobalBPM = bpm;
                mapEditor.SetMapValue("_beatsPerMinute", JToken.FromObject(bpm));
                TxtSongBpm.Text = FormatNumber(bpm);
                SetSongPosition(currentSongPositionMilliseconds, updateSlider: true, updateNavWaveform: false);
                UpdateDifficultyPrediction();
            }
        );
    }

    void CommitDifficultyNumber() {
        if (mapEditor == null) {
            return;
        }

        var previousLevel = GetMapInt("_difficultyRank", RagnarockMapDifficulties.Current);
        if (!int.TryParse(TxtDifficultyNumber.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var level) ||
            level < Editor.Difficulty.LevelMin ||
            level > Editor.Difficulty.LevelMax) {
            appSession.ShowError(this, "Error", $"The difficulty level must be an integer between {Editor.Difficulty.LevelMin} and {Editor.Difficulty.LevelMax}.", () => {
                TxtDifficultyNumber.Text = previousLevel.ToString(CultureInfo.InvariantCulture);
            });
            return;
        }

        if (level != previousLevel) {
            mapEditor.SetMapValue("_difficultyRank", JToken.FromObject(level), RagnarockMapDifficulties.Current);
            mapEditor.SortDifficulties();

            suppressControlEvents = true;
            LoadDifficultyIntoControls(mapEditor.currentDifficultyIndex);
            suppressControlEvents = false;
            RefreshDifficultyButtons();
        }

        TxtDifficultyNumber.Text = level.ToString(CultureInfo.InvariantCulture);
    }

    void CommitNoteSpeed() {
        CommitValidatedDouble(
            TxtNoteSpeed,
            () => GetMapDouble("_noteJumpMovementSpeed", RagnarockMapDifficulties.Current),
            value => value > 0,
            value => mapEditor?.SetMapValue("_noteJumpMovementSpeed", JToken.FromObject(value), RagnarockMapDifficulties.Current),
            "The note speed must be a positive number."
        );
    }

    void CommitGridSpacing() {
        var previousSpacing = GetMapDouble("_editorGridSpacing", RagnarockMapDifficulties.Current, custom: true);
        CommitValidatedDouble(
            TxtGridSpacing,
            () => previousSpacing,
            _ => true,
            value => {
                mapEditor?.SetMapValue("_editorGridSpacing", JToken.FromObject(value), RagnarockMapDifficulties.Current, custom: true);
                if (Math.Abs(value - previousSpacing) >= 0.0001) {
                    RefreshEditorSurface();
                }
            },
            "The grid spacing must be numerical."
        );
    }

    void CommitGridDivision() {
        var previousDivision = GetMapInt("_editorGridDivision", RagnarockMapDifficulties.Current, custom: true);
        CommitValidatedInteger(
            TxtGridDivision,
            () => previousDivision,
            value => value >= 1 && value <= Editor.GridDivisionMax,
            value => {
                if (mapEditor == null) {
                    return;
                }

                mapEditor.defaultGridDivision = value;
                mapEditor.SetMapValue("_editorGridDivision", JToken.FromObject(value), RagnarockMapDifficulties.Current, custom: true);
                if (value != previousDivision) {
                    RefreshEditorSurface();
                }
            },
            $"The grid division amount must be an integer from 1 to {Editor.GridDivisionMax}."
        );
    }

    void CommitMedalDistance(TextBox textBox, RagnarockScoreMedals medal) {
        if (mapEditor == null) {
            return;
        }

        var previousValue = mapEditor.GetMedalDistance(medal, RagnarockMapDifficulties.Current);
        var input = (textBox.Text ?? string.Empty).Trim();
        if (string.Equals(input, "auto", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(input)) {
            if (previousValue != 0) {
                mapEditor.SetMedalDistance(medal, 0, RagnarockMapDifficulties.Current);
            }
            textBox.Text = "Auto";
            return;
        }

        if (int.TryParse(input, out var distance) && distance >= 0) {
            if (distance != previousValue) {
                mapEditor.SetMedalDistance(medal, distance, RagnarockMapDifficulties.Current);
            }
            textBox.Text = distance == 0 ? "Auto" : distance.ToString(CultureInfo.InvariantCulture);
            return;
        }

        appSession.ShowError(this, "Error", "The medal distance must be a non-negative integer or Auto.", () => {
            textBox.Text = FormatMedalDistance(previousValue);
        });
    }

    void CommitValidatedDouble(TextBox textBox, Func<double> getPreviousValue, Func<double, bool> validate, Action<double> applyValue, string errorMessage) {
        var previousValue = getPreviousValue();
        if (TryParseDouble(textBox.Text, out var parsedValue) && validate(parsedValue)) {
            if (Math.Abs(parsedValue - previousValue) >= 0.0001) {
                applyValue(parsedValue);
            }
            textBox.Text = FormatNumber(parsedValue);
            return;
        }

        appSession.ShowError(this, "Error", errorMessage, () => {
            textBox.Text = FormatNumber(previousValue);
        });
    }

    void CommitValidatedInteger(TextBox textBox, Func<int> getPreviousValue, Func<int, bool> validate, Action<int> applyValue, string errorMessage) {
        var previousValue = getPreviousValue();
        if (int.TryParse(textBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedValue) && validate(parsedValue)) {
            if (parsedValue != previousValue) {
                applyValue(parsedValue);
            }
            textBox.Text = parsedValue.ToString(CultureInfo.InvariantCulture);
            return;
        }

        appSession.ShowError(this, "Error", errorMessage, () => {
            textBox.Text = previousValue.ToString(CultureInfo.InvariantCulture);
        });
    }

    void SetSnapToGrid(bool enabled) {
        snapToGrid = enabled;
        suppressControlEvents = true;
        CheckGridSnap.IsChecked = enabled;
        menuItemSnapToGrid.IsChecked = enabled;
        var automationStatus = enabled ? "Checked" : "Unchecked";
        AutomationHelper.SetItemStatus(menuItemSnapToGrid, automationStatus);
        AutomationHelper.SetHelpText(menuItemSnapToGrid, automationStatus);
        suppressControlEvents = false;
    }

    void ToggleLeftSidebar() {
        leftSidebar.IsVisible = !leftSidebar.IsVisible;
    }

    void ToggleRightSidebar() {
        rightSidebar.IsVisible = !rightSidebar.IsVisible;
    }

    void BeginOpenMap() {
        PromptToSaveIfNeeded("OpenMap", () => appSession.OpenMapFromPicker(this));
    }

    void BeginCreateNewMap() {
        PromptToSaveIfNeeded("CreateNewMap", () => appSession.CreateNewMap(this));
    }

    void BeginImportMap() {
        PromptToSaveIfNeeded("ImportMap", () => appSession.ImportMap(this));
    }

    void BeginCloseMap() {
        PromptToSaveIfNeeded("CloseMap", () => appSession.ReturnToStartWindow());
    }

    void BeginClearCache() {
        appSession.ShowYesNoCancelConfirmation(
            this,
            "Warning",
            "This will delete all cached spectrogram images for this map. Do you want to proceed?",
            result => {
                if (result == AppDialogResult.Yes) {
                    ClearSongCache();
                }
            }
        );
    }

    void PromptToSaveIfNeeded(string reason, Action nextAction) {
        if (mapEditor == null || !mapEditor.saveIsNeeded) {
            nextAction();
            return;
        }

        appSession.ShowYesNoCancelConfirmation(
            this,
            "Unsaved Changes",
            "Do you want to save the current map before continuing?",
            result => {
                switch (result) {
                    case AppDialogResult.Yes:
                        SaveMapWithBackup();
                        nextAction();
                        break;
                    case AppDialogResult.No:
                        nextAction();
                        break;
                }
            }
        );
    }

    void SaveMapWithBackup() {
        if (mapEditor == null) {
            return;
        }

        mapEditor.SaveMap();
        CreateAutosaveSnapshot();
    }

    void CreateAutosaveSnapshot() {
        if (mapEditor == null) {
            return;
        }

        var backupFolder = Path.Combine(mapEditor.mapFolder, EddaProgram.BackupPath);
        Directory.CreateDirectory(backupFolder);

        var backupName = DateTime.Now.ToString("Backup - dd MMMM yyyy h.mmtt", CultureInfo.InvariantCulture);
        var backupPath = Path.Combine(backupFolder, backupName);
        if (Directory.Exists(backupPath)) {
            return;
        }

        var existingBackups = Directory.GetDirectories(backupFolder, "Backup *")
            .OrderBy(Directory.GetCreationTimeUtc)
            .ToList();
        while (existingBackups.Count >= EddaProgram.MaxBackups) {
            Directory.Delete(existingBackups[0], recursive: true);
            existingBackups.RemoveAt(0);
        }

        Directory.CreateDirectory(backupPath);
        foreach (var filePath in EnumerateMapFilesForAutosave()) {
            var destinationPath = Path.Combine(backupPath, Path.GetFileName(filePath));
            File.Copy(filePath, destinationPath, overwrite: true);
        }
    }

    IEnumerable<string> EnumerateMapFilesForAutosave() {
        if (mapEditor == null) {
            yield break;
        }

        var infoPath = Path.Combine(mapEditor.mapFolder, "info.dat");
        if (File.Exists(infoPath)) {
            yield return infoPath;
        }

        for (var index = 0; index < mapEditor.numDifficulties; index++) {
            var difficultyFile = GetMapString("_beatmapFilename", (RagnarockMapDifficulties)index);
            var difficultyPath = Path.Combine(mapEditor.mapFolder, difficultyFile);
            if (File.Exists(difficultyPath)) {
                yield return difficultyPath;
            }
        }
    }

    async void ExportMap() {
        if (mapEditor == null) {
            return;
        }

        var exportFolder = await appSession.PickExportFolderAsync(this, Helper.GetRagnarockMapFolder());
        if (string.IsNullOrWhiteSpace(exportFolder)) {
            return;
        }

        mapEditor.SaveMap();

        var songArtist = Helper.ValidFilenameFrom(GetMapString("_songAuthorName"));
        var songName = Helper.ValidFilenameFrom(GetMapString("_songName"));
        var zipName = Helper.ValidMapFolderNameFrom(songArtist + songName);
        if (string.IsNullOrWhiteSpace(zipName)) {
            zipName = "EddaExport";
        }

        var zipPath = Path.Combine(exportFolder, zipName + ".zip");
        Helper.FileDeleteIfExists(zipPath);

        using var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create);
        foreach (var filePath in EnumerateMapFilesForExport()) {
            archive.CreateEntryFromFile(filePath, Path.GetFileName(filePath));
        }
    }

    IEnumerable<string> EnumerateMapFilesForExport() {
        if (mapEditor == null) {
            yield break;
        }

        var baseFolder = mapEditor.mapFolder;
        yield return Path.Combine(baseFolder, "info.dat");

        var songFile = GetMapString("_songFilename");
        if (!string.IsNullOrWhiteSpace(songFile)) {
            var songPath = Path.Combine(baseFolder, songFile);
            if (File.Exists(songPath)) {
                yield return songPath;
            }
        }

        var coverFile = GetMapString("_coverImageFilename");
        if (!string.IsNullOrWhiteSpace(coverFile)) {
            var coverPath = Path.Combine(baseFolder, coverFile);
            if (File.Exists(coverPath)) {
                yield return coverPath;
            }
        }

        var previewPath = Path.Combine(baseFolder, BeatmapDefaults.PreviewFilename);
        if (File.Exists(previewPath)) {
            yield return previewPath;
        }

        for (var index = 0; index < mapEditor.numDifficulties; index++) {
            var difficultyFile = GetMapString("_beatmapFilename", (RagnarockMapDifficulties)index);
            if (string.IsNullOrWhiteSpace(difficultyFile)) {
                continue;
            }

            var difficultyPath = Path.Combine(baseFolder, difficultyFile);
            if (File.Exists(difficultyPath)) {
                yield return difficultyPath;
            }
        }
    }

    void ReplaceSong(string selectedSongPath) {
        if (mapEditor == null) {
            return;
        }

        using var vorbisStream = TryOpenVorbis(selectedSongPath);
        if (vorbisStream.TotalTime.TotalHours >= 1) {
            appSession.ShowError(this, "Error", "Songs over 1 hour in duration are not supported.", onDismissed: null);
            return;
        }

        var songFileName = Helper.SanitiseSongFileName(selectedSongPath);
        var destinationPath = Path.Combine(mapEditor.mapFolder, songFileName);
        var previousSongPath = Path.Combine(mapEditor.mapFolder, GetMapString("_songFilename"));

        if (!string.Equals(Path.GetFullPath(selectedSongPath), Path.GetFullPath(previousSongPath), StringComparison.OrdinalIgnoreCase)) {
            PauseSong();
            UnloadSongAudio();

            if (!string.Equals(Path.GetFullPath(selectedSongPath), Path.GetFullPath(destinationPath), StringComparison.OrdinalIgnoreCase)) {
                RetryDeleteFile(destinationPath);
                File.Copy(selectedSongPath, destinationPath, overwrite: true);
            }

            if (!string.Equals(previousSongPath, destinationPath, StringComparison.OrdinalIgnoreCase)) {
                RetryDeleteFile(previousSongPath);
            }
        }

        mapEditor.SetMapValue("_songApproximativeDuration", JToken.FromObject((int)vorbisStream.TotalTime.TotalSeconds + 1));
        mapEditor.SetMapValue("_songFilename", JToken.FromObject(songFileName));
        mapEditor.SaveMap();
        LoadSongAudio();
        mapEditor.SongDuration = currentSongDurationSeconds;
        TxtSongFileName.Text = songFileName;
        SetSongPosition(0, updateSlider: true, updateNavWaveform: false);
        RefreshEditorSurface();
        UpdateDifficultyPrediction();
        songPreviewController?.Restart(mapEditor, songIsPlaying);
    }

    static void RetryDeleteFile(string path) {
        for (var attempt = 0; ; attempt++) {
            try {
                Helper.FileDeleteIfExists(path);
                return;
            } catch (IOException) when (attempt < 39) {
                Thread.Sleep(50);
            } catch (UnauthorizedAccessException) when (attempt < 39) {
                Thread.Sleep(50);
            }
        }
    }

    void ReplaceCover(string selectedCoverPath) {
        if (mapEditor == null) {
            return;
        }

        var newFileName = Helper.SanitiseCoverFileName(selectedCoverPath);
        var destinationPath = Path.Combine(mapEditor.mapFolder, newFileName);
        var previousCoverFile = GetMapString("_coverImageFilename");
        var previousCoverPath = string.IsNullOrWhiteSpace(previousCoverFile)
            ? null
            : Path.Combine(mapEditor.mapFolder, previousCoverFile);

        if (!string.Equals(Path.GetFullPath(selectedCoverPath), Path.GetFullPath(destinationPath), StringComparison.OrdinalIgnoreCase)) {
            Helper.FileDeleteIfExists(destinationPath);
            File.Copy(selectedCoverPath, destinationPath, overwrite: true);
        }

        if (!string.IsNullOrWhiteSpace(previousCoverPath) &&
            !string.Equals(previousCoverPath, destinationPath, StringComparison.OrdinalIgnoreCase)) {
            Helper.FileDeleteIfExists(previousCoverPath);
        }

        mapEditor.SetMapValue("_coverImageFilename", JToken.FromObject(newFileName));
        mapEditor.SaveMap();
        TxtCoverFileName.Text = newFileName;
        UpdateCoverPreview();
    }

    void ClearSongCache() {
        if (mapEditor == null) {
            return;
        }

        var cachePath = Path.Combine(mapEditor.mapFolder, EddaProgram.CachePath);
        if (Directory.Exists(cachePath)) {
            Directory.Delete(cachePath, recursive: true);
        }
    }

    void OpenBpmFinderWindow() {
        ShowToolWindow(() => bpmFinderWindow, window => bpmFinderWindow = window, () => new BpmCalcWindow());
    }

    void OpenDifficultyPredictorWindow() {
        ShowToolWindow(() => predictorWindow, window => predictorWindow = window, () => new DifficultyPredictorWindow(this));
    }

    void OpenAboutWindow() {
        ShowToolWindow(() => aboutWindow, window => aboutWindow = window, () => new AboutWindow());
    }

    void OpenChangeBpmWindow() {
        ShowToolWindow(() => changeBpmWindow, window => changeBpmWindow = window, () => new ChangeBpmWindow(this));
    }

    void OpenCustomizeNavBarWindow() {
        ShowToolWindow(() => customizeNavBarWindow, window => customizeNavBarWindow = window, () => new CustomizeNavBarWindow(this));
    }

    void OpenSongPreviewWindow() {
        ShowToolWindow(() => songPreviewWindow, window => songPreviewWindow = window, () => new SongPreviewWindow(this));
    }

    void ShowToolWindow(Func<Window?> getter, Action<Window?> setter, Func<Window> factory) {
        var existingWindow = getter();
        if (existingWindow is { IsVisible: true }) {
            existingWindow.Activate();
            return;
        }

        var window = factory();
        window.Topmost = true;
        setter(window);
        window.Closed += (_, _) => {
            if (ReferenceEquals(getter(), window)) {
                setter(null);
            }
        };
        window.Show(this);
    }

    void DisposeWindowResources() {
        songPositionTimer.Stop();
        songPreviewController?.Dispose();
        songPreviewController = null;
        PauseSong();
        noteScanner?.Dispose();
        noteScanner = null;
        beatScanner?.Dispose();
        beatScanner = null;
        drummer?.Dispose();
        drummer = null;
        metronome?.Dispose();
        metronome = null;
        UnloadSongAudio();
        InvalidateEditorAudioVisuals(clearVisuals: true);

        songPlaybackCancellationTokenSource.Dispose();
        songPlayerSource?.Dispose();
        songPlayerSource = null;
        audioEngine?.Dispose();
        audioEngine = null;
        coverPreviewBitmap?.Dispose();
        coverPreviewBitmap = null;
        foreach (var bitmap in resourceBitmapCache.Values) {
            bitmap?.Dispose();
        }
        resourceBitmapCache.Clear();
        mapEditor?.Dispose();
    }

    void LoadSongAudio() {
        UnloadSongAudio();
        InvalidateEditorAudioVisuals(clearVisuals: true);

        var songPath = CurrentSongPath;
        currentSongDurationSeconds = ResolveSongDurationSeconds();
        UpdateSongDurationText();
        SliderSongProgress.Minimum = 0;
        SliderSongProgress.Maximum = currentSongDurationSeconds * 1000;
        if (!string.IsNullOrWhiteSpace(songPath) && File.Exists(songPath) && audioEngine != null) {
            try {
                songPlayerSource = audioEngine.CreateStreamingSource();
            } catch {
                songPlayerSource?.Dispose();
                songPlayerSource = null;
            }
        }
        InvalidateEditorAudioVisuals();
    }

    void UnloadSongAudio() {
        StopSongPlayer();
        songPlayerSource?.Dispose();
        songPlayerSource = null;
        InvalidateEditorAudioVisuals(clearVisuals: true);
    }

    void PlaySong() {
        if (Helper.DoubleApproxGreaterEqual(SliderSongProgress.Value, SliderSongProgress.Maximum)) {
            return;
        }

        playbackStartMilliseconds = SliderSongProgress.Value;
        if (!StartSongPlayer(playbackStartMilliseconds)) {
            return;
        }
        playbackClock.Restart();

        songIsPlaying = true;
        lastPlaybackUiTextPositionMilliseconds = double.NaN;
        ResetPlaybackCanvasScroll();
        UpdatePlaybackUi();
        songPreviewController?.StopPreview();
        songPreviewController?.DisablePreviewButton();

        if (metronome != null) {
            metronome.isEnabled = CheckMetronome.IsChecked ?? false;
        }

        noteScanner?.Stop();
        beatScanner?.Stop();
        noteScanner = new NoteScanner(new AvaloniaNoteScannerUiAdapter(this), drummer, SliderSongTempo.Value);
        beatScanner = new BeatScanner(metronome, SliderSongTempo.Value);
        if (mapEditor?.currentMapDifficulty != null) {
            noteScanner.Start((int)(SliderSongProgress.Value - editorAudioLatency), new List<Note>(mapEditor.currentMapDifficulty.notes), CurrentGlobalBpm);
            beatScanner.Start((int)(SliderSongProgress.Value - editorAudioLatency), BuildPlaybackBeatMarkers(), CurrentGlobalBpm);
        }

        var previousTokenSource = songPlaybackCancellationTokenSource;
        songPlaybackCancellationTokenSource = new CancellationTokenSource();
        previousTokenSource.Dispose();

        songPositionTimer.Start();
    }

    bool StartSongPlayer(double startMilliseconds) {
        var songPath = CurrentSongPath;
        if (string.IsNullOrWhiteSpace(songPath) || !File.Exists(songPath) || songPlayerSource == null) {
            return false;
        }

        StopSongPlayer();
        var latencyAdjustedStart = Math.Max(0, startMilliseconds - editorAudioLatency);
        return songPlayerSource.PlayVorbis(
            songPath,
            tempo: SliderSongTempo.Value,
            volume: SliderSongVol.Value,
            startSeconds: latencyAdjustedStart / 1000.0);
    }

    void StopSongPlayer() {
        songPlayerSource?.Stop();
    }

    void UpdatePlaybackPositionFromAudio() {
        if (!songIsPlaying || songPauseInProgress) {
            return;
        }

        var milliseconds = playbackStartMilliseconds + (playbackClock.Elapsed.TotalMilliseconds * Math.Max(Audio.MinSongTempo, SliderSongTempo.Value));
        SetSongPosition(milliseconds, updateSlider: true, updateNavWaveform: false);
        if (milliseconds >= SliderSongProgress.Maximum - 10) {
            SetSongPosition(SliderSongProgress.Maximum, updateSlider: true, updateNavWaveform: false);
            PauseSong();
        }
    }

    void UpdateAudioVolumes() {
        songPlayerSource?.SetVolume(SliderSongVol.Value);
        songPreviewController?.UpdateVolume();
        drummer?.ChangeVolume(SliderDrumVol.Value);
        metronome?.ChangeVolume(SliderDrumVol.Value);
    }

    void ReinitializePlaybackDependencies() {
        playbackDevices.Clear();
        if (mapEditor != null) {
            songPreviewController?.Restart(mapEditor, songIsPlaying);
        }
        RestartDrummer();
        RestartMetronome();
    }

    void RestartMetronome() {
        var oldMetronome = metronome;
        metronome = CreateAudioCuePlayer(
            Audio.MetronomeFilename,
            Audio.MetronomeStreams,
            isPanned: false,
            GetSettingDouble(UserSettingsKey.DefaultNoteVolume, DefaultUserSettings.DefaultNoteVolume),
            isEnabled: CheckMetronome.IsChecked ?? false
        );
        oldMetronome?.Dispose();
        metronome?.ChangeVolume(SliderDrumVol.Value);

        if (beatScanner != null) {
            beatScanner.SetAudioPlayer(metronome);
        } else {
            beatScanner = new BeatScanner(metronome, SliderSongTempo.Value);
        }
    }

    IAudioCuePlayer? CreateAudioCuePlayer(string basePath, int streams, bool isPanned, double defaultVolume, bool isEnabled = true) {
        if (audioEngine == null) {
            return null;
        }

        try {
            return new AvaloniaOpenAlAudioCuePlayer(
                audioEngine,
                basePath,
                streams,
                isEnabled,
                isPanned,
                defaultVolume);
        } catch {
            return null;
        }
    }

    List<double> BuildPlaybackBeatMarkers() {
        var totalBeats = Math.Max(GetCurrentSongBeat(), currentSongDurationSeconds * Math.Max(1, CurrentGlobalBpm) / 60.0);
        var beats = new List<double>();
        for (var beat = 0.0; beat <= totalBeats; beat += 1.0) {
            beats.Add(beat);
        }
        return beats;
    }

    void AnimateDrum(int column) {
        var drum = column switch {
            0 => drum0,
            1 => drum1,
            2 => drum2,
            3 => drum3,
            _ => null
        };
        if (drum == null) {
            return;
        }

        drumFeedbackSequence++;
        if (column == 0) {
            UpdateLaneOneDrumAutomationStatus(true);
        } else {
            AutomationHelper.SetItemStatus(drum, $"hit:{drumFeedbackSequence}");
        }
    }

    void AnimateNote(Note note) {
        if (!editorNoteVisuals.TryGetValue(Helper.UidGenerator(note), out var noteVisual)) {
            return;
        }

        noteVisual.Opacity = 1;
        noteVisual.RenderTransform = null;
        noteFeedbackSequence++;
        AutomationHelper.SetItemStatus(noteVisual, $"hit:{noteFeedbackSequence}|opacity:{noteVisual.Opacity:0.##}");
    }

    void OnKeyDown(object? sender, KeyEventArgs e) {
        shiftKeyDown = e.KeyModifiers.HasFlag(KeyModifiers.Shift) || e.Key is Key.LeftShift or Key.RightShift;

        if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.KeyModifiers.HasFlag(KeyModifiers.Shift)) {
            switch (e.Key) {
                case Key.B:
                    if (!songIsPlaying) {
                        AddBookmarkAtEditorPointerOrCurrentPosition(snappedToGrid: true);
                        e.Handled = true;
                        return;
                    }
                    break;
                case Key.T:
                    if (!songIsPlaying) {
                        AddTimingChangeAtEditorPointerOrCurrentPosition(snappedToGrid: true);
                        e.Handled = true;
                        return;
                    }
                    break;
                case Key.Z:
                    if (!songIsPlaying) {
                        mapEditor?.Redo();
                        UpdateDifficultyPrediction();
                        e.Handled = true;
                        return;
                    }
                    break;
            }
        }

        if (TryHandleEditorArrowShortcut(e)) {
            e.Handled = true;
            return;
        }

        if (e.KeyModifiers.HasFlag(KeyModifiers.Control)) {
            switch (e.Key) {
                case Key.W:
                    BeginCloseMap();
                    e.Handled = true;
                    return;
                case Key.S:
                    SaveMapWithBackup();
                    e.Handled = true;
                    return;
                case Key.O:
                    BeginOpenMap();
                    e.Handled = true;
                    return;
                case Key.N:
                    BeginCreateNewMap();
                    e.Handled = true;
                    return;
                case Key.I:
                    BeginImportMap();
                    e.Handled = true;
                    return;
                case Key.E:
                    ExportMap();
                    e.Handled = true;
                    return;
                case Key.G:
                    SetSnapToGrid(!snapToGrid);
                    e.Handled = true;
                    return;
                case Key.A:
                    if (!songIsPlaying && !textInputHasFocus) {
                        mapEditor?.SelectAllNotes();
                        e.Handled = true;
                        return;
                    }
                    break;
                case Key.C:
                    if (CopySelection()) {
                        e.Handled = true;
                        return;
                    }
                    break;
                case Key.X:
                    if (CutSelection()) {
                        e.Handled = true;
                        return;
                    }
                    break;
                case Key.V:
                    if (PasteClipboardAtEditor(e.KeyModifiers.HasFlag(KeyModifiers.Shift))) {
                        e.Handled = true;
                        return;
                    }
                    break;
                case Key.M:
                    if (MirrorSelection()) {
                        e.Handled = true;
                        return;
                    }
                    break;
                case Key.Q:
                    if (QuantizeSelection()) {
                        e.Handled = true;
                        return;
                    }
                    break;
                case Key.B:
                    if (!songIsPlaying) {
                        AddBookmarkAtEditorPointerOrCurrentPosition(snappedToGrid: false);
                        e.Handled = true;
                        return;
                    }
                    break;
                case Key.T:
                    if (!songIsPlaying) {
                        AddTimingChangeAtEditorPointerOrCurrentPosition(snappedToGrid: false);
                        e.Handled = true;
                        return;
                    }
                    break;
                case Key.Z:
                    if (!songIsPlaying) {
                        mapEditor?.Undo();
                        UpdateDifficultyPrediction();
                        e.Handled = true;
                        return;
                    }
                    break;
                case Key.Y:
                    if (!songIsPlaying) {
                        mapEditor?.Redo();
                        UpdateDifficultyPrediction();
                        e.Handled = true;
                        return;
                    }
                    break;
                case Key.OemOpenBrackets:
                    ToggleLeftSidebar();
                    e.Handled = true;
                    return;
                case Key.OemCloseBrackets:
                    ToggleRightSidebar();
                    e.Handled = true;
                    return;
            }
        }

        if (e.Key == Key.Space && !textInputHasFocus) {
            var focusedElement = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();
            var focusedElementType = focusedElement == null ? string.Empty : focusedElement.GetType().Name;
            Focus();
            ToggleSongPlayback();
            e.Handled = true;
            return;
        }

        if (textInputHasFocus) {
            return;
        }

        if (TryHandleEditorNumberKey(e.Key)) {
            e.Handled = true;
            return;
        }

        switch (e.Key) {
            case Key.Delete:
                mapEditor?.RemoveSelectedNotes();
                UpdateDifficultyPrediction();
                e.Handled = true;
                break;
            case Key.Escape:
                mapEditor?.UnselectAllNotes();
                e.Handled = true;
                break;
        }
    }

    bool TryHandleEditorArrowShortcut(KeyEventArgs e) {
        if (textInputHasFocus || mapEditor?.currentMapDifficulty == null) {
            return false;
        }

        var hasShift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        var hasControl = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        if (!hasShift && !hasControl) {
            return false;
        }

        switch (e.Key) {
            case Key.Up:
                mapEditor.ShiftSelectionByBeat(hasControl ? MoveNote.MOVE_GRID_UP : MoveNote.MOVE_BEAT_UP);
                break;
            case Key.Down:
                mapEditor.ShiftSelectionByBeat(hasControl ? MoveNote.MOVE_GRID_DOWN : MoveNote.MOVE_BEAT_DOWN);
                break;
            case Key.Left:
                mapEditor.ShiftSelectionByCol(-1);
                break;
            case Key.Right:
                mapEditor.ShiftSelectionByCol(1);
                break;
            default:
                return false;
        }

        UpdateDifficultyPrediction();
        return true;
    }

    bool CanEditSelectedNotes() {
        return !songIsPlaying && !textInputHasFocus && mapEditor?.currentMapDifficulty != null;
    }

    bool CopySelection() {
        if (!CanEditSelectedNotes()) {
            return false;
        }

        mapEditor?.CopySelection();
        return true;
    }

    bool CutSelection() {
        if (!CanEditSelectedNotes()) {
            return false;
        }

        mapEditor?.CutSelection();
        UpdateDifficultyPrediction();
        return true;
    }

    bool PasteClipboardAtEditor(bool pasteOnColumn) {
        if (!CanEditSelectedNotes()) {
            return false;
        }

        var beatOffset = editorPointerInside
            ? GetActiveEditorBeat()
            : GetCurrentSongBeat();
        var column = pasteOnColumn && editorHoveredColumn is >= 0 and <= 3
            ? editorHoveredColumn
            : (int?)null;
        mapEditor?.PasteClipboard(beatOffset, column);
        UpdateDifficultyPrediction();
        return true;
    }

    bool MirrorSelection() {
        if (!CanEditSelectedNotes()) {
            return false;
        }

        mapEditor?.MirrorSelection();
        UpdateDifficultyPrediction();
        return true;
    }

    bool QuantizeSelection() {
        if (!CanEditSelectedNotes()) {
            return false;
        }

        mapEditor?.QuantizeSelection();
        UpdateDifficultyPrediction();
        return true;
    }

    void OnKeyUp(object? sender, KeyEventArgs e) {
        if (e.Key is Key.LeftShift or Key.RightShift) {
            shiftKeyDown = false;
        }

        if (e.Key == Key.Space && !textInputHasFocus) {
            e.Handled = true;
        }
    }

    void OnSpectrogramResizePointerPressed(object? sender, PointerPressedEventArgs e) {
        if (editorPanel == null || gridSpectrogram == null) {
            return;
        }

        if (sender is not Control control) {
            return;
        }

        var localPosition = e.GetPosition(control);
        if (!ReferenceEquals(control, spectrogramResize) && !IsPointerOnSpectrogramResizeEdge(control, localPosition)) {
            return;
        }

        spectrogramResizeDragging = true;
        spectrogramResizeDragOriginX = e.GetPosition(editorPanel).X;
        spectrogramResizeDragOriginWidth = GetSpectrogramWidth();
        e.Pointer.Capture(control);
        e.Handled = true;
    }

    void OnSpectrogramResizePointerMoved(object? sender, PointerEventArgs e) {
        if (!spectrogramResizeDragging || editorPanel == null) {
            return;
        }

        var position = e.GetPosition(editorPanel);
        SetSpectrogramWidth(spectrogramResizeDragOriginWidth + (position.X - spectrogramResizeDragOriginX), refreshSurface: false);
        e.Handled = true;
    }

    void OnSpectrogramResizePointerReleased(object? sender, PointerReleasedEventArgs e) {
        if (!spectrogramResizeDragging) {
            return;
        }

        spectrogramResizeDragging = false;
        e.Pointer.Capture(null);
        SetSpectrogramWidth(GetSpectrogramWidth());
        e.Handled = true;
    }

    bool IsPointerOnSpectrogramResizeEdge(Control control, Point position) {
        var boundsWidth = control.Bounds.Width;
        if (boundsWidth <= 0) {
            return false;
        }

        return position.X >= boundsWidth - Math.Max(SpectrogramResizeGripWidth + 2, 10);
    }

    double GetSpectrogramWidth() {
        if (gridSpectrogram?.ColumnDefinitions.Count > 0) {
            var configuredWidth = gridSpectrogram.ColumnDefinitions[0].Width;
            if (configuredWidth.IsAbsolute && configuredWidth.Value > 0) {
                return configuredWidth.Value;
            }

            var width = gridSpectrogram.ColumnDefinitions[0].ActualWidth;
            if (width > 1) {
                return width;
            }
        }

        return spectrogramPreferredWidth;
    }

    void SetSpectrogramWidth(double desiredWidth, bool refreshSurface = true) {
        if (gridSpectrogram == null || gridSpectrogram.ColumnDefinitions.Count == 0) {
            spectrogramPreferredWidth = desiredWidth;
            return;
        }

        var clampedWidth = Math.Clamp(desiredWidth, 20, GetSpectrogramMaxWidth());
        spectrogramPreferredWidth = clampedWidth;
        gridSpectrogram.ColumnDefinitions[0].Width = new GridLength(clampedWidth);
        gridSpectrogram.ColumnDefinitions[1].Width = new GridLength(SpectrogramResizeGripWidth);
        gridSpectrogram.Width = clampedWidth + SpectrogramResizeGripWidth;

        if (refreshSurface) {
            ApplyEditorLayoutMetrics();
            RefreshSpectrogramVisuals();
        }
    }

    double GetSpectrogramMaxWidth() {
        var panelWidth = editorPanel != null && editorPanel.Bounds.Width > 1
            ? editorPanel.Bounds.Width
            : ClientSize.Width;
        var navWidth = borderNavWaveform != null && borderNavWaveform.Bounds.Width > 1
            ? borderNavWaveform.Bounds.Width
            : ImgWaveformVertical?.Width ?? 64;
        var available = panelWidth - navWidth - EditorSurfaceMinWidth - 3;
        return Math.Max(20, available);
    }

    void OnNumericTextBoxKeyDown(object? sender, KeyEventArgs e) {
        if (e.Key != Key.Enter || sender is not TextBox textBox) {
            return;
        }

        switch (textBox.Name) {
            case "txtSongBPM":
                CommitSongBpm();
                break;
            case "txtDifficultyNumber":
                CommitDifficultyNumber();
                break;
            case "txtNoteSpeed":
                CommitNoteSpeed();
                break;
            case "txtDistMedal0":
                CommitMedalDistance(TxtDistMedal0, RagnarockScoreMedals.Bronze);
                break;
            case "txtDistMedal1":
                CommitMedalDistance(TxtDistMedal1, RagnarockScoreMedals.Silver);
                break;
            case "txtDistMedal2":
                CommitMedalDistance(TxtDistMedal2, RagnarockScoreMedals.Gold);
                break;
            case "txtGridSpacing":
                CommitGridSpacing();
                break;
            case "txtGridDivision":
                CommitGridDivision();
                break;
        }

        e.Handled = true;
    }

    void UpdateRecentMapEntry() {
        if (mapEditor == null) {
            return;
        }

        appSession.RecentMaps.RemoveRecentlyOpened(mapEditor.mapFolder);
        appSession.RecentMaps.AddRecentlyOpened(TxtSongName.Text ?? string.Empty, mapEditor.mapFolder);
        appSession.RecentMaps.Write();
    }

    static void ClearAutoText(TextBox textBox) {
        if (string.Equals(textBox.Text, "Auto", StringComparison.OrdinalIgnoreCase)) {
            textBox.SelectAll();
        }
    }

    string GetMapString(string key, RagnarockMapDifficulties? difficulty = null, bool custom = false) {
        if (mapEditor == null) {
            return string.Empty;
        }

        var token = mapEditor.GetMapValue(key, difficulty, custom);
        return token.Type switch {
            JTokenType.String => token.Value<string>() ?? string.Empty,
            JTokenType.Integer => token.Value<long>().ToString(CultureInfo.InvariantCulture),
            JTokenType.Float => token.Value<double>().ToString("0.##", CultureInfo.InvariantCulture),
            JTokenType.Boolean => token.Value<bool>().ToString(),
            _ => token.ToString()
        };
    }

    double GetMapDouble(string key, RagnarockMapDifficulties? difficulty = null, bool custom = false) {
        var text = GetMapString(key, difficulty, custom);
        return TryParseDouble(text, out var value) ? value : 0;
    }

    int GetMapInt(string key, RagnarockMapDifficulties? difficulty = null, bool custom = false) {
        var text = GetMapString(key, difficulty, custom);
        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : 0;
    }

    bool GetMapBoolString(string key) {
        return string.Equals(GetMapString(key), "true", StringComparison.OrdinalIgnoreCase);
    }

    bool GetSettingBool(string key, bool defaultValue) {
        return userSettings.GetValueForKey(key) == null ? defaultValue : userSettings.GetBoolForKey(key);
    }

    double GetSettingDouble(string key, double defaultValue) {
        return TryParseDouble(userSettings.GetValueForKey(key), out var parsedValue) ? parsedValue : defaultValue;
    }

    void RefreshEditorDisplayPreferences() {
        if (mainWaveformCanvas == null || gridSpectrogram == null || borderSpectrogram == null || spectrogramCanvas == null) {
            return;
        }

        var showSpectrogram = GetSettingBool(UserSettingsKey.EnableSpectrogram, DefaultUserSettings.EnableSpectrogram);
        var showGridWaveform = CheckWaveform.IsChecked ?? false;

        gridSpectrogram.IsVisible = showSpectrogram;
        borderSpectrogram.IsVisible = showSpectrogram;
        spectrogramCanvas.IsVisible = showSpectrogram;

        mainWaveformCanvas.IsVisible = showGridWaveform;

        RefreshSpectrogramVisuals();
        RefreshEditorSurface();
    }

    void RefreshSpectrogramVisuals() {
        if (spectrogramCanvas == null) {
            return;
        }

        if (!GetSettingBool(UserSettingsKey.EnableSpectrogram, DefaultUserSettings.EnableSpectrogram)) {
            ClearSpectrogramBitmaps(clearVisuals: true);
            return;
        }

        var width = Math.Max(20, scrollSpectrogram.Bounds.Width > 1
            ? scrollSpectrogram.Bounds.Width
            : borderSpectrogram.Bounds.Width > 1
                ? borderSpectrogram.Bounds.Width
                : spectrogramCanvas.Width);
        var totalHeight = Math.Max(GetEditorViewportHeight(), GetEditorContentHeight());
        var beatContentHeight = Math.Max(1, totalHeight - GetEditorViewportHeight());
        var topPadding = GetEditorTopPadding();
        var numChunks = GetSettingBool(UserSettingsKey.SpectrogramChunking, DefaultUserSettings.SpectrogramChunking)
            ? Editor.Spectrogram.NumberOfChunks
            : 1;

        UpdateSpectrogramLayout(width, totalHeight, beatContentHeight, topPadding, numChunks);
        ScheduleSpectrogramRender(numChunks);
    }

    void RefreshNavigationVisuals() {
        if (ImgWaveformVertical == null || navWaveformBackdrop == null || CanvasNavNotes == null || CanvasBookmarks == null || CanvasTimingChanges == null) {
            return;
        }

        var width = GetNavWaveformWidth();
        var height = GetNavWaveformHeight();

        ConfigureNavCanvas(navWaveformBackdrop, width, height);
        ConfigureNavCanvas(CanvasNavNotes, width, height);
        ConfigureNavCanvas(CanvasBookmarks, width, height);
        ConfigureNavCanvas(CanvasTimingChanges, width, height);
        ConfigureNavCanvas(canvasBookmarkLabels, width, height);
        ConfigureNavCanvas(canvasTimingChangeLabels, width, height);
        ConfigureNavCanvas(CanvasNavInputBox, width, height);

        EnsureNavWaveformImage();
        CanvasNavNotes.Children.Clear();
        navNoteVisuals.Clear();
        CanvasBookmarks.Children.Clear();
        CanvasTimingChanges.Children.Clear();
        canvasBookmarkLabels.Children.Clear();
        canvasTimingChangeLabels.Children.Clear();

        if (navWaveformImage != null) {
            navWaveformImage.Width = width;
            navWaveformImage.Height = height;
        }

        AutomationHelper.SetItemStatus(ImgWaveformVertical, $"width:{width:0.##}|height:{height:0.##}");
        ScheduleNavWaveformRender(width, height);

        if (mapEditor?.currentMapDifficulty != null) {
            var bookmarkBrush = ResolveBrush(userSettings.GetValueForKey(UserSettingsKey.NavBookmarkColor), Editor.NavBookmark.Colour, opacity: Editor.NavBookmark.Opacity);
            var bpmChangeBrush = ResolveBrush(userSettings.GetValueForKey(UserSettingsKey.NavBPMChangeColor), Editor.NavBPMChange.Colour, opacity: Editor.NavBPMChange.Opacity);

            DrawNavNotes(mapEditor.currentMapDifficulty.notes);

            foreach (var bookmark in mapEditor.currentMapDifficulty.bookmarks) {
                var y = BeatToNavY(bookmark.beat, height);
                CanvasBookmarks.Children.Add(CreateNavLine(width, y, bookmarkBrush, Editor.NavBookmark.Thickness));
                canvasBookmarkLabels.Children.Add(CreateBookmarkNavLabel(bookmark.name, y, width, bookmarkBrush));
            }

            foreach (var bpmChange in mapEditor.currentMapDifficulty.bpmChanges) {
                var y = BeatToNavY(bpmChange.globalBeat, height);
                CanvasTimingChanges.Children.Add(CreateNavLine(width, y, bpmChangeBrush, Editor.NavBPMChange.Thickness));
                foreach (var label in CreateTimingChangeNavLabels(bpmChange, y, width)) {
                    canvasTimingChangeLabels.Children.Add(label);
                }
            }
        }

        RefreshSongProgressIndicator();
    }

    void RefreshSongProgressIndicator() {
        if (lineSongProgress == null || ImgWaveformVertical == null) {
            return;
        }

        var width = GetNavWaveformWidth();
        var height = GetNavWaveformHeight();
        var ratio = SliderSongProgress.Maximum <= 0 ? 0 : Math.Clamp(currentSongPositionMilliseconds / SliderSongProgress.Maximum, 0, 1);
        var y = height * (1 - ratio);
        if (songIsPlaying &&
            !double.IsNaN(lastSongProgressIndicatorY) &&
            Math.Abs(y - lastSongProgressIndicatorY) < PlaybackProgressIndicatorEpsilon) {
            return;
        }

        lineSongProgress.StartPoint = new Point(0, y);
        lineSongProgress.EndPoint = new Point(width, y);
        lastSongProgressIndicatorY = y;
        if (!songIsPlaying ||
            double.IsNaN(lastSongProgressAutomationPositionMilliseconds) ||
            Math.Abs(currentSongPositionMilliseconds - lastSongProgressAutomationPositionMilliseconds) >= PlaybackUiTextUpdateIntervalMilliseconds) {
            AutomationHelper.SetItemStatus(lineSongProgress, $"y:{y:0.##}|height:{height:0.##}|ratio:{ratio:0.####}");
            lastSongProgressAutomationPositionMilliseconds = currentSongPositionMilliseconds;
        }
    }

    void RefreshSongMouseoverIndicator(double pointerY, bool isVisible) {
        if (lineSongMouseover == null || ImgWaveformVertical == null) {
            return;
        }

        var width = GetNavWaveformWidth();
        var height = GetNavWaveformHeight();
        var y = Math.Clamp(pointerY, 0, height);
        lineSongMouseover.StartPoint = new Point(0, y);
        lineSongMouseover.EndPoint = new Point(width, y);
        lineSongMouseover.Opacity = isVisible ? 1 : 0;
    }

    bool TryGetNavMouseoverBeat(out double beat) {
        beat = 0;
        if (lineSongMouseover == null ||
            lineSongMouseover.Opacity <= 0 ||
            ImgWaveformVertical == null ||
            SliderSongProgress.Maximum <= 0) {
            return false;
        }

        var height = GetNavWaveformHeight();
        if (height <= 0) {
            return false;
        }

        var ratio = 1 - Math.Clamp(lineSongMouseover.StartPoint.Y / height, 0, 1);
        beat = ratio * SliderSongProgress.Maximum / 60000.0 * Math.Max(1, CurrentGlobalBpm);
        return true;
    }

    double GetNavWaveformWidth() {
        if (navWaveformVisualHost != null && navWaveformVisualHost.Bounds.Width > 1) {
            return Math.Max(20, navWaveformVisualHost.Bounds.Width);
        }

        if (borderNavWaveform != null && borderNavWaveform.Bounds.Width > 1) {
            return Math.Max(20, borderNavWaveform.Bounds.Width);
        }

        if (ImgWaveformVertical != null && ImgWaveformVertical.Bounds.Width > 1) {
            return Math.Max(20, ImgWaveformVertical.Bounds.Width);
        }

        return Math.Max(20, ImgWaveformVertical?.Width ?? 64);
    }

    double GetNavWaveformHeight() {
        if (navWaveformVisualHost != null && navWaveformVisualHost.Bounds.Height > 1) {
            return Math.Max(1, navWaveformVisualHost.Bounds.Height);
        }

        if (borderNavWaveform != null && borderNavWaveform.Bounds.Height > 1) {
            return Math.Max(1, borderNavWaveform.Bounds.Height);
        }

        if (ImgWaveformVertical != null && ImgWaveformVertical.Bounds.Height > 1) {
            return Math.Max(1, ImgWaveformVertical.Bounds.Height);
        }

        if (navWaveformVisualHost != null && navWaveformVisualHost.Height > 1) {
            return navWaveformVisualHost.Height;
        }

        if (ImgWaveformVertical != null && ImgWaveformVertical.Height > 1) {
            return ImgWaveformVertical.Height;
        }

        return Math.Max(1, GetEditorViewportHeight());
    }

    double BeatToNavY(double beat, double navHeight) {
        var totalBeats = Math.Max(1, currentSongDurationSeconds * Math.Max(1, CurrentGlobalBpm) / 60.0);
        var ratio = Math.Clamp(beat / totalBeats, 0, 1);
        return navHeight * (1 - ratio);
    }

    static void ConfigureNavCanvas(Canvas canvas, double width, double height) {
        canvas.Width = width;
        canvas.Height = height;
    }

    Line CreateNavLine(double width, double y, IBrush brush, double thickness) {
        return new Line {
            StartPoint = new Point(0, y),
            EndPoint = new Point(width, y),
            Stroke = brush,
            StrokeThickness = thickness
        };
    }

    Border CreateBookmarkNavLabel(string text, double y, double width, IBrush foreground) {
        var label = new Border {
            Background = new SolidColorBrush(AvaloniaColor.Parse("#BF000000")),
            Padding = new Thickness(2, 0),
            Child = new TextBlock {
                Text = text,
                FontSize = 10,
                FontWeight = FontWeight.Bold,
                Foreground = foreground,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Width = Math.Max(24, width - 2),
                TextAlignment = TextAlignment.Right
            }
        };
        Canvas.SetLeft(label, 0);
        Canvas.SetTop(label, Math.Clamp(y - 8, 0, Math.Max(0, ImgWaveformVertical.Height - 14)));
        return label;
    }

    IEnumerable<Border> CreateTimingChangeNavLabels(BPMChange bpmChange, double y, double width) {
        var labelBrush = ResolveBrush(userSettings.GetValueForKey(UserSettingsKey.NavBPMChangeLabelColor), Editor.NavBPMChange.LabelColour, opacity: Editor.NavBPMChange.Opacity);

        Border makeLabel(string text, double top) {
            var label = new Border {
                Background = new SolidColorBrush(AvaloniaColor.Parse("#BF000000")),
                Padding = new Thickness(2, 0),
                Child = new TextBlock {
                    Text = text,
                    FontSize = 10,
                    FontWeight = FontWeight.Bold,
                    Foreground = labelBrush,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    Width = Math.Max(24, width - 2),
                    TextAlignment = TextAlignment.Left
                }
            };
            Canvas.SetLeft(label, 0);
            Canvas.SetTop(label, Math.Clamp(top, 0, Math.Max(0, ImgWaveformVertical.Height - 14)));
            return label;
        }

        yield return makeLabel($"1/{Math.Max(1, bpmChange.gridDivision)} beat", y - 8);
        yield return makeLabel($"{FormatNumber(bpmChange.BPM)} BPM", y - 19);
    }

    SolidColorBrush ResolveBrush(string? colorValue, string fallbackColor, double opacity = 1) {
        AvaloniaColor color;
        try {
            color = AvaloniaColor.Parse(string.IsNullOrWhiteSpace(colorValue) ? fallbackColor : colorValue);
        } catch {
            color = AvaloniaColor.Parse(fallbackColor);
        }

        if (opacity < 1) {
            color = AvaloniaColor.FromArgb((byte)Math.Clamp((int)Math.Round(color.A * opacity), 0, 255), color.R, color.G, color.B);
        }

        return new SolidColorBrush(color);
    }

    static string FormatMedalDistance(int distance) {
        return distance == 0 ? "Auto" : distance.ToString(CultureInfo.InvariantCulture);
    }

    static string FormatCoverFileName(string fileName) {
        return string.IsNullOrWhiteSpace(fileName) ? "N/A" : fileName;
    }

    static string FormatNumber(double value) {
        return value.ToString("0.##", CultureInfo.InvariantCulture);
    }

    static bool TryParseDouble(string? text, out double value) {
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) ||
               double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value);
    }

    void UpdateSongDurationText() {
        if (TxtSongDuration != null) {
            TxtSongDuration.Text = Helper.TimeFormat(currentSongDurationSeconds);
        }
    }

    void UpdateCoverPreview() {
        if (ImgCover == null) {
            return;
        }

        coverPreviewBitmap?.Dispose();
        coverPreviewBitmap = null;

        string? coverPath = null;
        if (mapEditor != null) {
            var coverFileName = GetMapString("_coverImageFilename");
            if (!string.IsNullOrWhiteSpace(coverFileName)) {
                coverPath = Path.Combine(mapEditor.mapFolder, coverFileName);
            }
        }

        coverPreviewBitmap = TryLoadBitmap(coverPath ?? string.Empty) ??
            TryLoadBitmap(Path.Combine(AppContext.BaseDirectory, "Resources", "placeholder.png"));
        ImgCover.Source = coverPreviewBitmap;
    }

    void SuspendDifficultyPredictionUpdates() {
        difficultyPredictionUpdateSuspensionDepth++;
    }

    void ResumeDifficultyPredictionUpdates() {
        if (difficultyPredictionUpdateSuspensionDepth <= 0) {
            difficultyPredictionUpdateSuspensionDepth = 0;
            return;
        }

        difficultyPredictionUpdateSuspensionDepth--;
        if (difficultyPredictionUpdateSuspensionDepth == 0 && difficultyPredictionUpdatePending) {
            difficultyPredictionUpdatePending = false;
            UpdateDifficultyPredictionCore();
        }
    }

    internal void UpdateDifficultyPrediction() {
        if (difficultyPredictionUpdateSuspensionDepth > 0) {
            difficultyPredictionUpdatePending = true;
            return;
        }

        UpdateDifficultyPredictionCore();
    }

    void UpdateDifficultyPredictionCore() {
        var predictor = ResolveDifficultyPredictor();
        var showInMapStats = GetSettingBool(UserSettingsKey.DifficultyPredictorShowInMapStats, DefaultUserSettings.DifficultyPredictorShowInMapStats);
        if (!showInMapStats || mapEditor?.currentMapDifficulty == null || !predictor.GetSupportedFeatures().HasFlag(IDifficultyPredictor.Features.RealTime)) {
            DifficultyPrediction.IsVisible = false;
            DifficultyPrediction.Text = "Difficulty prediction unavailable in this slice.";
            return;
        }

        var supportedFeatures = predictor.GetSupportedFeatures();
        var prediction = predictor.PredictDifficulty(mapEditor.currentMapDifficulty.notes, mapEditor.GlobalBPM, mapEditor.SongDuration);
        if (prediction.HasValue && (float.IsNaN(prediction.Value) || float.IsInfinity(prediction.Value))) {
            prediction = supportedFeatures.HasFlag(IDifficultyPredictor.Features.AlwaysPredict) ? 0 : null;
        }
        DifficultyPrediction.IsVisible = true;
        if (prediction.HasValue) {
            var precise = GetSettingBool(UserSettingsKey.DifficultyPredictorShowPrecise, DefaultUserSettings.DifficultyPredictorShowPrecise) &&
                supportedFeatures.HasFlag(IDifficultyPredictor.Features.PreciseFloat);
            var displayValue = Math.Round(prediction.Value, precise ? 2 : 0);
            DifficultyPrediction.Text = $"Difficulty: {displayValue.ToString(precise ? "#0.00" : "0", CultureInfo.CurrentCulture)}";
            SetTextForeground(DifficultyPrediction, Edda.Const.DifficultyPrediction.Colour);
            return;
        }

        if (!supportedFeatures.HasFlag(IDifficultyPredictor.Features.AlwaysPredict)) {
            DifficultyPrediction.Text = "Difficulty: ???";
            SetTextForeground(DifficultyPrediction, Edda.Const.DifficultyPrediction.WarningColour);
            return;
        }

        DifficultyPrediction.Text = "Difficulty: 0";
        SetTextForeground(DifficultyPrediction, Edda.Const.DifficultyPrediction.Colour);
    }

    internal void RefreshNavigationLayersFromSettings() {
        ImgWaveformVertical.IsVisible = GetSettingBool(UserSettingsKey.EnableNavWaveform, DefaultUserSettings.EnableNavWaveform);

        var showBookmarks = GetSettingBool(UserSettingsKey.EnableNavBookmarks, DefaultUserSettings.EnableNavBookmarks);
        var showTimingChanges = GetSettingBool(UserSettingsKey.EnableNavBPMChanges, DefaultUserSettings.EnableNavBPMChanges);
        var showNotes = GetSettingBool(UserSettingsKey.EnableNavNotes, DefaultUserSettings.EnableNavNotes);

        CanvasBookmarks.IsVisible = showBookmarks;
        canvasBookmarkLabels.IsVisible = showBookmarks;
        CanvasTimingChanges.IsVisible = showTimingChanges;
        canvasTimingChangeLabels.IsVisible = showTimingChanges;
        CanvasNavNotes.IsVisible = showNotes;
        RefreshNavigationVisuals();
    }

    void AddBookmarkAtCurrentPosition() {
        if (mapEditor == null) {
            return;
        }

        mapEditor.AddBookmark(new Bookmark(GetCurrentSongBeat(), Editor.NavBookmark.DefaultName));
    }

    void AddBookmarkAtEditorPointerOrCurrentPosition(bool snappedToGrid) {
        var beat = editorPointerInside
            ? GetActiveEditorBeat(snappedToGrid)
            : TryGetNavMouseoverBeat(out var navBeat)
                ? navBeat
                : GetCurrentSongBeat();
        AddBookmarkAtBeat(beat);
    }

    void AddBookmarkAtBeat(double beat) {
        if (mapEditor == null) {
            return;
        }

        mapEditor.AddBookmark(new Bookmark(Math.Round(beat, 3), Editor.NavBookmark.DefaultName));
    }

    void AddTimingChangeAtCurrentPosition() {
        if (mapEditor == null) {
            return;
        }

        mapEditor.AddBPMChange(new BPMChange(Math.Round(GetCurrentSongBeat(), 3), CurrentGlobalBpm, CurrentGridDivision));
        RefreshOpenToolWindows();
    }

    void AddTimingChangeAtEditorPointerOrCurrentPosition(bool snappedToGrid) {
        var beat = editorPointerInside
            ? GetActiveEditorBeat(snappedToGrid)
            : TryGetNavMouseoverBeat(out var navBeat)
                ? navBeat
                : GetCurrentSongBeat();
        AddTimingChangeAtBeat(beat);
    }

    void AddTimingChangeAtBeat(double beat) {
        if (mapEditor?.currentMapDifficulty == null) {
            return;
        }

        var roundedBeat = Math.Round(beat, 3);
        var previous = new BPMChange(0, mapEditor.GlobalBPM, CurrentGridDivision);
        foreach (var change in mapEditor.currentMapDifficulty.bpmChanges) {
            if (change.globalBeat < roundedBeat) {
                previous = change;
            }
        }

        mapEditor.AddBPMChange(new BPMChange(roundedBeat, previous.BPM, previous.gridDivision));
        RefreshOpenToolWindows();
    }

    void AddNoteAtCurrentPosition(int column) {
        if (mapEditor == null || column < 0 || column > 3) {
            return;
        }

        var note = new Note(Math.Round(GetCurrentSongBeat(), 3), column);
        mapEditor.AddNotes(note);
        drummer?.Play(note.col);
        AnimateDrum(note.col);
        AnimateNote(note);
        UpdateDifficultyPrediction();
    }

    bool TryHandleEditorNumberKey(Key key) {
        if (mapEditor == null) {
            return false;
        }

        var column = key switch {
            Key.D1 or Key.NumPad1 => 0,
            Key.D2 or Key.NumPad2 => 1,
            Key.D3 or Key.NumPad3 => 2,
            Key.D4 or Key.NumPad4 => 3,
            _ => -1
        };
        if (column < 0) {
            return false;
        }
        if (songIsPlaying) {
            AddNoteAtCurrentPosition(column);
            return true;
        }

        if (!editorPointerInside) {
            return false;
        }

        AddNoteAtEditorBeat(column);
        return true;
    }

    internal void RefreshOpenToolWindows() {
        if (changeBpmWindow is ChangeBpmWindow bpmWindow && bpmWindow.IsVisible) {
            bpmWindow.RefreshRows();
        }
    }

    internal void RefreshEditorGridFromToolWindow() {
        RefreshEditorSurface();
    }

    double GetCurrentSongBeat() {
        return currentSongPositionMilliseconds / 60000.0 * Math.Max(1, CurrentGlobalBpm);
    }

    double ResolveSongDurationSeconds() {
        var fallbackDurationSeconds = Math.Max(1, GetMapDouble("_songApproximativeDuration"));
        var songPath = CurrentSongPath;
        if (string.IsNullOrWhiteSpace(songPath) || !File.Exists(songPath)) {
            return fallbackDurationSeconds;
        }

        try {
            using var vorbisStream = TryOpenVorbis(songPath);
            var actualDurationSeconds = Math.Max(1, vorbisStream.TotalTime.TotalSeconds);
            var approximativeDurationSeconds = (int)vorbisStream.TotalTime.TotalSeconds + 1;
            if (mapEditor != null && (int)fallbackDurationSeconds != approximativeDurationSeconds) {
                mapEditor.SetMapValue("_songApproximativeDuration", JToken.FromObject(approximativeDurationSeconds));
            }

            return actualDurationSeconds;
        } catch {
            return fallbackDurationSeconds;
        }
    }

    internal IDifficultyPredictor ResolveDifficultyPredictor() {
        return userSettings.GetValueForKey(UserSettingsKey.DifficultyPredictorAlgorithm) switch {
            Edda.Const.DifficultyPrediction.SupportedAlgorithms.Nytilde => DifficultyPredictorNytilde.SINGLETON,
            Edda.Const.DifficultyPrediction.SupportedAlgorithms.Melchior => DifficultyPredictorMelchior.SINGLETON,
            Edda.Const.DifficultyPrediction.SupportedAlgorithms.Timeline => DifficultyPredictorTimeline.SINGLETON,
            _ => DifficultyPredictorPKBeam.SINGLETON
        };
    }

    static void SetTextForeground(TextBlock textBlock, System.Drawing.Color color) {
        textBlock.Foreground = new SolidColorBrush(AvaloniaColor.FromArgb(color.A, color.R, color.G, color.B));
    }

    static VorbisWaveReader TryOpenVorbis(string songFilePath) {
        try {
            return new VorbisWaveReader(songFilePath);
        } catch (Exception ex) {
            throw new InvalidDataException("The .ogg file is corrupted.", ex);
        }
    }

    public sealed record PlaybackDeviceOption(string Id, string Name) {
        public override string ToString() {
            return Name;
        }
    }

    sealed class EditorUiAdapter : IMapEditorUiAdapter {
        readonly MainWindow owner;
        readonly UserSettingsManager userSettings;
        string? clipboardText;

        public EditorUiAdapter(MainWindow owner, UserSettingsManager userSettings) {
            this.owner = owner;
            this.userSettings = userSettings;
        }

        public string GetUserSetting(string key) => userSettings.GetValueForKey(key) ?? string.Empty;
        public bool IsShiftKeyDown => owner.shiftKeyDown;
        public void UpdateDifficultyButtons() => owner.RefreshDifficultyButtons();
        public void DrawEditorGrid(bool redrawWaveform = true) => owner.RefreshEditorSurface();
        public void RefreshBPMChanges() => owner.RefreshOpenToolWindows();
        public void RefreshDiscordPresence() { }
        public void SetMapStats(MapStats stats) => owner.SetMapStats(stats);
        public void DrawNotes(IEnumerable<Note> notes) => owner.DrawEditorNotes(notes);
        public void DrawNavNotes(IEnumerable<Note> notes) => owner.DrawNavNotes(notes);
        public void UndrawNotes(IEnumerable<Note> notes) => owner.UndrawEditorNotes(notes);
        public void UndrawNavNotes(IEnumerable<Note> notes) => owner.UndrawNavNotes(notes);
        public void HighlightNotes(IEnumerable<Note> notes) => owner.UpdateEditorNoteHighlights(notes, isHighlighted: true);
        public void HighlightNavNotes(IEnumerable<Note> notes) => owner.UpdateNavNoteHighlights(notes, isHighlighted: true);
        public void HighlightAllNotes() => owner.UpdateAllEditorNoteHighlights(isHighlighted: true);
        public void HighlightAllNavNotes() => owner.UpdateAllNavNoteHighlights(isHighlighted: true);
        public void UnhighlightNotes(IEnumerable<Note> notes) => owner.UpdateEditorNoteHighlights(notes, isHighlighted: false);
        public void UnhighlightNavNotes(IEnumerable<Note> notes) => owner.UpdateNavNoteHighlights(notes, isHighlighted: false);
        public void UnhighlightAllNotes() => owner.UpdateAllEditorNoteHighlights(isHighlighted: false);
        public void UnhighlightAllNavNotes() => owner.UpdateAllNavNoteHighlights(isHighlighted: false);
        public void SetClipboardText(string text) => clipboardText = text;
        public string? GetClipboardText() => clipboardText;
    }

    sealed class AvaloniaSongPreviewUiAdapter : ISongPreviewUiAdapter {
        readonly MainWindow owner;

        public AvaloniaSongPreviewUiAdapter(MainWindow owner) {
            this.owner = owner;
        }

        public float GetSongVolume() {
            return (float)owner.SliderSongVol.Value;
        }

        public int GetEditorAudioLatency() {
            return owner.editorAudioLatency;
        }

        public void SetPreviewPlaying(bool isPlaying) {
            owner.SetButtonIcon(owner.BtnPlayPreview, isPlaying ? "stopButton.png" : "playButton.png", isPlaying ? "Stop preview" : "Play preview", 14);
        }

        public void SetPreviewButtonEnabled(bool isEnabled) {
            owner.BtnPlayPreview.IsEnabled = isEnabled;
            owner.BtnPlayPreview.Opacity = isEnabled ? 1 : 0.6;
        }
    }

    sealed class AvaloniaNoteScannerUiAdapter : INoteScannerUiAdapter {
        readonly MainWindow owner;

        public AvaloniaNoteScannerUiAdapter(MainWindow owner) {
            this.owner = owner;
        }

        public void InvokeOnUiThread(Action action) {
            Dispatcher.UIThread.Post(action);
        }

        public void AnimateDrum(int column) {
            owner.AnimateDrum(column);
        }

        public void AnimateNote(Note note) {
            owner.AnimateNote(note);
        }
    }
}
