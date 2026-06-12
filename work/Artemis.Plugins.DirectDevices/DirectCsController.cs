using System.Globalization;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using HidSharp;

namespace Artemis.Plugins.DirectDevices;

public sealed class DirectCsController : IDisposable
{
    private const int Port = 9697;
    private static readonly TimeSpan CsActiveHold = TimeSpan.FromSeconds(18);
    private static readonly RgbColor CtStudyColor = new(0, 115, 255);
    private static readonly RgbColor TStudyColor = new(255, 118, 0);
    private static readonly RgbColor MenuStudyColor = new(110, 42, 255);

    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly Task _serverTask;
    private readonly Task _renderTask;
    private readonly object _stateSync = new();
    private readonly GameRearLightRole _upperRole;
    private readonly GameRearLightRole _lowerRole;
    private readonly CsEventTuning _tuning;
    private readonly int _frameDelayMs;
    private CsGameState _state = new();

    public DirectCsController(
        string? upperRole,
        string? lowerRole,
        int fps,
        WizLightAddresses wizAddresses,
        CsEventTuning tuning,
        string? numpadLayout)
    {
        _upperRole = GameRearLightRoles.Parse(upperRole);
        _lowerRole = GameRearLightRoles.Parse(lowerRole);
        _tuning = tuning;
        _frameDelayMs = LightingFrameRate.ToDelayMilliseconds(LightingFrameRate.ClampGame(fps));
        _serverTask = Task.Run(() => RunServer(_cancellationTokenSource.Token));
        _renderTask = Task.Run(() => RunRenderer(wizAddresses, _cancellationTokenSource.Token));
    }

    public void Dispose()
    {
        _cancellationTokenSource.Cancel();
        try
        {
            Task.WaitAll(new[] { _serverTask, _renderTask }, TimeSpan.FromSeconds(2));
        }
        catch
        {
            // Shutdown is best-effort; device handles are disposed in the renderer.
        }

        _cancellationTokenSource.Dispose();
    }

