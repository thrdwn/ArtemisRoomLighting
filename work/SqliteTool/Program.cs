using Microsoft.Data.Sqlite;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

string dbPath = Environment.GetEnvironmentVariable("ARTEMIS_DB_PATH") ?? @"C:\ProgramData\Artemis\artemis.db";
Guid directPluginGuid = Guid.Parse("8f080623-0fb1-4389-9083-bef0d0bc2cd0");
Guid razerPluginGuid = Guid.Parse("58a3d80e-d5cb-4a40-9465-c0a5d54825d6");
const string directFeatureType = "Artemis.Plugins.DirectDevices.DirectDevicesProvider";
const string defaultCsNumpadLayout =
    "NumLock=SelectedGrenade;Divide=Armor;Multiply=Helmet;Minus=DefuseKit;" +
    "Num7=Flash;Num8=Smoke;Num9=HE;Plus=Bomb;" +
    "Num4=Fire;Num5=Decoy;Num6=AmmoState;" +
    "Num1=Health1;Num2=Health2;Num3=Health3;Enter=Ammo3;" +
    "Num0=Ammo1;Decimal=Ammo2";

using SqliteConnection connection = new($"Data Source={dbPath}");
connection.Open();

if (args.Length == 0 || args[0] == "schema")
{
    using SqliteCommand schema = connection.CreateCommand();
    schema.CommandText = "select name, sql from sqlite_master where type = 'table' and name like '%Plugin%' order by name";
    using SqliteDataReader reader = schema.ExecuteReader();
    while (reader.Read())
    {
        Console.WriteLine("--- " + reader.GetString(0));
        Console.WriteLine(reader.GetString(1));
    }
    return;
}

if (args[0] == "list")
{
    PrintPlugins(connection, filtered: true);
    return;
}

if (args[0] == "list-all")
{
    PrintPlugins(connection, filtered: false);
    return;
}

if (args[0] == "tables")
{
    using SqliteCommand tables = connection.CreateCommand();
    tables.CommandText = "select name, sql from sqlite_master where type = 'table' order by name";
    using SqliteDataReader reader = tables.ExecuteReader();
    while (reader.Read())
    {
        Console.WriteLine("--- " + reader.GetString(0));
        Console.WriteLine(reader.GetString(1));
    }

    return;
}

if (args[0] == "table" && args.Length == 2)
{
    PrintTable(connection, args[1]);
    return;
}

if (args[0] == "profiles")
{
    PrintProfiles(connection);
    return;
}

if (args[0] == "profile-devices")
{
    PrintProfileDevices(connection);
    return;
}

if (args[0] == "profile-layers")
{
    PrintProfileLayers(connection);
    return;
}

if (args[0] == "enable-direct")
{
    using SqliteTransaction tx = connection.BeginTransaction();

    string pluginId = EnsurePlugin(connection, tx, directPluginGuid, isEnabled: true);
    EnsureFeature(connection, tx, pluginId, directFeatureType, isEnabled: true);

    tx.Commit();
    PrintPlugins(connection, filtered: true);
    return;
}

if (args[0] == "disable-razer")
{
    using SqliteTransaction tx = connection.BeginTransaction();
    SetPluginEnabled(connection, tx, razerPluginGuid, isEnabled: false);
    tx.Commit();
    PrintPlugins(connection, filtered: true);
    return;
}

if (args[0] == "rename-wiz")
{
    using SqliteTransaction tx = connection.BeginTransaction();
    RenameDevice(connection, tx,
        "WiZ Light 1-Philips WiZ-WiZ RGB light-Unknown",
        "Study Lamp-Philips WiZ-WiZ RGB light-Unknown",
        x: 538, y: 0, zIndex: 3);
    RenameDevice(connection, tx,
        "WiZ Light 2-Philips WiZ-WiZ RGB light-Unknown",
        "Lower Light-Philips WiZ-WiZ RGB light-Unknown",
        x: 664, y: 803, zIndex: 2);
    RenameDevice(connection, tx,
        "WiZ Light 3-Philips WiZ-WiZ RGB light-Unknown",
        "Upper Light-Philips WiZ-WiZ RGB light-Unknown",
        x: 764, y: 80, zIndex: 1);
    DisableDevicesForProvider(connection, tx, razerPluginGuid);
    tx.Commit();

    PrintTable(connection, "Devices");
    return;
}

if (args[0] == "setup-room-ambilight")
{
    using SqliteTransaction tx = connection.BeginTransaction();
    SetupRoomAmbilight(connection, tx);
    tx.Commit();

    PrintProfiles(connection);
    return;
}

if (args[0] == "use-study-ambilight")
{
    using SqliteTransaction tx = connection.BeginTransaction();
    UseStudyAmbilight(connection, tx);
    tx.Commit();

    PrintProfileDevices(connection);
    return;
}

if (args[0] == "add-ambient-base-glow")
{
    using SqliteTransaction tx = connection.BeginTransaction();
    AddAmbientBaseGlow(connection, tx);
    tx.Commit();

    PrintProfiles(connection);
    return;
}

if (args[0] == "clean-ambient-test")
{
    using SqliteTransaction tx = connection.BeginTransaction();
    SetPluginSetting(connection, tx, directPluginGuid, "EnableDirectAmbient", "false");
    SetPluginSetting(connection, tx, directPluginGuid, "DirectAmbientMode", JsonSerializer.Serialize("StudyOnly"));
    SetProfileSuspended(connection, tx, "Room Ambilight", false);
    SetProfileSuspended(connection, tx, "Ambilight", true);
    SetProfileSuspended(connection, tx, "Counter-Strike 2", true);
    tx.Commit();

    PrintProfiles(connection);
    return;
}

if (args[0] == "simplify-room-ambilight")
{
    using SqliteTransaction tx = connection.BeginTransaction();
    SetPluginSetting(connection, tx, directPluginGuid, "EnableDirectAmbient", "false");
    SetPluginSetting(connection, tx, directPluginGuid, "DirectAmbientMode", JsonSerializer.Serialize("StudyOnly"));
    SetProfileSuspended(connection, tx, "Room Ambilight", false);
    SetProfileSuspended(connection, tx, "Ambilight", true);
    SetProfileSuspended(connection, tx, "Counter-Strike 2", true);
    SimplifyRoomAmbilight(connection, tx);
    tx.Commit();

    PrintProfiles(connection);
    return;
}

if (args[0] == "enable-direct-study-ambient")
{
    using SqliteTransaction tx = connection.BeginTransaction();
    SetPluginSetting(connection, tx, directPluginGuid, "EnableDirectAmbient", "true");
    SetPluginSetting(connection, tx, directPluginGuid, "DirectAmbientMode", JsonSerializer.Serialize("StudyOnly"));
    SetPluginSetting(connection, tx, directPluginGuid, "SelectedControlMode", JsonSerializer.Serialize("Study"));
    SetProfileSuspended(connection, tx, "Room Ambilight", true);
    SetProfileSuspended(connection, tx, "Ambilight", true);
    SetProfileSuspended(connection, tx, "Counter-Strike 2", true);
    tx.Commit();

    PrintProfiles(connection);
    return;
}

if (args[0] == "enable-direct-room-ambient")
{
    using SqliteTransaction tx = connection.BeginTransaction();
    SetPluginSetting(connection, tx, directPluginGuid, "EnableDirectAmbient", "true");
    SetPluginSetting(connection, tx, directPluginGuid, "DirectAmbientMode", JsonSerializer.Serialize("FullRoomTasteful"));
    SetPluginSetting(connection, tx, directPluginGuid, "SelectedControlMode", JsonSerializer.Serialize("Watch"));
    SetPluginSetting(connection, tx, directPluginGuid, "EnableDirectCs", "true");
    SetPluginSetting(connection, tx, directPluginGuid, "EnableDirectValorant", "true");
    SetProfileSuspended(connection, tx, "Room Ambilight", true);
    SetProfileSuspended(connection, tx, "Ambilight", true);
    SetProfileSuspended(connection, tx, "Counter-Strike 2", true);
    tx.Commit();

    PrintProfiles(connection);
    return;
}

