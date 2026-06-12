using HidSharp;
using RGB.NET.Core;
using Serilog;

namespace Artemis.Plugins.DirectDevices;

public class DirectRgbDeviceProvider : AbstractRGBDeviceProvider
{
    private static DirectRgbDeviceProvider? _instance;

    public DirectRgbDeviceProvider()
    {
        if (_instance != null)
            throw new InvalidOperationException($"There can be only one instance of type {nameof(DirectRgbDeviceProvider)}");

        _instance = this;
    }

    public static DirectRgbDeviceProvider Instance => _instance ?? new DirectRgbDeviceProvider();

    public List<DirectDeviceDefinition> DeviceDefinitions { get; } = new();

    protected override void InitializeSDK()
    {
    }

    protected override IEnumerable<IRGBDevice> LoadDevices()
    {
        IDeviceUpdateTrigger updateTrigger = GetUpdateTrigger();

        foreach (DirectDeviceDefinition definition in DeviceDefinitions)
        {
            switch (definition.Kind)
            {
                case DirectDeviceKind.Wiz:
                    yield return DirectRgbDevice.Create(definition, new WizUpdateQueue(updateTrigger, definition.WizIp!));
                    break;
                case DirectDeviceKind.Lenovo4Zone:
                    foreach (HidDevice hidDevice in HidDeviceLocator.Find(definition.HidTargets))
                        yield return DirectRgbDevice.Create(definition with { Location = hidDevice.DevicePath }, new LenovoUpdateQueue(updateTrigger, hidDevice));
                    break;
                case DirectDeviceKind.RazerExtendedMatrix:
                    foreach (HidDevice hidDevice in HidDeviceLocator.Find(definition.HidTargets))
                        yield return DirectRgbDevice.Create(definition with { Location = hidDevice.DevicePath }, new RazerUpdateQueue(updateTrigger, hidDevice, definition.Razer!));
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(definition.Kind), definition.Kind, null);
            }
        }
    }

    protected override IDeviceUpdateTrigger CreateUpdateTrigger(int id, double updateRateHardLimit)
    {
        return new DeviceUpdateTrigger(updateRateHardLimit);
    }
}
