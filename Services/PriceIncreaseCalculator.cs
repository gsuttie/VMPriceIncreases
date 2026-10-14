using Microsoft.Extensions.Options;
using VMPriceIncreases.Models;

namespace VMPriceIncreases.Services;

public sealed class PriceIncreaseCalculator(
    RetailPriceService prices,
    IOptions<PriceIncreaseOptions> increaseOptions,
    IOptions<PricingOptions> pricingOptions)
{
    public PriceIncreaseOptions Increase => increaseOptions.Value;
    public PricingOptions Pricing => pricingOptions.Value;

    public async Task<IReadOnlyList<VmCostRow>> BuildRowsAsync(
        IReadOnlyList<VmRecord> vms,
        string currency,
        IProgress<int>? progress = null,
        CancellationToken ct = default)
    {
        var rows = new VmCostRow[vms.Count];
        var done = 0;
        using var throttle = new SemaphoreSlim(Math.Max(1, Pricing.MaxParallelLookups));

        var tasks = vms.Select(async (vm, i) =>
        {
            await throttle.WaitAsync(ct);
            try
            {
                var hourly = await prices.GetHourlyRateAsync(currency, vm.Region, vm.VmSize, vm.OsType, ct);
                rows[i] = Build(vm, hourly);
            }
            finally
            {
                throttle.Release();
                progress?.Report(Interlocked.Increment(ref done));
            }
        });

        await Task.WhenAll(tasks);
        return rows;
    }

    public VmCostRow Build(VmRecord vm, decimal? hourly)
    {
        var (series, recognised) = SkuFamilyParser.Parse(vm.VmSize);
        var inScope = Increase.RegionIncreasePercent.TryGetValue(vm.Region, out var pct);
        var excluded = inScope && recognised && SkuFamilyParser.IsExcluded(series, Increase.ExcludedSeries);

        IncreaseStatus status;
        string? note = null;
        decimal applied = 0;

        if (hourly is null)
        {
            status = IncreaseStatus.PriceUnknown;
            note = "No pay-as-you-go list price found for this size/region/OS.";
            applied = inScope && !excluded ? pct : 0;
        }
        else if (!inScope)
        {
            status = IncreaseStatus.NotInScope;
            note = "Region is not part of the announced increase.";
        }
        else if (excluded)
        {
            status = IncreaseStatus.Excluded;
            note = $"Series {series} is on the exclusion list.";
        }
        else
        {
            status = IncreaseStatus.Affected;
            applied = pct;
            if (!recognised)
                note = $"Could not parse a series from size '{vm.VmSize}'; treated as affected.";
        }

        var monthly = hourly is null ? (decimal?)null : hourly.Value * Pricing.HoursPerMonth;

        return new VmCostRow
        {
            Vm = vm,
            Series = series,
            SeriesRecognised = recognised,
            HourlyRate = hourly,
            MonthlyCost = monthly,
            IncreasePercent = applied,
            Status = status,
            Note = note
        };
    }
}
