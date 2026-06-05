using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using System;
using System.IO;
using AvaloniaColor = Avalonia.Media.Color;
using AvaloniaFontStyle = Avalonia.Media.FontStyle;
using OrientationMode = Avalonia.Layout.Orientation;

namespace Edda.Avalonia.Windows;

public sealed class RecentMapListItem : ListBoxItem {
    static readonly IBrush NormalBackground = new SolidColorBrush(AvaloniaColor.Parse("#11FFFFFF"));
    static readonly IBrush HoverBackground = new SolidColorBrush(AvaloniaColor.Parse("#55FFFFFF"));
    static readonly IBrush PressedBackground = new SolidColorBrush(AvaloniaColor.Parse("#88D7E7FF"));
    static readonly IBrush ItemBorderBrush = new SolidColorBrush(AvaloniaColor.Parse("#8BA0C6"));

    readonly Action primaryAction;
    readonly Action secondaryAction;
    readonly Border chrome;
    bool isPointerOverItem;
    bool isPointerPressed;

    public TextBlock MapNameTextBlock { get; }
    public TextBlock MapPathTextBlock { get; }
    public string MapPath => MapPathTextBlock.Text ?? string.Empty;

    public RecentMapListItem(string name, string path, Action primaryAction, Action secondaryAction) {
        this.primaryAction = primaryAction;
        this.secondaryAction = secondaryAction;
        AutomationProperties.SetName(this, $"{name} {path}".Trim());

        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        Background = Brushes.Transparent;
        Margin = new Thickness(0, 0, 0, 8);
        Padding = new Thickness(0);
        Cursor = new Cursor(StandardCursorType.Hand);

        chrome = new Border {
            Background = NormalBackground,
            BorderBrush = ItemBorderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(2),
            Padding = new Thickness(10, 8)
        };

        var row = new StackPanel {
            Orientation = OrientationMode.Horizontal,
            Spacing = 10
        };

        row.Children.Add(new Image {
            Source = GetResourceBitmap("blankMap.png"),
            Width = 22,
            Height = 22,
            Stretch = Stretch.Uniform,
            VerticalAlignment = VerticalAlignment.Top
        });

        var textColumn = new StackPanel {
            Orientation = OrientationMode.Vertical,
            Spacing = 2,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        MapNameTextBlock = new TextBlock {
            Text = string.IsNullOrWhiteSpace(name) ? "Untitled Map" : name,
            FontWeight = FontWeight.Bold,
            FontStyle = string.IsNullOrWhiteSpace(name) ? AvaloniaFontStyle.Italic : AvaloniaFontStyle.Normal,
            Foreground = new SolidColorBrush(AvaloniaColor.Parse("#002668")),
            FontSize = 14,
            FontFamily = new FontFamily("Bahnschrift"),
            TextTrimming = TextTrimming.CharacterEllipsis
        };

        MapPathTextBlock = new TextBlock {
            Text = path,
            FontSize = 11,
            FontFamily = new FontFamily("Bahnschrift SemiLight"),
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextWrapping = TextWrapping.NoWrap
        };

        textColumn.Children.Add(MapNameTextBlock);
        textColumn.Children.Add(MapPathTextBlock);
        row.Children.Add(textColumn);
        chrome.Child = row;
        Content = chrome;

        PointerEntered += OnPointerEntered;
        PointerExited += OnPointerExited;
        PointerPressed += OnPointerPressed;
        PointerReleased += OnPointerReleased;
    }

    public void InvokePrimaryAction() {
        primaryAction();
    }

    public void InvokeSecondaryAction() {
        secondaryAction();
    }

    void OnPointerEntered(object? sender, PointerEventArgs e) {
        isPointerOverItem = true;
        UpdateVisualState();
    }

    void OnPointerExited(object? sender, PointerEventArgs e) {
        isPointerOverItem = false;
        isPointerPressed = false;
        UpdateVisualState();
    }

    void OnPointerPressed(object? sender, PointerPressedEventArgs e) {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed || e.GetCurrentPoint(this).Properties.IsRightButtonPressed) {
            isPointerPressed = true;
            UpdateVisualState();
        }
    }

    void OnPointerReleased(object? sender, PointerReleasedEventArgs e) {
        var releasePoint = e.GetPosition(this);
        var releasedInsideItem = releasePoint.X >= 0 &&
                                 releasePoint.Y >= 0 &&
                                 releasePoint.X <= Bounds.Width &&
                                 releasePoint.Y <= Bounds.Height;
        isPointerPressed = false;
        UpdateVisualState();
        if (!releasedInsideItem) {
            return;
        }

        switch (e.InitialPressMouseButton) {
            case MouseButton.Left:
                IsSelected = false;
                e.Handled = true;
                Dispatcher.UIThread.Post(InvokePrimaryAction, DispatcherPriority.Background);
                break;
            case MouseButton.Right:
                e.Handled = true;
                Dispatcher.UIThread.Post(InvokeSecondaryAction, DispatcherPriority.Background);
                break;
        }
    }

    void UpdateVisualState() {
        chrome.Background = isPointerPressed
            ? PressedBackground
            : isPointerOverItem
                ? HoverBackground
                : NormalBackground;
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