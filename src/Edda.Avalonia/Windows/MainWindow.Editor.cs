using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using Avalonia.Threading;
using Edda.Classes.MapEditorNS;
using Edda.Classes.MapEditorNS.NoteNS;
using Edda.Classes.MapEditorNS.Stats;
using Edda.Const;
using AvaloniaColor = Avalonia.Media.Color;
using Button = Avalonia.Controls.Button;

namespace Edda.Avalonia.Windows;

public sealed partial class MainWindow {
    const double EditorSurfaceDefaultWidth = 500;
    const double EditorSurfaceMinWidth = 300;
    const double EditorSurfaceMaxWidth = 600;
    const double EditorViewportDefaultHeight = 620;
    const double EditorViewportMinHeight = 520;
    const double EditorSurfaceMinimumBeats = 32;
    const double EditorDragThreshold = 8;
    const double EditorPanelSideMargin = 50;
    const double EditorLaneSubdivisionCount = 16;
    const double EditorMarkerHeight = 26;
    const double EditorMinimumNoteHeight = 28;
    const double EditorMaximumNoteHeight = 72;
    const double EditorMouseWheelScrollStep = 72;

    ScrollViewer scrollEditor = null!;
    Grid editorPanel = null!;
    Grid editorViewport = null!;
    Grid editorMarginGrid = null!;
    Grid drumGrid = null!;
    Grid scrollEditorRoot = null!;
    Canvas scrollEditorCanvas = null!;
    Canvas scrollEditorOverlay = null!;
    Border scrollEditorInputLayer = null!;
    Border scrollEditorSelection = null!;
    Line editorMouseoverLine = null!;
    global::Avalonia.Controls.Image editorPreviewNote = null!;
    global::Avalonia.Controls.Image scrollEditorHoldIcon = null!;
    readonly Dictionary<string, global::Avalonia.Controls.Image> editorNoteVisuals = new(StringComparer.Ordinal);
    readonly Dictionary<string, Rectangle> navNoteVisuals = new(StringComparer.Ordinal);
    readonly List<double> editorGridBeatLines = new();
    readonly List<double> editorMajorGridBeatLines = new();

    TextBlock notesStatsAll = null!;
    TextBlock notesStatsSelected = null!;
    TextBlock notesStatsSingle = null!;
    TextBlock notesStatsDouble = null!;
    TextBlock notesStatsTriplePlusLabel = null!;
    TextBlock notesStatsTriplePlus = null!;
    TextBlock columnStatsValue1 = null!;
    TextBlock columnStatsValue2 = null!;
    TextBlock columnStatsValue3 = null!;
    TextBlock columnStatsValue4 = null!;
    TextBlock columnStatsPercentage1 = null!;
    TextBlock columnStatsPercentage2 = null!;
    TextBlock columnStatsPercentage3 = null!;
    TextBlock columnStatsPercentage4 = null!;
    TextBlock npsStatsSong = null!;
    TextBlock npsStatsMapped = null!;
    TextBlock npsStats16Beat = null!;
    TextBlock npsStats8Beat = null!;
    TextBlock npsStats4Beat = null!;

    bool editorPointerInside;
    bool editorPointerPressed;
    bool editorDragSelectionActive;
    bool editorLayoutRefreshQueued;
    bool suppressNextEditorClick;
    bool suppressEditorScrollSync;
    bool suppressSpectrogramScrollSync;
    Point? editorSelectionStart;
    EditorMarkerDescriptor? editorDraggedMarker;
    Control? editorDraggedMarkerControl;
    IPointer? editorCapturedPointer;
    double editorHoveredBeatSnapped;
    double editorHoveredBeatUnsnapped;
    double editorHoveredCanvasY = -1;
    int editorHoveredColumn = -1;
    double? lastPlacedEditorBeat;
    int? lastPlacedEditorColumn;
    double editorViewportWidth = EditorSurfaceDefaultWidth;
    double editorViewportHeight = EditorViewportDefaultHeight;

    Control BuildEditorSurface() {
        scrollEditor = AutomationHelper.WithAutomationId(new ScrollViewer {
            Name = "scrollEditor",
            MinWidth = EditorSurfaceMinWidth,
            MinHeight = EditorViewportMinHeight,
            Background = Brushes.Transparent,
            BorderBrush = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            ClipToBounds = true,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Hidden
        }, "scrollEditor");
        scrollEditor.PropertyChanged += OnEditorScrollViewerPropertyChanged;
        scrollEditorRoot = new Grid {
            ClipToBounds = true,
            Background = Brushes.Transparent,
            Focusable = true
        };

        mainWaveformCanvas = AutomationHelper.WithAutomationId(new Canvas {
            Name = "MainWaveform",
            Margin = new Thickness(EditorPanelSideMargin, 0),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            IsVisible = false,
            IsHitTestVisible = false,
            Opacity = 0.6
        }, "MainWaveform");

        scrollEditorCanvas = new Canvas {
            Background = Brushes.Transparent,
            ClipToBounds = true
        };

        scrollEditorOverlay = new Canvas {
            ClipToBounds = true,
            IsHitTestVisible = false
        };

        scrollEditorSelection = new Border {
            IsVisible = false,
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(AvaloniaColor.Parse("#2A67D4")),
            Background = new SolidColorBrush(AvaloniaColor.Parse("#402A67D4"))
        };

        editorMouseoverLine = AutomationHelper.WithAutomationId(new Line {
            Name = "lineGridMouseover",
            Stroke = new SolidColorBrush(AvaloniaColor.Parse(Editor.GridPreviewLine.Colour)),
            StrokeThickness = Editor.GridPreviewLine.Thickness,
            IsVisible = false
        }, "lineGridMouseover");

        editorPreviewNote = AutomationHelper.WithAutomationId(new global::Avalonia.Controls.Image {
            Name = "imgPreviewNote",
            Stretch = Stretch.Fill,
            Opacity = Editor.PreviewNoteOpacity,
            IsVisible = false
        }, "imgPreviewNote");
        AutomationProperties.SetName(editorPreviewNote, "Preview Note");

        scrollEditorHoldIcon = AutomationHelper.WithAutomationId(new global::Avalonia.Controls.Image {
            Name = "scrollEditorHoldIcon",
            Width = 30,
            Height = 30,
            Source = GetResourceBitmap("scrollHold.png"),
            Stretch = Stretch.Uniform,
            IsVisible = false
        }, "scrollEditorHoldIcon");

        scrollEditorOverlay.Children.Add(editorMouseoverLine);
        scrollEditorOverlay.Children.Add(editorPreviewNote);
        scrollEditorOverlay.Children.Add(scrollEditorSelection);
        scrollEditorOverlay.Children.Add(scrollEditorHoldIcon);
        scrollEditorInputLayer = AutomationHelper.WithAutomationId(new Border {
            Name = "scrollEditorInputLayer",
            Background = Brushes.Transparent,
            ClipToBounds = true,
            Focusable = true
        }, "scrollEditorInputLayer");
        scrollEditorInputLayer.AddHandler(InputElement.PointerPressedEvent, OnScrollEditorPointerPressed, RoutingStrategies.Bubble, handledEventsToo: true);
        scrollEditorInputLayer.AddHandler(InputElement.PointerMovedEvent, OnScrollEditorPointerMoved, RoutingStrategies.Bubble, handledEventsToo: true);
        scrollEditorInputLayer.AddHandler(InputElement.PointerReleasedEvent, OnScrollEditorPointerReleased, RoutingStrategies.Bubble, handledEventsToo: true);
        scrollEditorInputLayer.AddHandler(InputElement.PointerExitedEvent, OnScrollEditorPointerExited, RoutingStrategies.Bubble, handledEventsToo: true);
        scrollEditorInputLayer.AddHandler(InputElement.PointerWheelChangedEvent, OnScrollEditorPointerWheelChanged, RoutingStrategies.Bubble, handledEventsToo: true);
        scrollEditorRoot.Children.Add(mainWaveformCanvas);
        scrollEditorRoot.Children.Add(scrollEditorCanvas);
        scrollEditorRoot.Children.Add(scrollEditorOverlay);
        scrollEditorRoot.Children.Add(scrollEditorInputLayer);
        scrollEditor.Content = scrollEditorRoot;

        ApplyEditorLayoutMetrics(refreshSurface: false);
        RefreshEditorSurface();
        return scrollEditor;
    }

    double GetEditorContentHeight() {
        return Math.Max(GetEditorViewportHeight(), GetEditorBeatRange() * GetEditorBeatPixelLength() + GetEditorViewportHeight());
    }

    double GetEditorViewportWidth() {
        return editorViewportWidth;
    }

    double GetEditorViewportHeight() {
        return editorViewportHeight;
    }

    double GetVisibleDockWidth(Border? dock) {
        if (dock == null || !dock.IsVisible) {
            return 0;
        }

        if (dock.Bounds.Width > 1) {
            return dock.Bounds.Width;
        }

        return double.IsNaN(dock.Width) ? 0 : dock.Width;
    }

