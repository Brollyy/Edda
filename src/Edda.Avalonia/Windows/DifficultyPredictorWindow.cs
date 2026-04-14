using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Edda.Classes.MapEditorNS.Stats;
using Edda.Const;
using System;
using System.Globalization;

namespace Edda.Avalonia.Windows;

internal sealed class DifficultyPredictorWindow : Window {
    readonly MainWindow mainWindow;
    readonly UserSettingsManager userSettings;

    StackPanel resultsPanel = null!;
    TextBlock warningPanel = null!;
    RadioButton pkBeamRadio = null!;
    RadioButton nytildeRadio = null!;
    RadioButton melchiorRadio = null!;
    CheckBox showPreciseCheckbox = null!;
    CheckBox showInMapStatsCheckbox = null!;
    Button difficultyButton0 = null!;
    Button difficultyButton1 = null!;
    Button difficultyButton2 = null!;
    TextBlock difficultyLabel1 = null!;
    TextBlock difficultyLabel2 = null!;
    TextBlock difficultyLabel3 = null!;
    bool initialized;

    public DifficultyPredictorWindow(MainWindow mainWindow) {
        this.mainWindow = mainWindow;
        userSettings = mainWindow.UserSettings;
        Title = "Difficulty Predictor";
        Width = 300;
        Height = 360;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        AutomationHelper.SetAutomationId(this, "DifficultyPredictorWindow");
        Content = BuildRoot();
        LoadSettings();
        initialized = true;
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
        var predictButton = AutomationHelper.WithAutomationId(new Button {
            Name = "btnPredict",
            Content = "Predict",
            HorizontalAlignment = HorizontalAlignment.Right,
            MinWidth = 84
        }, "btnPredict");
        predictButton.Click += (_, _) => Predict();
        footer.Child = predictButton;
        root.Children.Add(footer);

        var body = new StackPanel {
            Margin = new Thickness(16),
            Spacing = 14
        };

        body.Children.Add(new TextBlock {
            Text = "Select the algorithm used for prediction:",
            TextWrapping = TextWrapping.Wrap
        });

        pkBeamRadio = CreateAlgorithmRadioButton("PKBeamAlgoRadioButton", "PKBeam's ML model", DifficultyPrediction.SupportedAlgorithms.PKBeam);
        nytildeRadio = CreateAlgorithmRadioButton("NytildeAlgoRadioButton", "Nytilde's ML model (beta)", DifficultyPrediction.SupportedAlgorithms.Nytilde);
        melchiorRadio = CreateAlgorithmRadioButton("MelchiorAlgoRadioButton", "Melchior's scoring", DifficultyPrediction.SupportedAlgorithms.Melchior);
        body.Children.Add(pkBeamRadio);
        body.Children.Add(nytildeRadio);
        body.Children.Add(melchiorRadio);

        showPreciseCheckbox = CreateSettingsCheckbox("CheckShowPreciseValues", "Show precise values", (_, _) => PersistSettings());
        showInMapStatsCheckbox = CreateSettingsCheckbox("CheckShowInMapStats", "Show in map stats", (_, _) => PersistSettings());
        body.Children.Add(showPreciseCheckbox);
        body.Children.Add(showInMapStatsCheckbox);

        resultsPanel = AutomationHelper.WithAutomationId(new StackPanel {
            Name = "PanelPredictionResults",
            IsVisible = false,
            Spacing = 8
        }, "PanelPredictionResults");
        resultsPanel.Children.Add(new TextBlock {
            Text = "Predicted Difficulty Ranks:"
        });

        var difficultyButtons = new StackPanel {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            Spacing = 10
        };
        (difficultyButton0, difficultyLabel1) = CreateDifficultyButton("btnDifficulty0", "lblDifficultyRank1");
        (difficultyButton1, difficultyLabel2) = CreateDifficultyButton("btnDifficulty1", "lblDifficultyRank2");
        (difficultyButton2, difficultyLabel3) = CreateDifficultyButton("btnDifficulty2", "lblDifficultyRank3");
        difficultyButtons.Children.Add(difficultyButton0);
        difficultyButtons.Children.Add(difficultyButton1);
        difficultyButtons.Children.Add(difficultyButton2);
        resultsPanel.Children.Add(difficultyButtons);

        warningPanel = AutomationHelper.WithAutomationId(new TextBlock {
            Name = "PanelPredictionWarning",
            IsVisible = false,
            Text = "Map parameters outside of normal range, predictions marked in orange couldn't be determined.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brushes.OrangeRed,
            FontSize = 11
        }, "PanelPredictionWarning");
        resultsPanel.Children.Add(warningPanel);
        body.Children.Add(resultsPanel);

        root.Children.Add(body);
        return root;
    }

    RadioButton CreateAlgorithmRadioButton(string automationId, string text, string algorithm) {
        var radio = AutomationHelper.WithAutomationId(new RadioButton {
            Name = automationId,
            Content = text,
            GroupName = "DifficultyAlgorithm"
        }, automationId);
        radio.IsCheckedChanged += (_, _) => {
            if (!initialized || radio.IsChecked != true) {
                return;
            }

            userSettings.SetValueForKey(UserSettingsKey.DifficultyPredictorAlgorithm, algorithm);
            PersistSettings();
        };
        return radio;
    }

