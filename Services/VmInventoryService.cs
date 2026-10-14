using System.Text.Json;
using Azure.ResourceManager.ResourceGraph;
using Azure.ResourceManager.ResourceGraph.Models;
using Azure.ResourceManager.Resources;
using VMPriceIncreases.Models;

namespace VMPriceIncreases.Services;

public sealed record SubscriptionInfo(string Id, string Name);

public sealed class VmInventoryService(AzureCredentialProvider azure, ILogger<VmInventoryService> logger)
{
    private const string Query = """
        Resources
        | where type =~ 'microsoft.compute/virtualmachines'
        | project subscriptionId, resourceGroup, name, location,
                  vmSize = tostring(properties.hardwareProfile.vmSize),
                  osType = tostring(properties.storageProfile.osDisk.osType),
                  powerState = tostring(properties.extended.instanceView.powerState.code)
        | order by subscriptionId asc, resourceGroup asc, name asc
        """;

    public async Task<IReadOnlyList<SubscriptionInfo>> GetSubscriptionsAsync(CancellationToken ct = default)
    {
        var list = new List<SubscriptionInfo>();
        await foreach (SubscriptionResource sub in azure.ArmClient.GetSubscriptions().GetAllAsync(ct))
        {
            list.Add(new SubscriptionInfo(sub.Data.SubscriptionId, sub.Data.DisplayName ?? sub.Data.SubscriptionId));
        }
        return list.OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public async Task<IReadOnlyList<VmRecord>> GetVirtualMachinesAsync(
        IReadOnlyList<SubscriptionInfo> subscriptions,
        CancellationToken ct = default)
    {
        if (subscriptions.Count == 0) return [];

        var subNames = subscriptions.ToDictionary(s => s.Id, s => s.Name, StringComparer.OrdinalIgnoreCase);
        var tenant = await GetTenantAsync(ct);
        var results = new List<VmRecord>();

        string? skipToken = null;
        do
        {
            var content = new ResourceQueryContent(Query)
            {
                Options = new ResourceQueryRequestOptions
                {
                    Top = 1000,
                    ResultFormat = ResultFormat.ObjectArray,
                    SkipToken = skipToken
                }
            };
            foreach (var s in subscriptions) content.Subscriptions.Add(s.Id);

            var response = await tenant.GetResourcesAsync(content, ct);
            var page = response.Value;

            using var doc = JsonDocument.Parse(page.Data.ToMemory());
            foreach (var row in doc.RootElement.EnumerateArray())
            {
                var subId = Get(row, "subscriptionId");
                results.Add(new VmRecord(
                    SubscriptionId: subId,
                    SubscriptionName: subNames.GetValueOrDefault(subId, subId),
                    ResourceGroup: Get(row, "resourceGroup"),
                    Name: Get(row, "name"),
                    Region: Get(row, "location"),
                    VmSize: Get(row, "vmSize"),
                    OsType: Get(row, "osType"),
                    PowerState: Get(row, "powerState")));
            }

            skipToken = page.SkipToken;
        } while (!string.IsNullOrEmpty(skipToken));

        logger.LogInformation("Resource Graph returned {Count} virtual machines across {Subs} subscriptions", results.Count, subscriptions.Count);
        return results;
    }

    private async Task<TenantResource> GetTenantAsync(CancellationToken ct)
    {
        await foreach (var tenant in azure.ArmClient.GetTenants().GetAllAsync(ct))
            return tenant;
        throw new InvalidOperationException("No Azure tenant is visible to the current credential.");
    }

    private static string Get(JsonElement row, string name) =>
        row.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
}
