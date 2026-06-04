using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;

namespace Edda.Avalonia.Windows;

internal sealed class BpmCalcWindow : Window {
    readonly Stopwatch stopwatch = new();
    readonly List<long> intervalSamples = [];

    TextBlock lblInputCounter = null!;
    TextBlock lblAvgBPM = null!;
    TextBlock lblUnroundedAvgBPM = null!;
    long prevTime;
    int numInputs;

    public BpmCalcWindow() {
        Title = "BPM Finder";
        Width = 240;
        Height = 260;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        AutomationHelper.SetAutomationId(this, "BpmCalcWindow");
        Content = BuildRoot();
        KeyDown += OnKeyDown;
        Opened += (_, _) => Focus();
        Reset();
    }

    Control BuildRoot() {
        var root = new DockPanel();

        var header = new Border {
            Padding = new Thickness(12),
            Background = new LinearGradientBrush {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                GradientStops = new GradientStops {
                    new GradientStop(Color.Parse("#F7F9FD"), 0),
                    new GradientStop(Color.Parse("#DCE5F5"), 1)
                }
            },
            Child = new TextBlock {
                Text = "Press any key in time with the beat.",
                HorizontalAlignment = HorizontalAlignment.Center
            }
        };
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);

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
        var footerPanel = new StackPanel {
            Spacing = 10
        };
        lblInputCounter = AutomationHelper.WithAutomationId(new TextBlock {
            Name = "lblInputCounter",
            FontWeight = FontWeight.Bold
        }, "lblInputCounter");
        footerPanel.Children.Add(new StackPanel {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            Spacing = 6,
            Children = {
                new TextBlock { Text = "Number of inputs:" },
                lblInputCounter
            }
        });
        var resetButton = AutomationHelper.WithAutomationId(new Button {
            Name = "btnReset",
            Content = "Reset",
            HorizontalAlignment = HorizontalAlignment.Center,
            MinWidth = 70
        }, "btnReset");
        resetButton.Click += (_, _) => Reset();
        footerPanel.Children.Add(resetButton);
        footer.Child = footerPanel;
        root.Children.Add(footer);

        var body = new StackPanel {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            Spacing = 4
        };
        body.Children.Add(new TextBlock {
            Text = "Average",
            FontSize = 14
        });
        var averageRow = new StackPanel {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            VerticalAlignment = VerticalAlignment.Bottom
        };
        lblAvgBPM = AutomationHelper.WithAutomationId(new TextBlock {
            Name = "lblAvgBPM",
            FontSize = 36,
            FontWeight = FontWeight.Bold
        }, "lblAvgBPM");
        averageRow.Children.Add(lblAvgBPM);
        averageRow.Children.Add(new TextBlock {
            Text = "BPM",
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 0, 4)
        });
        body.Children.Add(averageRow);
        lblUnroundedAvgBPM = AutomationHelper.WithAutomationId(new TextBlock {
            Name = "lblUnroundedAvgBPM",
            FontSize = 16
        }, "lblUnroundedAvgBPM");
        body.Children.Add(lblUnroundedAvgBPM);
        root.Children.Add(body);

        return root;
    }

    void Reset() {
        stopwatch.Reset();
        intervalSamples.Clear();
        prevTime = 0;
        numInputs = 0;
        lblInputCounter.Text = "0";
        lblAvgBPM.Text = "0";
        lblUnroundedAvgBPM.Text = "(0.00)";
    }

    void OnKeyDown(object? sender, KeyEventArgs e) {
        if (!stopwatch.IsRunning) {
            stopwatch.Start();
            return;
        }

        var now = stopwatch.ElapsedMilliseconds;
        intervalSamples.Add(now - prevTime);
        numInputs++;
        lblInputCounter.Text = numInputs.ToString(CultureInfo.InvariantCulture);
        prevTime = now;
        Recalculate();
    }

    void Recalculate() {
        if (intervalSamples.Count == 0) {
            return;
        }

        intervalSamples.Sort();
        var avgInterval = intervalSamples.Sum() / (double)intervalSamples.Count;
        var bpm = 60000.0 / avgInterval;
        lblAvgBPM.Text = bpm.ToString("0.", CultureInfo.CurrentCulture);
        lblUnroundedAvgBPM.Text = $"({bpm.ToString("0.00", CultureInfo.CurrentCulture)})";
    }
}
