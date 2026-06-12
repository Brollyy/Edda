using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using System;
using System.Collections.Generic;
using System.IO;
using AvaloniaBrushes = Avalonia.Media.Brushes;
using AvaloniaColor = Avalonia.Media.Color;
using Button = Avalonia.Controls.Button;
using CoreProgram = Edda.Const.Program;
using Horizontal = Avalonia.Layout.HorizontalAlignment;
using PixelPoint = Avalonia.PixelPoint;
using Point = Avalonia.Point;
using Vertical = Avalonia.Layout.VerticalAlignment;

namespace Edda.Avalonia.Windows;

public sealed class StartWindow : Window {
    readonly AppSession appSession;
    readonly List<RecentMapListItem> recentMapItems = new();

    Point? dragStart;
    PixelPoint? windowStart;

    public string WindowId => "StartWindow";
    public Border InvisibleTitleBar { get; }
    public Button ButtonExit { get; }
    public Button ButtonNewMap { get; }
    public Button ButtonImportMap { get; }
    public Button ButtonOpenMap { get; }
    public TextBlock TxtVersionNumber { get; }
    public ListBox ListViewRecentMaps { get; }
    public IReadOnlyList<RecentMapListItem> RecentMapItems => recentMapItems;

    public StartWindow(AppSession appSession) {
        this.appSession = appSession;
        AutomationHelper.SetAutomationId(this, WindowId);

        Width = 800;
        Height = 470;
        MinWidth = 800;
        MinHeight = 470;
        CanResize = false;
        SystemDecorations = SystemDecorations.None;
        Background = AvaloniaBrushes.Transparent;
        Position = new PixelPoint(120, 120);
        Title = "Edda";
        var iconPath = Path.Combine(AppContext.BaseDirectory, "Resources", "icon.png");
        if (File.Exists(iconPath)) {
            Icon = new WindowIcon(iconPath);
        }

        var root = new Grid {
            ColumnDefinitions = new ColumnDefinitions("2*,3*"),
            Background = new LinearGradientBrush {
                StartPoint = new RelativePoint(0.5, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(0.5, 1, RelativeUnit.Relative),
                GradientStops = new GradientStops {
                    new GradientStop(AvaloniaColor.Parse("#EEF4FF"), 0),
                    new GradientStop(AvaloniaColor.Parse("#A9B9D8"), 1)
                }
            }
        };

        InvisibleTitleBar = AutomationHelper.WithAutomationId(new Border {
            Name = "InvisibleTitleBar",
            Height = 24,
            Margin = new Thickness(8),
            HorizontalAlignment = Horizontal.Stretch,
            VerticalAlignment = Vertical.Top,
            Background = AvaloniaBrushes.Transparent
        }, "StartWindowDragSurface");
        InvisibleTitleBar.PointerPressed += InvisibleTitleBar_PointerPressed;
        InvisibleTitleBar.PointerMoved += InvisibleTitleBar_PointerMoved;
        InvisibleTitleBar.PointerReleased += InvisibleTitleBar_PointerReleased;

        ButtonExit = AutomationHelper.WithAutomationId(new Button {
            Name = "ButtonExit",
            Width = 28,
            Height = 28,
            HorizontalAlignment = Horizontal.Right,
            VerticalAlignment = Vertical.Center,
            Content = new TextBlock {
                Text = "×",
                FontSize = 22,
                HorizontalAlignment = Horizontal.Center,
                VerticalAlignment = Vertical.Center
            }
        }, "ButtonExit");
        ButtonExit.Click += (_, _) => appSession.RequestExit();
        InvisibleTitleBar.Child = ButtonExit;
        Grid.SetColumnSpan(InvisibleTitleBar, 2);
        root.Children.Add(InvisibleTitleBar);

        var leftPanel = new StackPanel {
            Width = 220,
            VerticalAlignment = Vertical.Center,
            HorizontalAlignment = Horizontal.Center,
            Spacing = 10
        };
        leftPanel.Children.Add(new Image {
            Source = GetResourceBitmap("icon.png"),
            Width = 110,
            Height = 110,
            HorizontalAlignment = Horizontal.Center,
            Stretch = Stretch.Uniform
        });
        leftPanel.Children.Add(new TextBlock {
            Text = "Edda",
            FontSize = 72,
            FontWeight = FontWeight.Bold,
            HorizontalAlignment = Horizontal.Center,
            Foreground = new SolidColorBrush(AvaloniaColor.Parse("#002668"))
        });

        TxtVersionNumber = AutomationHelper.WithAutomationId(new TextBlock {
            Name = "TxtVersionNumber",
            Text = $"version {CoreProgram.DisplayVersionString}",
            FontSize = 18,
            FontWeight = FontWeight.Bold,
            HorizontalAlignment = Horizontal.Center
        }, "TxtVersionNumber");
        leftPanel.Children.Add(TxtVersionNumber);
        root.Children.Add(leftPanel);

        var rightPanel = new StackPanel {
            Margin = new Thickness(0, 40, 35, 30),
            Spacing = 12,
            HorizontalAlignment = Horizontal.Stretch
        };
        Grid.SetColumn(rightPanel, 1);

        var primaryButtons = new Grid {
            ColumnDefinitions = new ColumnDefinitions("*,*"),
            ColumnSpacing = 10,
            HorizontalAlignment = Horizontal.Stretch
        };

        ButtonNewMap = BuildActionButton("ButtonNewMap", "newMap.png", "New Map", "Create a new map");
        ButtonNewMap.Click += (_, _) => appSession.CreateNewMap(this);
        primaryButtons.Children.Add(ButtonNewMap);

        ButtonImportMap = BuildActionButton("ButtonImportMap", "importMap.png", "Import Map", "Import StepMania simfiles");
        ButtonImportMap.Click += (_, _) => appSession.ImportMap(this);
        Grid.SetColumn(ButtonImportMap, 1);
        primaryButtons.Children.Add(ButtonImportMap);

        rightPanel.Children.Add(primaryButtons);

        ButtonOpenMap = BuildActionButton("ButtonOpenMap", "openMap.png", "Open Map", "Continue working on an existing map");
        ButtonOpenMap.Click += (_, _) => appSession.OpenMapFromPicker(this);
        ButtonOpenMap.HorizontalAlignment = Horizontal.Stretch;
        ButtonOpenMap.MinWidth = 320;
        rightPanel.Children.Add(ButtonOpenMap);

        rightPanel.Children.Add(new Border {
            Height = 2,
            Background = new SolidColorBrush(AvaloniaColor.Parse("#5B6475")),
            Margin = new Thickness(0, 4, 0, 2)
        });

        rightPanel.Children.Add(new TextBlock {
            Text = "Recent Maps",
            FontSize = 18,
            FontWeight = FontWeight.Bold,
            Foreground = new SolidColorBrush(AvaloniaColor.Parse("#002668"))
        });

        ListViewRecentMaps = AutomationHelper.WithAutomationId(new ListBox {
            Name = "ListViewRecentMaps",
            Height = 220,
            BorderThickness = new Thickness(0),
            Background = AvaloniaBrushes.Transparent
        }, "ListViewRecentMaps");
        rightPanel.Children.Add(ListViewRecentMaps);

        root.Children.Add(rightPanel);
        Content = root;

        RefreshRecentMaps();
    }

    public void RefreshRecentMaps() {
        recentMapItems.Clear();
        ListViewRecentMaps.Items.Clear();

        foreach (var (name, path) in appSession.RecentMaps.GetRecentlyOpened()) {
            var item = new RecentMapListItem(
                name,
                path,
                () => appSession.OpenRecentMap(this, path),
                () => appSession.ConfirmRemoveRecentMap(this, path)
            );

            recentMapItems.Add(item);
            ListViewRecentMaps.Items.Add(item);
        }
    }

    static Button BuildActionButton(string name, string iconFileName, string title, string subtitle) {
        return AutomationHelper.WithAutomationId(new Button {
            Name = name,
            HorizontalAlignment = Horizontal.Stretch,
            HorizontalContentAlignment = Horizontal.Left,
            Padding = new Thickness(12, 10),
            Content = new StackPanel {
                Orientation = Orientation.Horizontal,
                Spacing = 10,
                Children = {
                    new Image {
                        Source = GetResourceBitmap(iconFileName),
                        Width = 24,
                        Height = 24,
                        Stretch = Stretch.Uniform,
                        VerticalAlignment = VerticalAlignment.Center
                    },
                    new StackPanel {
                        Spacing = 2,
                        VerticalAlignment = VerticalAlignment.Center,
                        Children = {
                            new TextBlock {
                                Text = title,
                                FontSize = 18,
                                FontWeight = FontWeight.Bold,
                                Foreground = new SolidColorBrush(AvaloniaColor.Parse("#002668"))
                            },
                            new TextBlock {
                                Text = subtitle,
                                FontSize = 13
                            }
                        }
                    }
                }
            }
        }, name);
    }

    static Bitmap? GetResourceBitmap(string resourceFileName) {
        var resourcePath = Path.Combine(AppContext.BaseDirectory, "Resources", resourceFileName);
        if (!File.Exists(resourcePath)) {
            return null;
        }

        using var stream = File.OpenRead(resourcePath);
        return new Bitmap(stream);
    }

    void InvisibleTitleBar_PointerPressed(object? sender, PointerPressedEventArgs e) {
        if (!e.GetCurrentPoint(InvisibleTitleBar).Properties.IsLeftButtonPressed) {
            return;
        }

        dragStart = e.GetPosition(this);
        windowStart = Position;
        e.Pointer.Capture(InvisibleTitleBar);
    }

    void InvisibleTitleBar_PointerMoved(object? sender, PointerEventArgs e) {
        if (dragStart is null || windowStart is null || !e.GetCurrentPoint(InvisibleTitleBar).Properties.IsLeftButtonPressed) {
            return;
        }

        var current = e.GetPosition(this);
        var delta = current - dragStart.Value;
        Position = new PixelPoint(
            windowStart.Value.X + (int)Math.Round(delta.X),
            windowStart.Value.Y + (int)Math.Round(delta.Y)
        );
    }

    void InvisibleTitleBar_PointerReleased(object? sender, PointerReleasedEventArgs e) {
        dragStart = null;
        windowStart = null;
        e.Pointer.Capture(null);
    }
}
