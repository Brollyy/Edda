using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Edda.Avalonia.Services;
using Edda.Classes.MapEditorNS.NoteNS;
using Edda.Classes.MapEditorNS.Stats;
using Edda.Const;
using Edda.Startup;
using Newtonsoft.Json.Linq;
using NAudio.Vorbis;
using AvaloniaColor = Avalonia.Media.Color;
using Button = Avalonia.Controls.Button;
using EddaProgram = Edda.Const.Program;
using PixelPoint = Avalonia.PixelPoint;
using TextBox = Avalonia.Controls.TextBox;

namespace Edda.Avalonia.Windows;

public sealed class MainWindow : Window {
    readonly AppSession appSession;
    readonly UserSettingsManager userSettings;
    readonly EditorUiAdapter mapEditorUiAdapter;

    MapEditor? mapEditor;
    bool suppressControlEvents;
    bool textInputHasFocus;
    bool songIsPlaying;
    bool snapToGrid = true;
    bool navWaveformDragging;
    double currentSongDurationSeconds;
    double currentSongPositionMilliseconds;

    MenuItem menuItemSnapToGrid = null!;
    MenuItem menuItemClearCache = null!;
    Border leftSidebar = null!;
    Border rightSidebar = null!;

    Window? bpmFinderWindow;
    Window? predictorWindow;
    Window? aboutWindow;
    Window? changeBpmWindow;
    Window? customizeNavBarWindow;
    Window? songPreviewWindow;

    public string WindowId => "AppMainWindow";

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
    public TextBlock TxtSongVol { get; private set; } = null!;
    public TextBlock TxtDrumVol { get; private set; } = null!;
    public CheckBox CheckMetronome { get; private set; } = null!;
    public CheckBox CheckWaveform { get; private set; } = null!;
    public CheckBox CheckGridSnap { get; private set; } = null!;
    public TextBox TxtGridDivision { get; private set; } = null!;
    public TextBox TxtGridSpacing { get; private set; } = null!;
    public TextBlock LblSelectedBeat { get; private set; } = null!;
    public TextBlock DifficultyPrediction { get; private set; } = null!;
    public Border ImgWaveformVertical { get; private set; } = null!;
    public TextBlock CanvasBookmarks { get; private set; } = null!;
    public TextBlock CanvasTimingChanges { get; private set; } = null!;
    public TextBlock CanvasNavNotes { get; private set; } = null!;

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

    public IReadOnlyList<PlaybackDeviceOption> PlaybackDevices { get; }
    public string? PlaybackDeviceId { get; private set; }
    public bool PlayingOnDefaultDevice { get; private set; } = true;
    public bool DefaultDeviceAvailable => PlaybackDevices.Count > 0;

    public MainWindow(AppSession appSession, MapDocumentSummary summary) {
        this.appSession = appSession;
        userSettings = appSession.UserSettings;
        mapEditorUiAdapter = new EditorUiAdapter(userSettings);

        AutomationHelper.SetAutomationId(this, WindowId);
        Title = "Edda";
        Width = 1180;
        Height = 820;
        MinWidth = 980;
        MinHeight = 640;
        Position = new PixelPoint(160, 140);

        PlaybackDevices = [
            new PlaybackDeviceOption("speakers", "Speakers"),
            new PlaybackDeviceOption("headphones", "Headphones")
        ];

        Content = BuildRoot();
        Closed += (_, _) => mapEditor?.Dispose();
        KeyDown += OnKeyDown;

        LoadMap(summary.MapFolder);
        LoadSettingsFile();
        SetSnapToGrid(true);
        UpdatePlaybackUi();
    }

    public void OpenSettings() {
        appSession.ShowSettingsWindow(this);
    }

    public void LoadSettingsFile(bool reloadWaveforms = false) {
        var preferredPlaybackDeviceId = userSettings.GetValueForKey(UserSettingsKey.PlaybackDeviceID);
        var matchingDevice = PlaybackDevices.FirstOrDefault(device => string.Equals(device.Id, preferredPlaybackDeviceId, StringComparison.Ordinal));

        PlaybackDeviceId = matchingDevice?.Id;
        PlayingOnDefaultDevice = matchingDevice == null;

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

        ImgWaveformVertical.IsVisible = GetSettingBool(UserSettingsKey.EnableNavWaveform, DefaultUserSettings.EnableNavWaveform);
        CanvasBookmarks.IsVisible = GetSettingBool(UserSettingsKey.EnableNavBookmarks, DefaultUserSettings.EnableNavBookmarks);
        CanvasTimingChanges.IsVisible = GetSettingBool(UserSettingsKey.EnableNavBPMChanges, DefaultUserSettings.EnableNavBPMChanges);
        CanvasNavNotes.IsVisible = GetSettingBool(UserSettingsKey.EnableNavNotes, DefaultUserSettings.EnableNavNotes);
        DifficultyPrediction.IsVisible = GetSettingBool(UserSettingsKey.DifficultyPredictorShowInMapStats, DefaultUserSettings.DifficultyPredictorShowInMapStats);
    }