    private async Task RunServer(CancellationToken cancellationToken)
    {
        TcpListener listener = new(IPAddress.Loopback, Port);
        try
        {
            listener.Start();
            using CancellationTokenRegistration registration = cancellationToken.Register(listener.Stop);

            while (!cancellationToken.IsCancellationRequested)
            {
                TcpClient client = await listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
                _ = Task.Run(() => HandleClient(client, cancellationToken), cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (SocketException) when (cancellationToken.IsCancellationRequested)
        {
        }
        finally
        {
            listener.Stop();
        }
    }

    private async Task HandleClient(TcpClient client, CancellationToken cancellationToken)
    {
        await using NetworkStream stream = client.GetStream();
        using (client)
        {
            try
            {
                byte[] request = await ReadHttpRequest(stream, cancellationToken).ConfigureAwait(false);
                int headerEnd = FindHeaderEnd(request, request.Length);
                if (headerEnd >= 0)
                {
                    byte[] body = ExtractBody(request, headerEnd);
                    if (body.Length > 0)
                        UpdateState(Encoding.UTF8.GetString(body));
                }

                byte[] response = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 2\r\nConnection: close\r\n\r\nOK");
                await stream.WriteAsync(response, cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                try
                {
                    byte[] response = Encoding.ASCII.GetBytes("HTTP/1.1 204 No Content\r\nContent-Length: 0\r\nConnection: close\r\n\r\n");
                    await stream.WriteAsync(response, cancellationToken).ConfigureAwait(false);
                }
                catch
                {
                }
            }
        }
    }

    private static async Task<byte[]> ReadHttpRequest(NetworkStream stream, CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[8192];
        using MemoryStream memory = new();
        int headerEnd = -1;
        int contentLength = 0;

        while (headerEnd < 0)
        {
            int read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read <= 0)
                break;

            memory.Write(buffer, 0, read);
            byte[] current = memory.ToArray();
            headerEnd = FindHeaderEnd(current, current.Length);
            if (headerEnd >= 0)
                contentLength = ParseContentLength(current, headerEnd);

            if (memory.Length > 1024 * 1024)
                break;
        }

        while (headerEnd >= 0 && memory.Length < headerEnd + 4 + contentLength)
        {
            int read = await stream.ReadAsync(buffer.AsMemory(0, Math.Min(buffer.Length, headerEnd + 4 + contentLength - (int)memory.Length)), cancellationToken).ConfigureAwait(false);
            if (read <= 0)
                break;

            memory.Write(buffer, 0, read);
        }

        return memory.ToArray();
    }

    private static int FindHeaderEnd(byte[] bytes, int length)
    {
        for (int i = 3; i < length; i++)
        {
            if (bytes[i - 3] == '\r' && bytes[i - 2] == '\n' && bytes[i - 1] == '\r' && bytes[i] == '\n')
                return i - 3;
        }

        return -1;
    }

    private static int ParseContentLength(byte[] request, int headerEnd)
    {
        string headers = Encoding.ASCII.GetString(request, 0, headerEnd);
        foreach (string line in headers.Split("\r\n", StringSplitOptions.RemoveEmptyEntries))
        {
            int colon = line.IndexOf(':');
            if (colon < 0)
                continue;

            string name = line[..colon].Trim();
            string value = line[(colon + 1)..].Trim();
            if (string.Equals(name, "Content-Length", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int length))
                return Math.Clamp(length, 0, 1024 * 1024);
        }

        return 0;
    }

    private static byte[] ExtractBody(byte[] request, int headerEnd)
    {
        int contentLength = ParseContentLength(request, headerEnd);
        int bodyStart = headerEnd + 4;
        int available = Math.Max(0, request.Length - bodyStart);
        int length = Math.Min(contentLength, available);
        byte[] body = new byte[length];
        Array.Copy(request, bodyStart, body, 0, length);
        return body;
    }

    private void UpdateState(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        CsGameState next;
        lock (_stateSync)
            next = _state.Clone();

        DateTime now = DateTime.UtcNow;
        JsonElement root = document.RootElement;
        JsonElement player = GetProperty(root, "player");
        JsonElement map = GetProperty(root, "map");
        JsonElement round = GetProperty(root, "round");
        JsonElement bomb = GetProperty(root, "bomb");
        JsonElement phaseCountdowns = GetProperty(root, "phase_countdowns");
        JsonElement allPlayers = GetProperty(root, "allplayers");

        next.LastUpdateUtc = now;
        next.Activity = GetString(player, "activity") ?? next.Activity;
        next.Team = GetString(player, "team") ?? next.Team;
        next.MapName = GetString(map, "name") ?? next.MapName;

        JsonElement state = GetProperty(player, "state");
        int? health = GetInt(state, "health");
        if (health.HasValue)
        {
            if (next.Health.HasValue && health.Value < next.Health.Value && health.Value > 0)
                next.LastDamageUtc = now;

            next.Health = health;
        }

        next.Armor = GetInt(state, "armor") ?? next.Armor;
        next.HasHelmet = GetBool(state, "helmet") ?? next.HasHelmet;
        next.HasDefuseKit = GetBool(state, "defusekit") ?? next.HasDefuseKit;
        next.FlashAmount = Math.Clamp(GetInt(state, "flashed") ?? next.FlashAmount, 0, 255);
        next.SmokeAmount = Math.Clamp(GetInt(state, "smoked") ?? next.SmokeAmount, 0, 255);
        next.BurningAmount = Math.Clamp(GetInt(state, "burning") ?? next.BurningAmount, 0, 255);

        int? roundKills = GetInt(state, "round_kills");
        int? roundHeadshots = GetInt(state, "round_killhs");
        int? roundDamage = GetInt(state, "round_totaldmg");
        if (roundKills.HasValue)
        {
            if (roundKills.Value > next.RoundKills)
                next.LastKillUtc = now;
            next.RoundKills = Math.Max(0, roundKills.Value);
        }

        if (roundHeadshots.HasValue)
        {
            if (roundHeadshots.Value > next.RoundHeadshots)
                next.LastHeadshotUtc = now;
            next.RoundHeadshots = Math.Max(0, roundHeadshots.Value);
        }

        if (roundDamage.HasValue)
        {
            if (roundDamage.Value > next.RoundTotalDamage &&
                now - next.LastGrenadeThrowUtc <= TimeSpan.FromSeconds(5))
            {
                next.LastGrenadeDamageUtc = now;
            }

            next.RoundTotalDamage = Math.Max(0, roundDamage.Value);
        }

        JsonElement matchStats = GetProperty(player, "match_stats");
        int? kills = GetInt(matchStats, "kills");
        if (kills.HasValue)
        {
            if (next.Kills.HasValue && kills.Value > next.Kills.Value)
                next.LastKillUtc = now;

            next.Kills = kills;
        }

        int? deaths = GetInt(matchStats, "deaths");
        if (deaths.HasValue)
        {
            if (next.Deaths.HasValue && deaths.Value > next.Deaths.Value)
                next.LastDeathUtc = now;

            next.Deaths = deaths;
        }

        int? mvps = GetInt(matchStats, "mvps");
        UpdateMvpState(next, mvps, now);

        UpdateWeapons(next, player, now);
        UpdateRoundAndBombState(next, now, round, bomb, phaseCountdowns);
        UpdateClutchState(next, now, allPlayers);

        if (next.Health == 0)
            next.LastDeathUtc = now;

        lock (_stateSync)
            _state = next;

        DirectLightingRuntimeState.MarkCsActive();
    }

    private static void UpdateMvpState(CsGameState state, int? mvps, DateTime now)
    {
        if (!mvps.HasValue)
            return;

        if (state.Mvps.HasValue && mvps.Value > state.Mvps.Value)
            state.LastMvpUtc = now;

        state.Mvps = mvps;
    }

    private async Task RunRenderer(WizLightAddresses wizAddresses, CancellationToken cancellationToken)
    {
        using UdpClient studyClient = new();
        studyClient.Connect(wizAddresses.StudyEndpoint, 38899);
        using UdpClient lowerClient = new();
        lowerClient.Connect(wizAddresses.LowerEndpoint, 38899);
        using UdpClient upperClient = new();
        upperClient.Connect(wizAddresses.UpperEndpoint, 38899);

        List<RazerCsDevice> razerDevices = OpenRazerDevices(_tuning);
        List<LenovoCsDevice> lenovoDevices = OpenLenovoDevices();
        int loop = 0;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                CsGameState snapshot;
                lock (_stateSync)
                    snapshot = _state.Clone();

                bool hasRecentState = DateTime.UtcNow - snapshot.LastUpdateUtc < CsActiveHold;
                bool csRunning = IsCsProcessRunning();
                if (hasRecentState || csRunning)
                {
                    DirectLightingRuntimeState.MarkCsActive();

                    CsLightingFrame frame = CreateLightingFrame(snapshot, _tuning);
                    RgbColor studyColor = GetStudyColor(snapshot, frame, _tuning);
                    SendStudyWiz(studyClient, studyColor, frame, _tuning);
                    foreach (RazerCsDevice razerDevice in razerDevices)
                    {
                        try
                        {
                            razerDevice.Update(frame);
                        }
                        catch
                        {
                            // Keep the CS renderer alive even if a HID write fails once.
                        }
                    }

                    UpdateRearLight(upperClient, _upperRole, upper: true, frame, _tuning);
                    UpdateRearLight(lowerClient, _lowerRole, upper: false, frame, _tuning);
                    if (frame.BigRoomIntensity > 0.04 || frame.IsClutch)
                    {
                        foreach (LenovoCsDevice lenovoDevice in lenovoDevices)
                            lenovoDevice.Update(frame);
                    }
                    else if (loop++ % 3 == 0)
                    {
                        foreach (LenovoCsDevice lenovoDevice in lenovoDevices)
                            lenovoDevice.TurnOff();
                    }
                }

                await Task.Delay(_frameDelayMs, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            foreach (RazerCsDevice razerDevice in razerDevices)
                razerDevice.Dispose();

            foreach (LenovoCsDevice lenovoDevice in lenovoDevices)
                lenovoDevice.Dispose();
        }
    }

    private static bool IsCsProcessRunning()
    {
        try
        {
            return Process.GetProcessesByName("cs2").Length > 0;
        }
        catch
        {
            return false;
        }
    }

    private static List<RazerCsDevice> OpenRazerDevices(CsEventTuning tuning)
    {
        (DirectDeviceDefinition Definition, RazerCsRole Role)[] definitions =
        {
            (DirectDeviceDefinition.CreateRazerBlackWidowV3(), RazerCsRole.Keyboard),
            (DirectDeviceDefinition.CreateRazerDeathAdderV2ProWireless(), RazerCsRole.Mouse),
            (DirectDeviceDefinition.CreateRazerMouseDockChroma(), RazerCsRole.Dock)
        };

        List<RazerCsDevice> devices = new();
        foreach ((DirectDeviceDefinition definition, RazerCsRole role) in definitions)
        {
            HidDevice? hidDevice = HidDeviceLocator.Find(definition.HidTargets).FirstOrDefault();
            if (hidDevice != null && definition.Razer != null)
                devices.Add(new RazerCsDevice(hidDevice.DevicePath, definition.Razer, role, tuning));
        }

        return devices;
    }

    private static List<LenovoCsDevice> OpenLenovoDevices()
    {
        DirectDeviceDefinition definition = DirectDeviceDefinition.CreateLenovoLegion5();
        return HidDeviceLocator.Find(definition.HidTargets).Select(device => new LenovoCsDevice(device)).ToList();
    }

    private static RgbColor GetStudyColor(CsGameState state, CsLightingFrame frame, CsEventTuning tuning)
    {
        RgbColor color = state.Team?.ToUpperInvariant() switch
        {
            "CT" => CtStudyColor,
            "T" => TStudyColor,
            _ => MenuStudyColor
        };

        if (frame.BombResolutionIntensity > 0.04)
            return frame.BigRoomColor;

        if (frame.MvpIntensity > 0.04)
            return frame.BigRoomColor;

        if (frame.FlashIntensity > 0.02)
            color = Blend(color, new RgbColor(210, 230, 255), Tuned(frame.FlashIntensity, tuning.Flash) * 0.84);

        if (frame.BurningIntensity > 0.02)
        {
            double flame = 0.55 + (0.45 * ((Math.Sin(DateTime.UtcNow.TimeOfDay.TotalSeconds * Math.PI * 7.2) + 1) / 2));
            color = Blend(color, new RgbColor(255, 62, 0), Tuned(frame.BurningIntensity, tuning.Fire) * flame * 0.94);
        }
        else if (frame.SmokeIntensity > 0.02)
        {
            color = Blend(color, new RgbColor(150, 175, 185), Tuned(frame.SmokeIntensity, tuning.Smoke) * 0.30);
        }

        if (frame.BombPlanted)
            color = Blend(color, RazerCsDevice.GetBombTimerColor(frame), frame.BombPulse * 0.52 * tuning.Bomb);

        if (frame.IsClutch)
            color = Blend(color, frame.BigRoomColor, frame.ClutchPulse * 0.42 * tuning.Clutch);

        color = Blend(color, new RgbColor(255, 110, 0), Tuned(frame.BombPlantIntensity, tuning.Bomb) * 0.82);
        color = Blend(color, frame.BombResolutionColor, Tuned(frame.BombResolutionIntensity, tuning.Bomb) * 0.92);
        color = Blend(color, frame.RoundWinColor, Tuned(frame.RoundWinIntensity, tuning.Bomb) * 0.90);
        color = Blend(color, frame.BigRoomColor, frame.BigRoomIntensity * 0.96);
        color = Blend(color, new RgbColor(255, 0, 0), Tuned(frame.DamageIntensity, tuning.Impact) * 0.88);
        color = Blend(color, new RgbColor(255, 235, 170), frame.KillIntensity * 0.82);
        color = Blend(color, new RgbColor(255, 242, 195), frame.HeadshotIntensity * 0.96);
        color = Blend(color, new RgbColor(95, 0, 0), Tuned(frame.DeathIntensity, tuning.Death) * 0.94);
        return color;
    }

    private static CsLightingFrame CreateLightingFrame(CsGameState state, CsEventTuning tuning)
    {
        DateTime now = DateTime.UtcNow;
        double damage = Fade(now - state.LastDamageUtc, 0.55);
        double kill = Fade(now - state.LastKillUtc, 0.70);
        double headshot = Fade(now - state.LastHeadshotUtc, 0.85);
        double death = Fade(now - state.LastDeathUtc, 2.50);
        double grenadeThrow = Fade(now - state.LastGrenadeThrowUtc, 0.65);
        double grenadeHit = Fade(now - state.LastGrenadeDamageUtc, 0.80);
        double bombPlant = Fade(now - state.LastBombPlantUtc, 1.20);
        double bombResolution = Fade(now - state.LastBombResolutionUtc, 3.40);
        double roundWin = Fade(now - state.LastRoundWinUtc, 2.30);
        double clutchStart = Fade(now - state.LastClutchStartUtc, 1.70);
        double clutchProgress = Fade(now - state.LastClutchProgressUtc, 1.10);
        double clutchWin = Fade(now - state.LastClutchWinUtc, 2.80);
        double mvp = Tuned(Fade(now - state.LastMvpUtc, 4.80), tuning.Clutch);
        double ace = state.RoundKills >= 5 ? kill : 0;
        double? bombSeconds = state.BombEndUtc.HasValue
            ? Math.Max(0, (state.BombEndUtc.Value - now).TotalSeconds)
            : state.BombSecondsRemaining;
        double? defuseSeconds = state.DefuseEndUtc.HasValue
            ? Math.Max(0, (state.DefuseEndUtc.Value - now).TotalSeconds)
            : state.DefuseSecondsRemaining;
        double? roundSeconds = state.RoundSecondsRemaining;
        bool defusing = string.Equals(state.BombPhase, "defuse", StringComparison.OrdinalIgnoreCase);
        bool defuseWillSucceed = !defusing || !state.BombEndUtc.HasValue || !state.DefuseEndUtc.HasValue || state.DefuseEndUtc.Value <= state.BombEndUtc.Value;

        double bombPulse = 0;
        if (state.BombPlanted)
        {
            double seconds = bombSeconds ?? defuseSeconds ?? 40;
            double hz = seconds <= 5 ? 7.0 : seconds <= 10 ? 4.8 : seconds <= 20 ? 2.8 : 1.4;
            bombPulse = 0.35 + (0.65 * ((Math.Sin(now.TimeOfDay.TotalSeconds * Math.PI * 2 * hz) + 1) / 2));
        }
        else if (roundSeconds.HasValue && roundSeconds.Value <= 10)
        {
            double hz = roundSeconds.Value <= 5 ? 3.0 : 1.8;
            bombPulse = 0.25 + (0.45 * ((Math.Sin(now.TimeOfDay.TotalSeconds * Math.PI * 2 * hz) + 1) / 2));
        }

        double clutchPulse = 0;
        if (state.IsClutch)
        {
            double hz = state.ClutchEnemyCount <= 1 ? 3.2 : state.ClutchEnemyCount == 2 ? 2.45 : 1.95;
            clutchPulse = 0.25 + (0.75 * ((Math.Sin(now.TimeOfDay.TotalSeconds * Math.PI * 2 * hz) + 1) / 2));
        }

        double bigRoomIntensity = Math.Max(
            Math.Max(Math.Max(bombResolution, clutchWin), Math.Max(ace, mvp)),
            Math.Max(
                Math.Max(clutchProgress * 0.95, clutchStart * 0.75),
                roundWin * 0.55));
        RgbColor bigRoomColor = BigRoomEventColor(state, bombResolution, clutchStart, clutchProgress, clutchWin, ace, mvp, roundWin, clutchPulse);

        return new CsLightingFrame(
            Math.Clamp(state.Health ?? 100, 0, 100),
            HealthColor(state.Health),
            TeamColor(state.Team),
            MapColor(state.MapName),
            state.HasC4,
            state.BombPlanted,
            bombSeconds,
            defusing,
            defuseSeconds,
            defuseWillSucceed,
            roundSeconds,
            state.RoundPhase,
            bombPulse,
            state.IsClutch,
            state.ClutchEnemyCount,
            clutchPulse,
            clutchStart,
            clutchProgress,
            clutchWin,
            bigRoomIntensity,
            bigRoomColor,
            damage,
            kill,
            headshot,
            death,
            state.RoundKills,
            Math.Clamp(state.Armor, 0, 100),
            state.HasHelmet,
            state.HasDefuseKit,
            state.FlashAmount / 255.0,
            state.SmokeAmount / 255.0,
            state.BurningAmount / 255.0,
            state.ActiveWeaponName,
            state.ActiveWeaponState,
            Math.Max(0, state.AmmoClip),
            Math.Max(0, state.AmmoClipMax),
            Math.Max(0, state.AmmoReserve),
            state.HasPrimaryWeapon,
            Math.Max(0, state.PrimaryAmmoClip),
            Math.Max(0, state.PrimaryAmmoClipMax),
            state.PrimaryWeaponActive,
            state.HasPistol,
            Math.Max(0, state.PistolAmmoClip),
            Math.Max(0, state.PistolAmmoClipMax),
            state.PistolActive,
            state.HasKnife,
            state.KnifeActive,
            state.FlashGrenades,
            state.SmokeGrenades,
            state.HeGrenades,
            state.FireGrenades,
            state.DecoyGrenades,
            state.ActiveGrenadeKind,
            grenadeThrow,
            GrenadeColor(state.LastGrenadeThrownKind),
            grenadeHit,
            bombPlant,
            bombResolution,
            BombResolutionColor(state.BombResolution),
            mvp,
            roundWin,
            TeamColor(state.LastRoundWinner));
    }

    private static RgbColor BigRoomEventColor(
        CsGameState state,
        double bombResolution,
        double clutchStart,
        double clutchProgress,
        double clutchWin,
        double ace,
        double mvp,
        double roundWin,
        double clutchPulse)
    {
        double strobe = 0.5 + (0.5 * Math.Sin(DateTime.UtcNow.TimeOfDay.TotalSeconds * Math.PI * 2 * 4.2));
        if (bombResolution > 0.04)
        {
            return string.Equals(state.BombResolution, "exploded", StringComparison.OrdinalIgnoreCase)
                ? Blend(new RgbColor(255, 12, 0), new RgbColor(255, 250, 215), strobe * 0.92)
                : Blend(new RgbColor(0, 145, 255), new RgbColor(205, 255, 235), strobe * 0.88);
        }

        if (mvp > 0.04)
            return Blend(new RgbColor(255, 168, 0), new RgbColor(255, 255, 225), 0.28 + (strobe * 0.70));

        if (clutchWin > 0.04)
            return Blend(new RgbColor(255, 180, 0), new RgbColor(255, 255, 220), strobe * 0.70);

        if (ace > 0.04)
            return Blend(new RgbColor(255, 155, 0), new RgbColor(255, 255, 225), strobe * 0.88);

        if (clutchProgress > 0.04)
            return Blend(new RgbColor(255, 145, 0), new RgbColor(255, 245, 175), strobe * 0.66);

        if (clutchStart > 0.04)
            return Blend(TeamColor(state.Team), new RgbColor(255, 255, 255), 0.32 + (strobe * 0.22));

        if (roundWin > 0.04)
            return TeamColor(state.LastRoundWinner);

        if (state.IsClutch)
            return Blend(TeamColor(state.Team), new RgbColor(255, 160, 35), clutchPulse * 0.54);

        return TeamColor(state.Team);
    }

    private static double Fade(TimeSpan age, double durationSeconds)
    {
        if (age < TimeSpan.Zero || age.TotalSeconds > durationSeconds)
            return 0;

        return 1 - (age.TotalSeconds / durationSeconds);
    }

    private static RgbColor HealthColor(int? health)
    {
        int hp = Math.Clamp(health ?? 100, 0, 100);
        if (hp >= 70)
            return Blend(new RgbColor(255, 180, 0), new RgbColor(0, 210, 80), (hp - 70) / 30.0);

        if (hp >= 35)
            return Blend(new RgbColor(255, 30, 0), new RgbColor(255, 180, 0), (hp - 35) / 35.0);

        return Blend(new RgbColor(90, 0, 0), new RgbColor(255, 30, 0), hp / 35.0);
    }

    private static RgbColor TeamColor(string? team)
    {
        return team?.ToUpperInvariant() switch
        {
            "CT" => CtStudyColor,
            "T" => TStudyColor,
            _ => MenuStudyColor
        };
    }

    private static RgbColor BombResolutionColor(string? resolution)
    {
        return resolution?.ToLowerInvariant() switch
        {
            "defused" => new RgbColor(0, 210, 255),
            "exploded" => new RgbColor(255, 35, 0),
            _ => new RgbColor(255, 145, 0)
        };
    }

    private static RgbColor MapColor(string? mapName)
    {
        return mapName?.ToLowerInvariant() switch
        {
            "de_dust2" => new RgbColor(235, 145, 45),
            "de_mirage" => new RgbColor(220, 120, 70),
            "de_inferno" => new RgbColor(225, 65, 25),
            "de_nuke" => new RgbColor(35, 175, 230),
            "de_ancient" => new RgbColor(65, 155, 70),
            "de_anubis" => new RgbColor(205, 155, 45),
            "de_vertigo" => new RgbColor(90, 180, 215),
            "de_train" => new RgbColor(65, 115, 170),
            "de_overpass" => new RgbColor(85, 170, 125),
            "de_cache" => new RgbColor(75, 145, 80),
            _ => new RgbColor(105, 80, 165)
        };
    }

    private static RgbColor GrenadeColor(string? grenadeKind)
    {
        return grenadeKind?.ToLowerInvariant() switch
        {
            "flash" => new RgbColor(255, 255, 235),
            "smoke" => new RgbColor(75, 185, 205),
            "he" => new RgbColor(145, 235, 45),
            "fire" => new RgbColor(255, 72, 0),
            "decoy" => new RgbColor(185, 65, 255),
            _ => new RgbColor(255, 205, 90)
        };
    }

    private static void UpdateWeapons(CsGameState state, JsonElement player, DateTime now)
    {
        JsonElement weapons = GetProperty(player, "weapons");
        if (weapons.ValueKind != JsonValueKind.Object)
            return;

        int previousFlash = state.FlashGrenades;
        int previousSmoke = state.SmokeGrenades;
        int previousHe = state.HeGrenades;
        int previousFire = state.FireGrenades;
        int previousDecoy = state.DecoyGrenades;
        bool wasInitialized = state.WeaponsInitialized;

        state.HasC4 = false;
        state.FlashGrenades = 0;
        state.SmokeGrenades = 0;
        state.HeGrenades = 0;
        state.FireGrenades = 0;
        state.DecoyGrenades = 0;
        state.ActiveGrenadeKind = null;
        state.ActiveWeaponName = null;
        state.ActiveWeaponState = null;
        state.AmmoClip = 0;
        state.AmmoClipMax = 0;
        state.AmmoReserve = 0;
        state.HasPrimaryWeapon = false;
        state.HasPistol = false;
        state.HasKnife = false;
        state.PrimaryWeaponActive = false;
        state.PistolActive = false;
        state.KnifeActive = false;
        state.PrimaryAmmoClip = 0;
        state.PrimaryAmmoClipMax = 0;
        state.PistolAmmoClip = 0;
        state.PistolAmmoClipMax = 0;

        foreach (JsonProperty weapon in weapons.EnumerateObject())
        {
            string? name = GetString(weapon.Value, "name");
            string? type = GetString(weapon.Value, "type");
            string? weaponState = GetString(weapon.Value, "state");
            int reserve = Math.Max(0, GetInt(weapon.Value, "ammo_reserve") ?? 0);
            int ammoClip = Math.Max(0, GetInt(weapon.Value, "ammo_clip") ?? 0);
            int ammoClipMax = Math.Max(0, GetInt(weapon.Value, "ammo_clip_max") ?? 0);
            bool active = string.Equals(weaponState, "active", StringComparison.OrdinalIgnoreCase) ||
                          string.Equals(weaponState, "reloading", StringComparison.OrdinalIgnoreCase);

            if (name?.Contains("c4", StringComparison.OrdinalIgnoreCase) == true)
                state.HasC4 = true;

            string? grenadeKind = GetGrenadeKind(name);
            if (grenadeKind != null)
            {
                int count = Math.Max(1, reserve);
                switch (grenadeKind)
                {
                    case "flash":
                        state.FlashGrenades += count;
                        break;
                    case "smoke":
                        state.SmokeGrenades += count;
                        break;
                    case "he":
                        state.HeGrenades += count;
                        break;
                    case "fire":
                        state.FireGrenades += count;
                        break;
                    case "decoy":
                        state.DecoyGrenades += count;
                        break;
                }

                if (active)
                    state.ActiveGrenadeKind = grenadeKind;
            }

            if (IsPrimaryWeaponType(type))
            {
                state.HasPrimaryWeapon = true;
                state.PrimaryAmmoClip = ammoClip;
                state.PrimaryAmmoClipMax = ammoClipMax;
                state.PrimaryWeaponActive = active;
            }
            else if (IsPistolType(type))
            {
                state.HasPistol = true;
                state.PistolAmmoClip = ammoClip;
                state.PistolAmmoClipMax = ammoClipMax;
                state.PistolActive = active;
            }
            else if (IsKnifeType(type))
            {
                state.HasKnife = true;
                state.KnifeActive = active;
            }

            if (active)
            {
                state.ActiveWeaponName = name;
                state.ActiveWeaponState = weaponState;
                state.AmmoClip = ammoClip;
                state.AmmoClipMax = ammoClipMax;
                state.AmmoReserve = reserve;
            }
        }

        if (wasInitialized)
        {
            string? droppedKind = FindDroppedGrenadeKind(
                previousFlash - state.FlashGrenades,
                previousSmoke - state.SmokeGrenades,
                previousHe - state.HeGrenades,
                previousFire - state.FireGrenades,
                previousDecoy - state.DecoyGrenades);
            if (droppedKind != null)
            {
                state.LastGrenadeThrowUtc = now;
                state.LastGrenadeThrownKind = droppedKind;
            }
        }

        state.WeaponsInitialized = true;
    }

    private static string? GetGrenadeKind(string? weaponName)
    {
        if (string.IsNullOrWhiteSpace(weaponName))
            return null;

        string name = weaponName.ToLowerInvariant();
        if (name.Contains("flashbang"))
            return "flash";
        if (name.Contains("smokegrenade"))
            return "smoke";
        if (name.Contains("hegrenade") || name.Contains("frag"))
            return "he";
        if (name.Contains("molotov") || name.Contains("incgrenade") || name.Contains("firebomb"))
            return "fire";
        if (name.Contains("decoy"))
            return "decoy";
        return null;
    }

    private static bool IsPrimaryWeaponType(string? weaponType)
    {
        string normalized = NormalizeWeaponType(weaponType);
        return normalized is "rifle" or "submachinegun" or "machinegun" or "shotgun" or "sniperrifle";
    }

    private static bool IsPistolType(string? weaponType)
    {
        return NormalizeWeaponType(weaponType) == "pistol";
    }

    private static bool IsKnifeType(string? weaponType)
    {
        return NormalizeWeaponType(weaponType) == "knife";
    }

    private static string NormalizeWeaponType(string? weaponType)
    {
        if (string.IsNullOrWhiteSpace(weaponType))
            return "";

        return new string(weaponType
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray());
    }

    private static string? FindDroppedGrenadeKind(int flash, int smoke, int he, int fire, int decoy)
    {
        (string Kind, int Drop)[] drops =
        {
            ("flash", flash),
            ("smoke", smoke),
            ("he", he),
            ("fire", fire),
            ("decoy", decoy)
        };

        return drops
            .Where(drop => drop.Drop > 0)
            .OrderByDescending(drop => drop.Drop)
            .Select(drop => drop.Kind)
            .FirstOrDefault();
    }

    private static void UpdateRoundAndBombState(CsGameState state, DateTime now, JsonElement round, JsonElement bomb, JsonElement phaseCountdowns)
    {
        bool wasBombPlanted = state.BombPlanted;
        string? roundPhase = GetString(round, "phase");
        string? roundBomb = GetString(round, "bomb");
        string? roundWinner = GetString(round, "win_team");
        string? bombState = GetString(bomb, "state");
        string? countdownPhase = GetString(phaseCountdowns, "phase");
        double? countdownSeconds = GetPhaseSecondsRemaining(phaseCountdowns);

        state.RoundPhase = roundPhase ?? countdownPhase ?? state.RoundPhase;
        state.RoundSecondsRemaining = null;
        state.DefuseSecondsRemaining = null;

        if (string.IsNullOrWhiteSpace(roundWinner))
        {
            state.RoundWinnerLatched = false;
        }
        else if (!state.RoundWinnerLatched)
        {
            state.LastRoundWinner = roundWinner;
            state.LastRoundWinUtc = now;
            state.RoundWinnerLatched = true;
        }

        string combined = string.Join(" ",
            roundBomb,
            roundPhase,
            bombState,
            countdownPhase);

        bool explodedOrDefused = combined.Contains("exploded", StringComparison.OrdinalIgnoreCase) ||
                                 combined.Contains("defused", StringComparison.OrdinalIgnoreCase);
        bool defusing = combined.Contains("defus", StringComparison.OrdinalIgnoreCase);
        bool planted = !explodedOrDefused && (
            combined.Contains("planted", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("bomb", StringComparison.OrdinalIgnoreCase) ||
            defusing);

        if (explodedOrDefused)
        {
            if (wasBombPlanted)
            {
                state.LastBombResolutionUtc = now;
                state.BombResolution = combined.Contains("defused", StringComparison.OrdinalIgnoreCase)
                    ? "defused"
                    : "exploded";
            }

            state.BombPlanted = false;
            state.BombPhase = null;
            state.BombSecondsRemaining = null;
            state.DefuseSecondsRemaining = null;
            state.BombEndUtc = null;
            state.DefuseEndUtc = null;
            state.RoundSecondsRemaining = countdownSeconds;
            return;
        }

        state.BombPlanted = planted;
        if (planted && !wasBombPlanted)
        {
            state.LastBombPlantUtc = now;
            state.BombSecondsRemaining = 40;
            state.BombEndUtc = now.AddSeconds(40);
        }

        if (defusing)
        {
            state.BombPhase = "defuse";
            if (countdownSeconds.HasValue)
            {
                state.DefuseSecondsRemaining = Math.Max(0, countdownSeconds.Value);
                state.DefuseEndUtc = now.AddSeconds(state.DefuseSecondsRemaining.Value);
            }

            if (state.BombEndUtc.HasValue)
                state.BombSecondsRemaining = Math.Max(0, (state.BombEndUtc.Value - now).TotalSeconds);

            return;
        }

        if (planted)
        {
            state.BombPhase = "bomb";
            if (countdownSeconds.HasValue)
            {
                state.BombSecondsRemaining = Math.Max(0, countdownSeconds.Value);
                state.BombEndUtc = now.AddSeconds(state.BombSecondsRemaining.Value);
            }
            else if (state.BombEndUtc.HasValue)
            {
                state.BombSecondsRemaining = Math.Max(0, (state.BombEndUtc.Value - now).TotalSeconds);
            }

            state.DefuseEndUtc = null;
            return;
        }

        state.BombPhase = null;
        state.BombSecondsRemaining = null;
        state.DefuseSecondsRemaining = null;
        state.BombEndUtc = null;
        state.DefuseEndUtc = null;
        state.RoundSecondsRemaining = countdownSeconds;
    }

    private static void UpdateClutchState(CsGameState state, DateTime now, JsonElement allPlayers)
    {
        if (state.LastRoundWinUtc == now)
        {
            if (state.WasClutchThisRound && TeamsMatch(state.LastRoundWinner, state.Team))
                state.LastClutchWinUtc = now;

            state.IsClutch = false;
            state.ClutchEnemyCount = 0;
            state.PreviousClutchEnemyCount = null;
            return;
        }

        if (string.Equals(state.RoundPhase, "freezetime", StringComparison.OrdinalIgnoreCase))
        {
            state.IsClutch = false;
            state.WasClutchThisRound = false;
            state.ClutchEnemyCount = 0;
            state.PreviousClutchEnemyCount = null;
            return;
        }

        if (allPlayers.ValueKind != JsonValueKind.Object || string.IsNullOrWhiteSpace(state.Team))
        {
            state.IsClutch = false;
            return;
        }

        (int ctAlive, int tAlive) = CountAlivePlayers(allPlayers);
        string ownTeam = state.Team.ToUpperInvariant();
        int teamAlive = ownTeam == "CT" ? ctAlive : tAlive;
        int enemyAlive = ownTeam == "CT" ? tAlive : ctAlive;
        bool playerAlive = (state.Health ?? 0) > 0;
        bool roundActive =
            string.Equals(state.RoundPhase, "live", StringComparison.OrdinalIgnoreCase) ||
            state.BombPlanted ||
            state.RoundSecondsRemaining.HasValue;
        bool clutch = playerAlive && roundActive && teamAlive == 1 && enemyAlive >= 1;

        if (!clutch)
        {
            state.IsClutch = false;
            state.ClutchEnemyCount = 0;
            state.PreviousClutchEnemyCount = enemyAlive > 0 ? enemyAlive : null;
            return;
        }

        if (!state.IsClutch)
        {
            state.LastClutchStartUtc = now;
            state.WasClutchThisRound = true;
        }
        else if (state.PreviousClutchEnemyCount.HasValue && enemyAlive < state.PreviousClutchEnemyCount.Value)
        {
            state.LastClutchProgressUtc = now;
        }

        state.IsClutch = true;
        state.ClutchEnemyCount = enemyAlive;
        state.PreviousClutchEnemyCount = enemyAlive;
    }

    private static (int ctAlive, int tAlive) CountAlivePlayers(JsonElement allPlayers)
    {
        int ctAlive = 0;
        int tAlive = 0;

        foreach (JsonProperty player in allPlayers.EnumerateObject())
        {
            string? team = GetString(player.Value, "team")?.ToUpperInvariant();
            int health = GetInt(GetProperty(player.Value, "state"), "health") ?? 0;
            if (health <= 0)
                continue;

            if (team == "CT")
                ctAlive++;
            else if (team == "T")
                tAlive++;
        }

        return (ctAlive, tAlive);
    }

    private static bool TeamsMatch(string? left, string? right)
    {
        return !string.IsNullOrWhiteSpace(left) &&
               !string.IsNullOrWhiteSpace(right) &&
               string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    }

    private static double? GetPhaseSecondsRemaining(JsonElement phaseCountdowns)
    {
        return GetDouble(phaseCountdowns, "phase_ends_in") ??
               GetDouble(phaseCountdowns, "phaseEndsIn");
    }

    private static JsonElement GetProperty(JsonElement element, string name)
    {
        return element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out JsonElement value)
            ? value
            : default;
    }

    private static string? GetString(JsonElement element, string name)
    {
        JsonElement value = GetProperty(element, name);
        return value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    }

    private static int? GetInt(JsonElement element, string name)
    {
        JsonElement value = GetProperty(element, name);
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int number))
            return number;

        if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out number))
            return number;

