using CIEL.Reconciliation.Models;
using FuzzySharp;
using System.Text;
using System.Text.RegularExpressions;

namespace CIEL.Reconciliation.Services;

public sealed class SmartNameMatchResult
{
    public int Score { get; init; }
    public string Method { get; init; } = "No reliable name match";
    public string Explanation { get; init; } = "The names were not similar enough.";
}

/// <summary>
/// Local, privacy-friendly smart name matcher. It combines transliteration,
/// spelling aliases, token comparison, initials and a lightweight phonetic key.
/// No guest data is sent outside the application.
/// </summary>
public static class SmartNameMatcher
{
    private static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["mohamed"] = "muhammad", ["mohammed"] = "muhammad", ["mohammad"] = "muhammad",
        ["mohamad"] = "muhammad", ["muhamed"] = "muhammad", ["muhammed"] = "muhammad",
        ["ahmed"] = "ahmad", ["akhmed"] = "ahmad",
        ["abdulla"] = "abdullah", ["abdulah"] = "abdullah",
        ["abdelrahman"] = "abdulrahman", ["abdelrahim"] = "abdulrahim",
        ["yousef"] = "yusuf", ["youssef"] = "yusuf", ["yousif"] = "yusuf", ["josef"] = "yusuf",
        ["hussain"] = "hussein", ["husain"] = "hussein", ["hossein"] = "hussein",
        ["hasan"] = "hassan", ["ali"] = "aly",
        ["sergei"] = "sergey", ["sergiy"] = "sergey",
        ["vitaliy"] = "vitaly", ["vitally"] = "vitaly",
        ["alexandr"] = "alexander", ["aleksandr"] = "alexander", ["alexey"] = "alexei",
        ["nikolay"] = "nikolai", ["dmitriy"] = "dmitry", ["andrey"] = "andrei",
        ["nataliya"] = "natalia", ["tatiana"] = "tatyana",
        ["micheal"] = "michael", ["jon"] = "john", ["steven"] = "stephen"
    };

    public static SmartNameMatchResult Compare(BookingRecord booking, OperaRecord opera) =>
        Compare(booking.GuestName, opera.GuestName, booking.NormalizedName, opera.NormalizedName);

    public static SmartNameMatchResult Compare(
        string? bookingName,
        string? operaName,
        string? bookingNormalized = null,
        string? operaNormalized = null)
    {
        var left = string.IsNullOrWhiteSpace(bookingNormalized) ? NameTools.Normalize(bookingName) : bookingNormalized!;
        var right = string.IsNullOrWhiteSpace(operaNormalized) ? NameTools.Normalize(operaName) : operaNormalized!;

        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
            return new SmartNameMatchResult { Score = 0, Explanation = "One of the guest names is empty." };

        if (string.Equals(left, right, StringComparison.Ordinal))
            return new SmartNameMatchResult
            {
                Score = 100,
                Method = "Exact normalized name",
                Explanation = "The names are identical after transliteration, title removal and token reordering."
            };

        var aliasLeft = ApplyAliases(left);
        var aliasRight = ApplyAliases(right);
        if (string.Equals(aliasLeft, aliasRight, StringComparison.Ordinal))
            return new SmartNameMatchResult
            {
                Score = 99,
                Method = "Known spelling variation",
                Explanation = "The names match after applying common international spelling variations."
            };

        var tokenSort = Fuzz.TokenSortRatio(aliasLeft, aliasRight);
        var tokenSet = Fuzz.TokenSetRatio(aliasLeft, aliasRight);
        var fuzzy = Math.Max(tokenSort, tokenSet);

        var phoneticLeft = PhoneticNameKey(aliasLeft);
        var phoneticRight = PhoneticNameKey(aliasRight);
        var phoneticExact = phoneticLeft.Length > 0 && phoneticLeft == phoneticRight;

        var initialsCompatible = InitialsCompatible(aliasLeft, aliasRight);
        var sharedTokenRatio = SharedTokenRatio(aliasLeft, aliasRight);
        var score = fuzzy;
        var method = "Fuzzy token comparison";
        var reasons = new List<string> { $"Fuzzy similarity {fuzzy}%." };

        if (phoneticExact)
        {
            score = Math.Max(score, 94);
            method = "Phonetic and fuzzy comparison";
            reasons.Add("The phonetic forms are identical.");
        }

        if (initialsCompatible)
        {
            score = Math.Min(100, score + 3);
            reasons.Add("The first and last initials are compatible.");
        }

        if (sharedTokenRatio >= 0.75)
        {
            score = Math.Min(100, score + 3);
            reasons.Add("Most name tokens are shared.");
        }

        // A high token-set score can occur when a short common name is contained in a longer,
        // unrelated name. Cap it unless there is another supporting signal.
        if (!phoneticExact && !initialsCompatible && sharedTokenRatio < 0.5)
            score = Math.Min(score, 82);

        score = Math.Clamp(score, 0, 100);
        return new SmartNameMatchResult
        {
            Score = score,
            Method = method,
            Explanation = string.Join(" ", reasons)
        };
    }

    private static string ApplyAliases(string normalized)
    {
        var tokens = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(token => Aliases.TryGetValue(token, out var canonical) ? canonical : token)
            .OrderBy(token => token, StringComparer.Ordinal);
        return string.Join(' ', tokens);
    }

    private static bool InitialsCompatible(string left, string right)
    {
        var a = left.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var b = right.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (a.Length == 0 || b.Length == 0) return false;

        var aInitials = a.Select(x => x[0]).OrderBy(x => x).ToArray();
        var bInitials = b.Select(x => x[0]).OrderBy(x => x).ToArray();
        var common = aInitials.Intersect(bInitials).Count();
        return common >= Math.Min(2, Math.Min(aInitials.Length, bInitials.Length));
    }

    private static double SharedTokenRatio(string left, string right)
    {
        var a = left.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal);
        var b = right.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal);
        if (a.Count == 0 || b.Count == 0) return 0;
        return (double)a.Intersect(b).Count() / Math.Min(a.Count, b.Count);
    }

    private static string PhoneticNameKey(string normalized)
    {
        var keys = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(PhoneticToken)
            .Where(x => x.Length > 0)
            .OrderBy(x => x, StringComparer.Ordinal);
        return string.Join(' ', keys);
    }

    private static string PhoneticToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token)) return "";
        var value = token.ToLowerInvariant();
        value = Regex.Replace(value, "^(al|el)", "");
        value = value.Replace("ph", "f", StringComparison.Ordinal)
                     .Replace("kh", "h", StringComparison.Ordinal)
                     .Replace("gh", "g", StringComparison.Ordinal)
                     .Replace("sh", "s", StringComparison.Ordinal)
                     .Replace("ch", "s", StringComparison.Ordinal)
                     .Replace("th", "t", StringComparison.Ordinal)
                     .Replace("dh", "d", StringComparison.Ordinal)
                     .Replace("ck", "k", StringComparison.Ordinal)
                     .Replace("q", "k", StringComparison.Ordinal)
                     .Replace("c", "k", StringComparison.Ordinal)
                     .Replace("v", "f", StringComparison.Ordinal)
                     .Replace("w", "v", StringComparison.Ordinal)
                     .Replace("y", "i", StringComparison.Ordinal);

        var result = new StringBuilder();
        for (var i = 0; i < value.Length; i++)
        {
            var ch = value[i];
            if (i > 0 && "aeiou".Contains(ch)) continue;
            if (result.Length == 0 || result[^1] != ch) result.Append(ch);
        }
        return result.ToString();
    }
}
