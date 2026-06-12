using RGB.NET.Core;

namespace Artemis.Plugins.DirectDevices;

public sealed class DirectRgbDeviceInfo : IRGBDeviceInfo
{
    public DirectRgbDeviceInfo(DirectDeviceDefinition definition)
    {
        Definition = definition;
        DeviceType = definition.DeviceType;
        DeviceName = definition.Name;
        Manufacturer = definition.Manufacturer;
        Model = definition.Model;
    }

    public DirectDeviceDefinition Definition { get; }
    public RGBDeviceType DeviceType { get; }
    public string DeviceName { get; }
    public string Manufacturer { get; }
    public string Model { get; }
    public object? LayoutMetadata { get; set; }
}