    double GetTargetEditorPanelWidth() {
        var centerHostWidth = editorPanel?.Parent is Control parent && parent.Bounds.Width > 1
            ? parent.Bounds.Width
            : 0;
        if (centerHostWidth <= 1) {
            var clientWidth = ClientSize.Width > 1
                ? ClientSize.Width
                : Bounds.Width > 1
                    ? Bounds.Width
                    : 0;
            if (clientWidth > 1) {
                centerHostWidth = clientWidth - GetVisibleDockWidth(leftSidebar) - GetVisibleDockWidth(rightSidebar);
            }
        }

        if (centerHostWidth <= 1 && editorPanel != null && editorPanel.Bounds.Width > 1) {
            centerHostWidth = editorPanel.Bounds.Width;
        }

        return centerHostWidth > 1 ? Math.Max(EditorSurfaceMinWidth, centerHostWidth) : 0;
    }

    double GetAvailableEditorViewportWidth() {
        var panelWidth = editorPanel != null && editorPanel.Bounds.Width > 1
            ? editorPanel.Bounds.Width
            : GetTargetEditorPanelWidth();
        if (panelWidth <= 1) {
            return 0;
        }

        var spectrogramWidth = gridSpectrogram != null && gridSpectrogram.IsVisible && gridSpectrogram.Bounds.Width > 1
            ? gridSpectrogram.Bounds.Width
            : borderSpectrogram != null && borderSpectrogram.IsVisible && borderSpectrogram.Bounds.Width > 1
                ? borderSpectrogram.Bounds.Width
                : gridSpectrogram?.Width ?? 0;
        var navWidth = borderNavWaveform != null && borderNavWaveform.Bounds.Width > 1
            ? borderNavWaveform.Bounds.Width
            : ImgWaveformVertical != null && ImgWaveformVertical.Bounds.Width > 1
                ? ImgWaveformVertical.Bounds.Width
                : ImgWaveformVertical?.Width ?? 0;

        return Math.Max(0, panelWidth - spectrogramWidth - navWidth);
    }

    double GetEditorMaxScrollOffset() {
        return Math.Max(0, GetEditorContentHeight() - GetEditorViewportHeight());
    }

    internal void ApplyEditorLayoutMetrics(bool refreshSurface = true) {
        if (scrollEditor == null || scrollEditorRoot == null || scrollEditorCanvas == null || scrollEditorOverlay == null || scrollEditorInputLayer == null) {
            return;
        }

        if (gridSpectrogram != null && gridSpectrogram.IsVisible) {
            SetSpectrogramWidth(GetSpectrogramWidth(), refreshSurface: false);
        }

        var viewportWidth = GetAvailableEditorViewportWidth();
        if (viewportWidth <= 1) {
            viewportWidth = editorViewport != null && editorViewport.Bounds.Width > 1
                ? editorViewport.Bounds.Width
                : scrollEditor.Bounds.Width > 1
                    ? scrollEditor.Bounds.Width
                    : EditorSurfaceDefaultWidth;
        }
        viewportWidth = Math.Clamp(viewportWidth, EditorSurfaceMinWidth, EditorSurfaceMaxWidth);

        var viewportHeight = editorViewport != null && editorViewport.Bounds.Height > 1
            ? editorViewport.Bounds.Height
            : scrollEditor.Bounds.Height > 1
                ? scrollEditor.Bounds.Height
                : EditorViewportDefaultHeight;
        viewportHeight = Math.Max(EditorViewportMinHeight, viewportHeight);

        var layoutChanged = Math.Abs(editorViewportWidth - viewportWidth) > 0.5 ||
            Math.Abs(editorViewportHeight - viewportHeight) > 0.5;

        editorViewportWidth = viewportWidth;
        editorViewportHeight = viewportHeight;

        if (editorPanel != null) {
            var targetPanelWidth = GetTargetEditorPanelWidth();
            if (targetPanelWidth > 1) {
                editorPanel.MaxWidth = targetPanelWidth;
            }
            editorPanel.Width = double.NaN;
        }
        if (editorViewport != null) {
            editorViewport.Width = viewportWidth;
        }
        scrollEditor.Width = viewportWidth;
        scrollEditorRoot.Width = viewportWidth;
        scrollEditorCanvas.Width = viewportWidth;
        scrollEditorOverlay.Width = viewportWidth;
        scrollEditorInputLayer.Width = viewportWidth;

        var drumHeight = GetEditorNoteHeight();
        if (lineBeatScan != null) {
            lineBeatScan.StartPoint = new Point(0, 0);
            lineBeatScan.EndPoint = new Point(Math.Max(GetEditorLaneVisualWidth(), drumGrid?.Bounds.Width ?? 0), 0);
            lineBeatScan.VerticalAlignment = VerticalAlignment.Center;
            lineBeatScan.Margin = default;
        }

        if (drumGrid != null) {
            drumGrid.Height = drumHeight;
        }

        if (drum0 != null) {
            drum0.Height = drumHeight;
        }
        if (drum1 != null) {
            drum1.Height = drumHeight;
        }
        if (drum2 != null) {
            drum2.Height = drumHeight;
        }
        if (drum3 != null) {
            drum3.Height = drumHeight;
        }

        if (navWaveformVisualHost != null) {
            navWaveformVisualHost.Height = viewportHeight;
        }

        if (ImgWaveformVertical != null) {
            ImgWaveformVertical.Height = viewportHeight;
        }

        if (refreshSurface && layoutChanged && mapEditor?.currentMapDifficulty != null) {
            QueueEditorSurfaceRefresh();
        } else if (refreshSurface) {
            RefreshSongProgressIndicator();
        }

        UpdateEditorHoverVisuals();
    }

    void QueueEditorSurfaceRefresh() {
        if (editorLayoutRefreshQueued) {
            return;
        }

        editorLayoutRefreshQueued = true;
        Dispatcher.UIThread.Post(() => {
            editorLayoutRefreshQueued = false;
            if (mapEditor?.currentMapDifficulty != null) {
                RefreshEditorSurface();
            }
        }, DispatcherPriority.Background);
    }

    internal void SyncEditorScrollToCurrentBeat() {
        if (scrollEditor == null || editorPointerPressed) {
            return;
        }

        try {
            var ratio = SliderSongProgress.Maximum <= 0
                ? 0
                : Math.Clamp(currentSongPositionMilliseconds / SliderSongProgress.Maximum, 0, 1);
            suppressEditorScrollSync = true;
            scrollEditor.Offset = new Vector(0, (1 - ratio) * GetEditorMaxScrollOffset());
            suppressEditorScrollSync = false;
            SyncSpectrogramScrollToEditor();
        } catch (Exception) {
            suppressEditorScrollSync = false;
        }
    }

    internal void ScrollEditorToBeat(double beat) {
        if (scrollEditor == null) {
            return;
        }

        var contentHeight = GetEditorContentHeight();
        var maxOffset = Math.Max(0, contentHeight - GetEditorViewportHeight());
        var desiredOffset = BeatToCanvasY(beat) - (GetEditorViewportHeight() * 0.7);
        suppressEditorScrollSync = true;
        scrollEditor.Offset = new Vector(0, Math.Clamp(desiredOffset, 0, maxOffset));
        suppressEditorScrollSync = false;
        SyncSpectrogramScrollToEditor();
    }

