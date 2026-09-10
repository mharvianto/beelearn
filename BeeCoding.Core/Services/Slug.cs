namespace BeeCoding.Services;

/// <summary>
/// Random, stable, opaque public identifiers for problems (like <c>Board.Slug</c>).
/// 8 characters from a 31-symbol alphabet (~40 bits) — no ambiguous glyphs, all
/// lowercase so URLs are case-insensitive in practice, and no information leak.
/// </summary>
public static class Slug
{
    private static readonly char[] Alphabet = "abcdefghijkmnpqrstuvwxyz23456789".ToCharArray();

    public static string New(int length = 8) =>
        string.Concat(Enumerable.Range(0, length)
            .Select(_ => Alphabet[Random.Shared.Next(Alphabet.Length)]));
}
