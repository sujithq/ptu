namespace Ptu.Cli.Availability;

/// <summary>Provisioned-throughput deployment types exposed by the availability API.</summary>
public enum PtuType
{
    DataZone,
    Regional,
    Global,
}

public static class PtuTypes
{
    public static bool TryParse(string value, out PtuType type)
    {
        switch (value.Trim().Replace("-", "", StringComparison.Ordinal).ToLowerInvariant())
        {
            case "datazone":
                type = PtuType.DataZone;
                return true;
            case "regional":
                type = PtuType.Regional;
                return true;
            case "global":
                type = PtuType.Global;
                return true;
            default:
                type = default;
                return false;
        }
    }

    public static string DisplayName(PtuType type) => type switch
    {
        PtuType.DataZone => "Data Zone",
        PtuType.Regional => "Regional",
        PtuType.Global => "Global",
        _ => type.ToString(),
    };
}
