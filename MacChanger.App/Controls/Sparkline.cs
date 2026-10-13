using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace MacChanger.App.Controls;

/// <summary>
/// A single-series trend line: one 2px stroke with a 10% wash beneath it and a
/// dot on the newest sample.
///
/// It draws in real pixels rather than scaling a fixed coordinate space.
/// Stretching a <see cref="System.Windows.Shapes.Shape"/> to fit would scale
/// its stroke along with its geometry, so the line would come out thicker
/// along one axis than the other and the round end dot would render as an
/// oval — the same fault the HTML mockup hit with
/// <c>preserveAspectRatio="none"</c>. Rendering in <see cref="OnRender"/> with
/// the element's own <see cref="FrameworkElement.ActualWidth"/> sidesteps it
/// entirely, and costs nothing at one repaint a second.
///
/// One series, one colour. Two colours would need two hues that can be told
/// apart side by side, and BigRat's palette spends its distinct hues on status
/// — amber means a warning, red means a failure, lime is the primary action —
/// so there is no free pair, and borrowing one would quietly mislabel it.
/// </summary>
public sealed class Sparkline : FrameworkElement
{
    /// <summary>Samples to draw, oldest first.</summary>
    public static readonly DependencyProperty ValuesProperty =
        DependencyProperty.Register(
            nameof(Values),
            typeof(IReadOnlyList<double>),
            typeof(Sparkline),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>The stroke. The wash beneath it is this colour at 10%.</summary>
    public static readonly DependencyProperty LineBrushProperty =
        DependencyProperty.Register(
            nameof(LineBrush),
            typeof(Brush),
            typeof(Sparkline),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>
    /// What the end dot is ringed in, so it stays legible wherever the last
    /// sample lands. Set it to the surface the sparkline is sitting on.
    /// </summary>
    public static readonly DependencyProperty SurfaceBrushProperty =
        DependencyProperty.Register(
            nameof(SurfaceBrush),
            typeof(Brush),
            typeof(Sparkline),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public IReadOnlyList<double>? Values
    {
        get => (IReadOnlyList<double>?)GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
    }

    public Brush? LineBrush
    {
        get => (Brush?)GetValue(LineBrushProperty);
        set => SetValue(LineBrushProperty, value);
    }

    public Brush? SurfaceBrush
    {
        get => (Brush?)GetValue(SurfaceBrushProperty);
        set => SetValue(SurfaceBrushProperty, value);
    }

    static Sparkline() => FocusableProperty.OverrideMetadata(
        typeof(Sparkline), new FrameworkPropertyMetadata(false));

    // Room for the end dot at each end. Without it the dot on the newest
    // sample sits exactly on the element's edge and is sliced in half.
    const double Inset = 5;

    const double LineWidth = 2;
    const double DotRadius = 4;
    const double DotRing = 2;

    // The floor the vertical scale will not go below. A connection passing a
    // few kilobits still gets a scale in megabits, so the line it draws at
    // the bottom of the plot means "almost nothing" rather than filling the
    // box and reading as healthy.
    const double MinScaleMbps = 1.0;

    // Headroom above the tallest sample, so the line does not touch the top.
    const double Headroom = 1.15;

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        var values = Values;
        var stroke = LineBrush;
        double width = ActualWidth, height = ActualHeight;

        // Two points is the fewest a line can be drawn from, and until the
        // monitor has taken its second sample there genuinely is no trend to
        // show. Nothing is drawn rather than a flat line at zero, which would
        // say "no traffic" when what is true is "not measured yet".
        if (values is null || values.Count < 2 || stroke is null) return;
        if (width <= 0 || height <= 0) return;

        double top = DotRadius + 1;
        double bottom = height - DotRadius - 1;
        double plot = bottom - top;
        if (plot <= 0) return;

        double peak = 0;
        for (var i = 0; i < values.Count; i++)
        {
            var value = values[i];
            if (double.IsFinite(value) && value > peak) peak = value;
        }

        double scale = Math.Max(peak * Headroom, MinScaleMbps);

        double x0 = Inset;
        double x1 = Math.Max(Inset + 1, width - Inset);
        double step = (x1 - x0) / (values.Count - 1);

        double Y(int index)
        {
            var value = values[index];
            if (!double.IsFinite(value) || value <= 0) return bottom;
            return bottom - (value / scale) * plot;
        }

        var line = new StreamGeometry();
        using (var context = line.Open())
        {
            context.BeginFigure(new Point(x0, Y(0)), isFilled: false, isClosed: false);
            for (var i = 1; i < values.Count; i++)
                context.LineTo(new Point(x0 + i * step, Y(i)), isStroked: true, isSmoothJoin: false);
        }
        line.Freeze();

        var area = new StreamGeometry();
        using (var context = area.Open())
        {
            context.BeginFigure(new Point(x0, bottom), isFilled: true, isClosed: true);
            for (var i = 0; i < values.Count; i++)
                context.LineTo(new Point(x0 + i * step, Y(i)), isStroked: true, isSmoothJoin: false);
            context.LineTo(new Point(x1, bottom), isStroked: true, isSmoothJoin: false);
        }
        area.Freeze();

        var pen = new Pen(stroke, LineWidth)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round,
        };
        pen.Freeze();

        drawingContext.DrawGeometry(Wash(stroke), null, area);
        drawingContext.DrawGeometry(null, pen, line);

        var ring = SurfaceBrush;
        var dotPen = ring is null ? null : new Pen(ring, DotRing);
        dotPen?.Freeze();
        drawingContext.DrawEllipse(stroke, dotPen, new Point(x1, Y(values.Count - 1)), DotRadius, DotRadius);
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        InvalidateVisual();
    }

    /// <summary>
    /// The same hue at a tenth of the strength. Derived from the stroke rather
    /// than taken as a second property, so the wash can never drift out of
    /// step with the line it belongs to.
    /// </summary>
    static Brush Wash(Brush stroke)
    {
        if (stroke is not SolidColorBrush solid) return stroke;

        var wash = new SolidColorBrush(solid.Color) { Opacity = 0.10 };
        wash.Freeze();
        return wash;
    }
}
