using RGB.NET.Core;

namespace Artemis.Plugins.DirectDevices;

public static class ColorExtensions
{
    public static byte RedByte(this Color color) => ToByte(color.R);
    public static byte GreenByte(this Color color) => ToByte(color.G);
    public static byte BlueByte(this Color color) => ToByte(color.B);

    private static byte ToByte(float value)
    {
        if (value <= 0)
            return 0;

        if (value <= 1)
            return (byte)MathF.Round(value * 255);

        if (value >= 255)
            return 255;

        return (byte)MathF.Round(value);
    }
}
