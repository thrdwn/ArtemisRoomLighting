using Microsoft.Data.Sqlite;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace ArtemisRoomLighting.Installer;

internal static class ArtemisState
{
    public static readonly string ArtemisData = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "Artemis");
    public static readonly string DatabasePath = Path.Combine(ArtemisData, "artemis.db");
    public static readonly string InstallRoot = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "ArtemisRoomLighting");
    public static readonly string ConfigurationPath = Path.Combine(InstallRoot, "setup.json");
    public static readonly string DirectPluginTarget = Path.Combine(
        ArtemisData,
        "Plugins",
        "Artemis.Plugins.DirectDevices-8f080623");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static string DetectArtemis()
    {
        string[] candidates =
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Artemis", "Artemis.UI.Windows.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Artemis", "Artemis.UI.Windows.exe")
        };
        return candidates.FirstOrDefault(File.Exists) ?? candidates[0];
    }

    public static string DetectCs2Cfg()
    {
        string suffix = Path.Combine("steamapps", "common", "Counter-Strike Global Offensive", "game", "csgo", "cfg");
        List<string> libraries =
        [
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam")
        ];
        string libraryFile = Path.Combine(libraries[0], "steamapps", "libraryfolders.vdf");
        if (File.Exists(libraryFile))
        {
            foreach (string line in File.ReadLines(libraryFile))
            {
                if (!line.Contains("\"path\"", StringComparison.OrdinalIgnoreCase))
                    continue;
                string? path = line.Split('"', StringSplitOptions.RemoveEmptyEntries).LastOrDefault()?.Replace(@"\\", @"\");
                if (!string.IsNullOrWhiteSpace(path))
                    libraries.Add(path);
            }
        }

        return libraries.Select(root => Path.Combine(root, suffix)).FirstOrDefault(Directory.Exists) ?? "";
    }

    public static SetupConfiguration LoadConfiguration()
    {
        if (!File.Exists(ConfigurationPath))
            return new SetupConfiguration();
        try
        {
            return JsonSerializer.Deserialize<SetupConfiguration>(File.ReadAllText(ConfigurationPath), JsonOptions)
                ?? new SetupConfiguration();
        }
        catch
        {
            return new SetupConfiguration();
        }
    }

    public static void SaveConfiguration(SetupConfiguration configuration)
    {
        Directory.CreateDirectory(InstallRoot);
        File.WriteAllText(ConfigurationPath, JsonSerializer.Serialize(configuration, JsonOptions));
    }

    public static List<DeviceAssignment> LoadDevices(SetupConfiguration configuration)
    {
        if (!File.Exists(DatabasePath))
            return [];

        Dictionary<string, string> providerNames = LoadProviderNames();
        Dictionary<string, DeviceAssignment> saved = configuration.Devices.ToDictionary(
            device => device.DeviceId,
            StringComparer.OrdinalIgnoreCase);
        List<DeviceAssignment> devices = [];

        using SqliteConnection connection = new($"Data Source={DatabasePath};Mode=ReadOnly");
        connection.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            select Id, DeviceProvider, X, Y, RedScale, GreenScale, BlueScale, IsEnabled
            from Devices
            order by IsEnabled desc, DeviceProvider, Id
            """;
        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            string id = reader.GetString(0);
            string providerGuid = reader.GetString(1);
            if (saved.TryGetValue(id, out DeviceAssignment? assignment))
            {
                assignment.ProviderGuid = providerGuid;
                assignment.ProviderName = providerNames.GetValueOrDefault(providerGuid, "Unknown provider");
                devices.Add(assignment);
                continue;
            }

            double roomX = Math.Clamp(reader.GetDouble(2) / 1600d, 0, 1);
            double roomY = Math.Clamp(reader.GetDouble(3) / 900d, 0, 1);
            devices.Add(new DeviceAssignment
            {
                DeviceId = id,
                FriendlyName = FriendlyDeviceName(id),
                ProviderGuid = providerGuid,
                ProviderName = providerNames.GetValueOrDefault(providerGuid, "Unknown provider"),
                Enabled = reader.GetBoolean(7),
                Placement = GuessPlacement(roomX, roomY),
                WatchRole = "Screen sample",
                GameRole = "Full game",
                Intensity = 100,
                RoomX = roomX,
                RoomY = roomY,
                RedScale = reader.GetDouble(4),
                GreenScale = reader.GetDouble(5),
                BlueScale = reader.GetDouble(6)
            });
        }

        return devices;
    }

    public static HashSet<string> LoadEnabledPluginGuids()
    {
        HashSet<string> result = new(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(DatabasePath))
            return result;

        using SqliteConnection connection = new($"Data Source={DatabasePath};Mode=ReadOnly");
        connection.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "select PluginGuid from Plugins where IsEnabled = 1";
        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
            result.Add(reader.GetString(0));
        return result;
    }

    public static Dictionary<string, (string Name, string Version, string HelpPage)> LoadInstalledPlugins()
    {
        Dictionary<string, (string Name, string Version, string HelpPage)> result =
            new(StringComparer.OrdinalIgnoreCase);
        IEnumerable<string> manifests = [];
        string workshop = Path.Combine(ArtemisData, "workshop");
        string plugins = Path.Combine(ArtemisData, "Plugins");
        if (Directory.Exists(workshop))
            manifests = manifests.Concat(Directory.EnumerateFiles(workshop, "plugin.json", SearchOption.AllDirectories));
        if (Directory.Exists(plugins))
            manifests = manifests.Concat(Directory.EnumerateFiles(plugins, "plugin.json", SearchOption.AllDirectories));

        foreach (string manifest in manifests)
        {
            try
            {
                using JsonDocument document = JsonDocument.Parse(File.ReadAllText(manifest));
                JsonElement root = document.RootElement;
                string guid = root.GetProperty("Guid").GetString() ?? "";
                string name = root.TryGetProperty("Name", out JsonElement nameValue) ? nameValue.GetString() ?? "" : "";
                string version = root.TryGetProperty("Version", out JsonElement versionValue) ? versionValue.GetString() ?? "" : "";
                string helpPage = root.TryGetProperty("HelpPage", out JsonElement helpValue) ? helpValue.GetString() ?? "" : "";
                if (!string.IsNullOrWhiteSpace(guid))
                    result[guid] = (name, version, helpPage);
            }
            catch
            {
            }
        }

        return result;
    }

    public static string Backup()
    {
        string backup = Path.Combine(InstallRoot, "Backups", DateTime.Now.ToString("yyyyMMdd-HHmmss"));
        Directory.CreateDirectory(backup);
        if (File.Exists(DatabasePath))
            File.Copy(DatabasePath, Path.Combine(backup, "artemis.db"), overwrite: true);
        if (Directory.Exists(DirectPluginTarget))
            CopyDirectory(DirectPluginTarget, Path.Combine(backup, Path.GetFileName(DirectPluginTarget)));
        return backup;
    }

    public static void StopArtemis()
    {
        foreach (Process process in Process.GetProcesses().Where(
                     process => process.ProcessName.Contains("Artemis", StringComparison.OrdinalIgnoreCase)))
        {
            try
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(8000);
            }
            catch
            {
            }
        }
    }

    public static void StartArtemis(string executable, string? route = null)
    {
        ProcessStartInfo start = new()
        {
            FileName = executable,
            UseShellExecute = true
        };
        if (!string.IsNullOrWhiteSpace(route))
            start.ArgumentList.Add("--route=" + route);
        Process.Start(start);
    }

    public static void ApplyDeviceMapping(IReadOnlyCollection<DeviceAssignment> devices)
    {
        using SqliteConnection connection = new($"Data Source={DatabasePath}");
        connection.Open();
        using SqliteTransaction transaction = connection.BeginTransaction();
        foreach (DeviceAssignment device in devices)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                update Devices
                set X = $x,
                    Y = $y,
                    ZIndex = $z,
                    RedScale = $red,
                    GreenScale = $green,
                    BlueScale = $blue,
                    IsEnabled = $enabled
                where Id = $id
                """;
            command.Parameters.AddWithValue("$x", Math.Round(device.RoomX * 1600, 2));
            command.Parameters.AddWithValue("$y", Math.Round(device.RoomY * 900, 2));
            command.Parameters.AddWithValue("$z", PlacementZIndex(device.Placement));
            command.Parameters.AddWithValue("$red", Math.Clamp(device.RedScale, 0, 2));
            command.Parameters.AddWithValue("$green", Math.Clamp(device.GreenScale, 0, 2));
            command.Parameters.AddWithValue("$blue", Math.Clamp(device.BlueScale, 0, 2));
            command.Parameters.AddWithValue("$enabled", device.Enabled ? 1 : 0);
            command.Parameters.AddWithValue("$id", device.DeviceId);
            command.ExecuteNonQuery();
        }
        transaction.Commit();
    }

    public static void CreateShortcut(
        string path,
        string target,
        string arguments,
        string workingDirectory,
        string description)
    {
        Type shellType = Type.GetTypeFromProgID("WScript.Shell")
            ?? throw new InvalidOperationException("Windows Script Host is unavailable.");
        object shell = Activator.CreateInstance(shellType)!;
        object shortcut = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, [path])!;
        Type shortcutType = shortcut.GetType();
        shortcutType.InvokeMember("TargetPath", BindingFlags.SetProperty, null, shortcut, [target]);
        shortcutType.InvokeMember("Arguments", BindingFlags.SetProperty, null, shortcut, [arguments]);
        shortcutType.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, shortcut, [workingDirectory]);
        shortcutType.InvokeMember("Description", BindingFlags.SetProperty, null, shortcut, [description]);
        shortcutType.InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);
        Marshal.FinalReleaseComObject(shortcut);
        Marshal.FinalReleaseComObject(shell);
    }

    public static void ReplaceDirectory(string source, string destination)
    {
        if (Directory.Exists(destination))
            Directory.Delete(destination, recursive: true);
        CopyDirectory(source, destination);
    }

    private static Dictionary<string, string> LoadProviderNames()
    {
        Dictionary<string, string> result = new(StringComparer.OrdinalIgnoreCase);
        foreach ((string guid, (string name, _, _)) in LoadInstalledPlugins())
            result[guid] = name;
        return result;
    }

    private static string FriendlyDeviceName(string id)
    {
        string[] parts = id.Split('-');
        if (parts.Length < 3)
            return id;
        int providerIndex = Array.FindIndex(parts, 1, part =>
            part.Equals("Razer", StringComparison.OrdinalIgnoreCase) ||
            part.Equals("Corsair", StringComparison.OrdinalIgnoreCase) ||
            part.Equals("Logitech", StringComparison.OrdinalIgnoreCase) ||
            part.Equals("Philips WiZ", StringComparison.OrdinalIgnoreCase) ||
            part.Equals("Lenovo", StringComparison.OrdinalIgnoreCase));
        return providerIndex > 0 ? string.Join("-", parts.Take(providerIndex)) : parts[0];
    }

    private static string GuessPlacement(double x, double y)
    {
        if (y < 0.2)
            return "Screen top";
        if (y > 0.78)
            return "Screen bottom";
        if (x < 0.2)
            return "Left";
        if (x > 0.8)
            return "Right";
        return y > 0.58 ? "Desk" : "Room";
    }

    private static int PlacementZIndex(string placement) => placement switch
    {
        "Screen top" => 8,
        "Screen bottom" => 7,
        "Left" => 6,
        "Right" => 5,
        "Desk" => 4,
        "Rear left" => 3,
        "Rear right" => 2,
        _ => 1
    };

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (string file in Directory.GetFiles(source))
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), overwrite: true);
        foreach (string directory in Directory.GetDirectories(source))
            CopyDirectory(directory, Path.Combine(destination, Path.GetFileName(directory)));
    }
}
