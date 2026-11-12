using contact_palette_core;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace contact_palette;

internal sealed partial class contact_fields_page : ListPage
{
    private readonly IListItem[] _items;

    public contact_fields_page(contact_record contact)
    {
        Title = contact.display_name;
        Name = "Choose field to copy";
        PlaceholderText = "Choose a field and press Enter to copy";
        Icon = new IconInfo("\uE8C8");
        var fields = new List<(string label, string value)> { ("Name", contact.display_name) };
        fields.AddRange(contact.emails.Select(value => ("Email", value)));
        fields.AddRange(contact.phones.Select(value => ("Phone", value)));
        fields.AddRange(contact.details.Select(detail => (detail.label, detail.value)));
        _items = fields.Where(f => !string.IsNullOrWhiteSpace(f.value)).Distinct().Select(field => (IListItem)new ListItem(new CopyTextCommand(field.value) { Name = "Copy " + field.label.ToLowerInvariant() })
        {
            Title = field.value,
            Subtitle = field.label + " · Enter to copy",
        }).ToArray();
    }

    public override IListItem[] GetItems() => _items;
}
