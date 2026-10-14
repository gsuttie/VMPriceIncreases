namespace VMPriceIncreases.Models;

public enum IncreaseStatus
{
    /// <summary>Region is in the price-increase list and the series is not excluded.</summary>
    Affected,
    /// <summary>Region is in the list but the VM series is explicitly excluded.</summary>
    Excluded,
    /// <summary>Region is not part of the announced increase.</summary>
    NotInScope,
    /// <summary>No retail price could be found for this size/region/OS.</summary>
    PriceUnknown
}

public sealed class VmCostRow
{
    public required VmRecord Vm { get; init; }
    public required string Series { get; init; }
    public bool SeriesRecognised { get; init; } = true;
    public decimal? HourlyRate { get; init; }
    public decimal? MonthlyCost { get; init; }
    public decimal IncreasePercent { get; init; }
    public IncreaseStatus Status { get; init; }
    public string? Note { get; init; }

    public decimal? ProjectedMonthly => MonthlyCost is null ? null : MonthlyCost * (1 + IncreasePercent / 100m);
    public decimal? MonthlyDelta => MonthlyCost is null ? null : ProjectedMonthly - MonthlyCost;
}
