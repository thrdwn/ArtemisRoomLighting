using System.ComponentModel;

namespace ArtemisRoomLighting.Installer;

internal sealed class RoomMapControl : Control
{
    private IReadOnlyList<DeviceAssignment> _devices = [];
    private DeviceAssignment? _selected;
    private DeviceAssignment? _dragging;

    public RoomMapControl()
    {
        DoubleBuffered = true;
        BackColor = Color.FromArgb(24, 27, 32);
        Cursor = Cursors.Cross;
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IReadOnlyList<DeviceAssignment> Devices
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
    public DeviceAssignment? SelectedDevice
    {
        get => _selected;
        set
        {
            _selected = value;
            Invalidate();
        }
    }

    public event EventHandler<DeviceAssignment?>? SelectedDeviceChanged;
    public event EventHandler? DeviceMoved;

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        Graphics graphics = e.Graphics;
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        Rectangle room = new(14, 14, Math.Max(1, Width - 28), Math.Max(1, Height - 28));
        using SolidBrush roomBrush = new(Color.FromArgb(28, 32, 38));
        graphics.FillRectangle(roomBrush, room);
        using Pen border = new(Color.FromArgb(78, 88, 103), 2);
        graphics.DrawRectangle(border, room);

        Rectangle monitor = new(
            room.Left + room.Width / 4,
            room.Top + room.Height / 5,
            room.Width / 2,
            room.Height / 4);
        Rectangle desk = new(
            room.Left + room.Width / 5,
            monitor.Bottom + room.Height / 14,
            room.Width * 3 / 5,
            room.Height / 7);
        Rectangle rear = new(
            room.Left + 8,
            room.Bottom - room.Height / 5,
            room.Width - 16,
            room.Height / 5 - 8);
        using SolidBrush rearBrush = new(Color.FromArgb(35, 42, 48));
        using SolidBrush sideBrush = new(Color.FromArgb(31, 37, 45));
        using SolidBrush deskBrush = new(Color.FromArgb(60, 48, 36));
        graphics.FillRectangle(sideBrush, room.Left + 8, room.Top + 8, room.Width / 7, room.Height - 16);
        graphics.FillRectangle(sideBrush, room.Right - room.Width / 7 - 8, room.Top + 8, room.Width / 7, room.Height - 16);
        graphics.FillRectangle(rearBrush, rear);
        graphics.FillRectangle(deskBrush, desk);
        using SolidBrush monitorBrush = new(Color.FromArgb(46, 54, 65));
        graphics.FillRectangle(monitorBrush, monitor);
        graphics.DrawRectangle(Pens.DimGray, monitor);
        using Font small = new("Segoe UI", 8);
        using SolidBrush textBrush = new(Color.FromArgb(178, 186, 197));
        graphics.DrawString("Main screen", small, textBrush, monitor.Left + 8, monitor.Top + 7);
        graphics.DrawString("Desk", small, textBrush, desk.Left + 8, desk.Top + 7);
        graphics.DrawString("Rear room", small, textBrush, rear.Left + 8, rear.Top + 7);
        graphics.DrawString("Left", small, textBrush, room.Left + 14, room.Top + 12);
        graphics.DrawString("Right", small, textBrush, room.Right - 52, room.Top + 12);

        foreach (DeviceAssignment device in _devices)
        {
            Point point = ToPoint(device.RoomX, device.RoomY);
            bool selected = ReferenceEquals(device, _selected);
            int radius = selected ? 9 : 7;
            using SolidBrush fill = new(selected
                ? Color.FromArgb(118, 165, 235)
                : device.Enabled ? KindColor(device.DeviceKind) : Color.FromArgb(88, 92, 100));
            graphics.FillEllipse(fill, point.X - radius, point.Y - radius, radius * 2, radius * 2);
            graphics.DrawEllipse(Pens.White, point.X - radius, point.Y - radius, radius * 2, radius * 2);
            string label = device.FriendlyName.Length > 18 ? device.FriendlyName[..18] + "..." : device.FriendlyName;
            using SolidBrush labelBrush = new(device.Enabled ? Color.FromArgb(214, 221, 232) : Color.FromArgb(138, 144, 154));
            graphics.DrawString(label, small, labelBrush, point.X + 10, point.Y - 8);
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        _dragging = _devices
            .OrderBy(device => Distance(ToPoint(device.RoomX, device.RoomY), e.Location))
            .FirstOrDefault(device => Distance(ToPoint(device.RoomX, device.RoomY), e.Location) <= 22);
        _selected = _dragging;
        SelectedDeviceChanged?.Invoke(this, _selected);
        Invalidate();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_dragging == null || e.Button != MouseButtons.Left)
            return;

        Rectangle room = new(14, 14, Math.Max(1, Width - 28), Math.Max(1, Height - 28));
        _dragging.RoomX = Math.Clamp((e.X - room.Left) / (double)room.Width, 0, 1);
        _dragging.RoomY = Math.Clamp((e.Y - room.Top) / (double)room.Height, 0, 1);
        _dragging.PhysicalZone = ZoneFromPosition(_dragging.RoomX, _dragging.RoomY);
        _dragging.Placement = SetupLabels.PlacementFromZone(_dragging.PhysicalZone);
        DeviceMoved?.Invoke(this, EventArgs.Empty);
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        _dragging = null;
    }

    private Point ToPoint(double x, double y)
    {
        Rectangle room = new(14, 14, Math.Max(1, Width - 28), Math.Max(1, Height - 28));
        return new Point(
            room.Left + (int)Math.Round(x * room.Width),
            room.Top + (int)Math.Round(y * room.Height));
    }

    private static double Distance(Point a, Point b)
    {
        int x = a.X - b.X;
        int y = a.Y - b.Y;
        return Math.Sqrt(x * x + y * y);
    }

    private static string ZoneFromPosition(double x, double y)
    {
        if (y < 0.22 && x is > 0.28 and < 0.72)
            return "Screen top";
        if (y is > 0.48 and < 0.68 && x is > 0.25 and < 0.75)
            return "Desk";
        if (y > 0.78)
            return x < 0.5 ? "Rear left" : "Rear right";
        if (x < 0.22)
            return "Left";
        if (x > 0.78)
            return "Right";
        if (y > 0.58)
            return "Screen bottom";
        return "Room";
    }

    private static Color KindColor(string kind) => kind switch
    {
        "Light" => Color.FromArgb(250, 194, 95),
        "Keyboard" => Color.FromArgb(118, 184, 255),
        "Mouse" => Color.FromArgb(194, 120, 235),
        "Dock" => Color.FromArgb(120, 218, 178),
        _ => Color.FromArgb(178, 186, 197)
    };
}
