using Artemis.Core;
using Artemis.Core.DeviceProviders;
using Artemis.Core.Services;
using RGB.NET.Core;
using Serilog;

namespace Artemis.Plugins.DirectDevices;

[PluginFeature(Name = "Direct Room Device Provider")]
public class DirectDevicesProvider : DeviceProvider
{
    private readonly IDeviceService _deviceService;
    private readonly ILogger _logger;
    private readonly PluginSetting<string> _studyLampIp;
    private readonly PluginSetting<string> _lowerLampIp;
    private readonly PluginSetting<string> _upperLampIp;
    private readonly PluginSetting<bool> _enableLenovo;
    private readonly PluginSetting<bool> _enableRazer;
    private readonly PluginSetting<bool> _enableDirectAmbient;
    private readonly PluginSetting<string> _directAmbientMode;
    private readonly PluginSetting<bool> _enableDirectCs;
    private readonly PluginSetting<bool> _enableDirectValorant;
    private readonly PluginSetting<bool> _watchStudyEnabled;
    private readonly PluginSetting<bool> _watchUpperEnabled;
    private readonly PluginSetting<bool> _watchLowerEnabled;
    private readonly PluginSetting<string> _upperLightRole;
    private readonly PluginSetting<string> _lowerLightRole;
    private readonly PluginSetting<string> _upperGameRole;
    private readonly PluginSetting<string> _lowerGameRole;
    private readonly PluginSetting<bool> _watchRazerKeyboardEnabled;
    private readonly PluginSetting<bool> _watchRazerMouseEnabled;
    private readonly PluginSetting<bool> _watchRazerDockEnabled;
    private readonly PluginSetting<bool> _watchLenovoKeyboardEnabled;
    private readonly PluginSetting<bool> _blackoutOnBlack;
    private readonly PluginSetting<int> _ambientFps;
    private readonly PluginSetting<int> _gameFps;
    private readonly PluginSetting<int> _watchStudyStrength;
    private readonly PluginSetting<int> _watchRearStrength;
    private readonly PluginSetting<int> _watchRazerStrength;
    private readonly PluginSetting<int> _watchColorBoost;
    private readonly PluginSetting<string> _csNumpadLayout;
    private readonly PluginSetting<int> _csFlashIntensity;
    private readonly PluginSetting<int> _csFireIntensity;
    private readonly PluginSetting<int> _csSmokeIntensity;
    private readonly PluginSetting<int> _csDeathIntensity;
    private readonly PluginSetting<int> _csImpactIntensity;
    private readonly PluginSetting<int> _csBombIntensity;
    private readonly PluginSetting<int> _csClutchIntensity;
    private readonly PluginSetting<int> _csTeamContrast;
    private readonly PluginSetting<int> _csUtilityBrightness;
    private DirectAmbientController? _ambientController;
    private DirectCsController? _csController;
    private DirectValorantController? _valorantController;

    public DirectDevicesProvider(IDeviceService deviceService, PluginSettings settings, ILogger logger)
    {
        _deviceService = deviceService;
        _logger = logger;
        _studyLampIp = settings.GetSetting("StudyLampIp", "");
        _lowerLampIp = settings.GetSetting("LowerLampIp", "");
        _upperLampIp = settings.GetSetting("UpperLampIp", "");
        _enableLenovo = settings.GetSetting("EnableLenovo", false);
        _enableRazer = settings.GetSetting("EnableRazer", true);
        _enableDirectAmbient = settings.GetSetting("EnableDirectAmbient", false);
        _directAmbientMode = settings.GetSetting("DirectAmbientMode", "StudyOnly");
        _enableDirectCs = settings.GetSetting("EnableDirectCs", true);
        _enableDirectValorant = settings.GetSetting("EnableDirectValorant", true);
        _watchStudyEnabled = settings.GetSetting("WatchStudyEnabled", true);
        _watchUpperEnabled = settings.GetSetting("WatchUpperEnabled", true);
        _watchLowerEnabled = settings.GetSetting("WatchLowerEnabled", true);
        _upperLightRole = settings.GetSetting("UpperLightRole", _watchUpperEnabled.Value ? "SoftDepth" : "Off");
        _lowerLightRole = settings.GetSetting("LowerLightRole", _watchLowerEnabled.Value ? "SoftDepth" : "Off");
        _upperGameRole = settings.GetSetting("UpperGameRole", "ObjectiveAlerts");
        _lowerGameRole = settings.GetSetting("LowerGameRole", "HealthDamage");
        _watchRazerKeyboardEnabled = settings.GetSetting("WatchRazerKeyboardEnabled", true);
        _watchRazerMouseEnabled = settings.GetSetting("WatchRazerMouseEnabled", true);
        _watchRazerDockEnabled = settings.GetSetting("WatchRazerDockEnabled", true);
        _watchLenovoKeyboardEnabled = settings.GetSetting("WatchLenovoKeyboardEnabled", false);
        _blackoutOnBlack = settings.GetSetting("BlackoutOnBlack", true);
        _ambientFps = settings.GetSetting("AmbientFps", LightingFrameRate.DefaultAmbientFps);
        _gameFps = settings.GetSetting("GameFps", LightingFrameRate.DefaultGameFps);
        _watchStudyStrength = settings.GetSetting("WatchStudyStrength", 135);
        _watchRearStrength = settings.GetSetting("WatchRearStrength", 165);
        _watchRazerStrength = settings.GetSetting("WatchRazerStrength", 120);
        _watchColorBoost = settings.GetSetting("WatchColorBoost", 108);
        _csNumpadLayout = settings.GetSetting("CsNumpadLayout", CsNumpadLayout.DefaultSerialized);
        CsEventTuning defaultTuning = CsEventTuning.Default;
        _csFlashIntensity = settings.GetSetting("CsFlashIntensity", defaultTuning.FlashIntensity);
        _csFireIntensity = settings.GetSetting("CsFireIntensity", defaultTuning.FireIntensity);
        _csSmokeIntensity = settings.GetSetting("CsSmokeIntensity", defaultTuning.SmokeIntensity);
        _csDeathIntensity = settings.GetSetting("CsDeathIntensity", defaultTuning.DeathIntensity);
        _csImpactIntensity = settings.GetSetting("CsImpactIntensity", defaultTuning.ImpactIntensity);
        _csBombIntensity = settings.GetSetting("CsBombIntensity", defaultTuning.BombIntensity);
        _csClutchIntensity = settings.GetSetting("CsClutchIntensity", defaultTuning.ClutchIntensity);
        _csTeamContrast = settings.GetSetting("CsTeamContrast", defaultTuning.TeamContrast);
        _csUtilityBrightness = settings.GetSetting("CsUtilityBrightness", defaultTuning.UtilityBrightness);

        CreateMissingLedsSupported = false;
        RemoveExcessiveLedsSupported = true;
    }

