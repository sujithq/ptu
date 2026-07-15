namespace Ptu.Cli.Availability;

public static class PaygDataZoneTabs
{
    public const string Default = Europe;
    public const string Americas = "az-americas";
    public const string Europe = "az-europe";
    public const string AsiaPacific = "az-apac";
    public const string MiddleEastAfrica = "az-mea";

    public static readonly IReadOnlyList<string> All = [Americas, Europe, AsiaPacific, MiddleEastAfrica];

    public static bool TryNormalize(string? value, out string tab)
    {
        tab = All.FirstOrDefault(candidate =>
            string.Equals(candidate, value?.Trim(), StringComparison.OrdinalIgnoreCase)) ?? string.Empty;
        return tab.Length > 0;
    }
}