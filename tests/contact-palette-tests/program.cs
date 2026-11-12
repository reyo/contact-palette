using Microsoft.Data.Sqlite;
using contact_palette_core;
using System.Security.Cryptography;
using System.Diagnostics;

var assertion_count = 0;
if (args.Contains("--local-smoke"))
{
    var local = contact_repository.load();
    Console.WriteLine($"Local read-only smoke: {local.contacts.Count} contacts, {local.contacts.Count(c => c.photo is not null)} local photos, {local.issues.Count} notices. No contact values or paths are printed.");
    return local.issues.Count == 0 && local.contacts.Count > 0 ? 0 : 1;
}

var root_dir = Path.Combine(Path.GetTempPath(), "contact-palette-tests-" + Guid.NewGuid().ToString("N"));
var profile_dir = Path.Combine(root_dir, "Profiles", "sample.default");
Directory.CreateDirectory(profile_dir);

try
{
    File.WriteAllText(Path.Combine(root_dir, "profiles.ini"), "[Profile0]\nName=Default\nIsRelative=1\nPath=Profiles/sample.default\n\n[InstallABC]\nDefault=Profiles/sample.default\n");
    File.WriteAllText(Path.Combine(profile_dir, "prefs.js"), "user_pref(\"ldap_2.servers.carddav.filename\", \"carddav.sqlite\");\nuser_pref(\"ldap_2.servers.carddav.description\", \"Cloud Contacts\");\n");

    create_book(Path.Combine(profile_dir, "abook.sqlite"), [
        ("123", "_vCard", "BEGIN:VCARD\nVERSION:4.0\nFN:Élodie Test\nEMAIL;TYPE=work:elodie@example.eu\nEMAIL;TYPE=home:e.test@example.eu\nTEL;VALUE=uri:tel:+37255501234\nEND:VCARD"),
        ("123", "PrimaryEmail", "elodie@example.eu"),
        ("456", "DisplayName", "Kaspar Sample"),
        ("456", "PrimaryEmail", "kaspar@example.eu"),
        ("456", "CellularNumber", "+372 555 22 33"),
    ]);
    create_book(Path.Combine(profile_dir, "carddav.sqlite"), [
        ("777", "_vCard", "BEGIN:VCARD\nVERSION:4.0\nFN:Thunder\n bird Person\nEMAIL:person@cloud.eu\nEND:VCARD"),
    ]);
    create_book(Path.Combine(profile_dir, "unrelated.sqlite"), [("999", "DisplayName", "Excluded")]);

    var sources = profile_discovery.discover(root_dir);
    check(sources.Count == 2, "discovers only Thunderbird address books");
    check(sources.Any(x => x.book_name == "Cloud Contacts"), "discovers CardDAV friendly name");

    var result = contact_repository.load(root_dir);
    check(result.issues.Count == 0, "loads without warnings");
    check(result.contacts.Count == 3, "loads three contacts across two books");
    var elodie = result.contacts.Single(c => c.display_name == "Élodie Test");
    check(elodie.emails.Count == 2, "preserves multiple email addresses");
    check(elodie.phones.Single() == "+37255501234", "parses tel URI values");
    check(contact_search.find(result.contacts, "elodie").Single().uid == "123", "search is accent insensitive");
    check(contact_search.find(result.contacts, "5552233").Single().uid == "456", "search ignores phone formatting");
    check(contact_search.find(result.contacts, "PERSON@CLOUD.EU").Single().uid == "777", "search is case insensitive");
    check(result.contacts.Single(c => c.uid == "777").display_name == "Thunderbird Person", "unfolds vCard lines");
    check(contact_search.find(result.contacts, "not found").Count == 0, "returns no false matches");

    var fallback = vcard_parser.parse("BEGIN:VCARD\nN:Surname;Forename;;;\nitem1.EMAIL;TYPE=work:test@example.org\nTEL;TYPE=cell:+372-55-55\nEND:VCARD");
    check(fallback.full_name == "Forename Surname", "parses structured name");
    check(fallback.emails.Single() == "test@example.org", "parses grouped email property");
    var quoted = vcard_parser.parse("FN;ALTID=\"urn:example:name\":Quoted Name\nEMAIL;TYPE=\"work:office\":quoted@example.org");
    check(quoted.full_name == "Quoted Name" && quoted.emails.Single() == "quoted@example.org", "ignores colons inside quoted vCard parameters");
    check(vcard_parser.parse("N:Family;Given;Middle;Dr.;Jr.").full_name == "Dr. Given Middle Family Jr.", "preserves all structured name components");
    check(vcard_parser.parse("FN:Comma\\, Name\\; Value\\nLine").full_name == "Comma, Name; Value\nLine", "unescapes vCard punctuation and newline");
    check(vcard_parser.parse(null).emails.Count == 0, "accepts an empty vCard");
    var rich_card = vcard_parser.parse("FN:Sample Person\nORG:Example Ltd;Research\nTITLE:Engineer\nADR:;;Example Street 1;Tallinn;;10111;Estonia\nURL:https://example.org\nBDAY:1990-11-12\nNOTE:First line\\nSecond line");
    check(rich_card.details.Any(d => d.label == "Organization" && d.value == "Example Ltd · Research"), "reads organization and department");
    check(rich_card.details.Any(d => d.label == "Address" && d.value.Contains("Tallinn")), "reads postal address");
    check(rich_card.details.Any(d => d.label == "Job title" && d.value == "Engineer"), "reads job title");
    check(rich_card.details.Any(d => d.label == "Notes" && d.value == "First line\nSecond line"), "reads multiline contact notes");
    check(rich_card.details.Any(d => d.label == "Website") && rich_card.details.Any(d => d.label == "Birthday"), "reads website and birthday");
    const string png = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+a5V8AAAAASUVORK5CYII=";
    var photo_card = vcard_parser.parse($"PHOTO:data:image/png;base64,{png}");
    check(contact_photo.load(photo_card, Path.Combine(profile_dir, "abook.sqlite"), null)?.Length == Convert.FromBase64String(png).Length, "reads embedded vCard 4 photo in memory");
    var escaped_photo = vcard_parser.parse($"PHOTO:data:image/png;base64\\,{png}");
    check(contact_photo.load(escaped_photo, Path.Combine(profile_dir, "abook.sqlite"), null)?.SequenceEqual(Convert.FromBase64String(png)) == true, "reads Thunderbird photo data URI with escaped comma");
    var binary_card = vcard_parser.parse($"PHOTO;ENCODING=b;TYPE=PNG:{png[..40]}\n {png[40..]}");
    check(contact_photo.load(binary_card, Path.Combine(profile_dir, "abook.sqlite"), null) is not null, "reads folded vCard 3 binary photo");
    var repeated_photo = vcard_parser.parse($"PHOTO;ENCODING=b;TYPE=PNG:{png}\nPHOTO:https://example.invalid/unused.jpg");
    check(contact_photo.load(repeated_photo, Path.Combine(profile_dir, "abook.sqlite"), null) is not null, "keeps the encoding associated with the first photo");
    check(contact_photo.load(vcard_parser.parse("PHOTO:https://example.invalid/private-photo.jpg"), "abook.sqlite", null) is null, "never fetches remote contact photos");
    check(contact_photo.load(vcard_parser.parse("PHOTO:data:image/png;base64,not-base64"), "abook.sqlite", null) is null, "ignores malformed embedded photo");
    check(contact_photo.load(vcard_parser.parse("PHOTO:data:image/svg+xml;base64,PHN2Zz48L3N2Zz4="), "abook.sqlite", null) is null, "ignores unsupported photo formats");
    Directory.CreateDirectory(Path.Combine(profile_dir, "Photos"));
    File.WriteAllBytes(Path.Combine(profile_dir, "Photos", "sample.png"), Convert.FromBase64String(png));
    check(contact_photo.load(vcard_parser.parse(null), Path.Combine(profile_dir, "abook.sqlite"), "sample.png") is not null, "reads legacy Thunderbird Photos file");
    check(contact_photo.load(vcard_parser.parse(null), Path.Combine(profile_dir, "abook.sqlite"), "../sample.png") is null, "rejects photo paths outside the Photos directory");
    check(contact_search.find(result.contacts, "abc5552233").Count == 0, "does not turn mixed text queries into phone matches");
    check(contact_search.find(result.contacts, "(555) 22-33").Single().uid == "456", "accepts formatted phone queries");
    check(contact_search.find(result.contacts, "", limit: 1).Count == 1, "respects result limits");

    File.AppendAllText(Path.Combine(profile_dir, "prefs.js"), "user_pref(\"ldap_2.servers.carddav.description\", \"Pilv \\u00d5\\u00e4\");\n");
    check(profile_discovery.discover(root_dir).Any(s => s.book_name == "Pilv Õä"), "decodes Unicode escapes in Thunderbird preferences");

    var database_path = Path.Combine(profile_dir, "abook.sqlite");
    var original_hash = SHA256.HashData(File.ReadAllBytes(database_path));
    var original_write_time = File.GetLastWriteTimeUtc(database_path);
    contact_repository.load(root_dir);
    check(SHA256.HashData(File.ReadAllBytes(database_path)).SequenceEqual(original_hash), "read leaves database bytes unchanged");
    check(File.GetLastWriteTimeUtc(database_path) == original_write_time, "read leaves database modification time unchanged");

    using (var locked = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = database_path, Mode = SqliteOpenMode.ReadWrite, Pooling = false }.ToString()))
    {
        locked.Open();
        using var command = locked.CreateCommand();
        command.CommandText = "PRAGMA journal_mode=WAL; PRAGMA wal_autocheckpoint=0; INSERT INTO properties VALUES ('wal-contact', 'DisplayName', 'WAL Person')";
        command.ExecuteNonQuery();
        check(File.Exists(database_path + "-wal"), "fixture contains a live WAL file");
        check(contact_repository.load(root_dir).contacts.Any(c => c.uid == "wal-contact"), "reads committed contacts from a live WAL");
        using (var transaction = locked.BeginTransaction())
        {
            command.Transaction = transaction;
            command.CommandText = "INSERT INTO properties VALUES ('pending', 'DisplayName', 'Pending Person')";
            command.ExecuteNonQuery();
            check(!contact_repository.load(root_dir).contacts.Any(c => c.uid == "pending"), "does not expose uncommitted contacts");
            transaction.Rollback();
        }
    }

    using (var locked = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = database_path, Mode = SqliteOpenMode.ReadWrite, Pooling = false }.ToString()))
    {
        locked.Open();
        using var command = locked.CreateCommand();
        command.CommandText = "PRAGMA journal_mode=DELETE; BEGIN EXCLUSIVE";
        command.ExecuteNonQuery();
        var timer = Stopwatch.StartNew();
        var partial = contact_repository.load(root_dir);
        check(partial.issues.Count == 1 && partial.contacts.Any(c => c.uid == "777"), "locked database does not hide other address books");
        check(timer.Elapsed < TimeSpan.FromSeconds(5), "locked database has a bounded timeout");
        command.CommandText = "ROLLBACK";
        command.ExecuteNonQuery();
    }

    File.WriteAllText(Path.Combine(profile_dir, "abook-9.sqlite"), "not a sqlite database");
    check(contact_repository.load(root_dir).issues.Count == 1, "corrupt book reports a notice");
    File.Delete(Path.Combine(profile_dir, "abook-9.sqlite"));
    using (var unsupported = new SqliteConnection($"Data Source={Path.Combine(profile_dir, "abook-8.sqlite")};Pooling=False"))
    {
        unsupported.Open();
        using var command = unsupported.CreateCommand();
        command.CommandText = "CREATE TABLE unexpected (value TEXT)";
        command.ExecuteNonQuery();
    }
    check(contact_repository.load(root_dir).issues.Any(i => i.Contains("Unsupported address book schema")), "unsupported schema is not silently treated as an empty book");
    check(contact_repository.load(Path.Combine(root_dir, "missing")).issues.Count == 1, "missing Thunderbird installation reports a notice");

    var external_profile = Path.Combine(root_dir, "external");
    Directory.CreateDirectory(external_profile);
    create_book(Path.Combine(external_profile, "history.sqlite"), [("ext", "DisplayName", "External")]);
    File.AppendAllText(Path.Combine(root_dir, "profiles.ini"), $"\n[Profile1]\nName=External\nIsRelative=0\nPath={external_profile}\n");
    check(profile_discovery.discover(root_dir).Any(s => s.profile_name == "External"), "discovers absolute profile paths");
    check(profile_discovery.discover(root_dir).Count(s => s.file_path == database_path) == 1, "does not duplicate profiles found in ini and directory scan");
    Console.WriteLine($"PASS: {assertion_count} assertions — synthetic contacts only");
}
finally
{
    // All SQLite connections have been disposed before cleanup on Windows.
    try { Directory.Delete(root_dir, recursive: true); } catch (IOException) { }
}
return 0;

void check(bool passed, string description)
{
    if (!passed) throw new Exception($"FAILED: {description}");
    assertion_count++;
    Console.WriteLine($"PASS: {description}");
}

static void create_book(string file_path, IEnumerable<(string card, string name, string value)> properties)
{
    using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = file_path, Mode = SqliteOpenMode.ReadWriteCreate, Pooling = false }.ToString());
    connection.Open();
    using var create_command = connection.CreateCommand();
    create_command.CommandText = "CREATE TABLE properties (card TEXT, name TEXT, value TEXT); CREATE INDEX properties_card ON properties(card)";
    create_command.ExecuteNonQuery();
    foreach (var (card, name, value) in properties)
    {
        using var insert_command = connection.CreateCommand();
        insert_command.CommandText = "INSERT INTO properties (card, name, value) VALUES ($card, $name, $value)";
        insert_command.Parameters.AddWithValue("$card", card);
        insert_command.Parameters.AddWithValue("$name", name);
        insert_command.Parameters.AddWithValue("$value", value);
        insert_command.ExecuteNonQuery();
    }
}
