using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Edda.Avalonia.Services;
using Edda.Const;
using NAudio.Vorbis;
using System;
using System.IO;

namespace Edda.Avalonia.Windows;

internal sealed class SongPreviewWindow : Window {
    readonly MainWindow mainWindow;

    TextBox txtStartTimeMin = null!;
    TextBox txtStartTimeSec = null!;
    TextBox txtEndTimeMin = null!;
    TextBox txtEndTimeSec = null!;
    TextBox txtFadeInDuration = null!;
    TextBox txtFadeOutDuration = null!;
    Button btnGenerate = null!;

    int startMin;
    int startSec;
    int endMin;
    int endSec;
    int fadeInDur;
    int fadeOutDur;
    int songEndMin;
    int songEndSec;

    public SongPreviewWindow(MainWindow mainWindow) {
        this.mainWindow = mainWindow;
        Title = "Song Preview";
        Width = 290;
        Height = 260;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        AutomationHelper.SetAutomationId(this, "SongPreviewWindow");
        SeedInitialValues();
        Content = BuildRoot();
        UpdateTextFields();
    }

    void SeedInitialValues() {
        var songPath = mainWindow.CurrentSongPath;
        if (string.IsNullOrWhiteSpace(songPath) || !File.Exists(songPath)) {
            songEndMin = 0;
            songEndSec = Audio.MaxPreviewLength;
        } else {
            using var songStream = new VorbisWaveReader(songPath);
            songEndMin = (int)songStream.TotalTime.TotalSeconds / 60;
            songEndSec = (int)songStream.TotalTime.TotalSeconds % 60;
        }

        startMin = (int)(mainWindow.CurrentSongPositionMilliseconds / 1000) / 60;
        startSec = (int)(mainWindow.CurrentSongPositionMilliseconds / 1000) % 60;
        endMin = (startMin * 60 + startSec + Audio.MaxPreviewLength) / 60;
        endSec = (startMin * 60 + startSec + Audio.MaxPreviewLength) % 60;
        fadeInDur = Audio.DefaultPreviewFadeIn;
        fadeOutDur = Audio.DefaultPreviewFadeOut;
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
        btnGenerate = AutomationHelper.WithAutomationId(new Button {
            Name = "btnGenerate",
            Content = "Create Preview",
            HorizontalAlignment = HorizontalAlignment.Right,
            MinWidth = 110
        }, "btnGenerate");
        btnGenerate.Click += (_, _) => BeginGenerate();
        footer.Child = btnGenerate;
        root.Children.Add(footer);

        var body = new Grid {
            Margin = new Thickness(14),
            ColumnDefinitions = new ColumnDefinitions("110,*"),
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto"),
            RowSpacing = 10,
            ColumnSpacing = 10
        };

        AddTimeInputRow(body, 0, "Start Time", out txtStartTimeMin, out txtStartTimeSec, "TxtStartTimeMin", "TxtStartTimeSec", CommitStartTime);
        AddTimeInputRow(body, 1, "End Time", out txtEndTimeMin, out txtEndTimeSec, "TxtEndTimeMin", "TxtEndTimeSec", CommitEndTime);
        AddSingleInputRow(body, 2, "Fade In", out txtFadeInDuration, "TxtFadeInDuration", CommitFadeIn, "sec");
        AddSingleInputRow(body, 3, "Fade Out", out txtFadeOutDuration, "TxtFadeOutDuration", CommitFadeOut, "sec");

        root.Children.Add(body);
        return root;
    }

