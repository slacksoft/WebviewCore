using SkiaSharp;

namespace WebviewCore;

static class FontResolver
{
    private static readonly Dictionary<string, SKTypeface> _typefaceCache = new();
    private static readonly string[] _cjkFallbackFamilies =
    {
        "Microsoft YaHei UI", "Microsoft YaHei", "SimSun-ExtB", "MingLiU-ExtB", "PMingLiU-ExtB",
        "SimSun", "NSimSun", "SimHei",
        "DengXian", "KaiTi", "FangSong", "Microsoft JhengHei UI", "Microsoft JhengHei",
        "Noto Sans CJK SC", "Noto Sans SC", "Source Han Sans SC", "Source Han Sans CN",
        "Noto Serif CJK SC", "Source Han Serif SC", "Malgun Gothic", "Meiryo", "Yu Gothic",
        "Arial Unicode MS",
    };
    private static readonly HashSet<string> _nonCjkFamilySet = new(StringComparer.OrdinalIgnoreCase)
    {
        "s", "segoe", "segoeui", "arial", "helvetica", "tahoma", "verdana",
        "trebuchet", "times", "timesnewroman", "consolas", "courier", "couriernew",
        "georgia", "palatino", "garamond", "impact", "lucida", "calibri", "cambria",
        "candara", "franklingothic", "gillsans",
    };

    public static string ResolveFamily(string family, string text)
    {
        if (string.IsNullOrEmpty(text) || !HasCjk(text)) return family;
        if (_nonCjkFamilySet.Contains(NormalizeFamily(family))) return _cjkFallbackFamilies[0];
        return family;
    }

    private const string DefaultFont = "Segoe UI";

    public static SKTypeface GetTypeface(string family, bool bold, bool italic, string? text = null)
    {
        var hasCjk = text != null && HasCjk(text);
        if (hasCjk && _nonCjkFamilySet.Contains(NormalizeFamily(family)))
            return GetSystemFallbackForText(text!, bold, italic) ?? GetCjkFallback(bold, italic) ?? GetOrCreate(DefaultFont, bold, italic) ?? SKTypeface.Default;

        var resolved = text != null ? ResolveFamily(family, text) : family;
        return GetOrCreate(resolved, bold, italic)
            ?? (hasCjk ? GetSystemFallbackForText(text!, bold, italic) ?? GetCjkFallback(bold, italic) : null)
            ?? GetOrCreate(DefaultFont, bold, italic)
            ?? SKTypeface.Default;
    }

    public static SKTypeface GetTypefaceForTextElement(string family, bool bold, bool italic, string textElement, SKTypeface? preferred = null)
    {
        if (string.IsNullOrEmpty(textElement)) return preferred ?? GetTypeface(family, bold, italic, textElement);
        if (preferred != null && TypefaceContains(preferred, textElement)) return preferred;

        var direct = GetTypeface(family, bold, italic, textElement);
        if (TypefaceContains(direct, textElement)) return direct;

        var fallbackChar = GetFirstCodePoint(textElement);
        if (fallbackChar >= 0)
        {
            var fallback = GetSystemFallbackForCodePoint(fallbackChar, bold, italic);
            if (fallback != null && TypefaceContains(fallback, textElement)) return fallback;
        }

        var installedFallback = GetInstalledFallbackForTextElement(textElement, bold, italic);
        if (installedFallback != null) return installedFallback;

        var cjkFallback = GetCjkFallback(bold, italic);
        return cjkFallback != null && TypefaceContains(cjkFallback, textElement) ? cjkFallback : direct;
    }

    private static SKTypeface? GetOrCreate(string family, bool bold, bool italic)
        => GetOrCreate(family, bold, italic, requireExactFamily: true);

    private static SKTypeface? GetCjkFallback(bool bold, bool italic)
    {
        foreach (var family in _cjkFallbackFamilies)
        {
            var typeface = GetOrCreate(family, bold, italic, requireExactFamily: false);
            if (typeface != null) return typeface;
        }

        return null;
    }

    private static SKTypeface? GetSystemFallbackForText(string text, bool bold, bool italic)
    {
        var fallbackChar = GetFirstCjkCodePoint(text);
        if (fallbackChar < 0) return null;

        return GetSystemFallbackForCodePoint(fallbackChar, bold, italic);
    }