if (args[0] == "enable-direct-watch")
{
    using SqliteTransaction tx = connection.BeginTransaction();
    SetPluginSetting(connection, tx, directPluginGuid, "EnableDirectAmbient", "true");
    SetPluginSetting(connection, tx, directPluginGuid, "DirectAmbientMode", JsonSerializer.Serialize("WatchCinematic"));
    SetPluginSetting(connection, tx, directPluginGuid, "SelectedControlMode", JsonSerializer.Serialize("Watch"));
    SetProfileSuspended(connection, tx, "Room Ambilight", true);
    SetProfileSuspended(connection, tx, "Ambilight", true);
    SetProfileSuspended(connection, tx, "Counter-Strike 2", true);
    tx.Commit();

    PrintWatchConfiguration(connection, directPluginGuid);
    return;
}

if (args[0] == "set-watch-config" && args.Length is 8 or 9 or 11 or 13)
{
    using SqliteTransaction tx = connection.BeginTransaction();
    bool hasGameRoles = args.Length is 11 or 13;
    bool hasFps = args.Length == 13;
    int deviceOffset = hasGameRoles ? 2 : 0;
    string upperRole = ParseRearLightRoleArgument(args[2]);
    string lowerRole = ParseRearLightRoleArgument(args[3]);
    string upperGameRole = hasGameRoles
        ? ParseGameRearLightRoleArgument(args[4])
        : GetPluginStringSetting(connection, directPluginGuid, "UpperGameRole", "ObjectiveAlerts");
    string lowerGameRole = hasGameRoles
        ? ParseGameRearLightRoleArgument(args[5])
        : GetPluginStringSetting(connection, directPluginGuid, "LowerGameRole", "HealthDamage");
    SetPluginSetting(connection, tx, directPluginGuid, "WatchStudyEnabled", ParseBooleanArgument(args[1]).ToString().ToLowerInvariant());
    SetPluginSetting(connection, tx, directPluginGuid, "WatchUpperEnabled", (upperRole != "Off").ToString().ToLowerInvariant());
    SetPluginSetting(connection, tx, directPluginGuid, "WatchLowerEnabled", (lowerRole != "Off").ToString().ToLowerInvariant());
    SetPluginSetting(connection, tx, directPluginGuid, "UpperLightRole", JsonSerializer.Serialize(upperRole));
    SetPluginSetting(connection, tx, directPluginGuid, "LowerLightRole", JsonSerializer.Serialize(lowerRole));
    SetPluginSetting(connection, tx, directPluginGuid, "UpperGameRole", JsonSerializer.Serialize(upperGameRole));
    SetPluginSetting(connection, tx, directPluginGuid, "LowerGameRole", JsonSerializer.Serialize(lowerGameRole));
    SetPluginSetting(connection, tx, directPluginGuid, "WatchRazerKeyboardEnabled", ParseBooleanArgument(args[4 + deviceOffset]).ToString().ToLowerInvariant());
    SetPluginSetting(connection, tx, directPluginGuid, "WatchRazerMouseEnabled", ParseBooleanArgument(args[5 + deviceOffset]).ToString().ToLowerInvariant());
    SetPluginSetting(connection, tx, directPluginGuid, "WatchRazerDockEnabled", ParseBooleanArgument(args[6 + deviceOffset]).ToString().ToLowerInvariant());
    SetPluginSetting(connection, tx, directPluginGuid, "WatchLenovoKeyboardEnabled", ParseBooleanArgument(args[7 + deviceOffset]).ToString().ToLowerInvariant());
    if (args.Length is 9 or 11)
        SetPluginSetting(connection, tx, directPluginGuid, "BlackoutOnBlack", ParseBooleanArgument(args[8 + deviceOffset]).ToString().ToLowerInvariant());
    if (hasFps)
    {
        SetPluginSetting(connection, tx, directPluginGuid, "BlackoutOnBlack", ParseBooleanArgument(args[10]).ToString().ToLowerInvariant());
        SetPluginSetting(connection, tx, directPluginGuid, "AmbientFps", ParseFpsArgument(args[11], 1, 20).ToString(CultureInfo.InvariantCulture));
        SetPluginSetting(connection, tx, directPluginGuid, "GameFps", ParseFpsArgument(args[12], 1, 30).ToString(CultureInfo.InvariantCulture));
    }
    SetPluginSetting(connection, tx, directPluginGuid, "EnableDirectAmbient", "true");
    SetPluginSetting(connection, tx, directPluginGuid, "DirectAmbientMode", JsonSerializer.Serialize("WatchCinematic"));
    SetProfileSuspended(connection, tx, "Room Ambilight", true);
    SetProfileSuspended(connection, tx, "Ambilight", true);
    SetProfileSuspended(connection, tx, "Counter-Strike 2", true);
    tx.Commit();

    PrintWatchConfiguration(connection, directPluginGuid);
    return;
}

if (args[0] == "get-watch-config")
{
    PrintWatchConfiguration(connection, directPluginGuid);
    return;
}

if (args[0] == "set-control-mode" && args.Length == 3)
{
    string mode = args[1].Trim().Trim('"');
    if (!mode.Equals("Study", StringComparison.OrdinalIgnoreCase) &&
        !mode.Equals("Watch", StringComparison.OrdinalIgnoreCase))
    {
        throw new ArgumentException("Expected Study or Watch; received: " + args[1]);
    }

    bool autoGameEnabled = ParseBooleanArgument(args[2]);
    string selectedMode = mode.Equals("Watch", StringComparison.OrdinalIgnoreCase) ? "Watch" : "Study";
    string ambientMode = selectedMode == "Watch" ? "WatchCinematic" : "StudyOnly";

    using SqliteTransaction tx = connection.BeginTransaction();
    SetPluginSetting(connection, tx, directPluginGuid, "EnableDirectAmbient", "true");
    SetPluginSetting(connection, tx, directPluginGuid, "DirectAmbientMode", JsonSerializer.Serialize(ambientMode));
    SetPluginSetting(connection, tx, directPluginGuid, "SelectedControlMode", JsonSerializer.Serialize(selectedMode));
    SetPluginSetting(connection, tx, directPluginGuid, "EnableDirectCs", autoGameEnabled.ToString().ToLowerInvariant());
    SetPluginSetting(connection, tx, directPluginGuid, "EnableDirectValorant", autoGameEnabled.ToString().ToLowerInvariant());
    SetProfileSuspended(connection, tx, "Room Ambilight", true);
    SetProfileSuspended(connection, tx, "Ambilight", true);
    SetProfileSuspended(connection, tx, "Counter-Strike 2", true);
    tx.Commit();

    PrintWatchConfiguration(connection, directPluginGuid);
    return;
}

if (args[0] == "set-cs-numpad-layout" && args.Length == 2)
{
    string layout = args[1].Trim();
    if (string.IsNullOrWhiteSpace(layout) || layout.Length > 4096)
        throw new ArgumentException("The CS2 numpad layout is empty or too large.");

    using SqliteTransaction tx = connection.BeginTransaction();
    SetPluginSetting(connection, tx, directPluginGuid, "CsNumpadLayout", JsonSerializer.Serialize(layout));
    tx.Commit();

    PrintWatchConfiguration(connection, directPluginGuid);
    return;
}

