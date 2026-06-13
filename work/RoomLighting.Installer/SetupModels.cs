using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace ArtemisRoomLighting.Installer;

internal sealed class SetupConfiguration
{
    public int Version { get; set; } = 2;
    public string SelectedActivity { get; set; } = "Watch";
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
    public AdvancedSettings AdvancedSettings { get; set; } = new();
    public List<DeviceAssignment> Devices { get; set; } = [];
}

internal sealed class AdvancedSettings
{
    public int WatchFps { get; set; } = 30;
    public bool BlackoutOnBlack { get; set; } = true;
    public bool InstallCs2Gsi { get; set; }
    public bool CreateShortcuts { get; set; } = true;
    public bool UseDirectBridge { get; set; }
    public string StudyIp { get; set; } = "";
    public string UpperIp { get; set; } = "";
    public string LowerIp { get; set; } = "";
    public string Cs2CfgPath { get; set; } = "";
}

internal sealed class DeviceAssignment : INotifyPropertyChanged
{
    private bool _enabled;
    private string _friendlyName = "";
    private string _placement = "Desk";
    private string _watchRole = "Screen sample";
    private string _gameRole = "Full game";
    private string _deviceKind = "Unknown";
    private string _physicalZone = "Desk";
    private int _intensity = 100;
    private double _roomX = 0.5;
    private double _roomY = 0.5;

    public string DeviceId { get; set; } = "";
    public string ProviderGuid { get; set; } = "";
    public string ProviderName { get; set; } = "Unknown provider";
    public double RedScale { get; set; } = 1;
    public double GreenScale { get; set; } = 1;
    public double BlueScale { get; set; } = 1;
    public ModeAssignments ModeAssignments { get; set; } = new();

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

    public string DeviceKind
    {
        get => _deviceKind;
        set => Set(ref _deviceKind, value);
    }

    public string PhysicalZone
    {
        get => _physicalZone;
        set => Set(ref _physicalZone, value);
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

internal sealed class ModeAssignments
{
    public string Watch { get; set; } = SetupLabels.WatchScreenColor;
    public string Game { get; set; } = SetupLabels.GameMainEffects;
    public string Study { get; set; } = SetupLabels.StudyBrightDesk;
}

internal static class SetupLabels
{
    public const string WatchScreenColor = "Screen color";
    public const string WatchSoftDepth = "Soft room depth";
    public const string WatchGentleGlow = "Gentle glow";
    public const string WatchOff = "Off";

    public const string GameMainEffects = "Main game effects";
    public const string GameTeamMood = "Team mood";
    public const string GameImpactOnly = "Impact only";
    public const string GameOff = "Off";

    public const string StudyBrightDesk = "Bright desk light";
    public const string StudyCalmGlow = "Calm glow";
    public const string StudyOff = "Off";

    public static readonly string[] DeviceKinds = ["Light", "Keyboard", "Mouse", "Dock", "Unknown"];
    public static readonly string[] PhysicalZones =
    [
        "Screen top", "Screen bottom", "Desk", "Left", "Right", "Rear left", "Rear right", "Room"
    ];
    public static readonly string[] WatchBehaviors = [WatchScreenColor, WatchSoftDepth, WatchGentleGlow, WatchOff];
    public static readonly string[] GameBehaviors = [GameMainEffects, GameTeamMood, GameImpactOnly, GameOff];
    public static readonly string[] StudyBehaviors = [StudyBrightDesk, StudyCalmGlow, StudyOff];

    public static string ToWatchRole(string behavior) => behavior switch
    {
        WatchSoftDepth => "Soft depth",
        WatchGentleGlow => "Base glow",
        WatchOff => "Off",
        _ => "Screen sample"
    };

    public static string ToGameRole(string behavior) => behavior switch
    {
        GameTeamMood => "Team ambient",
        GameImpactOnly => "Impact alerts",
        GameOff => "Off",
        _ => "Full game"
    };

    public static string ToFriendlyWatch(string role) => role switch
    {
        "Soft depth" => WatchSoftDepth,
        "Base glow" => WatchGentleGlow,
        "Off" => WatchOff,
        _ => WatchScreenColor
    };

    public static string ToFriendlyGame(string role) => role switch
    {
        "Team ambient" => GameTeamMood,
        "Impact alerts" => GameImpactOnly,
        "Off" => GameOff,
        _ => GameMainEffects
    };

    public static string StudyBehaviorToWatchRole(string behavior) => behavior switch
    {
        StudyCalmGlow => "Base glow",
        StudyOff => "Off",
        _ => "Screen sample"
    };

    public static string PlacementFromZone(string zone) => PhysicalZones.Contains(zone) ? zone : "Desk";

    public static string ZoneFromPlacement(string placement) => PhysicalZones.Contains(placement) ? placement : "Desk";

    public static string GuessDeviceKind(string text)
    {
        if (text.Contains("keyboard", StringComparison.OrdinalIgnoreCase))
            return "Keyboard";
        if (text.Contains("mouse", StringComparison.OrdinalIgnoreCase))
            return "Mouse";
        if (text.Contains("dock", StringComparison.OrdinalIgnoreCase))
            return "Dock";
        if (text.Contains("light", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("lamp", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("hue", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("wiz", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("wled", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("nanoleaf", StringComparison.OrdinalIgnoreCase))
            return "Light";
        return "Unknown";
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