    private static SKTypeface? GetInstalledFallbackForTextElement(string textElement, bool bold, bool italic)
    {
        var cacheKey = $"glyph|{textElement}|{bold}|{italic}";
        lock (_typefaceCache)
        {
            if (_typefaceCache.TryGetValue(cacheKey, out var cached)) return cached;

            foreach (var family in _cjkFallbackFamilies)
            {
                var typeface = GetOrCreate(family, bold, italic, requireExactFamily: false);
                if (typeface != null && TypefaceContains(typeface, textElement))
                {
                    _typefaceCache[cacheKey] = typeface;
                    return typeface;
                }
            }

            foreach (var family in SKFontManager.Default.FontFamilies)
            {
                var typeface = GetOrCreate(family, bold, italic, requireExactFamily: true);
                if (typeface != null && TypefaceContains(typeface, textElement))
                {
                    _typefaceCache[cacheKey] = typeface;
                    return typeface;
                }
            }

            return null;
        }
    }

    private static SKTypeface? GetSystemFallbackForCodePoint(int codePoint, bool bold, bool italic)
    {
        var cacheKey = $"match|{codePoint}|{bold}|{italic}";
        lock (_typefaceCache)
        {
            if (_typefaceCache.TryGetValue(cacheKey, out var cached)) return cached;

            var typeface = SKFontManager.Default.MatchCharacter(
                DefaultFont,
                bold ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal,
                SKFontStyleWidth.Normal,
                italic ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright,
                new[] { "zh-CN", "zh-Hans", "zh" },
                codePoint);

            if (typeface == null) typeface = SKFontManager.Default.MatchCharacter(codePoint);
            if (typeface == null) return null;

            _typefaceCache[cacheKey] = typeface;
            return typeface;
        }
    }

    private static SKTypeface? GetOrCreate(string family, bool bold, bool italic, bool requireExactFamily)
    {
        var cacheKey = $"{family.ToLowerInvariant()}|{bold}|{italic}|{requireExactFamily}";
        lock (_typefaceCache)
        {
            if (_typefaceCache.TryGetValue(cacheKey, out var cached)) return cached;

            var tf = SKTypeface.FromFamilyName(family,
                bold ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal,
                SKFontStyleWidth.Normal,
                italic ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright);

            if (tf != null && (!requireExactFamily || NormalizeFamily(tf.FamilyName).Equals(NormalizeFamily(family), StringComparison.OrdinalIgnoreCase)))
            {
                _typefaceCache[cacheKey] = tf;
                return tf;
            }
            tf?.Dispose();
            return null;
        }
    }

    public static bool HasCjk(string text)
    {
        for (var i = 0; i < text.Length; i++)
        {
            var codePoint = char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1])
                ? char.ConvertToUtf32(text[i++], text[i])
                : text[i];
            if (IsCjkCodePoint(codePoint)) return true;
        }
        return false;
    }

    private static bool IsCjk(char c)
    {
        return IsCjkCodePoint(c);
    }

    private static bool IsCjkCodePoint(int codePoint)
    {
        if (codePoint >= 0x2E80 && codePoint <= 0x2EFF) return true;
        if (codePoint >= 0x3000 && codePoint <= 0x303F) return true;
        if (codePoint >= 0x3400 && codePoint <= 0x4DBF) return true;
        if (codePoint >= 0x4E00 && codePoint <= 0x9FFF) return true;
        if (codePoint >= 0xF900 && codePoint <= 0xFAFF) return true;
        if (codePoint >= 0xFF00 && codePoint <= 0xFFEF) return true;
        if (codePoint >= 0x20000 && codePoint <= 0x2A6DF) return true;
        if (codePoint >= 0x2A700 && codePoint <= 0x2B73F) return true;
        if (codePoint >= 0x2B740 && codePoint <= 0x2B81F) return true;
        if (codePoint >= 0x2B820 && codePoint <= 0x2CEAF) return true;
        if (codePoint >= 0x2CEB0 && codePoint <= 0x2EBEF) return true;
        if (codePoint >= 0x30000 && codePoint <= 0x3134F) return true;
        return false;
    }

    private static bool TypefaceContains(SKTypeface typeface, string text)
    {
        using var font = new SKFont(typeface, 12);
        return font.ContainsGlyphs(text);
    }

    private static int GetFirstCodePoint(string text)
    {
        if (string.IsNullOrEmpty(text)) return -1;
        return char.IsHighSurrogate(text[0]) && text.Length > 1 && char.IsLowSurrogate(text[1])
            ? char.ConvertToUtf32(text[0], text[1])
            : text[0];
    }

    private static int GetFirstCjkCodePoint(string text)
    {
        for (var i = 0; i < text.Length; i++)
        {
            var codePoint = char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1])
                ? char.ConvertToUtf32(text[i++], text[i])
                : text[i];
            if (IsCjkCodePoint(codePoint)) return codePoint;
        }
        return -1;
    }

    private static string NormalizeFamily(string family) => family.Replace(" ", "").Replace("-", "").ToLowerInvariant();
}
