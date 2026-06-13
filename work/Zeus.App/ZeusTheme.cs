using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace ZeusLighting;

internal static class ZeusTheme
{
    public static readonly Color Background = FromHex("#101317");
    public static readonly Color Surface = FromHex("#151A20");
    public static readonly Color SurfaceRaised = FromHex("#1D232A");
    public static readonly Color SurfaceInteractive = FromHex("#25313B");
    public static readonly Color Border = FromHex("#40505E");
    public static readonly Color Text = FromHex("#EEF2F6");
    public static readonly Color TextMuted = FromHex("#A8B4C1");
    public static readonly Color TextInverse = FromHex("#071014");
    public static readonly Color Action = FromHex("#3E91C9");
    public static readonly Color ActionHover = FromHex("#56A9DE");
    public static readonly Color Success = FromHex("#7ED9A5");
    public static readonly Color Warning = FromHex("#E6BC70");
    public static readonly Color Danger = FromHex("#F06F61");
    public static readonly Color Fire = FromHex("#F2743D");
    public static readonly Color Amber = FromHex("#F7C95C");
    public static readonly Color Teal = FromHex("#67DCCB");
    public static readonly Color Purple = FromHex("#B57AE6");
    public static readonly Color Blue = FromHex("#66B7FF");

    public static Font DisplayFont() => new("Segoe UI Semibold", 24f);
    public static Font TitleFont(float size = 14f) => new("Segoe UI Semibold", size);
    public static Font BodyFont(float size = 9.5f) => new("Segoe UI", size);
    public static Font LabelFont(float size = 9.5f) => new("Segoe UI Semibold", size);

    public static Color FromHex(string value) => ColorTranslator.FromHtml(value);

    public static GraphicsPath RoundedPath(Rectangle rect, int radius)
    {
        int diameter = radius * 2;
        GraphicsPath path = new();
        path.AddArc(rect.Left, rect.Top, diameter, diameter, 180, 90);
        path.AddArc(rect.Right - diameter, rect.Top, diameter, diameter, 270, 90);
        path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rect.Left, rect.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}

internal sealed class ZeusCard : Panel
{
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color FillColor { get; set; } = ZeusTheme.SurfaceRaised;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color StrokeColor { get; set; } = Color.FromArgb(78, 91, 105);

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int CornerRadius { get; set; } = 8;

    public ZeusCard()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor | ControlStyles.UserPaint, true);
        BackColor = Color.Transparent;
        Padding = new Padding(1);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        Rectangle rect = new(0, 0, Width - 1, Height - 1);
        using GraphicsPath path = ZeusTheme.RoundedPath(rect, CornerRadius);
        using SolidBrush brush = new(FillColor);
        using Pen pen = new(StrokeColor);
        e.Graphics.FillPath(brush, path);
        e.Graphics.DrawPath(pen, path);
    }
}

internal sealed class ZeusButton : Button
{
    private bool _hovered;
    private bool _pressed;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color FillColor { get; set; } = ZeusTheme.SurfaceInteractive;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color HoverColor { get; set; } = Color.FromArgb(52, 65, 78);

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color PressedColor { get; set; } = Color.FromArgb(45, 55, 66);

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color StrokeColor { get; set; } = ZeusTheme.Border;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color ActiveFillColor { get; set; } = ZeusTheme.Action;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool Active { get; set; }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int CornerRadius { get; set; } = 8;

    public ZeusButton()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        UseVisualStyleBackColor = false;
        Cursor = Cursors.Hand;
        Font = ZeusTheme.LabelFont();
        ForeColor = ZeusTheme.Text;
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        _hovered = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hovered = false;
        _pressed = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnMouseDown(MouseEventArgs mevent)
    {
        _pressed = true;
        Invalidate();
        base.OnMouseDown(mevent);
    }

    protected override void OnMouseUp(MouseEventArgs mevent)
    {
        _pressed = false;
        Invalidate();
        base.OnMouseUp(mevent);
    }

    protected override void OnPaint(PaintEventArgs pevent)
    {
        pevent.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        Rectangle rect = new(0, 0, Width - 1, Height - 1);
        Color fill = Active ? ActiveFillColor : _pressed ? PressedColor : _hovered ? HoverColor : FillColor;
        Color text = Active ? ZeusTheme.TextInverse : ForeColor;

        using GraphicsPath path = ZeusTheme.RoundedPath(rect, CornerRadius);
        using SolidBrush brush = new(fill);
        using Pen pen = new(Active ? ZeusTheme.ActionHover : StrokeColor);
        pevent.Graphics.FillPath(brush, path);
        pevent.Graphics.DrawPath(pen, path);

        Rectangle textRect = Rectangle.Inflate(rect, -12, 0);
        TextFormatFlags alignment = TextAlign switch
        {
            ContentAlignment.MiddleLeft => TextFormatFlags.Left,
            ContentAlignment.MiddleRight => TextFormatFlags.Right,
            _ => TextFormatFlags.HorizontalCenter
        };
        TextRenderer.DrawText(
            pevent.Graphics,
            Text,
            Font,
            textRect,
            Enabled ? text : Color.FromArgb(120, 130, 140),
            alignment | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }
}

internal sealed class ZeusChip : Label
{
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color FillColor { get; set; } = ZeusTheme.SurfaceInteractive;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color StrokeColor { get; set; } = Color.FromArgb(80, 94, 108);

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int CornerRadius { get; set; } = 999;

    public ZeusChip()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor | ControlStyles.UserPaint, true);
        Font = ZeusTheme.LabelFont(8.8f);
        ForeColor = ZeusTheme.Text;
        TextAlign = ContentAlignment.MiddleCenter;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        Rectangle rect = new(0, 0, Width - 1, Height - 1);
        int radius = Math.Min(CornerRadius, Math.Min(Height / 2, 999));
        using GraphicsPath path = ZeusTheme.RoundedPath(rect, radius);
        using SolidBrush brush = new(FillColor);
        using Pen pen = new(StrokeColor);
        e.Graphics.FillPath(brush, path);
        e.Graphics.DrawPath(pen, path);
        TextRenderer.DrawText(
            e.Graphics,
            Text,
            Font,
            rect,
            ForeColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }
}
