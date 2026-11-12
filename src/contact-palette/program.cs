using Microsoft.CommandPalette.Extensions;
using Shmuelie.WinRTServer;
using Shmuelie.WinRTServer.CsWinRT;

namespace contact_palette;

public static class Program
{
    [MTAThread]
    public static void Main(string[] args)
    {
        if (args.Length == 0 || args[0] != "-RegisterProcessAsComServer") return;
        using var disposed_event = new ManualResetEvent(false);
        using var extension_instance = new contact_palette_extension(disposed_event);
        var server = new ComServer();
        server.RegisterClass<contact_palette_extension, IExtension>(() => extension_instance);
        server.Start();
        disposed_event.WaitOne();
        server.Stop();
        server.UnsafeDispose();
    }
}