if (args[0] == "set-cs-event-tuning" && args.Length == 10)
{
    using SqliteTransaction tx = connection.BeginTransaction();
    SetPluginSetting(connection, tx, directPluginGuid, "CsFlashIntensity", ParsePercentArgument(args[1]).ToString(CultureInfo.InvariantCulture));
    SetPluginSetting(connection, tx, directPluginGuid, "CsFireIntensity", ParsePercentArgument(args[2]).ToString(CultureInfo.InvariantCulture));
    SetPluginSetting(connection, tx, directPluginGuid, "CsSmokeIntensity", ParsePercentArgument(args[3]).ToString(CultureInfo.InvariantCulture));
    SetPluginSetting(connection, tx, directPluginGuid, "CsDeathIntensity", ParsePercentArgument(args[4]).ToString(CultureInfo.InvariantCulture));
    SetPluginSetting(connection, tx, directPluginGuid, "CsImpactIntensity", ParsePercentArgument(args[5]).ToString(CultureInfo.InvariantCulture));
    SetPluginSetting(connection, tx, directPluginGuid, "CsBombIntensity", ParsePercentArgument(args[6]).ToString(CultureInfo.InvariantCulture));
    SetPluginSetting(connection, tx, directPluginGuid, "CsClutchIntensity", ParsePercentArgument(args[7]).ToString(CultureInfo.InvariantCulture));
    SetPluginSetting(connection, tx, directPluginGuid, "CsTeamContrast", ParsePercentArgument(args[8]).ToString(CultureInfo.InvariantCulture));
    SetPluginSetting(connection, tx, directPluginGuid, "CsUtilityBrightness", ParsePercentArgument(args[9]).ToString(CultureInfo.InvariantCulture));
    tx.Commit();

    PrintWatchConfiguration(connection, directPluginGuid);
    return;
}

if (args[0] == "set-watch-tuning" && args.Length == 5)
{
    using SqliteTransaction tx = connection.BeginTransaction();
    SetPluginSetting(connection, tx, directPluginGuid, "WatchStudyStrength", ParsePercentArgument(args[1]).ToString(CultureInfo.InvariantCulture));
    SetPluginSetting(connection, tx, directPluginGuid, "WatchRearStrength", ParsePercentArgument(args[2]).ToString(CultureInfo.InvariantCulture));
    SetPluginSetting(connection, tx, directPluginGuid, "WatchRazerStrength", ParsePercentArgument(args[3]).ToString(CultureInfo.InvariantCulture));
    SetPluginSetting(connection, tx, directPluginGuid, "WatchColorBoost", ParsePercentArgument(args[4]).ToString(CultureInfo.InvariantCulture));
    tx.Commit();

    PrintWatchConfiguration(connection, directPluginGuid);
    return;
}

if (args[0] == "set-wiz-ips" && args.Length >= 2)
{
    string[] ips = args
        .Skip(1)
        .Select(ip => ip.Trim())
        .Where(ip => !string.IsNullOrWhiteSpace(ip))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

    if (ips.Length == 0)
        throw new ArgumentException("At least one WiZ IP address is required.");

    using SqliteTransaction tx = connection.BeginTransaction();
    SetPluginSetting(connection, tx, directPluginGuid, "WizIps", JsonSerializer.Serialize(ips));
    if (ips.Length >= 1)
        SetPluginSetting(connection, tx, directPluginGuid, "StudyLampIp", JsonSerializer.Serialize(ips[0]));
    if (ips.Length >= 2)
        SetPluginSetting(connection, tx, directPluginGuid, "LowerLampIp", JsonSerializer.Serialize(ips[1]));
    if (ips.Length >= 3)
        SetPluginSetting(connection, tx, directPluginGuid, "UpperLampIp", JsonSerializer.Serialize(ips[2]));
    tx.Commit();

    PrintWatchConfiguration(connection, directPluginGuid);
    return;
}

if (args[0] == "set-device-setup" && args.Length == 6)
{
    string studyIp = args[1].Trim();
    string lowerIp = args[2].Trim();
    string upperIp = args[3].Trim();
    bool enableRazer = ParseBooleanArgument(args[4]);
    bool enableLenovo = ParseBooleanArgument(args[5]);
    string[] ips = new[] { studyIp, lowerIp, upperIp }
        .Where(ip => !string.IsNullOrWhiteSpace(ip))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

    using SqliteTransaction tx = connection.BeginTransaction();
    SetPluginSetting(connection, tx, directPluginGuid, "StudyLampIp", JsonSerializer.Serialize(studyIp));
    SetPluginSetting(connection, tx, directPluginGuid, "LowerLampIp", JsonSerializer.Serialize(lowerIp));
    SetPluginSetting(connection, tx, directPluginGuid, "UpperLampIp", JsonSerializer.Serialize(upperIp));
    SetPluginSetting(connection, tx, directPluginGuid, "WizIps", JsonSerializer.Serialize(ips));
    SetPluginSetting(connection, tx, directPluginGuid, "EnableRazer", enableRazer.ToString().ToLowerInvariant());
    SetPluginSetting(connection, tx, directPluginGuid, "EnableLenovo", enableLenovo.ToString().ToLowerInvariant());
    tx.Commit();

    PrintWatchConfiguration(connection, directPluginGuid);
    return;
}

if (args[0] is "enable-direct-cs-auto" or "enable-direct-game-auto")
{
    using SqliteTransaction tx = connection.BeginTransaction();
    SetPluginSetting(connection, tx, directPluginGuid, "EnableDirectAmbient", "true");
    SetPluginSetting(connection, tx, directPluginGuid, "DirectAmbientMode", JsonSerializer.Serialize("StudyOnly"));
    SetPluginSetting(connection, tx, directPluginGuid, "SelectedControlMode", JsonSerializer.Serialize("GameAuto"));
    SetPluginSetting(connection, tx, directPluginGuid, "EnableDirectCs", "true");
    SetPluginSetting(connection, tx, directPluginGuid, "EnableDirectValorant", "true");
    SetProfileSuspended(connection, tx, "Room Ambilight", true);
    SetProfileSuspended(connection, tx, "Ambilight", true);
    SetProfileSuspended(connection, tx, "Counter-Strike 2", true);
    tx.Commit();

    PrintProfiles(connection);
    return;
}

throw new ArgumentException("Unknown command: " + args[0]);

static bool ParseBooleanArgument(string value)
{
    return value.Trim().ToLowerInvariant() switch
    {
        "1" or "true" or "yes" or "on" => true,
        "0" or "false" or "no" or "off" => false,
        _ => throw new ArgumentException("Expected a boolean value, received: " + value)
    };
}

static string ParseRearLightRoleArgument(string value)
{
    return value.Trim().Trim('"').ToLowerInvariant() switch
    {
        "1" or "true" or "yes" or "on" or "softdepth" => "SoftDepth",
        "positionalambient" => "PositionalAmbient",
        "gameeventsonly" => "GameEventsOnly",
        "0" or "false" or "no" or "off" => "Off",
        _ => throw new ArgumentException("Expected Off, SoftDepth, PositionalAmbient, or GameEventsOnly; received: " + value)
    };
}

static string ParseGameRearLightRoleArgument(string value)
{
    return value.Trim().Trim('"').ToLowerInvariant() switch
    {
        "objectivealerts" => "ObjectiveAlerts",
        "healthdamage" => "HealthDamage",
        "teamagentmood" => "TeamAgentMood",
        "mapmood" => "MapMood",
        "fullgamemix" => "FullGameMix",
        "off" => "Off",
        _ => throw new ArgumentException("Expected Off, ObjectiveAlerts, HealthDamage, TeamAgentMood, MapMood, or FullGameMix; received: " + value)
    };
}

static int ParseFpsArgument(string value, int minimum, int maximum)
{
    if (!int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int fps))
        throw new ArgumentException("Expected a whole-number FPS value, received: " + value);

    return Math.Clamp(fps, minimum, maximum);
}

static int ParsePercentArgument(string value)
{
    if (!int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int percent))
        throw new ArgumentException("Expected a whole-number percent value, received: " + value);

    return Math.Clamp(percent, 0, 200);
}

