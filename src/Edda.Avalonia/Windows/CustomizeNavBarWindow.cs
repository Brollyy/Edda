using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Edda.Const;
using System.Globalization;

namespace Edda.Avalonia.Windows;

internal sealed class CustomizeNavBarWindow : Window {
    readonly MainWindow mainWindow;
    readonly UserSettingsManager userSettings;

    CheckBox checkWaveform = null!;
    CheckBox checkBookmark = null!;
    CheckBox checkBpmChange = null!;
    CheckBox checkNote = null!;
    TextBox colorWaveform = null!;
    TextBox colorBookmark = null!;
    TextBox colorBpmChange = null!;
    TextBox colorNote = null!;
    Slider sliderBookmarkShadowOpacity = null!;
    Slider sliderBpmChangeShadowOpacity = null!;
    bool initialized;

    public CustomizeNavBarWindow(MainWindow mainWindow) {
        this.mainWindow = mainWindow;
        userSettings = mainWindow.UserSettings;
        Title = "Customize Nav Bar";
        Width = 500;
        Height = 280;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        AutomationHelper.SetAutomationId(this, "CustomizeNavBarWindow");
        Content = BuildRoot();
        LoadSettings();
        initialized = true;
        UpdateControlEnablement();
    }

    Control BuildRoot() {
        var root = new DockPanel();

        var footer = new Border {
            Padding = new Thickness(12),
            Background = new LinearGradientBrush {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                GradientStops = new GradientStops {
                    new GradientStop(Color.Parse("#F7F9FD"), 0),
                    new GradientStop(Color.Parse("#DCE5F5"), 1)
                }
            }
        };
        DockPanel.SetDock(footer, Dock.Bottom);
        var saveButton = AutomationHelper.WithAutomationId(new Button {
            Name = "btnSave",
            Content = "OK",
            HorizontalAlignment = HorizontalAlignment.Right,
            MinWidth = 84
        }, "btnSave");
        saveButton.Click += (_, _) => Close();
        footer.Child = saveButton;
        root.Children.Add(footer);

        var body = new Grid {
            Margin = new Thickness(14),
            ColumnDefinitions = new ColumnDefinitions("110,60,95,120,70"),
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto,Auto"),
            RowSpacing = 10,
            ColumnSpacing = 8
        };

        AddHeader(body, 0, 1, "Show");
        AddHeader(body, 0, 2, "Color");
        AddHeader(body, 0, 3, "Shadow");

        BuildWaveformRow(body, 1);
        BuildBookmarkRow(body, 2);
        BuildBpmRow(body, 3);
        BuildNoteRow(body, 4);

        root.Children.Add(body);
        return root;
    }

