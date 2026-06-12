using HidSharp;
using RGB.NET.Core;

namespace Artemis.Plugins.DirectDevices;

public sealed class RazerUpdateQueue : UpdateQueue
{
    private readonly HidDevice _device;
    private readonly RazerMatrixOptions _options;
    private WindowsHidFeatureSender? _sender;
    private DateTime _nextErrorLogUtc;

    public RazerUpdateQueue(IDeviceUpdateTrigger updateTrigger, HidDevice device, RazerMatrixOptions options)
        : base(updateTrigger)
    {
        _device = device;
        _options = options;
    }

    protected override bool Update(ReadOnlySpan<(object key, Color color)> dataSet)
    {
        if (DirectLightingRuntimeState.IsExclusiveControlActive)
            return true;

        try
        {
            Color[,] colors = new Color[_options.Rows, _options.Columns];
            foreach ((object key, Color color) in dataSet)
            {
                if (key is not RazerLedData ledData)
                    continue;

                if (ledData.Row < 0 || ledData.Row >= _options.Rows || ledData.Column < 0 || ledData.Column >= _options.Columns)
                    continue;

                colors[ledData.Row, ledData.Column] = color;
            }

            WindowsHidFeatureSender sender = GetSender();
            for (int row = 0; row < _options.Rows; row++)
            {
                byte[] rgb = new byte[_options.Columns * 3];
                for (int col = 0; col < _options.Columns; col++)
                {
                    Color color = colors[row, col];
                    int offset = col * 3;
                    rgb[offset] = color.RedByte();
                    rgb[offset + 1] = color.GreenByte();
                    rgb[offset + 2] = color.BlueByte();
                }

                Thread.Sleep(1);
                sender.SetFeature(RazerReports.CreateCustomFrameExtended(_options.TransactionId, (byte)row, 0, (byte)(_options.Columns - 1), rgb));
            }

            Thread.Sleep(1);
            sender.SetFeature(RazerReports.CreateCustomModeExtended(_options.TransactionId));
            return true;
        }
        catch (Exception ex)
        {
            ResetSender();
            ReportException(ex);
            return false;
        }
    }

    public override void Dispose()
    {
        base.Dispose();
        ResetSender();
    }

    private WindowsHidFeatureSender GetSender()
    {
        return _sender ??= new WindowsHidFeatureSender(_device.DevicePath);
    }

    private void ResetSender()
    {
        _sender?.Dispose();
        _sender = null;
    }

    private void ReportException(Exception ex)
    {
        DateTime now = DateTime.UtcNow;
        if (now < _nextErrorLogUtc)
            return;

        _nextErrorLogUtc = now.AddSeconds(5);
        DirectRgbDeviceProvider.Instance.Throw(ex);
    }
}
