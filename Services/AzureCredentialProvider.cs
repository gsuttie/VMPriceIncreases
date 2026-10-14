using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;

namespace VMPriceIncreases.Services;

/// <summary>Single ARM client backed by the Azure CLI (az login) session.</summary>
public sealed class AzureCredentialProvider
{
    public TokenCredential Credential { get; } = new AzureCliCredential();
    public ArmClient ArmClient { get; }

    public AzureCredentialProvider()
    {
        ArmClient = new ArmClient(Credential);
    }
}
