using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using contact_palette_core;
using Windows.Storage.Streams;
using Windows.System;

namespace contact_palette;

internal sealed partial class contacts_page : DynamicListPage
{
    private IReadOnlyList<contact_record> _contacts = [];
    private IReadOnlyList<string> _issues = [];
    private IListItem[] _items = [];
    private string _search_text = string.Empty;
    private DateTime _last_loaded_at = DateTime.MinValue;
    private readonly object _gate = new();
    private bool _loading;

    public contacts_page()
    {
        Id = "ContactPalette.SearchThunderbird";
        Icon = IconHelpers.FromRelativePath("Assets\\StoreLogo.png");
        Title = "Thunderbird contacts";
        Name = "Search contacts";
        PlaceholderText = "Search name, email, or phone number";
        ShowDetails = true;
        reload_contacts();
    }

    public override void UpdateSearchText(string old_search, string new_search)
    {
        lock (_gate) _search_text = new_search;
        reload_if_stale();
        update_results();
    }

    public override IListItem[] GetItems()
    {
        reload_if_stale();
        lock (_gate) return _items;
    }

    private void reload_if_stale()
    {
        bool stale;
        lock (_gate) stale = DateTime.UtcNow - _last_loaded_at > TimeSpan.FromSeconds(20);
        if (stale) reload_contacts();
    }

    private void reload_contacts()
    {
        lock (_gate)
        {
            if (_loading) return;
            _loading = true;
        }
        update_results();
        _ = Task.Run(() =>
        {
            load_result loaded;
            try { loaded = contact_repository.load(); }
            catch (Exception)
            {
                // Do not log contact data or private profile paths.
                loaded = new load_result([], ["Could not load address books. Refresh to retry."]);
            }
            lock (_gate)
            {
                _contacts = loaded.contacts;
                _issues = loaded.issues;
                _last_loaded_at = DateTime.UtcNow;
                _loading = false;
            }
            update_results();
        });
    }

    private void update_results()
    {
        lock (_gate)
        {
        var items = new List<IListItem>();
        if (_loading) items.Add(new ListItem(new NoOpCommand()) { Title = "Loading contacts…", Subtitle = "Reading local Thunderbird address books" });
        if (string.IsNullOrWhiteSpace(_search_text))
        {
            items.Add(new ListItem(new AnonymousCommand(reload_contacts) { Result = CommandResult.KeepOpen() })
            {
                Title = $"Refresh contacts ({_contacts.Count} loaded)",
                Subtitle = "Re-read local Thunderbird address books",
            });
        }
        foreach (var contact in contact_search.find(_contacts, _search_text)) items.Add(build_contact_item(contact));
        if (items.Count == 0) items.Add(new ListItem(new NoOpCommand()) { Title = "No matching contacts", Subtitle = "Try a different name, email address or number" });
        if (string.IsNullOrWhiteSpace(_search_text))
        {
            foreach (var issue in _issues.Take(3)) items.Add(new ListItem(new NoOpCommand()) { Title = "Address book notice", Subtitle = issue });
        }
        _items = items.ToArray();
        }
        RaiseItemsChanged();
    }

    private static IListItem build_contact_item(contact_record contact)
    {
        var primary_value = contact.emails.FirstOrDefault() ?? contact.phones.FirstOrDefault() ?? contact.display_name;
        var secondary = contact.emails.FirstOrDefault() ?? contact.phones.FirstOrDefault() ?? contact.address_book;
        var actions = new List<IContextItem>();
        actions.Add(new CommandContextItem(new contact_fields_page(contact))
        {
            Title = "Choose field to copy",
            RequestedShortcut = KeyChordHelpers.FromModifiers(vkey: VirtualKey.Right),
        });
        foreach (var email in contact.emails)
        {
            actions.Add(new CommandContextItem(new CopyTextCommand(email) { Name = $"Copy email: {email}" }));
            actions.Add(new CommandContextItem(new OpenUrlCommand($"mailto:{Uri.EscapeDataString(email)}") { Name = $"Compose email: {email}" }));
        }
        foreach (var phone in contact.phones) actions.Add(new CommandContextItem(new CopyTextCommand(phone) { Name = $"Copy phone: {phone}" }));
        actions.Add(new CommandContextItem(new CopyTextCommand(contact.display_name) { Name = "Copy contact name" }));
        var metadata = new List<IDetailsElement>();
        void add_detail(string key, string value) => metadata.Add(new DetailsElement { Key = key, Data = new DetailsLink { Text = value } });
        foreach (var email in contact.emails) add_detail("Email", email);
        foreach (var phone in contact.phones) add_detail("Phone", phone);
        foreach (var detail in contact.details) add_detail(detail.label, detail.value);
        add_detail("Address book", contact.address_book);
        add_detail("Profile", contact.profile_name);
        var portrait = contact.photo is { } bytes
            ? photo_icon(bytes)
            : new IconInfo("\uE77B");
        return new ListItem(new CopyTextCommand(primary_value))
        {
            Title = contact.display_name,
            Subtitle = $"{secondary}  ·  {contact.address_book} ({contact.profile_name})",
            MoreCommands = actions.ToArray(),
            Icon = portrait,
            Details = new Details
            {
                Title = contact.display_name,
                HeroImage = portrait,
                Metadata = metadata.ToArray(),
            },
        };
    }

    private static IconInfo photo_icon(byte[] bytes)
    {
        // Use a native WinRT stream: .NET stream adapters cannot be read by the host process.
        var stream = new InMemoryRandomAccessStream();
        using var writer = new DataWriter(stream);
        writer.WriteBytes(bytes);
        writer.StoreAsync().AsTask().GetAwaiter().GetResult();
        writer.DetachStream();
        stream.Seek(0);
        return IconInfo.FromStream(stream);
    }
}