    Control BuildStatsPanel() {
        var panel = new StackPanel {
            Spacing = 0,
            Margin = new Thickness(10, 0, 12, 0),
            Width = SidebarDockWidth - 22,
            MaxWidth = SidebarDockWidth - 22,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        notesStatsAll = CreateStatsText("notesStatsAll");
        notesStatsSelected = CreateStatsText("notesStatsSelected");
        notesStatsSingle = CreateStatsText("notesStatsSingle");
        notesStatsDouble = CreateStatsText("notesStatsDouble");
        notesStatsTriplePlusLabel = AutomationHelper.WithAutomationId(new TextBlock {
            Name = "notesStatsTriplePlusLabel",
            Text = "Triple+",
            FontSize = 9.25,
            VerticalAlignment = VerticalAlignment.Center
        }, "notesStatsTriplePlusLabel");
        notesStatsTriplePlus = CreateStatsText("notesStatsTriplePlus");
        columnStatsValue1 = CreateStatsText("columnStatsValue1");
        columnStatsValue2 = CreateStatsText("columnStatsValue2");
        columnStatsValue3 = CreateStatsText("columnStatsValue3");
        columnStatsValue4 = CreateStatsText("columnStatsValue4");
        columnStatsPercentage1 = CreateStatsText("columnStatsPercentage1");
        columnStatsPercentage2 = CreateStatsText("columnStatsPercentage2");
        columnStatsPercentage3 = CreateStatsText("columnStatsPercentage3");
        columnStatsPercentage4 = CreateStatsText("columnStatsPercentage4");
        npsStatsSong = CreateStatsText("npsStatsSong");
        npsStatsMapped = CreateStatsText("npsStatsMapped");
        npsStats16Beat = CreateStatsText("npsStats16Beat");
        npsStats8Beat = CreateStatsText("npsStats8Beat");
        npsStats4Beat = CreateStatsText("npsStats4Beat");

        var notesGrid = BuildCompactStatsGrid(
            new[] { 0.75, 0.95, 1.0, 1.05, 1.25 },
            new Thickness(2, 1),
            (CreateStatsLabel("All"), notesStatsAll),
            (CreateStatsLabel("Select"), notesStatsSelected),
            (CreateStatsLabel("Single"), notesStatsSingle),
            (CreateStatsLabel("Double"), notesStatsDouble),
            (notesStatsTriplePlusLabel, notesStatsTriplePlus));
        panel.Children.Add(BuildStatsExpander(
            "notesStats",
            "Notes",
            notesGrid,
            isExpanded: true));
        panel.Children.Add(CreateStatsDivider());
        panel.Children.Add(BuildStatsExpander(
            "columnStats",
            "Column variety",
            BuildCompactColumnStatsGrid()));
        panel.Children.Add(CreateStatsDivider());
        panel.Children.Add(BuildStatsExpander(
            "npsStats",
            "Notes per second (NPS)",
            BuildCompactNpsGrid()));

        return panel;
    }

    static Expander BuildStatsExpander(string automationId, string headerText, Control content, bool isExpanded = false) {
        var expander = AutomationHelper.WithAutomationId(new Expander {
            Name = automationId,
            IsExpanded = isExpanded,
            Width = SidebarDockWidth - 22,
            MaxWidth = SidebarDockWidth - 22,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            ClipToBounds = true,
            Header = new TextBlock {
                Text = headerText,
                FontWeight = FontWeight.DemiBold
            },
            Content = content
        }, automationId);
        expander.ExpandDirection = ExpandDirection.Down;
        return expander;
    }

    static Border CreateStatsDivider() {
        return new Border {
            BorderBrush = new SolidColorBrush(AvaloniaColor.Parse("#6D6E73")),
            BorderThickness = new Thickness(0, 0, 0, 1)
        };
    }

    static Grid BuildCompactStatsGrid(double[] columnWeights, Thickness cellPadding, params (TextBlock label, TextBlock value)[] columns) {
        var grid = new Grid {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        for (var index = 0; index < columns.Length; index++) {
            var weight = index < columnWeights.Length ? columnWeights[index] : 1;
            grid.ColumnDefinitions.Add(new ColumnDefinition(weight, GridUnitType.Star));
        }

        grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

        for (var index = 0; index < columns.Length; index++) {
            var (label, value) = columns[index];
            AddCompactStatsCell(grid, 0, index, label, cellPadding);
            AddCompactStatsCell(grid, 1, index, value, cellPadding);
            if (index < columns.Length - 1) {
                var separator = new Border {
                    BorderBrush = new SolidColorBrush(AvaloniaColor.Parse("#6D6E73")),
                    BorderThickness = new Thickness(0, 0, 1, 0)
                };
                Grid.SetColumn(separator, index);
                Grid.SetRowSpan(separator, 2);
                grid.Children.Add(separator);
            }
        }

        return grid;
    }

    Grid BuildCompactColumnStatsGrid() {
        return BuildCompactStatsGrid(
            [1, 1, 1, 1],
            new Thickness(5, 1),
            (columnStatsValue1, columnStatsPercentage1),
            (columnStatsValue2, columnStatsPercentage2),
            (columnStatsValue3, columnStatsPercentage3),
            (columnStatsValue4, columnStatsPercentage4));
    }

    Grid BuildCompactNpsGrid() {
        var grid = new Grid {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        grid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
        grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(40)));
        grid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
        grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(40)));

        var cellPadding = new Thickness(5, 1);
        AddCompactStatsCell(grid, 0, 0, new TextBlock { Text = "Song:", FontSize = 9.25, VerticalAlignment = VerticalAlignment.Center }, cellPadding);
        AddCompactStatsCell(grid, 0, 1, npsStatsSong, cellPadding);
        AddCompactStatsCell(grid, 0, 2, new TextBlock { Text = "16-beat:", FontSize = 9.25, VerticalAlignment = VerticalAlignment.Center }, cellPadding);
        AddCompactStatsCell(grid, 0, 3, npsStats16Beat, cellPadding);

        AddCompactStatsCell(grid, 1, 0, new TextBlock { Text = "Mapped:", FontSize = 9.25, VerticalAlignment = VerticalAlignment.Center }, cellPadding);
        AddCompactStatsCell(grid, 1, 1, npsStatsMapped, cellPadding);
        AddCompactStatsCell(grid, 1, 2, new TextBlock { Text = "8-beat:", FontSize = 9.25, VerticalAlignment = VerticalAlignment.Center }, cellPadding);
        AddCompactStatsCell(grid, 1, 3, npsStats8Beat, cellPadding);

        AddCompactStatsCell(grid, 2, 2, new TextBlock { Text = "4-beat:", FontSize = 9.25, VerticalAlignment = VerticalAlignment.Center }, cellPadding);
        AddCompactStatsCell(grid, 2, 3, npsStats4Beat, cellPadding);

