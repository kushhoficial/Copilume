using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shell;

namespace Copilume;

// Shared native window frame for preferences, preview and project information.
internal sealed class SurfaceWindow : Window
{
    internal ContentControl Body { get; } = new();
    private readonly Border surface;
    internal SurfaceWindow(string title)
    {
        Title = title;
        Style = (Style)Application.Current.FindResource(typeof(Window));
        WindowStyle = WindowStyle.None;
        Background = Brushes.Transparent;
        MinWidth = 420; MinHeight = 350;
        WindowChrome.SetWindowChrome(this, new WindowChrome { CaptionHeight = 0, ResizeBorderThickness = new Thickness(6), CornerRadius = new CornerRadius(24), GlassFrameThickness = new Thickness(0), UseAeroCaptionButtons = false });
        var layout = new Grid();
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition());
        var header = new Grid { Background = Brushes.Transparent, Margin = new Thickness(24, 18, 16, 8) };
        var heading = new TextBlock { Text = title, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center };
        heading.SetResourceReference(TextBlock.ForegroundProperty, "Muted");
        header.Children.Add(heading);
        var close = new Button { Style = (Style)FindResource("ToolButton"), Width = 28, Height = 28, Padding = new Thickness(7), HorizontalAlignment = HorizontalAlignment.Right, Background = Brushes.Transparent, ToolTip = "Fechar" };
        System.Windows.Automation.AutomationProperties.SetName(close, "Fechar " + title);
        close.Content = new System.Windows.Shapes.Path { Style = (Style)FindResource("Glyph"), Data = (Geometry)FindResource("CloseIcon"), Width = 12, Height = 12 };
        close.Click += (_, _) => Close(); header.Children.Add(close);
        header.MouseLeftButtonDown += (_, e) =>
        {
            if (e.LeftButton != MouseButtonState.Pressed) return;
            for (DependencyObject? node = e.OriginalSource as DependencyObject; node != null; node = node is Visual ? VisualTreeHelper.GetParent(node) : LogicalTreeHelper.GetParent(node))
                if (node is Button) return;
            DragMove();
        };
        layout.Children.Add(header); Grid.SetRow(Body, 1); layout.Children.Add(Body);
        surface = new Border { CornerRadius = new CornerRadius(24), BorderThickness = new Thickness(1), Child = layout };
        surface.SetResourceReference(Border.BackgroundProperty, "Surface"); surface.SetResourceReference(Border.BorderBrushProperty, "Hairline");
        Content = surface;
        SourceInitialized += (_, _) => RefreshMaterial();
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) { Close(); e.Handled = true; } };
    }
    internal void RefreshMaterial() => surface.SetResourceReference(Border.BackgroundProperty, WindowMaterial.Apply(this, Theme.IsDark) ? "WindowTint" : "Surface");
    internal void SolidPreview() => surface.SetResourceReference(Border.BackgroundProperty, "Surface");
}
