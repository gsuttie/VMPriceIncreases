using System.Collections.Concurrent;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using VMPriceIncreases.Models;

namespace VMPriceIncreases.Services;

/// <summary>
/// Looks up pay-as-you-go hourly list prices from the public Azure Retail Prices API.
/// Results are cached per (currency, region, size, os) for the lifetime of the process.
/// </summary>
public sealed class RetailPriceService(
    HttpClient http,
    ILogger<RetailPriceService> logger)
{
    private readonly ConcurrentDictionary<string, Lazy<Task<decimal?>>> _cache = new(StringComparer.OrdinalIgnoreCase);

    public Task<decimal?> GetHourlyRateAsync(string currency, string region, string vmSize, string osType, CancellationToken ct = default)
    {
        var isWindows = osType.Equals("Windows", StringComparison.OrdinalIgnoreCase);
        var key = CacheKey(currency, region, vmSize, isWindows);
        var lazy = _cache.GetOrAdd(key, _ => new Lazy<Task<decimal?>>(() => FetchAsync(key, currency, region, vmSize, isWindows, ct)));
        return lazy.Value;
    }

    private static string CacheKey(string currency, string region, string vmSize, bool isWindows) =>
        $"{currency}|{region}|{vmSize}|{(isWindows ? "win" : "linux")}";

    private async Task<decimal?> FetchAsync(string key, string currency, string region, string vmSize, bool isWindows, CancellationToken ct)
    {
        var filter = $"serviceName eq 'Virtual Machines' and armRegionName eq '{region}' and armSkuName eq '{vmSize}' and priceType eq 'Consumption'";
        var url = $"api/retail/prices?api-version=2023-01-01-preview&currencyCode={Uri.EscapeDataString(currency)}&$filter={Uri.EscapeDataString(filter)}";

        var items = new List<RetailPriceItem>();
        string? next = url;
        var pages = 0;
        while (next is not null && pages++ < 10)
        {
            RetailPriceResponse? page;
            try
            {
                page = await http.GetFromJsonAsync<RetailPriceResponse>(next, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Retail price lookup failed for {Size} in {Region}", vmSize, region);
                _cache.TryRemove(key, out _); // allow a retry on the next refresh
                return null;
            }
            if (page is null) break;
            items.AddRange(page.Items);
            next = page.NextPageLink;
        }

        var candidates = items
            .Where(i => !i.SkuName.Contains("Spot", StringComparison.OrdinalIgnoreCase))
            .Where(i => !i.SkuName.Contains("Low Priority", StringComparison.OrdinalIgnoreCase))
            .Where(i => i.UnitOfMeasure.Contains("Hour", StringComparison.OrdinalIgnoreCase))
            .Where(i => i.ProductName.Contains("Windows", StringComparison.OrdinalIgnoreCase) == isWindows)
            .ToList();

        if (candidates.Count == 0)
        {
            logger.LogWarning("No retail price found for {Size} ({Os}) in {Region} [{Currency}]", vmSize, isWindows ? "Windows" : "Linux", region, currency);
            return null;
        }

        // Prefer the plain VM meter over Cloud Services meters; then the cheapest remaining match.
        var best = candidates
            .OrderBy(i => i.ProductName.Contains("Cloud Services", StringComparison.OrdinalIgnoreCase) ? 1 : 0)
            .ThenBy(i => i.RetailPrice)
            .First();

        return best.RetailPrice;
    }
}
