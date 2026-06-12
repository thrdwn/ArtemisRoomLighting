namespace Artemis.Plugins.DirectDevices;

internal static class LightingFrameRate
{
    public const int DefaultAmbientFps = 10;
    public const int DefaultGameFps = 10;
    public const int MinFps = 1;
    public const int MaxAmbientFps = 20;
    public const int MaxGameFps = 30;

    public static int ClampAmbient(int fps)
    {
        return Math.Clamp(fps, MinFps, MaxAmbientFps);
    }

    public static int ClampGame(int fps)
    {
        return Math.Clamp(fps, MinFps, MaxGameFps);
    }

    public static int ToDelayMilliseconds(int fps)
    {
        return Math.Max(1, (int)Math.Round(1000.0 / Math.Max(MinFps, fps)));
    }
}
