using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.CommandPalette.Extensions;
using WinRT;
using Windows.Graphics.Imaging;

internal static class Program
{
    [DllImport("ole32.dll", ExactSpelling = true)]
    private static extern int CoCreateInstance(in Guid clsid, nint outer, uint context, in Guid iid, out nint instance);

    [MTAThread]
    private static int Main(string[] args)
    {
        if (args.Length != 1) throw new ArgumentException("Pass the published x64 extension executable, or --installed to inspect the registered instance.");
        var installed = args[0] == "--installed";
        // A registered server may be shared with the live host. Never dispose that instance.
        using var process = installed ? null : Process.Start(new ProcessStartInfo(Path.GetFullPath(args[0]), "-RegisterProcessAsComServer")
        { UseShellExecute = false, CreateNoWindow = true }) ?? throw new Exception("Could not start COM server.");
        nint pointer = 0;
        try
        {
            var timer = Stopwatch.StartNew();
            var clsid = new Guid("bff97378-7082-4702-b907-514039b3e61b");
            var iid = typeof(IExtension).GUID;
            int result;
            do
            {
                if (process?.HasExited == true) throw new Exception($"COM server exited early: {process.ExitCode}");
                result = CoCreateInstance(clsid, 0, 4, iid, out pointer);
                if (result >= 0) break;
                Thread.Sleep(100);
            } while (timer.Elapsed < TimeSpan.FromSeconds(10));
            Marshal.ThrowExceptionForHR(result);
            var extension = MarshalInspectable<IExtension>.FromAbi(pointer);
            var provider = extension.GetProvider(ProviderType.Commands) as ICommandProvider ?? throw new Exception("No command provider.");
            if (provider.DisplayName != "Contact Palette") throw new Exception("Wrong provider display name.");
            if (provider.Id != "ContactPalette.Commands") throw new Exception("Expected a stable provider ID.");
            var commands = provider.TopLevelCommands();
            if (commands.Length != 1) throw new Exception("Expected one top-level command.");
            if (commands[0].Command.Id != "ContactPalette.SearchThunderbird" || provider.GetCommand(commands[0].Command.Id) is null) throw new Exception("Stable command lookup failed.");
            var page = commands[0].Command as IListPage ?? throw new Exception("Expected a list page.");
            if (!page.ShowDetails) throw new Exception("Contact details panel must open automatically.");
            var items = page.GetItems();
            if (items.Length == 0) throw new Exception("Expected loading/status/contact rows.");
            Console.WriteLine("PASS: x64 process start, COM activation, WinRT provider, top-level command, list page and automatic details panel. No contact data printed.");
            if (installed)
            {
                timer.Restart();
                while (!items.Any(i => i.Details is not null) && timer.Elapsed < TimeSpan.FromSeconds(10))
                {
                    Thread.Sleep(100);
                    items = page.GetItems();
                }
                var details = items.Where(i => i.Details is not null).Select(i => i.Details!).ToArray();
                if (details.Length == 0 || details.Any(d => d.Metadata.Length == 0)) throw new Exception("Expected populated contact details.");
                Console.WriteLine("PASS: populated contact metadata crosses the COM boundary.");
                var contactItem = items.First(i => i.Details is not null);
                var chooser = contactItem.MoreCommands.OfType<ICommandContextItem>().FirstOrDefault(c => c.RequestedShortcut.Vkey == 39);
                if (chooser?.Command is not IListPage fields || fields.GetItems().Length == 0) throw new Exception("Right arrow must open the copy-field chooser.");
                if (fields.GetItems().Any(i => i.Command.Name.Length == 0)) throw new Exception("Copy-field actions must be named.");
                Console.WriteLine("PASS: Right-arrow shortcut opens populated copy-field commands.");
                var photos = details.Select(d => d.HeroImage?.Light?.Data).Where(d => d is not null).ToArray();
                foreach (var photo in photos)
                {
                    using var stream = photo!.OpenReadAsync().AsTask().GetAwaiter().GetResult();
                    if (stream.Size == 0) throw new Exception("Empty contact image stream.");
                    var decoder = BitmapDecoder.CreateAsync(stream).AsTask().GetAwaiter().GetResult();
                    var pixels = decoder.GetPixelDataAsync().AsTask().GetAwaiter().GetResult().DetachPixelData();
                    if (decoder.PixelWidth == 0 || decoder.PixelHeight == 0 || pixels.Length == 0) throw new Exception("Contact photo did not decode.");
                }
                Console.WriteLine($"PASS: {photos.Length} visible contact photos decoded across the COM boundary.");
                var localPhotos = contact_palette_core.contact_repository.load().contacts.Where(c => c.photo is not null).ToArray();
                foreach (var contact in localPhotos)
                {
                    using var stream = new Windows.Storage.Streams.InMemoryRandomAccessStream();
                    using var writer = new Windows.Storage.Streams.DataWriter(stream);
                    writer.WriteBytes(contact.photo!);
                    writer.StoreAsync().AsTask().GetAwaiter().GetResult();
                    writer.DetachStream();
                    stream.Seek(0);
                    var decoder = BitmapDecoder.CreateAsync(stream).AsTask().GetAwaiter().GetResult();
                    var pixels = decoder.GetPixelDataAsync().AsTask().GetAwaiter().GetResult().DetachPixelData();
                    if (pixels.Length == 0) throw new Exception("A local contact photo did not decode.");
                }
                Console.WriteLine($"PASS: all {localPhotos.Length} local contact photos decoded; no contact data printed.");
                return 0;
            }
            extension.As<IDisposable>().Dispose();
            if (!process!.WaitForExit(5000)) throw new Exception("COM server did not stop after disposal. Use --installed if this CLSID is already registered and running.");
            if (process.ExitCode != 0) throw new Exception($"COM server exit code: {process.ExitCode}");
            Console.WriteLine("PASS: COM disposal stops the extension cleanly.");
            return 0;
        }
        finally
        {
            if (pointer != 0) Marshal.Release(pointer);
            if (process is not null && !process.HasExited) process.Kill(entireProcessTree: true);
        }
    }
}
