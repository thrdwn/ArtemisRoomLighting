namespace Artemis.Plugins.DirectDevices;

internal static class DirectLightingRuntimeState
{
    private static readonly TimeSpan CsActiveHold = TimeSpan.FromSeconds(18);
    private static readonly TimeSpan ValorantActiveHold = TimeSpan.FromSeconds(2);
    private static readonly object Sync = new();
    private static DateTime _lastCsUpdateUtc = DateTime.MinValue;
    private static DateTime _lastValorantUpdateUtc = DateTime.MinValue;
    private static bool _ambientControlActive;

    public static bool IsExclusiveControlActive
    {
        get
        {
            lock (Sync)
                return _ambientControlActive ||
                       DateTime.UtcNow - _lastCsUpdateUtc < CsActiveHold ||
                       DateTime.UtcNow - _lastValorantUpdateUtc < ValorantActiveHold;
        }
    }

    public static bool IsCsActive
    {
        get
        {
            lock (Sync)
                return DateTime.UtcNow - _lastCsUpdateUtc < CsActiveHold;
        }
    }

    public static bool IsValorantActive
    {
        get
        {
            lock (Sync)
                return DateTime.UtcNow - _lastValorantUpdateUtc < ValorantActiveHold;
        }
    }

    public static bool IsGameActive
    {
        get
        {
            lock (Sync)
                return DateTime.UtcNow - _lastCsUpdateUtc < CsActiveHold ||
                       DateTime.UtcNow - _lastValorantUpdateUtc < ValorantActiveHold;
        }
    }

    public static void MarkCsActive()
    {
        lock (Sync)
            _lastCsUpdateUtc = DateTime.UtcNow;
    }

    public static void MarkValorantActive()
    {
        lock (Sync)
            _lastValorantUpdateUtc = DateTime.UtcNow;
    }

    public static void SetAmbientControlActive(bool active)
    {
        lock (Sync)
            _ambientControlActive = active;
    }
}