static void PrintWatchConfiguration(SqliteConnection connection, Guid pluginGuid)
{
    bool upperEnabled = GetPluginBooleanSetting(connection, pluginGuid, "WatchUpperEnabled", true);
    bool lowerEnabled = GetPluginBooleanSetting(connection, pluginGuid, "WatchLowerEnabled", true);
    string storedMode = GetPluginStringSetting(
        connection,
        pluginGuid,
        "SelectedControlMode",
        GetPluginStringSetting(connection, pluginGuid, "DirectAmbientMode", "StudyOnly") == "WatchCinematic" ? "Watch" : "Study");
    string currentMode = storedMode == "Watch" ? "Watch" : "Study";
    bool directCsEnabled = GetPluginBooleanSetting(connection, pluginGuid, "EnableDirectCs", true);
    bool directValorantEnabled = GetPluginBooleanSetting(connection, pluginGuid, "EnableDirectValorant", true);
    var configuration = new
    {
        study = GetPluginBooleanSetting(connection, pluginGuid, "WatchStudyEnabled", true),
        upper = upperEnabled,
        lower = lowerEnabled,
        upperRole = GetPluginStringSetting(connection, pluginGuid, "UpperLightRole", upperEnabled ? "SoftDepth" : "Off"),
        lowerRole = GetPluginStringSetting(connection, pluginGuid, "LowerLightRole", lowerEnabled ? "SoftDepth" : "Off"),
        upperGameRole = GetPluginStringSetting(connection, pluginGuid, "UpperGameRole", "ObjectiveAlerts"),
        lowerGameRole = GetPluginStringSetting(connection, pluginGuid, "LowerGameRole", "HealthDamage"),
        studyLampIp = GetPluginStringSetting(connection, pluginGuid, "StudyLampIp", ""),
        lowerLampIp = GetPluginStringSetting(connection, pluginGuid, "LowerLampIp", ""),
        upperLampIp = GetPluginStringSetting(connection, pluginGuid, "UpperLampIp", ""),
        wizIps = new[]
        {
            GetPluginStringSetting(connection, pluginGuid, "StudyLampIp", ""),
            GetPluginStringSetting(connection, pluginGuid, "LowerLampIp", ""),
            GetPluginStringSetting(connection, pluginGuid, "UpperLampIp", "")
        },
        razerKeyboard = GetPluginBooleanSetting(connection, pluginGuid, "WatchRazerKeyboardEnabled", true),
        razerMouse = GetPluginBooleanSetting(connection, pluginGuid, "WatchRazerMouseEnabled", true),
        razerDock = GetPluginBooleanSetting(connection, pluginGuid, "WatchRazerDockEnabled", true),
        lenovoKeyboard = GetPluginBooleanSetting(connection, pluginGuid, "WatchLenovoKeyboardEnabled", false),
        blackoutOnBlack = GetPluginBooleanSetting(connection, pluginGuid, "BlackoutOnBlack", true),
        ambientFps = GetPluginIntSetting(connection, pluginGuid, "AmbientFps", 10, 1, 20),
        gameFps = GetPluginIntSetting(connection, pluginGuid, "GameFps", 10, 1, 30),
        watchStudyStrength = GetPluginIntSetting(connection, pluginGuid, "WatchStudyStrength", 135, 0, 200),
        watchRearStrength = GetPluginIntSetting(connection, pluginGuid, "WatchRearStrength", 165, 0, 200),
        watchRazerStrength = GetPluginIntSetting(connection, pluginGuid, "WatchRazerStrength", 120, 0, 200),
        watchColorBoost = GetPluginIntSetting(connection, pluginGuid, "WatchColorBoost", 108, 0, 200),
        csNumpadLayout = GetPluginStringSetting(connection, pluginGuid, "CsNumpadLayout", defaultCsNumpadLayout),
        csFlashIntensity = GetPluginIntSetting(connection, pluginGuid, "CsFlashIntensity", 150, 0, 200),
        csFireIntensity = GetPluginIntSetting(connection, pluginGuid, "CsFireIntensity", 145, 0, 200),
        csSmokeIntensity = GetPluginIntSetting(connection, pluginGuid, "CsSmokeIntensity", 115, 0, 200),
        csDeathIntensity = GetPluginIntSetting(connection, pluginGuid, "CsDeathIntensity", 150, 0, 200),
        csImpactIntensity = GetPluginIntSetting(connection, pluginGuid, "CsImpactIntensity", 135, 0, 200),
        csBombIntensity = GetPluginIntSetting(connection, pluginGuid, "CsBombIntensity", 130, 0, 200),
        csClutchIntensity = GetPluginIntSetting(connection, pluginGuid, "CsClutchIntensity", 125, 0, 200),
        csTeamContrast = GetPluginIntSetting(connection, pluginGuid, "CsTeamContrast", 145, 0, 200),
        csUtilityBrightness = GetPluginIntSetting(connection, pluginGuid, "CsUtilityBrightness", 150, 0, 200),
        currentMode,
        autoGameEnabled = directCsEnabled && directValorantEnabled
    };
    Console.WriteLine(JsonSerializer.Serialize(configuration));
}

static int GetPluginIntSetting(SqliteConnection connection, Guid pluginGuid, string name, int defaultValue, int minimum, int maximum)
{
    using SqliteCommand command = connection.CreateCommand();
    command.CommandText = """
        select Value
        from PluginSettings
        where lower(PluginGuid) = lower($guid) and Name = $name
        order by rowid
        limit 1
        """;
    command.Parameters.AddWithValue("$guid", pluginGuid.ToString());
    command.Parameters.AddWithValue("$name", name);
    object? value = command.ExecuteScalar();
    if (value == null)
        return defaultValue;

    string text = Convert.ToString(value)?.Trim().Trim('"') ?? "";
    return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
        ? Math.Clamp(parsed, minimum, maximum)
        : defaultValue;
}

static string GetPluginStringSetting(SqliteConnection connection, Guid pluginGuid, string name, string defaultValue)
{
    using SqliteCommand command = connection.CreateCommand();
    command.CommandText = """
        select Value
        from PluginSettings
        where lower(PluginGuid) = lower($guid) and Name = $name
        order by rowid
        limit 1
        """;
    command.Parameters.AddWithValue("$guid", pluginGuid.ToString());
    command.Parameters.AddWithValue("$name", name);
    object? value = command.ExecuteScalar();
    if (value == null)
        return defaultValue;

    string text = Convert.ToString(value)?.Trim().Trim('"') ?? "";
    return string.IsNullOrWhiteSpace(text) ? defaultValue : text;
}

static bool GetPluginBooleanSetting(SqliteConnection connection, Guid pluginGuid, string name, bool defaultValue)
{
    using SqliteCommand command = connection.CreateCommand();
    command.CommandText = """
        select Value
        from PluginSettings
        where lower(PluginGuid) = lower($guid) and Name = $name
        order by rowid
        limit 1
        """;
    command.Parameters.AddWithValue("$guid", pluginGuid.ToString());
    command.Parameters.AddWithValue("$name", name);
    object? value = command.ExecuteScalar();
    if (value == null)
        return defaultValue;

    string text = Convert.ToString(value)?.Trim().Trim('"') ?? "";
    return bool.TryParse(text, out bool parsed) ? parsed : defaultValue;
}

static void SetPluginEnabled(SqliteConnection connection, SqliteTransaction tx, Guid pluginGuid, bool isEnabled)
{
    using SqliteCommand update = connection.CreateCommand();
    update.Transaction = tx;
    update.CommandText = "update Plugins set IsEnabled = $enabled where lower(PluginGuid) = lower($guid)";
    update.Parameters.AddWithValue("$enabled", isEnabled ? 1 : 0);
    update.Parameters.AddWithValue("$guid", pluginGuid.ToString());
    update.ExecuteNonQuery();
}

