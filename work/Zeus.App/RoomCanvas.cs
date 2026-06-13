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
        BackColor = Color.FromArgb(21, 25, 29);
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
        using SolidBrush roomBrush = new(Color.FromArgb(29, 34, 39));
        using Pen border = new(Color.FromArgb(76, 88, 99), 2);
        g.FillRectangle(roomBrush, room);
        g.DrawRectangle(border, room);

        Rectangle monitor = new(room.Left + room.Width / 4, room.Top + 48, room.Width / 2, room.Height / 4);
        Rectangle desk = new(room.Left + room.Width / 5, monitor.Bottom + 34, room.Width * 3 / 5, 64);
        Rectangle rear = new(room.Left + 14, room.Bottom - 104, room.Width - 28, 88);
        Rectangle left = new(room.Left + 14, room.Top + 18, 82, room.Height - 36);
        Rectangle right = new(room.Right - 96, room.Top + 18, 82, room.Height - 36);

        FillRound(g, left, Color.FromArgb(34, 45, 50), 10);
        FillRound(g, right, Color.FromArgb(43, 39, 53), 10);
        FillRound(g, rear, Color.FromArgb(39, 45, 49), 10);
        FillRound(g, desk, Color.FromArgb(67, 52, 40), 10);
        FillRound(g, monitor, Color.FromArgb(52, 62, 72), 8);

        using Font labelFont = new("Segoe UI Semibold", 8.5f);
        using SolidBrush labelBrush = new(Color.FromArgb(192, 202, 214));
        g.DrawString("Main monitor", labelFont, labelBrush, monitor.Left + 14, monitor.Top + 12);
        g.DrawString("Desk", labelFont, labelBrush, desk.Left + 14, desk.Top + 12);
        g.DrawString("Rear wall / room lights", labelFont, labelBrush, rear.Left + 14, rear.Top + 12);
        g.DrawString("Left", labelFont, labelBrush, left.Left + 12, left.Top + 12);
        g.DrawString("Right", labelFont, labelBrush, right.Left + 10, right.Top + 12);

        using Pen zonePen = new(Color.FromArgb(82, 98, 110), 1) { DashStyle = DashStyle.Dash };
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

        string label = device.Name.Length > 20 ? device.Name[..20] + "..." : device.Name;
        using Font font = new("Segoe UI", 8.3f);
        using SolidBrush text = new(device.Enabled ? Color.FromArgb(232, 237, 242) : Color.FromArgb(145, 153, 162));
        using SolidBrush bg = new(Color.FromArgb(140, 18, 21, 25));
        SizeF size = g.MeasureString(label, font);
        RectangleF labelBox = new(point.X + 13, point.Y - 12, size.Width + 8, size.Height + 4);
        g.FillRectangle(bg, labelBox);
        g.DrawString(label, font, text, point.X + 17, point.Y - 10);
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
        "Room light" => Color.FromArgb(250, 198, 92),
        "Keyboard" => Color.FromArgb(102, 183, 255),
        "Mouse" => Color.FromArgb(202, 126, 230),
        "Dock" => Color.FromArgb(108, 218, 173),
        "Laptop" => Color.FromArgb(242, 132, 118),
        "Strip" => Color.FromArgb(120, 224, 210),
        _ => Color.FromArgb(178, 188, 198)
    };

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
}
