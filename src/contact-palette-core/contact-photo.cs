namespace contact_palette_core;

public static class contact_photo
{
    private const int maximum_bytes = 4 * 1024 * 1024;

    // Photos stay in memory. Never fetch remote PHOTO URLs or persist a photo cache.
    public static byte[]? load(vcard_fields card, string source_file, string? photo_name)
    {
        try
        {
            byte[]? bytes = null;
            var value = card.photo_value;
            if (value?.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase) == true)
            {
                var comma = value.IndexOf(',');
                if (comma < 0 || !value[..comma].EndsWith(";base64", StringComparison.OrdinalIgnoreCase)) return null;
                if (value.Length - comma > maximum_bytes * 4 / 3 + 8) return null;
                bytes = Convert.FromBase64String(value[(comma + 1)..]);
            }
            else if (card.photo_is_binary && value is not null)
            {
                if (value.Length > maximum_bytes * 4 / 3 + 8) return null;
                bytes = Convert.FromBase64String(value);
            }
            if (supported_image(bytes)) return bytes;

            if (string.IsNullOrWhiteSpace(photo_name) || photo_name.IndexOfAny(['/', '\\', ':']) >= 0 || photo_name is "." or "..") return null;
            var directory = Path.Combine(Path.GetDirectoryName(source_file)!, "Photos");
            var path = Path.Combine(directory, photo_name);
            if (!File.Exists(path) || (File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0 || (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) return null;
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            if (stream.Length > maximum_bytes || stream.Length < 12) return null;
            bytes = new byte[(int)stream.Length];
            stream.ReadExactly(bytes);
            return supported_image(bytes) ? bytes : null;
        }
        catch (Exception exception) when (exception is FormatException or IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    private static bool supported_image(byte[]? bytes)
    {
        if (bytes is null || bytes.Length < 12 || bytes.Length > maximum_bytes) return false;
        var data = bytes.AsSpan();
        return data.StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }) ||
            (bytes[0] == 0xff && bytes[1] == 0xd8 && bytes[2] == 0xff) ||
            data.StartsWith("GIF87a"u8) || data.StartsWith("GIF89a"u8) ||
            data.StartsWith("BM"u8) || (data.StartsWith("RIFF"u8) && data[8..].StartsWith("WEBP"u8));
    }
}
