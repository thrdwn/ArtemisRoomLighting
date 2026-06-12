namespace Artemis.Plugins.DirectDevices;

internal enum GameRearLightRole
{
    Off,
    ObjectiveAlerts,
    HealthDamage,
    TeamAgentMood,
    MapMood,
    FullGameMix
}

internal static class GameRearLightRoles
{
    public static GameRearLightRole Parse(string? value)
    {
        string normalized = (value ?? string.Empty).Trim().Trim('"');
        return normalized.ToLowerInvariant() switch
        {
            "objectivealerts" => GameRearLightRole.ObjectiveAlerts,
            "healthdamage" => GameRearLightRole.HealthDamage,
            "teamagentmood" => GameRearLightRole.TeamAgentMood,
            "mapmood" => GameRearLightRole.MapMood,
            "fullgamemix" => GameRearLightRole.FullGameMix,
            _ => GameRearLightRole.Off
        };
    }

    public static byte BaseDimming(this GameRearLightRole role, bool upper)
    {
        return role switch
        {
            GameRearLightRole.ObjectiveAlerts => 0,
            GameRearLightRole.HealthDamage => upper ? (byte)13 : (byte)10,
            GameRearLightRole.TeamAgentMood => upper ? (byte)15 : (byte)11,
            GameRearLightRole.MapMood => upper ? (byte)14 : (byte)10,
            GameRearLightRole.FullGameMix => upper ? (byte)17 : (byte)13,
            _ => 0
        };
    }

    public static byte EventDimming(this GameRearLightRole role, bool upper)
    {
        return role switch
        {
            GameRearLightRole.ObjectiveAlerts => upper ? (byte)22 : (byte)16,
            GameRearLightRole.HealthDamage => upper ? (byte)20 : (byte)15,
            GameRearLightRole.TeamAgentMood => upper ? (byte)18 : (byte)13,
            GameRearLightRole.MapMood => upper ? (byte)18 : (byte)13,
            GameRearLightRole.FullGameMix => upper ? (byte)25 : (byte)18,
            _ => 0
        };
    }
}
