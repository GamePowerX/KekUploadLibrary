using System.Security.Cryptography;
using System.Text.Json;
using SharpHash.Interfaces;

namespace KekUploadLibrary;

public static class Utils
{
    public static string HashBytes(byte[] data)
    {
        var hash = SHA1.Create().ComputeHash(data);
        return string.Concat(hash.Select(b => b.ToString("x2")));
    }

    public static string HashFile(string file)
    {
        var stream = File.OpenRead(file);
        var hash = SHA1.Create().ComputeHash(stream);
        return string.Concat(hash.Select(b => b.ToString("x2")));
    }

    public static string HashStream(Stream stream)
    {
        var hash = SHA1.Create().ComputeHash(stream);
        return string.Concat(hash.Select(b => b.ToString("x2")));
    }

    public static IHash UpdateHash(IHash hash, byte[] data)
    {
        hash.TransformBytes(data);
        return hash;
    }

    public static string? ParseUploadStreamId(string streamId)
    {
        var id = JsonSerializer.Deserialize<Dictionary<string, string>>(streamId);
        return id?["stream"];
    }

    public static string? ParseDownloadId(string downloadId)
    {
        var id = JsonSerializer.Deserialize<Dictionary<string, string>>(downloadId);
        return id?["id"];
    }


    public static string SizeToString(long size)
    {
        if (size >= 1099511627776)
            return decimal.Round((decimal)(Math.Round(size / 10995116277.76) * 0.01), 2) + " TiB";
        if (size >= 1073741824)
            return decimal.Round((decimal)(Math.Round(size / 10737418.24) * 0.01), 2) + " GiB";
        if (size >= 1048576)
            return decimal.Round((decimal)(Math.Round(size / 10485.76) * 0.01), 2) + " MiB";
        if (size >= 1024)
            return decimal.Round((decimal)(Math.Round(size / 10.24) * 0.01), 2) + " KiB";
        return size + " bytes";
    }
}