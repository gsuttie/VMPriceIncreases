using System.Text.RegularExpressions;

namespace VMPriceIncreases.Services;

/// <summary>
/// Turns an ARM VM size (e.g. Standard_D2s_v5) into the "series" name Microsoft uses
/// in its pricing notices (e.g. Dsv5) so it can be compared against the exclusion list.
/// </summary>
public static partial class SkuFamilyParser
{
    // Standard_D2s_v5    => family "D", size "2", suffix "s", version "5"
    // Standard_E4-2ds_v5 => family "E", size "4", constrained "-2", suffix "ds", version "5"
    // Standard_B2ms      => family "B", size "2", suffix "ms", no version
    // Standard_DS2_v2    => family "DS", size "2", no suffix, version "2"
    [GeneratedRegex(@"^(?<family>[A-Z]+)(?<size>\d+)(?:-\d+)?(?<suffix>[a-z]*)(?:_v(?<ver>\d+))?$", RegexOptions.CultureInvariant)]
    private static partial Regex SizeRegex();

    [GeneratedRegex("_Promo$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PromoSuffix();

    public static (string Series, bool Recognised) Parse(string vmSize)
    {
        if (string.IsNullOrWhiteSpace(vmSize))
            return ("", false);

        var s = vmSize.Trim();
        if (s.StartsWith("Standard_", StringComparison.OrdinalIgnoreCase))
            s = s["Standard_".Length..];
        else if (s.StartsWith("Basic_", StringComparison.OrdinalIgnoreCase))
            s = s["Basic_".Length..];

        // Promo sizes (e.g. Standard_DS2_v2_Promo) share the base series.
        s = PromoSuffix().Replace(s, "");

        var m = SizeRegex().Match(s);
        if (!m.Success)
            return (s, false);

        var family = m.Groups["family"].Value;
        var suffix = m.Groups["suffix"].Value;
        var ver = m.Groups["ver"].Value;

        // Families the notice names without the size-suffix convention.
        if (family.Equals("NP", StringComparison.Ordinal)) return ("NP", true);
        if (family.Equals("HC", StringComparison.Ordinal)) return ("HCS", true);

        // Older DS/GS families carry the premium-storage "s" as a capital letter in the family name.
        // Normalise so DS2_v2 -> Dsv2 and GS4 -> Gs.
        if (family.Length == 2 && family[1] == 'S')
        {
            family = family[..1];
            suffix = "s" + suffix;
        }

        // B-series v1 has no _v1 suffix, but the notice calls it Bv1; its ms/s suffixes are not part of the series name.
        if (family.Equals("B", StringComparison.Ordinal) && ver.Length == 0)
            return ("Bv1", true);

        // Suffix letters that are not part of the published series name: r (RDMA) and i (isolated).
        suffix = suffix.Replace("r", "").Replace("i", "");

        var series = family + suffix + (ver.Length == 0 ? "" : "v" + ver);
        return (series, true);
    }

    public static bool IsExcluded(string series, IEnumerable<string> excludedSeries) =>
        excludedSeries.Any(e => e.Equals(series, StringComparison.OrdinalIgnoreCase));
}
