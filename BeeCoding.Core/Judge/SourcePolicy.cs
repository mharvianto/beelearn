using System.Text.RegularExpressions;

namespace BeeCoding.Services.Judge;

/// <summary>Per-problem source restrictions: a banned-header denylist and a banned-symbol denylist.</summary>
public static partial class SourcePolicy
{
    [GeneratedRegex(@"#\s*include\s*[<""]\s*([^>""\s]+)\s*[>""]")]
    private static partial Regex IncludeRegex();

    [GeneratedRegex(@"/\*.*?\*/", RegexOptions.Singleline)]
    private static partial Regex BlockComment();
    [GeneratedRegex(@"//[^\n]*")]
    private static partial Regex LineComment();
    [GeneratedRegex(@"""(?:\\.|[^""\\])*""")]
    private static partial Regex StringLiteral();
    [GeneratedRegex(@"'(?:\\.|[^'\\])*'")]
    private static partial Regex CharLiteral();

    // Headers that pull in (almost) the whole standard library — blocked whenever the
    // problem restricts any header, since they'd defeat the restriction.
    private static readonly string[] Umbrella = { "bits/stdc++.h", "bits/extc++.h" };

    public static string Normalize(string? csv) =>
        string.IsNullOrWhiteSpace(csv) ? "" :
        string.Join(",", csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(h => h.Trim().Trim('<', '>', '"').ToLowerInvariant())
            .Where(h => h.Length > 0).Distinct());

    /// <summary>Symbol names keep their case and any :: qualification, e.g. "std::sort,qsort".</summary>
    public static string NormalizeSymbols(string? csv) =>
        string.IsNullOrWhiteSpace(csv) ? "" :
        string.Join(",", csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => s.Trim())
            .Where(s => s.Length > 0 && Regex.IsMatch(s, @"^[A-Za-z_][A-Za-z0-9_:]*$"))
            .Distinct());

    /// <summary>A human-readable message if the source breaks a restriction, else null.</summary>
    public static string? Violation(string? code, string? bannedHeadersCsv, string? bannedSymbolsCsv = null)
    {
        if (string.IsNullOrEmpty(code)) return null;

        var headers = bannedHeadersCsv is null ? new HashSet<string>()
            : Normalize(bannedHeadersCsv).Split(',', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
        if (headers.Count > 0)
        {
            foreach (Match m in IncludeRegex().Matches(code))
            {
                var h = m.Groups[1].Value.ToLowerInvariant();
                if (headers.Contains(h))
                    return $"This problem does not allow #include <{h}>. Implement it yourself.";
                if (Array.IndexOf(Umbrella, h) >= 0)
                    return $"This problem restricts some headers, so #include <{h}> (which pulls in everything) is not allowed — include only the headers you actually need.";
            }
        }

        var symbols = NormalizeSymbols(bannedSymbolsCsv).Split(',', StringSplitOptions.RemoveEmptyEntries);
        if (symbols.Length > 0)
        {
            var bare = StripNonCode(code);
            foreach (var s in symbols)
            {
                // identifier boundary (':' is allowed just before, so banning "sort" also
                // catches "std::sort"; banning "std::sort" catches only the qualified form)
                var pat = @"(?<![A-Za-z0-9_])" + Regex.Escape(s) + @"(?![A-Za-z0-9_])";
                if (Regex.IsMatch(bare, pat))
                    return $"This problem does not allow '{s}'. Implement it yourself.";
            }
        }

        return null;
    }

    private static string StripNonCode(string code)
    {
        code = BlockComment().Replace(code, " ");
        code = LineComment().Replace(code, " ");
        code = StringLiteral().Replace(code, "\"\"");
        code = CharLiteral().Replace(code, "''");
        return code;
    }
}