        return null;
    }

    private static bool? GetBool(JsonElement element, string name)
    {
        JsonElement value = GetProperty(element, name);
        if (value.ValueKind is JsonValueKind.True or JsonValueKind.False)
            return value.GetBoolean();

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int number))
            return number != 0;

        if (value.ValueKind == JsonValueKind.String)
        {
            if (bool.TryParse(value.GetString(), out bool boolean))
                return boolean;
            if (int.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out number))
                return number != 0;
        }

        return null;
    }

    private static double? GetDouble(JsonElement element, string name)
    {
        JsonElement value = GetProperty(element, name);
        if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out double number))
            return number;

        if (value.ValueKind == JsonValueKind.String && double.TryParse(value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out number))
            return number;

        return null;
    }

    private static void SendWiz(UdpClient client, RgbColor color, byte dimming)
    {
        SendWizChannels(client, color, 0, 0, dimming);
    }

    private static void SendWizChannels(UdpClient client, RgbColor color, byte coolWhite, byte warmWhite, byte dimming)
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
                c = coolWhite,
                w = warmWhite,
                dimming
            }
        });
        client.Send(payload, payload.Length);
    }

    private static void SendStudyWiz(UdpClient client, RgbColor color, CsLightingFrame frame, CsEventTuning tuning)
    {
        if (frame.BombResolutionIntensity > 0.04)
        {
            SendWizChannels(client, color, 0, 0, 100);
            return;
        }

        if (frame.MvpIntensity > 0.04)
        {
            SendWizChannels(client, frame.BigRoomColor, 20, 12, 100);
            return;
        }

        if (TrySendWizPlayerConditionTakeover(client, frame, roomAccent: false, tuning))
            return;

        if (frame.FlashIntensity > 0.02)
        {
            double flash = Tuned(frame.FlashIntensity, tuning.Flash);
            RgbColor flashColor = Blend(color, new RgbColor(215, 230, 255), flash * 0.70);
            byte flashCoolWhite = ClampToByte(88 + (flash * 38));
            byte flashWarmWhite = ClampToByte(46 + (flash * 26));
            SendWizChannels(client, flashColor, flashCoolWhite, flashWarmWhite, 100);
            return;
        }

        if (frame.BurningIntensity > 0.02)
        {
            double flame = 0.58 + (0.42 * ((Math.Sin(DateTime.UtcNow.TimeOfDay.TotalSeconds * Math.PI * 7.2) + 1) / 2));
            RgbColor fire = Blend(new RgbColor(255, 30, 0), new RgbColor(255, 150, 15), flame);
            SendWizChannels(client, fire, 34, 24, 100);
            return;
        }

        double objectiveStrength = Math.Max(
            frame.BombPlanted ? Math.Max(0.20, frame.BombPulse * 0.48 * tuning.Bomb) : 0,
            Math.Max(
                Tuned(frame.BombPlantIntensity, tuning.Bomb),
                Math.Max(Tuned(frame.BombResolutionIntensity, tuning.Bomb), Tuned(frame.RoundWinIntensity, tuning.Bomb))));
        double playerStrength = Math.Max(
            Math.Max(Tuned(frame.DamageIntensity, tuning.Impact), Tuned(frame.DeathIntensity, tuning.Death)),
            Math.Max(
                frame.KillIntensity,
                Math.Max(
                    frame.HeadshotIntensity,
                    Math.Max(frame.ClutchStartIntensity, frame.ClutchProgressIntensity))));
        double eventStrength = Math.Max(
            Math.Max(objectiveStrength, playerStrength),
            Math.Max(frame.BigRoomIntensity, Math.Max(frame.GrenadeThrowIntensity, Tuned(frame.GrenadeHitIntensity, tuning.Impact))));
        bool ctTint = frame.TeamColor.B >= frame.TeamColor.R;
        double teamScale = Math.Clamp(tuning.Team, 0.25, 2.0);
        double tintScale = Math.Clamp(0.34 * teamScale + (eventStrength * 0.66), 0, 1);
        RgbColor tint = new(
            ClampToByte(color.R * tintScale),
            ClampToByte(color.G * tintScale),
            ClampToByte(color.B * tintScale));
        byte baseCoolWhite = ctTint ? (byte)108 : (byte)42;
        byte baseWarmWhite = ctTint ? (byte)10 : (byte)72;
        if (frame.BombPlanted || frame.IsClutch)
        {
            baseCoolWhite = ClampToByte(baseCoolWhite * 0.75);
            baseWarmWhite = ClampToByte(baseWarmWhite * 0.75);
        }

        byte coolWhite = ClampToByte(baseCoolWhite - (eventStrength * 70));
        byte warmWhite = ClampToByte(baseWarmWhite - (eventStrength * 40));
        SendWizChannels(client, tint, coolWhite, warmWhite, 100);
    }

    private static bool TrySendWizPlayerConditionTakeover(UdpClient client, CsLightingFrame frame, bool roomAccent, CsEventTuning tuning)
    {
        if (frame.FlashIntensity > 0.02)
        {
            double strength = Tuned(frame.FlashIntensity, tuning.Flash * 1.22);
            SendWizChannels(
                client,
                new RgbColor(255, 255, 255),
                ClampToByte(170 + (85 * strength)),
                ClampToByte(128 + (82 * strength)),
                100);
            return true;
        }

        if (frame.BurningIntensity > 0.02)
        {
            double flame = 0.50 + (0.50 * ((Math.Sin(DateTime.UtcNow.TimeOfDay.TotalSeconds * Math.PI * 9.4) + 1) / 2));
            double strength = Tuned(frame.BurningIntensity, tuning.Fire * 1.18);
            RgbColor fire = Blend(
                Blend(new RgbColor(210, 0, 0), new RgbColor(255, 45, 0), strength),
                new RgbColor(255, 135, 0),
                flame * 0.42);
            SendWizChannels(client, fire, 0, ClampToByte(8 + (30 * strength)), 100);
            return true;
        }

        if (frame.DeathIntensity > 0.04)
        {
            double pulse = 0.32 + (0.68 * ((Math.Sin(DateTime.UtcNow.TimeOfDay.TotalSeconds * Math.PI * 6.0) + 1) / 2));
            double deathIntensity = Tuned(frame.DeathIntensity, tuning.Death);
            RgbColor death = Blend(new RgbColor(95, 0, 0), new RgbColor(255, 0, 0), deathIntensity * pulse);
            byte dimming = roomAccent
                ? ClampToByte(54 + (deathIntensity * 34))
                : ClampToByte(80 + (deathIntensity * 20));
            SendWizChannels(client, death, 0, 0, dimming);
            return true;
        }

        if (frame.SmokeIntensity > 0.04)
        {
            double haze = 0.44 + (0.56 * ((Math.Sin(DateTime.UtcNow.TimeOfDay.TotalSeconds * Math.PI * 1.6) + 1) / 2));
            double strength = Tuned(frame.SmokeIntensity, tuning.Smoke * 1.08);
            RgbColor smoke = Blend(new RgbColor(62, 82, 92), new RgbColor(170, 188, 195), haze * strength);
            byte dimming = ClampToByte((roomAccent ? 48 : 68) + (strength * (roomAccent ? 28 : 26)));
            SendWizChannels(client, smoke, ClampToByte(28 + (62 * strength)), ClampToByte(8 + (24 * strength)), dimming);
            return true;
        }

        double impact = Math.Max(Tuned(frame.GrenadeHitIntensity, tuning.Impact), Tuned(frame.DamageIntensity, tuning.Impact * 0.92));
        if (impact > 0.04)
        {
            double pulse = 0.35 + (0.65 * ((Math.Sin(DateTime.UtcNow.TimeOfDay.TotalSeconds * Math.PI * 8.5) + 1) / 2));
            RgbColor blast = Blend(new RgbColor(255, 18, 0), new RgbColor(255, 215, 105), impact * pulse);
            SendWizChannels(client, blast, ClampToByte(18 * impact), ClampToByte(10 * impact), ClampToByte(82 + (18 * impact)));
            return true;
        }

        return false;
    }

    private static void SendWizOff(UdpClient client)
    {
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(new
        {
            method = "setPilot",
            @params = new
            {
                state = false
            }
        });
        client.Send(payload, payload.Length);
    }

    private static void UpdateRearLight(UdpClient client, GameRearLightRole role, bool upper, CsLightingFrame frame, CsEventTuning tuning)
    {
        if (frame.BombResolutionIntensity > 0.04)
        {
            UpdateRearTakeoverLight(client, upper, frame, tuning);
            return;
        }

        if (frame.MvpIntensity > 0.04)
        {
            UpdateRearTakeoverLight(client, upper, frame, tuning);
            return;
        }

        if (TrySendWizPlayerConditionTakeover(client, frame, roomAccent: true, tuning))
            return;

        if (frame.BigRoomIntensity > 0.03)
        {
            UpdateRearTakeoverLight(client, upper, frame, tuning);
            return;
        }

        if (frame.IsClutch)
        {
            RgbColor clutchColor = Blend(frame.TeamColor, frame.BigRoomColor, frame.ClutchPulse * 0.60);
            byte clutchDimming = ClampToByte((upper ? 16 : 10) + (frame.ClutchPulse * (upper ? 16 : 10)));
            SendWiz(client, clutchColor, clutchDimming);
            return;
        }

        if (role == GameRearLightRole.Off)
        {
            SendWizOff(client);
            return;
        }

        double objectiveStrength = Math.Max(
            frame.BombPlanted ? Math.Max(0.18, frame.BombPulse * 0.45 * tuning.Bomb) : 0,
            Math.Max(
                Tuned(frame.BombPlantIntensity, tuning.Bomb),
                Math.Max(Tuned(frame.BombResolutionIntensity, tuning.Bomb), Tuned(frame.RoundWinIntensity, tuning.Bomb))));
        double healthStrength = Math.Max(
            Math.Max(Tuned(frame.DamageIntensity, tuning.Impact), Tuned(frame.DeathIntensity, tuning.Death)),
            Math.Max(Tuned(frame.BurningIntensity, tuning.Fire), Tuned(frame.FlashIntensity, tuning.Flash * 0.75)));
        double anyEvent = Math.Max(
            Math.Max(objectiveStrength, healthStrength),
            Math.Max(frame.KillIntensity, frame.HeadshotIntensity));

        if (role == GameRearLightRole.ObjectiveAlerts && objectiveStrength <= 0.03)
        {
            SendWizOff(client);
            return;
        }

        RgbColor color = role switch
        {
            GameRearLightRole.HealthDamage => Blend(frame.MapColor, frame.TeamColor, 0.48),
            GameRearLightRole.TeamAgentMood => frame.TeamColor,
            GameRearLightRole.MapMood => frame.MapColor,
            GameRearLightRole.FullGameMix => Blend(frame.MapColor, frame.TeamColor, 0.58),
            _ => new RgbColor(255, 90, 0)
        };

        if (role is GameRearLightRole.ObjectiveAlerts or GameRearLightRole.FullGameMix)
        {
            if (frame.BombPlanted)
                color = Blend(color, RazerCsDevice.GetBombTimerColor(frame), 0.72 + (frame.BombPulse * 0.22));
            color = Blend(color, new RgbColor(255, 100, 0), frame.BombPlantIntensity * 0.78);
            color = Blend(color, frame.BombResolutionColor, frame.BombResolutionIntensity * 0.84);
            color = Blend(color, frame.RoundWinColor, frame.RoundWinIntensity * 0.76);
        }

        if (role is GameRearLightRole.HealthDamage or GameRearLightRole.FullGameMix)
        {
            color = Blend(color, new RgbColor(120, 140, 145), frame.SmokeIntensity * 0.28);
            color = Blend(color, new RgbColor(255, 68, 0), frame.BurningIntensity * 0.88);
            color = Blend(color, new RgbColor(255, 255, 245), frame.FlashIntensity * 0.92);
            color = Blend(color, new RgbColor(255, 0, 0), frame.DamageIntensity * 0.78);
            color = Blend(color, new RgbColor(55, 0, 0), frame.DeathIntensity * 0.86);
        }

        if (role == GameRearLightRole.FullGameMix)
        {
            color = Blend(color, new RgbColor(255, 225, 150), frame.KillIntensity * 0.50);
            color = Blend(color, new RgbColor(255, 250, 210), frame.HeadshotIntensity * 0.72);
        }

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

    private static void UpdateRearTakeoverLight(UdpClient client, bool upper, CsLightingFrame frame, CsEventTuning tuning)
    {
        if (frame.BombResolutionIntensity > 0.04)
        {
            SendWiz(client, frame.BigRoomColor, ClampToByte(70 + (30 * tuning.Bomb)));
            return;
        }

        if (frame.MvpIntensity > 0.04)
        {
            double pulse = 0.62 + (0.38 * ((Math.Sin(DateTime.UtcNow.TimeOfDay.TotalSeconds * Math.PI * 2 * 4.2) + 1) / 2));
            double mvpDimming = (upper ? 58 : 42) + (frame.MvpIntensity * pulse * (upper ? 32 : 25));
            SendWiz(client, frame.BigRoomColor, ClampToByte(mvpDimming));
            return;
        }

        double clutchScale = tuning.Clutch;
        double dimming = frame.ClutchWinIntensity > 0.04
                ? upper ? 66 : 44
                : frame.ClutchProgressIntensity > 0.04
                    ? upper ? 50 : 34
                    : frame.ClutchStartIntensity > 0.04
                        ? upper ? 38 : 26
                        : upper ? 34 : 22;

        dimming = dimming * clutchScale;
        dimming += frame.BigRoomIntensity * (upper ? 10 : 7) * clutchScale;
        SendWiz(client, frame.BigRoomColor, ClampToByte(dimming));
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

    private static double Tuned(double value, double multiplier)
    {
        return Math.Clamp(value * multiplier, 0, 1);
    }

    private sealed class RazerCsDevice : IDisposable
    {
        private readonly WindowsHidFeatureSender _sender;
        private readonly RazerMatrixOptions _options;
        private readonly RazerCsRole _role;
        private readonly CsEventTuning _tuning;

        public RazerCsDevice(
            string devicePath,
            RazerMatrixOptions options,
            RazerCsRole role,
            CsEventTuning tuning)
        {
            _sender = new WindowsHidFeatureSender(devicePath);
            _options = options;
            _role = role;
            _tuning = tuning;
        }

        public void Update(CsLightingFrame frame)
        {
            if (_role == RazerCsRole.Keyboard)
                UpdateKeyboard(frame);
            else
                UpdateSingle(frame);
        }

        private void UpdateKeyboard(CsLightingFrame frame)
        {
            RgbColor[,] colors = new RgbColor[_options.Rows, _options.Columns];
            RgbColor baseColor = Blend(new RgbColor(2, 3, 7), frame.TeamColor, 0.24);
            for (int row = 0; row < _options.Rows; row++)
            {
                for (int column = 0; column < _options.Columns; column++)
                    colors[row, column] = baseColor;
            }

            ApplyHealthBar(colors, frame);

            if (frame.HasC4 && !frame.BombPlanted)
            {
                for (int column = 7; column <= 14 && column < _options.Columns; column++)
                    colors[0, column] = new RgbColor(255, 145, 0);
            }

            if (frame.BombPlanted)
            {
                double bombSeconds = frame.BombSecondsRemaining ?? 40;
                int litColumns = Math.Clamp((int)Math.Ceiling(_options.Columns * Math.Clamp(bombSeconds / 40.0, 0, 1)), 1, _options.Columns);
                RgbColor pulse = GetBombTimerColor(frame);
                RgbColor spent = new(35, 0, 0);
                for (int column = 0; column < _options.Columns; column++)
                    colors[0, column] = column < litColumns ? pulse : spent;

                if (frame.Defusing)
                    ApplyDefuseStrip(colors, frame);

                if (bombSeconds <= 10 || (frame.Defusing && !frame.DefuseWillSucceed))
                {
                    for (int row = 1; row < _options.Rows; row++)
                    {
                        for (int column = 0; column < _options.Columns; column++)
                            colors[row, column] = Blend(colors[row, column], pulse, frame.BombPulse * 0.55);
                    }
                }
            }
            else
            {
                ApplyRoundTimer(colors, frame);
            }

            ApplyControlKeyStatus(colors, frame, _tuning);
            ApplyRightSideHealthProtectionMeter(colors, frame);
            ApplyEventOverlays(colors, frame, _tuning);
            if (frame.MvpIntensity <= 0.04)
                ApplyPlayerConditionOverlays(colors, frame, _tuning);
            WriteMatrix(colors);
        }

        private void UpdateSingle(CsLightingFrame frame)
        {
            RgbColor color = frame.HealthColor;
            if (frame.BombPlanted)
                color = GetBombTimerColor(frame);
            else if (frame.HasC4 && _role == RazerCsRole.Dock)
                color = new RgbColor(255, 145, 0);
            else if (frame.RoundSecondsRemaining.HasValue && frame.RoundSecondsRemaining.Value <= 10)
                color = Blend(color, new RgbColor(255, 45, 0), frame.BombPulse);

            if (frame.IsClutch)
                color = Blend(color, frame.BigRoomColor, frame.ClutchPulse * 0.46);

            color = ApplyEventOverlays(color, frame, _tuning);
            if (frame.MvpIntensity <= 0.04)
                color = ApplyPlayerConditionOverlays(color, frame, _tuning);
            WriteSolid(color);
        }

        private static void ApplyControlKeyStatus(RgbColor[,] colors, CsLightingFrame frame, CsEventTuning tuning)
        {
            if (colors.GetLength(0) < 6 || colors.GetLength(1) < 18)
                return;

            double pulse = 0.50 + (0.50 * ((Math.Sin(DateTime.UtcNow.TimeOfDay.TotalSeconds * Math.PI * 5.2) + 1) / 2));
            double slowPulse = 0.55 + (0.45 * ((Math.Sin(DateTime.UtcNow.TimeOfDay.TotalSeconds * Math.PI * 2.2) + 1) / 2));
            double activeAmmoRatio = frame.AmmoClipMax > 0
                ? Math.Clamp(frame.AmmoClip / (double)frame.AmmoClipMax, 0, 1)
                : 0;

            RgbColor movementColor = MovementHealthColor(frame, pulse);
            Set(colors, 2, 3, movementColor); // W
            Set(colors, 3, 2, movementColor); // A
            Set(colors, 3, 3, movementColor); // S
            Set(colors, 3, 4, movementColor); // D

            RgbColor protectionColor = ProtectionColor(frame, slowPulse);
            Set(colors, 4, 1, protectionColor); // Shift
            Set(colors, 5, 1, protectionColor); // Ctrl
            Set(colors, 5, 6, Scale(protectionColor, 0.82)); // Space

            Set(colors, 1, 2, WeaponSlotColor(frame.HasPrimaryWeapon, frame.PrimaryAmmoClip, frame.PrimaryAmmoClipMax, frame.PrimaryWeaponActive, pulse)); // 1
            Set(colors, 1, 3, WeaponSlotColor(frame.HasPistol, frame.PistolAmmoClip, frame.PistolAmmoClipMax, frame.PistolActive, pulse)); // 2
            Set(colors, 1, 4, frame.HasKnife ? ActiveKeyColor(new RgbColor(210, 220, 230), frame.KnifeActive, pulse) : new RgbColor(6, 7, 8)); // 3
            Set(colors, 1, 5, GrenadeGroupColor(frame, pulse, tuning)); // 4
            Set(colors, 1, 6, frame.BombPlanted ? GetBombTimerColor(frame) : frame.HasC4 ? new RgbColor(255, 145, 0) : new RgbColor(10, 5, 0)); // 5

            Set(colors, 2, 2, Scale(frame.TeamColor, 0.42)); // Q
            Set(colors, 2, 4, UseKeyColor(frame, pulse)); // E
            Set(colors, 2, 5, AmmoStateColor(frame, activeAmmoRatio, pulse)); // R
            Set(colors, 3, 6, frame.GrenadeThrowIntensity > 0.04 ? Scale(frame.GrenadeThrowColor, Math.Clamp((0.45 + (pulse * 0.55)) * tuning.Utility, 0, 1)) : Scale(frame.TeamColor, 0.18)); // G

            Set(colors, 4, 3, GrenadeControlColor("flash", frame.FlashGrenades, frame.ActiveGrenadeKind, pulse, tuning)); // Z
            Set(colors, 4, 4, GrenadeControlColor("smoke", frame.SmokeGrenades, frame.ActiveGrenadeKind, pulse, tuning)); // X
            Set(colors, 4, 5, GrenadeControlColor("he", frame.HeGrenades, frame.ActiveGrenadeKind, pulse, tuning)); // C
            Set(colors, 4, 6, GrenadeControlColor("fire", frame.FireGrenades, frame.ActiveGrenadeKind, pulse, tuning)); // V
            Set(colors, 4, 7, GrenadeControlColor("decoy", frame.DecoyGrenades, frame.ActiveGrenadeKind, pulse, tuning)); // B
        }

        private static RgbColor MovementHealthColor(CsLightingFrame frame, double pulse)
        {
            RgbColor color = frame.HealthColor;
            if (frame.Health >= 100 && frame.Armor >= 95 && frame.HasHelmet)
                color = Blend(color, new RgbColor(125, 255, 205), 0.34);
            else if (frame.Health >= 95)
                color = new RgbColor(0, 215, 75);

            if (frame.Health <= 20)
                color = Blend(color, new RgbColor(255, 0, 0), 0.30 + (pulse * 0.55));

            return color;
        }

        private static RgbColor ProtectionColor(CsLightingFrame frame, double pulse)
        {
            if (frame.Armor >= 95 && frame.HasHelmet)
                return Blend(new RgbColor(0, 210, 105), new RgbColor(95, 235, 255), pulse * 0.28);

            if (frame.Armor > 0 && frame.HasHelmet)
                return MeterColor(new RgbColor(0, 185, 255), frame.Armor / 100.0);

            if (frame.Armor > 0)
                return MeterColor(new RgbColor(35, 105, 255), frame.Armor / 100.0);

            return new RgbColor(45, 0, 0);
        }

        private static RgbColor WeaponSlotColor(bool hasWeapon, int clip, int clipMax, bool active, double pulse)
        {
            if (!hasWeapon)
                return new RgbColor(4, 4, 4);

            RgbColor color = clipMax > 0
                ? AmmoColor(Math.Clamp(clip / (double)clipMax, 0, 1))
                : new RgbColor(115, 130, 145);
            return ActiveKeyColor(color, active, pulse);
        }

        private static RgbColor ActiveKeyColor(RgbColor color, bool active, double pulse)
        {
            return active
                ? Blend(color, new RgbColor(255, 255, 245), 0.18 + (pulse * 0.22))
                : Scale(color, 0.54);
        }

        private static RgbColor GrenadeGroupColor(CsLightingFrame frame, double pulse, CsEventTuning tuning)
        {
            int total = frame.FlashGrenades + frame.SmokeGrenades + frame.HeGrenades + frame.FireGrenades + frame.DecoyGrenades;
            if (frame.ActiveGrenadeKind != null)
                return Scale(GrenadeColor(frame.ActiveGrenadeKind), Math.Clamp((0.72 + (pulse * 0.28)) * tuning.Utility, 0, 1));

            if (total <= 0)
                return new RgbColor(0, 12, 3);

            bool hasDuplicate = frame.FlashGrenades > 1 ||
                                frame.SmokeGrenades > 1 ||
                                frame.HeGrenades > 1 ||
                                frame.FireGrenades > 1 ||
                                frame.DecoyGrenades > 1;
            RgbColor color = hasDuplicate
                ? new RgbColor(30, 115, 255)
                : new RgbColor(0, 215, 75);
            return Scale(color, Math.Clamp(0.70 * tuning.Utility, 0, 1));
        }

        private static RgbColor GrenadeControlColor(string kind, int count, string? activeKind, double pulse, CsEventTuning tuning)
        {
            double utility = Math.Clamp(tuning.Utility, 0, 2);
            RgbColor baseColor = GrenadeColor(kind);
            if (string.Equals(kind, activeKind, StringComparison.OrdinalIgnoreCase))
                return Scale(Blend(baseColor, new RgbColor(255, 255, 255), pulse * 0.22), Math.Clamp((0.72 + (pulse * 0.28)) * utility, 0, 1));

            if (count <= 0)
                return Scale(baseColor, Math.Clamp(0.06 * utility, 0, 0.22));

            RgbColor color = count > 1
                ? Blend(baseColor, new RgbColor(45, 155, 255), 0.34)
                : baseColor;
            return Scale(color, Math.Clamp((count > 1 ? 0.98 : 0.78) * utility, 0, 1));
        }

        private static RgbColor UseKeyColor(CsLightingFrame frame, double pulse)
        {
            if (frame.Defusing)
                return frame.DefuseWillSucceed
                    ? Blend(new RgbColor(0, 155, 255), new RgbColor(0, 255, 120), pulse)
                    : Blend(new RgbColor(255, 0, 0), new RgbColor(255, 255, 255), pulse);

            if (frame.BombPlanted)
                return frame.HasDefuseKit
                    ? new RgbColor(0, 235, 150)
                    : new RgbColor(255, 195, 45);

            return frame.HasDefuseKit
                ? new RgbColor(0, 150, 95)
                : Scale(frame.TeamColor, 0.22);
        }

        private static void ApplyRightSideHealthProtectionMeter(RgbColor[,] colors, CsLightingFrame frame)
        {
            if (colors.GetLength(0) < 6 || colors.GetLength(1) < 22 ||
                frame.BombResolutionIntensity > 0.04)
            {
                return;
            }

            double pulse = 0.45 + (0.55 * ((Math.Sin(DateTime.UtcNow.TimeOfDay.TotalSeconds * Math.PI * 4.6) + 1) / 2));
            (int Row, int Column)[] healthKeys =
            {
                (5, 19), (5, 20),
                (4, 18), (4, 19), (4, 20), (4, 21),
                (3, 18), (3, 19), (3, 20), (2, 21),
                (2, 18), (2, 19), (2, 20), (1, 21),
                (1, 18), (1, 19), (1, 20)
            };

            double filledKeys = (frame.Health / 100.0) * healthKeys.Length;
            for (int index = 0; index < healthKeys.Length; index++)
            {
                double fill = Math.Clamp(filledKeys - index, 0, 1);
                RgbColor color = fill <= 0
                    ? new RgbColor(16, 0, 0)
                    : MeterColor(frame.HealthColor, fill);
                if (frame.Health <= 22 && fill > 0)
                    color = Blend(color, new RgbColor(255, 0, 0), pulse * 0.52);
                Set(colors, healthKeys[index].Row, healthKeys[index].Column, color);
            }

            RgbColor armorColor = frame.Armor > 0
                ? MeterColor(new RgbColor(20, 130, 255), frame.Armor / 100.0)
                : new RgbColor(18, 3, 0);
            RgbColor helmetColor = frame.HasHelmet
                ? Blend(new RgbColor(55, 190, 255), new RgbColor(130, 255, 235), pulse * 0.30)
                : new RgbColor(4, 7, 9);
            RgbColor kitColor = frame.HasDefuseKit
                ? new RgbColor(0, 245, 160)
                : Scale(frame.TeamColor, 0.12);

            Set(colors, 1, 19, armorColor); // numpad /
            Set(colors, 1, 20, helmetColor); // numpad *
            Set(colors, 1, 21, kitColor); // numpad -
        }

        private static RgbColor GetNumpadAssignmentColor(
            string assignment,
            CsLightingFrame frame,
            double ammoRatio,
            double selectedPulse)
        {
            if (assignment.StartsWith("Health", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(assignment[^1..], out int healthSegment))
            {
                double fill = Math.Clamp((frame.Health - ((healthSegment - 1) * (100.0 / 3.0))) / (100.0 / 3.0), 0, 1);
                return MeterColor(frame.HealthColor, fill);
            }

            if (assignment.StartsWith("Ammo", StringComparison.OrdinalIgnoreCase) &&
                assignment.Length == 5 &&
                int.TryParse(assignment[^1..], out int ammoSegment))
            {
                double fill = Math.Clamp((ammoRatio * 3) - (ammoSegment - 1), 0, 1);
                return MeterColor(AmmoColor(ammoRatio), fill);
            }

            return assignment.ToLowerInvariant() switch
            {
                "off" => new RgbColor(0, 0, 0),
                "team" => Scale(frame.TeamColor, 0.72),
                "selectedgrenade" => frame.ActiveGrenadeKind == null
                    ? Blend(new RgbColor(1, 2, 5), frame.TeamColor, 0.18)
                    : Scale(GrenadeColor(frame.ActiveGrenadeKind), selectedPulse),
                "armor" => MeterColor(new RgbColor(20, 125, 255), frame.Armor / 100.0),
                "helmet" => frame.HasHelmet ? new RgbColor(75, 225, 255) : new RgbColor(1, 7, 10),
                "defusekit" => frame.HasDefuseKit ? new RgbColor(0, 255, 175) : new RgbColor(1, 8, 5),
                "flash" => GrenadeStatusColor("flash", frame.FlashGrenades, frame.ActiveGrenadeKind, selectedPulse),
                "smoke" => GrenadeStatusColor("smoke", frame.SmokeGrenades, frame.ActiveGrenadeKind, selectedPulse),
                "he" => GrenadeStatusColor("he", frame.HeGrenades, frame.ActiveGrenadeKind, selectedPulse),
                "fire" => GrenadeStatusColor("fire", frame.FireGrenades, frame.ActiveGrenadeKind, selectedPulse),
                "decoy" => GrenadeStatusColor("decoy", frame.DecoyGrenades, frame.ActiveGrenadeKind, selectedPulse),
                "ammostate" => AmmoStateColor(frame, ammoRatio, selectedPulse),
                "reserveammo" => frame.AmmoReserve <= 0
                    ? new RgbColor(45, 0, 0)
                    : MeterColor(new RgbColor(80, 205, 255), Math.Clamp(frame.AmmoReserve / 90.0, 0, 1)),
                "bomb" => frame.BombPlanted
                    ? GetBombTimerColor(frame)
                    : frame.HasC4
                        ? new RgbColor(255, 145, 0)
                        : new RgbColor(8, 3, 0),
                "roundkills" => frame.RoundKills <= 0
                    ? new RgbColor(8, 5, 0)
                    : Scale(new RgbColor(255, 205, 70), Math.Clamp(0.28 + (frame.RoundKills * 0.16), 0, 1)),
                _ => new RgbColor(0, 0, 0)
            };
        }

        private static RgbColor GrenadeStatusColor(
            string kind,
            int count,
            string? activeKind,
            double selectedPulse)
        {
            RgbColor baseColor = GrenadeColor(kind);
            if (count <= 0)
                return Scale(baseColor, 0.025);

            double strength = count >= 2 ? 1.0 : 0.64;
            if (string.Equals(kind, activeKind, StringComparison.OrdinalIgnoreCase))
                strength = 0.72 + (selectedPulse * 0.28);
            return Scale(baseColor, strength);
        }

        private static RgbColor AmmoStateColor(CsLightingFrame frame, double ammoRatio, double selectedPulse)
        {
            bool reloading = string.Equals(frame.ActiveWeaponState, "reloading", StringComparison.OrdinalIgnoreCase);
            if (reloading)
                return Scale(new RgbColor(0, 190, 255), selectedPulse);
            if (frame.AmmoClipMax <= 0)
            {
                return frame.ActiveGrenadeKind != null
                    ? Scale(GrenadeColor(frame.ActiveGrenadeKind), 0.50)
                    : Scale(frame.TeamColor, 0.18);
            }
            if (ammoRatio <= 0.20 && frame.AmmoClipMax > 0)
                return Scale(new RgbColor(255, 22, 0), selectedPulse);
            return MeterColor(AmmoColor(ammoRatio), ammoRatio);
        }

        private static RgbColor AmmoColor(double ratio)
        {
            if (ratio <= 0.20)
                return new RgbColor(255, 25, 0);
            if (ratio <= 0.55)
                return new RgbColor(255, 155, 0);
            return new RgbColor(0, 215, 75);
        }

        private static RgbColor MeterColor(RgbColor color, double fill)
        {
            fill = Math.Clamp(fill, 0, 1);
            return fill <= 0
                ? Scale(color, 0.025)
                : Scale(color, 0.18 + (fill * 0.82));
        }

        private static RgbColor Scale(RgbColor color, double amount)
        {
            amount = Math.Clamp(amount, 0, 1);
            return new RgbColor(
                ClampToByte(color.R * amount),
                ClampToByte(color.G * amount),
                ClampToByte(color.B * amount));
        }

        private static void Set(RgbColor[,] colors, int row, int column, RgbColor color)
        {
            if (row >= 0 && row < colors.GetLength(0) && column >= 0 && column < colors.GetLength(1))
                colors[row, column] = color;
        }

        private static void ApplyHealthBar(RgbColor[,] colors, CsLightingFrame frame)
        {
            int row = colors.GetLength(0) - 1;
            int columns = colors.GetLength(1);
            int litColumns = Math.Clamp((int)Math.Ceiling(columns * (frame.Health / 100.0)), 0, columns);
            for (int column = 0; column < columns; column++)
            {
                colors[row, column] = column < litColumns
                    ? frame.HealthColor
                    : new RgbColor(20, 0, 0);
            }

            if (frame.Health <= 25)
            {
                double pulse = 0.35 + (0.65 * ((Math.Sin(DateTime.UtcNow.TimeOfDay.TotalSeconds * Math.PI * 5.5) + 1) / 2));
                for (int column = 0; column < litColumns; column++)
                    colors[row, column] = Blend(colors[row, column], new RgbColor(255, 0, 0), pulse * 0.62);
            }
        }

        private static void ApplyRoundTimer(RgbColor[,] colors, CsLightingFrame frame)
        {
            if (!frame.RoundSecondsRemaining.HasValue)
                return;

            double maxSeconds = string.Equals(frame.RoundPhase, "freezetime", StringComparison.OrdinalIgnoreCase) ? 20 : 115;
            double seconds = Math.Clamp(frame.RoundSecondsRemaining.Value, 0, maxSeconds);
            int columns = colors.GetLength(1);
            int litColumns = Math.Clamp((int)Math.Ceiling(columns * (seconds / maxSeconds)), 0, columns);
            RgbColor active = seconds <= 10
                ? Blend(new RgbColor(255, 85, 0), new RgbColor(255, 0, 0), frame.BombPulse)
                : seconds <= 20
                    ? new RgbColor(255, 145, 0)
                    : new RgbColor(80, 170, 255);
            RgbColor spent = new(0, 18, 35);

            for (int column = 0; column < columns; column++)
                colors[0, column] = column < litColumns ? active : spent;
        }

        private static void ApplyDefuseStrip(RgbColor[,] colors, CsLightingFrame frame)
        {
            if (!frame.DefuseSecondsRemaining.HasValue || colors.GetLength(0) < 2)
                return;

            double maxSeconds = frame.DefuseSecondsRemaining.Value > 5.4 ? 10 : 5;
            double seconds = Math.Clamp(frame.DefuseSecondsRemaining.Value, 0, maxSeconds);
            int columns = colors.GetLength(1);
            int litColumns = Math.Clamp((int)Math.Ceiling(columns * (seconds / maxSeconds)), 0, columns);
            RgbColor active = frame.DefuseWillSucceed
                ? Blend(new RgbColor(0, 120, 255), new RgbColor(0, 255, 115), 1 - (seconds / maxSeconds))
                : Blend(new RgbColor(255, 0, 0), new RgbColor(255, 255, 255), frame.BombPulse);
            RgbColor spent = frame.DefuseWillSucceed ? new RgbColor(0, 35, 20) : new RgbColor(60, 0, 0);

            for (int column = 0; column < columns; column++)
                colors[1, column] = column < litColumns ? active : spent;
        }

        public static RgbColor GetBombTimerColor(CsLightingFrame frame)
        {
            if (frame.Defusing)
            {
                return frame.DefuseWillSucceed
                    ? Blend(new RgbColor(0, 115, 255), new RgbColor(0, 255, 95), frame.BombPulse)
                    : Blend(new RgbColor(255, 0, 0), new RgbColor(255, 255, 255), frame.BombPulse);
            }

            double seconds = frame.BombSecondsRemaining ?? 40;
            if (seconds <= 5)
                return Blend(new RgbColor(255, 0, 0), new RgbColor(255, 255, 255), frame.BombPulse);

            if (seconds <= 10)
                return Blend(new RgbColor(170, 0, 0), new RgbColor(255, 0, 0), frame.BombPulse);

            if (seconds <= 20)
                return Blend(new RgbColor(190, 25, 0), new RgbColor(255, 95, 0), frame.BombPulse);

            return Blend(new RgbColor(130, 50, 0), new RgbColor(255, 145, 0), frame.BombPulse);
        }

        private static void ApplyEventOverlays(RgbColor[,] colors, CsLightingFrame frame, CsEventTuning tuning)
        {
            int rows = colors.GetLength(0);
            int columns = colors.GetLength(1);
            double center = (columns - 1) / 2.0;
            for (int row = 0; row < colors.GetLength(0); row++)
            {
                for (int column = 0; column < colors.GetLength(1); column++)
                {
                    RgbColor color = colors[row, column];
                    double edgeDistance = Math.Min(column, columns - 1 - column) / Math.Max(1.0, center);
                    double centerDistance = Math.Abs(column - center) / Math.Max(1.0, center);
                    double vertical = rows <= 1 ? 1 : 1 - (row / (double)(rows - 1));

                    double damageWave = Tuned(frame.DamageIntensity, tuning.Impact) * Math.Clamp(1.25 - edgeDistance, 0, 1) * (0.68 + (vertical * 0.32));
                    double killWave = frame.KillIntensity * Math.Clamp(1.20 - centerDistance, 0, 1);
                    double plantWave = frame.BombPlantIntensity * Math.Clamp(1.35 - centerDistance, 0, 1);
                    double winWave = frame.RoundWinIntensity * (0.45 + (vertical * 0.55));
                    double clutchTension = frame.IsClutch
                        ? frame.ClutchPulse * Math.Clamp(1.10 - centerDistance, 0, 1) * (0.36 + (vertical * 0.24))
                        : 0;
                    double clutchStartWave = frame.ClutchStartIntensity * Math.Clamp(1.18 - edgeDistance, 0, 1);
                    double clutchProgressWave = frame.ClutchProgressIntensity * Math.Clamp(1.26 - centerDistance, 0, 1);
                    double clutchWinWave = frame.ClutchWinIntensity * (0.45 + (vertical * 0.55));
                    double mvpSweep = 0.52 + (0.48 * ((Math.Sin(
                        (DateTime.UtcNow.TimeOfDay.TotalSeconds * Math.PI * 2 * 3.8) -
                        (column * 0.58) -
                        (row * 0.34)) + 1) / 2));
                    double mvpWave = frame.MvpIntensity * (0.48 + (mvpSweep * 0.52));

                    color = Blend(color, frame.BigRoomColor, clutchTension * 0.74 * tuning.Clutch);
                    color = Blend(color, frame.BigRoomColor, clutchStartWave * 0.76 * tuning.Clutch);
                    color = Blend(color, frame.BigRoomColor, clutchProgressWave * 0.92 * tuning.Clutch);
                    color = Blend(color, frame.BigRoomColor, clutchWinWave * 0.94 * tuning.Clutch);
                    color = Blend(color, new RgbColor(255, 0, 0), damageWave);
                    color = Blend(color, new RgbColor(255, 235, 165), killWave * 0.94);
                    color = Blend(color, new RgbColor(255, 250, 205), frame.HeadshotIntensity * Math.Clamp(1.28 - centerDistance, 0, 1));
                    color = Blend(color, frame.GrenadeThrowColor, frame.GrenadeThrowIntensity * Math.Clamp(1.18 - edgeDistance, 0, 1) * 0.72 * tuning.Impact);
                    color = Blend(color, new RgbColor(255, 210, 95), Tuned(frame.GrenadeHitIntensity, tuning.Impact) * Math.Clamp(1.35 - centerDistance, 0, 1));
                    color = Blend(color, new RgbColor(255, 115, 0), plantWave * 0.82 * tuning.Bomb);
                    color = Blend(color, frame.BigRoomColor, Tuned(frame.BombResolutionIntensity, tuning.Bomb) * 0.90);
                    color = Blend(color, frame.RoundWinColor, winWave * 0.88 * tuning.Bomb);
                    color = Blend(color, new RgbColor(90, 0, 0), Tuned(frame.DeathIntensity, tuning.Death) * 0.94);
                    color = Blend(color, frame.BigRoomColor, mvpWave * 0.98);
                    if (frame.BombResolutionIntensity > 0.04)
                        color = frame.BigRoomColor;
                    colors[row, column] = color;
                }
            }
        }

        private static RgbColor ApplyEventOverlays(RgbColor color, CsLightingFrame frame, CsEventTuning tuning)
        {
            if (frame.IsClutch)
                color = Blend(color, frame.BigRoomColor, frame.ClutchPulse * 0.46 * tuning.Clutch);

            color = Blend(color, frame.BigRoomColor, frame.ClutchStartIntensity * 0.76 * tuning.Clutch);
            color = Blend(color, frame.BigRoomColor, frame.ClutchProgressIntensity * 0.90 * tuning.Clutch);
            color = Blend(color, frame.BigRoomColor, frame.ClutchWinIntensity * 0.94 * tuning.Clutch);
            color = Blend(color, new RgbColor(255, 0, 0), Tuned(frame.DamageIntensity, tuning.Impact));
            color = Blend(color, new RgbColor(255, 230, 150), frame.KillIntensity * 0.85);
            color = Blend(color, new RgbColor(255, 250, 205), frame.HeadshotIntensity * 0.96);
            color = Blend(color, frame.GrenadeThrowColor, frame.GrenadeThrowIntensity * 0.70 * tuning.Impact);
            color = Blend(color, new RgbColor(255, 210, 95), Tuned(frame.GrenadeHitIntensity, tuning.Impact));
            color = Blend(color, new RgbColor(255, 110, 0), Tuned(frame.BombPlantIntensity, tuning.Bomb) * 0.80);
            color = Blend(color, frame.BigRoomColor, Tuned(frame.BombResolutionIntensity, tuning.Bomb) * 0.92);
            color = Blend(color, frame.RoundWinColor, Tuned(frame.RoundWinIntensity, tuning.Bomb) * 0.86);
            color = Blend(color, new RgbColor(130, 0, 0), Tuned(frame.DeathIntensity, tuning.Death) * 0.90);
            color = Blend(color, frame.BigRoomColor, frame.MvpIntensity * 0.98);
            if (frame.BombResolutionIntensity > 0.04)
                color = frame.BigRoomColor;
            return color;
        }

        private static void ApplyPlayerConditionOverlays(RgbColor[,] colors, CsLightingFrame frame, CsEventTuning tuning)
        {
            int rows = colors.GetLength(0);
            int columns = colors.GetLength(1);
            double flame = 0.52 + (0.48 * ((Math.Sin(DateTime.UtcNow.TimeOfDay.TotalSeconds * Math.PI * 7.5) + 1) / 2));
            for (int row = 0; row < rows; row++)
            {
                for (int column = 0; column < columns; column++)
                {
                    RgbColor color = colors[row, column];
                    color = Blend(color, new RgbColor(125, 148, 158), Tuned(frame.SmokeIntensity, tuning.Smoke * 0.90));

                    double stripe = 0.52 + (0.48 * ((Math.Sin((row * 1.7) + (column * 0.85) + (DateTime.UtcNow.TimeOfDay.TotalSeconds * 15)) + 1) / 2));
                    RgbColor fire = Blend(new RgbColor(220, 0, 0), new RgbColor(255, 125, 0), stripe);
                    color = Blend(color, fire, Tuned(frame.BurningIntensity, tuning.Fire * flame * 1.22));
                    color = Blend(color, new RgbColor(255, 255, 255), Tuned(frame.FlashIntensity, tuning.Flash * 1.18));
                    colors[row, column] = color;
                }
            }
        }

        private static RgbColor ApplyPlayerConditionOverlays(RgbColor color, CsLightingFrame frame, CsEventTuning tuning)
        {
            color = Blend(color, new RgbColor(125, 148, 158), Tuned(frame.SmokeIntensity, tuning.Smoke * 0.90));
            double flame = 0.52 + (0.48 * ((Math.Sin(DateTime.UtcNow.TimeOfDay.TotalSeconds * Math.PI * 7.5) + 1) / 2));
            RgbColor fire = Blend(new RgbColor(220, 0, 0), new RgbColor(255, 125, 0), flame);
            color = Blend(color, fire, Tuned(frame.BurningIntensity, tuning.Fire * flame * 1.22));
            return Blend(color, new RgbColor(255, 255, 255), Tuned(frame.FlashIntensity, tuning.Flash * 1.18));
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

                _sender.SetFeature(RazerReports.CreateCustomFrameExtended(_options.TransactionId, (byte)row, 0, (byte)(_options.Columns - 1), rgb));
                Thread.Sleep(1);
            }

            _sender.SetFeature(RazerReports.CreateCustomModeExtended(_options.TransactionId));
        }

        public void Dispose()
        {
            _sender.Dispose();
        }
    }

    private sealed class LenovoCsDevice : IDisposable
    {
        private readonly HidDevice _device;
        private HidStream? _stream;

        public LenovoCsDevice(HidDevice device)
        {
            _device = device;
        }

        public void Update(CsLightingFrame frame)
        {
            try
            {
                byte[] report = CreateBaseReport();
                for (int zone = 0; zone < 4; zone++)
                {
                    double center = 1 - (Math.Abs(zone - 1.5) / 1.5);
                    RgbColor color = frame.IsClutch
                        ? Blend(new RgbColor(0, 1, 5), frame.TeamColor, 0.22)
                        : new RgbColor(0, 0, 0);

                    if (frame.IsClutch)
                        color = Blend(color, frame.BigRoomColor, frame.ClutchPulse * (0.32 + (center * 0.25)));

                    color = Blend(color, frame.BigRoomColor, frame.BigRoomIntensity * (0.82 + (center * 0.16)));
                    if (frame.BombResolutionIntensity > 0.04)
                        color = frame.BigRoomColor;

                    int offset = 5 + (zone * 3);
                    report[offset] = color.R;
                    report[offset + 1] = color.G;
                    report[offset + 2] = color.B;
                }

                GetStream().SetFeature(report);
            }
            catch
            {
                ResetStream();
            }
        }

        public void TurnOff()
        {
            try
            {
                byte[] report = CreateBaseReport();
                GetStream().SetFeature(report);
            }
            catch
            {
                ResetStream();
            }
        }

        private static byte[] CreateBaseReport()
        {
            byte[] report = new byte[33];
            report[0] = 0xCC;
            report[1] = 0x16;
            report[2] = 0x01;
            report[3] = 0x01;
            report[4] = 0x02;
            return report;
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

    private sealed class CsGameState
    {
        public DateTime LastUpdateUtc { get; set; } = DateTime.MinValue;
        public DateTime LastDamageUtc { get; set; } = DateTime.MinValue;
        public DateTime LastKillUtc { get; set; } = DateTime.MinValue;
        public DateTime LastHeadshotUtc { get; set; } = DateTime.MinValue;
        public DateTime LastDeathUtc { get; set; } = DateTime.MinValue;
        public DateTime LastGrenadeThrowUtc { get; set; } = DateTime.MinValue;
        public DateTime LastGrenadeDamageUtc { get; set; } = DateTime.MinValue;
        public DateTime LastBombPlantUtc { get; set; } = DateTime.MinValue;
        public DateTime LastBombResolutionUtc { get; set; } = DateTime.MinValue;
        public DateTime LastRoundWinUtc { get; set; } = DateTime.MinValue;
        public DateTime LastMvpUtc { get; set; } = DateTime.MinValue;
        public DateTime LastClutchStartUtc { get; set; } = DateTime.MinValue;
        public DateTime LastClutchProgressUtc { get; set; } = DateTime.MinValue;
        public DateTime LastClutchWinUtc { get; set; } = DateTime.MinValue;
        public string? Activity { get; set; }
        public string? Team { get; set; }
        public string? MapName { get; set; }
        public int? Health { get; set; }
        public int Armor { get; set; }
        public bool HasHelmet { get; set; }
        public bool HasDefuseKit { get; set; }
        public int FlashAmount { get; set; }
        public int SmokeAmount { get; set; }
        public int BurningAmount { get; set; }
        public int RoundKills { get; set; }
        public int RoundHeadshots { get; set; }
        public int RoundTotalDamage { get; set; }
        public int? Kills { get; set; }
        public int? Deaths { get; set; }
        public int? Mvps { get; set; }
        public string? ActiveWeaponName { get; set; }
        public string? ActiveWeaponState { get; set; }
        public int AmmoClip { get; set; }
        public int AmmoClipMax { get; set; }
        public int AmmoReserve { get; set; }
        public bool HasPrimaryWeapon { get; set; }
        public int PrimaryAmmoClip { get; set; }
        public int PrimaryAmmoClipMax { get; set; }
        public bool PrimaryWeaponActive { get; set; }
        public bool HasPistol { get; set; }
        public int PistolAmmoClip { get; set; }
        public int PistolAmmoClipMax { get; set; }
        public bool PistolActive { get; set; }
        public bool HasKnife { get; set; }
        public bool KnifeActive { get; set; }
        public int FlashGrenades { get; set; }
        public int SmokeGrenades { get; set; }
        public int HeGrenades { get; set; }
        public int FireGrenades { get; set; }
        public int DecoyGrenades { get; set; }
        public string? ActiveGrenadeKind { get; set; }
        public string? LastGrenadeThrownKind { get; set; }
        public bool WeaponsInitialized { get; set; }
        public bool HasC4 { get; set; }
        public bool BombPlanted { get; set; }
        public string? BombPhase { get; set; }
        public string? RoundPhase { get; set; }
        public double? BombSecondsRemaining { get; set; }
        public double? DefuseSecondsRemaining { get; set; }
        public double? RoundSecondsRemaining { get; set; }
        public DateTime? BombEndUtc { get; set; }
        public DateTime? DefuseEndUtc { get; set; }
        public string? BombResolution { get; set; }
        public string? LastRoundWinner { get; set; }
        public bool RoundWinnerLatched { get; set; }
        public bool IsClutch { get; set; }
        public bool WasClutchThisRound { get; set; }
        public int ClutchEnemyCount { get; set; }
        public int? PreviousClutchEnemyCount { get; set; }

        public CsGameState Clone()
        {
            return (CsGameState)MemberwiseClone();
        }
    }

    private readonly record struct CsLightingFrame(
        int Health,
        RgbColor HealthColor,
        RgbColor TeamColor,
        RgbColor MapColor,
        bool HasC4,
        bool BombPlanted,
        double? BombSecondsRemaining,
        bool Defusing,
        double? DefuseSecondsRemaining,
        bool DefuseWillSucceed,
        double? RoundSecondsRemaining,
        string? RoundPhase,
        double BombPulse,
        bool IsClutch,
        int ClutchEnemyCount,
        double ClutchPulse,
        double ClutchStartIntensity,
        double ClutchProgressIntensity,
        double ClutchWinIntensity,
        double BigRoomIntensity,
        RgbColor BigRoomColor,
        double DamageIntensity,
        double KillIntensity,
        double HeadshotIntensity,
        double DeathIntensity,
        int RoundKills,
        int Armor,
        bool HasHelmet,
        bool HasDefuseKit,
        double FlashIntensity,
        double SmokeIntensity,
        double BurningIntensity,
        string? ActiveWeaponName,
        string? ActiveWeaponState,
        int AmmoClip,
        int AmmoClipMax,
        int AmmoReserve,
        bool HasPrimaryWeapon,
        int PrimaryAmmoClip,
        int PrimaryAmmoClipMax,
        bool PrimaryWeaponActive,
        bool HasPistol,
        int PistolAmmoClip,
        int PistolAmmoClipMax,
        bool PistolActive,
        bool HasKnife,
        bool KnifeActive,
        int FlashGrenades,
        int SmokeGrenades,
        int HeGrenades,
        int FireGrenades,
        int DecoyGrenades,
        string? ActiveGrenadeKind,
        double GrenadeThrowIntensity,
        RgbColor GrenadeThrowColor,
        double GrenadeHitIntensity,
        double BombPlantIntensity,
        double BombResolutionIntensity,
        RgbColor BombResolutionColor,
        double MvpIntensity,
        double RoundWinIntensity,
        RgbColor RoundWinColor);

    private readonly record struct RgbColor(byte R, byte G, byte B);

    private enum RazerCsRole
    {
        Keyboard,
        Mouse,
        Dock
    }
}
