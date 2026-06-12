using System.Net.Sockets;
using System.Runtime.InteropServices;
using HidSharp;
using HPPH;
using ScreenCapture.NET;
using Serilog;

namespace Artemis.Plugins.DirectDevices;

public sealed record DirectWatchOptions(
    bool Study,
    string UpperRole,
    string LowerRole,
    bool RazerKeyboard,
    bool RazerMouse,
    bool RazerDock,
    bool LenovoKeyboard,
    bool BlackoutOnBlack,
    int Fps,
    int StudyStrength,
    int RearStrength,
    int RazerStrength,
    int ColorBoost);

public sealed class DirectAmbientController : IDisposable
{
    private const byte StudyLampDimming = 100;
    private const byte UpperRoomDimming = 30;
    private const byte LowerRoomDimming = 22;

    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly Task _worker;

    public DirectAmbientController(string mode, DirectWatchOptions watchOptions, WizLightAddresses wizAddresses, ILogger logger)
    {
        DirectAmbientMode ambientMode = ParseMode(mode);
        _worker = Task.Run(() => Run(ambientMode, watchOptions, wizAddresses, logger, _cancellationTokenSource.Token));
    }

    public void Dispose()
    {
        _cancellationTokenSource.Cancel();
        try
        {
            _worker.Wait(TimeSpan.FromSeconds(2));
        }
        catch
        {
            // Shutdown is best-effort; device handles are disposed below.
        }

        _cancellationTokenSource.Dispose();
    }

    private static DirectAmbientMode ParseMode(string mode)
    {
        string normalizedMode = mode.Trim().Trim('"');
        if (string.Equals(normalizedMode, "WatchCinematic", StringComparison.OrdinalIgnoreCase))
            return DirectAmbientMode.WatchCinematic;

        return string.Equals(normalizedMode, "FullRoomTasteful", StringComparison.OrdinalIgnoreCase)
            ? DirectAmbientMode.FullRoomTasteful
            : DirectAmbientMode.StudyOnly;
    }

