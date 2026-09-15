namespace Walos.Domain.Features;

public static class WalosFeatures
{
    public const string Dashboard = "dashboard";
    public const string Inventory = "inventory";
    public const string Restaurant = "restaurant";
    public const string Pos = "pos";
    public const string Cash = "cash";
    public const string Purchases = "purchases";
    public const string Suppliers = "suppliers";
    public const string Delivery = "delivery";
    public const string Finance = "finance";
    public const string Ai = "ai";

    private static readonly HashSet<string> Known = new(StringComparer.Ordinal)
    {
        Dashboard,
        Inventory,
        Restaurant,
        Pos,
        Cash,
        Purchases,
        Suppliers,
        Delivery,
        Finance,
        Ai
    };

    public static string Normalize(string? featureCode)
        => featureCode?.Trim().ToLowerInvariant() ?? string.Empty;

    public static bool IsKnown(string? featureCode)
        => Known.Contains(Normalize(featureCode));
}
