using System;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using AvaloniaColor = Avalonia.Media.Color;
using AvaloniaFontStyle = Avalonia.Media.FontStyle;
using OrientationMode = Avalonia.Layout.Orientation;

namespace Edda.Avalonia.Windows;

public sealed class RecentMapListItem : ListBoxItem {
    readonly Action primaryAction;
    readonly Action secondaryAction;

    public TextBlock MapNameTextBlock { get; }
    public TextBlock MapPathTextBlock { get; }
    public string MapPath => MapPathTextBlock.Text ?? string.Empty;

    public RecentMapListItem(string name, string path, Action primaryAction, Action secondaryAction) {
        this.primaryAction = primaryAction;
        this.secondaryAction = secondaryAction;
        AutomationProperties.SetName(this, $"{name} {path}".Trim());

        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        Background = new SolidColorBrush(AvaloniaColor.Parse("#11FFFFFF"));
        Margin = new Thickness(0, 0, 0, 8);
        Padding = new Thickness(10, 8);

        var root = new StackPanel {
            Orientation = OrientationMode.Vertical,
            Spacing = 2
        };

        MapNameTextBlock = new TextBlock {
            Text = string.IsNullOrWhiteSpace(name) ? "Untitled Map" : name,
            FontWeight = FontWeight.Bold,
            FontStyle = string.IsNullOrWhiteSpace(name) ? AvaloniaFontStyle.Italic : AvaloniaFontStyle.Normal,
            Foreground = new SolidColorBrush(AvaloniaColor.Parse("#002668"))
        };

        MapPathTextBlock = new TextBlock {
            Text = path,
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap
        };

        root.Children.Add(MapNameTextBlock);
        root.Children.Add(MapPathTextBlock);
        Content = root;

        PointerReleased += OnPointerReleased;
    }

    public void InvokePrimaryAction() {
        primaryAction();
    }

    public void InvokeSecondaryAction() {
        secondaryAction();
    }

    void OnPointerReleased(object? sender, PointerReleasedEventArgs e) {
        switch (e.InitialPressMouseButton) {
            case MouseButton.Left:
                InvokePrimaryAction();
                break;
            case MouseButton.Right:
                InvokeSecondaryAction();
                break;
        }
    }
}
