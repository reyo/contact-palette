using System.Text;

namespace contact_palette_core;

public sealed record vcard_fields(string? full_name, IReadOnlyList<string> emails, IReadOnlyList<string> phones)
{
    public IReadOnlyList<contact_detail> details { get; init; } = [];
    public string? photo_value { get; init; }
    public bool photo_is_binary { get; init; }
}

public static class vcard_parser
{
    public static vcard_fields parse(string? raw_card)
    {
        if (string.IsNullOrWhiteSpace(raw_card)) return new vcard_fields(null, [], []);
        var email_values = new List<string>();
        var phone_values = new List<string>();
        string? full_name = null;
        string? structured_name = null;
        var details = new List<contact_detail>();
        string? photo_value = null;
        var photo_is_binary = false;

        foreach (var line in unfold(raw_card))
        {
            var colon = value_separator(line);
            if (colon < 1) continue;
            var field = line[..colon].Split(';', 2)[0].Split('.').Last().ToUpperInvariant();
            var raw_value = line[(colon + 1)..].Trim();
            var value = unescape(raw_value);
            if (string.IsNullOrWhiteSpace(value)) continue;

            switch (field)
            {
                case "FN":
                    full_name ??= value;
                    break;
                case "N":
                    var names = split_name(line[(colon + 1)..]);
                    structured_name ??= string.Join(" ", new[] { names.ElementAtOrDefault(3), names.ElementAtOrDefault(1), names.ElementAtOrDefault(2), names.ElementAtOrDefault(0), names.ElementAtOrDefault(4) }.Where(x => !string.IsNullOrWhiteSpace(x)));
                    break;
                case "EMAIL":
                    add_unique(email_values, strip_scheme(value, "mailto:"));
                    break;
                case "TEL":
                    add_unique(phone_values, strip_scheme(value, "tel:"));
                    break;
                case "PHOTO":
                    if (photo_value is null)
                    {
                        // Some Thunderbird/CardDAV cards escape the data URI comma as \,.
                        photo_value = value;
                        photo_is_binary = line[..colon].Split(';').Any(p => p.Equals("ENCODING=b", StringComparison.OrdinalIgnoreCase) || p.Equals("ENCODING=BASE64", StringComparison.OrdinalIgnoreCase));
                    }
                    break;
                case "ORG":
                    details.Add(new contact_detail("Organization", string.Join(" · ", split_name(raw_value).Where(p => !string.IsNullOrWhiteSpace(p)))));
                    break;
                case "ADR":
                    details.Add(new contact_detail("Address", string.Join(", ", split_name(raw_value).Where(p => !string.IsNullOrWhiteSpace(p)))));
                    break;
                case "TITLE": details.Add(new contact_detail("Job title", value)); break;
                case "ROLE": details.Add(new contact_detail("Role", value)); break;
                case "NICKNAME": details.Add(new contact_detail("Nickname", value)); break;
                case "URL": details.Add(new contact_detail("Website", value)); break;
                case "BDAY": details.Add(new contact_detail("Birthday", value)); break;
                case "NOTE": details.Add(new contact_detail("Notes", value)); break;
            }
        }
        return new vcard_fields(full_name ?? structured_name, email_values, phone_values)
        {
            details = details.Where(d => !string.IsNullOrWhiteSpace(d.value)).Distinct().ToArray(),
            photo_value = photo_value,
            photo_is_binary = photo_is_binary,
        };
    }

    private static int value_separator(string line)
    {
        var quoted = false;
        for (var index = 0; index < line.Length; index++)
        {
            if (line[index] == '"') quoted = !quoted;
            else if (line[index] == ':' && !quoted) return index;
        }
        return -1;
    }

    private static IEnumerable<string> unfold(string input)
    {
        var current = new StringBuilder();
        using var reader = new StringReader(input);
        while (reader.ReadLine() is { } line)
        {
            if (line.Length > 0 && (line[0] == ' ' || line[0] == '\t'))
            {
                current.Append(line.AsSpan(1));
                continue;
            }
            if (current.Length > 0) yield return current.ToString();
            current.Clear();
            current.Append(line);
        }
        if (current.Length > 0) yield return current.ToString();
    }

    private static string[] split_name(string value)
    {
        var parts = new List<string>();
        var current = new StringBuilder();
        var escaping = false;
        foreach (var character in value)
        {
            if (!escaping && character == '\\') { escaping = true; continue; }
            if (!escaping && character == ';') { parts.Add(current.ToString()); current.Clear(); continue; }
            current.Append(escaping && character is 'n' or 'N' ? ' ' : character);
            escaping = false;
        }
        parts.Add(current.ToString());
        return parts.ToArray();
    }

    private static string unescape(string value)
    {
        var output = new StringBuilder(value.Length);
        for (var index = 0; index < value.Length; index++)
        {
            if (value[index] != '\\' || index == value.Length - 1) { output.Append(value[index]); continue; }
            var next = value[++index];
            output.Append(next is 'n' or 'N' ? '\n' : next);
        }
        return output.ToString();
    }

    private static string strip_scheme(string value, string prefix) => value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? value[prefix.Length..] : value;

    private static void add_unique(List<string> values, string value)
    {
        if (!string.IsNullOrWhiteSpace(value) && !values.Contains(value, StringComparer.OrdinalIgnoreCase)) values.Add(value);
    }
}
