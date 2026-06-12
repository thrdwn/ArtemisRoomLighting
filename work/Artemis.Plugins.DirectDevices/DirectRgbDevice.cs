using RGB.NET.Core;

namespace Artemis.Plugins.DirectDevices;

public sealed class DirectRgbDevice : AbstractRGBDevice<DirectRgbDeviceInfo>
{
    private DirectRgbDevice(DirectRgbDeviceInfo deviceInfo, UpdateQueue updateQueue)
        : base(deviceInfo, updateQueue)
    {
        InitializeLayout();
    }

    public static DirectRgbDevice Create(DirectDeviceDefinition definition, UpdateQueue updateQueue)
    {
        return new DirectRgbDevice(new DirectRgbDeviceInfo(definition), updateQueue);
    }

    private void InitializeLayout()
    {
        foreach (LedPlacement placement in DeviceInfo.Definition.Leds)
        {
            Led? led = AddLed(placement.LedId, placement.Point, placement.Size, placement.CustomData);
            if (led != null && placement.Shape.HasValue)
                led.Shape = placement.Shape.Value;
        }
    }
}
