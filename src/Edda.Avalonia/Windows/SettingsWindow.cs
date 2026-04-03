using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Edda.Const;
using CoreProgram = Edda.Const.Program;

namespace Edda.Avalonia.Windows;

public sealed class SettingsWindow : Window {
    readonly AppSession appSession;
    readonly MainWindow caller;
    readonly UserSettingsManager userSettings;

    bool doneInit;
    bool suppressMapSaveSelectionHandling;

    public CheckBox CheckAutosave { get; private set; } = null!;
    public TextBox TxtDefaultMapper { get; private set; } = null!;
    public TextBox TxtDefaultNoteSpeed { get; private set; } = null!;
    public TextBox TxtDefaultGridSpacing { get; private set; } = null!;
    public ComboBox ComboNotePasteBehavior { get; private set; } = null!;
    public ComboBox ComboPlaybackDevice { get; private set; } = null!;
    public TextBox TxtAudioLatency { get; private set; } = null!;
    public ComboBox ComboDrumSample { get; private set; } = null!;
    public CheckBox CheckPanNotes { get; private set; } = null!;
    public CheckBox CheckShowSpectrogram { get; private set; } = null!;
    public TextBlock SpectrogramOptionsLabel { get; private set; } = null!;
    public StackPanel SpectrogramOptions { get; private set; } = null!;
    public ComboBox ComboSpectrogramQuality { get; private set; } = null!;
    public ComboBox ComboSpectrogramType { get; private set; } = null!;
    public TextBox TxtSpectrogramFrequency { get; private set; } = null!;
    public ComboBox ComboSpectrogramColormap { get; private set; } = null!;
    public CheckBox CheckSpectrogramFlipped { get; private set; } = null!;
    public CheckBox CheckSpectrogramChunking { get; private set; } = null!;
    public CheckBox CheckSpectrogramCache { get; private set; } = null!;
    public CheckBox CheckDiscord { get; private set; } = null!;
    public CheckBox CheckStartupUpdate { get; private set; } = null!;
    public ComboBox ComboMapSaveFolder { get; private set; } = null!;
    public TextBlock TxtMapSaveFolderPath { get; private set; } = null!;
    public Button BtnSave { get; private set; } = null!;

    public SettingsWindow(AppSession appSession, MainWindow caller) {
        this.appSession = appSession;
        this.caller = caller;
        userSettings = appSession.UserSettings;
        AutomationHelper.SetAutomationId(this, "SettingsWindow");

        Title = "Settings";
        Width = 420;
        MinWidth = 420;
        Height = 760;
        MinHeight = 620;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var root = new DockPanel();

        BtnSave = AutomationHelper.WithAutomationId(new Button {
            Name = "btnSave",
            Content = "OK",
            Width = 90,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 0, 0, 0)
        }, "btnSave");
        BtnSave.Click += (_, _) => Close();

        var footer = new Border {
            Padding = new Thickness(16),
            Child = BtnSave
        };
        DockPanel.SetDock(footer, Dock.Bottom);
        root.Children.Add(footer);

        var scrollViewer = new ScrollViewer {
            Content = BuildContent()
        };
        root.Children.Add(scrollViewer);

        Content = root;

