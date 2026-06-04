using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Edda.Classes.MapEditorNS;
using Edda.Const;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Edda.Avalonia.Windows;

internal sealed class ChangeBpmWindow : Window {
    readonly MainWindow mainWindow;
    readonly List<BPMChange> bpmChanges = [];

    TextBlock lblGlobalBPM = null!;
    StackPanel gridRowsHost = null!;
    ContentControl dataGridRoot = null!;
    int selectedRowIndex = -1;
    bool isRefreshingRows;
    bool isPersistingChanges;

    public ChangeBpmWindow(MainWindow mainWindow) {
        this.mainWindow = mainWindow;
        Title = "Timing Settings";
        Width = 300;
        Height = 370;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(Color.Parse("#D4D0C8"));
        AutomationHelper.SetAutomationId(this, "ChangeBpmWindow");
        Content = BuildRoot();
        Closing += (_, _) => ApplyLocalChanges(forceSaveToMap: true);
        LoadFromMap();
    }

    Control BuildRoot() {
        var root = new DockPanel();

        var header = new Border {
            BorderBrush = new SolidColorBrush(Color.Parse("#5B6475")),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Background = CreatePanelGradient()
        };
        DockPanel.SetDock(header, Dock.Top);
        var headerRow = new StackPanel {
            Orientation = Orientation.Horizontal
        };
        headerRow.Children.Add(new TextBlock {
            Text = "Global BPM:",
            Margin = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(10, 10, 0, 10),
            FontSize = 16,
            FontWeight = FontWeight.Bold,
            FontFamily = new FontFamily("Bahnschrift")
        });
        lblGlobalBPM = AutomationHelper.WithAutomationId(new TextBlock {
            Name = "lblGlobalBPM",
            FontSize = 14,
            Padding = new Thickness(5, 10, 10, 10),
            VerticalAlignment = VerticalAlignment.Center
        }, "lblGlobalBPM");
        headerRow.Children.Add(lblGlobalBPM);
        header.Child = headerRow;
        root.Children.Add(header);

        var footer = new Border {
            BorderBrush = new SolidColorBrush(Color.Parse("#5B6475")),
            BorderThickness = new Thickness(0, 1, 0, 0),
            Background = CreatePanelGradient()
        };
        DockPanel.SetDock(footer, Dock.Bottom);
        var footerRow = new Grid {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            Margin = new Thickness(15, 10, 10, 10)
        };
        var addButton = AutomationHelper.WithAutomationId(new Button {
            Name = "dataBPMChange_Add",
            Content = "Add Timing Change",
            HorizontalAlignment = HorizontalAlignment.Left,
            Width = 145
        }, "dataBPMChange_Add");
        addButton.Click += (_, _) => AddNewTimingChange();
        footerRow.Children.Add(addButton);
        var exitButton = AutomationHelper.WithAutomationId(new Button {
            Name = "btnExit",
            Content = "Exit",
            HorizontalAlignment = HorizontalAlignment.Right,
            Width = 60
        }, "btnExit");
        exitButton.Click += (_, _) => Close();
        Grid.SetColumn(exitButton, 1);
        footerRow.Children.Add(exitButton);
        footer.Child = footerRow;
        root.Children.Add(footer);

        var body = new StackPanel {
            Margin = new Thickness(15),
            Spacing = 10
        };
        body.Children.Add(new TextBlock {
            Text = "Timing Changes:",
            Padding = new Thickness(0, 0, 0, 5),
            FontWeight = FontWeight.Bold,
            FontFamily = new FontFamily("Bahnschrift"),
            FontSize = 14
        });

        dataGridRoot = AutomationHelper.WithAutomationId(new ContentControl {
            Name = "dataBPMChange",
            Focusable = true
        }, "dataBPMChange");
        dataGridRoot.KeyDown += OnKeyboardTargetKeyDown;

        var gridBorder = new Border {
            BorderBrush = new SolidColorBrush(Color.Parse("#C7D2E7")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(10),
            Height = 200
        };

        var gridContainer = new StackPanel {
            Spacing = 8
        };
        gridContainer.Children.Add(BuildHeaderRow());
        gridRowsHost = new StackPanel {
            Spacing = 6
        };
        gridContainer.Children.Add(new ScrollViewer {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = gridRowsHost
        });
        gridBorder.Child = gridContainer;
        dataGridRoot.Content = gridBorder;
        body.Children.Add(dataGridRoot);

        root.Children.Add(body);
        return root;
    }

    Control BuildHeaderRow() {
        var header = new Grid {
            ColumnDefinitions = new ColumnDefinitions("48,*,*,*"),
            ColumnSpacing = 8
        };
        AddHeaderText(header, 0, "Row");
        AddHeaderText(header, 1, "Global Beat");
        AddHeaderText(header, 2, "BPM");
        AddHeaderText(header, 3, "Beat Division");
        return header;
    }

    void AddHeaderText(Grid grid, int column, string text) {
        var label = new TextBlock {
            Text = text,
            FontWeight = FontWeight.SemiBold
        };
        Grid.SetColumn(label, column);
        grid.Children.Add(label);
    }

    void OnKeyboardTargetKeyDown(object? sender, KeyEventArgs e) {
        if (e.Key != Key.Delete) {
            return;
        }

        DeleteSelectedRow();
        e.Handled = true;
    }

    void LoadFromMap() {
        lblGlobalBPM.Text = mainWindow.TxtSongBpm.Text ?? "0";
        bpmChanges.Clear();
        var source = mainWindow.MapEditorInstance?.currentMapDifficulty?.bpmChanges ?? [];
        bpmChanges.AddRange(source
            .OrderBy(change => change.globalBeat)
            .Select(CloneBpmChange));
        RefreshRows();
    }

    internal void RefreshRows() {
        isRefreshingRows = true;
        try {
            if (selectedRowIndex >= bpmChanges.Count) {
                selectedRowIndex = bpmChanges.Count - 1;
            }

            gridRowsHost.Children.Clear();
            for (var rowIndex = 0; rowIndex < bpmChanges.Count; rowIndex++) {
                gridRowsHost.Children.Add(BuildRow(rowIndex, bpmChanges[rowIndex]));
            }
        } finally {
            isRefreshingRows = false;
        }
    }

    Control BuildRow(int rowIndex, BPMChange change) {
        var row = AutomationHelper.WithAutomationId(new Grid {
            Name = $"dataBPMChange_Row{rowIndex}",
            ColumnDefinitions = new ColumnDefinitions("48,*,*,*"),
            ColumnSpacing = 8,
            Background = selectedRowIndex == rowIndex
                ? new SolidColorBrush(Color.Parse("#E6F1FF"))
                : Brushes.Transparent
        }, $"dataBPMChange_Row{rowIndex}");

        var selectButton = AutomationHelper.WithAutomationId(new Button {
            Name = $"dataBPMChange_Row{rowIndex}_Select",
            Content = selectedRowIndex == rowIndex ? "Sel" : "Row",
            Padding = new Thickness(4, 0)
        }, $"dataBPMChange_Row{rowIndex}_Select");
        selectButton.Click += (_, _) => {
            selectedRowIndex = rowIndex;
            dataGridRoot.Focus();
            RefreshRows();
        };
        Grid.SetColumn(selectButton, 0);
        row.Children.Add(selectButton);

        row.Children.Add(CreateCell(rowIndex, 0, change.globalBeat.ToString("0.###", CultureInfo.CurrentCulture)));
        row.Children.Add(CreateCell(rowIndex, 1, change.BPM.ToString("0.###", CultureInfo.CurrentCulture)));
        row.Children.Add(CreateCell(rowIndex, 2, change.gridDivision.ToString(CultureInfo.InvariantCulture)));
        return row;
    }

    Control CreateCell(int rowIndex, int columnIndex, string text) {
        var textBox = AutomationHelper.WithAutomationId(new TextBox {
            Name = $"dataBPMChange_Cell{rowIndex}_{columnIndex}",
            Text = text,
            HorizontalContentAlignment = HorizontalAlignment.Left
        }, $"dataBPMChange_Cell{rowIndex}_{columnIndex}");
        textBox.GotFocus += (_, _) => selectedRowIndex = rowIndex;
        textBox.LostFocus += (_, _) => {
            if (!isRefreshingRows && !isPersistingChanges) {
                CommitCell(rowIndex, columnIndex, textBox);
            }
        };
        textBox.KeyDown += (_, e) => {
            if (e.Key == Key.Enter) {
                CommitCell(rowIndex, columnIndex, textBox);
                dataGridRoot.Focus();
                e.Handled = true;
            }
        };
        Grid.SetColumn(textBox, columnIndex + 1);
        return textBox;
    }

    void CommitCell(int rowIndex, int columnIndex, TextBox textBox) {
        if (rowIndex < 0 || rowIndex >= bpmChanges.Count) {
            return;
        }

        var current = bpmChanges[rowIndex];
        switch (columnIndex) {
            case 0:
                if (TryParseDouble(textBox.Text, out var beat) && beat >= 0) {
                    current.globalBeat = beat;
                    ApplyLocalChanges();
                    return;
                }

                ShowValidationError("The beat must be a non-negative number.");
                textBox.Text = current.globalBeat.ToString("0.###", CultureInfo.CurrentCulture);
                break;
            case 1:
                if (TryParseDouble(textBox.Text, out var bpm) && bpm > 0) {
                    current.BPM = bpm;
                    ApplyLocalChanges();
                    return;
                }

                ShowValidationError("The BPM must be a positive number.");
                textBox.Text = current.BPM.ToString("0.###", CultureInfo.CurrentCulture);
                break;
            case 2:
                if (int.TryParse(textBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var division) &&
                    division >= 1 &&
                    division <= Editor.GridDivisionMax) {
                    current.gridDivision = division;
                    ApplyLocalChanges();
                    return;
                }

                ShowValidationError($"The grid division amount must be an integer from 1 to {Editor.GridDivisionMax}.");
                textBox.Text = current.gridDivision.ToString(CultureInfo.InvariantCulture);
                break;
        }
    }

    void DeleteSelectedRow() {
        if (selectedRowIndex < 0 || selectedRowIndex >= bpmChanges.Count) {
            return;
        }

        bpmChanges.RemoveAt(selectedRowIndex);
        selectedRowIndex = Math.Min(selectedRowIndex, bpmChanges.Count - 1);
        ApplyLocalChanges(forceSaveToMap: true);
    }

    void AddNewTimingChange() {
        var beat = Math.Round(mainWindow.SliderSongProgress.Value / 60000.0 * Math.Max(1, mainWindow.CurrentGlobalBpm), 3);
        bpmChanges.Add(new BPMChange(beat, mainWindow.CurrentGlobalBpm, mainWindow.CurrentGridDivision));
        selectedRowIndex = bpmChanges.Count - 1;
        ApplyLocalChanges(forceSaveToMap: true);
    }

    void ApplyLocalChanges(bool forceSaveToMap = false) {
        if (isPersistingChanges) {
            return;
        }

        isPersistingChanges = true;
        try {
            bpmChanges.Sort((left, right) => left.globalBeat.CompareTo(right.globalBeat));
            RefreshRows();
            SaveLocalChangesToMap();
        } finally {
            isPersistingChanges = false;
        }
    }

    void ShowValidationError(string message) {
        mainWindow.Session.ShowError(this, "Error", message, onDismissed: null);
    }

    static bool TryParseDouble(string? text, out double value) {
        return double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value) ||
               double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    static BPMChange CloneBpmChange(BPMChange change) {
        return new BPMChange(change.globalBeat, change.BPM, change.gridDivision);
    }

    void SaveLocalChangesToMap() {
        var difficulty = mainWindow.MapEditorInstance?.currentMapDifficulty;
        if (difficulty == null) {
            return;
        }

        difficulty.bpmChanges = new SortedSet<BPMChange>(bpmChanges.Select(CloneBpmChange));
        difficulty.MarkDirty();
        mainWindow.RefreshEditorGridFromToolWindow();
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
}
