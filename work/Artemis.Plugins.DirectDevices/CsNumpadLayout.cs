namespace Artemis.Plugins.DirectDevices;

public sealed class CsNumpadLayout
{
    public const string DefaultSerialized =
        "NumLock=SelectedGrenade;Divide=Armor;Multiply=Helmet;Minus=DefuseKit;" +
        "Num7=Flash;Num8=Smoke;Num9=HE;Plus=Bomb;" +
        "Num4=Fire;Num5=Decoy;Num6=AmmoState;" +
        "Num1=Health1;Num2=Health2;Num3=Health3;Enter=Ammo3;" +
        "Num0=Ammo1;Decimal=Ammo2";

    private static readonly HashSet<string> ValidKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "NumLock", "Divide", "Multiply", "Minus",
        "Num7", "Num8", "Num9", "Plus",
        "Num4", "Num5", "Num6",
        "Num1", "Num2", "Num3", "Enter",
        "Num0", "Decimal"
    };

    private static readonly HashSet<string> ValidAssignments = new(StringComparer.OrdinalIgnoreCase)
    {
        "Off", "Team", "SelectedGrenade",
        "Armor", "Helmet", "DefuseKit",
        "Flash", "Smoke", "HE", "Fire", "Decoy",
        "Health1", "Health2", "Health3",
        "Ammo1", "Ammo2", "Ammo3", "AmmoState", "ReserveAmmo",
        "Bomb", "RoundKills"
    };

    private readonly Dictionary<string, string> _assignments;

    private CsNumpadLayout(Dictionary<string, string> assignments)
    {
        _assignments = assignments;
    }

    public static CsNumpadLayout Parse(string? serialized)
    {
        Dictionary<string, string> assignments = ParseAssignments(DefaultSerialized);
        foreach ((string key, string assignment) in ParseAssignments(serialized))
            assignments[key] = assignment;
        return new CsNumpadLayout(assignments);
    }

    public string Get(string key)
    {
        return _assignments.TryGetValue(key, out string? assignment) ? assignment : "Off";
    }

    private static Dictionary<string, string> ParseAssignments(string? serialized)
    {
        Dictionary<string, string> assignments = new(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(serialized))
            return assignments;

        foreach (string part in serialized.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            int separator = part.IndexOf('=');
            if (separator <= 0 || separator >= part.Length - 1)
                continue;

            string key = part[..separator].Trim();
            string assignment = part[(separator + 1)..].Trim();
            if (ValidKeys.Contains(key) && ValidAssignments.Contains(assignment))
                assignments[key] = assignment;
        }

        return assignments;
    }
}
