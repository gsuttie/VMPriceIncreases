namespace VMPriceIncreases.Models;

public sealed class PriceIncreaseOptions
{
    public const string SectionName = "PriceIncrease";

    public string SourceUrl { get; set; } = "";
    public DateOnly EffectiveDate { get; set; }
    public Dictionary<string, decimal> RegionIncreasePercent { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<string> ExcludedSeries { get; set; } = [];
}

public sealed class PricingOptions
{
    public const string SectionName = "Pricing";

    public string DefaultCurrency { get; set; } = "USD";
    public List<string> Currencies { get; set; } = ["USD"];
    public decimal HoursPerMonth { get; set; } = 730;
    public int MaxParallelLookups { get; set; } = 8;
}