    void AddHeader(Grid body, int row, int column, string text) {
        var label = new TextBlock {
            Text = text,
            FontWeight = FontWeight.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        Grid.SetRow(label, row);
        Grid.SetColumn(label, column);
        body.Children.Add(label);
    }

    void BuildWaveformRow(Grid body, int row) {
        AddRowLabel(body, row, "Waveform");
        checkWaveform = CreateCheckBox("CheckWaveform", row, PersistWaveformSettings);
        colorWaveform = CreateColorTextBox("ColorWaveform", row, 2);
        body.Children.Add(checkWaveform);
        body.Children.Add(colorWaveform);
        var reset = CreateResetButton("ButtonResetWaveform", row, () => {
            colorWaveform.Text = Editor.Waveform.ColourWPF;
            PersistWaveformSettings();
        });
        body.Children.Add(reset);
    }

    void BuildBookmarkRow(Grid body, int row) {
        AddRowLabel(body, row, "Bookmarks");
        checkBookmark = CreateCheckBox("CheckBookmark", row, PersistBookmarkSettings);
        colorBookmark = CreateColorTextBox("ColorBookmark", row, 2);
        sliderBookmarkShadowOpacity = CreateShadowSlider("SliderBookmarkShadowOpacity", row);
        body.Children.Add(checkBookmark);
        body.Children.Add(colorBookmark);
        body.Children.Add(sliderBookmarkShadowOpacity);
        var reset = CreateResetButton("ButtonResetBookmark", row, () => {
            colorBookmark.Text = Editor.NavBookmark.Colour;
            sliderBookmarkShadowOpacity.Value = Editor.NavBookmark.ShadowOpacity;
            PersistBookmarkSettings();
        });
        body.Children.Add(reset);
    }

    void BuildBpmRow(Grid body, int row) {
        AddRowLabel(body, row, "Timing Changes");
        checkBpmChange = CreateCheckBox("CheckBPMChange", row, PersistBpmChangeSettings);
        colorBpmChange = CreateColorTextBox("ColorBPMChange", row, 2);
        sliderBpmChangeShadowOpacity = CreateShadowSlider("SliderBPMChangeShadowOpacity", row);
        body.Children.Add(checkBpmChange);
        body.Children.Add(colorBpmChange);
        body.Children.Add(sliderBpmChangeShadowOpacity);
        var reset = CreateResetButton("ButtonResetBPMChange", row, () => {
            colorBpmChange.Text = Editor.NavBPMChange.Colour;
            sliderBpmChangeShadowOpacity.Value = Editor.NavBPMChange.ShadowOpacity;
            PersistBpmChangeSettings();
        });
        body.Children.Add(reset);
    }

    void BuildNoteRow(Grid body, int row) {
        AddRowLabel(body, row, "Notes");
        checkNote = CreateCheckBox("CheckNote", row, PersistNoteSettings);
        colorNote = CreateColorTextBox("ColorNote", row, 2);
        body.Children.Add(checkNote);
        body.Children.Add(colorNote);
        var reset = CreateResetButton("ButtonResetNote", row, () => {
            colorNote.Text = Editor.NavNote.Colour;
            PersistNoteSettings();
        });
        body.Children.Add(reset);
    }

    void AddRowLabel(Grid body, int row, string text) {
        var label = new TextBlock {
            Text = text,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        Grid.SetRow(label, row);
        body.Children.Add(label);
    }

    CheckBox CreateCheckBox(string automationId, int row, Action onChanged) {
        var checkBox = AutomationHelper.WithAutomationId(new CheckBox {
            Name = automationId,
            HorizontalAlignment = HorizontalAlignment.Center
        }, automationId);
        checkBox.Click += (_, _) => {
            if (!initialized) {
                return;
            }

            onChanged();
        };
        Grid.SetRow(checkBox, row);
        Grid.SetColumn(checkBox, 1);
        return checkBox;
    }

    TextBox CreateColorTextBox(string automationId, int row, int column) {
        var textBox = AutomationHelper.WithAutomationId(new TextBox {
            Name = automationId,
            Width = 90
        }, automationId);
        Grid.SetRow(textBox, row);
        Grid.SetColumn(textBox, column);
        return textBox;
    }

    Slider CreateShadowSlider(string automationId, int row) {
        var slider = AutomationHelper.WithAutomationId(new Slider {
            Name = automationId,
            Minimum = 0,
            Maximum = 1,
            Width = 100
        }, automationId);
        slider.PropertyChanged += (_, args) => {
            if (!initialized || args.Property != RangeBase.ValueProperty) {
                return;
            }

            if (automationId == "SliderBookmarkShadowOpacity") {
                PersistBookmarkSettings();
            } else {
                PersistBpmChangeSettings();
            }
        };
        Grid.SetRow(slider, row);
        Grid.SetColumn(slider, 3);
        return slider;
    }

    Button CreateResetButton(string automationId, int row, Action onClick) {
        var button = AutomationHelper.WithAutomationId(new Button {
            Name = automationId,
            Content = "Reset",
            MinWidth = 60
        }, automationId);
        button.Click += (_, _) => onClick();
        Grid.SetRow(button, row);
        Grid.SetColumn(button, 4);
        return button;
    }

    void LoadSettings() {
        checkWaveform.IsChecked = GetSettingBool(UserSettingsKey.EnableNavWaveform, DefaultUserSettings.EnableNavWaveform);
        checkBookmark.IsChecked = GetSettingBool(UserSettingsKey.EnableNavBookmarks, DefaultUserSettings.EnableNavBookmarks);
        checkBpmChange.IsChecked = GetSettingBool(UserSettingsKey.EnableNavBPMChanges, DefaultUserSettings.EnableNavBPMChanges);
        checkNote.IsChecked = GetSettingBool(UserSettingsKey.EnableNavNotes, DefaultUserSettings.EnableNavNotes);

        colorWaveform.Text = userSettings.GetValueForKey(UserSettingsKey.NavWaveformColor) ?? Editor.Waveform.ColourWPF;
        colorBookmark.Text = userSettings.GetValueForKey(UserSettingsKey.NavBookmarkColor) ?? Editor.NavBookmark.Colour;
        colorBpmChange.Text = userSettings.GetValueForKey(UserSettingsKey.NavBPMChangeColor) ?? Editor.NavBPMChange.Colour;
        colorNote.Text = userSettings.GetValueForKey(UserSettingsKey.NavNoteColor) ?? Editor.NavNote.Colour;
        sliderBookmarkShadowOpacity.Value = GetSettingDouble(UserSettingsKey.NavBookmarkShadowOpacity, Editor.NavBookmark.ShadowOpacity);
        sliderBpmChangeShadowOpacity.Value = GetSettingDouble(UserSettingsKey.NavBPMChangeShadowOpacity, Editor.NavBPMChange.ShadowOpacity);
    }

    void PersistWaveformSettings() {
        userSettings.SetValueForKey(UserSettingsKey.EnableNavWaveform, checkWaveform.IsChecked ?? false);
        userSettings.SetValueForKey(UserSettingsKey.NavWaveformColor, colorWaveform.Text ?? Editor.Waveform.ColourWPF);
        CommitAndRefresh();
    }

    void PersistBookmarkSettings() {
        userSettings.SetValueForKey(UserSettingsKey.EnableNavBookmarks, checkBookmark.IsChecked ?? false);
        userSettings.SetValueForKey(UserSettingsKey.NavBookmarkColor, colorBookmark.Text ?? Editor.NavBookmark.Colour);
        userSettings.SetValueForKey(UserSettingsKey.NavBookmarkShadowOpacity, sliderBookmarkShadowOpacity.Value.ToString("0.##", CultureInfo.InvariantCulture));
        CommitAndRefresh();
    }

    void PersistBpmChangeSettings() {
        userSettings.SetValueForKey(UserSettingsKey.EnableNavBPMChanges, checkBpmChange.IsChecked ?? false);
        userSettings.SetValueForKey(UserSettingsKey.NavBPMChangeColor, colorBpmChange.Text ?? Editor.NavBPMChange.Colour);
        userSettings.SetValueForKey(UserSettingsKey.NavBPMChangeShadowOpacity, sliderBpmChangeShadowOpacity.Value.ToString("0.##", CultureInfo.InvariantCulture));
        CommitAndRefresh();
    }

    void PersistNoteSettings() {
        userSettings.SetValueForKey(UserSettingsKey.EnableNavNotes, checkNote.IsChecked ?? false);
        userSettings.SetValueForKey(UserSettingsKey.NavNoteColor, colorNote.Text ?? Editor.NavNote.Colour);
        CommitAndRefresh();
    }

    void CommitAndRefresh() {
        userSettings.Write();
        UpdateControlEnablement();
        mainWindow.LoadSettingsFile();
        mainWindow.RefreshNavigationLayersFromSettings();
    }

    void UpdateControlEnablement() {
        colorWaveform.IsEnabled = checkWaveform.IsChecked ?? false;
        colorBookmark.IsEnabled = checkBookmark.IsChecked ?? false;
        sliderBookmarkShadowOpacity.IsEnabled = checkBookmark.IsChecked ?? false;
        colorBpmChange.IsEnabled = checkBpmChange.IsChecked ?? false;
        sliderBpmChangeShadowOpacity.IsEnabled = checkBpmChange.IsChecked ?? false;
        colorNote.IsEnabled = checkNote.IsChecked ?? false;
    }

    bool GetSettingBool(string key, bool defaultValue) {
        return userSettings.GetValueForKey(key) == null ? defaultValue : userSettings.GetBoolForKey(key);
    }

    double GetSettingDouble(string key, double defaultValue) {
        return double.TryParse(userSettings.GetValueForKey(key), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : defaultValue;
    }
}
