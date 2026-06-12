using System.Diagnostics;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using HidSharp;

namespace Artemis.Plugins.DirectDevices;

public sealed class DirectValorantController : IDisposable
{
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "VALORANT",
        "Saved",
        "Logs",
        "ShooterGame.log");

    private static readonly string TestPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Artemis",
        "DirectValorantTest.txt");

    private static readonly Dictionary<string, AgentPalette> AgentPalettes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Wushu"] = new("Jett", new(95, 220, 255), new(235, 255, 255)),
        ["Thorne"] = new("Sage", new(30, 225, 175), new(225, 255, 245)),
        ["Sprinter"] = new("Neon", new(20, 110, 255), new(255, 235, 20)),
        ["BountyHunter"] = new("Fade", new(75, 70, 145), new(145, 255, 225)),
        ["Sarge"] = new("Brimstone", new(255, 92, 28), new(255, 205, 120)),
        ["Vampire"] = new("Reyna", new(210, 45, 255), new(255, 125, 210)),
        ["Clay"] = new("Raze", new(255, 112, 20), new(255, 235, 50)),
        ["Deadeye"] = new("Chamber", new(245, 190, 45), new(70, 105, 150)),
        ["Pandemic"] = new("Viper", new(75, 235, 55), new(210, 255, 65)),
        ["Gumshoe"] = new("Cypher", new(100, 195, 255), new(245, 225, 160)),
        ["Hunter"] = new("Sova", new(45, 125, 255), new(145, 220, 255)),
        ["Stealth"] = new("Yoru", new(55, 70, 255), new(210, 80, 255)),
        ["Rift"] = new("Astra", new(150, 75, 255), new(255, 160, 235)),
        ["AggroBot"] = new("Killjoy", new(255, 220, 20), new(105, 255, 115)),
        ["Grenadier"] = new("KAY/O", new(95, 85, 255), new(90, 235, 255)),
        ["Smonk"] = new("Breach", new(255, 115, 20), new(255, 215, 80))
    };

    private static readonly Dictionary<string, RgbColor> MapColors = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Ascent"] = new(115, 170, 215),
        ["Duality"] = new(220, 120, 60),
        ["Bind"] = new(220, 120, 60),
        ["Triad"] = new(95, 165, 105),
        ["Haven"] = new(95, 165, 105),
        ["Bonsai"] = new(210, 95, 145),
        ["Split"] = new(210, 95, 145),
        ["Port"] = new(80, 180, 230),
        ["Icebox"] = new(80, 180, 230),
        ["Foxtrot"] = new(60, 195, 165),
        ["Breeze"] = new(60, 195, 165),
        ["Canyon"] = new(195, 115, 55),
        ["Fracture"] = new(195, 115, 55),
        ["Pitt"] = new(105, 85, 190),
        ["Pearl"] = new(105, 85, 190),
        ["Jam"] = new(90, 170, 85),
        ["Lotus"] = new(90, 170, 85),
        ["Juliett"] = new(235, 95, 85),
        ["Sunset"] = new(235, 95, 85),
        ["Infinity"] = new(80, 105, 205),
        ["Abyss"] = new(80, 105, 205),
        ["Rook"] = new(155, 175, 105),
        ["Corrode"] = new(155, 175, 105)
    };

    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly Task _renderTask;
    private readonly object _stateSync = new();
    private readonly GameRearLightRole _upperRole;
    private readonly GameRearLightRole _lowerRole;
    private readonly int _frameDelayMs;
    private ValorantState _state = new();
    private long _logOffset;
    private DateTime _lastLogWriteUtc = DateTime.MinValue;
    private bool _logInitialized;

    public DirectValorantController(string? upperRole, string? lowerRole, int fps, WizLightAddresses wizAddresses)
    {
        _upperRole = GameRearLightRoles.Parse(upperRole);
        _lowerRole = GameRearLightRoles.Parse(lowerRole);
        _frameDelayMs = LightingFrameRate.ToDelayMilliseconds(LightingFrameRate.ClampGame(fps));
        _renderTask = Task.Run(() => RunRenderer(wizAddresses, _cancellationTokenSource.Token));
    }

    public void Dispose()
    {
        _cancellationTokenSource.Cancel();
        try
        {
            _renderTask.Wait(TimeSpan.FromSeconds(2));
        }
        catch
        {
            // Device handles are disposed by the renderer.
        }

        _cancellationTokenSource.Dispose();
    }

    private async Task RunRenderer(WizLightAddresses wizAddresses, CancellationToken cancellationToken)
    {
        using UdpClient studyClient = new();
        studyClient.Connect(wizAddresses.StudyEndpoint, 38899);
        using UdpClient lowerClient = new();
        lowerClient.Connect(wizAddresses.LowerEndpoint, 38899);
        using UdpClient upperClient = new();
        upperClient.Connect(wizAddresses.UpperEndpoint, 38899);
        using GameScreenSampler sampler = new();

        List<RazerValorantDevice> razerDevices = OpenRazerDevices();
        List<LenovoValorantDevice> lenovoDevices = OpenLenovoDevices();
        DateTime nextProcessCheckUtc = DateTime.MinValue;
        bool valorantRunning = false;
        int loop = 0;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                DateTime now = DateTime.UtcNow;
                if (now >= nextProcessCheckUtc)
                {
                    valorantRunning = IsValorantProcessRunning() || File.Exists(TestPath);
                    nextProcessCheckUtc = now.AddSeconds(1);
                    if (!valorantRunning)
                        _logInitialized = false;
                }

                bool testActive = File.Exists(TestPath);
                if (!valorantRunning || (!testActive && !IsValorantForeground()) || DirectLightingRuntimeState.IsCsActive)
                {
                    await Task.Delay(_frameDelayMs, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                DirectLightingRuntimeState.MarkValorantActive();
                ReadNewLogLines();
                ReadTestCommands();
                if (!testActive && sampler.Capture())
                    UpdateFromScreen(sampler);

                ValorantState snapshot;
                lock (_stateSync)
                    snapshot = _state.Clone();

                ValorantLightingFrame frame = CreateFrame(snapshot);
                SendWiz(studyClient, StudyColor(frame), 100);

                foreach (RazerValorantDevice device in razerDevices)
                {
                    try
                    {
                        device.Update(frame);
                    }
                    catch
                    {
                        // One failed HID update must not stop game lighting.
                    }
                }

                UpdateRearLight(upperClient, _upperRole, upper: true, frame);
                UpdateRearLight(lowerClient, _lowerRole, upper: false, frame);
                if (loop++ % 3 == 0)
                {
                    foreach (LenovoValorantDevice device in lenovoDevices)
                        device.TurnOff();
                }

                await Task.Delay(_frameDelayMs, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            foreach (RazerValorantDevice device in razerDevices)
                device.Dispose();
            foreach (LenovoValorantDevice device in lenovoDevices)
                device.Dispose();
        }
    }

    private void ReadNewLogLines()
    {
        try
        {
            if (!File.Exists(LogPath))
                return;

            FileInfo file = new(LogPath);
            if (!_logInitialized || file.Length < _logOffset || file.LastWriteTimeUtc < _lastLogWriteUtc)
            {
                InitializeLogState(file);
                return;
            }

            if (file.Length == _logOffset)
                return;

            using FileStream stream = new(LogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            stream.Seek(_logOffset, SeekOrigin.Begin);
            using StreamReader reader = new(stream, Encoding.UTF8, true);
            string? line;
            while ((line = reader.ReadLine()) != null)
                ProcessLogLine(line);

            _logOffset = stream.Position;
            _lastLogWriteUtc = file.LastWriteTimeUtc;
        }
        catch
        {
            _logInitialized = false;
        }
    }

    private void InitializeLogState(FileInfo file)
    {
        using FileStream stream = new(LogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        long start = Math.Max(0, stream.Length - (1024 * 1024));
        stream.Seek(start, SeekOrigin.Begin);
        using StreamReader reader = new(stream, Encoding.UTF8, true);
        if (start > 0)
            reader.ReadLine();

        string? latestAgent = null;
        string? latestMap = null;
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            string? agent = ParseAgentCode(line);
            if (agent != null)
                latestAgent = agent;
            string? map = ParseMapCode(line);
            if (map != null)
                latestMap = map;
        }

        lock (_stateSync)
        {
            if (latestAgent != null)
                _state.AgentCode = latestAgent;
            if (latestMap != null)
                _state.MapCode = latestMap;
        }

        _logOffset = stream.Position;
        _lastLogWriteUtc = file.LastWriteTimeUtc;
        _logInitialized = true;
    }

    private void ProcessLogLine(string line)
    {
        DateTime now = DateTime.UtcNow;
        string? agent = ParseAgentCode(line);
        string? map = ParseMapCode(line);

        lock (_stateSync)
        {
            if (agent != null)
                _state.AgentCode = agent;
            if (map != null)
                _state.MapCode = map;

            if (line.Contains("Opening WBP_Screen_PreRoundShopContainer", StringComparison.OrdinalIgnoreCase))
                _state.ShopOpen = true;
            else if (line.Contains("Closing WBP_Screen_PreRoundShopContainer", StringComparison.OrdinalIgnoreCase))
                _state.ShopOpen = false;

            if (line.Contains("BombInteractionBuff_C", StringComparison.OrdinalIgnoreCase))
                _state.LastPlantInteractionUtc = now;

            if (line.Contains("TimedBomb_C", StringComparison.OrdinalIgnoreCase) &&
                now - _state.LastPlantInteractionUtc < TimeSpan.FromSeconds(12) &&
                !_state.SpikePlanted)
            {
                StartSpikeTimer(_state, now);
            }

            if (line.Contains("AShooterGameState::OnRoundEnded", StringComparison.OrdinalIgnoreCase))
            {
                _state.LastRoundEndUtc = now;
                _state.SpikePlanted = false;
                _state.SpikeEndUtc = null;
            }

            if (line.Contains("Match Ended:", StringComparison.OrdinalIgnoreCase))
                _state.LastRoundEndUtc = now;

            if (!string.IsNullOrWhiteSpace(_state.AgentCode) &&
                line.Contains($"{_state.AgentCode}_PC_C", StringComparison.OrdinalIgnoreCase) &&
                (line.Contains("CastingBuff_C", StringComparison.OrdinalIgnoreCase) ||
                 line.Contains("Ability", StringComparison.OrdinalIgnoreCase)))
            {
                int slot = StableAbilitySlot(line);
                _state.LastAbilitySlot = slot;
                _state.LastAbilityUtc = now;
            }
        }
    }

    private void ReadTestCommands()
    {
        try
        {
            if (!File.Exists(TestPath))
                return;

            DateTime writeUtc = File.GetLastWriteTimeUtc(TestPath);
            lock (_stateSync)
            {
                if (writeUtc <= _state.LastTestCommandUtc)
                    return;

                _state.LastTestCommandUtc = writeUtc;
                DateTime now = DateTime.UtcNow;
                foreach (string raw in File.ReadAllLines(TestPath))
                {
                    string command = raw.Trim();
                    if (command.StartsWith("agent=", StringComparison.OrdinalIgnoreCase))
                        _state.AgentCode = command[6..].Trim();
                    else if (command.Equals("damage", StringComparison.OrdinalIgnoreCase))
                        _state.LastDamageUtc = now;
                    else if (command.Equals("heal", StringComparison.OrdinalIgnoreCase))
                        _state.LastHealUtc = now;
                    else if (command.Equals("plant", StringComparison.OrdinalIgnoreCase))
                        StartSpikeTimer(_state, now);
                    else if (command.Equals("win", StringComparison.OrdinalIgnoreCase))
                    {
                        _state.LastRoundResultUtc = now;
                        _state.RoundWon = true;
                    }
                    else if (command.Equals("lose", StringComparison.OrdinalIgnoreCase))
                    {
                        _state.LastRoundResultUtc = now;
                        _state.RoundWon = false;
                    }
                    else if (command.StartsWith("ability=", StringComparison.OrdinalIgnoreCase) &&
                             int.TryParse(command[8..], out int slot))
                    {
                        _state.LastAbilitySlot = Math.Clamp(slot, 0, 3);
                        _state.LastAbilityUtc = now;
                    }
                }
            }
        }
        catch
        {
        }
    }

    private void UpdateFromScreen(GameScreenSampler sampler)
    {
        DateTime now = DateTime.UtcNow;
        ScreenRegionStats leftEdge = sampler.Sample(0.00, 0.13, 0.12, 0.88);
        ScreenRegionStats rightEdge = sampler.Sample(0.87, 1.00, 0.12, 0.88);
        ScreenRegionStats healthHud = sampler.Sample(0.035, 0.18, 0.79, 0.98);
        ScreenRegionStats spikeHud = sampler.Sample(0.455, 0.545, 0.015, 0.115);
        ScreenRegionStats centerBanner = sampler.Sample(0.29, 0.71, 0.36, 0.62);

        lock (_stateSync)
        {
            double edgeRed = Math.Max(leftEdge.RedRatio, rightEdge.RedRatio);
            bool damageRise = edgeRed - _state.PreviousEdgeRedRatio >= 0.035;
            if (edgeRed >= 0.115 &&
                (damageRise || edgeRed >= 0.28) &&
                now - _state.LastDamageUtc > TimeSpan.FromMilliseconds(420))
                _state.LastDamageUtc = now;
            _state.PreviousEdgeRedRatio = edgeRed;

            bool healRise = healthHud.GreenRatio - _state.PreviousHealthGreenRatio >= 0.035;
            if (healthHud.GreenRatio >= 0.12 &&
                healRise &&
                now - _state.LastHealUtc > TimeSpan.FromMilliseconds(650))
                _state.LastHealUtc = now;
            _state.PreviousHealthGreenRatio = healthHud.GreenRatio;

            bool spikeVisible = spikeHud.RedRatio >= 0.16 && spikeHud.BrightRatio >= 0.045;
            if (spikeVisible)
            {
                _state.SpikeVisibleFrames++;
                _state.SpikeMissingFrames = 0;
                if (_state.SpikeVisibleFrames >= 3 && !_state.SpikePlanted)
                    StartSpikeTimer(_state, now);
            }
            else
            {
                _state.SpikeVisibleFrames = 0;
                _state.SpikeMissingFrames++;
                if (_state.SpikePlanted && _state.SpikeMissingFrames >= 18)
                {
                    _state.SpikePlanted = false;
                    _state.SpikeEndUtc = null;
                }
            }

            bool tealBanner = centerBanner.TealRatio >= 0.22 && centerBanner.BrightRatio >= 0.14;
            bool redBanner = centerBanner.RedRatio >= 0.22 && centerBanner.BrightRatio >= 0.14;
            if ((tealBanner || redBanner) && now - _state.LastRoundResultUtc > TimeSpan.FromSeconds(4))
            {
                _state.BannerFrames++;
                if (_state.BannerFrames >= 3)
                {
                    _state.LastRoundResultUtc = now;
                    _state.RoundWon = tealBanner;
                    _state.BannerFrames = 0;
                }
            }
            else if (!tealBanner && !redBanner)
            {
                _state.BannerFrames = 0;
            }

            for (int slot = 0; slot < 4; slot++)
            {
                double startX = 0.325 + (slot * 0.085);
                ScreenRegionStats ability = sampler.Sample(startX, startX + 0.07, 0.84, 0.985);
                double previous = _state.AbilityLuminance[slot];
                if (_state.AbilitySamplesReady && previous >= 0.24 && previous - ability.Luminance >= 0.13)
                {
                    _state.LastAbilitySlot = slot;
                    _state.LastAbilityUtc = now;
                }

                _state.AbilityLuminance[slot] = ability.Luminance;
            }

            _state.AbilitySamplesReady = true;
        }
    }

    private static void StartSpikeTimer(ValorantState state, DateTime now)
    {
        state.SpikePlanted = true;
        state.SpikeEndUtc = now.AddSeconds(45);
        state.LastSpikePlantUtc = now;
        state.SpikeMissingFrames = 0;
    }

    private static string? ParseAgentCode(string line)
    {
        const string marker = "Current character: Default__";
        int start = line.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (start < 0)
            return null;

        start += marker.Length;
        int end = line.IndexOf("_PC_C", start, StringComparison.OrdinalIgnoreCase);
        return end > start ? line[start..end] : null;
    }

    private static string? ParseMapCode(string line)
    {
        const string marker = "/Game/Maps/";
        int start = line.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (start < 0)
            return null;

        start += marker.Length;
        int end = line.IndexOfAny(new[] { '/', '.', ' ', '?', '\'' }, start);
        string code = (end > start ? line[start..end] : line[start..]).Trim();
        return MapColors.ContainsKey(code) ? code : null;
    }

    private static int StableAbilitySlot(string line)
    {
        if (line.Contains("Ultimate", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("_Ult", StringComparison.OrdinalIgnoreCase))
            return 3;

        uint hash = 2166136261;
        foreach (char character in line)
            hash = (hash ^ char.ToUpperInvariant(character)) * 16777619;
        return (int)(hash % 3);
    }

    private static bool IsValorantProcessRunning()
    {
        try
        {
            return Process.GetProcessesByName("VALORANT-Win64-Shipping").Length > 0;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsValorantForeground()
    {
        try
        {
            IntPtr foregroundWindow = GetForegroundWindow();
            if (foregroundWindow == IntPtr.Zero)
                return false;

            GetWindowThreadProcessId(foregroundWindow, out uint processId);
            using Process process = Process.GetProcessById((int)processId);
            return string.Equals(process.ProcessName, "VALORANT-Win64-Shipping", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static ValorantLightingFrame CreateFrame(ValorantState state)
    {
        DateTime now = DateTime.UtcNow;
        AgentPalette palette = AgentPalettes.TryGetValue(state.AgentCode ?? string.Empty, out AgentPalette known)
            ? known
            : new AgentPalette("Valorant", new RgbColor(255, 70, 85), new RgbColor(70, 225, 210));

        double? spikeSeconds = state.SpikeEndUtc.HasValue
            ? Math.Max(0, (state.SpikeEndUtc.Value - now).TotalSeconds)
            : null;
        double spikePulse = 0;
        if (state.SpikePlanted)
        {
            double seconds = spikeSeconds ?? 45;
            double hz = seconds <= 10 ? 5.5 : seconds <= 20 ? 3.2 : 1.6;
            spikePulse = 0.28 + (0.72 * ((Math.Sin(now.TimeOfDay.TotalSeconds * Math.PI * 2 * hz) + 1) / 2));
        }

        return new ValorantLightingFrame(
            palette,
            MapColors.TryGetValue(state.MapCode ?? string.Empty, out RgbColor mapColor)
                ? mapColor
                : new RgbColor(95, 85, 145),
            state.ShopOpen,
            state.SpikePlanted,
            spikeSeconds,
            spikePulse,
            Fade(now - state.LastDamageUtc, 0.75),
            Fade(now - state.LastHealUtc, 0.90),
            Fade(now - state.LastAbilityUtc, 1.15),
            state.LastAbilitySlot,
            Fade(now - state.LastSpikePlantUtc, 1.45),
            Fade(now - state.LastRoundResultUtc, 2.40),
            state.RoundWon,
            Fade(now - state.LastRoundEndUtc, 1.30));
    }

    private static RgbColor StudyColor(ValorantLightingFrame frame)
    {
        RgbColor color = frame.Palette.Primary;
        if (frame.ShopOpen)
            color = Blend(color, new RgbColor(225, 245, 255), 0.30);
        if (frame.SpikePlanted)
            color = Blend(color, SpikeColor(frame), frame.SpikePulse * 0.58);

        color = Blend(color, new RgbColor(255, 0, 0), frame.DamageIntensity * 0.94);
        color = Blend(color, new RgbColor(25, 255, 120), frame.HealIntensity * 0.78);
        color = Blend(color, frame.Palette.Accent, frame.AbilityIntensity * 0.72);
        color = Blend(color, new RgbColor(255, 70, 15), frame.PlantIntensity * 0.84);
        color = Blend(color, frame.RoundWon ? new RgbColor(20, 255, 180) : new RgbColor(255, 28, 25), frame.ResultIntensity * 0.92);
        return color;
    }

    private static RgbColor SpikeColor(ValorantLightingFrame frame)
    {
        double seconds = frame.SpikeSeconds ?? 45;
        if (seconds <= 10)
            return Blend(new RgbColor(220, 0, 0), new RgbColor(255, 255, 255), frame.SpikePulse);
        if (seconds <= 20)
            return Blend(new RgbColor(190, 0, 0), new RgbColor(255, 55, 0), frame.SpikePulse);
        return Blend(new RgbColor(130, 20, 0), new RgbColor(255, 105, 0), frame.SpikePulse);
    }

    private static double Fade(TimeSpan age, double durationSeconds)
    {
        if (age < TimeSpan.Zero || age.TotalSeconds > durationSeconds)
            return 0;
        return 1 - (age.TotalSeconds / durationSeconds);
    }

    private static List<RazerValorantDevice> OpenRazerDevices()
    {
        (DirectDeviceDefinition Definition, RazerValorantRole Role)[] definitions =
        {
            (DirectDeviceDefinition.CreateRazerBlackWidowV3(), RazerValorantRole.Keyboard),
            (DirectDeviceDefinition.CreateRazerDeathAdderV2ProWireless(), RazerValorantRole.Mouse),
            (DirectDeviceDefinition.CreateRazerMouseDockChroma(), RazerValorantRole.Dock)
        };

        List<RazerValorantDevice> devices = new();
        foreach ((DirectDeviceDefinition definition, RazerValorantRole role) in definitions)
        {
            HidDevice? hidDevice = HidDeviceLocator.Find(definition.HidTargets).FirstOrDefault();
            if (hidDevice != null && definition.Razer != null)
                devices.Add(new RazerValorantDevice(hidDevice.DevicePath, definition.Razer, role));
        }

        return devices;
    }

    private static List<LenovoValorantDevice> OpenLenovoDevices()
    {
        DirectDeviceDefinition definition = DirectDeviceDefinition.CreateLenovoLegion5();
        return HidDeviceLocator.Find(definition.HidTargets).Select(device => new LenovoValorantDevice(device)).ToList();
    }

    private static void SendWiz(UdpClient client, RgbColor color, byte dimming)
    {
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(new
        {
            method = "setPilot",
            @params = new
            {
                state = true,
                r = color.R,
                g = color.G,
                b = color.B,
                c = 0,
                w = 0,
                dimming
            }
        });
        client.Send(payload, payload.Length);
    }

    private static void SendWizOff(UdpClient client)
    {
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(new
        {
            method = "setPilot",
            @params = new { state = false }
        });
        client.Send(payload, payload.Length);
    }

    private static void UpdateRearLight(UdpClient client, GameRearLightRole role, bool upper, ValorantLightingFrame frame)
    {
        if (role == GameRearLightRole.Off)
        {
            SendWizOff(client);
            return;
        }

        double objectiveStrength = Math.Max(
            frame.SpikePlanted ? Math.Max(0.18, frame.SpikePulse * 0.45) : 0,
            Math.Max(frame.PlantIntensity, frame.ResultIntensity));
        double healthStrength = Math.Max(frame.DamageIntensity, frame.HealIntensity);
        double anyEvent = Math.Max(
            Math.Max(objectiveStrength, healthStrength),
            frame.AbilityIntensity);

        if (role == GameRearLightRole.ObjectiveAlerts && objectiveStrength <= 0.03)
        {
            SendWizOff(client);
            return;
        }

        RgbColor color = role switch
        {
            GameRearLightRole.HealthDamage => new RgbColor(30, 210, 105),
            GameRearLightRole.TeamAgentMood => Blend(frame.Palette.Primary, frame.Palette.Accent, upper ? 0.12 : 0.28),
            GameRearLightRole.MapMood => frame.MapColor,
            GameRearLightRole.FullGameMix => Blend(frame.MapColor, frame.Palette.Primary, 0.60),
            _ => new RgbColor(255, 70, 10)
        };

        if (role is GameRearLightRole.ObjectiveAlerts or GameRearLightRole.FullGameMix)
        {
            if (frame.SpikePlanted)
                color = Blend(color, SpikeColor(frame), 0.72 + (frame.SpikePulse * 0.22));
            color = Blend(color, new RgbColor(255, 70, 10), frame.PlantIntensity * 0.78);
            color = Blend(
                color,
                frame.RoundWon ? new RgbColor(20, 255, 180) : new RgbColor(255, 20, 20),
                frame.ResultIntensity * 0.84);
        }

        if (role is GameRearLightRole.HealthDamage or GameRearLightRole.FullGameMix)
        {
            color = Blend(color, new RgbColor(255, 0, 0), frame.DamageIntensity * 0.82);
            color = Blend(color, new RgbColor(20, 255, 115), frame.HealIntensity * 0.72);
        }

        if (role == GameRearLightRole.FullGameMix)
            color = Blend(color, frame.Palette.Accent, frame.AbilityIntensity * 0.42);

        double relevantEvent = role switch
        {
            GameRearLightRole.ObjectiveAlerts => objectiveStrength,
            GameRearLightRole.HealthDamage => healthStrength,
            GameRearLightRole.FullGameMix => anyEvent,
            _ => 0
        };
        byte dimming = relevantEvent > 0.03 ? role.EventDimming(upper) : role.BaseDimming(upper);
        SendWiz(client, color, dimming);
    }

    private static RgbColor Blend(RgbColor from, RgbColor to, double amount)
    {
        amount = Math.Clamp(amount, 0, 1);
        return new RgbColor(
            ClampToByte((from.R * (1 - amount)) + (to.R * amount)),
            ClampToByte((from.G * (1 - amount)) + (to.G * amount)),
            ClampToByte((from.B * (1 - amount)) + (to.B * amount)));
    }

    private static byte ClampToByte(double value)
    {
        return (byte)Math.Clamp((int)Math.Round(value), 0, 255);
    }

    private sealed class RazerValorantDevice : IDisposable
    {
        private readonly WindowsHidFeatureSender _sender;
        private readonly RazerMatrixOptions _options;
        private readonly RazerValorantRole _role;

        public RazerValorantDevice(string devicePath, RazerMatrixOptions options, RazerValorantRole role)
        {
            _sender = new WindowsHidFeatureSender(devicePath);
            _options = options;
            _role = role;
        }

        public void Update(ValorantLightingFrame frame)
        {
            if (_role == RazerValorantRole.Keyboard)
                UpdateKeyboard(frame);
            else
                UpdateSingle(frame);
        }

        private void UpdateKeyboard(ValorantLightingFrame frame)
        {
            int rows = _options.Rows;
            int columns = _options.Columns;
            RgbColor[,] colors = new RgbColor[rows, columns];
            RgbColor baseColor = Blend(new RgbColor(2, 3, 7), frame.Palette.Primary, frame.ShopOpen ? 0.27 : 0.20);
            for (int row = 0; row < rows; row++)
            {
                for (int column = 0; column < columns; column++)
                    colors[row, column] = baseColor;
            }

            ApplyAbilityKeys(colors, frame);
            if (frame.SpikePlanted)
                ApplySpikeTimer(colors, frame);
            ApplyEventLayers(colors, frame);
            WriteMatrix(colors);
        }

        private static void ApplyAbilityKeys(RgbColor[,] colors, ValorantLightingFrame frame)
        {
            int columns = colors.GetLength(1);
            int rows = colors.GetLength(0);
            int[] centers = { 4, 8, 12, 16 };
            for (int slot = 0; slot < centers.Length; slot++)
            {
                int center = Math.Min(columns - 1, centers[slot]);
                for (int row = Math.Max(1, rows - 4); row < Math.Max(1, rows - 1); row++)
                {
                    for (int column = Math.Max(0, center - 1); column <= Math.Min(columns - 1, center + 1); column++)
                        colors[row, column] = Blend(colors[row, column], frame.Palette.Accent, 0.48);
                }
            }
        }

        private static void ApplySpikeTimer(RgbColor[,] colors, ValorantLightingFrame frame)
        {
            int columns = colors.GetLength(1);
            double seconds = frame.SpikeSeconds ?? 45;
            int lit = Math.Clamp((int)Math.Ceiling(columns * (seconds / 45.0)), 0, columns);
            RgbColor active = SpikeColor(frame);
            for (int column = 0; column < columns; column++)
                colors[0, column] = column < lit ? active : new RgbColor(40, 0, 0);
        }

        private static void ApplyEventLayers(RgbColor[,] colors, ValorantLightingFrame frame)
        {
            int rows = colors.GetLength(0);
            int columns = colors.GetLength(1);
            double center = (columns - 1) / 2.0;
            int abilityCenter = Math.Min(columns - 1, new[] { 4, 8, 12, 16 }[Math.Clamp(frame.AbilitySlot, 0, 3)]);

            for (int row = 0; row < rows; row++)
            {
                for (int column = 0; column < columns; column++)
                {
                    RgbColor color = colors[row, column];
                    double edgeDistance = Math.Min(column, columns - 1 - column) / Math.Max(1.0, center);
                    double abilityDistance = Math.Abs(column - abilityCenter) / Math.Max(1.0, center);
                    double damage = frame.DamageIntensity * Math.Clamp(1.24 - edgeDistance, 0, 1);
                    double ability = frame.AbilityIntensity * Math.Clamp(1.10 - abilityDistance, 0, 1);
                    double result = frame.ResultIntensity * (0.45 + (row / Math.Max(1.0, rows - 1) * 0.55));

                    color = Blend(color, new RgbColor(255, 0, 0), damage * 0.96);
                    color = Blend(color, new RgbColor(20, 255, 115), frame.HealIntensity * 0.78);
                    color = Blend(color, frame.Palette.Accent, ability * 0.90);
                    color = Blend(color, new RgbColor(255, 70, 10), frame.PlantIntensity * Math.Clamp(1.25 - Math.Abs(column - center) / Math.Max(1.0, center), 0, 1));
                    color = Blend(color, frame.RoundWon ? new RgbColor(20, 255, 180) : new RgbColor(255, 20, 20), result * 0.92);
                    colors[row, column] = color;
                }
            }
        }

        private void UpdateSingle(ValorantLightingFrame frame)
        {
            RgbColor color = frame.Palette.Primary;
            if (frame.SpikePlanted)
                color = SpikeColor(frame);
            color = Blend(color, new RgbColor(255, 0, 0), frame.DamageIntensity * 0.92);
            color = Blend(color, new RgbColor(25, 255, 120), frame.HealIntensity * 0.74);
            color = Blend(color, frame.Palette.Accent, frame.AbilityIntensity * 0.82);
            color = Blend(color, frame.RoundWon ? new RgbColor(20, 255, 180) : new RgbColor(255, 20, 20), frame.ResultIntensity * 0.90);
            WriteSolid(color);
        }

        private void WriteSolid(RgbColor color)
        {
            RgbColor[,] colors = new RgbColor[_options.Rows, _options.Columns];
            for (int row = 0; row < _options.Rows; row++)
            {
                for (int column = 0; column < _options.Columns; column++)
                    colors[row, column] = color;
            }
            WriteMatrix(colors);
        }

        private void WriteMatrix(RgbColor[,] colors)
        {
            for (int row = 0; row < _options.Rows; row++)
            {
                byte[] rgb = new byte[_options.Columns * 3];
                for (int column = 0; column < _options.Columns; column++)
                {
                    int offset = column * 3;
                    rgb[offset] = colors[row, column].R;
                    rgb[offset + 1] = colors[row, column].G;
                    rgb[offset + 2] = colors[row, column].B;
                }

                _sender.SetFeature(RazerReports.CreateCustomFrameExtended(
                    _options.TransactionId,
                    (byte)row,
                    0,
                    (byte)(_options.Columns - 1),
                    rgb));
                Thread.Sleep(1);
            }

            _sender.SetFeature(RazerReports.CreateCustomModeExtended(_options.TransactionId));
        }

        public void Dispose()
        {
            _sender.Dispose();
        }
    }

    private sealed class LenovoValorantDevice : IDisposable
    {
        private readonly HidDevice _device;
        private HidStream? _stream;

        public LenovoValorantDevice(HidDevice device)
        {
            _device = device;
        }

        public void TurnOff()
        {
            try
            {
                byte[] report = new byte[33];
                report[0] = 0xCC;
                report[1] = 0x16;
                report[2] = 0x01;
                report[3] = 0x01;
                report[4] = 0x02;
                GetStream().SetFeature(report);
            }
            catch
            {
                ResetStream();
            }
        }

        private HidStream GetStream()
        {
            return _stream ??= _device.Open();
        }

        private void ResetStream()
        {
            _stream?.Dispose();
            _stream = null;
        }

        public void Dispose()
        {
            ResetStream();
        }
    }

    private sealed class ValorantState
    {
        public string? AgentCode { get; set; } = "Wushu";
        public string? MapCode { get; set; }
        public bool ShopOpen { get; set; }
        public bool SpikePlanted { get; set; }
        public DateTime? SpikeEndUtc { get; set; }
        public DateTime LastPlantInteractionUtc { get; set; } = DateTime.MinValue;
        public DateTime LastSpikePlantUtc { get; set; } = DateTime.MinValue;
        public DateTime LastDamageUtc { get; set; } = DateTime.MinValue;
        public DateTime LastHealUtc { get; set; } = DateTime.MinValue;
        public DateTime LastAbilityUtc { get; set; } = DateTime.MinValue;
        public int LastAbilitySlot { get; set; }
        public DateTime LastRoundEndUtc { get; set; } = DateTime.MinValue;
        public DateTime LastRoundResultUtc { get; set; } = DateTime.MinValue;
        public bool RoundWon { get; set; }
        public int SpikeVisibleFrames { get; set; }
        public int SpikeMissingFrames { get; set; }
        public int BannerFrames { get; set; }
        public double[] AbilityLuminance { get; } = new double[4];
        public bool AbilitySamplesReady { get; set; }
        public DateTime LastTestCommandUtc { get; set; } = DateTime.MinValue;
        public double PreviousEdgeRedRatio { get; set; }
        public double PreviousHealthGreenRatio { get; set; }

        public ValorantState Clone()
        {
            ValorantState clone = (ValorantState)MemberwiseClone();
            Array.Copy(AbilityLuminance, clone.AbilityLuminance, AbilityLuminance.Length);
            return clone;
        }
    }

    private readonly record struct AgentPalette(string Name, RgbColor Primary, RgbColor Accent);

    private readonly record struct ValorantLightingFrame(
        AgentPalette Palette,
        RgbColor MapColor,
        bool ShopOpen,
        bool SpikePlanted,
        double? SpikeSeconds,
        double SpikePulse,
        double DamageIntensity,
        double HealIntensity,
        double AbilityIntensity,
        int AbilitySlot,
        double PlantIntensity,
        double ResultIntensity,
        bool RoundWon,
        double RoundEndIntensity);

    private readonly record struct RgbColor(byte R, byte G, byte B);

    private enum RazerValorantRole
    {
        Keyboard,
        Mouse,
        Dock
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr windowHandle, out uint processId);
}
