using Artemis.Plugins.Devices.Razer;
using RGB.NET.Core;

namespace Artemis.Plugins.DirectDevices;

public enum DirectDeviceKind
{
    Wiz,
    Lenovo4Zone,
    RazerExtendedMatrix
}

public sealed record HidTarget(int VendorId, int ProductId, int? InterfaceNumber, int? UsagePage, int? Usage, int? FeatureReportLength = null);

public sealed record RazerMatrixOptions(int Rows, int Columns, byte TransactionId, byte LedId);

public sealed record LedPlacement(LedId LedId, Point Point, Size Size, object CustomData, Shape? Shape = null);

public sealed record DirectDeviceDefinition(
    DirectDeviceKind Kind,
    string Name,
    string Manufacturer,
    string Model,
    RGBDeviceType DeviceType,
    IReadOnlyList<LedPlacement> Leds,
    IReadOnlyList<HidTarget> HidTargets,
    string? WizIp = null,
    string? Location = null,
    RazerMatrixOptions? Razer = null)
{
    public static DirectDeviceDefinition CreateWiz(string name, string ip)
    {
        return new DirectDeviceDefinition(
            DirectDeviceKind.Wiz,
            name,
            "Philips WiZ",
            "WiZ RGB light",
            RGBDeviceType.Unknown,
            new[] { new LedPlacement(LedId.Custom1, new Point(0, 0), new Size(60), new SingleLedData(), Shape.Circle) },
            Array.Empty<HidTarget>(),
            WizIp: ip,
            Location: $"IP: {ip}");
    }

    public static DirectDeviceDefinition CreateLenovoLegion5()
    {
        LedPlacement[] leds =
        {
            new(LedId.Custom1, new Point(0, 0), new Size(80, 24), new LenovoZoneData(0)),
            new(LedId.Custom2, new Point(82, 0), new Size(80, 24), new LenovoZoneData(1)),
            new(LedId.Custom3, new Point(164, 0), new Size(80, 24), new LenovoZoneData(2)),
            new(LedId.Custom4, new Point(246, 0), new Size(80, 24), new LenovoZoneData(3))
        };

        return new DirectDeviceDefinition(
            DirectDeviceKind.Lenovo4Zone,
            "Lenovo 5 2021",
            "Lenovo",
            "Legion 5 2021 4-zone keyboard",
            RGBDeviceType.Keyboard,
            leds,
            new[]
            {
                new HidTarget(0x048D, 0xC965, 0x00, null, null, 33),
                new HidTarget(0x048D, 0xC963, 0x00, null, null, 33)
            });
    }

    public static DirectDeviceDefinition CreateRazerBlackWidowV3()
    {
        List<LedPlacement> leds = new();

        foreach (LedId ledId in LedMappings.KeyboardBlackWidowV3.LedIds)
        {
            int sdkIndex = LedMappings.KeyboardBlackWidowV3[ledId];
            int sdkRow = sdkIndex / LedMappings.KEYBOARD_MAX_COLUMN;
            int sdkColumn = sdkIndex % LedMappings.KEYBOARD_MAX_COLUMN;

            int row = sdkRow - 1;
            int column = sdkColumn - 1;
            if (row < 0 || row >= 6 || column < 0 || column >= 22)
                continue;

            leds.Add(new LedPlacement(
                ledId,
                new Point(column * 19, row * 19),
                new Size(17, 17),
                new RazerLedData(row, column)));
        }

        return new DirectDeviceDefinition(
            DirectDeviceKind.RazerExtendedMatrix,
            "Razer Blackwidow V3",
            "Razer",
            "BlackWidow V3",
            RGBDeviceType.Keyboard,
            leds,
            new[] { new HidTarget(0x1532, 0x024E, 0x03, 0x0C, 0x01, 91) },
            Razer: new RazerMatrixOptions(6, 22, 0x3F, RazerConstants.LedIdBacklight));
    }

    public static DirectDeviceDefinition CreateRazerDeathAdderV2ProWireless()
    {
        return new DirectDeviceDefinition(
            DirectDeviceKind.RazerExtendedMatrix,
            "Razer DeathAdder V2 Pro (Wireless)",
            "Razer",
            "DeathAdder V2 Pro Wireless",
            RGBDeviceType.Mouse,
            new[] { new LedPlacement(LedId.Logo, new Point(0, 0), new Size(50), new RazerLedData(0, 0), Shape.Circle) },
            new[] { new HidTarget(0x1532, 0x007D, 0x00, 0x01, 0x02, 91) },
            Razer: new RazerMatrixOptions(1, 1, 0x3F, RazerConstants.LedIdBacklight));
    }

    public static DirectDeviceDefinition CreateRazerMouseDockChroma()
    {
        return new DirectDeviceDefinition(
            DirectDeviceKind.RazerExtendedMatrix,
            "Razer Mouse Dock Chroma",
            "Razer",
            "Mouse Dock Chroma",
            RGBDeviceType.Unknown,
            new[] { new LedPlacement(LedId.Custom1, new Point(0, 0), new Size(50), new RazerLedData(0, 0), Shape.Circle) },
            new[] { new HidTarget(0x1532, 0x007E, 0x00, 0x01, 0x02, 91) },
            Razer: new RazerMatrixOptions(1, 1, 0x3F, RazerConstants.LedIdBacklight));
    }
}

public sealed record SingleLedData;
public sealed record LenovoZoneData(int Zone);
public sealed record RazerLedData(int Row, int Column);