        var separator = new Border {
            BorderBrush = new SolidColorBrush(AvaloniaColor.Parse("#6D6E73")),
            BorderThickness = new Thickness(0, 0, 1, 0)
        };
        Grid.SetColumn(separator, 1);
        Grid.SetRowSpan(separator, 3);
        grid.Children.Add(separator);
        return grid;
    }

    static void AddCompactStatsCell(Grid grid, int row, int column, Control child, Thickness padding) {
        var cell = new Border {
            Padding = padding,
            Child = child
        };
        Grid.SetRow(cell, row);
        Grid.SetColumn(cell, column);
        grid.Children.Add(cell);
    }

    static TextBlock CreateStatsLabel(string text) {
        return new TextBlock {
            Text = text,
            FontSize = 9.25,
            VerticalAlignment = VerticalAlignment.Center
        };
    }

    static TextBlock CreateStatsText(string automationId) {
        return AutomationHelper.WithAutomationId(new TextBlock {
            Name = automationId,
            Text = "0",
            FontSize = 9.25,
            VerticalAlignment = VerticalAlignment.Center
        }, automationId);
    }

    internal void SetMapStats(MapStats stats) {
        var triplePlusNotes = stats.tripleNotes + stats.quadrupleNotes;
        notesStatsAll.Text = stats.allNotes.ToString(CultureInfo.InvariantCulture);
        notesStatsSelected.Text = stats.selectedNotes.ToString(CultureInfo.InvariantCulture);
        notesStatsSingle.Text = stats.singleNotes.ToString(CultureInfo.InvariantCulture);
        notesStatsDouble.Text = stats.doubleNotes.ToString(CultureInfo.InvariantCulture);
        notesStatsTriplePlus.Text = triplePlusNotes.ToString(CultureInfo.InvariantCulture);
        columnStatsValue1.Text = stats.columnCounts[0].ToString(CultureInfo.InvariantCulture);
        columnStatsValue2.Text = stats.columnCounts[1].ToString(CultureInfo.InvariantCulture);
        columnStatsValue3.Text = stats.columnCounts[2].ToString(CultureInfo.InvariantCulture);
        columnStatsValue4.Text = stats.columnCounts[3].ToString(CultureInfo.InvariantCulture);
        columnStatsPercentage1.Text = $"{stats.columnPercentages[0].ToString(CultureInfo.InvariantCulture)}%";
        columnStatsPercentage2.Text = $"{stats.columnPercentages[1].ToString(CultureInfo.InvariantCulture)}%";
        columnStatsPercentage3.Text = $"{stats.columnPercentages[2].ToString(CultureInfo.InvariantCulture)}%";
        columnStatsPercentage4.Text = $"{stats.columnPercentages[3].ToString(CultureInfo.InvariantCulture)}%";
        npsStatsSong.Text = stats.npsSong.ToString(CultureInfo.InvariantCulture);
        npsStatsMapped.Text = stats.npsMapped.ToString(CultureInfo.InvariantCulture);
        npsStats16Beat.Text = stats.nps16Beat.ToString(CultureInfo.InvariantCulture);
        npsStats8Beat.Text = stats.nps8Beat.ToString(CultureInfo.InvariantCulture);
        npsStats4Beat.Text = stats.nps4Beat.ToString(CultureInfo.InvariantCulture);
        var triplePlusWeight = triplePlusNotes > 0 ? FontWeight.Bold : FontWeight.Normal;
        SetTextForeground(notesStatsTriplePlusLabel, triplePlusNotes > 0 ? Editor.Stats.WarningColour : Editor.Stats.Colour);
        SetTextForeground(notesStatsTriplePlus, triplePlusNotes > 0 ? Editor.Stats.WarningColour : Editor.Stats.Colour);
        notesStatsTriplePlusLabel.FontWeight = triplePlusWeight;
        notesStatsTriplePlus.FontWeight = triplePlusWeight;
    }

    void RefreshEditorSurface() {
        if (scrollEditorCanvas == null || mapEditor?.currentMapDifficulty == null) {
            return;
        }

        ApplyEditorLayoutMetrics(refreshSurface: false);
        var contentHeight = GetEditorContentHeight();
        scrollEditorRoot.Height = contentHeight;
        scrollEditorCanvas.Height = contentHeight;
        scrollEditorOverlay.Height = contentHeight;
        scrollEditorInputLayer.Height = contentHeight;

        scrollEditorCanvas.Children.Clear();
        editorNoteVisuals.Clear();
        navNoteVisuals.Clear();
        RefreshMainWaveformVisuals(contentHeight);
        DrawEditorGridLines();

        foreach (var note in mapEditor.currentMapDifficulty.notes) {
            DrawEditorNotes([note]);
        }

        foreach (var bookmark in mapEditor.currentMapDifficulty.bookmarks) {
            scrollEditorCanvas.Children.Add(CreateBookmarkVisual(bookmark));
        }

        foreach (var bpmChange in mapEditor.currentMapDifficulty.bpmChanges) {
            scrollEditorCanvas.Children.Add(CreateTimingChangeVisual(bpmChange));
        }

        UpdateScrollEditorAutomationStatus();
        RefreshSpectrogramVisuals();
        RefreshNavigationVisuals();
        SyncEditorScrollToCurrentBeat();
        UpdateEditorHoverVisuals();
    }

    global::Avalonia.Controls.Image CreateNoteVisual(Note note) {
        var isSelected = mapEditor?.currentMapDifficulty?.selectedNotes.Contains(note) == true;
        var width = GetEditorNoteWidth();
        var height = GetEditorNoteHeight();
        var control = new global::Avalonia.Controls.Image {
            Width = width,
            Height = height,
            Source = GetRuneBitmap(note.beat, isSelected),
            Stretch = Stretch.Fill,
            IsHitTestVisible = false
        };

        AutomationProperties.SetName(control, $"Note {note.col + 1}");
        AutomationHelper.SetItemStatus(control, "hit:0|opacity:1");
        Canvas.SetLeft(control, GetEditorNoteLeft(note.col));
        Canvas.SetTop(control, BeatToCanvasY(note.beat) - (height / 2));
        return control;
    }

    string GetNoteVisualKey(Note note) {
        return Helper.UidGenerator(note);
    }

    void DrawEditorNotes(IEnumerable<Note> notes) {
        if (scrollEditorCanvas == null) {
            return;
        }

        foreach (var note in notes) {
            var key = GetNoteVisualKey(note);
            if (editorNoteVisuals.ContainsKey(key)) {
                continue;
            }

            var visual = CreateNoteVisual(note);
            editorNoteVisuals[key] = visual;
            scrollEditorCanvas.Children.Add(visual);
        }
    }

    void UndrawEditorNotes(IEnumerable<Note> notes) {
        if (scrollEditorCanvas == null) {
            return;
        }

        foreach (var note in notes) {
            var key = GetNoteVisualKey(note);
            if (!editorNoteVisuals.Remove(key, out var visual)) {
                continue;
            }

            scrollEditorCanvas.Children.Remove(visual);
        }
    }

    void UpdateEditorNoteHighlights(IEnumerable<Note> notes, bool isHighlighted) {
        foreach (var note in notes) {
            var key = GetNoteVisualKey(note);
            if (!editorNoteVisuals.TryGetValue(key, out var visual)) {
                continue;
            }

            visual.Source = GetRuneBitmap(note.beat, isHighlighted);
        }
    }

    void UpdateAllEditorNoteHighlights(bool isHighlighted) {
        if (mapEditor?.currentMapDifficulty == null) {
            return;
        }

        foreach (var note in mapEditor.currentMapDifficulty.notes) {
            UpdateEditorNoteHighlights([note], isHighlighted);
        }
    }

    Rectangle CreateNavNoteVisual(Note note) {
        var navNoteBrush = ResolveBrush(userSettings.GetValueForKey(UserSettingsKey.NavNoteColor), Editor.NavNote.Colour);
        var navSelectedNoteBrush = ResolveBrush(userSettings.GetValueForKey(UserSettingsKey.NavSelectedNoteColor), Editor.NavNote.HighlightColour);
        var width = GetNavWaveformWidth();
        var height = GetNavWaveformHeight();
        var horizontalOffset = (width - (4 * Editor.NavNote.Size) - (3 * Editor.NavNote.ColumnGap)) / 2;
        var y = BeatToNavY(note.beat, height) - (Editor.NavNote.Size / 2);

        var noteSquare = new Rectangle {
            Width = Editor.NavNote.Size,
            Height = Editor.NavNote.Size,
            Fill = mapEditor?.currentMapDifficulty?.selectedNotes.Contains(note) == true ? navSelectedNoteBrush : navNoteBrush,
            IsHitTestVisible = false
        };
        Canvas.SetLeft(noteSquare, horizontalOffset + note.col * (Editor.NavNote.Size + Editor.NavNote.ColumnGap));
        Canvas.SetTop(noteSquare, y);
        return noteSquare;
    }

    void DrawNavNotes(IEnumerable<Note> notes) {
        if (CanvasNavNotes == null || ImgWaveformVertical == null) {
            return;
        }

        foreach (var note in notes) {
            var key = GetNoteVisualKey(note);
            if (navNoteVisuals.ContainsKey(key)) {
                continue;
            }

            var visual = CreateNavNoteVisual(note);
            navNoteVisuals[key] = visual;
            CanvasNavNotes.Children.Add(visual);
        }
    }

    void UndrawNavNotes(IEnumerable<Note> notes) {
        if (CanvasNavNotes == null) {
            return;
        }

        foreach (var note in notes) {
            var key = GetNoteVisualKey(note);
            if (!navNoteVisuals.Remove(key, out var visual)) {
                continue;
            }

            CanvasNavNotes.Children.Remove(visual);
        }
    }

    void UpdateNavNoteHighlights(IEnumerable<Note> notes, bool isHighlighted) {
        var navNoteBrush = ResolveBrush(userSettings.GetValueForKey(UserSettingsKey.NavNoteColor), Editor.NavNote.Colour);
        var navSelectedNoteBrush = ResolveBrush(userSettings.GetValueForKey(UserSettingsKey.NavSelectedNoteColor), Editor.NavNote.HighlightColour);

        foreach (var note in notes) {
            var key = GetNoteVisualKey(note);
            if (!navNoteVisuals.TryGetValue(key, out var visual)) {
                continue;
            }

            visual.Fill = isHighlighted ? navSelectedNoteBrush : navNoteBrush;
        }
    }

    void UpdateAllNavNoteHighlights(bool isHighlighted) {
        if (mapEditor?.currentMapDifficulty == null) {
            return;
        }

        foreach (var note in mapEditor.currentMapDifficulty.notes) {
            UpdateNavNoteHighlights([note], isHighlighted);
        }
    }

    Control CreateBookmarkVisual(Bookmark bookmark) {
        var markerWidth = GetEditorLaneVisualWidth() / 2;
        var marker = new Canvas {
            Width = markerWidth,
            Height = EditorMarkerHeight,
            Tag = new EditorMarkerDescriptor(bookmark, null),
            IsHitTestVisible = false
        };
        AutomationProperties.SetName(marker, "Bookmark");

        var lineY = EditorMarkerHeight / 2;
        marker.Children.Add(new Line {
            StartPoint = new Point(0, lineY),
            EndPoint = new Point(markerWidth, lineY),
            Stroke = ResolveBrush(null, Editor.GridBookmark.Colour, opacity: Editor.GridBookmark.Opacity),
            StrokeThickness = Editor.GridBookmark.Thickness
        });
        marker.Children.Add(CreateMarkerLabel(
            bookmark.name,
            markerWidth,
            EditorMarkerHeight,
            TextAlignment.Right,
            ResolveBrush(null, Editor.GridBookmark.NameColour, opacity: Editor.GridBookmark.Opacity),
            Editor.GridBookmark.NameSize,
            Editor.GridBookmark.NamePadding));

        Canvas.SetLeft(marker, GetEditorBookmarkMarkerLeft());
        SetMarkerCanvasTop(marker, bookmark.beat);
        return marker;
    }

    Control CreateTimingChangeVisual(BPMChange bpmChange) {
        const double timingMarkerHeight = 42;
        var markerWidth = GetEditorLaneVisualWidth() / 2;
        var marker = new Canvas {
            Width = markerWidth,
            Height = timingMarkerHeight,
            Tag = new EditorMarkerDescriptor(null, bpmChange),
            IsHitTestVisible = false
        };
        AutomationProperties.SetName(marker, $"1/{Math.Max(1, bpmChange.gridDivision)} beat");

        var lineY = timingMarkerHeight - Math.Max(2, Editor.GridBPMChange.Thickness);
        marker.Children.Add(new Line {
            StartPoint = new Point(0, lineY),
            EndPoint = new Point(markerWidth, lineY),
            Stroke = ResolveBrush(null, Editor.GridBPMChange.Colour, opacity: Editor.GridBPMChange.Opacity),
            StrokeThickness = Editor.GridBPMChange.Thickness
        });

        var divisionLabel = CreateMarkerLabel(
            $"1/{Math.Max(1, bpmChange.gridDivision)} beat",
            null,
            18,
            TextAlignment.Left,
            ResolveBrush(null, Editor.GridBPMChange.NameColour, opacity: Editor.GridBPMChange.Opacity),
            Editor.GridBPMChange.NameSize,
            Editor.GridBPMChange.NamePadding);
        Canvas.SetTop(divisionLabel, Math.Max(0, lineY - 19));
        marker.Children.Add(divisionLabel);

        var bpmLabel = CreateMarkerLabel(
            $"{FormatNumber(bpmChange.BPM)} BPM",
            null,
            18,
            TextAlignment.Left,
            ResolveBrush(null, Editor.GridBPMChange.NameColour, opacity: Editor.GridBPMChange.Opacity),
            Editor.GridBPMChange.NameSize,
            Editor.GridBPMChange.NamePadding);
        Canvas.SetTop(bpmLabel, Math.Max(0, lineY - 35));
        marker.Children.Add(bpmLabel);

        Canvas.SetLeft(marker, EditorPanelSideMargin);
        SetMarkerCanvasTop(marker, bpmChange.globalBeat);
        return marker;
    }

    Border CreateMarkerLabel(string text, double? width, double height, TextAlignment textAlignment, IBrush background, double fontSize, double padding) {
        var textBlock = new TextBlock {
            Text = text,
            Foreground = Brushes.White,
            FontSize = fontSize,
            FontWeight = FontWeight.Bold,
            TextAlignment = textAlignment,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = width.HasValue ? HorizontalAlignment.Stretch : HorizontalAlignment.Left
        };
        var label = new Border {
            Height = height,
            Background = background,
            Padding = new Thickness(padding, 0)
        };
        label.Child = textBlock;
        if (width.HasValue) {
            label.Width = width.Value;
        } else {
            label.Measure(Size.Infinity);
            label.Width = Math.Ceiling(label.DesiredSize.Width);
        }
        return label;
    }

    void DrawEditorGridLines() {
        var gridHeight = Math.Max(0, GetEditorContentHeight() - GetEditorViewportHeight());
        var contentHeight = GetEditorContentHeight();
        var laneLeft = EditorPanelSideMargin;
        var laneRight = GetEditorViewportWidth() - EditorPanelSideMargin;
        var unitSubLength = GetEditorUnitSubLength();
        var beatPixelLength = GetEditorBeatPixelLength();
        var hitLineOffset = GetEditorHitLineOffset();
        var difficulty = mapEditor?.currentMapDifficulty;

        editorMajorGridBeatLines.Clear();
        editorGridBeatLines.Clear();

        scrollEditorCanvas.Children.Add(new Line {
            StartPoint = new Point(laneLeft, 0),
            EndPoint = new Point(laneLeft, contentHeight),
            Stroke = new SolidColorBrush(AvaloniaColor.Parse("#6A85B8")),
            StrokeThickness = 1
        });
        scrollEditorCanvas.Children.Add(new Line {
            StartPoint = new Point(laneRight, 0),
            EndPoint = new Point(laneRight, contentHeight),
            Stroke = new SolidColorBrush(AvaloniaColor.Parse("#6A85B8")),
            StrokeThickness = 1
        });

        for (var separator = 1; separator <= 3; separator++) {
            var x = laneLeft + (separator * unitSubLength * 4);
            scrollEditorCanvas.Children.Add(new Line {
                StartPoint = new Point(x, 0),
                EndPoint = new Point(x, contentHeight),
                Stroke = new SolidColorBrush(AvaloniaColor.Parse("#6A85B8")),
                StrokeThickness = 1
            });
        }

        if (difficulty == null) {
            return;
        }

        var offset = 0.0;
        var localBpm = Math.Max(1, CurrentGlobalBpm);
        var localGridDiv = Math.Max(1, CurrentGridDivision);
        var counter = 0;
        using var bpmChanges = difficulty.bpmChanges.GetEnumerator();
        var hasNextBpmChange = bpmChanges.MoveNext();

        while (offset <= gridHeight + 0.5) {
            var isMajor = counter % localGridDiv == 0;
            var currentBeat = offset / beatPixelLength;
            if (isMajor) {
                editorMajorGridBeatLines.Add(currentBeat);
            }
            editorGridBeatLines.Add(currentBeat);
            var y = contentHeight - offset - hitLineOffset;
            scrollEditorCanvas.Children.Add(new Line {
                StartPoint = new Point(laneLeft, y),
                EndPoint = new Point(laneRight, y),
                Stroke = new SolidColorBrush(AvaloniaColor.Parse(isMajor ? Editor.MajorGridlineColour : Editor.MinorGridlineColour)),
                StrokeThickness = isMajor ? Editor.MajorGridlineThickness : Editor.MinorGridlineThickness
            });

            offset += CurrentGlobalBpm / localBpm * beatPixelLength / localGridDiv;
            counter++;

            if (hasNextBpmChange && offset / beatPixelLength >= bpmChanges.Current.globalBeat - 0.0001) {
                var next = bpmChanges.Current;
                offset = next.globalBeat * beatPixelLength;
                localBpm = Math.Max(1, next.BPM);
                localGridDiv = Math.Max(1, next.gridDivision);
                hasNextBpmChange = bpmChanges.MoveNext();
                counter = 0;
            }
        }
    }

    void RefreshMainWaveformVisuals(double contentHeight) {
        if (mainWaveformCanvas == null) {
            return;
        }

        EnsureMainWaveformImage();
        mainWaveformCanvas.Height = contentHeight;
        mainWaveformCanvas.Width = GetEditorLaneVisualWidth();
        mainWaveformCanvas.IsVisible = CheckWaveform.IsChecked ?? false;
        if (!mainWaveformCanvas.IsVisible) {
            ReplaceBitmap(ref renderedMainWaveformBitmap, null, mainWaveformImage);
            return;
        }

        var beatContentHeight = Math.Max(1, contentHeight - GetEditorViewportHeight());
        var topPadding = GetEditorTopPadding();
        if (mainWaveformImage != null) {
            mainWaveformImage.Width = mainWaveformCanvas.Width;
            mainWaveformImage.Height = beatContentHeight;
            Canvas.SetTop(mainWaveformImage, topPadding);
        }

        AutomationHelper.SetItemStatus(mainWaveformCanvas, $"offset:{scrollEditor?.Offset.Y ?? 0:0.##}|height:{beatContentHeight:0.##}");
        UpdateScrollEditorAutomationStatus(beatContentHeight);
        ScheduleMainWaveformRender(mainWaveformCanvas.Width, beatContentHeight);
    }

    void UpdateScrollEditorAutomationStatus(double? waveformHeightOverride = null) {
        if (scrollEditor == null || mapEditor?.currentMapDifficulty == null) {
            return;
        }

        var waveformVisible = CheckWaveform.IsChecked ?? false;
        var waveformHeight = waveformHeightOverride ?? Math.Max(1, GetEditorContentHeight() - GetEditorViewportHeight());
        var laneOneNote = mapEditor.currentMapDifficulty.notes
            .Where(note => note.col == 0)
            .OrderBy(note => note.beat)
            .FirstOrDefault();
        var laneAligned = true;
        if (laneOneNote != null) {
            var noteHeight = GetEditorNoteHeight();
            var noteTop = BeatToCanvasY(laneOneNote.beat) - (noteHeight / 2);
            var noteCenter = noteTop + (noteHeight / 2);
            laneAligned = Math.Abs(noteCenter - BeatToCanvasY(laneOneNote.beat)) <= 0.5;
        }

        AutomationHelper.SetItemStatus(
            scrollEditor,
            $"waveformVisible:{waveformVisible.ToString().ToLowerInvariant()}|waveformOffset:{scrollEditor.Offset.Y:0.##}|waveformHeight:{waveformHeight:0.##}|lane1Aligned:{laneAligned.ToString().ToLowerInvariant()}|hoverCol:{editorHoveredColumn}|hoverSnapped:{editorHoveredBeatSnapped:0.###}|hoverUnsnapped:{editorHoveredBeatUnsnapped:0.###}|lastPlacedCol:{(lastPlacedEditorColumn.HasValue ? lastPlacedEditorColumn.Value.ToString(CultureInfo.InvariantCulture) : "-")}|lastPlacedBeat:{(lastPlacedEditorBeat.HasValue ? lastPlacedEditorBeat.Value.ToString("0.###", CultureInfo.InvariantCulture) : "-")}");
        AutomationHelper.SetItemStatus(
            CheckWaveform,
            $"waveformVisible:{waveformVisible.ToString().ToLowerInvariant()}|waveformOffset:{scrollEditor.Offset.Y:0.##}|waveformHeight:{waveformHeight:0.##}");
        AutomationProperties.SetName(
            CheckWaveform,
            $"waveformVisible:{waveformVisible.ToString().ToLowerInvariant()}|waveformOffset:{scrollEditor.Offset.Y:0.##}|waveformHeight:{waveformHeight:0.##}");
        UpdateLaneOneDrumAutomationStatus(laneAligned);
    }

    void UpdateLaneOneDrumAutomationStatus(bool laneAligned) {
        if (drum0 == null) {
            return;
        }

        AutomationHelper.SetItemStatus(drum0, $"lane1Aligned:{laneAligned.ToString().ToLowerInvariant()}|hit:{drumFeedbackSequence}");
        AutomationProperties.SetName(drum0, $"lane1Aligned:{laneAligned.ToString().ToLowerInvariant()}|hit:{drumFeedbackSequence}");
    }

    double GetEditorUnitSubLength() {
        if (editorMarginGrid?.ColumnDefinitions.Count > 1) {
            var laneColumnWidth = editorMarginGrid.ColumnDefinitions[1].ActualWidth;
            if (laneColumnWidth > 1) {
                return laneColumnWidth / 3.0;
            }
        }

        if (drumGrid?.ColumnDefinitions.Count > 1) {
            var laneColumnWidth = drumGrid.ColumnDefinitions[1].ActualWidth;
            if (laneColumnWidth > 1) {
                return laneColumnWidth / 3.0;
            }
        }

        return GetEditorLaneVisualWidth() / 17.0;
    }

    double GetEditorGridSpacing() {
        if (mapEditor == null) {
            return 1;
        }

        var spacing = GetMapDouble("_editorGridSpacing", RagnarockMapDifficulties.Current, custom: true);
        return spacing > 0 ? spacing : 1;
    }

    double GetEditorBeatPixelLength() {
        return GetEditorNoteWidth() * GetEditorGridSpacing();
    }

    double GetEditorLaneVisualWidth() {
        return GetEditorViewportWidth() - (EditorPanelSideMargin * 2);
    }

    double GetEditorBookmarkMarkerLeft() {
        return GetEditorViewportWidth() - EditorPanelSideMargin - (GetEditorLaneVisualWidth() / 2);
    }

    double GetEditorNoteLeft(int column) {
        return EditorPanelSideMargin + ((1 + (4 * column)) * GetEditorUnitSubLength());
    }

    double GetEditorNoteWidth() {
        return GetEditorUnitSubLength() * 3;
    }

    double GetEditorNoteHeight() {
        return Math.Max(EditorMinimumNoteHeight, GetEditorNoteWidth());
    }

    double GetEditorHitLineOffset() {
        if (drumGrid != null && drumGrid.Bounds.Height > 1) {
            return drumGrid.Bounds.Height / 2;
        }

        return GetEditorNoteHeight() / 2;
    }

    double GetEditorTopPadding() {
        return Math.Max(0, GetEditorViewportHeight() - GetEditorHitLineOffset());
    }

    void SetMarkerCanvasTop(Control markerControl, double beat) {
        var markerLineY = markerControl.Tag is EditorMarkerDescriptor { BpmChange: not null }
            ? markerControl.Height - Math.Max(2, Editor.GridBPMChange.Thickness)
            : markerControl.Height / 2;

        Canvas.SetTop(markerControl, BeatToCanvasY(beat) - markerLineY);
    }

    Bitmap? GetRuneBitmap(double beat, bool isHighlighted) {
        var normalizedBeat = beat;
        if (mapEditor != null) {
            var lastChange = mapEditor.GetLastBeatChange(beat);
            var gridLength = mapEditor.GetGridLength(lastChange.BPM, 1);
            if (gridLength > 0) {
                normalizedBeat = (beat - lastChange.globalBeat) / gridLength;
            }
        }

        const int denominator = Editor.GridDivisionMax * 6;
        var fraction = ((int)Math.Round(normalizedBeat * denominator) % denominator + denominator) % denominator;
        var rune = fraction switch {
            0 => "1",
            denominator * 1 / 4 => "14",
            denominator * 1 / 3 => "13",
            denominator * 5 / 6 => "13",
            denominator * 1 / 2 => "12",
            denominator * 2 / 3 => "23",
            denominator * 1 / 6 => "23",
            denominator * 3 / 4 => "34",
            _ => "X"
        };

        return GetResourceBitmap($"rune{rune}{(isHighlighted ? "highlight" : string.Empty)}.png");
    }

    void OnScrollEditorPointerPressed(object? sender, PointerPressedEventArgs e) {
        if (!TryGetHoverState(e.GetPosition(scrollEditorInputLayer), out var hoverState, allowOutsideBounds: false)) {
            FocusEditorSurface();
            HideEditorHoverVisuals();
            e.Handled = true;
            return;
        }

        editorPointerInside = true;
        editorHoveredBeatSnapped = hoverState.snappedBeat;
        editorHoveredBeatUnsnapped = hoverState.unsnappedBeat;
        editorHoveredCanvasY = hoverState.position.Y;
        editorHoveredColumn = hoverState.column;
        UpdateEditorBeatDisplay();
        UpdateEditorHoverVisuals();

        var point = e.GetCurrentPoint(scrollEditorInputLayer);
        if (!songIsPlaying &&
            (point.Properties.PointerUpdateKind == PointerUpdateKind.RightButtonPressed || point.Properties.IsRightButtonPressed)) {
            RemoveHoveredNote();
            e.Handled = true;
            return;
        }

        if (songIsPlaying ||
            point.Properties.PointerUpdateKind == PointerUpdateKind.RightButtonPressed ||
            point.Properties.IsRightButtonPressed) {
            return;
        }

        FocusEditorSurface();
        e.Pointer.Capture(scrollEditorInputLayer);
        editorCapturedPointer = e.Pointer;
        editorPointerPressed = true;
        editorDragSelectionActive = false;
        suppressNextEditorClick = false;
        editorSelectionStart = hoverState.position;

        var markerControl = FindMarkerControlAtPosition(hoverState.position);
        if (markerControl is { Tag: EditorMarkerDescriptor descriptor }) {
            editorDraggedMarker = descriptor;
            editorDraggedMarkerControl = markerControl;
        } else {
            editorDraggedMarker = null;
            editorDraggedMarkerControl = null;
        }

        e.Handled = true;
    }

    void OnScrollEditorPointerMoved(object? sender, PointerEventArgs e) {
        if (!TryGetHoverState(e.GetPosition(scrollEditorInputLayer), out var hoverState, allowOutsideBounds: editorPointerPressed)) {
            return;
        }

        editorPointerInside = true;
        editorHoveredBeatSnapped = hoverState.snappedBeat;
        editorHoveredBeatUnsnapped = hoverState.unsnappedBeat;
        editorHoveredCanvasY = hoverState.position.Y;
        editorHoveredColumn = hoverState.column;
        UpdateEditorBeatDisplay();
        UpdateEditorHoverVisuals();

        var currentPoint = e.GetCurrentPoint(scrollEditorInputLayer);
        if (editorPointerPressed && !currentPoint.Properties.IsLeftButtonPressed) {
            FinalizeLeftGesture(hoverState.position);
            e.Handled = true;
            return;
        }

        if (!editorPointerPressed) {
            return;
        }

        if (editorDraggedMarkerControl != null) {
            SetMarkerCanvasTop(editorDraggedMarkerControl, GetActiveEditorBeat());
            suppressNextEditorClick = true;
            e.Handled = true;
            return;
        }

        if (editorSelectionStart.HasValue &&
            Distance(editorSelectionStart.Value, hoverState.position) > EditorDragThreshold) {
            editorDragSelectionActive = true;
            suppressNextEditorClick = true;
            UpdateSelectionRectangle(editorSelectionStart.Value, hoverState.position);
            e.Handled = true;
        }
    }

    void OnScrollEditorPointerReleased(object? sender, PointerReleasedEventArgs e) {
        var currentPoint = e.GetCurrentPoint(scrollEditorInputLayer);
        var updateKind = currentPoint.Properties.PointerUpdateKind;

        if (updateKind == PointerUpdateKind.RightButtonReleased && !songIsPlaying) {
            RemoveHoveredNote();
            e.Handled = true;
            return;
        }

        if (!editorPointerPressed) {
            return;
        }

        if (!TryGetHoverState(e.GetPosition(scrollEditorInputLayer), out var hoverState, allowOutsideBounds: editorPointerPressed)) {
            ResetEditorPointerGesture(e.Pointer);
            return;
        }

        editorHoveredBeatSnapped = hoverState.snappedBeat;
        editorHoveredBeatUnsnapped = hoverState.unsnappedBeat;
        editorHoveredCanvasY = hoverState.position.Y;
        editorHoveredColumn = hoverState.column;
        UpdateEditorBeatDisplay();
        UpdateEditorHoverVisuals();

        FinalizeLeftGesture(hoverState.position);
        e.Handled = true;
    }

    void OnScrollEditorPointerWheelChanged(object? sender, PointerWheelEventArgs e) {
        if (!e.KeyModifiers.HasFlag(KeyModifiers.Control)) {
            if (scrollEditor == null) {
                return;
            }

            var deltaOffset = -e.Delta.Y * EditorMouseWheelScrollStep;
            if (Math.Abs(deltaOffset) > 0.001) {
                suppressEditorScrollSync = true;
                scrollEditor.Offset = new Vector(0, Math.Clamp(scrollEditor.Offset.Y + deltaOffset, 0, GetEditorMaxScrollOffset()));
                suppressEditorScrollSync = false;
                SyncSpectrogramScrollToEditor();

                var maxOffset = GetEditorMaxScrollOffset();
                var ratio = maxOffset <= 0
                    ? 0
                    : 1 - Math.Clamp(scrollEditor.Offset.Y / maxOffset, 0, 1);
                SetSongPosition(ratio * SliderSongProgress.Maximum, updateSlider: true, updateNavWaveform: false, updateEditorScroll: false);
                e.Handled = true;
            }
            return;
        }

        if (mapEditor?.currentMapDifficulty == null) {
            return;
        }

        var delta = Math.Sign(e.Delta.Y);
        if (delta == 0) {
            return;
        }

        var beat = editorPointerInside ? GetActiveEditorBeat(snappedToGrid: false) : GetCurrentSongBeat();
        if (scrollEditorInputLayer != null &&
            TryGetHoverState(e.GetPosition(scrollEditorInputLayer), out var hoverState, allowOutsideBounds: false)) {
            editorPointerInside = true;
            editorHoveredBeatSnapped = hoverState.snappedBeat;
            editorHoveredBeatUnsnapped = hoverState.unsnappedBeat;
            editorHoveredCanvasY = hoverState.position.Y;
            editorHoveredColumn = hoverState.column;
            UpdateEditorBeatDisplay();
            UpdateEditorHoverVisuals();
            beat = hoverState.unsnappedBeat;
        }

        var lastChange = mapEditor.GetLastBeatChange(beat);
        var currentBeat = GetCurrentSongBeat();
        if (currentBeat > beat &&
            currentBeat - beat <= 0.5) {
            var currentBeatChange = mapEditor.GetLastBeatChange(currentBeat);
            if (currentBeatChange.globalBeat > lastChange.globalBeat) {
                beat = currentBeat;
                lastChange = currentBeatChange;
            }
        }

        if (lastChange.globalBeat <= 0.0001) {
            var nextDivision = Math.Clamp(CurrentGridDivision + delta, 1, Editor.GridDivisionMax);
            mapEditor.SetMapValue("_editorGridDivision", Newtonsoft.Json.Linq.JToken.FromObject(nextDivision), RagnarockMapDifficulties.Current, custom: true);
            TxtGridDivision.Text = nextDivision.ToString(CultureInfo.InvariantCulture);
        } else {
            lastChange.gridDivision = Math.Clamp(lastChange.gridDivision + delta, 1, Editor.GridDivisionMax);
            mapEditor.currentMapDifficulty.MarkDirty();
        }

        RefreshEditorSurface();
        e.Handled = true;
    }

    void OnScrollEditorPointerExited(object? sender, PointerEventArgs e) {
        editorPointerInside = false;
        editorHoveredCanvasY = -1;
        editorHoveredColumn = -1;
        HideEditorHoverVisuals();
        var seconds = currentSongPositionMilliseconds / 1000.0;
        var beat = GetCurrentSongBeat();
        LblSelectedBeat.Text = $"Time: {Helper.TimeFormat(seconds)} | Global Beat: {beat:0.##}";
    }

    void OnScrollEditorClick() {
        if (suppressNextEditorClick) {
            suppressNextEditorClick = false;
            return;
        }

        if (!songIsPlaying) {
            ToggleOrAddHoveredNote();
        }
    }

    void FinalizeLeftGesture(Point position) {
        if (editorDraggedMarker != null) {
            suppressNextEditorClick = true;
            FinalizeDraggedMarker();
        } else if (editorDragSelectionActive && editorSelectionStart.HasValue) {
            suppressNextEditorClick = true;
            SelectNotesInRectangle(editorSelectionStart.Value, position);
        } else if (!songIsPlaying) {
            suppressNextEditorClick = true;
            ToggleOrAddHoveredNote();
        }

        ResetEditorPointerGesture(null);
    }

    void ResetEditorPointerGesture(IPointer? pointer) {
        (pointer ?? editorCapturedPointer)?.Capture(null);
        editorCapturedPointer = null;
        editorPointerPressed = false;
        editorDragSelectionActive = false;
        editorSelectionStart = null;
        editorDraggedMarker = null;
        editorDraggedMarkerControl = null;
        scrollEditorSelection.IsVisible = false;
        UpdateEditorHoverVisuals();
    }

    void UpdateEditorBeatDisplay() {
        var beat = GetActiveEditorBeat();
        var seconds = beat * 60.0 / Math.Max(1, CurrentGlobalBpm);
        LblSelectedBeat.Text = $"Time: {Helper.TimeFormat(seconds)} | Global Beat: {beat:0.###}";
    }

    void UpdateSelectionRectangle(Point start, Point end) {
        var x = Math.Min(start.X, end.X);
        var y = Math.Min(start.Y, end.Y);
        scrollEditorSelection.IsVisible = true;
        scrollEditorSelection.Width = Math.Abs(end.X - start.X);
        scrollEditorSelection.Height = Math.Abs(end.Y - start.Y);
        Canvas.SetLeft(scrollEditorSelection, x);
        Canvas.SetTop(scrollEditorSelection, y);
    }

    void SelectNotesInRectangle(Point start, Point end) {
        if (mapEditor?.currentMapDifficulty == null) {
            return;
        }

        var minBeat = Math.Min(CanvasYToBeat(start.Y), CanvasYToBeat(end.Y));
        var maxBeat = Math.Max(CanvasYToBeat(start.Y), CanvasYToBeat(end.Y));
        var minCol = Math.Min(PositionToColumn(start.X), PositionToColumn(end.X));
        var maxCol = Math.Max(PositionToColumn(start.X), PositionToColumn(end.X));

        var selectedNotes = mapEditor.currentMapDifficulty.notes
            .Where(note => note.beat >= minBeat - 0.001 &&
                           note.beat <= maxBeat + 0.001 &&
                           note.col >= Math.Max(0, minCol) &&
                           note.col <= Math.Min(3, maxCol))
            .ToList();

        mapEditor.SelectNewNotes(selectedNotes);
    }

    void ToggleOrAddHoveredNote() {
        if (mapEditor?.currentMapDifficulty == null || editorHoveredColumn < 0 || editorHoveredColumn > 3) {
            return;
        }

        var note = CreateHoveredNote();
        if (mapEditor.currentMapDifficulty.notes.Contains(note)) {
            mapEditor.SelectNewNotes(note);
        } else {
            mapEditor.AddNotes(note);
            lastPlacedEditorBeat = note.beat;
            lastPlacedEditorColumn = note.col;
            drummer?.Play(note.col);
            AnimateDrum(note.col);
            AnimateNote(note);
            UpdateDifficultyPrediction();
            UpdateScrollEditorAutomationStatus();
        }
    }

    void RemoveHoveredNote() {
        if (mapEditor == null || editorHoveredColumn < 0 || editorHoveredColumn > 3) {
            return;
        }

        var note = CreateHoveredNote();
        mapEditor.RemoveNote(note);
        UpdateDifficultyPrediction();
    }

    void FinalizeDraggedMarker() {
        if (mapEditor?.currentMapDifficulty == null || editorDraggedMarker == null) {
            return;
        }

        var newBeat = Math.Round(GetActiveEditorBeat(), 3);
        if (editorDraggedMarker.Bookmark != null) {
            mapEditor.RemoveBookmark(editorDraggedMarker.Bookmark);
            editorDraggedMarker.Bookmark.beat = newBeat;
            mapEditor.AddBookmark(editorDraggedMarker.Bookmark);
            return;
        }

        if (editorDraggedMarker.BpmChange != null) {
            mapEditor.RemoveBPMChange(editorDraggedMarker.BpmChange, redraw: false);
            editorDraggedMarker.BpmChange.globalBeat = newBeat;
            mapEditor.AddBPMChange(editorDraggedMarker.BpmChange);
            RefreshOpenToolWindows();
        }
    }

    Note CreateHoveredNote() {
        return new Note(Math.Round(GetActiveEditorBeat(), 3), editorHoveredColumn);
    }

    void AddNoteAtEditorBeat(int column) {
        if (mapEditor == null || column < 0 || column > 3) {
            return;
        }

        var note = new Note(Math.Round(GetActiveEditorBeat(), 3), column);
        mapEditor.AddNotes(note);
        lastPlacedEditorBeat = note.beat;
        lastPlacedEditorColumn = note.col;
        drummer?.Play(note.col);
        AnimateDrum(note.col);
        AnimateNote(note);
        UpdateDifficultyPrediction();
        UpdateScrollEditorAutomationStatus();
    }

    void UpdateEditorHoverVisuals() {
        if (editorPreviewNote == null || editorMouseoverLine == null) {
            return;
        }

        if (!editorPointerInside ||
            songIsPlaying ||
            editorHoveredColumn < 0 ||
            editorHoveredColumn > 3) {
            HideEditorHoverVisuals();
            return;
        }

        var noteWidth = GetEditorNoteWidth();
        var noteHeight = GetEditorNoteHeight();
        var hoveredBeat = GetActiveEditorBeat();
        var noteTop = BeatToCanvasY(hoveredBeat) - (noteHeight / 2);
        var noteLeft = GetEditorNoteLeft(editorHoveredColumn);
        var lineY = editorHoveredCanvasY >= 0
            ? Math.Clamp(editorHoveredCanvasY, 0, GetEditorContentHeight())
            : noteTop + (noteHeight / 2);
        var laneLeft = EditorPanelSideMargin;
        var laneRight = GetEditorViewportWidth() - EditorPanelSideMargin;

        editorPreviewNote.Width = noteWidth;
        editorPreviewNote.Height = noteHeight;
        editorPreviewNote.Source = GetRuneBitmap(hoveredBeat, false);
        Canvas.SetLeft(editorPreviewNote, noteLeft);
        Canvas.SetTop(editorPreviewNote, noteTop);
        editorPreviewNote.IsVisible = !editorDragSelectionActive && editorDraggedMarkerControl == null;

        editorMouseoverLine.StartPoint = new Point(laneLeft, lineY);
        editorMouseoverLine.EndPoint = new Point(laneRight, lineY);
        editorMouseoverLine.IsVisible = true;
    }

    void HideEditorHoverVisuals() {
        if (editorPreviewNote != null) {
            editorPreviewNote.IsVisible = false;
        }

        if (editorMouseoverLine != null) {
            editorMouseoverLine.IsVisible = false;
        }
    }

    double GetActiveEditorBeat(bool snappedToGrid = true) {
        return snappedToGrid && snapToGrid ? editorHoveredBeatSnapped : editorHoveredBeatUnsnapped;
    }

    bool TryGetHoverState(Point position, out (Point position, int column, double snappedBeat, double unsnappedBeat) state, bool allowOutsideBounds = false) {
        var normalizedX = double.IsNaN(position.X) || double.IsInfinity(position.X) ? 0 : position.X;
        var normalizedY = double.IsNaN(position.Y) || double.IsInfinity(position.Y) ? 0 : position.Y;
        var maxHeight = GetEditorContentHeight();
        var maxWidth = GetEditorViewportWidth();
        var isWithinBounds = normalizedX >= 0 && normalizedX <= maxWidth && normalizedY >= 0 && normalizedY <= maxHeight;
        if (!isWithinBounds && !allowOutsideBounds) {
            state = default;
            return false;
        }

        var x = Math.Clamp(normalizedX, 0, maxWidth);
        var y = Math.Clamp(normalizedY, 0, maxHeight);
        var column = PositionToColumn(x);
        if (column < 0 || column > 3) {
            state = default;
            return false;
        }

        var rawBeat = CanvasYToBeat(y);
        var snappedBeat = Math.Round(SnapBeat(rawBeat), 3);
        var unsnappedBeat = Math.Round(rawBeat, 3);
        state = (new Point(x, y), column, snappedBeat, unsnappedBeat);
        return true;
    }

    int PositionToColumn(double x) {
        var subLength = (x - EditorPanelSideMargin) / GetEditorUnitSubLength();
        if (subLength < 0) {
            return -1;
        }

        if (subLength <= 4.5) {
            return 0;
        }

        if (subLength <= 8.5) {
            return 1;
        }

        if (subLength <= 12.5) {
            return 2;
        }

        if (subLength <= 17.0) {
            return 3;
        }

        return -1;
    }

    double BeatToCanvasY(double beat) {
        var contentHeight = GetEditorContentHeight();
        return contentHeight - (Math.Max(0, beat) * GetEditorBeatPixelLength()) - GetEditorHitLineOffset();
    }

    double CanvasYToBeat(double y) {
        var contentHeight = GetEditorContentHeight();
        var position = Math.Max(0, contentHeight - y - GetEditorHitLineOffset());
        return position / Math.Max(1, GetEditorBeatPixelLength());
    }

    double GetEditorBeatRange() {
        var songBeatRange = Math.Max(EditorSurfaceMinimumBeats, currentSongDurationSeconds * Math.Max(1, CurrentGlobalBpm) / 60.0);
        if (mapEditor?.currentMapDifficulty == null) {
            return songBeatRange;
        }

        var maxNoteBeat = mapEditor.currentMapDifficulty.notes.Select(note => note.beat).DefaultIfEmpty(0).Max();
        var maxBookmarkBeat = mapEditor.currentMapDifficulty.bookmarks.Select(bookmark => bookmark.beat).DefaultIfEmpty(0).Max();
        var maxTimingBeat = mapEditor.currentMapDifficulty.bpmChanges.Select(change => change.globalBeat).DefaultIfEmpty(0).Max();
        var maxBeat = Math.Max(Math.Max(maxNoteBeat, maxBookmarkBeat), Math.Max(maxTimingBeat, GetCurrentSongBeat()));
        return Math.Max(songBeatRange, Math.Ceiling(maxBeat + 4));
    }

    double SnapBeat(double beat) {
        if (mapEditor?.currentMapDifficulty == null) {
            return Math.Max(0, beat);
        }

        var targetBeat = Math.Max(0, beat);
        EnsureEditorGridBeatLines();
        if (editorGridBeatLines.Count == 0) {
            return targetBeat;
        }

        var binarySearch = editorGridBeatLines.BinarySearch(targetBeat);
        if (binarySearch >= 0) {
            return editorGridBeatLines[binarySearch];
        }

        var upperIndex = Math.Min(editorGridBeatLines.Count - 1, ~binarySearch);
        var lowerIndex = Math.Max(0, upperIndex - 1);
        var upperBeat = editorGridBeatLines[upperIndex];
        var lowerBeat = editorGridBeatLines[lowerIndex];
        return (upperBeat - targetBeat) < (targetBeat - lowerBeat) ? upperBeat : lowerBeat;
    }

    void EnsureEditorGridBeatLines() {
        if (editorGridBeatLines.Count > 0 || mapEditor?.currentMapDifficulty == null) {
            return;
        }

        var beatPixelLength = GetEditorBeatPixelLength();
        if (beatPixelLength <= 0) {
            return;
        }

        var maxOffset = Math.Max(0, GetEditorContentHeight() - GetEditorViewportHeight());
        var offset = 0.0;
        var localBpm = Math.Max(1, CurrentGlobalBpm);
        var localGridDiv = Math.Max(1, CurrentGridDivision);
        var counter = 0;
        using var bpmChanges = mapEditor.currentMapDifficulty.bpmChanges.GetEnumerator();
        var hasNextBpmChange = bpmChanges.MoveNext();

        editorMajorGridBeatLines.Clear();
        editorGridBeatLines.Clear();
        while (offset <= maxOffset + 0.5) {
            var currentBeat = offset / beatPixelLength;
            if (counter % localGridDiv == 0) {
                editorMajorGridBeatLines.Add(currentBeat);
            }
            editorGridBeatLines.Add(currentBeat);

            offset += CurrentGlobalBpm / localBpm * beatPixelLength / localGridDiv;
            counter++;

            if (hasNextBpmChange && offset / beatPixelLength >= bpmChanges.Current.globalBeat - 0.0001) {
                var next = bpmChanges.Current;
                offset = next.globalBeat * beatPixelLength;
                localBpm = Math.Max(1, next.BPM);
                localGridDiv = Math.Max(1, next.gridDivision);
                hasNextBpmChange = bpmChanges.MoveNext();
                counter = 0;
            }
        }
    }

    static double Distance(Point start, Point end) {
        var delta = end - start;
        return Math.Sqrt((delta.X * delta.X) + (delta.Y * delta.Y));
    }

    void OnMainWindowPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e) {
        if (e.Property == BoundsProperty || e.Property == ClientSizeProperty) {
            ApplyEditorLayoutMetrics();
        }
    }

    void OnEditorScrollViewerPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e) {
        if (e.Property == BoundsProperty) {
            ApplyEditorLayoutMetrics();
            return;
        }

        if (e.Property != ScrollViewer.OffsetProperty || scrollEditor == null) {
            return;
        }

        if (ReferenceEquals(sender, scrollEditor)) {
            if (suppressEditorScrollSync || suppressControlEvents || songIsPlaying) {
                return;
            }

            var maxOffset = GetEditorMaxScrollOffset();
            var ratio = maxOffset <= 0
                ? 0
                : 1 - Math.Clamp(scrollEditor.Offset.Y / maxOffset, 0, 1);
            SyncSpectrogramScrollToEditor();
            mainWaveformCanvas?.InvalidateVisual();
            mainWaveformImage?.InvalidateVisual();
            UpdateScrollEditorAutomationStatus();
            SetSongPosition(ratio * SliderSongProgress.Maximum, updateSlider: true, updateNavWaveform: false, updateEditorScroll: false);
            return;
        }

        if (ReferenceEquals(sender, scrollSpectrogram)) {
            if (suppressSpectrogramScrollSync || suppressControlEvents || songIsPlaying) {
                return;
            }

            suppressEditorScrollSync = true;
            scrollEditor.Offset = new Vector(0, scrollSpectrogram.Offset.Y);
            suppressEditorScrollSync = false;

            var maxOffset = GetEditorMaxScrollOffset();
            var ratio = maxOffset <= 0
                ? 0
                : 1 - Math.Clamp(scrollEditor.Offset.Y / maxOffset, 0, 1);
            UpdateScrollEditorAutomationStatus();
            SetSongPosition(ratio * SliderSongProgress.Maximum, updateSlider: true, updateNavWaveform: false, updateEditorScroll: false);
        }
    }

    void SyncSpectrogramScrollToEditor() {
        if (scrollSpectrogram == null || scrollEditor == null) {
            return;
        }

        suppressSpectrogramScrollSync = true;
        scrollSpectrogram.Offset = new Vector(0, scrollEditor.Offset.Y);
        suppressSpectrogramScrollSync = false;
        mainWaveformCanvas?.InvalidateVisual();
        mainWaveformImage?.InvalidateVisual();
    }

    void FocusEditorSurface() {
        TopLevel.GetTopLevel(this)?.FocusManager?.ClearFocus();
        textInputHasFocus = false;
        scrollEditorInputLayer?.Focus();
        scrollEditorRoot?.Focus();
        scrollEditor?.Focus();
        Focus();
    }

    Control? FindMarkerControlAtPosition(Point position) {
        foreach (var child in scrollEditorCanvas.Children.OfType<Control>()) {
            if (child.Tag is not EditorMarkerDescriptor descriptor) {
                continue;
            }

            var left = Canvas.GetLeft(child);
            var top = Canvas.GetTop(child);
            var width = child.Width;
            var height = child.Height;

            if (descriptor.BpmChange != null) {
                foreach (var label in child.GetVisualChildren().OfType<Border>()) {
                    var labelLeft = left + NormalizeCanvasCoordinate(Canvas.GetLeft(label));
                    var labelTop = top + NormalizeCanvasCoordinate(Canvas.GetTop(label));
                    var labelWidth = label.Bounds.Width > 1 ? label.Bounds.Width : label.Width;
                    var labelHeight = label.Bounds.Height > 1 ? label.Bounds.Height : label.Height;
                    if (position.X >= labelLeft &&
                        position.X <= labelLeft + labelWidth &&
                        position.Y >= labelTop &&
                        position.Y <= labelTop + labelHeight) {
                        return child;
                    }
                }

                continue;
            }

            if (position.X >= left && position.X <= left + width &&
                position.Y >= top && position.Y <= top + height) {
                return child;
            }
        }

        return null;
    }

    static double NormalizeCanvasCoordinate(double value) {
        return double.IsNaN(value) || double.IsInfinity(value) ? 0 : value;
    }

    sealed class EditorMarkerDescriptor {
        public EditorMarkerDescriptor(Bookmark? bookmark, BPMChange? bpmChange) {
            Bookmark = bookmark;
            BpmChange = bpmChange;
        }

        public Bookmark? Bookmark { get; }
        public BPMChange? BpmChange { get; }
    }
}
