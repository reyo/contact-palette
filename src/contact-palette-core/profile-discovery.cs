using System.Text.RegularExpressions;
using System.Text.Json;

namespace contact_palette_core;

public static partial class profile_discovery
{
    private static readonly StringComparer _path_comparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    public static IReadOnlyList<address_book_source> discover(string? root_dir = null)
    {
        root_dir ??= Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Thunderbird");
        if (!Directory.Exists(root_dir)) return [];

        var profiles = new List<(string name, string path)>();
        var ini_path = Path.Combine(root_dir, "profiles.ini");
        if (File.Exists(ini_path))
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var section = string.Empty;

            void flush_section()
            {
                if (!section.StartsWith("Profile", StringComparison.OrdinalIgnoreCase) || !values.TryGetValue("Path", out var directory)) return;
                var relative = !values.TryGetValue("IsRelative", out var is_relative) || is_relative == "1";
                var profile_path = relative ? Path.GetFullPath(Path.Combine(root_dir, directory.Replace('/', Path.DirectorySeparatorChar))) : directory;
                if (Directory.Exists(profile_path)) profiles.Add((values.GetValueOrDefault("Name", section), profile_path));
            }

            foreach (var raw_line in File.ReadLines(ini_path))
            {
                var line = raw_line.Trim();
                if (line.StartsWith('[') && line.EndsWith(']'))
                {
                    flush_section();
                    section = line[1..^1];
                    values.Clear();
                    continue;
                }
                if (line.StartsWith('#') || line.StartsWith(';')) continue;
                var separator = line.IndexOf('=');
                if (separator > 0) values[line[..separator].Trim()] = line[(separator + 1)..].Trim();
            }
            flush_section();
        }

        var fallback_dir = Path.Combine(root_dir, "Profiles");
        if (Directory.Exists(fallback_dir))
        {
            foreach (var directory in Directory.EnumerateDirectories(fallback_dir))
            {
                if (!profiles.Any(p => _path_comparer.Equals(Path.GetFullPath(p.path), Path.GetFullPath(directory)))) profiles.Add((Path.GetFileName(directory), directory));
            }
        }
        if (File.Exists(Path.Combine(root_dir, "abook.sqlite"))) profiles.Add(("Portable profile", root_dir));

        var sources = new List<address_book_source>();
        var used_paths = new HashSet<string>(_path_comparer);
        foreach (var (name, path) in profiles)
        {
            var book_names = read_book_names(Path.Combine(path, "prefs.js"));
            foreach (var file_path in Directory.EnumerateFiles(path, "*.sqlite", SearchOption.TopDirectoryOnly))
            {
                var file_name = Path.GetFileName(file_path);
                if (!default_book_pattern().IsMatch(file_name) && !book_names.ContainsKey(file_name)) continue;
                if (!used_paths.Add(Path.GetFullPath(file_path))) continue;
                var book_name = book_names.GetValueOrDefault(file_name) ?? (file_name.Equals("history.sqlite", StringComparison.OrdinalIgnoreCase) ? "Collected Addresses" : file_name.Equals("abook.sqlite", StringComparison.OrdinalIgnoreCase) ? "Personal Address Book" : file_name);
                sources.Add(new address_book_source(file_path, name, book_name));
            }
        }
        return sources.OrderBy(s => s.profile_name, StringComparer.OrdinalIgnoreCase).ThenBy(s => s.book_name, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static Dictionary<string, string> read_book_names(string prefs_path)
    {
        var properties = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(prefs_path)) return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in File.ReadLines(prefs_path))
        {
            var match = book_pref_pattern().Match(line);
            if (!match.Success) continue;
            var book_id = match.Groups["book_id"].Value;
            var field = match.Groups["field"].Value;
            string value;
            try { value = JsonSerializer.Deserialize<string>("\"" + match.Groups["value"].Value + "\"") ?? string.Empty; }
            catch (JsonException) { continue; }
            if (!properties.TryGetValue(book_id, out var book)) properties[book_id] = book = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            book[field] = value;
        }
        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var book in properties.Values)
        {
            if (!book.TryGetValue("filename", out var file_name) || !file_name.EndsWith(".sqlite", StringComparison.OrdinalIgnoreCase) || Path.GetFileName(file_name) != file_name) continue;
            names[file_name] = book.GetValueOrDefault("description") ?? file_name;
        }
        return names;
    }

    [GeneratedRegex(@"^(abook(?:-\d+)?|history)\.sqlite$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex default_book_pattern();

    [GeneratedRegex("^\\s*user_pref\\(\\\"ldap_2\\.servers\\.(?<book_id>[^\\\"]+)\\.(?<field>filename|description)\\\",\\s*\\\"(?<value>(?:\\\\.|[^\\\"\\\\])*)\\\"\\);", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex book_pref_pattern();
}
