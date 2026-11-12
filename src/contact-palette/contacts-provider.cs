using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace contact_palette;

public sealed partial class contacts_provider : CommandProvider
{
    private readonly ICommandItem[] _commands;
    private readonly contacts_page _page;

    public contacts_provider()
    {
        Id = "ContactPalette.Commands";
        DisplayName = "Contact Palette";
        Icon = IconHelpers.FromRelativePath("Assets\\StoreLogo.png");
        _page = new contacts_page();
        _commands = [new CommandItem(_page) { Title = "Search Thunderbird contacts" }];
    }

    public override ICommandItem[] TopLevelCommands() => _commands;

    public override ICommand? GetCommand(string id) => id == _page.Id ? _page : null;
}