    public override DirectRgbDeviceProvider RgbDeviceProvider => DirectRgbDeviceProvider.Instance;

    public override void Enable()
    {
        DirectLightingRuntimeState.SetAmbientControlActive(_enableDirectAmbient.Value);
        RgbDeviceProvider.Exception += ProviderOnException;
        RgbDeviceProvider.DeviceDefinitions.Clear();

        WizLightAddresses wizAddresses = new(
            _studyLampIp.Value ?? "",
            _lowerLampIp.Value ?? "",
            _upperLampIp.Value ?? "");
        foreach ((string name, string ip) in wizAddresses.Enumerate().DistinctBy(light => light.Ip))
            RgbDeviceProvider.DeviceDefinitions.Add(DirectDeviceDefinition.CreateWiz(name, ip));

        if (_enableLenovo.Value)
            RgbDeviceProvider.DeviceDefinitions.Add(DirectDeviceDefinition.CreateLenovoLegion5());

        if (_enableRazer.Value)
        {
            RgbDeviceProvider.DeviceDefinitions.Add(DirectDeviceDefinition.CreateRazerBlackWidowV3());
            RgbDeviceProvider.DeviceDefinitions.Add(DirectDeviceDefinition.CreateRazerDeathAdderV2ProWireless());
            RgbDeviceProvider.DeviceDefinitions.Add(DirectDeviceDefinition.CreateRazerMouseDockChroma());
        }

        _deviceService.AddDeviceProvider(this);

        if (_enableDirectAmbient.Value)
        {
            DirectWatchOptions watchOptions = new(
                _watchStudyEnabled.Value,
                _upperLightRole.Value ?? "Off",
                _lowerLightRole.Value ?? "Off",
                _watchRazerKeyboardEnabled.Value,
                _watchRazerMouseEnabled.Value,
                _watchRazerDockEnabled.Value,
                _watchLenovoKeyboardEnabled.Value,
                _blackoutOnBlack.Value,
                _ambientFps.Value,
                _watchStudyStrength.Value,
                _watchRearStrength.Value,
                _watchRazerStrength.Value,
                _watchColorBoost.Value);
            _ambientController = new DirectAmbientController(_directAmbientMode.Value ?? "StudyOnly", watchOptions, wizAddresses, _logger);
        }

        if (_enableDirectCs.Value)
        {
            CsEventTuning tuning = new(
                _csFlashIntensity.Value,
                _csFireIntensity.Value,
                _csSmokeIntensity.Value,
                _csDeathIntensity.Value,
                _csImpactIntensity.Value,
                _csBombIntensity.Value,
                _csClutchIntensity.Value,
                _csTeamContrast.Value,
                _csUtilityBrightness.Value);
            _csController = new DirectCsController(
                _upperGameRole.Value,
                _lowerGameRole.Value,
                _gameFps.Value,
                wizAddresses,
                tuning,
                _csNumpadLayout.Value);
        }

        if (_enableDirectValorant.Value)
            _valorantController = new DirectValorantController(_upperGameRole.Value, _lowerGameRole.Value, _gameFps.Value, wizAddresses);
    }

    public override void Disable()
    {
        DirectLightingRuntimeState.SetAmbientControlActive(false);
        _ambientController?.Dispose();
        _ambientController = null;
        _csController?.Dispose();
        _csController = null;
        _valorantController?.Dispose();
        _valorantController = null;
        _deviceService.RemoveDeviceProvider(this);
        RgbDeviceProvider.Exception -= ProviderOnException;
        RgbDeviceProvider.Dispose();
    }

    private void ProviderOnException(object? sender, ExceptionEventArgs args)
    {
        _logger.Debug(args.Exception, "Direct device exception: {message}", args.Exception.Message);
    }
}
