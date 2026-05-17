using SkiaSharp;
using System.Globalization;

namespace WebviewCore;

static class TextMeasurer
{
    private static readonly SKPaint _paint = new SKPaint { IsAntialias = true, SubpixelText = true };

    public static float MeasureWidth(string text, string fontFamily, float fontSize, bool bold = false, bool italic = false)
    {
        lock (_paint)
        {
            _paint.TextSize = fontSize;
            _paint.Typeface = FontResolver.GetTypeface(fontFamily, bold, italic, text);
            return MeasureTextRun(text, fontFamily, bold, italic);
        }
    }

    public static (float width, float height) Measure(string text, string fontFamily, float fontSize, bool bold = false, bool italic = false)
    {
        lock (_paint)
        {
            _paint.TextSize = fontSize;
            _paint.Typeface = FontResolver.GetTypeface(fontFamily, bold, italic, text);
            var w = MeasureTextRun(text, fontFamily, bold, italic);
            _paint.GetFontMetrics(out var fm);
            return (w, fm.Descent - fm.Ascent);
        }
    }

    private static float MeasureTextRun(string text, string fontFamily, bool bold, bool italic)
    {
        var width = 0f;
        var originalTypeface = _paint.Typeface;
        foreach (var element in EnumerateTextElements(text))
        {
            _paint.Typeface = FontResolver.GetTypefaceForTextElement(fontFamily, bold, italic, element, originalTypeface);
            width += _paint.MeasureText(element);
        }
        _paint.Typeface = originalTypeface;
        return width;
    }

    private static IEnumerable<string> EnumerateTextElements(string text)
    {
        var enumerator = StringInfo.GetTextElementEnumerator(text);
        while (enumerator.MoveNext())
            yield return enumerator.GetTextElement();
    }
}
