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
        using Pen border = new(Color.FromArgb(73, 80, 91), 2);
        graphics.DrawRectangle(border, room);

        Rectangle monitor = new(
            room.Left + room.Width / 4,
            room.Top + room.Height / 4,
            room.Width / 2,
            room.Height / 3);
        using SolidBrush monitorBrush = new(Color.FromArgb(46, 54, 65));
        graphics.FillRectangle(monitorBrush, monitor);
        graphics.DrawRectangle(Pens.DimGray, monitor);
        using Font small = new("Segoe UI", 8);
        using SolidBrush textBrush = new(Color.FromArgb(178, 186, 197));
        graphics.DrawString("Main screen", small, textBrush, monitor.Left + 8, monitor.Top + 7);

        foreach (DeviceAssignment device in _devices.Where(item => item.Enabled))
        {
            Point point = ToPoint(device.RoomX, device.RoomY);
            bool selected = ReferenceEquals(device, _selected);
            int radius = selected ? 9 : 7;
            using SolidBrush fill = new(selected ? Color.FromArgb(118, 165, 235) : RoleColor(device.WatchRole));
            graphics.FillEllipse(fill, point.X - radius, point.Y - radius, radius * 2, radius * 2);
            graphics.DrawEllipse(Pens.White, point.X - radius, point.Y - radius, radius * 2, radius * 2);
            string label = device.FriendlyName.Length > 18 ? device.FriendlyName[..18] + "..." : device.FriendlyName;
            graphics.DrawString(label, small, textBrush, point.X + 10, point.Y - 8);
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        _dragging = _devices
            .Where(device => device.Enabled)
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

    private static Color RoleColor(string role) => role switch
    {
        "Soft depth" => Color.FromArgb(95, 180, 150),
        "Base glow" => Color.FromArgb(220, 165, 78),
        "Off" => Color.FromArgb(98, 102, 110),
        _ => Color.FromArgb(224, 92, 104)
    };
}
