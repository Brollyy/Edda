using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Edda.Classes.MapEditorNS.Stats;
using Edda.Const;
using System;
using System.Globalization;
using System.IO;

namespace Edda.Avalonia.Windows;

internal sealed class DifficultyPredictorWindow : Window {
    readonly MainWindow mainWindow;
    readonly UserSettingsManager userSettings;

    StackPanel resultsPanel = null!;
    TextBlock warningPanel = null!;
    RadioButton pkBeamRadio = null!;
    RadioButton nytildeRadio = null!;
    RadioButton melchiorRadio = null!;
    RadioButton timelineRadio = null!;
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
        Width = 360;
        Height = 450;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(Color.Parse("#D4D0C8"));
        AutomationHelper.SetAutomationId(this, "DifficultyPredictorWindow");
        Content = BuildRoot();
        LoadSettings();
        initialized = true;
    }

    Control BuildRoot() {
        var root = new DockPanel();

        var header = new Border {
            Padding = new Thickness(10),
            BorderBrush = new SolidColorBrush(Color.Parse("#5B6475")),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Background = CreatePanelGradient()
        };
        DockPanel.SetDock(header, Dock.Top);
        header.Child = BuildAlgorithmPanel();
        root.Children.Add(header);

        var footer = new Border {
            Padding = new Thickness(0),
            BorderBrush = new SolidColorBrush(Color.Parse("#5B6475")),
            BorderThickness = new Thickness(0, 1, 0, 0),
            Background = CreatePanelGradient()
        };
        DockPanel.SetDock(footer, Dock.Bottom);
        var footerPanel = new StackPanel();
        var predictButton = AutomationHelper.WithAutomationId(new Button {
            Name = "btnPredict",
            Content = "Predict",
            HorizontalAlignment = HorizontalAlignment.Right,
            Width = 70,
            Margin = new Thickness(0, 10, 10, 10)
        }, "btnPredict");
        predictButton.Click += (_, _) => Predict();
        footerPanel.Children.Add(predictButton);
        footer.Child = footerPanel;
        root.Children.Add(footer);

        var body = new StackPanel {
            Margin = new Thickness(0),
            Spacing = 0,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        var settingsGrid = new Grid {
            ColumnDefinitions = new ColumnDefinitions("11.684,138.316,25"),
            RowDefinitions = new RowDefinitions("*,*,*,*,*,*,*,*,*"),
            Width = 200,
            Margin = new Thickness(0, 15, 0, 15)
        };
        settingsGrid.Children.Add(CreateSettingsLabel(2, 0, 2, 2, new Thickness(0, 0, 0, 26), "Show precise values"));
        settingsGrid.Children.Add(CreateSettingsCheckboxCell("CheckShowPreciseValues", 2, 2, 2, new Thickness(0, 0, 0, 26), (_, _) => PersistSettings()));
        settingsGrid.Children.Add(CreateSettingsLabel(3, 0, 6, 2, default, "Show in map stats"));
        settingsGrid.Children.Add(CreateSettingsCheckboxCell("CheckShowInMapStats", 3, 2, 6, default, (_, _) => PersistSettings()));
        body.Children.Add(settingsGrid);

        resultsPanel = AutomationHelper.WithAutomationId(new StackPanel {
            Name = "PanelPredictionResults",
            Spacing = 8,
            Width = 200,
            IsVisible = false
        }, "PanelPredictionResults");
        resultsPanel.Children.Add(new TextBlock {
            Text = "Predicted Difficulty Ranks:",
            FontSize = 11,
            Margin = new Thickness(0, 0, 0, 5)
        });

        var difficultyButtons = new StackPanel {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            Spacing = 0
        };
        (difficultyButton0, difficultyLabel1) = CreateDifficultyButton("btnDifficulty0", "lblDifficultyRank1", "difficulty1.png");
        (difficultyButton1, difficultyLabel2) = CreateDifficultyButton("btnDifficulty1", "lblDifficultyRank2", "difficulty2.png");
        (difficultyButton2, difficultyLabel3) = CreateDifficultyButton("btnDifficulty2", "lblDifficultyRank3", "difficulty3.png");
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
        body.Children.Add(new Grid {
            Width = 200,
            MinHeight = 76,
            Children = { resultsPanel }
        });

        root.Children.Add(body);
        return root;
    }

    Control BuildAlgorithmPanel() {
        var panel = new StackPanel {
            Spacing = 8
        };
        panel.Children.Add(new TextBlock {
            Text = "Select the algorithm used for prediction:",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 5)
        });

        pkBeamRadio = CreateAlgorithmRadioButton(
            "PKBeamAlgoRadioButton",
            "PKBeam's ML model",
            DifficultyPrediction.SupportedAlgorithms.PKBeam,
            "Machine learning model developed by PKBeam and trained on selected custom maps. Overall best option at the moment, but it has issues estimating difficulty above 9. For best results, use with completed maps.");
        nytildeRadio = CreateAlgorithmRadioButton(
            "NytildeAlgoRadioButton",
            "Nytilde's ML model (beta)",
            DifficultyPrediction.SupportedAlgorithms.Nytilde,
            "Machine learning model developed by Nytilde and trained on OST maps up to Jonathan Young RAID. It has known issues with estimating difficulty for very hard maps and non-standard mapping patterns. Best used to estimate difficulty of maps in 3-7 range.");
        melchiorRadio = CreateAlgorithmRadioButton(
            "MelchiorAlgoRadioButton",
            "Melchior's scoring",
            DifficultyPrediction.SupportedAlgorithms.Melchior,
            "A simple scoring algorithm suggested by Melchior. It takes into account horizontal and vertical distances that each hand needs to move to hit the runes to estimate the map difficulty. More accurate for harder maps and best used with fully completed maps.");
        timelineRadio = CreateAlgorithmRadioButton(
            "TimelineAlgoRadioButton",
            "Brollyy's timeline model",
            DifficultyPrediction.SupportedAlgorithms.Timeline,
            "Timeline model developed by Brollyy. It was trained by comparing map patterns with player performance and mapper ratings, then fitting a score that follows how difficulty builds across the whole song. Best used with completed maps.");
        panel.Children.Add(pkBeamRadio);
        panel.Children.Add(nytildeRadio);
        panel.Children.Add(melchiorRadio);
        panel.Children.Add(timelineRadio);
        return panel;
    }

    RadioButton CreateAlgorithmRadioButton(string automationId, string text, string algorithm, string tooltipText) {
        var content = new StackPanel {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            VerticalAlignment = VerticalAlignment.Center
        };
        content.Children.Add(new TextBlock {
            Text = text,
            VerticalAlignment = VerticalAlignment.Center
        });
        var infoImage = new Image {
            Source = GetResourceBitmap("info_icon.png"),
            Width = 12,
            Height = 12,
            Stretch = Stretch.Uniform,
            VerticalAlignment = VerticalAlignment.Center
        };
        ToolTip.SetTip(infoImage, new TextBlock {
            Text = tooltipText,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 400
        });
        content.Children.Add(infoImage);

        var radio = AutomationHelper.WithAutomationId(new RadioButton {
            Name = automationId,
            Content = content,
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

    Control CreateSettingsLabel(int row, int column, int rowSpan, int columnSpan, Thickness margin, string text) {
        var host = new Border {
            Margin = margin,
            Padding = new Thickness(5)
        };
        var textBlock = new TextBlock {
            Text = text,
            VerticalAlignment = VerticalAlignment.Center
        };
        host.Child = textBlock;
        Grid.SetRow(host, row);
        Grid.SetColumn(host, column);
        Grid.SetRowSpan(host, rowSpan);
        Grid.SetColumnSpan(host, columnSpan);
        return host;
    }

    Control CreateSettingsCheckboxCell(string automationId, int row, int column, int rowSpan, Thickness margin, EventHandler<RoutedEventArgs> onClick) {
        var hostBorder = new Border {
            Margin = margin,
            Padding = new Thickness(5)
        };
        var host = new StackPanel {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        var checkbox = AutomationHelper.WithAutomationId(new CheckBox {
            Name = automationId,
            VerticalAlignment = VerticalAlignment.Center
        }, automationId);
        checkbox.Click += onClick;
        if (automationId == "CheckShowPreciseValues") {
            showPreciseCheckbox = checkbox;
        } else {
            showInMapStatsCheckbox = checkbox;
        }
        host.Children.Add(checkbox);
        hostBorder.Child = host;
        Grid.SetRow(hostBorder, row);
        Grid.SetColumn(hostBorder, column);
        Grid.SetRowSpan(hostBorder, rowSpan);
        return hostBorder;
    }

    static (Button button, TextBlock label) CreateDifficultyButton(string buttonId, string labelId, string imageFileName) {
        var label = AutomationHelper.WithAutomationId(new TextBlock {
            Name = labelId,
            FontWeight = FontWeight.Bold,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 2, 1)
        }, labelId);
        var content = new Grid {
            Width = 50
        };
        content.Children.Add(new Image {
            Source = GetResourceBitmap(imageFileName),
            Stretch = Stretch.Uniform
        });
        content.Children.Add(label);
        var button = AutomationHelper.WithAutomationId(new Button {
            Name = buttonId,
            Width = 55,
            Height = 40,
            Padding = new Thickness(0),
            Content = content,
            IsEnabled = false
        }, buttonId);
        return (button, label);
    }

    static LinearGradientBrush CreatePanelGradient() {
        return new LinearGradientBrush {
            StartPoint = new RelativePoint(0.5, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0.5, 1, RelativeUnit.Relative),
            GradientStops = new GradientStops {
                new GradientStop(Color.Parse("#F2F4F7"), 0),
                new GradientStop(Color.Parse("#D4D0C8"), 1)
            }
        };
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
            case DifficultyPrediction.SupportedAlgorithms.Timeline:
                timelineRadio.IsChecked = true;
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
        mainWindow.LoadSettingsFile(reloadWaveforms: true);
        mainWindow.UpdateDifficultyPrediction();
    }

    void UpdatePreciseAvailability() {
        showPreciseCheckbox.IsEnabled = mainWindow.ResolveDifficultyPredictor().GetSupportedFeatures().HasFlag(IDifficultyPredictor.Features.RealTime);
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

    static Bitmap? GetResourceBitmap(string resourceFileName) {
        var resourcePath = Path.Combine(AppContext.BaseDirectory, "Resources", resourceFileName);
        if (!File.Exists(resourcePath)) {
            return null;
        }

        using var stream = File.OpenRead(resourcePath);
        return new Bitmap(stream);
    }
}
