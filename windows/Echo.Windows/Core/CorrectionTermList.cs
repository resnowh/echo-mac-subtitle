using System.Globalization;

namespace Echo_Windows.Core;

public static class CorrectionTermList
{
    public static bool HasValidCandidateLength(string? candidate) =>
        !string.IsNullOrWhiteSpace(candidate) && StringInfo.ParseCombiningCharacters(candidate.Trim()).Length <= 80;

    public static bool TryAdd(string? current, string? candidate, out string updated)
    {
        string term = (candidate ?? "").Trim();
        var terms = (current ?? "").Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).ToList();
        if (term.Length == 0 || StringInfo.ParseCombiningCharacters(term).Length > 80
            || terms.Count >= 100 || terms.Contains(term, StringComparer.OrdinalIgnoreCase))
        {
            updated = current ?? "";
            return false;
        }

        terms.Add(term);
        updated = string.Join('\n', terms);
        return true;
    }
}
