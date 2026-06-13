using System.Drawing.Drawing2D;
using System.ComponentModel;

namespace ZeusLighting;

internal sealed class RoomCanvas : Control
{
    private IReadOnlyList<ZeusDevice> _devices = [];
    private ZeusDevice? _selected;
    private ZeusDevice? _dragging;

    public RoomCanvas()
    {
        DoubleBuffered = true;
        BackColor = ZeusTheme.SurfaceRaised;
        Cursor = Cursors.Hand;
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IReadOnlyList<ZeusDevice> Devices
    {
        get => _devices;
        set
        {
            _devices = value;
            Invalidate();
        }
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public ZeusDevice? SelectedDevice
    {
        get => _selected;
        set
        {
            _selected = value;
            Invalidate();
        }
    }

    public event EventHandler<ZeusDevice?>? SelectedDeviceChanged;
    public event EventHandler? DeviceMoved;

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        Rectangle room = RoomBounds();
        FillRound(g, room, Color.FromArgb(22, 27, 32), 12);
        DrawRound(g, room, Color.FromArgb(75, 91, 106), 12, 2);

        Rectangle ceiling = new(room.Left + 104, room.Top + 16, room.Width - 208, 34);
        Rectangle monitor = new(room.Left + room.Width / 4, room.Top + 64, room.Width / 2, Math.Max(78, room.Height / 4));
        Rectangle desk = new(room.Left + room.Width / 5, monitor.Bottom + 28, room.Width * 3 / 5, 58);
        Rectangle rear = new(room.Left + 16, room.Bottom - 86, room.Width - 32, 70);
        Rectangle left = new(room.Left + 16, room.Top + 24, 76, room.Height - 126);
        Rectangle right = new(room.Right - 92, room.Top + 24, 76, room.Height - 126);

        FillRound(g, ceiling, Color.FromArgb(40, 48, 53), 8);
        FillRound(g, left, Color.FromArgb(34, 48, 50), 10);
        FillRound(g, right, Color.FromArgb(45, 39, 55), 10);
        FillRound(g, rear, Color.FromArgb(39, 48, 50), 10);
        FillRound(g, desk, Color.FromArgb(68, 54, 41), 10);
        FillRound(g, monitor, Color.FromArgb(49, 60, 70), 8);
        DrawRound(g, monitor, ZeusTheme.Blue, 8, 1.2f);

        using Font labelFont = new("Segoe UI Semibold", 8.5f);
        using SolidBrush labelBrush = new(ZeusTheme.TextMuted);
        g.DrawString("Ceiling", labelFont, labelBrush, ceiling.Left + 14, ceiling.Top + 9);
        g.DrawString("Main monitor", labelFont, labelBrush, monitor.Left + 14, monitor.Top + 12);
        g.DrawString("Desk", labelFont, labelBrush, desk.Left + 14, desk.Top + 12);
        g.DrawString("Rear wall", labelFont, labelBrush, rear.Left + 14, rear.Top + 12);
        g.DrawString("Left", labelFont, labelBrush, left.Left + 12, left.Top + 12);
        g.DrawString("Right", labelFont, labelBrush, right.Left + 10, right.Top + 12);

        using Pen zonePen = new(Color.FromArgb(92, 111, 125), 1) { DashStyle = DashStyle.Dash };
        g.DrawLine(zonePen, monitor.Left, monitor.Top - 18, monitor.Right, monitor.Top - 18);
        g.DrawLine(zonePen, monitor.Left, monitor.Bottom + 18, monitor.Right, monitor.Bottom + 18);

        foreach (ZeusDevice device in _devices)
            DrawDevice(g, device);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        _dragging = _devices
            .OrderBy(device => Distance(ToPoint(device), e.Location))
            .FirstOrDefault(device => Distance(ToPoint(device), e.Location) <= 24);
        _selected = _dragging;
        SelectedDeviceChanged?.Invoke(this, _selected);
        Invalidate();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_dragging == null || e.Button != MouseButtons.Left)
            return;

        Rectangle room = RoomBounds();
        _dragging.X = Math.Clamp((e.X - room.Left) / (float)room.Width, 0, 1);
        _dragging.Y = Math.Clamp((e.Y - room.Top) / (float)room.Height, 0, 1);
        _dragging.Zone = GuessZone(_dragging.X, _dragging.Y);
        DeviceMoved?.Invoke(this, EventArgs.Empty);
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        _dragging = null;
    }