    CheckBox CreateSettingsCheckbox(string automationId, string text, EventHandler<RoutedEventArgs> onClick) {
        var checkbox = AutomationHelper.WithAutomationId(new CheckBox {
            Name = automationId,
            Content = text
        }, automationId);
        checkbox.Click += onClick;
        return checkbox;
    }

    static (Button button, TextBlock label) CreateDifficultyButton(string buttonId, string labelId) {
        var label = AutomationHelper.WithAutomationId(new TextBlock {
            Name = labelId,
            FontWeight = FontWeight.Bold,
            FontSize = 16,
            HorizontalAlignment = HorizontalAlignment.Center
        }, labelId);
        var button = AutomationHelper.WithAutomationId(new Button {
            Name = buttonId,
            Width = 70,
            Height = 52,
            Content = label,
            IsEnabled = false
        }, buttonId);
        return (button, label);
    }

    void LoadSettings() {
        showPreciseCheckbox.IsChecked = userSettings.GetBoolForKey(UserSettingsKey.DifficultyPredictorShowPrecise);
        showInMapStatsCheckbox.IsChecked = userSettings.GetBoolForKey(UserSettingsKey.DifficultyPredictorShowInMapStats);
        switch (userSettings.GetValueForKey(UserSettingsKey.DifficultyPredictorAlgorithm)) {
            case DifficultyPrediction.SupportedAlgorithms.Nytilde:
                nytildeRadio.IsChecked = true;
                break;
            case DifficultyPrediction.SupportedAlgorithms.Melchior:
                melchiorRadio.IsChecked = true;
                break;
            default:
                pkBeamRadio.IsChecked = true;
                break;
        }

        UpdatePreciseAvailability();
    }

    void PersistSettings() {
        userSettings.SetValueForKey(UserSettingsKey.DifficultyPredictorShowPrecise, showPreciseCheckbox.IsChecked ?? false);
        userSettings.SetValueForKey(UserSettingsKey.DifficultyPredictorShowInMapStats, showInMapStatsCheckbox.IsChecked ?? false);
        userSettings.Write();
        UpdatePreciseAvailability();
        mainWindow.LoadSettingsFile();
        mainWindow.UpdateDifficultyPrediction();
    }

    void UpdatePreciseAvailability() {
        showPreciseCheckbox.IsEnabled = mainWindow.ResolveDifficultyPredictor().GetSupportedFeatures().HasFlag(IDifficultyPredictor.Features.PreciseFloat);
    }

    void Predict() {
        var mapEditor = mainWindow.MapEditorInstance;
        if (mapEditor == null) {
            return;
        }

        resultsPanel.IsVisible = true;
        warningPanel.IsVisible = false;
        RenderDifficulty(0, difficultyButton0, difficultyLabel1);
        RenderDifficulty(1, difficultyButton1, difficultyLabel2);
        RenderDifficulty(2, difficultyButton2, difficultyLabel3);
    }

    void RenderDifficulty(int difficultyIndex, Button button, TextBlock label) {
        var mapEditor = mainWindow.MapEditorInstance;
        var predictor = mainWindow.ResolveDifficultyPredictor();
        if (mapEditor == null || difficultyIndex >= mapEditor.numDifficulties || mapEditor.GetDifficulty(difficultyIndex) is not { } difficultyMap) {
            button.IsEnabled = false;
            label.Text = string.Empty;
            return;
        }

        button.IsEnabled = true;
        var supportedFeatures = predictor.GetSupportedFeatures();
        var prediction = predictor.PredictDifficulty(difficultyMap.notes, mapEditor.GlobalBPM, mapEditor.SongDuration);
        if (prediction.HasValue && (float.IsNaN(prediction.Value) || float.IsInfinity(prediction.Value))) {
            prediction = supportedFeatures.HasFlag(IDifficultyPredictor.Features.AlwaysPredict) ? 0 : null;
        }
        if (prediction.HasValue) {
            var precise = (showPreciseCheckbox.IsChecked ?? false) && supportedFeatures.HasFlag(IDifficultyPredictor.Features.PreciseFloat);
            var displayValue = Math.Round(prediction.Value, precise ? 2 : 0);
            label.Text = displayValue.ToString(precise ? "#0.00" : "0", CultureInfo.CurrentCulture);
            SetTextForeground(label, DifficultyPrediction.Colour);
            return;
        }

        if (!supportedFeatures.HasFlag(IDifficultyPredictor.Features.AlwaysPredict)) {
            label.Text = "???";
            warningPanel.IsVisible = true;
            SetTextForeground(label, DifficultyPrediction.WarningColour);
            return;
        }

        label.Text = "0";
        SetTextForeground(label, DifficultyPrediction.Colour);
    }

    static void SetTextForeground(TextBlock textBlock, System.Drawing.Color color) {
        textBlock.Foreground = new SolidColorBrush(Color.FromArgb(color.A, color.R, color.G, color.B));
    }
}
