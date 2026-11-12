using Microsoft.Data.Sqlite;

namespace contact_palette_core;

public static class contact_repository
{
    public static load_result load(string? root_dir = null)
    {
        var contacts = new List<contact_record>();
        var issues = new List<string>();
        IReadOnlyList<address_book_source> sources;
        try { sources = profile_discovery.discover(root_dir); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return new load_result([], [$"Could not discover Thunderbird profiles: {exception.Message}"]);
        }
        foreach (var source in sources)
        {
            try { contacts.AddRange(read_book(source)); }
            catch (Exception exception) when (exception is SqliteException or IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                issues.Add($"Cannot read {source.book_name} ({source.profile_name}): {exception.Message}");
            }
        }
        if (sources.Count == 0) issues.Add("No Thunderbird SQLite address books were found. Check profiles.ini and prefs.js.");
        return new load_result(contacts, issues);
    }

    private static IReadOnlyList<contact_record> read_book(address_book_source source)
    {
        var connection_string = new SqliteConnectionStringBuilder { DataSource = source.file_path, Mode = SqliteOpenMode.ReadOnly, Cache = SqliteCacheMode.Private, Pooling = false, DefaultTimeout = 1 }.ToString();
        using var connection = new SqliteConnection(connection_string);
        connection.Open();
        using (var guard = connection.CreateCommand())
        {
            guard.CommandText = "PRAGMA query_only = ON";
            guard.ExecuteNonQuery();
        }
        using (var check = connection.CreateCommand())
        {
            check.CommandText = "SELECT EXISTS(SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = 'properties')";
            if (Convert.ToInt32(check.ExecuteScalar()) != 1) throw new InvalidOperationException("Unsupported address book schema: properties table is missing.");
        }
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT card, name, value FROM properties ORDER BY card";
        using var reader = command.ExecuteReader();
        var contacts = new List<contact_record>();
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string? current_uid = null;

        void finish_contact()
        {
            if (current_uid == null) return;
            var vcard = vcard_parser.parse(values.GetValueOrDefault("_vCard"));
            var emails = vcard.emails.ToList();
            var phones = vcard.phones.ToList();
            add_unique(emails, values.GetValueOrDefault("PrimaryEmail"));
            add_unique(emails, values.GetValueOrDefault("SecondEmail"));
            foreach (var field in new[] { "CellularNumber", "WorkPhone", "HomePhone", "FaxNumber", "PagerNumber" }) add_unique(phones, values.GetValueOrDefault(field));
            var display_name = vcard.full_name ?? values.GetValueOrDefault("DisplayName");
            if (string.IsNullOrWhiteSpace(display_name)) display_name = string.Join(" ", new[] { values.GetValueOrDefault("FirstName"), values.GetValueOrDefault("LastName") }.Where(x => !string.IsNullOrWhiteSpace(x)));
            if (string.IsNullOrWhiteSpace(display_name)) display_name = emails.FirstOrDefault() ?? phones.FirstOrDefault() ?? "Unnamed contact";
            if (display_name != "Unnamed contact" || emails.Count > 0 || phones.Count > 0)
            {
                var details = vcard.details.ToList();
                foreach (var (field, label) in new[] { ("Company", "Organization"), ("Department", "Department"), ("JobTitle", "Job title"), ("NickName", "Nickname"), ("WebPage1", "Website"), ("WebPage2", "Website"), ("Notes", "Notes") })
                {
                    var value = values.GetValueOrDefault(field);
                    if (!string.IsNullOrWhiteSpace(value) && !details.Any(d => d.label == label && d.value == value)) details.Add(new contact_detail(label, value));
                }
                contacts.Add(new contact_record(current_uid, display_name, source.book_name, source.profile_name, emails, phones, source.file_path)
                {
                    details = details,
                    photo = contact_photo.load(vcard, source.file_path, values.GetValueOrDefault("PhotoName")),
                });
            }
        }

        while (reader.Read())
        {
            var uid = reader.IsDBNull(0) ? string.Empty : reader.GetString(0);
            if (uid.Length == 0) continue;
            if (!string.Equals(uid, current_uid, StringComparison.Ordinal))
            {
                finish_contact();
                values.Clear();
                current_uid = uid;
            }
            if (!reader.IsDBNull(1) && !reader.IsDBNull(2)) values[reader.GetString(1)] = reader.GetString(2);
        }
        finish_contact();
        return contacts;
    }

    private static void add_unique(List<string> values, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value) && !values.Contains(value, StringComparer.OrdinalIgnoreCase)) values.Add(value);
    }
}