static void SetPluginSetting(SqliteConnection connection, SqliteTransaction tx, Guid pluginGuid, string name, string value)
{
    string canonicalGuid = pluginGuid.ToString().ToUpperInvariant();
    using SqliteCommand select = connection.CreateCommand();
    select.Transaction = tx;
    select.CommandText = "select Id from PluginSettings where lower(PluginGuid) = lower($guid) and Name = $name";
    select.Parameters.AddWithValue("$guid", canonicalGuid);
    select.Parameters.AddWithValue("$name", name);
    using SqliteDataReader reader = select.ExecuteReader();
    List<string> existingIds = new();
    while (reader.Read())
        existingIds.Add(reader.GetString(0));
    reader.Close();

    if (existingIds.Count > 0)
    {
        using SqliteCommand update = connection.CreateCommand();
        update.Transaction = tx;
        update.CommandText = "update PluginSettings set PluginGuid = $guid, Value = $value where Id = $id";
        update.Parameters.AddWithValue("$guid", canonicalGuid);
        update.Parameters.AddWithValue("$value", value);
        update.Parameters.AddWithValue("$id", existingIds[0]);
        update.ExecuteNonQuery();

        foreach (string duplicateId in existingIds.Skip(1))
        {
            using SqliteCommand delete = connection.CreateCommand();
            delete.Transaction = tx;
            delete.CommandText = "delete from PluginSettings where Id = $id";
            delete.Parameters.AddWithValue("$id", duplicateId);
            delete.ExecuteNonQuery();
        }

        return;
    }

    using SqliteCommand insert = connection.CreateCommand();
    insert.Transaction = tx;
    insert.CommandText = "insert into PluginSettings (Id, PluginGuid, Name, Value) values ($id, $guid, $name, $value)";
    insert.Parameters.AddWithValue("$id", Guid.NewGuid().ToString());
    insert.Parameters.AddWithValue("$guid", canonicalGuid);
    insert.Parameters.AddWithValue("$name", name);
    insert.Parameters.AddWithValue("$value", value);
    insert.ExecuteNonQuery();
}

static void SetProfileSuspended(SqliteConnection connection, SqliteTransaction tx, string name, bool suspended)
{
    using SqliteCommand select = connection.CreateCommand();
    select.Transaction = tx;
    select.CommandText = "select Id, ProfileConfiguration from ProfileContainers where json_extract(ProfileConfiguration, '$.Name') = $name";
    select.Parameters.AddWithValue("$name", name);

    using SqliteDataReader reader = select.ExecuteReader();
    List<(string Id, string Configuration)> rows = new();
    while (reader.Read())
        rows.Add((reader.GetString(0), reader.GetString(1)));
    reader.Close();

    foreach ((string id, string configurationText) in rows)
    {
        JsonNode configuration = JsonNode.Parse(configurationText)!;
        configuration["IsSuspended"] = suspended;

        using SqliteCommand update = connection.CreateCommand();
        update.Transaction = tx;
        update.CommandText = "update ProfileContainers set ProfileConfiguration = $configuration where Id = $id";
        update.Parameters.AddWithValue("$configuration", configuration.ToJsonString());
        update.Parameters.AddWithValue("$id", id);
        update.ExecuteNonQuery();
    }
}

static string EnsurePlugin(SqliteConnection connection, SqliteTransaction tx, Guid pluginGuid, bool isEnabled)
{
    using SqliteCommand select = connection.CreateCommand();
    select.Transaction = tx;
    select.CommandText = "select Id from Plugins where lower(PluginGuid) = lower($guid)";
    select.Parameters.AddWithValue("$guid", pluginGuid.ToString());
    object? existing = select.ExecuteScalar();

    if (existing != null)
    {
        string id = Convert.ToString(existing)!;
        using SqliteCommand update = connection.CreateCommand();
        update.Transaction = tx;
        update.CommandText = "update Plugins set IsEnabled = $enabled where Id = $id";
        update.Parameters.AddWithValue("$enabled", isEnabled ? 1 : 0);
        update.Parameters.AddWithValue("$id", id);
        update.ExecuteNonQuery();
        return id;
    }

    using SqliteCommand insert = connection.CreateCommand();
    insert.Transaction = tx;
    string newId = Guid.NewGuid().ToString();
    insert.CommandText = "insert into Plugins (Id, PluginGuid, IsEnabled) values ($id, $guid, $enabled)";
    insert.Parameters.AddWithValue("$id", newId);
    insert.Parameters.AddWithValue("$guid", pluginGuid.ToString());
    insert.Parameters.AddWithValue("$enabled", isEnabled ? 1 : 0);
    insert.ExecuteNonQuery();
    return newId;
}

static void EnsureFeature(SqliteConnection connection, SqliteTransaction tx, string pluginId, string type, bool isEnabled)
{
    using SqliteCommand select = connection.CreateCommand();
    select.Transaction = tx;
    select.CommandText = "select Id from PluginFeatures where PluginEntityId = $pluginId and Type = $type";
    select.Parameters.AddWithValue("$pluginId", pluginId);
    select.Parameters.AddWithValue("$type", type);
    object? existing = select.ExecuteScalar();

    if (existing != null)
    {
        using SqliteCommand update = connection.CreateCommand();
        update.Transaction = tx;
        update.CommandText = "update PluginFeatures set IsEnabled = $enabled where Id = $id";
        update.Parameters.AddWithValue("$enabled", isEnabled ? 1 : 0);
        update.Parameters.AddWithValue("$id", Convert.ToString(existing)!);
        update.ExecuteNonQuery();
        return;
    }

    using SqliteCommand insert = connection.CreateCommand();
    insert.Transaction = tx;
    insert.CommandText = "insert into PluginFeatures (Id, PluginEntityId, Type, IsEnabled) values ($id, $pluginId, $type, $enabled)";
    insert.Parameters.AddWithValue("$id", Guid.NewGuid().ToString());
    insert.Parameters.AddWithValue("$pluginId", pluginId);
    insert.Parameters.AddWithValue("$type", type);
    insert.Parameters.AddWithValue("$enabled", isEnabled ? 1 : 0);
    insert.ExecuteNonQuery();
}

static void PrintPlugins(SqliteConnection connection, bool filtered)
{
    using SqliteCommand command = connection.CreateCommand();
    string filter = filtered
        ? "where lower(p.PluginGuid) in ('8f080623-0fb1-4389-9083-bef0d0bc2cd0', '58a3d80e-d5cb-4a40-9465-c0a5d54825d6')"
        : "";

    command.CommandText = $"""
        select p.Id, p.PluginGuid, p.IsEnabled, f.Type, f.IsEnabled
        from Plugins p
        left join PluginFeatures f on f.PluginEntityId = p.Id
        {filter}
        order by p.PluginGuid, f.Type
        """;

    using SqliteDataReader reader = command.ExecuteReader();
    while (reader.Read())
    {
        Console.WriteLine(string.Join(" | ",
            reader.GetString(0),
            reader.GetString(1),
            reader.GetBoolean(2),
            reader.IsDBNull(3) ? "" : reader.GetString(3),
            reader.IsDBNull(4) ? "" : reader.GetBoolean(4).ToString()));
    }
}

static void PrintTable(SqliteConnection connection, string tableName)
{
    if (!tableName.All(static c => char.IsLetterOrDigit(c) || c == '_'))
        throw new ArgumentException("Invalid table name.");

    using SqliteCommand command = connection.CreateCommand();
    command.CommandText = $"select * from {tableName} limit 200";
    using SqliteDataReader reader = command.ExecuteReader();

    List<string> columns = Enumerable.Range(0, reader.FieldCount).Select(reader.GetName).ToList();
    Console.WriteLine(string.Join(" | ", columns));
    while (reader.Read())
    {
        List<string> values = new();
        for (int i = 0; i < reader.FieldCount; i++)
            values.Add(reader.IsDBNull(i) ? "" : Convert.ToString(reader.GetValue(i)) ?? "");

        Console.WriteLine(string.Join(" | ", values));
    }
}

static void RenameDevice(SqliteConnection connection, SqliteTransaction tx, string oldId, string newId, double x, double y, int zIndex)
{
    bool oldExists = DeviceExists(connection, tx, oldId);
    bool newExists = DeviceExists(connection, tx, newId);

    if (oldExists && !newExists)
    {
        using SqliteCommand update = connection.CreateCommand();
        update.Transaction = tx;
        update.CommandText = """
            update Devices
            set Id = $newId, X = $x, Y = $y, ZIndex = $zIndex, IsEnabled = 1
            where Id = $oldId
            """;
        update.Parameters.AddWithValue("$newId", newId);
        update.Parameters.AddWithValue("$oldId", oldId);
        update.Parameters.AddWithValue("$x", x);
        update.Parameters.AddWithValue("$y", y);
        update.Parameters.AddWithValue("$zIndex", zIndex);
        update.ExecuteNonQuery();
        return;
    }

    if (newExists)
    {
        using SqliteCommand update = connection.CreateCommand();
        update.Transaction = tx;
        update.CommandText = """
            update Devices
            set X = $x, Y = $y, ZIndex = $zIndex, IsEnabled = 1
            where Id = $newId
            """;
        update.Parameters.AddWithValue("$newId", newId);
        update.Parameters.AddWithValue("$x", x);
        update.Parameters.AddWithValue("$y", y);
        update.Parameters.AddWithValue("$zIndex", zIndex);
        update.ExecuteNonQuery();
    }

    if (oldExists)
    {
        using SqliteCommand delete = connection.CreateCommand();
        delete.Transaction = tx;
        delete.CommandText = "delete from Devices where Id = $oldId";
        delete.Parameters.AddWithValue("$oldId", oldId);
        delete.ExecuteNonQuery();
    }
}