    public void UpdatePlaybackDevice(string? newPlaybackDeviceId, bool isDefaultDevice) {
        PlaybackDeviceId = string.IsNullOrWhiteSpace(newPlaybackDeviceId) ? null : newPlaybackDeviceId;
        PlayingOnDefaultDevice = isDefaultDevice || string.IsNullOrWhiteSpace(newPlaybackDeviceId);
        PauseSong();
    }

    public void PauseSong() {
        songIsPlaying = false;
        UpdatePlaybackUi();
    }

    public void RestartDrummer() {
        // Placeholder until the full audio pipeline is migrated.
    }

    Control BuildRoot() {
        var root = new DockPanel();
        var menu = BuildMenu();
        DockPanel.SetDock(menu, Dock.Top);
        root.Children.Add(menu);

        var body = new Grid {
            ColumnDefinitions = new ColumnDefinitions("280,*,260"),
            RowDefinitions = new RowDefinitions("Auto,*"),
            Background = new LinearGradientBrush {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                GradientStops = new GradientStops {
                    new GradientStop(AvaloniaColor.Parse("#F6F8FC"), 0),
                    new GradientStop(AvaloniaColor.Parse("#E6ECF8"), 1)
                }
            }
        };

        leftSidebar = new Border {
            Padding = new Thickness(18),
            Background = new SolidColorBrush(AvaloniaColor.Parse("#FFFFFF")),
            BorderBrush = new SolidColorBrush(AvaloniaColor.Parse("#C7D2E7")),
            BorderThickness = new Thickness(0, 0, 1, 0)
        };
        leftSidebar.Child = BuildLeftSidebar();
        body.Children.Add(leftSidebar);

        var centerPanel = BuildCenterPanel();
        Grid.SetColumn(centerPanel, 1);
        body.Children.Add(centerPanel);

        rightSidebar = new Border {
            Padding = new Thickness(18),
            Background = new SolidColorBrush(AvaloniaColor.Parse("#FFFFFF")),
            BorderBrush = new SolidColorBrush(AvaloniaColor.Parse("#C7D2E7")),
            BorderThickness = new Thickness(1, 0, 0, 0)
        };
        rightSidebar.Child = BuildRightSidebar();
        Grid.SetColumn(rightSidebar, 2);
        body.Children.Add(rightSidebar);

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
        menuItemSnapToGrid = BuildMenuItem("Snap Notes to Grid", "MenuItemSnapToGrid", (_, _) => SetSnapToGrid(menuItemSnapToGrid.IsChecked));
        menuItemSnapToGrid.ToggleType = MenuItemToggleType.CheckBox;
        editMenu.Items.Add(menuItemSnapToGrid);

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
            Spacing = 14
        };

