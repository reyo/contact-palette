using System.Runtime.InteropServices;
using Microsoft.CommandPalette.Extensions;

namespace contact_palette;

[Guid("bff97378-7082-4702-b907-514039b3e61b")]
public sealed partial class contact_palette_extension : IExtension, IDisposable
{
    private readonly ManualResetEvent _disposed_event;
    private readonly contacts_provider _provider = new();

    public contact_palette_extension(ManualResetEvent disposed_event) => _disposed_event = disposed_event;

    public object? GetProvider(ProviderType provider_type) => provider_type == ProviderType.Commands ? _provider : null;

    public void Dispose() => _disposed_event.Set();
}
