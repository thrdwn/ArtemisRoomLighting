using HidSharp;
using RGB.NET.Core;

namespace Artemis.Plugins.DirectDevices;

public sealed class LenovoUpdateQueue : UpdateQueue
{
    private readonly HidDevice _device;
    private HidStream? _stream;

    public LenovoUpdateQueue(IDeviceUpdateTrigger updateTrigger, HidDevice device)
        : base(updateTrigger)
    {
        _device = device;
    }

    protected override bool Update(ReadOnlySpan<(object key, Color color)> dataSet)
    {
        if (DirectLightingRuntimeState.IsExclusiveControlActive)
            return true;

        try
        {
            byte[] report = new byte[33];
            report[0] = 0xCC;
            report[1] = 0x16;
            report[2] = 0x01;
            report[3] = 0x01;
            report[4] = 0x02;

            foreach ((object key, Color color) in dataSet)
            {
                if (key is not LenovoZoneData zoneData || zoneData.Zone is < 0 or > 3)
                    continue;

                int offset = 5 + (zoneData.Zone * 3);
                report[offset] = color.RedByte();
                report[offset + 1] = color.GreenByte();
                report[offset + 2] = color.BlueByte();
            }

            GetStream().SetFeature(report);
            return true;
        }
        catch (Exception ex)
        {
            ResetStream();
            DirectRgbDeviceProvider.Instance.Throw(ex);
            return false;
        }
    }

    public override void Dispose()
    {
        base.Dispose();
        ResetStream();
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
}
