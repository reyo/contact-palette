using System.Globalization;
using System.Text;

namespace contact_palette_core;

public static class contact_search
{
    public static IReadOnlyList<contact_record> find(IEnumerable<contact_record> contacts, string? query, int limit = 60)
    {
        var normalized = normalize(query ?? string.Empty);
        if (normalized.Length == 0) return contacts.OrderBy(c => c.display_name, StringComparer.CurrentCultureIgnoreCase).Take(limit).ToArray();
        // A mixed name/email query containing digits must not match unrelated phones.
        var phone_query = (query ?? string.Empty).All(c => char.IsAsciiDigit(c) || char.IsWhiteSpace(c) || "+-()./".Contains(c));
        var digits = phone_query ? new string((query ?? string.Empty).Where(char.IsAsciiDigit).ToArray()) : string.Empty;
        return contacts.Select(c => (contact: c, score: score(c, normalized, digits)))
            .Where(x => x.score > 0)
            .OrderByDescending(x => x.score)
            .ThenBy(x => x.contact.display_name, StringComparer.CurrentCultureIgnoreCase)
            .Take(limit)
            .Select(x => x.contact)
            .ToArray();
    }

    private static int score(contact_record contact, string query, string digits)
    {
        var name = normalize(contact.display_name);
        var best = name == query ? 130 : name.StartsWith(query, StringComparison.Ordinal) ? 110 : name.Contains(query, StringComparison.Ordinal) ? 90 : 0;
        foreach (var email in contact.emails)
        {
            var normalized_email = normalize(email);
            if (normalized_email == query) best = Math.Max(best, 125);
            else if (normalized_email.StartsWith(query, StringComparison.Ordinal)) best = Math.Max(best, 105);
            else if (normalized_email.Contains(query, StringComparison.Ordinal)) best = Math.Max(best, 85);
        }
        foreach (var phone in contact.phones)
        {
            var phone_digits = new string(phone.Where(char.IsDigit).ToArray());
            if (digits.Length >= 3 && phone_digits == digits) best = Math.Max(best, 120);
            else if (digits.Length >= 3 && phone_digits.Contains(digits, StringComparison.Ordinal)) best = Math.Max(best, 80);
        }
        return best;
    }

    private static string normalize(string text)
    {
        var decomposed = text.Normalize(NormalizationForm.FormD);
        var output = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark) output.Append(char.ToLowerInvariant(character));
        }
        return output.ToString().Normalize(NormalizationForm.FormC).Trim();
    }
}
