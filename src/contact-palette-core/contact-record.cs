namespace contact_palette_core;

public sealed record contact_record(
    string uid,
    string display_name,
    string address_book,
    string profile_name,
    IReadOnlyList<string> emails,
    IReadOnlyList<string> phones,
    string source_file)
{
    public IReadOnlyList<contact_detail> details { get; init; } = [];
    public byte[]? photo { get; init; }
}

public sealed record contact_detail(string label, string value);

public sealed record address_book_source(string file_path, string profile_name, string book_name);

public sealed record load_result(IReadOnlyList<contact_record> contacts, IReadOnlyList<string> issues);
