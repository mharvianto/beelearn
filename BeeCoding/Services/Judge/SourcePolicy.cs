using System.Text.RegularExpressions;

namespace BeeCoding.Services.Judge;

/// <summary>Per-problem source restrictions — currently a banned-header denylist.</summary>
public static partial class SourcePolicy
{
    [GeneratedRegex(@"#\s*include\s*[<""]\s*([^>""\s]+)\s*[>""]")]
    private static partial Regex IncludeRegex();

    // Headers that pull in (almost) the whole standard library — blocked whenever the
    // problem restricts any header, since they'd defeat the restriction.
    private static readonly string[] Umbrella = { "bits/stdc++.h", "bits/extc++.h" };

    public static string Normalize(string? csv) =>
        string.IsNullOrWhiteSpace(csv) ? "" :
        string.Join(",", Split(csv));

    /// <summary>A human-readable message if the source #includes a disallowed header, else null.</summary>
    public static string? Violation(string? code, string? bannedCsv)
    {
        var banned = Split(bannedCsv).ToHashSet();
        if (banned.Count == 0 || string.IsNullOrEmpty(code)) return null;

        foreach (Match m in IncludeRegex().Matches(code))
        {
            var h = m.Groups[1].Value.ToLowerInvariant();
            if (banned.Contains(h))
                return $"This problem does not allow #include <{h}>. Implement it yourself.";
            if (Array.IndexOf(Umbrella, h) >= 0)
                return $"This problem restricts some headers, so #include <{h}> (which pulls in everything) is not allowed — include only the headers you actually need.";
        }
        return null;
    }

    private static IEnumerable<string> Split(string? csv) =>
        string.IsNullOrWhiteSpace(csv)
            ? Enumerable.Empty<string>()
            : csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                 .Select(h => h.Trim().Trim('<', '>', '"').ToLowerInvariant())
                 .Where(h => h.Length > 0)
                 .Distinct();
}
