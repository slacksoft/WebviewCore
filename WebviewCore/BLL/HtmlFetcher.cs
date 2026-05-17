using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;

namespace WebviewCore;

static class HtmlFetcher
{
    private static readonly HttpClient Client = new(new SocketsHttpHandler
    {
        AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
        AllowAutoRedirect = true,
        MaxAutomaticRedirections = 10,
    })
    { Timeout = TimeSpan.FromSeconds(15) };

    public static string BaseUrl { get; private set; } = "";

    static HtmlFetcher()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        Client.DefaultRequestHeaders.UserAgent.ParseAdd("WebViewCore/1.0");
    }

    public static async Task<string> FetchAsync(string url)
    {
        BaseUrl = url;
        if (url.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
        {
            var path = Uri.UnescapeDataString(url[8..].TrimStart('/'));
            var bytes = await File.ReadAllBytesAsync(path);
            return DecodeHtml(bytes, null);
        }

        var resp = await Client.GetAsync(url).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        var data = await resp.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
        return DecodeHtml(data, resp.Content.Headers.ContentType);
    }

    internal static string DecodeHtml(byte[] data, MediaTypeHeaderValue? contentType)
    {
        if (data.Length >= 3 && data[0] == 0xEF && data[1] == 0xBB && data[2] == 0xBF)
            return Encoding.UTF8.GetString(data, 3, data.Length - 3);
        if (data.Length >= 2 && data[0] == 0xFF && data[1] == 0xFE)
            return Encoding.Unicode.GetString(data, 2, data.Length - 2);
        if (data.Length >= 2 && data[0] == 0xFE && data[1] == 0xFF)
            return Encoding.BigEndianUnicode.GetString(data, 2, data.Length - 2);

        var charset = NormalizeCharset(contentType?.CharSet);
        charset ??= SniffMetaCharset(data);
        if (charset != null && TryGetEncoding(charset, out var declared))
            return declared.GetString(data);

        var utf8 = new UTF8Encoding(false, true);
        try { return utf8.GetString(data); }
        catch (DecoderFallbackException)
        {
            if (TryGetEncoding("gb18030", out var gb18030))
                return gb18030.GetString(data);
            return Encoding.Default.GetString(data);
        }
    }

    private static string? SniffMetaCharset(byte[] data)
    {
        var scanLength = Math.Min(data.Length, 4096);
        var head = Encoding.ASCII.GetString(data, 0, scanLength);
        var match = Regex.Match(head, @"<meta[^>]+charset\s*=\s*[""']?\s*([a-zA-Z0-9_\-]+)", RegexOptions.IgnoreCase);
        if (match.Success) return NormalizeCharset(match.Groups[1].Value);

        match = Regex.Match(head, @"<meta[^>]+http-equiv\s*=\s*[""']?content-type[""']?[^>]+content\s*=\s*[""'][^""']*charset\s*=\s*([a-zA-Z0-9_\-]+)", RegexOptions.IgnoreCase);
        return match.Success ? NormalizeCharset(match.Groups[1].Value) : null;
    }

    private static string? NormalizeCharset(string? charset)
    {
        if (string.IsNullOrWhiteSpace(charset)) return null;
        var value = charset.Trim().Trim('"', '\'').ToLowerInvariant();
        return value switch
        {
            "gbk" or "gb2312" or "gb18030" or "cp936" => "gb18030",
            "big5" or "big5-hkscs" => "big5",
            "utf8" or "utf-8" => "utf-8",
            _ => value,
        };
    }

    private static bool TryGetEncoding(string charset, out Encoding encoding)
    {
        try
        {
            encoding = Encoding.GetEncoding(charset);
            return true;
        }
        catch
        {
            encoding = Encoding.UTF8;
            return false;
        }
    }
}