static bool DeviceExists(SqliteConnection connection, SqliteTransaction tx, string id)
{
    using SqliteCommand command = connection.CreateCommand();
    command.Transaction = tx;
    command.CommandText = "select 1 from Devices where Id = $id";
    command.Parameters.AddWithValue("$id", id);
    return command.ExecuteScalar() != null;
}

static void DisableDevicesForProvider(SqliteConnection connection, SqliteTransaction tx, Guid providerGuid)
{
    using SqliteCommand update = connection.CreateCommand();
    update.Transaction = tx;
    update.CommandText = "update Devices set IsEnabled = 0 where lower(DeviceProvider) = lower($guid)";
    update.Parameters.AddWithValue("$guid", providerGuid.ToString());
    update.ExecuteNonQuery();
}

static void PrintProfiles(SqliteConnection connection)
{
    using SqliteCommand command = connection.CreateCommand();
    command.CommandText = """
        select pc.Id, c.Name, pc.ProfileConfiguration, pc.Profile
        from ProfileContainers pc
        left join ProfileCategories c on c.Id = pc.ProfileCategoryId
        order by c."Order", json_extract(pc.ProfileConfiguration, '$.Order'), json_extract(pc.ProfileConfiguration, '$.Name')
        """;

    using SqliteDataReader reader = command.ExecuteReader();
    while (reader.Read())
    {
        string containerId = reader.GetString(0);
        string category = reader.IsDBNull(1) ? "" : reader.GetString(1);
        JsonNode configuration = JsonNode.Parse(reader.GetString(2))!;
        JsonNode profile = JsonNode.Parse(reader.GetString(3))!;
        string name = configuration["Name"]?.GetValue<string>() ?? profile["Name"]?.GetValue<string>() ?? "";
        bool suspended = configuration["IsSuspended"]?.GetValue<bool>() ?? false;
        JsonArray layers = profile["Layers"]?.AsArray() ?? new JsonArray();

        Console.WriteLine($"{name} | Category={category} | Container={containerId} | Suspended={suspended} | Layers={layers.Count}");
        foreach (JsonNode? layer in layers)
        {
            if (layer == null)
                continue;

            string layerName = layer["Name"]?.GetValue<string>() ?? "";
            bool layerSuspended = layer["Suspended"]?.GetValue<bool>() ?? false;
            int ledCount = layer["Leds"]?.AsArray().Count ?? 0;
            string brush = layer["LayerBrush"]?["BrushType"]?.GetValue<string>() ?? "";
            Console.WriteLine($"  - {layerName} | Suspended={layerSuspended} | Leds={ledCount} | Brush={brush}");
        }
    }
}

static void PrintProfileDevices(SqliteConnection connection)
{
    using SqliteCommand command = connection.CreateCommand();
    command.CommandText = "select ProfileConfiguration, Profile from ProfileContainers order by json_extract(ProfileConfiguration, '$.Name')";
    using SqliteDataReader reader = command.ExecuteReader();
    while (reader.Read())
    {
        JsonNode configuration = JsonNode.Parse(reader.GetString(0))!;
        JsonNode profile = JsonNode.Parse(reader.GetString(1))!;
        string name = configuration["Name"]?.GetValue<string>() ?? profile["Name"]?.GetValue<string>() ?? "";
        SortedSet<string> deviceIds = new(StringComparer.OrdinalIgnoreCase);

        foreach (JsonNode? layer in profile["Layers"]?.AsArray() ?? new JsonArray())
        {
            foreach (JsonNode? led in layer?["Leds"]?.AsArray() ?? new JsonArray())
            {
                string? deviceId = led?["DeviceIdentifier"]?.GetValue<string>();
                if (!string.IsNullOrWhiteSpace(deviceId))
                    deviceIds.Add(deviceId);
            }
        }

        Console.WriteLine(name);
        foreach (string deviceId in deviceIds)
            Console.WriteLine("  " + deviceId);
    }
}

static void PrintProfileLayers(SqliteConnection connection)
{
    using SqliteCommand command = connection.CreateCommand();
    command.CommandText = "select ProfileConfiguration, Profile from ProfileContainers order by json_extract(ProfileConfiguration, '$.Name')";
    using SqliteDataReader reader = command.ExecuteReader();
    while (reader.Read())
    {
        JsonNode configuration = JsonNode.Parse(reader.GetString(0))!;
        JsonNode profile = JsonNode.Parse(reader.GetString(1))!;
        string name = configuration["Name"]?.GetValue<string>() ?? profile["Name"]?.GetValue<string>() ?? "";
        Console.WriteLine(name);

        foreach (JsonNode? layer in profile["Layers"]?.AsArray() ?? new JsonArray())
        {
            string layerName = layer?["Name"]?.GetValue<string>() ?? "";
            SortedDictionary<string, int> counts = new(StringComparer.OrdinalIgnoreCase);
            foreach (JsonNode? led in layer?["Leds"]?.AsArray() ?? new JsonArray())
            {
                string? deviceId = led?["DeviceIdentifier"]?.GetValue<string>();
                if (string.IsNullOrWhiteSpace(deviceId))
                    continue;

                counts[deviceId] = counts.TryGetValue(deviceId, out int count) ? count + 1 : 1;
            }

            Console.WriteLine($"  - {layerName}");
            foreach ((string deviceId, int count) in counts)
                Console.WriteLine($"      {count,3} {deviceId}");
        }
    }
}

static void SetupRoomAmbilight(SqliteConnection connection, SqliteTransaction tx)
{
    string applicationsCategoryId = EnsureProfileCategory(connection, tx, "Applications", order: 3);

    using SqliteCommand select = connection.CreateCommand();
    select.Transaction = tx;
    select.CommandText = """
        select Id, ProfileConfiguration, Profile
        from ProfileContainers
        where json_extract(ProfileConfiguration, '$.Name') in ('Ambilight Smoothed - Profile v2', 'Room Ambilight')
        order by case json_extract(ProfileConfiguration, '$.Name') when 'Room Ambilight' then 0 else 1 end
        limit 1
        """;

    using SqliteDataReader reader = select.ExecuteReader();
    if (!reader.Read())
        throw new InvalidOperationException("Could not find the Ambilight Smoothed profile to customize.");

    string containerId = reader.GetString(0);
    JsonNode configuration = JsonNode.Parse(reader.GetString(1))!;
    JsonNode profile = JsonNode.Parse(reader.GetString(2))!;
    reader.Close();

    configuration["Name"] = "Room Ambilight";
    configuration["IsSuspended"] = false;
    configuration["Order"] = 0;
    configuration["ProfileCategoryId"] = applicationsCategoryId.ToLowerInvariant();
    profile["Name"] = "Room Ambilight";
    profile["IsFreshImport"] = false;

    JsonArray layers = profile["Layers"]?.AsArray() ?? new JsonArray();
    foreach (JsonNode? layer in layers)
    {
        if (layer == null)
            continue;

        string layerName = layer["Name"]?.GetValue<string>() ?? "";
        if (layerName.Contains("Keyboard/Mouse", StringComparison.OrdinalIgnoreCase))
            layer["Name"] = "Razer + Upper/Lower Ambilight";

        if (layerName.Contains("PC Case", StringComparison.OrdinalIgnoreCase))
            layer["Suspended"] = true;

        UpdateAmbilightCapture(layer);
        FilterAmbientLeds(layer);
    }

    using SqliteCommand update = connection.CreateCommand();
    update.Transaction = tx;
    update.CommandText = """
        update ProfileContainers
        set ProfileConfiguration = $configuration,
            Profile = $profile,
            ProfileCategoryId = $categoryId
        where Id = $id
        """;
    update.Parameters.AddWithValue("$configuration", configuration.ToJsonString());
    update.Parameters.AddWithValue("$profile", profile.ToJsonString());
    update.Parameters.AddWithValue("$categoryId", applicationsCategoryId);
    update.Parameters.AddWithValue("$id", containerId);
    update.ExecuteNonQuery();

    SuspendProfileByName(connection, tx, "Ambilight");
}

