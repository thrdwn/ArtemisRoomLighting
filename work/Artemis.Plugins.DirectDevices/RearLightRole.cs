namespace Artemis.Plugins.DirectDevices;

internal enum RearLightRole
{
    Off,
    SoftDepth,
    PositionalAmbient,
    GameEventsOnly
}

internal static class RearLightRoles
{
    public static RearLightRole Parse(string? value)
    {
        string normalized = (value ?? string.Empty).Trim().Trim('"');
        return normalized.ToLowerInvariant() switch
        {
            "softdepth" => RearLightRole.SoftDepth,
            "positionalambient" => RearLightRole.PositionalAmbient,
            "gameeventsonly" => RearLightRole.GameEventsOnly,
            _ => RearLightRole.Off
        };
    }

    public static bool UsesAmbientSampling(this RearLightRole role)
    {
        return role is RearLightRole.SoftDepth or RearLightRole.PositionalAmbient;
    }

    public static byte AmbientMaximum(this RearLightRole role, bool upper)
    {
        return role switch
        {
            RearLightRole.SoftDepth => upper ? (byte)24 : (byte)18,
            RearLightRole.PositionalAmbient => upper ? (byte)56 : (byte)46,
            _ => 0
        };
    }

    public static byte GameBaseDimming(this RearLightRole role, bool upper)
    {
        return role switch
        {
            RearLightRole.SoftDepth => upper ? (byte)15 : (byte)10,
            RearLightRole.PositionalAmbient => upper ? (byte)25 : (byte)18,
            _ => 0
        };
    }

    public static byte GameEventDimming(this RearLightRole role, bool upper)
    {
        return role switch
        {
            RearLightRole.GameEventsOnly => upper ? (byte)18 : (byte)13,
            RearLightRole.SoftDepth => upper ? (byte)20 : (byte)14,
            RearLightRole.PositionalAmbient => upper ? (byte)30 : (byte)22,
            _ => 0
        };
    }
}