        doneInit = false;
        InitializeValues();
        doneInit = true;
    }

    public void CommitTextField(string controlId) {
        switch (controlId) {
            case "txtDefaultMapper":
                CommitDefaultMapper();
                break;
            case "txtDefaultNoteSpeed":
                CommitDefaultNoteSpeed();
                break;
            case "txtDefaultGridSpacing":
                CommitDefaultGridSpacing();
                break;
            case "txtAudioLatency":
                CommitAudioLatency();
                break;
            case "txtSpectrogramFrequency":
                CommitSpectrogramFrequency();
                break;
            default:
                throw new InvalidOperationException($"Unsupported text field '{controlId}'.");
        }
    }

    Control BuildContent() {
        var content = new StackPanel {
            Margin = new Thickness(18),
            Spacing = 18
        };

        CheckAutosave = CreateCheckBox("CheckAutosave", OnAutosaveChanged);
        CheckShowSpectrogram = CreateCheckBox("CheckShowSpectrogram", OnShowSpectrogramChanged);
        TxtDefaultMapper = CreateTextBox("txtDefaultMapper");
        ComboNotePasteBehavior = CreateComboBox("comboNotePasteBehavior", OnNotePasteBehaviorChanged);
        TxtDefaultNoteSpeed = CreateTextBox("txtDefaultNoteSpeed");
        TxtDefaultGridSpacing = CreateTextBox("txtDefaultGridSpacing");

        content.Children.Add(CreateSection("Editor", [
            CreateFormRow("Autosave", CheckAutosave),
            CreateFormRow("Song Spectrogram", CheckShowSpectrogram),
            CreateFormRow("Default Mapper", TxtDefaultMapper),
            CreateFormRow("Default Note Speed", TxtDefaultNoteSpeed),
            CreateFormRow("Default Grid Spacing", TxtDefaultGridSpacing),
            CreateFormRow("Note Paste Behavior", ComboNotePasteBehavior)
        ]));

        ComboPlaybackDevice = CreateComboBox("comboPlaybackDevice", OnPlaybackDeviceChanged);
        TxtAudioLatency = CreateTextBox("txtAudioLatency");
        ComboDrumSample = CreateComboBox("comboDrumSample", OnDrumSampleChanged);
        CheckPanNotes = CreateCheckBox("checkPanNotes", OnPanNotesChanged);

        content.Children.Add(CreateSection("Audio Playback", [
            CreateFormRow("Output Device", ComboPlaybackDevice),
            CreateFormRow("Audio Latency", CreateInlineRow(TxtAudioLatency, "ms")),
            CreateFormRow("Note Sound", ComboDrumSample),
            CreateFormRow("Pan Note Sounds", CheckPanNotes)
        ]));

        ComboSpectrogramQuality = CreateComboBox("comboSpectrogramQuality", OnSpectrogramQualityChanged);
        ComboSpectrogramType = CreateComboBox("comboSpectrogramType", OnSpectrogramTypeChanged);
        TxtSpectrogramFrequency = CreateTextBox("txtSpectrogramFrequency");
        ComboSpectrogramColormap = CreateComboBox("comboSpectrogramColormap", OnSpectrogramColormapChanged);
        CheckSpectrogramFlipped = CreateCheckBox("checkSpectrogramFlipped", OnSpectrogramFlippedChanged);
        CheckSpectrogramChunking = CreateCheckBox("checkSpectrogramChunking", OnSpectrogramChunkingChanged);
        CheckSpectrogramCache = CreateCheckBox("checkSpectrogramCache", OnSpectrogramCacheChanged);

        SpectrogramOptionsLabel = new TextBlock {
            Text = "Song Spectrogram",
            FontSize = 20,
            FontWeight = FontWeight.Bold
        };
        content.Children.Add(SpectrogramOptionsLabel);

        SpectrogramOptions = AutomationHelper.WithAutomationId(new StackPanel {
            Name = "spectrogramOptions",
            Spacing = 10
        }, "spectrogramOptions");
        SpectrogramOptions.Children.Add(CreateFormRow("Quality", ComboSpectrogramQuality));
        SpectrogramOptions.Children.Add(CreateFormRow("Frequency Scale", ComboSpectrogramType));
        SpectrogramOptions.Children.Add(CreateFormRow("Max Frequency", CreateInlineRow(TxtSpectrogramFrequency, "Hz")));
        SpectrogramOptions.Children.Add(CreateFormRow("Color Theme", ComboSpectrogramColormap));
        SpectrogramOptions.Children.Add(CreateFormRow("Flip Image", CheckSpectrogramFlipped));
        SpectrogramOptions.Children.Add(CreateFormRow("Enable Chunking", CheckSpectrogramChunking));
        SpectrogramOptions.Children.Add(CreateFormRow("Cache Images", CheckSpectrogramCache));
        content.Children.Add(SpectrogramOptions);

        CheckDiscord = CreateCheckBox("checkDiscord", OnDiscordChanged);
        CheckStartupUpdate = CreateCheckBox("checkStartupUpdate", OnStartupUpdateChanged);
        ComboMapSaveFolder = CreateComboBox("comboMapSaveFolder", OnMapSaveFolderChangedAsync);
        TxtMapSaveFolderPath = AutomationHelper.WithAutomationId(new TextBlock {
            Name = "txtMapSaveFolderPath",
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12
        }, "txtMapSaveFolderPath");
        TxtMapSaveFolderPath.PointerReleased += OnMapSavePathPointerReleasedAsync;

        content.Children.Add(CreateSection("Miscellaneous", [
            CreateFormRow("Show in Discord", CheckDiscord),
            CreateFormRow("Check for Updates", CheckStartupUpdate),
            CreateFormRow("Map Save Location", ComboMapSaveFolder),
            TxtMapSaveFolderPath
        ]));

        return content;
    }

    void InitializeValues() {
        TxtDefaultMapper.Text = userSettings.GetValueForKey(UserSettingsKey.DefaultMapper);
        TxtDefaultNoteSpeed.Text = userSettings.GetValueForKey(UserSettingsKey.DefaultNoteSpeed);
        TxtDefaultGridSpacing.Text = userSettings.GetValueForKey(UserSettingsKey.DefaultGridSpacing);
        TxtAudioLatency.Text = userSettings.GetValueForKey(UserSettingsKey.EditorAudioLatency);
        TxtSpectrogramFrequency.Text = userSettings.GetValueForKey(UserSettingsKey.SpectrogramFrequency);

        CheckAutosave.IsChecked = userSettings.GetBoolForKey(UserSettingsKey.EnableAutosave);
        CheckShowSpectrogram.IsChecked = userSettings.GetBoolForKey(UserSettingsKey.EnableSpectrogram);
        CheckPanNotes.IsChecked = userSettings.GetBoolForKey(UserSettingsKey.PanDrumSounds);
        CheckSpectrogramCache.IsChecked = userSettings.GetBoolForKey(UserSettingsKey.SpectrogramCache);
        CheckSpectrogramFlipped.IsChecked = userSettings.GetBoolForKey(UserSettingsKey.SpectrogramFlipped);
        CheckSpectrogramChunking.IsChecked = userSettings.GetBoolForKey(UserSettingsKey.SpectrogramChunking);
        CheckDiscord.IsChecked = userSettings.GetBoolForKey(UserSettingsKey.EnableDiscordRPC);
        CheckStartupUpdate.IsChecked = userSettings.GetBoolForKey(UserSettingsKey.CheckForUpdates);

        PopulateNotePasteBehavior();
        PopulatePlaybackDevices();
        PopulateDrumSamples();
        PopulateSimpleCombo(ComboSpectrogramQuality, userSettings.GetValueForKey(UserSettingsKey.SpectrogramQuality), ["Low", "Medium", "High"]);
        PopulateSimpleCombo(ComboSpectrogramType, userSettings.GetValueForKey(UserSettingsKey.SpectrogramType), ["Standard", "Mel"]);
        PopulateSimpleCombo(ComboSpectrogramColormap, userSettings.GetValueForKey(UserSettingsKey.SpectrogramColormap), ["Blues", "Viridis", "Magma"]);
        PopulateSimpleCombo(ComboMapSaveFolder, GetMapSaveLocationLabel(), ["Documents", "Game Install"]);

        TxtMapSaveFolderPath.Text = ComboMapSaveFolder.SelectedIndex == 0
            ? CoreProgram.DocumentsMapFolder
            : userSettings.GetValueForKey(UserSettingsKey.MapSaveLocationPath);

        ToggleSpectrogramOptionsVisibility();
        ToggleMapPathVisibility();
    }

    void PopulateNotePasteBehavior() {
        var selectedValue = userSettings.GetValueForKey(UserSettingsKey.NotePasteBehavior) ?? DefaultUserSettings.NotePasteBehavior;
        var options = new[] {
            new LabeledValue(Editor.NotePasteBehavior.AlignToGlobalBeat, "Align to global beat"),
            new LabeledValue(Editor.NotePasteBehavior.AlignToFirstNoteBPM, "Align to first note BPM"),
            new LabeledValue(Editor.NotePasteBehavior.AlignToNoteBPM, "Align to all notes BPM")
        };

        ComboNotePasteBehavior.Items.Clear();
        foreach (var option in options) {
            ComboNotePasteBehavior.Items.Add(option);
            if (option.Value == selectedValue) {
                ComboNotePasteBehavior.SelectedItem = option;
            }
        }

        ComboNotePasteBehavior.SelectedItem ??= options.Last();
    }

    void PopulatePlaybackDevices() {
        var selectedId = userSettings.GetValueForKey(UserSettingsKey.PlaybackDeviceID);
        var defaultOption = new PlaybackDeviceChoice(null, "Default");
        var options = new List<PlaybackDeviceChoice> { defaultOption };
        options.AddRange(caller.PlaybackDevices.Select(device => new PlaybackDeviceChoice(device.Id, device.Name)));

        ComboPlaybackDevice.Items.Clear();
        foreach (var option in options) {
            ComboPlaybackDevice.Items.Add(option);
            if (!string.IsNullOrWhiteSpace(selectedId) && string.Equals(option.Id, selectedId, StringComparison.Ordinal)) {
                ComboPlaybackDevice.SelectedItem = option;
            }
        }

        ComboPlaybackDevice.SelectedItem ??= defaultOption;
        ComboPlaybackDevice.IsEnabled = options.Count > 0;
    }

    void PopulateDrumSamples() {
        var selectedSample = userSettings.GetValueForKey(UserSettingsKey.DrumSampleFile) ?? DefaultUserSettings.DrumSampleFile;
        var resourceDirectory = Path.Combine(AppContext.BaseDirectory, CoreProgram.ResourcesPath);
        var samples = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (Directory.Exists(resourceDirectory)) {
            foreach (var file in Directory.GetFiles(resourceDirectory)) {
                var extension = Path.GetExtension(file);
                if (!extension.Equals(".wav", StringComparison.OrdinalIgnoreCase) &&
                    !extension.Equals(".mp3", StringComparison.OrdinalIgnoreCase)) {
                    continue;
                }

                var fileName = Path.GetFileNameWithoutExtension(file);
                if (!fileName.EndsWith("1", StringComparison.Ordinal)) {
                    continue;
                }

                samples.Add(fileName[..^1]);
            }
        }

        samples.Add(DefaultUserSettings.DrumSampleFile);
        samples.Add("taiko");

        ComboDrumSample.Items.Clear();
        foreach (var sample in samples.OrderBy(sample => sample, StringComparer.OrdinalIgnoreCase)) {
            ComboDrumSample.Items.Add(sample);
            if (string.Equals(sample, selectedSample, StringComparison.OrdinalIgnoreCase)) {
                ComboDrumSample.SelectedItem = sample;
            }
        }

        ComboDrumSample.SelectedItem ??= selectedSample;
    }

    void PopulateSimpleCombo(ComboBox comboBox, string? selectedValue, IEnumerable<string> options) {
        comboBox.Items.Clear();
        foreach (var option in options) {
            comboBox.Items.Add(option);
            if (string.Equals(option, selectedValue, StringComparison.Ordinal)) {
                comboBox.SelectedItem = option;
            }
        }

        comboBox.SelectedItem ??= comboBox.Items.Cast<object?>().FirstOrDefault();
    }

    TextBox CreateTextBox(string name) {
        var textBox = AutomationHelper.WithAutomationId(new TextBox {
            Name = name,
            MinWidth = 160
        }, name);
        textBox.LostFocus += OnTextBoxLostFocus;
        textBox.KeyDown += OnTextBoxKeyDown;
        return textBox;
    }

    static ComboBox CreateComboBox(string name, EventHandler<SelectionChangedEventArgs> onSelectionChanged) {
        var comboBox = AutomationHelper.WithAutomationId(new ComboBox {
            Name = name,
            MinWidth = 160
        }, name);
        comboBox.SelectionChanged += onSelectionChanged;
        return comboBox;
    }

    static CheckBox CreateCheckBox(string name, EventHandler<RoutedEventArgs> onChanged) {
        var checkBox = AutomationHelper.WithAutomationId(new CheckBox {
            Name = name,
            VerticalAlignment = VerticalAlignment.Center
        }, name);
        checkBox.IsCheckedChanged += onChanged;
        return checkBox;
    }

    static Control CreateSection(string title, IEnumerable<Control> rows) {
        var section = new StackPanel {
            Spacing = 10
        };
        section.Children.Add(new TextBlock {
            Text = title,
            FontSize = 20,
            FontWeight = FontWeight.Bold
        });

        foreach (var row in rows) {
            section.Children.Add(row);
        }

        return section;
    }

    static Control CreateFormRow(string label, Control valueControl) {
        var grid = new Grid {
            ColumnDefinitions = new ColumnDefinitions("180,*"),
            ColumnSpacing = 12
        };

        grid.Children.Add(new TextBlock {
            Text = label,
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap
        });

        Grid.SetColumn(valueControl, 1);
        grid.Children.Add(valueControl);
        return grid;
    }

    static Control CreateInlineRow(Control primaryControl, string suffix) {
        var panel = new StackPanel {
            Orientation = Orientation.Horizontal,
            Spacing = 8
        };
        panel.Children.Add(primaryControl);
        panel.Children.Add(new TextBlock {
            Text = suffix,
            VerticalAlignment = VerticalAlignment.Center
        });
        return panel;
    }

    void OnAutosaveChanged(object? sender, RoutedEventArgs e) {
        if (!doneInit) {
            return;
        }

        userSettings.SetValueForKey(UserSettingsKey.EnableAutosave, CheckAutosave.IsChecked ?? false);
        UpdateSettings();
    }

    void OnTextBoxLostFocus(object? sender, RoutedEventArgs e) {
        if (!doneInit || sender is not TextBox textBox || string.IsNullOrWhiteSpace(textBox.Name)) {
            return;
        }

        CommitTextField(textBox.Name);
    }

    void OnTextBoxKeyDown(object? sender, KeyEventArgs e) {
        if (!doneInit || e.Key != Key.Enter || sender is not TextBox textBox || string.IsNullOrWhiteSpace(textBox.Name)) {
            return;
        }

        CommitTextField(textBox.Name);
        e.Handled = true;
    }

    void OnShowSpectrogramChanged(object? sender, RoutedEventArgs e) {
        if (!doneInit) {
            return;
        }

        ToggleSpectrogramOptionsVisibility();
        userSettings.SetValueForKey(UserSettingsKey.EnableSpectrogram, CheckShowSpectrogram.IsChecked ?? false);
        UpdateSettings();
    }

    void OnNotePasteBehaviorChanged(object? sender, SelectionChangedEventArgs e) {
        if (!doneInit || ComboNotePasteBehavior.SelectedItem is not LabeledValue option) {
            return;
        }

        userSettings.SetValueForKey(UserSettingsKey.NotePasteBehavior, option.Value);
        UpdateSettings();
    }

    void OnPlaybackDeviceChanged(object? sender, SelectionChangedEventArgs e) {
        if (!doneInit || ComboPlaybackDevice.SelectedItem is not PlaybackDeviceChoice choice) {
            return;
        }

        caller.UpdatePlaybackDevice(choice.Id, string.IsNullOrWhiteSpace(choice.Id));
        userSettings.SetValueForKey(UserSettingsKey.PlaybackDeviceID, choice.Id ?? string.Empty);
        UpdateSettings();
    }

    void OnDrumSampleChanged(object? sender, SelectionChangedEventArgs e) {
        if (!doneInit || ComboDrumSample.SelectedItem is not string sample) {
            return;
        }

        userSettings.SetValueForKey(UserSettingsKey.DrumSampleFile, sample);
        UpdateSettings();
        caller.PauseSong();
        caller.RestartDrummer();
    }

    void OnPanNotesChanged(object? sender, RoutedEventArgs e) {
        if (!doneInit) {
            return;
        }

        userSettings.SetValueForKey(UserSettingsKey.PanDrumSounds, CheckPanNotes.IsChecked ?? false);
        UpdateSettings();
        caller.PauseSong();
        caller.RestartDrummer();
    }

    void OnSpectrogramQualityChanged(object? sender, SelectionChangedEventArgs e) {
        if (!doneInit || ComboSpectrogramQuality.SelectedItem is not string quality) {
            return;
        }

        userSettings.SetValueForKey(UserSettingsKey.SpectrogramQuality, quality);
        UpdateSettings();
    }

    void OnSpectrogramTypeChanged(object? sender, SelectionChangedEventArgs e) {
        if (!doneInit || ComboSpectrogramType.SelectedItem is not string type) {
            return;
        }

        userSettings.SetValueForKey(UserSettingsKey.SpectrogramType, type);
        UpdateSettings();
    }

    void OnSpectrogramColormapChanged(object? sender, SelectionChangedEventArgs e) {
        if (!doneInit || ComboSpectrogramColormap.SelectedItem is not string colormap) {
            return;
        }

        userSettings.SetValueForKey(UserSettingsKey.SpectrogramColormap, colormap);
        UpdateSettings();
    }

    void OnSpectrogramFlippedChanged(object? sender, RoutedEventArgs e) {
        userSettings.SetValueForKey(UserSettingsKey.SpectrogramFlipped, CheckSpectrogramFlipped.IsChecked ?? false);
        if (doneInit) {
            UpdateSettings();
        }
    }

    void OnSpectrogramChunkingChanged(object? sender, RoutedEventArgs e) {
        userSettings.SetValueForKey(UserSettingsKey.SpectrogramChunking, CheckSpectrogramChunking.IsChecked ?? false);
        if (doneInit) {
            UpdateSettings();
        }
    }

    void OnSpectrogramCacheChanged(object? sender, RoutedEventArgs e) {
        userSettings.SetValueForKey(UserSettingsKey.SpectrogramCache, CheckSpectrogramCache.IsChecked ?? false);
        if (doneInit) {
            UpdateSettings();
        }
    }

    void OnDiscordChanged(object? sender, RoutedEventArgs e) {
        if (!doneInit) {
            return;
        }

        userSettings.SetValueForKey(UserSettingsKey.EnableDiscordRPC, CheckDiscord.IsChecked ?? false);
        UpdateSettings();
    }

    void OnStartupUpdateChanged(object? sender, RoutedEventArgs e) {
        if (!doneInit) {
            return;
        }

        userSettings.SetValueForKey(UserSettingsKey.CheckForUpdates, CheckStartupUpdate.IsChecked ?? false);
        UpdateSettings();
    }

    async void OnMapSaveFolderChangedAsync(object? sender, SelectionChangedEventArgs e) {
        if (!doneInit || suppressMapSaveSelectionHandling) {
            return;
        }

        var selectedLabel = ComboMapSaveFolder.SelectedItem as string ?? "Documents";
        if (string.Equals(selectedLabel, "Game Install", StringComparison.Ordinal)) {
            var gameInstall = await PickGameFolderAsync();
            if (string.IsNullOrWhiteSpace(gameInstall)) {
                suppressMapSaveSelectionHandling = true;
                ComboMapSaveFolder.SelectedItem = "Documents";
                suppressMapSaveSelectionHandling = false;

                userSettings.SetValueForKey(UserSettingsKey.MapSaveLocationIndex, DefaultUserSettings.MapSaveLocationIndex.ToString(CultureInfo.InvariantCulture));
                userSettings.SetValueForKey(UserSettingsKey.MapSaveLocationPath, DefaultUserSettings.MapSaveLocationPath);
                TxtMapSaveFolderPath.Text = CoreProgram.DocumentsMapFolder;
                ToggleMapPathVisibility();
                UpdateSettings();
                return;
            }

            TxtMapSaveFolderPath.Text = gameInstall;
            userSettings.SetValueForKey(UserSettingsKey.MapSaveLocationPath, gameInstall);
            userSettings.SetValueForKey(UserSettingsKey.MapSaveLocationIndex, "1");
            ToggleMapPathVisibility();
            UpdateSettings();
            return;
        }

        TxtMapSaveFolderPath.Text = CoreProgram.DocumentsMapFolder;
        userSettings.SetValueForKey(UserSettingsKey.MapSaveLocationIndex, "0");
        ToggleMapPathVisibility();
        UpdateSettings();
    }

    async void OnMapSavePathPointerReleasedAsync(object? sender, PointerReleasedEventArgs e) {
        if (!string.Equals(ComboMapSaveFolder.SelectedItem as string, "Game Install", StringComparison.Ordinal)) {
            return;
        }

        var gameInstall = await PickGameFolderAsync();
        if (string.IsNullOrWhiteSpace(gameInstall)) {
            return;
        }

        TxtMapSaveFolderPath.Text = gameInstall;
        userSettings.SetValueForKey(UserSettingsKey.MapSaveLocationPath, gameInstall);
        UpdateSettings();
    }

    void CommitDefaultMapper() {
        userSettings.SetValueForKey(UserSettingsKey.DefaultMapper, TxtDefaultMapper.Text ?? string.Empty);
        UpdateSettings();
    }

    void CommitDefaultNoteSpeed() {
        CommitValidatedDouble(
            TxtDefaultNoteSpeed,
            UserSettingsKey.DefaultNoteSpeed,
            "The note speed must be numerical.",
            onValid: null
        );
    }

    void CommitDefaultGridSpacing() {
        CommitValidatedDouble(
            TxtDefaultGridSpacing,
            UserSettingsKey.DefaultGridSpacing,
            "The grid spacing must be numerical.",
            onValid: null
        );
    }

    void CommitAudioLatency() {
        CommitValidatedDouble(
            TxtAudioLatency,
            UserSettingsKey.EditorAudioLatency,
            "The latency must be numerical.",
            onValid: caller.PauseSong
        );
    }

    void CommitSpectrogramFrequency() {
        var previousValue = userSettings.GetValueForKey(UserSettingsKey.SpectrogramFrequency) ?? DefaultUserSettings.SpectrogramFrequency.ToString(CultureInfo.InvariantCulture);
        if (int.TryParse(TxtSpectrogramFrequency.Text, out var frequency) &&
            frequency >= Editor.Spectrogram.MinFreq &&
            frequency <= Editor.Spectrogram.MaxFreq) {
            userSettings.SetValueForKey(UserSettingsKey.SpectrogramFrequency, frequency.ToString(CultureInfo.InvariantCulture));
            TxtSpectrogramFrequency.Text = frequency.ToString(CultureInfo.InvariantCulture);
            UpdateSettings();
            return;
        }

        appSession.ShowError(
            this,
            "Error",
            $"The frequency must be an integer between {Editor.Spectrogram.MinFreq} and {Editor.Spectrogram.MaxFreq}.",
            () => TxtSpectrogramFrequency.Text = previousValue
        );
    }

    void CommitValidatedDouble(TextBox textBox, string key, string errorMessage, Action? onValid) {
        var previousValue = userSettings.GetValueForKey(key) ?? string.Empty;
        if (double.TryParse(textBox.Text, out var parsedValue)) {
            userSettings.SetValueForKey(key, parsedValue);
            textBox.Text = parsedValue.ToString(CultureInfo.CurrentCulture);
            UpdateSettings();
            onValid?.Invoke();
            return;
        }

        appSession.ShowError(this, "Error", errorMessage, () => textBox.Text = previousValue);
    }

    void ToggleSpectrogramOptionsVisibility() {
        var isVisible = CheckShowSpectrogram.IsChecked ?? false;
        SpectrogramOptionsLabel.IsVisible = isVisible;
        SpectrogramOptions.IsVisible = isVisible;
    }

    void ToggleMapPathVisibility() {
        var isVisible = string.Equals(ComboMapSaveFolder.SelectedItem as string, "Game Install", StringComparison.Ordinal);
        TxtMapSaveFolderPath.IsVisible = isVisible;
    }

    async System.Threading.Tasks.Task<string?> PickGameFolderAsync() {
        var previousGamePath = userSettings.GetValueForKey(UserSettingsKey.MapSaveLocationPath);
        var gameInstall = await appSession.PickGameInstallFolderAsync(this, Directory.Exists(previousGamePath) ? previousGamePath : null);
        if (string.IsNullOrWhiteSpace(gameInstall)) {
            return null;
        }

        Directory.CreateDirectory(Path.Combine(gameInstall, CoreProgram.GameInstallRelativeMapFolder));
        return gameInstall;
    }

    string GetMapSaveLocationLabel() {
        return userSettings.GetValueForKey(UserSettingsKey.MapSaveLocationIndex) == "1"
            ? "Game Install"
            : "Documents";
    }

    void UpdateSettings() {
        userSettings.Write();
        caller.LoadSettingsFile(true);
    }

    sealed record LabeledValue(string Value, string Label) {
        public override string ToString() {
            return Label;
        }
    }

    sealed record PlaybackDeviceChoice(string? Id, string Name) {
        public override string ToString() {
            return Name;
        }
    }
}