        panel.Children.Add(new TextBlock {
            Text = "Map Details",
            FontSize = 24,
            FontWeight = FontWeight.Bold,
            Foreground = new SolidColorBrush(AvaloniaColor.Parse("#002668"))
        });

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
            MinWidth = 170
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
            TextWrapping = TextWrapping.Wrap
        }, "txtSongFileName");

        TxtCoverFileName = AutomationHelper.WithAutomationId(new TextBlock {
            Name = "txtCoverFileName",
            TextWrapping = TextWrapping.Wrap
        }, "txtCoverFileName");

        BtnPickSong = AutomationHelper.WithAutomationId(new Button {
            Name = "btnPickSong",
            Content = "Pick Song"
        }, "btnPickSong");
        BtnPickSong.Click += async (_, _) => {
            var selection = await appSession.PickSongFileAsync(this);
            if (!string.IsNullOrWhiteSpace(selection)) {
                ReplaceSong(selection);
            }
        };

        BtnPickCover = AutomationHelper.WithAutomationId(new Button {
            Name = "btnPickCover",
            Content = "Pick Cover"
        }, "btnPickCover");
        BtnPickCover.Click += async (_, _) => {
            var selection = await appSession.PickCoverFileAsync(this);
            if (!string.IsNullOrWhiteSpace(selection)) {
                ReplaceCover(selection);
            }
        };

        panel.Children.Add(CreateField("Song Name", TxtSongName));
        panel.Children.Add(CreateField("Artist", TxtArtistName));
        panel.Children.Add(CreateField("Mapper", TxtMapperName));
        panel.Children.Add(CreateField("BPM", TxtSongBpm));
        panel.Children.Add(CreateField("Environment", ComboEnvironment));
        panel.Children.Add(CreateField("Explicit", CheckExplicitContent));
        panel.Children.Add(CreateField("Song File", TxtSongFileName));
        panel.Children.Add(BtnPickSong);
        panel.Children.Add(CreateField("Cover File", TxtCoverFileName));
        panel.Children.Add(BtnPickCover);

        return new ScrollViewer {
            Content = panel
        };
    }

    Control BuildCenterPanel() {
        var panel = new Grid {
            RowDefinitions = new RowDefinitions("Auto,Auto,*,Auto"),
            Margin = new Thickness(20)
        };

        var heading = new StackPanel {
            Spacing = 4
        };
        heading.Children.Add(new TextBlock {
            Text = "Editor",
            FontSize = 30,
            FontWeight = FontWeight.Bold,
            Foreground = new SolidColorBrush(AvaloniaColor.Parse("#002668"))
        });
        heading.Children.Add(new TextBlock {
            Text = "This Avalonia slice now covers the core map management and playback shell while deeper editor behaviors continue to migrate.",
            TextWrapping = TextWrapping.Wrap
        });
        panel.Children.Add(heading);

        var playbackPanel = new Grid {
            Margin = new Thickness(0, 18, 0, 0),
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto"),
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
            ColumnSpacing = 12,
            RowSpacing = 10
        };
        Grid.SetRow(playbackPanel, 1);

        BtnSongPlayer = AutomationHelper.WithAutomationId(new Button {
            Name = "btnSongPlayer",
            Content = "Play Song",
            MinWidth = 120
        }, "btnSongPlayer");
        BtnSongPlayer.Click += (_, _) => ToggleSongPlayback();
        playbackPanel.Children.Add(BtnSongPlayer);

        SliderSongProgress = AutomationHelper.WithAutomationId(new Slider {
            Name = "sliderSongProgress",
            Minimum = 0,
            Maximum = 1000,
            Value = 0
        }, "sliderSongProgress");
        SliderSongProgress.PropertyChanged += OnSongProgressSliderPropertyChanged;
        Grid.SetColumn(SliderSongProgress, 1);
        playbackPanel.Children.Add(SliderSongProgress);

        TxtSongPosition = AutomationHelper.WithAutomationId(new TextBlock {
            Name = "txtSongPosition",
            VerticalAlignment = VerticalAlignment.Center,
            Text = Helper.TimeFormat(0)
        }, "txtSongPosition");
        Grid.SetColumn(TxtSongPosition, 2);
        playbackPanel.Children.Add(TxtSongPosition);

        SliderSongTempo = AutomationHelper.WithAutomationId(new Slider {
            Name = "sliderSongTempo",
            Minimum = Audio.MinSongTempo,
            Maximum = Audio.MaxSongTempo,
            Value = Audio.DefaultSongTempo
        }, "sliderSongTempo");
        SliderSongTempo.PropertyChanged += OnSongTempoSliderPropertyChanged;
        SliderSongTempo.PointerPressed += OnSongTempoSliderPointerPressed;
        SliderSongTempo.DoubleTapped += OnSongTempoSliderDoubleTapped;
        Grid.SetRow(SliderSongTempo, 1);
        Grid.SetColumn(SliderSongTempo, 1);
        playbackPanel.Children.Add(SliderSongTempo);

        TxtSongTempo = AutomationHelper.WithAutomationId(new TextBlock {
            Name = "txtSongTempo",
            VerticalAlignment = VerticalAlignment.Center
        }, "txtSongTempo");
        Grid.SetRow(TxtSongTempo, 1);
        Grid.SetColumn(TxtSongTempo, 2);
        playbackPanel.Children.Add(TxtSongTempo);

        BtnPlayPreview = AutomationHelper.WithAutomationId(new Button {
            Name = "btnPlayPreview",
            Content = "Play Preview"
        }, "btnPlayPreview");
        Grid.SetRow(BtnPlayPreview, 1);
        playbackPanel.Children.Add(BtnPlayPreview);

        SliderSongVol = AutomationHelper.WithAutomationId(new Slider {
            Name = "sliderSongVol",
            Minimum = 0,
            Maximum = 1,
            Value = DefaultUserSettings.DefaultSongVolume
        }, "sliderSongVol");
        SliderSongVol.PropertyChanged += (_, e) => {
            if (e.Property == RangeBase.ValueProperty) {
                UpdateVolumeTexts();
            }
        };
        Grid.SetRow(SliderSongVol, 2);
        Grid.SetColumn(SliderSongVol, 1);
        playbackPanel.Children.Add(SliderSongVol);

        TxtSongVol = AutomationHelper.WithAutomationId(new TextBlock {
            Name = "txtSongVol",
            VerticalAlignment = VerticalAlignment.Center
        }, "txtSongVol");
        TxtDrumVol = AutomationHelper.WithAutomationId(new TextBlock {
            Name = "txtDrumVol",
            VerticalAlignment = VerticalAlignment.Center
        }, "txtDrumVol");
        Grid.SetRow(TxtSongVol, 2);
        Grid.SetColumn(TxtSongVol, 2);
        playbackPanel.Children.Add(TxtSongVol);
        Grid.SetRow(TxtDrumVol, 3);
        Grid.SetColumn(TxtDrumVol, 2);
        playbackPanel.Children.Add(TxtDrumVol);

        SliderDrumVol = AutomationHelper.WithAutomationId(new Slider {
            Name = "sliderDrumVol",
            Minimum = 0,
            Maximum = 1,
            Value = DefaultUserSettings.DefaultNoteVolume
        }, "sliderDrumVol");
        SliderDrumVol.PropertyChanged += (_, e) => {
            if (e.Property == RangeBase.ValueProperty) {
                UpdateVolumeTexts();
            }
        };
        Grid.SetRow(SliderDrumVol, 3);
        Grid.SetColumn(SliderDrumVol, 1);
        playbackPanel.Children.Add(SliderDrumVol);

        var songVolumeLabel = new TextBlock {
            Text = "Song Volume",
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetRow(songVolumeLabel, 2);
        playbackPanel.Children.Add(songVolumeLabel);

        var drumVolumeLabel = new TextBlock {
            Text = "Drum Volume",
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetRow(drumVolumeLabel, 3);
        playbackPanel.Children.Add(drumVolumeLabel);
        panel.Children.Add(playbackPanel);

        var workspaceGrid = new Grid {
            Margin = new Thickness(0, 20, 0, 0),
            ColumnDefinitions = new ColumnDefinitions("*,120"),
            ColumnSpacing = 20
        };
        Grid.SetRow(workspaceGrid, 2);

        var timelinePanel = new StackPanel {
            Spacing = 14
        };
        LblSelectedBeat = AutomationHelper.WithAutomationId(new TextBlock {
            Name = "lblSelectedBeat",
            TextWrapping = TextWrapping.Wrap
        }, "lblSelectedBeat");
        timelinePanel.Children.Add(LblSelectedBeat);

        CheckMetronome = CreateCheckBox("checkMetronome", (_, _) => { });
        CheckWaveform = CreateCheckBox("checkWaveform", (_, _) => { });
        CheckGridSnap = CreateCheckBox("checkGridSnap", (_, _) => {
            if (!suppressControlEvents) {
                SetSnapToGrid(CheckGridSnap.IsChecked ?? false);
            }
        });
        TxtGridDivision = CreateTextBox("txtGridDivision");
        TxtGridDivision.LostFocus += (_, _) => CommitGridDivision();
        TxtGridDivision.KeyDown += OnNumericTextBoxKeyDown;
        TxtGridSpacing = CreateTextBox("txtGridSpacing");
        TxtGridSpacing.LostFocus += (_, _) => CommitGridSpacing();
        TxtGridSpacing.KeyDown += OnNumericTextBoxKeyDown;

        BtnChangeBPM = AutomationHelper.WithAutomationId(new Button {
            Name = "btnChangeBPM",
            Content = "Change BPM"
        }, "btnChangeBPM");
        BtnChangeBPM.Click += (_, _) => OpenChangeBpmWindow();

        BtnCustomizeNavBar = AutomationHelper.WithAutomationId(new Button {
            Name = "btnCustomizeNavBar",
            Content = "Customize Nav Bar"
        }, "btnCustomizeNavBar");
        BtnCustomizeNavBar.Click += (_, _) => OpenCustomizeNavBarWindow();

        BtnMakePreview = AutomationHelper.WithAutomationId(new Button {
            Name = "btnMakePreview",
            Content = "Create Preview"
        }, "btnMakePreview");
        BtnMakePreview.Click += (_, _) => OpenSongPreviewWindow();

        DifficultyPrediction = AutomationHelper.WithAutomationId(new TextBlock {
            Name = "difficultyPrediction",
            Text = "Difficulty prediction unavailable in this slice."
        }, "difficultyPrediction");

        timelinePanel.Children.Add(CreateField("Metronome", CheckMetronome));
        timelinePanel.Children.Add(CreateField("Waveform", CheckWaveform));
        timelinePanel.Children.Add(CreateField("Snap To Grid", CheckGridSnap));
        timelinePanel.Children.Add(CreateField("Grid Division", TxtGridDivision));
        timelinePanel.Children.Add(CreateField("Grid Spacing", TxtGridSpacing));
        timelinePanel.Children.Add(BtnChangeBPM);
        timelinePanel.Children.Add(BtnCustomizeNavBar);
        timelinePanel.Children.Add(BtnMakePreview);
        timelinePanel.Children.Add(DifficultyPrediction);
        workspaceGrid.Children.Add(timelinePanel);

        var navBorder = AutomationHelper.WithAutomationId(new Border {
            Name = "borderNavWaveform",
            Padding = new Thickness(12),
            Background = new SolidColorBrush(AvaloniaColor.Parse("#F0F4FB")),
            BorderBrush = new SolidColorBrush(AvaloniaColor.Parse("#C7D2E7")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10)
        }, "borderNavWaveform");
        Grid.SetColumn(navBorder, 1);

        var navPanel = new StackPanel {
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        ImgWaveformVertical = AutomationHelper.WithAutomationId(new Border {
            Name = "imgWaveformVertical",
            Width = 54,
            Height = 240,
            Background = new LinearGradientBrush {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                GradientStops = new GradientStops {
                    new GradientStop(AvaloniaColor.Parse("#92B3F0"), 0),
                    new GradientStop(AvaloniaColor.Parse("#18438A"), 1)
                }
            },
            BorderBrush = new SolidColorBrush(AvaloniaColor.Parse("#163768")),
            BorderThickness = new Thickness(1)
        }, "imgWaveformVertical");
        ImgWaveformVertical.PointerPressed += OnNavWaveformPointerPressed;
        ImgWaveformVertical.PointerMoved += OnNavWaveformPointerMoved;
        ImgWaveformVertical.PointerReleased += OnNavWaveformPointerReleased;
        navPanel.Children.Add(ImgWaveformVertical);

        CanvasBookmarks = AutomationHelper.WithAutomationId(new TextBlock {
            Name = "canvasBookmarks",
            Text = "Bookmarks",
            FontSize = 11
        }, "canvasBookmarks");
        CanvasTimingChanges = AutomationHelper.WithAutomationId(new TextBlock {
            Name = "canvasTimingChanges",
            Text = "Timing Changes",
            FontSize = 11
        }, "canvasTimingChanges");
        CanvasNavNotes = AutomationHelper.WithAutomationId(new TextBlock {
            Name = "canvasNavNotes",
            Text = "Notes",
            FontSize = 11
        }, "canvasNavNotes");

        navPanel.Children.Add(CanvasBookmarks);
        navPanel.Children.Add(CanvasTimingChanges);
        navPanel.Children.Add(CanvasNavNotes);
        navBorder.Child = navPanel;
        workspaceGrid.Children.Add(navBorder);

        panel.Children.Add(workspaceGrid);

        var footerActions = new StackPanel {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            Margin = new Thickness(0, 20, 0, 0)
        };
        var closeMapButton = new Button {
            Content = "Close Map"
        };
        closeMapButton.Click += (_, _) => BeginCloseMap();
        footerActions.Children.Add(closeMapButton);
        Grid.SetRow(footerActions, 3);
        panel.Children.Add(footerActions);

        return panel;
    }

    Control BuildRightSidebar() {
        var panel = new StackPanel {
            Spacing = 14
        };

        panel.Children.Add(new TextBlock {
            Text = "Difficulty",
            FontSize = 24,
            FontWeight = FontWeight.Bold,
            Foreground = new SolidColorBrush(AvaloniaColor.Parse("#002668"))
        });

        var difficultyButtons = new StackPanel {
            Orientation = Orientation.Horizontal,
            Spacing = 8
        };

        BtnChangeDifficulty0 = BuildDifficultyButton("btnChangeDifficulty0", 0);
        BtnChangeDifficulty1 = BuildDifficultyButton("btnChangeDifficulty1", 1);
        BtnChangeDifficulty2 = BuildDifficultyButton("btnChangeDifficulty2", 2);
        difficultyButtons.Children.Add(BtnChangeDifficulty0);
        difficultyButtons.Children.Add(BtnChangeDifficulty1);
        difficultyButtons.Children.Add(BtnChangeDifficulty2);
        panel.Children.Add(difficultyButtons);

        var difficultyActions = new StackPanel {
            Orientation = Orientation.Horizontal,
            Spacing = 8
        };
        BtnAddDifficulty = AutomationHelper.WithAutomationId(new Button {
            Name = "btnAddDifficulty",
            Content = "Add"
        }, "btnAddDifficulty");
        BtnAddDifficulty.Click += (_, _) => BeginAddDifficulty();
        BtnDeleteDifficulty = AutomationHelper.WithAutomationId(new Button {
            Name = "btnDeleteDifficulty",
            Content = "Delete"
        }, "btnDeleteDifficulty");
        BtnDeleteDifficulty.Click += (_, _) => BeginDeleteDifficulty();
        difficultyActions.Children.Add(BtnAddDifficulty);
        difficultyActions.Children.Add(BtnDeleteDifficulty);
        panel.Children.Add(difficultyActions);

        TxtDifficultyNumber = CreateTextBox("txtDifficultyNumber");
        TxtDifficultyNumber.LostFocus += (_, _) => CommitDifficultyNumber();
        TxtDifficultyNumber.KeyDown += OnNumericTextBoxKeyDown;

        TxtNoteSpeed = CreateTextBox("txtNoteSpeed");
        TxtNoteSpeed.LostFocus += (_, _) => CommitNoteSpeed();
        TxtNoteSpeed.KeyDown += OnNumericTextBoxKeyDown;

        TxtDistMedal0 = CreateTextBox("txtDistMedal0");
        TxtDistMedal0.GotFocus += (_, _) => ClearAutoText(TxtDistMedal0);
        TxtDistMedal0.LostFocus += (_, _) => CommitMedalDistance(TxtDistMedal0, RagnarockScoreMedals.Bronze);
        TxtDistMedal0.KeyDown += OnNumericTextBoxKeyDown;

        TxtDistMedal1 = CreateTextBox("txtDistMedal1");
        TxtDistMedal1.GotFocus += (_, _) => ClearAutoText(TxtDistMedal1);
        TxtDistMedal1.LostFocus += (_, _) => CommitMedalDistance(TxtDistMedal1, RagnarockScoreMedals.Silver);
        TxtDistMedal1.KeyDown += OnNumericTextBoxKeyDown;

        TxtDistMedal2 = CreateTextBox("txtDistMedal2");
        TxtDistMedal2.GotFocus += (_, _) => ClearAutoText(TxtDistMedal2);
        TxtDistMedal2.LostFocus += (_, _) => CommitMedalDistance(TxtDistMedal2, RagnarockScoreMedals.Gold);
        TxtDistMedal2.KeyDown += OnNumericTextBoxKeyDown;

        panel.Children.Add(CreateField("Difficulty Rank", TxtDifficultyNumber));
        panel.Children.Add(CreateField("Note Speed", TxtNoteSpeed));
        panel.Children.Add(CreateField("Bronze Medal", TxtDistMedal0));
        panel.Children.Add(CreateField("Silver Medal", TxtDistMedal1));
        panel.Children.Add(CreateField("Gold Medal", TxtDistMedal2));

        return new ScrollViewer {
            Content = panel
        };
    }

    Button BuildDifficultyButton(string automationId, int difficultyIndex) {
        var button = AutomationHelper.WithAutomationId(new Button {
            Name = automationId,
            Content = (difficultyIndex + 1).ToString(CultureInfo.InvariantCulture),
            MinWidth = 44
        }, automationId);
        button.Click += (_, _) => SelectDifficulty(difficultyIndex);
        return button;
    }

    TextBox CreateTextBox(string automationId) {
        var textBox = AutomationHelper.WithAutomationId(new TextBox {
            Name = automationId,
            MinWidth = 160
        }, automationId);
        textBox.GotFocus += (_, _) => textInputHasFocus = true;
        textBox.LostFocus += (_, _) => textInputHasFocus = false;
        return textBox;
    }

    CheckBox CreateCheckBox(string automationId, EventHandler<RoutedEventArgs> onChanged) {
        var checkBox = AutomationHelper.WithAutomationId(new CheckBox {
            Name = automationId,
            VerticalAlignment = VerticalAlignment.Center
        }, automationId);
        checkBox.IsCheckedChanged += onChanged;
        return checkBox;
    }

    static Control CreateField(string label, Control value) {
        var panel = new Grid {
            ColumnDefinitions = new ColumnDefinitions("120,*"),
            ColumnSpacing = 10
        };
        panel.Children.Add(new TextBlock {
            Text = label,
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap
        });
        Grid.SetColumn(value, 1);
        panel.Children.Add(value);
        return panel;
    }

    void LoadMap(string mapFolder) {
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

        currentSongDurationSeconds = Math.Max(1, GetMapDouble("_songApproximativeDuration"));
        SliderSongProgress.Maximum = currentSongDurationSeconds * 1000;
        SetSongPosition(0, updateSlider: true, updateNavWaveform: false);

        suppressControlEvents = false;
        RefreshDifficultyButtons();
        mapEditor.needsSave = mapDirtyState;
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
        TxtDistMedal0.Text = FormatMedalDistance(mapEditor.GetMedalDistance(RagnarockScoreMedals.Bronze, (RagnarockMapDifficulties)difficultyIndex));
        TxtDistMedal1.Text = FormatMedalDistance(mapEditor.GetMedalDistance(RagnarockScoreMedals.Silver, (RagnarockMapDifficulties)difficultyIndex));
        TxtDistMedal2.Text = FormatMedalDistance(mapEditor.GetMedalDistance(RagnarockScoreMedals.Gold, (RagnarockMapDifficulties)difficultyIndex));
    }

    void RefreshDifficultyButtons() {
        if (mapEditor == null) {
            return;
        }

        var count = mapEditor.numDifficulties;
        BtnChangeDifficulty0.IsVisible = count > 0;
        BtnChangeDifficulty1.IsVisible = count > 1;
        BtnChangeDifficulty2.IsVisible = count > 2;

        BtnAddDifficulty.IsEnabled = !songIsPlaying && count < 3;
        BtnDeleteDifficulty.IsEnabled = !songIsPlaying && count > 1;
    }

    void ToggleSongPlayback() {
        songIsPlaying = !songIsPlaying;
        UpdatePlaybackUi();
    }

    void UpdatePlaybackUi() {
        BtnSongPlayer.Content = songIsPlaying ? "Pause Song" : "Play Song";
        SliderSongTempo.IsEnabled = !songIsPlaying;
        SliderSongProgress.IsEnabled = !songIsPlaying;
        BtnAddDifficulty.IsEnabled = !songIsPlaying && mapEditor is { numDifficulties: < 3 };
        BtnDeleteDifficulty.IsEnabled = !songIsPlaying && mapEditor is { numDifficulties: > 1 };
        BtnChangeDifficulty0.IsEnabled = !songIsPlaying;
        BtnChangeDifficulty1.IsEnabled = !songIsPlaying;
        BtnChangeDifficulty2.IsEnabled = !songIsPlaying;
        BtnPlayPreview.IsEnabled = !songIsPlaying;
    }

    void OnSongProgressSliderPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e) {
        if (e.Property == RangeBase.ValueProperty && !suppressControlEvents) {
            SetSongPosition(SliderSongProgress.Value, updateSlider: false, updateNavWaveform: false);
        }
    }

    void OnSongTempoSliderPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e) {
        if (e.Property == RangeBase.ValueProperty) {
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

    void SetSongPosition(double milliseconds, bool updateSlider = true, bool updateNavWaveform = true) {
        var clamped = Math.Max(0, Math.Min(SliderSongProgress.Maximum, milliseconds));
        currentSongPositionMilliseconds = clamped;

        suppressControlEvents = true;
        if (updateSlider) {
            SliderSongProgress.Value = clamped;
        }
        suppressControlEvents = false;

        var seconds = clamped / 1000.0;
        TxtSongPosition.Text = Helper.TimeFormat(seconds);
        var bpm = Math.Max(1, GetMapDouble("_beatsPerMinute"));
        var beat = seconds * bpm / 60.0;
        LblSelectedBeat.Text = $"Time: {Helper.TimeFormat(seconds)} | Global Beat: {beat:0.##}";
    }

    void OnNavWaveformPointerPressed(object? sender, PointerPressedEventArgs e) {
        if (!e.GetCurrentPoint(ImgWaveformVertical).Properties.IsLeftButtonPressed) {
            return;
        }

        navWaveformDragging = true;
        e.Pointer.Capture(ImgWaveformVertical);
        UpdateSongPositionFromNavWaveform(e);
    }

    void OnNavWaveformPointerMoved(object? sender, PointerEventArgs e) {
        if (!navWaveformDragging || !e.GetCurrentPoint(ImgWaveformVertical).Properties.IsLeftButtonPressed) {
            return;
        }

        UpdateSongPositionFromNavWaveform(e);
    }

    void OnNavWaveformPointerReleased(object? sender, PointerReleasedEventArgs e) {
        navWaveformDragging = false;
        e.Pointer.Capture(null);
    }

    void UpdateSongPositionFromNavWaveform(PointerEventArgs e) {
        var position = e.GetPosition(ImgWaveformVertical);
        var ratio = ImgWaveformVertical.Bounds.Height <= 0
            ? 0
            : Math.Clamp(position.Y / ImgWaveformVertical.Bounds.Height, 0, 1);
        SetSongPosition(ratio * SliderSongProgress.Maximum, updateSlider: true, updateNavWaveform: false);
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

                mapEditor.CreateDifficulty(copyCurrentMarkers: result == AppDialogResult.Yes);
                SelectDifficulty(mapEditor.numDifficulties - 1);
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

                mapEditor.SetMapValue("_beatsPerMinute", JToken.FromObject(bpm));
                TxtSongBpm.Text = FormatNumber(bpm);
                SetSongPosition(currentSongPositionMilliseconds, updateSlider: true, updateNavWaveform: false);
            }
        );
    }

    void CommitDifficultyNumber() {
        CommitValidatedInteger(
            TxtDifficultyNumber,
            () => GetMapInt("_difficultyRank", RagnarockMapDifficulties.Current),
            value => value >= Editor.Difficulty.LevelMin && value <= Editor.Difficulty.LevelMax,
            value => mapEditor?.SetMapValue("_difficultyRank", JToken.FromObject(value), RagnarockMapDifficulties.Current),
            $"The difficulty level must be an integer between {Editor.Difficulty.LevelMin} and {Editor.Difficulty.LevelMax}."
        );
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
        CommitValidatedDouble(
            TxtGridSpacing,
            () => GetMapDouble("_editorGridSpacing", RagnarockMapDifficulties.Current, custom: true),
            _ => true,
            value => mapEditor?.SetMapValue("_editorGridSpacing", JToken.FromObject(value), RagnarockMapDifficulties.Current, custom: true),
            "The grid spacing must be numerical."
        );
    }

    void CommitGridDivision() {
        CommitValidatedInteger(
            TxtGridDivision,
            () => GetMapInt("_editorGridDivision", RagnarockMapDifficulties.Current, custom: true),
            value => value >= 1 && value <= Editor.GridDivisionMax,
            value => mapEditor?.SetMapValue("_editorGridDivision", JToken.FromObject(value), RagnarockMapDifficulties.Current, custom: true),
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
            if (!string.Equals(Path.GetFullPath(selectedSongPath), Path.GetFullPath(destinationPath), StringComparison.OrdinalIgnoreCase)) {
                Helper.FileDeleteIfExists(destinationPath);
                File.Copy(selectedSongPath, destinationPath, overwrite: true);
            }

            if (!string.Equals(previousSongPath, destinationPath, StringComparison.OrdinalIgnoreCase)) {
                Helper.FileDeleteIfExists(previousSongPath);
            }
        }

        mapEditor.SetMapValue("_songApproximativeDuration", JToken.FromObject((int)vorbisStream.TotalTime.TotalSeconds + 1));
        mapEditor.SetMapValue("_songFilename", JToken.FromObject(songFileName));
        mapEditor.SaveMap();

        currentSongDurationSeconds = Math.Max(1, (int)vorbisStream.TotalTime.TotalSeconds + 1);
        SliderSongProgress.Maximum = currentSongDurationSeconds * 1000;
        TxtSongFileName.Text = songFileName;
        SetSongPosition(0, updateSlider: true, updateNavWaveform: false);
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
        ShowToolWindow(() => bpmFinderWindow, window => bpmFinderWindow = window, () => CreateSentinelWindow("BPM Finder", "lblAvgBPM", "Average BPM"));
    }

    void OpenDifficultyPredictorWindow() {
        ShowToolWindow(() => predictorWindow, window => predictorWindow = window, () => CreateSentinelWindow("Difficulty Predictor", "btnPredict", "Predict", useButtonSentinel: true));
    }

    void OpenAboutWindow() {
        ShowToolWindow(() => aboutWindow, window => aboutWindow = window, () => CreateSentinelWindow("About Edda", "TxtGithubLink", EddaProgram.RepositoryURL));
    }

    void OpenChangeBpmWindow() {
        ShowToolWindow(() => changeBpmWindow, window => changeBpmWindow = window, () => CreateSentinelWindow("Change BPM", "dataBPMChange", "BPM Changes"));
    }

    void OpenCustomizeNavBarWindow() {
        ShowToolWindow(() => customizeNavBarWindow, window => customizeNavBarWindow = window, () => CreateSentinelWindow("Customize Nav Bar", "ColorWaveform", "Waveform Color"));
    }

    void OpenSongPreviewWindow() {
        ShowToolWindow(() => songPreviewWindow, window => songPreviewWindow = window, () => CreateSentinelWindow("Song Preview", "btnGenerate", "Generate", useButtonSentinel: true));
    }

    void ShowToolWindow(Func<Window?> getter, Action<Window?> setter, Func<Window> factory) {
        var existingWindow = getter();
        if (existingWindow is { IsVisible: true }) {
            existingWindow.Activate();
            return;
        }

        var window = factory();
        setter(window);
        window.Closed += (_, _) => {
            if (ReferenceEquals(getter(), window)) {
                setter(null);
            }
        };
        window.Show(this);
    }

    static Window CreateSentinelWindow(string title, string sentinelId, string sentinelText, bool useButtonSentinel = false) {
        var window = new Window {
            Title = title,
            Width = 360,
            Height = 220,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        AutomationHelper.SetAutomationId(window, $"Window{sentinelId}");
        window.Content = useButtonSentinel
            ? AutomationHelper.WithAutomationId(new Button {
                Name = sentinelId,
                Content = sentinelText,
                Margin = new Thickness(24)
            }, sentinelId)
            : AutomationHelper.WithAutomationId(new TextBlock {
                Name = sentinelId,
                Text = sentinelText,
                Margin = new Thickness(24),
                TextWrapping = TextWrapping.Wrap
            }, sentinelId);
        return window;
    }

    void OnKeyDown(object? sender, KeyEventArgs e) {
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
            ToggleSongPlayback();
            e.Handled = true;
        }
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
        readonly UserSettingsManager userSettings;
        string? clipboardText;

        public EditorUiAdapter(UserSettingsManager userSettings) {
            this.userSettings = userSettings;
        }

        public string GetUserSetting(string key) => userSettings.GetValueForKey(key) ?? string.Empty;
        public bool IsShiftKeyDown => false;
        public void UpdateDifficultyButtons() { }
        public void DrawEditorGrid(bool redrawWaveform = true) { }
        public void RefreshBPMChanges() { }
        public void RefreshDiscordPresence() { }
        public void SetMapStats(MapStats stats) { }
        public void DrawNotes(IEnumerable<Note> notes) { }
        public void DrawNavNotes(IEnumerable<Note> notes) { }
        public void UndrawNotes(IEnumerable<Note> notes) { }
        public void UndrawNavNotes(IEnumerable<Note> notes) { }
        public void HighlightNotes(IEnumerable<Note> notes) { }
        public void HighlightNavNotes(IEnumerable<Note> notes) { }
        public void HighlightAllNotes() { }
        public void HighlightAllNavNotes() { }
        public void UnhighlightNotes(IEnumerable<Note> notes) { }
        public void UnhighlightNavNotes(IEnumerable<Note> notes) { }
        public void UnhighlightAllNotes() { }
        public void UnhighlightAllNavNotes() { }
        public void SetClipboardText(string text) => clipboardText = text;
        public string? GetClipboardText() => clipboardText;
    }
}
