using System.Net.Sockets;
using System.Text.Json;
using RGB.NET.Core;

namespace Artemis.Plugins.DirectDevices;

public sealed class WizUpdateQueue : UpdateQueue
{
    private readonly string _ip;
    private readonly UdpClient _udpClient;

    public WizUpdateQueue(IDeviceUpdateTrigger updateTrigger, string ip)
        : base(updateTrigger)
    {
        _ip = ip;
        _udpClient = new UdpClient();
        _udpClient.Connect(_ip, 38899);
    }

    protected override bool Update(ReadOnlySpan<(object key, Color color)> dataSet)
    {
        if (DirectLightingRuntimeState.IsExclusiveControlActive)
            return true;

        if (dataSet.Length == 0)
            return true;

        try
        {
            Color color = dataSet[0].color;
            byte r = color.RedByte();
            byte g = color.GreenByte();
            byte b = color.BlueByte();

            var command = new
            {
                method = "setPilot",
                @params = new
                {
                    r,
                    g,
                    b,
                    c = 0,
                    w = 0,
                    dimming = 100,
                    state = true
                }
            };

            byte[] payload = JsonSerializer.SerializeToUtf8Bytes(command);
            _udpClient.Send(payload, payload.Length);
            return true;
        }
        catch (Exception ex)
        {
            DirectRgbDeviceProvider.Instance.Throw(ex);
            return false;
        }
    }

    public override void Dispose()
    {
        base.Dispose();
        _udpClient.Dispose();
    }
}