    private static async Task Run(
        DirectAmbientMode mode,
        DirectWatchOptions watchOptions,
        WizLightAddresses wizAddresses,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await RunSession(mode, watchOptions, wizAddresses, logger, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.Warning(exception, "Direct ambient session stalled; recreating capture and device handles");
                try
                {
                    await Task.Delay(500, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }

    private static async Task RunSession(
        DirectAmbientMode mode,
        DirectWatchOptions watchOptions,
        WizLightAddresses wizAddresses,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        using ScreenSampler sampler = new(160, 90, logger);
        using UdpClient wizClient = new();
        wizClient.Connect(wizAddresses.StudyEndpoint, 38899);
        using UdpClient lowerWizClient = new();
        lowerWizClient.Connect(wizAddresses.LowerEndpoint, 38899);
        using UdpClient upperWizClient = new();
        upperWizClient.Connect(wizAddresses.UpperEndpoint, 38899);

        List<RazerAmbientDevice> razerDevices = OpenRazerDevices();
        List<LenovoAmbientDevice> lenovoDevices = OpenLenovoDevices();
        RearLightRole upperRole = RearLightRoles.Parse(watchOptions.UpperRole);
        RearLightRole lowerRole = RearLightRoles.Parse(watchOptions.LowerRole);
        int frameDelayMs = LightingFrameRate.ToDelayMilliseconds(LightingFrameRate.ClampAmbient(watchOptions.Fps));
        double watchStudyStrength = PercentScale(watchOptions.StudyStrength);
        double watchRearStrength = PercentScale(watchOptions.RearStrength);
        double watchRazerStrength = PercentScale(watchOptions.RazerStrength);
        double watchColorBoost = PercentScale(watchOptions.ColorBoost);
        bool cinematicMode = mode is DirectAmbientMode.WatchCinematic or DirectAmbientMode.StudyOnly;
        double studySmoothedR = cinematicMode ? 0 : 80;
        double studySmoothedG = cinematicMode ? 0 : 32;
        double studySmoothedB = cinematicMode ? 0 : 180;
        double razerSmoothedR = cinematicMode ? 0 : 80;
        double razerSmoothedG = cinematicMode ? 0 : 32;
        double razerSmoothedB = cinematicMode ? 0 : 180;
        double upperSmoothedR = cinematicMode ? 0 : 18;
        double upperSmoothedG = cinematicMode ? 0 : 12;
        double upperSmoothedB = cinematicMode ? 0 : 28;
        double lowerSmoothedR = cinematicMode ? 0 : 18;
        double lowerSmoothedG = cinematicMode ? 0 : 12;
        double lowerSmoothedB = cinematicMode ? 0 : 28;
        double studySmoothedDimming = StudyLampDimming;
        double upperSmoothedDimming = upperRole.AmbientMaximum(upper: true);
        double lowerSmoothedDimming = lowerRole.AmbientMaximum(upper: false);
        int loopCount = 0;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (DirectLightingRuntimeState.IsGameActive)
                {
                    foreach (LenovoAmbientDevice lenovoDevice in lenovoDevices)
                        lenovoDevice.TurnOff();

                    loopCount++;
                    await Task.Delay(frameDelayMs, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                sampler.Capture();
                if (cinematicMode)
                {
                    CinematicSample studySample = ApplyBlackPolicy(
                        mode == DirectAmbientMode.StudyOnly
                            ? sampler.SamplePrimaryCinematic(0.08, 0.92, 0.08, 0.92)
                            : sampler.SamplePrimaryCinematic(0.02, 0.98, 0.00, 0.30),
                        watchOptions.BlackoutOnBlack);
                    CinematicSample upperSample = ApplyBlackPolicy(sampler.SamplePrimaryCinematic(0.55, 1.00, 0.00, 0.54), watchOptions.BlackoutOnBlack);
                    CinematicSample lowerSample = ApplyBlackPolicy(sampler.SamplePrimaryCinematic(0.55, 1.00, 0.46, 1.00), watchOptions.BlackoutOnBlack);
                    (byte watchStudyR, byte watchStudyG, byte watchStudyB) = CalibrateWizColor(
                        ScaleColor(BoostForWatch(studySample, roomWash: false, watchColorBoost), watchStudyStrength, 255),
                        WizRole.Study);
                    (byte watchUpperR, byte watchUpperG, byte watchUpperB) = CalibrateWizColor(
                        ScaleColor(BoostForWatch(upperSample, roomWash: true, watchColorBoost), watchRearStrength, 230),
                        WizRole.Upper);
                    (byte watchLowerR, byte watchLowerG, byte watchLowerB) = CalibrateWizColor(
                        ScaleColor(BoostForWatch(lowerSample, roomWash: true, watchColorBoost), watchRearStrength, 220),
                        WizRole.Lower);

                    double studyAlpha = GetAdaptiveColorAlpha(studySmoothedR, studySmoothedG, studySmoothedB, watchStudyR, watchStudyG, watchStudyB, 0.38, studySample.IsBlack ? 0.82 : 0.68);
                    double upperAlpha = GetAdaptiveColorAlpha(upperSmoothedR, upperSmoothedG, upperSmoothedB, watchUpperR, watchUpperG, watchUpperB, 0.30, upperSample.IsBlack ? 0.84 : 0.64);
                    double lowerAlpha = GetAdaptiveColorAlpha(lowerSmoothedR, lowerSmoothedG, lowerSmoothedB, watchLowerR, watchLowerG, watchLowerB, 0.32, lowerSample.IsBlack ? 0.84 : 0.66);
                    SmoothColor(ref studySmoothedR, ref studySmoothedG, ref studySmoothedB, watchStudyR, watchStudyG, watchStudyB, studyAlpha);
                    SmoothColor(ref upperSmoothedR, ref upperSmoothedG, ref upperSmoothedB, watchUpperR, watchUpperG, watchUpperB, upperAlpha);
                    SmoothColor(ref lowerSmoothedR, ref lowerSmoothedG, ref lowerSmoothedB, watchLowerR, watchLowerG, watchLowerB, lowerAlpha);
                    byte studyDimming = mode == DirectAmbientMode.StudyOnly
                        ? StudyLampDimming
                        : ScaleDimming(GetWatchDimming(studySample, StudyLampDimming, 12), watchStudyStrength, StudyLampDimming);
                    SmoothValue(ref studySmoothedDimming, studyDimming, studySample.IsBlack ? 0.68 : 0.30);
                    byte upperMaximum = ScaleDimming(upperRole.AmbientMaximum(upper: true), watchRearStrength, 82);
                    byte lowerMaximum = ScaleDimming(lowerRole.AmbientMaximum(upper: false), watchRearStrength, 72);
                    byte upperMinimum = upperMaximum == 0 ? (byte)0 : Math.Min((byte)(watchOptions.BlackoutOnBlack ? 4 : 8), upperMaximum);
                    byte lowerMinimum = lowerMaximum == 0 ? (byte)0 : Math.Min((byte)(watchOptions.BlackoutOnBlack ? 3 : 6), lowerMaximum);
                    SmoothValue(ref upperSmoothedDimming, GetWatchDimming(upperSample, upperMaximum, upperMinimum), upperSample.IsBlack ? 0.82 : 0.20);
                    SmoothValue(ref lowerSmoothedDimming, GetWatchDimming(lowerSample, lowerMaximum, lowerMinimum), lowerSample.IsBlack ? 0.82 : 0.22);

                    bool studyEnabled = watchOptions.Study;
                    bool upperEnabled = upperRole.UsesAmbientSampling();
                    bool lowerEnabled = lowerRole.UsesAmbientSampling();
                    bool lenovoEnabled = watchOptions.LenovoKeyboard;
                    if (mode == DirectAmbientMode.StudyOnly)
                    {
                        SendStudyDaylightWiz(
                            wizClient,
                            studyEnabled,
                            studySample,
                            ClampToByte(studySmoothedR),
                            ClampToByte(studySmoothedG),
                            ClampToByte(studySmoothedB));
                    }
                    else
                    {
                        SendWatchWiz(wizClient, studyEnabled, studySmoothedR, studySmoothedG, studySmoothedB, ClampToByte(studySmoothedDimming), WizRole.Study);
                    }
                    SendWatchWiz(upperWizClient, upperEnabled, upperSmoothedR, upperSmoothedG, upperSmoothedB, ClampToByte(upperSmoothedDimming), WizRole.Upper);
                    SendWatchWiz(lowerWizClient, lowerEnabled, lowerSmoothedR, lowerSmoothedG, lowerSmoothedB, ClampToByte(lowerSmoothedDimming), WizRole.Lower);

                    foreach (RazerAmbientDevice razerDevice in razerDevices)
                    {
                        bool enabled = razerDevice.Role switch
                        {
                            RazerAmbientRole.Keyboard => watchOptions.RazerKeyboard,
                            RazerAmbientRole.Mouse => watchOptions.RazerMouse,
                            RazerAmbientRole.Dock => watchOptions.RazerDock,
                            _ => false
                        };

                        if (enabled)
                            razerDevice.UpdateWatchFromScreen(sampler, watchOptions.BlackoutOnBlack, watchColorBoost, watchRazerStrength);
                        else
                            razerDevice.TurnOff();
                    }

                    foreach (LenovoAmbientDevice lenovoDevice in lenovoDevices)
                    {
                        if (lenovoEnabled)
                            lenovoDevice.UpdateWatchFromScreen(sampler, watchOptions.BlackoutOnBlack, watchColorBoost, watchRazerStrength);
                        else
                            lenovoDevice.TurnOff();
                    }

                    loopCount++;
                    await Task.Delay(frameDelayMs, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                (byte rawStudyR, byte rawStudyG, byte rawStudyB) = sampler.SampleAverage(SampleRegion.TopCenter);
                (byte rawRazerR, byte rawRazerG, byte rawRazerB) = sampler.SampleAverage(SampleRegion.LowerDesk);
                (byte rawUpperR, byte rawUpperG, byte rawUpperB) = sampler.SampleAverage(SampleRegion.UpperRight);
                (byte rawLowerR, byte rawLowerG, byte rawLowerB) = sampler.SampleAverage(SampleRegion.LowerRight);
                (byte studyR, byte studyG, byte studyB) = CalibrateWizColor(BoostForForeground(rawStudyR, rawStudyG, rawStudyB), WizRole.Study);
                (byte razerR, byte razerG, byte razerB) = BoostForForeground(rawRazerR, rawRazerG, rawRazerB);
                (byte upperR, byte upperG, byte upperB) = CalibrateWizColor(BoostForRoomWash(rawUpperR, rawUpperG, rawUpperB), WizRole.Upper);
                (byte lowerR, byte lowerG, byte lowerB) = CalibrateWizColor(BoostForRoomWash(rawLowerR, rawLowerG, rawLowerB), WizRole.Lower);

                studySmoothedR = (studySmoothedR * 0.62) + (studyR * 0.38);
                studySmoothedG = (studySmoothedG * 0.62) + (studyG * 0.38);
                studySmoothedB = (studySmoothedB * 0.62) + (studyB * 0.38);
                razerSmoothedR = (razerSmoothedR * 0.72) + (razerR * 0.28);
                razerSmoothedG = (razerSmoothedG * 0.72) + (razerG * 0.28);
                razerSmoothedB = (razerSmoothedB * 0.72) + (razerB * 0.28);
                upperSmoothedR = (upperSmoothedR * 0.90) + (upperR * 0.10);
                upperSmoothedG = (upperSmoothedG * 0.90) + (upperG * 0.10);
                upperSmoothedB = (upperSmoothedB * 0.90) + (upperB * 0.10);
                lowerSmoothedR = (lowerSmoothedR * 0.92) + (lowerR * 0.08);
                lowerSmoothedG = (lowerSmoothedG * 0.92) + (lowerG * 0.08);
                lowerSmoothedB = (lowerSmoothedB * 0.92) + (lowerB * 0.08);

                byte studyOutR = ClampToByte(studySmoothedR);
                byte studyOutG = ClampToByte(studySmoothedG);
                byte studyOutB = ClampToByte(studySmoothedB);
                byte razerOutR = ClampToByte(razerSmoothedR);
                byte razerOutG = ClampToByte(razerSmoothedG);
                byte razerOutB = ClampToByte(razerSmoothedB);
                byte upperOutR = ClampToByte(upperSmoothedR);
                byte upperOutG = ClampToByte(upperSmoothedG);
                byte upperOutB = ClampToByte(upperSmoothedB);
                byte lowerOutR = ClampToByte(lowerSmoothedR);
                byte lowerOutG = ClampToByte(lowerSmoothedG);
                byte lowerOutB = ClampToByte(lowerSmoothedB);

                SendWiz(wizClient, studyOutR, studyOutG, studyOutB, StudyLampDimming);
                if (mode == DirectAmbientMode.FullRoomTasteful)
                {
                    SendWiz(upperWizClient, upperOutR, upperOutG, upperOutB, UpperRoomDimming);
                    SendWiz(lowerWizClient, lowerOutR, lowerOutG, lowerOutB, LowerRoomDimming);
                    foreach (LenovoAmbientDevice lenovoDevice in lenovoDevices)
                        lenovoDevice.UpdateFromScreen(sampler);

                    loopCount++;
                }
                else
                {
                    SendWizOff(lowerWizClient);
                    SendWizOff(upperWizClient);
                    foreach (LenovoAmbientDevice lenovoDevice in lenovoDevices)
                        lenovoDevice.TurnOff();
                    loopCount++;
                }

                foreach (RazerAmbientDevice razerDevice in razerDevices)
                {
                    if (mode == DirectAmbientMode.FullRoomTasteful)
                        razerDevice.UpdateFromScreen(sampler);
                    else
                        razerDevice.UpdateSolid(razerOutR, razerOutG, razerOutB);
                }

                await Task.Delay(frameDelayMs, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            foreach (RazerAmbientDevice razerDevice in razerDevices)
                razerDevice.Dispose();

            foreach (LenovoAmbientDevice lenovoDevice in lenovoDevices)
                lenovoDevice.Dispose();
        }
    }

    private static List<RazerAmbientDevice> OpenRazerDevices()
    {
        (DirectDeviceDefinition Definition, RazerAmbientRole Role)[] definitions =
        {
            (DirectDeviceDefinition.CreateRazerBlackWidowV3(), RazerAmbientRole.Keyboard),
            (DirectDeviceDefinition.CreateRazerDeathAdderV2ProWireless(), RazerAmbientRole.Mouse),
            (DirectDeviceDefinition.CreateRazerMouseDockChroma(), RazerAmbientRole.Dock)
        };

        List<RazerAmbientDevice> devices = new();
        foreach ((DirectDeviceDefinition definition, RazerAmbientRole role) in definitions)
        {
            HidDevice? hidDevice = HidDeviceLocator.Find(definition.HidTargets).FirstOrDefault();
            if (hidDevice != null && definition.Razer != null)
                devices.Add(new RazerAmbientDevice(hidDevice.DevicePath, definition.Razer, role));
        }

        return devices;
    }

    private static List<LenovoAmbientDevice> OpenLenovoDevices()
    {
        DirectDeviceDefinition definition = DirectDeviceDefinition.CreateLenovoLegion5();
        return HidDeviceLocator.Find(definition.HidTargets).Select(device => new LenovoAmbientDevice(device)).ToList();
    }

    private static void SendWiz(UdpClient client, byte r, byte g, byte b, byte dimming)
    {
        SendWizChannels(client, r, g, b, 0, 0, dimming);
    }

    private static void SendWizChannels(UdpClient client, byte r, byte g, byte b, byte coolWhite, byte warmWhite, byte dimming)
    {
        byte[] payload = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(new
        {
            method = "setPilot",
            @params = new
            {
                state = true,
                r,
                g,
                b,
                c = coolWhite,
                w = warmWhite,
                dimming
            }
        });
        client.Send(payload, payload.Length);
    }

    private static void SendWizOff(UdpClient client)
    {
        byte[] payload = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(new
        {
            method = "setPilot",
            @params = new
            {
                state = false
            }
        });
        client.Send(payload, payload.Length);
    }

    private static void SendWatchWiz(UdpClient client, bool enabled, double r, double g, double b, byte dimming, WizRole role)
    {
        if (!enabled)
        {
            SendWizOff(client);
            return;
        }

        byte outR = ClampToByte(r);
        byte outG = ClampToByte(g);
        byte outB = ClampToByte(b);
        if (Math.Max(outR, Math.Max(outG, outB)) <= 10 || dimming <= 2)
        {
            SendWizOff(client);
            return;
        }

        WizChannels channels = CreateWatchWizChannels(outR, outG, outB, role);
        SendWizChannels(client, channels.R, channels.G, channels.B, channels.CoolWhite, channels.WarmWhite, dimming);
    }

    private static WizChannels CreateWatchWizChannels(byte r, byte g, byte b, WizRole role)
    {
        double max = Math.Max(r, Math.Max(g, b));
        double min = Math.Min(r, Math.Min(g, b));
        double saturation = max <= 0 ? 0 : (max - min) / max;

        // Saturated colors stay RGB-only. White emitters are reserved for
        // neutral scenes so they improve whites without washing out hues.
        if (saturation >= 0.20)
            return new WizChannels(r, g, b, 0, 0);

        double neutralAmount = min * Math.Clamp((0.20 - saturation) / 0.20, 0, 1);
        double extraction = role == WizRole.Study ? 0.90 : 0.72;
        double white = neutralAmount * extraction;
        double residualR = Math.Max(0, r - white);
        double residualG = Math.Max(0, g - white);
        double residualB = Math.Max(0, b - white);
        double warmth = max <= 0 ? 0 : Math.Clamp((r - b) / max, -1, 1);
        double coolShare = Math.Clamp(0.54 - (warmth * 0.42), 0.12, 0.88);

        return new WizChannels(
            ClampToByte(residualR),
            ClampToByte(residualG),
            ClampToByte(residualB),
            ClampToByte(white * coolShare),
            ClampToByte(white * (1 - coolShare)));
    }

    private static void SendStudyDaylightWiz(
        UdpClient client,
        bool enabled,
        CinematicSample sample,
        byte sampledR,
        byte sampledG,
        byte sampledB)
    {
        if (!enabled || sample.IsBlack)
        {
            SendWizOff(client);
            return;
        }

        byte minimum = Math.Min(sampledR, Math.Min(sampledG, sampledB));
        double residualR = sampledR - minimum;
        double residualG = sampledG - minimum;
        double residualB = sampledB - minimum;
        double chroma = Math.Max(residualR, Math.Max(residualG, residualB));
        double colorStrength = Math.Clamp((chroma - 12) / 150.0, 0, 1);
        colorStrength = colorStrength * colorStrength * (3 - (2 * colorStrength));

        double targetPeak = 70 + (185 * colorStrength);
        double colorScale = chroma <= 0 ? 0 : targetPeak / chroma;
        byte tintR = ClampToByte(residualR * colorScale);
        byte tintG = ClampToByte(residualG * colorScale);
        byte tintB = ClampToByte(residualB * colorScale);

        // Neutral scenes stay full daylight. Saturated scenes remain at 100%
        // dimming but become RGB-dominant so the color is physically visible.
        byte coolWhite = ClampToByte(255 - (colorStrength * 195));
        byte warmWhite = ClampToByte(92 - (colorStrength * 70));
        SendWizChannels(client, tintR, tintG, tintB, coolWhite, warmWhite, StudyLampDimming);
    }

    private static void SmoothColor(ref double smoothedR, ref double smoothedG, ref double smoothedB, byte r, byte g, byte b, double alpha)
    {
        smoothedR = (smoothedR * (1 - alpha)) + (r * alpha);
        smoothedG = (smoothedG * (1 - alpha)) + (g * alpha);
        smoothedB = (smoothedB * (1 - alpha)) + (b * alpha);
    }

    private static double GetAdaptiveColorAlpha(
        double currentR,
        double currentG,
        double currentB,
        byte targetR,
        byte targetG,
        byte targetB,
        double minimum,
        double maximum)
    {
        double distance = Math.Sqrt(
            Math.Pow(targetR - currentR, 2) +
            Math.Pow(targetG - currentG, 2) +
            Math.Pow(targetB - currentB, 2));
        double sceneCut = Math.Clamp((distance - 28) / 150.0, 0, 1);
        return minimum + ((maximum - minimum) * sceneCut);
    }

    private static void SmoothValue(ref double smoothed, byte value, double alpha)
    {
        smoothed = (smoothed * (1 - alpha)) + (value * alpha);
    }

    private static (byte r, byte g, byte b) BoostForForeground(byte r, byte g, byte b)
    {
        double max = Math.Max(r, Math.Max(g, b));
        if (max < 22)
            return (80, 32, 170);

        double min = Math.Min(r, Math.Min(g, b));
        double saturation = max <= 0 ? 0 : (max - min) / max;
        double targetMax = saturation < 0.28
            ? Math.Min(215, Math.Max(max * 1.55, 120))
            : Math.Min(235, Math.Max(max * 1.85, 155));
        double scale = Math.Min(2.6, targetMax / max);
        return (ClampToByte(r * scale), ClampToByte(g * scale), ClampToByte(b * scale));
    }

    private static (byte r, byte g, byte b) BoostForRoomWash(byte r, byte g, byte b)
    {
        double max = Math.Max(r, Math.Max(g, b));
        if (max < 18)
            return (18, 12, 28);

        double min = Math.Min(r, Math.Min(g, b));
        double saturation = max <= 0 ? 0 : (max - min) / max;
        double targetMax = saturation < 0.25
            ? Math.Min(135, Math.Max(max * 1.25, 55))
            : Math.Min(160, Math.Max(max * 1.55, 75));
        double scale = Math.Min(1.8, targetMax / max);
        double boostedR = r * scale;
        double boostedG = g * scale;
        double boostedB = b * scale;
        double luminance = (boostedR * 0.2126) + (boostedG * 0.7152) + (boostedB * 0.0722);

        return (
            ClampToByte((boostedR * 0.82) + (luminance * 0.18)),
            ClampToByte((boostedG * 0.82) + (luminance * 0.18)),
            ClampToByte((boostedB * 0.82) + (luminance * 0.18)));
    }

    private static (byte r, byte g, byte b) BoostForWatch(CinematicSample sample, bool roomWash, double colorBoost)
    {
        if (sample.IsBlack)
            return (0, 0, 0);

        double r = sample.R;
        double g = sample.G;
        double b = sample.B;
        double max = Math.Max(r, Math.Max(g, b));
        if (max <= 0)
            return (0, 0, 0);

        double cap = roomWash ? 176 : 245;
        double multiplier = max switch
        {
            < 32 => 1.14,
            < 80 => 1.34,
            < 150 => 1.24,
            _ => 1.08
        };
        double targetMax = Math.Min(cap, max * multiplier);
        double coverage = Math.Clamp(1 - sample.DarkRatio, 0, 1);
        double coverageScale = 0.36 + (Math.Sqrt(coverage) * 0.64);
        targetMax *= coverageScale;
        if (coverage < 0.08)
            targetMax = Math.Min(targetMax, roomWash ? 45 : 78);
        else if (coverage < 0.22)
            targetMax = Math.Min(targetMax, roomWash ? 82 : 132);

        double scale = targetMax / max;
        return AdjustSaturation(
            (ClampToByte(r * scale), ClampToByte(g * scale), ClampToByte(b * scale)),
            Math.Clamp(colorBoost, 0, 2.0));
    }

    private static (byte r, byte g, byte b) AdjustSaturation((byte r, byte g, byte b) color, double multiplier)
    {
        double max = Math.Max(color.r, Math.Max(color.g, color.b));
        double min = Math.Min(color.r, Math.Min(color.g, color.b));
        if (max <= 0 || max - min < 2)
            return color;

        double saturation = (max - min) / max;
        double adjustedSaturation = Math.Clamp(saturation * multiplier, 0, 1);
        double scale = saturation <= 0 ? 1 : adjustedSaturation / saturation;

        return (
            ClampToByte(max - ((max - color.r) * scale)),
            ClampToByte(max - ((max - color.g) * scale)),
            ClampToByte(max - ((max - color.b) * scale)));
    }

    private static CinematicSample ApplyBlackPolicy(CinematicSample sample, bool blackoutOnBlack)
    {
        return sample.IsBlack && !blackoutOnBlack
            ? new CinematicSample(14, 15, 19, false, 1)
            : sample;
    }

    private static byte GetWatchDimming(CinematicSample sample, byte maximum, byte minimum)
    {
        if (sample.IsBlack)
            return 0;

        double screenValue = Math.Max(sample.R, Math.Max(sample.G, sample.B)) / 255.0;
        double coverage = Math.Clamp(1 - sample.DarkRatio, 0, 1);
        if (coverage < 0.10)
            return minimum;

        double sceneLevel = Math.Pow(screenValue, 0.82) * (0.58 + (Math.Sqrt(coverage) * 0.42));
        return ClampToByte(minimum + ((maximum - minimum) * Math.Clamp(sceneLevel, 0, 1)));
    }

    private static double PercentScale(int percent)
    {
        return Math.Clamp(percent, 0, 200) / 100.0;
    }

    private static byte ScaleDimming(byte value, double scale, byte maximum)
    {
        return ClampToByte(Math.Min(maximum, value * Math.Clamp(scale, 0, 2.0)));
    }

    private static (byte r, byte g, byte b) ScaleColor((byte r, byte g, byte b) color, double scale, byte maximum)
    {
        double max = Math.Max(color.r, Math.Max(color.g, color.b));
        if (max <= 0)
            return (0, 0, 0);

        double constrainedScale = Math.Min(Math.Clamp(scale, 0, 2.0), maximum / max);
        return (
            ClampToByte(color.r * constrainedScale),
            ClampToByte(color.g * constrainedScale),
            ClampToByte(color.b * constrainedScale));
    }

    private static (byte r, byte g, byte b) CalibrateWizColor((byte r, byte g, byte b) color, WizRole role)
    {
        double redGain = role == WizRole.Study ? 0.95 : 0.90;
        double greenGain = role == WizRole.Study ? 1.03 : 1.12;
        double blueGain = role == WizRole.Study ? 0.98 : 0.94;
        double r = color.r * redGain;
        double g = color.g * greenGain;
        double b = color.b * blueGain;

        bool warmHue = r > g && g > b && g > 28 && b < r * 0.62;
        if (warmHue)
        {
            r *= 0.96;
            g *= 1.10;
            b *= 0.32;
        }

        bool yellowHue = r > 90 && g > 75 && b < 80 && Math.Abs(r - g) < 95;
        if (yellowHue)
        {
            r *= 0.97;
            g *= 1.05;
            b *= 0.25;
        }

        double saturationGain = role switch
        {
            WizRole.Study => 1.00,
            WizRole.Upper => 1.05,
            WizRole.Lower => 1.03,
            _ => 1.0
        };
        double max = Math.Max(r, Math.Max(g, b));
        double min = Math.Min(r, Math.Min(g, b));
        double saturation = max <= 0 ? 0 : (max - min) / max;
        if (saturation is > 0.05 and < 0.995)
        {
            r = max - ((max - r) * saturationGain);
            g = max - ((max - g) * saturationGain);
            b = max - ((max - b) * saturationGain);
        }

        return (ClampToByte(r), ClampToByte(g), ClampToByte(b));
    }

    private static (byte r, byte g, byte b) BoostForKeyboard(byte r, byte g, byte b)
    {
        double max = Math.Max(r, Math.Max(g, b));
        if (max < 18)
            return (48, 20, 105);

        double scale = Math.Min(2.6, 225 / max);
        return (ClampToByte(r * scale), ClampToByte(g * scale), ClampToByte(b * scale));
    }

    private static (byte r, byte g, byte b) BoostForWatchKeyboard(CinematicSample sample, double colorBoost = 1.0, double strength = 1.0)
    {
        if (sample.IsBlack)
            return (0, 0, 0);

        (byte r, byte g, byte b) = ScaleColor(BoostForWatch(sample, roomWash: false, colorBoost), strength, 225);
        double max = Math.Max(r, Math.Max(g, b));
        if (max <= 0)
            return (0, 0, 0);

        double target = Math.Min(max, 215);
        double scale = target / max;
        return (ClampToByte(r * scale), ClampToByte(g * scale), ClampToByte(b * scale));
    }

    private static byte ClampToByte(double value)
    {
        return (byte)Math.Clamp((int)Math.Round(value), 0, 255);
    }

    private sealed class RazerAmbientDevice : IDisposable
    {
        private readonly WindowsHidFeatureSender _sender;
        private readonly RazerMatrixOptions _options;
        private readonly RazerAmbientRole _role;
        private readonly double[,,] _smoothed;
        private readonly bool[,] _hasSmoothedColor;

        public RazerAmbientDevice(string devicePath, RazerMatrixOptions options, RazerAmbientRole role)
        {
            _sender = new WindowsHidFeatureSender(devicePath);
            _options = options;
            _role = role;
            _smoothed = new double[_options.Rows, _options.Columns, 3];
            _hasSmoothedColor = new bool[_options.Rows, _options.Columns];
        }

        public RazerAmbientRole Role => _role;

        public void UpdateSolid(byte r, byte g, byte b)
        {
            for (int column = 0; column < _options.Columns; column++)
            {
                for (int row = 0; row < _options.Rows; row++)
                    SetSmoothedColor(row, column, r, g, b, 0.45);
            }

            WriteMatrixFromSmoothed();
        }

        public void TurnOff()
        {
            for (int column = 0; column < _options.Columns; column++)
            {
                for (int row = 0; row < _options.Rows; row++)
                {
                    _smoothed[row, column, 0] = 0;
                    _smoothed[row, column, 1] = 0;
                    _smoothed[row, column, 2] = 0;
                    _hasSmoothedColor[row, column] = true;
                }
            }

            WriteMatrixFromSmoothed();
        }

        public void UpdateFromScreen(ScreenSampler sampler)
        {
            if (_role == RazerAmbientRole.Keyboard)
            {
                UpdateKeyboardFromScreen(sampler);
                return;
            }

            SampleRegion region = _role == RazerAmbientRole.Dock ? SampleRegion.DeskDock : SampleRegion.DeskMouse;
            (byte rawR, byte rawG, byte rawB) = sampler.SampleAverage(region);
            (byte r, byte g, byte b) = BoostForKeyboard(rawR, rawG, rawB);
            UpdateSolid(r, g, b);
        }

        public void UpdateWatchFromScreen(ScreenSampler sampler, bool blackoutOnBlack, double colorBoost, double strength)
        {
            if (_role == RazerAmbientRole.Keyboard)
            {
                UpdateWatchKeyboardFromScreen(sampler, blackoutOnBlack, colorBoost, strength);
                return;
            }

            SampleRegion region = _role == RazerAmbientRole.Dock ? SampleRegion.DeskDock : SampleRegion.DeskMouse;
            CinematicSample sample = ApplyBlackPolicy(sampler.SampleCinematic(region), blackoutOnBlack);
            if (sample.IsBlack)
            {
                TurnOff();
                return;
            }

            (byte r, byte g, byte b) = BoostForWatchKeyboard(sample, colorBoost, strength);
            UpdateSolid(r, g, b);
        }

        private void UpdateKeyboardFromScreen(ScreenSampler sampler)
        {
            for (int row = 0; row < _options.Rows; row++)
            {
                for (int column = 0; column < _options.Columns; column++)
                {
                    double startX = column / (double)_options.Columns;
                    double endX = (column + 1) / (double)_options.Columns;
                    double startY = 0.56 + (row / (double)_options.Rows * 0.40);
                    double endY = 0.56 + ((row + 1) / (double)_options.Rows * 0.40);
                    (byte rawR, byte rawG, byte rawB) = sampler.SamplePrimary(startX, endX, startY, endY);
                    (byte r, byte g, byte b) = BoostForKeyboard(rawR, rawG, rawB);
                    SetSmoothedColor(row, column, r, g, b, 0.28);
                }
            }

            WriteMatrixFromSmoothed();
        }

        private void UpdateWatchKeyboardFromScreen(ScreenSampler sampler, bool blackoutOnBlack, double colorBoost, double strength)
        {
            for (int row = 0; row < _options.Rows; row++)
            {
                for (int column = 0; column < _options.Columns; column++)
                {
                    double startX = column / (double)_options.Columns;
                    double endX = (column + 1) / (double)_options.Columns;
                    double startY = 0.56 + (row / (double)_options.Rows * 0.40);
                    double endY = 0.56 + ((row + 1) / (double)_options.Rows * 0.40);
                    CinematicSample sample = ApplyBlackPolicy(sampler.SamplePrimaryCinematic(startX, endX, startY, endY), blackoutOnBlack);
                    (byte r, byte g, byte b) = BoostForWatchKeyboard(sample, colorBoost, strength);
                    SetSmoothedColor(row, column, r, g, b, sample.IsBlack ? 1.0 : 0.36);
                }
            }

            WriteMatrixFromSmoothed();
        }

        private void SetSmoothedColor(int row, int column, byte r, byte g, byte b, double alpha)
        {
            if (!_hasSmoothedColor[row, column])
            {
                _smoothed[row, column, 0] = r;
                _smoothed[row, column, 1] = g;
                _smoothed[row, column, 2] = b;
                _hasSmoothedColor[row, column] = true;
                return;
            }

            _smoothed[row, column, 0] = (_smoothed[row, column, 0] * (1 - alpha)) + (r * alpha);
            _smoothed[row, column, 1] = (_smoothed[row, column, 1] * (1 - alpha)) + (g * alpha);
            _smoothed[row, column, 2] = (_smoothed[row, column, 2] * (1 - alpha)) + (b * alpha);
        }

        private void WriteMatrixFromSmoothed()
        {
            for (int row = 0; row < _options.Rows; row++)
            {
                byte[] rgb = new byte[_options.Columns * 3];
                for (int column = 0; column < _options.Columns; column++)
                {
                    int offset = column * 3;
                    rgb[offset] = ClampToByte(_smoothed[row, column, 0]);
                    rgb[offset + 1] = ClampToByte(_smoothed[row, column, 1]);
                    rgb[offset + 2] = ClampToByte(_smoothed[row, column, 2]);
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

    private sealed class LenovoAmbientDevice : IDisposable
    {
        private readonly HidDevice _device;
        private readonly double[,] _smoothed = new double[4, 3];
        private readonly bool[] _hasSmoothedColor = new bool[4];
        private HidStream? _stream;

        public LenovoAmbientDevice(HidDevice device)
        {
            _device = device;
        }

        public void UpdateFromScreen(ScreenSampler sampler)
        {
            try
            {
                byte[] report = new byte[33];
                report[0] = 0xCC;
                report[1] = 0x16;
                report[2] = 0x01;
                report[3] = 0x01;
                report[4] = 0x02;

                for (int zone = 0; zone < 4; zone++)
                {
                    const double keyboardRegionStartX = 0.52;
                    const double keyboardRegionEndX = 0.98;
                    double regionWidth = keyboardRegionEndX - keyboardRegionStartX;
                    double startX = keyboardRegionStartX + (regionWidth * zone / 4.0);
                    double endX = keyboardRegionStartX + (regionWidth * (zone + 1) / 4.0);
                    (byte rawR, byte rawG, byte rawB) = sampler.SamplePrimary(startX, endX, 0.54, 0.98);
                    (byte r, byte g, byte b) = BoostForKeyboard(rawR, rawG, rawB);
                    (byte outR, byte outG, byte outB) = SmoothZone(zone, r, g, b);

                    int offset = 5 + (zone * 3);
                    report[offset] = outR;
                    report[offset + 1] = outG;
                    report[offset + 2] = outB;
                }

                GetStream().SetFeature(report);
            }
            catch
            {
                ResetStream();
            }
        }

        public void UpdateWatchFromScreen(ScreenSampler sampler, bool blackoutOnBlack, double colorBoost, double strength)
        {
            try
            {
                byte[] report = CreateBaseReport();
                for (int zone = 0; zone < 4; zone++)
                {
                    const double keyboardRegionStartX = 0.52;
                    const double keyboardRegionEndX = 0.98;
                    double regionWidth = keyboardRegionEndX - keyboardRegionStartX;
                    double startX = keyboardRegionStartX + (regionWidth * zone / 4.0);
                    double endX = keyboardRegionStartX + (regionWidth * (zone + 1) / 4.0);
                    CinematicSample sample = ApplyBlackPolicy(sampler.SamplePrimaryCinematic(startX, endX, 0.54, 0.98), blackoutOnBlack);
                    (byte r, byte g, byte b) = BoostForWatchKeyboard(sample, colorBoost, strength);
                    (byte outR, byte outG, byte outB) = sample.IsBlack
                        ? SetZoneImmediate(zone, 0, 0, 0)
                        : SmoothZone(zone, r, g, b);

                    int offset = 5 + (zone * 3);
                    report[offset] = outR;
                    report[offset + 1] = outG;
                    report[offset + 2] = outB;
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
                for (int zone = 0; zone < 4; zone++)
                    SetZoneImmediate(zone, 0, 0, 0);
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

        private (byte r, byte g, byte b) SetZoneImmediate(int zone, byte r, byte g, byte b)
        {
            _smoothed[zone, 0] = r;
            _smoothed[zone, 1] = g;
            _smoothed[zone, 2] = b;
            _hasSmoothedColor[zone] = true;
            return (r, g, b);
        }

        private (byte r, byte g, byte b) SmoothZone(int zone, byte r, byte g, byte b)
        {
            const double alpha = 0.24;
            if (!_hasSmoothedColor[zone])
            {
                _smoothed[zone, 0] = r;
                _smoothed[zone, 1] = g;
                _smoothed[zone, 2] = b;
                _hasSmoothedColor[zone] = true;
            }
            else
            {
                _smoothed[zone, 0] = (_smoothed[zone, 0] * (1 - alpha)) + (r * alpha);
                _smoothed[zone, 1] = (_smoothed[zone, 1] * (1 - alpha)) + (g * alpha);
                _smoothed[zone, 2] = (_smoothed[zone, 2] * (1 - alpha)) + (b * alpha);
            }

            return (ClampToByte(_smoothed[zone, 0]), ClampToByte(_smoothed[zone, 1]), ClampToByte(_smoothed[zone, 2]));
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

    private sealed class ScreenSampler : IDisposable
    {
        private const int Srccopy = 0x00CC0020;
        private const int DIB_RGB_COLORS = 0;
        private const int BI_RGB = 0;
        private const int SM_CXSCREEN = 0;
        private const int SM_CYSCREEN = 1;
        private const int SM_XVIRTUALSCREEN = 76;
        private const int SM_YVIRTUALSCREEN = 77;
        private const int SM_CXVIRTUALSCREEN = 78;
        private const int SM_CYVIRTUALSCREEN = 79;
        private const uint MONITORINFOF_PRIMARY = 1;

        private readonly int _width;
        private readonly int _height;
        private readonly IntPtr _screenDc;
        private readonly IntPtr _memoryDc;
        private readonly IntPtr _bitmap;
        private readonly IntPtr _previousObject;
        private readonly byte[] _pixels;
        private readonly ScreenArea _captureVirtualArea;
        private readonly DX11ScreenCaptureService? _dx11CaptureService;
        private readonly DX11ScreenCapture? _dx11Capture;
        private readonly CaptureZone<ColorBGRA>? _dx11CaptureZone;
        private ScreenArea _virtualArea;
        private ScreenArea _primaryArea;
        private ScreenArea? _laptopArea;
        private string _primaryDeviceName = string.Empty;

        public ScreenSampler(int width, int height, ILogger logger)
        {
            RefreshDisplayAreas();
            try
            {
                _dx11CaptureService = new DX11ScreenCaptureService();
                Display? primaryDisplay = _dx11CaptureService
                    .GetGraphicsCards()
                    .SelectMany(card => _dx11CaptureService.GetDisplays(card))
                    .FirstOrDefault(display => string.Equals(display.DeviceName, _primaryDeviceName, StringComparison.OrdinalIgnoreCase));
                if (primaryDisplay is Display display && display.Width > 0 && display.Height > 0)
                {
                    _dx11Capture = _dx11CaptureService.GetScreenCapture(display);
                    _dx11Capture.Timeout = 35;
                    int downscaleLevel = CalculateDownscaleLevel(display.Width, display.Height, width, height);
                    _dx11CaptureZone = _dx11Capture.RegisterCaptureZone(0, 0, display.Width, display.Height, downscaleLevel);
                    _width = _dx11CaptureZone.Width;
                    _height = _dx11CaptureZone.Height;
                    _pixels = new byte[_width * _height * 4];
                    _virtualArea = new ScreenArea(0, 0, display.Width, display.Height);
                    _primaryArea = _virtualArea;
                    _captureVirtualArea = _virtualArea;
                    logger.Information(
                        "Direct ambient capture backend: DX11 ({Width}x{Height} -> {CaptureWidth}x{CaptureHeight})",
                        display.Width,
                        display.Height,
                        _width,
                        _height);
                    return;
                }

                _dx11CaptureService.Dispose();
                _dx11CaptureService = null;
            }
            catch (Exception exception)
            {
                _dx11CaptureZone = null;
                _dx11Capture = null;
                _dx11CaptureService?.Dispose();
                _dx11CaptureService = null;
                logger.Warning(exception, "DX11 ambient capture initialization failed; using GDI fallback");
            }

            _width = width;
            _height = height;
            _pixels = new byte[_width * _height * 4];
            _screenDc = GetDC(IntPtr.Zero);
            _memoryDc = CreateCompatibleDC(_screenDc);
            _bitmap = CreateCompatibleBitmap(_screenDc, _width, _height);
            _previousObject = SelectObject(_memoryDc, _bitmap);
            SetStretchBltMode(_memoryDc, 4);
            _captureVirtualArea = _virtualArea;
            logger.Information("Direct ambient capture backend: GDI fallback ({Width}x{Height})", _width, _height);
        }

        private bool _captureValid;

        public void Capture()
        {
            if (_dx11Capture != null && _dx11CaptureZone != null)
            {
                if (_dx11Capture.CaptureScreen())
                {
                    using (_dx11CaptureZone.Lock())
                        _dx11CaptureZone.RawBuffer.CopyTo(_pixels);
                    _captureValid = true;
                }

                return;
            }

            RefreshDisplayAreas();
            if (_virtualArea != _captureVirtualArea)
                throw new InvalidOperationException("Display geometry changed while ambient capture was active.");

            bool copied = StretchBlt(
                _memoryDc,
                0,
                0,
                _width,
                _height,
                _screenDc,
                _virtualArea.Left,
                _virtualArea.Top,
                _virtualArea.Width,
                _virtualArea.Height,
                Srccopy);
            if (!copied)
                throw new ExternalException("Ambient screen capture failed to copy the desktop frame.");

            BitmapInfo bitmapInfo = new()
            {
                Header = new BitmapInfoHeader
                {
                    Size = Marshal.SizeOf<BitmapInfoHeader>(),
                    Width = _width,
                    Height = -_height,
                    Planes = 1,
                    BitCount = 32,
                    Compression = BI_RGB
                }
            };

            int lines = GetDIBits(_memoryDc, _bitmap, 0, (uint)_height, _pixels, ref bitmapInfo, DIB_RGB_COLORS);
            if (lines != _height)
                throw new ExternalException($"Ambient screen capture returned {lines} of {_height} lines.");

            _captureValid = true;
        }

        private static int CalculateDownscaleLevel(int sourceWidth, int sourceHeight, int targetWidth, int targetHeight)
        {
            int level = 0;
            while (level < 6)
            {
                int nextScale = 1 << (level + 1);
                if (sourceWidth / nextScale < targetWidth || sourceHeight / nextScale < targetHeight)
                    break;
                level++;
            }

            return level;
        }

        public (byte r, byte g, byte b) SampleAverage(SampleRegion region)
        {
            return region switch
            {
                SampleRegion.TopCenter => SampleArea(_primaryArea, 0.12, 0.88, 0.00, 0.34),
                SampleRegion.LowerDesk => SampleArea(_primaryArea, 0.00, 1.00, 0.58, 1.00),
                SampleRegion.UpperRight => SampleArea(_primaryArea, 0.58, 0.98, 0.00, 0.42),
                SampleRegion.LowerRight => SampleArea(_primaryArea, 0.58, 0.98, 0.50, 0.96),
                SampleRegion.DeskMouse => SampleArea(_primaryArea, 0.70, 0.98, 0.62, 0.98),
                SampleRegion.DeskDock => SampleArea(_primaryArea, 0.50, 0.78, 0.55, 0.95),
                _ => SampleArea(_primaryArea, 0.00, 1.00, 0.00, 1.00)
            };
        }

        public CinematicSample SampleCinematic(SampleRegion region)
        {
            return region switch
            {
                SampleRegion.TopCenter => SampleAreaCinematic(_primaryArea, 0.18, 0.82, 0.02, 0.38),
                SampleRegion.UpperRight => SampleAreaCinematic(_primaryArea, 0.54, 0.98, 0.02, 0.48),
                SampleRegion.LowerRight => SampleAreaCinematic(_primaryArea, 0.54, 0.98, 0.48, 0.98),
                SampleRegion.DeskMouse => SampleAreaCinematic(_primaryArea, 0.72, 0.99, 0.60, 0.99),
                SampleRegion.DeskDock => SampleAreaCinematic(_primaryArea, 0.48, 0.76, 0.54, 0.96),
                _ => SampleAreaCinematic(_primaryArea, 0.00, 1.00, 0.00, 1.00)
            };
        }

        public (byte r, byte g, byte b) SamplePrimary(double startX, double endX, double startY, double endY)
        {
            return SampleArea(_primaryArea, startX, endX, startY, endY);
        }

        public CinematicSample SamplePrimaryCinematic(double startX, double endX, double startY, double endY)
        {
            return SampleAreaCinematic(_primaryArea, startX, endX, startY, endY);
        }

        public (byte r, byte g, byte b) SampleLaptop(double startX, double endX, double startY, double endY)
        {
            return SampleArea(_laptopArea ?? _primaryArea, startX, endX, startY, endY);
        }

        private (byte r, byte g, byte b) SampleArea(ScreenArea area, double startX, double endX, double startY, double endY)
        {
            if (!_captureValid)
                Capture();
            if (!_captureValid || !area.IsValid)
                return (80, 32, 170);

            (int sampleStartX, int sampleEndX, int sampleStartY, int sampleEndY) = GetSampleBounds(area, startX, endX, startY, endY);

            long r = 0;
            long g = 0;
            long b = 0;
            int count = 0;
            for (int y = sampleStartY; y < sampleEndY; y++)
            {
                int rowOffset = y * _width * 4;
                for (int x = sampleStartX; x < sampleEndX; x++)
                {
                    int i = rowOffset + (x * 4);
                    b += _pixels[i];
                    g += _pixels[i + 1];
                    r += _pixels[i + 2];
                    count++;
                }
            }

            if (count == 0)
                return (80, 32, 170);

            return ((byte)(r / count), (byte)(g / count), (byte)(b / count));
        }

        private CinematicSample SampleAreaCinematic(ScreenArea area, double startX, double endX, double startY, double endY)
        {
            if (!_captureValid)
                Capture();
            if (!_captureValid || !area.IsValid)
                return new CinematicSample(0, 0, 0, true, 1);

            (int sampleStartX, int sampleEndX, int sampleStartY, int sampleEndY) = GetSampleBounds(area, startX, endX, startY, endY);
            const int hueBinCount = 24;
            double[] hueWeights = new double[hueBinCount];
            double[] hueLinearR = new double[hueBinCount];
            double[] hueLinearG = new double[hueBinCount];
            double[] hueLinearB = new double[hueBinCount];
            double overallLinearR = 0;
            double overallLinearG = 0;
            double overallLinearB = 0;
            double overallWeight = 0;
            double coloredWeight = 0;
            double totalLuminance = 0;
            int darkCount = 0;
            int count = 0;

            for (int y = sampleStartY; y < sampleEndY; y++)
            {
                int rowOffset = y * _width * 4;
                for (int x = sampleStartX; x < sampleEndX; x++)
                {
                    int i = rowOffset + (x * 4);
                    byte b = _pixels[i];
                    byte g = _pixels[i + 1];
                    byte r = _pixels[i + 2];
                    double max = Math.Max(r, Math.Max(g, b));
                    double min = Math.Min(r, Math.Min(g, b));
                    double luminance = (r * 0.2126) + (g * 0.7152) + (b * 0.0722);
                    totalLuminance += luminance;
                    count++;

                    if (max <= 14)
                    {
                        darkCount++;
                        continue;
                    }

                    double saturation = max <= 0 ? 0 : (max - min) / max;
                    double brightness = max / 255.0;
                    double weight = 0.18 + (Math.Pow(brightness, 0.82) * 0.82);
                    double linearR = SrgbToLinear(r / 255.0);
                    double linearG = SrgbToLinear(g / 255.0);
                    double linearB = SrgbToLinear(b / 255.0);
                    overallLinearR += linearR * weight;
                    overallLinearG += linearG * weight;
                    overallLinearB += linearB * weight;
                    overallWeight += weight;

                    if (saturation < 0.16)
                        continue;

                    double hue = RgbToHue(r, g, b, max, min);
                    int hueBin = Math.Clamp((int)Math.Floor(hue / 360.0 * hueBinCount), 0, hueBinCount - 1);
                    double hueWeight = weight * (0.30 + (Math.Pow(saturation, 0.78) * 0.70));
                    hueWeights[hueBin] += hueWeight;
                    hueLinearR[hueBin] += linearR * hueWeight;
                    hueLinearG[hueBin] += linearG * hueWeight;
                    hueLinearB[hueBin] += linearB * hueWeight;
                    coloredWeight += hueWeight;
                }
            }

            if (count == 0)
                return new CinematicSample(0, 0, 0, true, 1);

            double darkRatio = darkCount / (double)count;
            double averageLuminance = totalLuminance / count;
            bool isBlack = averageLuminance < 5.5 ||
                           (darkRatio >= 0.88 && averageLuminance < 18) ||
                           (darkRatio >= 0.74 && averageLuminance < 9.5);
            if (isBlack)
                return new CinematicSample(0, 0, 0, true, darkRatio);

            if (overallWeight <= 0)
                return new CinematicSample(0, 0, 0, true, darkRatio);

            int dominantBin = 0;
            double dominantClusterWeight = 0;
            for (int bin = 0; bin < hueBinCount; bin++)
            {
                double clusterWeight =
                    hueWeights[(bin + hueBinCount - 1) % hueBinCount] +
                    hueWeights[bin] +
                    hueWeights[(bin + 1) % hueBinCount];
                if (clusterWeight > dominantClusterWeight)
                {
                    dominantBin = bin;
                    dominantClusterWeight = clusterWeight;
                }
            }

            bool useDominantColor =
                coloredWeight / overallWeight >= 0.10 &&
                dominantClusterWeight / overallWeight >= 0.075 &&
                dominantClusterWeight / Math.Max(coloredWeight, 0.001) >= 0.28;
            if (useDominantColor)
            {
                int previousBin = (dominantBin + hueBinCount - 1) % hueBinCount;
                int nextBin = (dominantBin + 1) % hueBinCount;
                double clusterLinearR = hueLinearR[previousBin] + hueLinearR[dominantBin] + hueLinearR[nextBin];
                double clusterLinearG = hueLinearG[previousBin] + hueLinearG[dominantBin] + hueLinearG[nextBin];
                double clusterLinearB = hueLinearB[previousBin] + hueLinearB[dominantBin] + hueLinearB[nextBin];
                return new CinematicSample(
                    ClampToByte(LinearToSrgb(clusterLinearR / dominantClusterWeight) * 255),
                    ClampToByte(LinearToSrgb(clusterLinearG / dominantClusterWeight) * 255),
                    ClampToByte(LinearToSrgb(clusterLinearB / dominantClusterWeight) * 255),
                    false,
                    darkRatio);
            }

            return new CinematicSample(
                ClampToByte(LinearToSrgb(overallLinearR / overallWeight) * 255),
                ClampToByte(LinearToSrgb(overallLinearG / overallWeight) * 255),
                ClampToByte(LinearToSrgb(overallLinearB / overallWeight) * 255),
                false,
                darkRatio);
        }

        private static double RgbToHue(byte r, byte g, byte b, double max, double min)
        {
            double range = max - min;
            if (range <= 0)
                return 0;

            double hue = max == r
                ? 60 * (((g - b) / range) % 6)
                : max == g
                    ? 60 * (((b - r) / range) + 2)
                    : 60 * (((r - g) / range) + 4);
            return hue < 0 ? hue + 360 : hue;
        }

        private static double SrgbToLinear(double value)
        {
            return value <= 0.04045
                ? value / 12.92
                : Math.Pow((value + 0.055) / 1.055, 2.4);
        }

        private static double LinearToSrgb(double value)
        {
            value = Math.Clamp(value, 0, 1);
            return value <= 0.0031308
                ? value * 12.92
                : (1.055 * Math.Pow(value, 1 / 2.4)) - 0.055;
        }

        private (int startX, int endX, int startY, int endY) GetSampleBounds(ScreenArea area, double startX, double endX, double startY, double endY)
        {
            startX = Math.Clamp(startX, 0, 1);
            endX = Math.Clamp(endX, startX, 1);
            startY = Math.Clamp(startY, 0, 1);
            endY = Math.Clamp(endY, startY, 1);

            double sourceStartX = area.Left + (area.Width * startX);
            double sourceEndX = area.Left + (area.Width * endX);
            double sourceStartY = area.Top + (area.Height * startY);
            double sourceEndY = area.Top + (area.Height * endY);

            int sampleStartX = Math.Clamp((int)Math.Floor((sourceStartX - _virtualArea.Left) / _virtualArea.Width * _width), 0, _width - 1);
            int sampleEndX = Math.Clamp((int)Math.Ceiling((sourceEndX - _virtualArea.Left) / _virtualArea.Width * _width), sampleStartX + 1, _width);
            int sampleStartY = Math.Clamp((int)Math.Floor((sourceStartY - _virtualArea.Top) / _virtualArea.Height * _height), 0, _height - 1);
            int sampleEndY = Math.Clamp((int)Math.Ceiling((sourceEndY - _virtualArea.Top) / _virtualArea.Height * _height), sampleStartY + 1, _height);

            return (sampleStartX, sampleEndX, sampleStartY, sampleEndY);
        }

        private void RefreshDisplayAreas()
        {
            int virtualLeft = GetSystemMetrics(SM_XVIRTUALSCREEN);
            int virtualTop = GetSystemMetrics(SM_YVIRTUALSCREEN);
            int virtualWidth = GetSystemMetrics(SM_CXVIRTUALSCREEN);
            int virtualHeight = GetSystemMetrics(SM_CYVIRTUALSCREEN);
            if (virtualWidth <= 0 || virtualHeight <= 0)
            {
                virtualLeft = 0;
                virtualTop = 0;
                virtualWidth = Math.Max(1, GetSystemMetrics(SM_CXSCREEN));
                virtualHeight = Math.Max(1, GetSystemMetrics(SM_CYSCREEN));
            }

            _virtualArea = new ScreenArea(virtualLeft, virtualTop, virtualLeft + virtualWidth, virtualTop + virtualHeight);
            _primaryArea = new ScreenArea(0, 0, GetSystemMetrics(SM_CXSCREEN), GetSystemMetrics(SM_CYSCREEN));
            _laptopArea = null;

            List<MonitorArea> monitors = GetMonitorAreas();
            foreach (MonitorArea monitor in monitors)
            {
                if (monitor.IsPrimary)
                {
                    _primaryArea = monitor.Bounds;
                    _primaryDeviceName = monitor.DeviceName;
                }
            }

            foreach (MonitorArea monitor in monitors.OrderBy(monitor => monitor.Bounds.Left))
            {
                if (monitor.IsPrimary)
                    continue;

                if (monitor.Bounds.Left >= _primaryArea.Right - 8)
                {
                    _laptopArea = monitor.Bounds;
                    break;
                }
            }

            if (_laptopArea == null)
            {
                foreach (MonitorArea monitor in monitors.Where(monitor => !monitor.IsPrimary).OrderByDescending(monitor => monitor.Bounds.Left))
                {
                    _laptopArea = monitor.Bounds;
                    break;
                }
            }
        }

        private static List<MonitorArea> GetMonitorAreas()
        {
            List<MonitorArea> monitors = new();
            MonitorEnumProc callback = (IntPtr monitorHandle, IntPtr monitorDc, ref Rect monitorRect, IntPtr data) =>
            {
                MonitorInfoEx monitorInfo = new()
                {
                    Size = Marshal.SizeOf<MonitorInfoEx>()
                };

                if (GetMonitorInfo(monitorHandle, ref monitorInfo))
                {
                    monitors.Add(new MonitorArea(
                        new ScreenArea(
                            monitorInfo.Monitor.Left,
                            monitorInfo.Monitor.Top,
                            monitorInfo.Monitor.Right,
                            monitorInfo.Monitor.Bottom),
                        (monitorInfo.Flags & MONITORINFOF_PRIMARY) != 0,
                        monitorInfo.DeviceName ?? string.Empty));
                }
                else
                {
                    monitors.Add(new MonitorArea(
                        new ScreenArea(monitorRect.Left, monitorRect.Top, monitorRect.Right, monitorRect.Bottom),
                        false,
                        string.Empty));
                }

                return true;
            };

            EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, callback, IntPtr.Zero);
            return monitors;
        }

        public void Dispose()
        {
            _dx11CaptureService?.Dispose();
            if (_memoryDc != IntPtr.Zero)
            {
                SelectObject(_memoryDc, _previousObject);
                DeleteObject(_bitmap);
                DeleteDC(_memoryDc);
            }
            if (_screenDc != IntPtr.Zero)
                ReleaseDC(IntPtr.Zero, _screenDc);
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hwnd);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hwnd, IntPtr dc);

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int index);

        private delegate bool MonitorEnumProc(IntPtr monitorHandle, IntPtr monitorDc, ref Rect monitorRect, IntPtr data);

        [DllImport("user32.dll")]
        private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr clipRect, MonitorEnumProc enumProc, IntPtr data);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool GetMonitorInfo(IntPtr monitorHandle, ref MonitorInfoEx monitorInfo);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleDC(IntPtr dc);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleBitmap(IntPtr dc, int width, int height);

        [DllImport("gdi32.dll")]
        private static extern IntPtr SelectObject(IntPtr dc, IntPtr obj);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr obj);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteDC(IntPtr dc);

        [DllImport("gdi32.dll")]
        private static extern bool SetStretchBltMode(IntPtr dc, int mode);

        [DllImport("gdi32.dll")]
        private static extern bool StretchBlt(IntPtr destDc, int destX, int destY, int destWidth, int destHeight, IntPtr sourceDc, int sourceX, int sourceY, int sourceWidth, int sourceHeight, int rasterOperation);

        [DllImport("gdi32.dll")]
        private static extern int GetDIBits(IntPtr dc, IntPtr bitmap, uint startScan, uint scanLines, byte[] bits, ref BitmapInfo bitmapInfo, int usage);

        [StructLayout(LayoutKind.Sequential)]
        private struct BitmapInfo
        {
            public BitmapInfoHeader Header;
            public uint Colors;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BitmapInfoHeader
        {
            public int Size;
            public int Width;
            public int Height;
            public short Planes;
            public short BitCount;
            public int Compression;
            public int SizeImage;
            public int XPelsPerMeter;
            public int YPelsPerMeter;
            public int ClrUsed;
            public int ClrImportant;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Rect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct MonitorInfoEx
        {
            public int Size;
            public Rect Monitor;
            public Rect WorkArea;
            public uint Flags;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string DeviceName;
        }

        private readonly record struct ScreenArea(int Left, int Top, int Right, int Bottom)
        {
            public int Width => Math.Max(0, Right - Left);
            public int Height => Math.Max(0, Bottom - Top);
            public bool IsValid => Width > 0 && Height > 0;
        }

        private readonly record struct MonitorArea(ScreenArea Bounds, bool IsPrimary, string DeviceName);
    }

    private enum SampleRegion
    {
        Full,
        TopCenter,
        LowerDesk,
        UpperRight,
        LowerRight,
        DeskMouse,
        DeskDock
    }

    private enum RazerAmbientRole
    {
        Keyboard,
        Mouse,
        Dock
    }

    private enum WizRole
    {
        Study,
        Upper,
        Lower
    }

    private enum DirectAmbientMode
    {
        StudyOnly,
        FullRoomTasteful,
        WatchCinematic
    }

    private readonly record struct WizChannels(byte R, byte G, byte B, byte CoolWhite, byte WarmWhite);
    private readonly record struct CinematicSample(byte R, byte G, byte B, bool IsBlack, double DarkRatio);
}
