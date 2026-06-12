using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace ArtemisRoomLighting.Installer;

internal sealed class SetupConfiguration
{
    public int Version { get; set; } = 1;
    public int DisplayWidth { get; set; } = 1920;
    public int DisplayHeight { get; set; } = 1080;
    public string DisplayName { get; set; } = @"\\.\DISPLAY1";
    public bool BlackoutOnBlack { get; set; } = true;
    public int WatchFps { get; set; } = 30;
    public bool InstallCs2Gsi { get; set; }
    public bool CreateShortcuts { get; set; } = true;
    public bool UseDirectBridge { get; set; }
    public string StudyIp { get; set; } = "";
    public string UpperIp { get; set; } = "";
    public string LowerIp { get; set; } = "";
    public List<DeviceAssignment> Devices { get; set; } = [];
}

internal sealed class DeviceAssignment : INotifyPropertyChanged
{
    private bool _enabled;
    private string _friendlyName = "";
    private string _placement = "Desk";
    private string _watchRole = "Screen sample";
    private string _gameRole = "Full game";
    private int _intensity = 100;
    private double _roomX = 0.5;
    private double _roomY = 0.5;

    public string DeviceId { get; set; } = "";
    public string ProviderGuid { get; set; } = "";
    public string ProviderName { get; set; } = "Unknown provider";
    public double RedScale { get; set; } = 1;
    public double GreenScale { get; set; } = 1;
    public double BlueScale { get; set; } = 1;

    public bool Enabled
    {
        get => _enabled;
        set => Set(ref _enabled, value);
    }

    public string FriendlyName
    {
        get => _friendlyName;
        set => Set(ref _friendlyName, value);
    }

    public string Placement
    {
        get => _placement;
        set => Set(ref _placement, value);
    }

    public string WatchRole
    {
        get => _watchRole;
        set => Set(ref _watchRole, value);
    }

    public string GameRole
    {
        get => _gameRole;
        set => Set(ref _gameRole, value);
    }

    public int Intensity
    {
        get => _intensity;
        set => Set(ref _intensity, Math.Clamp(value, 0, 150));
    }

    public double RoomX
    {
        get => _roomX;
        set => Set(ref _roomX, Math.Clamp(value, 0, 1));
    }

    public double RoomY
    {
        get => _roomY;
        set => Set(ref _roomY, Math.Clamp(value, 0, 1));
    }

    [JsonIgnore]
    public string ShortId => DeviceId.Length <= 52 ? DeviceId : DeviceId[..49] + "...";

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

internal sealed class WorkshopEntry
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Summary { get; set; } = "";
    public string Author { get; set; } = "";
    public string PluginGuid { get; set; } = "";
    public string Version { get; set; } = "";
    public List<string> Categories { get; set; } = [];
    public bool Installed { get; set; }
    public bool Enabled { get; set; }
    public string HelpPage { get; set; } = "";

    [JsonIgnore]
    public string Type => Categories.FirstOrDefault() ?? "Plugin";

    [JsonIgnore]
    public string Status => Installed ? (Enabled ? "Installed, enabled" : "Installed") : "Available";

    [JsonIgnore]
    public string Requirements => PrerequisiteCatalog.Describe(Name);
}

internal static class PrerequisiteCatalog
{
    private static readonly Dictionary<string, string> Requirements = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Razer Devices"] = "Razer Synapse must detect the device. Disable competing Chroma apps while testing.",
        ["Corsair Devices"] = "Install Corsair iCUE and allow SDK control.",
        ["Logitech Devices"] = "Install Logitech G Hub or Logitech Gaming Software.",
        ["SteelSeries Devices"] = "Install SteelSeries GG and enable game integrations.",
        ["ASUS Devices"] = "Install Armoury Crate or Aura and its lighting service.",
        ["MSI Devices"] = "Install MSI Center or Mystic Light components required by the device.",
        ["Philips Hue"] = "A Hue Bridge on the same LAN is required.",
        ["Nanoleaf"] = "Enable LAN control and keep the panels on the same network.",
        ["WLED"] = "WLED must be reachable on the same LAN.",
        ["Windows Dynamic Lighting"] = "Requires Windows 11 and Dynamic Lighting compatible hardware.",
        ["OpenRGB Devices"] = "Optional. Requires the OpenRGB SDK server. It is not needed for native providers.",
        ["Counter Strike 2"] = "Install the plugin, import its profile, and install the supplied GSI file.",
        ["Ambilight"] = "Import an Ambilight profile after installing the brush.",
        ["Ambilight Smoothed"] = "Import a matching profile after installing the brush."
    };

    public static string Describe(string name)
    {
        if (Requirements.TryGetValue(name, out string? value))
            return value;
        return "Open the plugin details in Artemis for vendor-specific requirements.";
    }
}
