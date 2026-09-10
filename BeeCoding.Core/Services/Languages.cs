namespace BeeCoding.Services;

/// <summary>
/// Per-problem "allowed languages" list. Stored as a comma-separated string of
/// <see cref="Supported"/> ids; an empty list means every supported language is allowed.
/// </summary>
public static class Languages
{
    public static readonly string[] Supported = { "c", "cpp" };

    /// <summary>Canonicalize free-form input to a comma-separated allow-list (may be empty).</summary>
    public static string Normalize(string? csv) =>
        string.Join(',', (csv ?? "")
            .Split(new[] { ',', ' ', ';', '/' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => s.ToLowerInvariant())
            .Select(s => s is "c++" or "cxx" or "cc" ? "cpp" : s)
            .Where(Supported.Contains)
            .Distinct());

    private static string[] Set(string? csv)
    {
        var n = Normalize(csv);
        return n.Length == 0 ? Array.Empty<string>() : n.Split(',');
    }

    /// <summary>Does this allow-list permit <paramref name="lang"/>? An empty list permits anything.</summary>
    public static bool Allows(string? allowedCsv, string? lang)
    {
        var set = Set(allowedCsv);
        return set.Length == 0 || (lang is not null && set.Contains(lang));
    }

    /// <summary>Language the judge should compile with when the caller didn't choose one.</summary>
    public static string Default(string? allowedCsv)
    {
        var set = Set(allowedCsv);
        return set.Length == 0 ? "cpp" : set[0];
    }

    /// <summary>Human label for UI / error messages ("Any", "C", "C++", "C/C++").</summary>
    public static string Label(string? allowedCsv)
    {
        var set = Set(allowedCsv);
        return set.Length == 0 ? "Any" : string.Join("/", set.Select(s => s == "c" ? "C" : "C++"));
    }
}