    void AddTimeInputRow(Grid body, int rowIndex, string labelText, out TextBox minuteBox, out TextBox secondBox, string minuteId, string secondId, Action onCommit) {
        var label = new TextBlock {
            Text = labelText,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetRow(label, rowIndex);
        body.Children.Add(label);

        var inputRow = new StackPanel {
            Orientation = Orientation.Horizontal,
            Spacing = 6
        };
        minuteBox = CreateNumericTextBox(minuteId, onCommit);
        secondBox = CreateNumericTextBox(secondId, onCommit);
        inputRow.Children.Add(minuteBox);
        inputRow.Children.Add(new TextBlock { Text = "min", VerticalAlignment = VerticalAlignment.Center });
        inputRow.Children.Add(secondBox);
        inputRow.Children.Add(new TextBlock { Text = "sec", VerticalAlignment = VerticalAlignment.Center });
        Grid.SetColumn(inputRow, 1);
        Grid.SetRow(inputRow, rowIndex);
        body.Children.Add(inputRow);
    }

    void AddSingleInputRow(Grid body, int rowIndex, string labelText, out TextBox textBox, string automationId, Action onCommit, string unitText) {
        var label = new TextBlock {
            Text = labelText,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetRow(label, rowIndex);
        body.Children.Add(label);

        var inputRow = new StackPanel {
            Orientation = Orientation.Horizontal,
            Spacing = 6
        };
        textBox = CreateNumericTextBox(automationId, onCommit, width: 72);
        inputRow.Children.Add(textBox);
        inputRow.Children.Add(new TextBlock { Text = unitText, VerticalAlignment = VerticalAlignment.Center });
        Grid.SetColumn(inputRow, 1);
        Grid.SetRow(inputRow, rowIndex);
        body.Children.Add(inputRow);
    }

    TextBox CreateNumericTextBox(string automationId, Action onCommit, double width = 28) {
        var textBox = AutomationHelper.WithAutomationId(new TextBox {
            Name = automationId,
            Width = width
        }, automationId);
        textBox.GotFocus += (_, _) => textBox.SelectAll();
        textBox.LostFocus += (_, _) => onCommit();
        return textBox;
    }

    void BeginGenerate() {
        if (TimeRangeDurationCheck()) {
            GeneratePreview();
            return;
        }

        mainWindow.Session.ShowYesNoConfirmation(
            this,
            "Warning",
            $"The preview duration should be less than {Audio.MaxPreviewLength} seconds.\nContinue anyway?",
            result => {
                if (result == AppDialogResult.Yes) {
                    GeneratePreview();
                }
            }
        );
    }

    void GeneratePreview() {
        var mapFolder = mainWindow.CurrentMapFolder;
        var songPath = mainWindow.CurrentSongPath;
        if (string.IsNullOrWhiteSpace(mapFolder) || string.IsNullOrWhiteSpace(songPath)) {
            return;
        }

        var savePath = Path.Combine(mapFolder, BeatmapDefaults.PreviewFilename);
        btnGenerate.IsEnabled = false;
        int exitCode;
        try {
            exitCode = Helper.FFmpeg(
                mapFolder,
                $"-i \"{songPath}\" -y -ss 00:{startMin:D2}:{startSec:D2} -to 00:{endMin:D2}:{endSec:D2} -vn -af afade=t=out:st={TotalSec(endMin, endSec) - fadeOutDur}:d={fadeOutDur},afade=t=in:st={TotalSec(startMin, startSec)}:d={fadeInDur} \"{savePath}\""
            );
        } finally {
            btnGenerate.IsEnabled = true;
        }

        if (exitCode == 0) {
            mainWindow.Session.ShowError(this, "Success", "Song preview created successfully.", onDismissed: null);
        } else {
            mainWindow.Session.ShowError(this, "Error", "There was an issue creating song preview.", onDismissed: null);
        }
    }

    void CommitFadeIn() {
        if (TryParseNonNegativeInt(txtFadeInDuration.Text, out var value)) {
            fadeInDur = Math.Min(value, TotalSec(songEndMin, songEndSec));
            UpdateTextFields();
            return;
        }

        ShowDurationValueError();
        UpdateTextFields();
    }

    void CommitFadeOut() {
        if (TryParseNonNegativeInt(txtFadeOutDuration.Text, out var value)) {
            fadeOutDur = Math.Min(value, TotalSec(songEndMin, songEndSec));
            UpdateTextFields();
            return;
        }

        ShowDurationValueError();
        UpdateTextFields();
    }

    void CommitStartTime() {
        if (TryParseTimePart(txtStartTimeMin.Text, out var minute) && TryParseTimePart(txtStartTimeSec.Text, out var second)) {
            startMin = minute;
            startSec = second;
            if (!TimeRangeCheck()) {
                endMin = startMin;
                endSec = startSec;
            }
            UpdateTextFields();
            return;
        }

        ShowRangeError();
        UpdateTextFields();
    }

    void CommitEndTime() {
        if (TryParseTimePart(txtEndTimeMin.Text, out var minute) && TryParseTimePart(txtEndTimeSec.Text, out var second)) {
            endMin = minute;
            endSec = second;
            if (!TimeRangeCheck()) {
                startMin = endMin;
                startSec = endSec;
            }
            UpdateTextFields();
            return;
        }

        ShowRangeError();
        UpdateTextFields();
    }

    void ShowRangeError() {
        mainWindow.Session.ShowError(this, "Error", "The input must be an integer from 0 to 59.", onDismissed: null);
    }

    void ShowDurationValueError() {
        mainWindow.Session.ShowError(this, "Error", "The duration must be a positive integer", onDismissed: null);
    }

    void UpdateTextFields() {
        txtStartTimeMin.Text = startMin.ToString();
        txtStartTimeSec.Text = startSec.ToString();
        txtEndTimeMin.Text = endMin.ToString();
        txtEndTimeSec.Text = endSec.ToString();
        txtFadeInDuration.Text = fadeInDur.ToString();
        txtFadeOutDuration.Text = fadeOutDur.ToString();
    }

    static bool TryParseNonNegativeInt(string? text, out int value) {
        return int.TryParse(text, out value) && value >= 0;
    }

    static bool TryParseTimePart(string? text, out int value) {
        return int.TryParse(text, out value) && value >= 0 && value <= 59;
    }

    static int TotalSec(int min, int sec) => 60 * min + sec;

    bool TimeRangeCheck() => TotalSec(startMin, startSec) <= TotalSec(endMin, endSec);

    bool TimeRangeDurationCheck() => TotalSec(endMin, endSec) - TotalSec(startMin, startSec) <= Audio.MaxPreviewLength;
}
