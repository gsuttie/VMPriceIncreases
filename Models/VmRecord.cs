namespace VMPriceIncreases.Models;

public sealed record VmRecord(
    string SubscriptionId,
    string SubscriptionName,
    string ResourceGroup,
    string Name,
    string Region,
    string VmSize,
    string OsType,
    string PowerState)
{
    public bool IsDeallocated =>
        PowerState.Contains("deallocated", StringComparison.OrdinalIgnoreCase) ||
        PowerState.Contains("stopped", StringComparison.OrdinalIgnoreCase);

    public string PowerStateDisplay =>
        string.IsNullOrEmpty(PowerState) ? "unknown"
        : PowerState.StartsWith("PowerState/", StringComparison.OrdinalIgnoreCase) ? PowerState["PowerState/".Length..]
        : PowerState;
}
