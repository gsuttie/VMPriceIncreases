using System.Text.Json.Serialization;

namespace VMPriceIncreases.Models;

public sealed class RetailPriceResponse
{
    [JsonPropertyName("Items")] public List<RetailPriceItem> Items { get; set; } = [];
    [JsonPropertyName("NextPageLink")] public string? NextPageLink { get; set; }
}

public sealed class RetailPriceItem
{
    [JsonPropertyName("currencyCode")] public string CurrencyCode { get; set; } = "";
    [JsonPropertyName("retailPrice")] public decimal RetailPrice { get; set; }
    [JsonPropertyName("unitPrice")] public decimal UnitPrice { get; set; }
    [JsonPropertyName("armRegionName")] public string ArmRegionName { get; set; } = "";
    [JsonPropertyName("armSkuName")] public string ArmSkuName { get; set; } = "";
    [JsonPropertyName("skuName")] public string SkuName { get; set; } = "";
    [JsonPropertyName("productName")] public string ProductName { get; set; } = "";
    [JsonPropertyName("meterName")] public string MeterName { get; set; } = "";
    [JsonPropertyName("unitOfMeasure")] public string UnitOfMeasure { get; set; } = "";
    [JsonPropertyName("type")] public string Type { get; set; } = "";
}