static string EnsureProfileCategory(SqliteConnection connection, SqliteTransaction tx, string name, int order)
{
    using SqliteCommand select = connection.CreateCommand();
    select.Transaction = tx;
    select.CommandText = "select Id from ProfileCategories where Name = $name";
    select.Parameters.AddWithValue("$name", name);
    object? existing = select.ExecuteScalar();
    if (existing != null)
        return Convert.ToString(existing)!;

    string id = Guid.NewGuid().ToString().ToUpperInvariant();
    using SqliteCommand insert = connection.CreateCommand();
    insert.Transaction = tx;
    insert.CommandText = "insert into ProfileCategories (Id, Name, IsCollapsed, IsSuspended, \"Order\") values ($id, $name, 0, 0, $order)";
    insert.Parameters.AddWithValue("$id", id);
    insert.Parameters.AddWithValue("$name", name);
    insert.Parameters.AddWithValue("$order", order);
    insert.ExecuteNonQuery();
    return id;
}

static void SuspendProfileByName(SqliteConnection connection, SqliteTransaction tx, string name)
{
    using SqliteCommand select = connection.CreateCommand();
    select.Transaction = tx;
    select.CommandText = "select Id, ProfileConfiguration from ProfileContainers where json_extract(ProfileConfiguration, '$.Name') = $name";
    select.Parameters.AddWithValue("$name", name);
    using SqliteDataReader reader = select.ExecuteReader();

    List<(string Id, string Configuration)> rows = new();
    while (reader.Read())
        rows.Add((reader.GetString(0), reader.GetString(1)));
    reader.Close();

    foreach ((string id, string configurationText) in rows)
    {
        JsonNode configuration = JsonNode.Parse(configurationText)!;
        configuration["IsSuspended"] = true;

        using SqliteCommand update = connection.CreateCommand();
        update.Transaction = tx;
        update.CommandText = "update ProfileContainers set ProfileConfiguration = $configuration where Id = $id";
        update.Parameters.AddWithValue("$configuration", configuration.ToJsonString());
        update.Parameters.AddWithValue("$id", id);
        update.ExecuteNonQuery();
    }
}

static void UpdateAmbilightCapture(JsonNode layer)
{
    JsonArray? propertyGroups = layer["LayerBrush"]?["PropertyGroup"]?["PropertyGroups"]?.AsArray();
    if (propertyGroups == null)
        return;

    foreach (JsonNode? group in propertyGroups)
    {
        if (group == null)
            continue;

        if (!string.Equals(group?["Identifier"]?.GetValue<string>(), "Capture", StringComparison.OrdinalIgnoreCase))
            continue;

        foreach (JsonNode? property in group!["Properties"]?.AsArray() ?? new JsonArray())
        {
            string identifier = property?["Identifier"]?.GetValue<string>() ?? "";
            if (property == null)
                continue;

            property["Value"] = identifier switch
            {
                "DisplayName" => JsonSerializer.Serialize(@"\\.\DISPLAY3"),
                "X" => "0",
                "Y" => "0",
                "Width" => "1920",
                "Height" => "1080",
                "CaptureFullScreen" => "true",
                "DownscaleLevel" => "5",
                "SmoothingLevel" => "6",
                "TargetCaptureFps" => "30",
                _ => property["Value"]?.GetValue<string>() ?? ""
            };
        }
    }
}

static void FilterAmbientLeds(JsonNode layer)
{
    JsonArray? leds = layer["Leds"]?.AsArray();
    if (leds == null || leds.Count == 0)
        return;

    HashSet<string> allowedDevices = new(StringComparer.OrdinalIgnoreCase)
    {
        "Study Lamp-Philips WiZ-WiZ RGB light-Unknown",
        "Razer Blackwidow V3-Razer-BlackWidow V3-Keyboard",
        "Razer DeathAdder V2 Pro (Wireless)-Razer-DeathAdder V2 Pro Wireless-Mouse",
        "Razer Mouse Dock Chroma-Razer-Mouse Dock Chroma-Unknown"
    };

    JsonArray filtered = new();
    foreach (JsonNode? led in leds)
    {
        string? deviceId = led?["DeviceIdentifier"]?.GetValue<string>();
        if (deviceId != null && allowedDevices.Contains(deviceId))
            filtered.Add(led!.DeepClone());
    }

    layer["Leds"] = filtered;
}

static void UseStudyAmbilight(SqliteConnection connection, SqliteTransaction tx)
{
    using SqliteCommand select = connection.CreateCommand();
    select.Transaction = tx;
    select.CommandText = """
        select Id, Profile
        from ProfileContainers
        where json_extract(ProfileConfiguration, '$.Name') = 'Room Ambilight'
        limit 1
        """;

    using SqliteDataReader reader = select.ExecuteReader();
    if (!reader.Read())
        throw new InvalidOperationException("Room Ambilight profile was not found.");

    string containerId = reader.GetString(0);
    JsonNode profile = JsonNode.Parse(reader.GetString(1))!;
    reader.Close();

    foreach (JsonNode? layer in profile["Layers"]?.AsArray() ?? new JsonArray())
    {
        if (layer == null)
            continue;

        string layerName = layer["Name"]?.GetValue<string>() ?? "";
        if (layerName.Contains("Razer", StringComparison.OrdinalIgnoreCase) ||
            layerName.Contains("Keyboard/Mouse", StringComparison.OrdinalIgnoreCase))
        {
            layer["Name"] = "Razer + Study Lamp Ambilight";
            FilterAmbientLeds(layer);
            AddStudyLampLed(layer);
        }
        else if (layerName.Contains("PC Case", StringComparison.OrdinalIgnoreCase))
        {
            layer["Suspended"] = true;
            layer["Leds"] = new JsonArray();
        }
    }

    using SqliteCommand update = connection.CreateCommand();
    update.Transaction = tx;
    update.CommandText = "update ProfileContainers set Profile = $profile where Id = $id";
    update.Parameters.AddWithValue("$profile", profile.ToJsonString());
    update.Parameters.AddWithValue("$id", containerId);
    update.ExecuteNonQuery();
}

static void AddStudyLampLed(JsonNode layer)
{
    const string studyLampId = "Study Lamp-Philips WiZ-WiZ RGB light-Unknown";
    JsonArray leds = layer["Leds"]?.AsArray() ?? new JsonArray();

    foreach (JsonNode? led in leds)
    {
        if (string.Equals(led?["DeviceIdentifier"]?.GetValue<string>(), studyLampId, StringComparison.OrdinalIgnoreCase))
            return;
    }

    leds.Add(new JsonObject
    {
        ["LedName"] = "Custom1",
        ["DeviceIdentifier"] = studyLampId,
        ["PhysicalLayout"] = null
    });
    layer["Leds"] = leds;
}

