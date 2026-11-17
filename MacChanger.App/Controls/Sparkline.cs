using System;
using System.Collections.Generic;
using System.Globalization;
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

    /// <summary>
    /// Gridlines and axis labels. Null draws neither, for a sparkline used
    /// without an axis.
    /// </summary>
    public static readonly DependencyProperty AxisBrushProperty =
        DependencyProperty.Register(
            nameof(AxisBrush),
            typeof(Brush),
            typeof(Sparkline),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>
    /// The axis labels. Falls back to <see cref="AxisBrush"/> when unset, so a
    /// chart that wants one colour for both still only needs to set one.
    /// </summary>
    public static readonly DependencyProperty LabelBrushProperty =
        DependencyProperty.Register(
            nameof(LabelBrush),
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

    public Brush? AxisBrush
    {
        get => (Brush?)GetValue(AxisBrushProperty);
        set => SetValue(AxisBrushProperty, value);
    }

    public Brush? LabelBrush
    {
        get => (Brush?)GetValue(LabelBrushProperty);
        set => SetValue(LabelBrushProperty, value);
    }

    static Sparkline() => FocusableProperty.OverrideMetadata(
        typeof(Sparkline), new FrameworkPropertyMetadata(false));

    // Room for the end dot at each end. Without it the dot on the newest
    // sample sits exactly on the element's edge and is sliced in half.
    const double Inset = 5;

    const double LineWidth = 2;
    const double DotRadius = 4;
    const double DotRing = 2.5;

    // The chart is drawn against a fixed log scale: 0.1 Mbps to 350 Mbps.
    // The old version scaled to its own peak, so a line's height meant
    // something different every second and two glances could not be compared.
    // A log scale keeps a trickle and a saturated link both visible, and the
    // ceiling is 350 because anything faster is not a reading this card is
    // meant to characterise — the line flattens at the top instead of
    // rescaling the whole chart around one burst.
    const double AxisMinMbps = 0.1;
    const double AxisMaxMbps = 350;
    static readonly double AxisDecades = Math.Log10(AxisMaxMbps / AxisMinMbps);

    // Space at the right for the decade labels (1000 is the widest).
    const double LabelGutter = 38;

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

        double top = 8;
        double bottom = height - 8;
        double plot = bottom - top;
        if (plot <= 0) return;

        double x0 = Inset;
        double x1 = Math.Max(Inset + 1, width - LabelGutter);
        double step = (x1 - x0) / (values.Count - 1);

        // Fixed scale: the same height is always the same speed.
        double Y(double value)
        {
            if (!double.IsFinite(value) || value <= 0) return bottom;
            var clamped = Math.Clamp(value, AxisMinMbps, AxisMaxMbps);
            var decades = Math.Log10(clamped) - Math.Log10(AxisMinMbps);
            return bottom - decades / AxisDecades * plot;
        }

        DrawAxis(drawingContext, AxisBrush, LabelBrush, x0, x1, bottom, plot);

        var line = new StreamGeometry();
        using (var context = line.Open())
        {
            context.BeginFigure(new Point(x0, Y(values[0])), isFilled: false, isClosed: false);
            for (var i = 1; i < values.Count; i++)
                context.LineTo(new Point(x0 + i * step, Y(values[i])), isStroked: true, isSmoothJoin: false);
        }
        line.Freeze();

        var area = new StreamGeometry();
        using (var context = area.Open())
        {
            context.BeginFigure(new Point(x0, bottom), isFilled: true, isClosed: true);
            for (var i = 0; i < values.Count; i++)
                context.LineTo(new Point(x0 + i * step, Y(values[i])), isStroked: true, isSmoothJoin: false);
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
        drawingContext.DrawEllipse(stroke, dotPen, new Point(x1, Y(values[values.Count - 1])), DotRadius, DotRadius);
    }

    /// <summary>
    /// Draws the decade gridlines and their labels. The lowest decade (0.1) is
    /// a gridline without a label: it keeps the plot's floor honest without
    /// adding a figure nobody reads. The ceiling is 350, so the topmost
    /// labelled line is 100 and the final fifteen percent of the plot is the
    /// headroom above it.
    /// </summary>
    void DrawAxis(DrawingContext drawingContext, Brush? axis, Brush? labels, double x0, double x1, double bottom, double plot)
    {
        if (axis is null) return;

        var pen = new Pen(axis, 1);
        pen.Freeze();

        var labelBrush = labels ?? axis;
        var typeface = new Typeface("Segoe UI");
        var pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;

        for (var value = AxisMinMbps; value <= AxisMaxMbps + 0.001; value *= 10)
        {
            var decades = Math.Log10(value) - Math.Log10(AxisMinMbps);
            var y = bottom - decades / AxisDecades * plot;

            drawingContext.DrawLine(pen, new Point(x0, y), new Point(x1, y));

            if (value < 1) continue;

            var label = value.ToString("0", CultureInfo.InvariantCulture);
            var text = new FormattedText(
                label,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                typeface,
                12,
                labelBrush,
                pixelsPerDip);
            drawingContext.DrawText(text, new Point(x1 + 7, y - text.Height / 2));
        }
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
