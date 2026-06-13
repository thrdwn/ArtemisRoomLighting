namespace ZeusLighting;

internal sealed class ZeusDevice
{
    public string Name { get; set; } = "";
    public string Kind { get; set; } = "";
    public string Backend { get; set; } = "";
    public string Zone { get; set; } = "";
    public string WatchRole { get; set; } = "";
    public string GameRole { get; set; } = "";
    public int Brightness { get; set; } = 100;
    public int FpsCap { get; set; } = 30;
    public bool Enabled { get; set; } = true;
    public float X { get; set; }
    public float Y { get; set; }
}

internal sealed record ZeusProfile(
    string Name,
    string Subtitle,
    string Trigger,
    string PrimaryDevices,
    string RoomBehavior,
    string Performance);

internal sealed record ZeusBackend(
    string Name,
    string Status,
    string Purpose,
    string Examples,
    bool Recommended);