static void AddAmbientBaseGlow(SqliteConnection connection, SqliteTransaction tx)
{
    using SqliteCommand select = connection.CreateCommand();
    select.Transaction = tx;
    select.CommandText = """
        select Id, Profile
        from ProfileContainers
        where json_extract(ProfileConfiguration, '$.Name') = 'Room Ambilight'
        limit 1
        """;

    using SqliteDataReader reader = select.ExecuteReader();
    if (!reader.Read())
        throw new InvalidOperationException("Room Ambilight profile was not found.");

    string containerId = reader.GetString(0);
    JsonNode profile = JsonNode.Parse(reader.GetString(1))!;
    reader.Close();

    JsonArray layers = profile["Layers"]?.AsArray() ?? throw new InvalidOperationException("Room Ambilight has no layers.");
    JsonNode? ambientLayer = layers.FirstOrDefault(layer => (layer?["Name"]?.GetValue<string>() ?? "").Contains("Study Lamp", StringComparison.OrdinalIgnoreCase));
    if (ambientLayer == null)
        throw new InvalidOperationException("Could not find the Study Lamp ambilight layer.");

    ambientLayer["Name"] = "Screen Color - Razer + Study Lamp";
    ambientLayer["Order"] = 2;
    FilterAmbientLeds(ambientLayer);
    AddStudyLampLed(ambientLayer);

    for (int i = layers.Count - 1; i >= 0; i--)
    {
        if (string.Equals(layers[i]?["Name"]?.GetValue<string>(), "Dim Base Glow", StringComparison.OrdinalIgnoreCase))
            layers.RemoveAt(i);
    }

    JsonNode baseLayer = ambientLayer.DeepClone();
    baseLayer["Id"] = Guid.NewGuid().ToString();
    baseLayer["Name"] = "Dim Base Glow";
    baseLayer["Order"] = 1;
    baseLayer["Suspended"] = false;
    SetLayerOpacity(baseLayer, "45");
    SetSolidBrush(baseLayer, "#ff3a2460");
    layers.Add(baseLayer);

    using SqliteCommand update = connection.CreateCommand();
    update.Transaction = tx;
    update.CommandText = "update ProfileContainers set Profile = $profile where Id = $id";
    update.Parameters.AddWithValue("$profile", profile.ToJsonString());
    update.Parameters.AddWithValue("$id", containerId);
    update.ExecuteNonQuery();
}

static void SimplifyRoomAmbilight(SqliteConnection connection, SqliteTransaction tx)
{
    using SqliteCommand select = connection.CreateCommand();
    select.Transaction = tx;
    select.CommandText = """
        select Id, Profile
        from ProfileContainers
        where json_extract(ProfileConfiguration, '$.Name') = 'Room Ambilight'
        limit 1
        """;

    using SqliteDataReader reader = select.ExecuteReader();
    if (!reader.Read())
        throw new InvalidOperationException("Room Ambilight profile was not found.");

    string containerId = reader.GetString(0);
    JsonNode profile = JsonNode.Parse(reader.GetString(1))!;
    reader.Close();

    JsonArray layers = profile["Layers"]?.AsArray() ?? throw new InvalidOperationException("Room Ambilight has no layers.");
    JsonNode? screenLayer = layers.FirstOrDefault(layer => IsAmbilightLayer(layer) && ((layer?["Leds"]?.AsArray().Count ?? 0) > 0));
    if (screenLayer == null)
        screenLayer = layers.FirstOrDefault(IsAmbilightLayer);
    if (screenLayer == null)
        throw new InvalidOperationException("Could not find an Ambilight layer.");

    foreach (JsonNode? layer in layers)
    {
        if (layer == null)
            continue;

        if (ReferenceEquals(layer, screenLayer))
        {
            layer["Name"] = "Ambilight Smoothed - Study + Razer";
            layer["Order"] = 1;
            layer["Suspended"] = false;
            SetLayerOpacity(layer, "100");
            SetGeneralProperty(layer, "BlendMode", "3");
            UpdateAmbilightCapture(layer);
            FilterAmbientLeds(layer);
            AddStudyLampLed(layer);
        }
        else
        {
            layer["Suspended"] = true;
            layer["Leds"] = new JsonArray();
        }
    }

    using SqliteCommand update = connection.CreateCommand();
    update.Transaction = tx;
    update.CommandText = "update ProfileContainers set Profile = $profile where Id = $id";
    update.Parameters.AddWithValue("$profile", profile.ToJsonString());
    update.Parameters.AddWithValue("$id", containerId);
    update.ExecuteNonQuery();
}

static bool IsAmbilightLayer(JsonNode? layer)
{
    string brush = layer?["LayerBrush"]?["BrushType"]?.GetValue<string>() ?? "";
    return brush.Contains("Ambilight", StringComparison.OrdinalIgnoreCase);
}

static void SetGeneralProperty(JsonNode layer, string identifier, string value)
{
    JsonArray? properties = layer["GeneralPropertyGroup"]?["Properties"]?.AsArray();
    if (properties == null)
        return;

    foreach (JsonNode? property in properties)
    {
        if (property == null)
            continue;

        if (string.Equals(property?["Identifier"]?.GetValue<string>(), identifier, StringComparison.OrdinalIgnoreCase))
        {
            property!["Value"] = value;
            return;
        }
    }
}

static void SetLayerOpacity(JsonNode layer, string opacity)
{
    JsonArray? properties = layer["TransformPropertyGroup"]?["Properties"]?.AsArray();
    if (properties == null)
        return;

    foreach (JsonNode? property in properties)
    {
        if (property == null)
            continue;

        if (string.Equals(property?["Identifier"]?.GetValue<string>(), "Opacity", StringComparison.OrdinalIgnoreCase))
        {
            property!["Value"] = opacity;
            property!["KeyframesEnabled"] = false;
            property!["KeyframeEntities"] = new JsonArray();
            return;
        }
    }
}

static void SetSolidBrush(JsonNode layer, string color)
{
    JsonObject brushReference = new()
    {
        ["LayerBrushProviderId"] = "Artemis.Plugins.LayerBrushes.Color.ColorBrushProvider-92a9d6ba",
        ["BrushType"] = "SolidBrush"
    };

    JsonArray? generalProperties = layer["GeneralPropertyGroup"]?["Properties"]?.AsArray();
    if (generalProperties != null)
    {
        foreach (JsonNode? property in generalProperties)
        {
            string identifier = property?["Identifier"]?.GetValue<string>() ?? "";
            if (property == null)
                continue;

            property["Value"] = identifier switch
            {
                "BrushReference" => brushReference.ToJsonString(),
                "ShapeType" => "1",
                "BlendMode" => "0",
                "TransformMode" => "0",
                _ => property["Value"]?.GetValue<string>() ?? ""
            };
        }
    }

    layer["LayerBrush"] = new JsonObject
    {
        ["ProviderId"] = "Artemis.Plugins.LayerBrushes.Color.ColorBrushProvider-92a9d6ba",
        ["BrushType"] = "Artemis.Plugins.LayerBrushes.Color.SolidBrush",
        ["PropertyGroup"] = new JsonObject
        {
            ["Identifier"] = "Brush",
            ["Properties"] = new JsonArray
            {
                CreateProperty("ColorMode", "0"),
                CreateProperty("Color", JsonSerializer.Serialize(color)),
                CreateProperty("Colors", "[{\"Color\":\"#ffff0000\",\"Position\":0},{\"Color\":\"#ffff9900\",\"Position\":0.125},{\"Color\":\"#ffffff00\",\"Position\":0.25},{\"Color\":\"#ff00ff00\",\"Position\":0.375},{\"Color\":\"#ff00ff7e\",\"Position\":0.5},{\"Color\":\"#ff0078ff\",\"Position\":0.625},{\"Color\":\"#ff9e22ff\",\"Position\":0.75},{\"Color\":\"#ffff34ae\",\"Position\":0.875},{\"Color\":\"#ffff0000\",\"Position\":1}]"),
                CreateProperty("GradientPosition", "0"),
                CreateProperty("AnimationSpeed", "100")
            },
            ["PropertyGroups"] = new JsonArray()
        }
    };
}

static JsonObject CreateProperty(string identifier, string value)
{
    return new JsonObject
    {
        ["Identifier"] = identifier,
        ["Value"] = value,
        ["KeyframesEnabled"] = false,
        ["DataBinding"] = new JsonObject
        {
            ["IsEnabled"] = false,
            ["NodeScript"] = null
        },
        ["KeyframeEntities"] = new JsonArray()
    };
}