    private void DrawDevice(Graphics g, ZeusDevice device)
    {
        Point point = ToPoint(device);
        bool selected = ReferenceEquals(device, _selected);
        int radius = selected ? 11 : 9;
        Color fill = device.Enabled ? DeviceColor(device.Kind) : Color.FromArgb(88, 94, 101);
        using SolidBrush brush = new(fill);
        using Pen ring = new(selected ? Color.White : Color.FromArgb(210, 218, 226), selected ? 2.4f : 1.3f);
        g.FillEllipse(brush, point.X - radius, point.Y - radius, radius * 2, radius * 2);
        g.DrawEllipse(ring, point.X - radius, point.Y - radius, radius * 2, radius * 2);

        string initials = DeviceInitials(device.Name);
        using Font initialsFont = new("Segoe UI Semibold", 6.5f);
        TextRenderer.DrawText(
            g,
            initials,
            initialsFont,
            new Rectangle(point.X - radius, point.Y - radius, radius * 2, radius * 2),
            ZeusTheme.TextInverse,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

        if (!selected)
            return;

        string label = device.Name.Length > 20 ? device.Name[..20] + "..." : device.Name;
        using Font font = new("Segoe UI", 8.3f);
        using SolidBrush text = new(device.Enabled ? ZeusTheme.Text : Color.FromArgb(145, 153, 162));
        SizeF size = g.MeasureString(label, font);
        float labelX = point.X + 14;
        if (labelX + size.Width + 14 > Width - 8)
            labelX = point.X - size.Width - 22;
        labelX = Math.Max(8, labelX);
        RectangleF labelBox = new(labelX, point.Y - 13, size.Width + 12, size.Height + 6);
        FillRound(g, Rectangle.Round(labelBox), Color.FromArgb(218, 18, 22, 27), 6);
        g.DrawString(label, font, text, labelX + 6, point.Y - 10);
    }

    private Point ToPoint(ZeusDevice device)
    {
        Rectangle room = RoomBounds();
        return new Point(
            room.Left + (int)Math.Round(device.X * room.Width),
            room.Top + (int)Math.Round(device.Y * room.Height));
    }

    private Rectangle RoomBounds() => new(12, 12, Math.Max(1, Width - 24), Math.Max(1, Height - 24));

    private static double Distance(Point a, Point b)
    {
        int x = a.X - b.X;
        int y = a.Y - b.Y;
        return Math.Sqrt(x * x + y * y);
    }

    private static string GuessZone(float x, float y)
    {
        if (y < 0.30f && x is > 0.25f and < 0.75f)
            return "Screen top";
        if (y > 0.78f)
            return x < 0.5f ? "Rear left" : "Rear right";
        if (y > 0.50f && x is > 0.25f and < 0.75f)
            return "Desk";
        if (x < 0.22f)
            return "Left wall";
        if (x > 0.78f)
            return "Right wall";
        return "Room";
    }

    private static Color DeviceColor(string kind) => kind switch
    {
        "Room light" => ZeusTheme.Amber,
        "Keyboard" => ZeusTheme.Blue,
        "Mouse" => ZeusTheme.Purple,
        "Dock" => ZeusTheme.Success,
        "Laptop" => ZeusTheme.Fire,
        "Strip" => ZeusTheme.Teal,
        _ => ZeusTheme.TextMuted
    };

    private static string DeviceInitials(string name)
    {
        string[] parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
            return "?";
        if (parts.Length == 1)
            return parts[0].Length <= 2 ? parts[0].ToUpperInvariant() : parts[0][..2].ToUpperInvariant();
        return string.Concat(parts.Take(2).Select(part => char.ToUpperInvariant(part[0])));
    }

    private static void FillRound(Graphics g, Rectangle rect, Color color, int radius)
    {
        using GraphicsPath path = new();
        int diameter = radius * 2;
        path.AddArc(rect.Left, rect.Top, diameter, diameter, 180, 90);
        path.AddArc(rect.Right - diameter, rect.Top, diameter, diameter, 270, 90);
        path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rect.Left, rect.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        using SolidBrush brush = new(color);
        g.FillPath(brush, path);
    }

    private static void DrawRound(Graphics g, Rectangle rect, Color color, int radius, float width)
    {
        using GraphicsPath path = new();
        int diameter = radius * 2;
        path.AddArc(rect.Left, rect.Top, diameter, diameter, 180, 90);
        path.AddArc(rect.Right - diameter, rect.Top, diameter, diameter, 270, 90);
        path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rect.Left, rect.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        using Pen pen = new(color, width);
        g.DrawPath(pen, path);
    }
}
