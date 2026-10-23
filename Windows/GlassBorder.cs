using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Copilume;

public sealed class GlassBorder : Border
{
    private Point highlight = new(0.3, 0);
    private static readonly DependencyProperty HighlightOpacityProperty = DependencyProperty.Register(
        "HighlightOpacity", typeof(double), typeof(GlassBorder), new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));
    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo) { base.OnRenderSizeChanged(sizeInfo); UpdateClip(); }
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == CornerRadiusProperty) UpdateClip();
    }
    private void UpdateClip()
    {
        if (ActualWidth <= 0 || ActualHeight <= 0) return;
        // Clip descendants, not only the background painted by Border.
        Clip = new RectangleGeometry(new Rect(0, 0, ActualWidth, ActualHeight), CornerRadius.TopLeft, CornerRadius.TopLeft);
    }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (SystemParameters.HighContrast || !WindowMaterial.EffectsEnabled) return;
        var point = e.GetPosition(this);
        highlight = new Point(point.X / Math.Max(1, ActualWidth), point.Y / Math.Max(1, ActualHeight));
        BeginAnimation(HighlightOpacityProperty, null);
        SetCurrentValue(HighlightOpacityProperty, 0.09);
        InvalidateVisual();
    }
    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        if (SystemParameters.ClientAreaAnimation && !SystemParameters.HighContrast)
            BeginAnimation(HighlightOpacityProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(170)));
        else SetCurrentValue(HighlightOpacityProperty, 0.0);
    }
    protected override void OnRender(DrawingContext drawing)
    {
        base.OnRender(drawing);
        double opacity = (double)GetValue(HighlightOpacityProperty);
        if (opacity <= 0 || SystemParameters.HighContrast) return;
        var light = new RadialGradientBrush(Colors.White, Colors.Transparent) { Center = highlight, GradientOrigin = highlight, RadiusX = 0.65, RadiusY = 0.9, Opacity = opacity };
        drawing.DrawRoundedRectangle(light, null, new Rect(0, 0, ActualWidth, ActualHeight), CornerRadius.TopLeft, CornerRadius.TopLeft);
    }
}
