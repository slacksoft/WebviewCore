using System.Net;
using SkiaSharp;

namespace WebviewCore;

static class ImageLoader
{
    private static readonly Dictionary<string, SKBitmap> Cache = new();
    private static readonly HttpClient Client = new(new SocketsHttpHandler
    {
        AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
        AllowAutoRedirect = true,
        MaxAutomaticRedirections = 10,
    })
    { Timeout = TimeSpan.FromSeconds(10) };

    static ImageLoader() { Client.DefaultRequestHeaders.UserAgent.ParseAdd("WebViewCore/1.0"); }

    public static SKBitmap? GetCached(string url) { lock (Cache) return Cache.TryGetValue(url, out var i) ? i : null; }

    public static async Task<SKBitmap?> FetchAsync(string url)
    {
        lock (Cache) if (Cache.TryGetValue(url, out var c)) return c;
        try
        {
            byte[] data;
            if (url.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
            {
                var path = Uri.UnescapeDataString(url[8..].TrimStart('/'));
                data = await File.ReadAllBytesAsync(path);
            }
            else data = await Client.GetByteArrayAsync(url);
            using var ms = new MemoryStream(data);
            var img = SKBitmap.Decode(ms);
            if (img != null)
                lock (Cache) Cache[url] = img;
            return img;
        }
        catch { return null; }
    }
}
