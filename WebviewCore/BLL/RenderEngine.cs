using AngleSharp.Dom;
using System.Globalization;
using System.Text.RegularExpressions;
using SkiaSharp;

namespace WebviewCore;

class RenderEngine
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
    private readonly LayoutBox _root;
    private LayoutBox? _focus;

    public RenderEngine(LayoutBox root) { _root = root; }
    public void SetFocus(LayoutBox? b) => _focus = b;

    public void Render(SKCanvas canvas, Size clientSize, float scrollY)
    {
        canvas.Clear(SKColors.White);
        RenderBox(canvas, _root, clientSize, scrollY, parentFixed: false);
    }

    private void RenderBox(SKCanvas canvas, LayoutBox b, Size clientSize, float scrollY, bool parentFixed)
    {
        var s = b.Style;
        if (s != null && s.Visibility == "hidden") return;

        var fixedContext = parentFixed || b.IsFixed;
        var sy = fixedContext ? b.Bounds.Y : b.Bounds.Y - scrollY;
        var r = new SKRect(b.Bounds.X, sy, b.Bounds.X + b.Bounds.Width, sy + b.Bounds.Height);

        var subtree = Inflate(GetSubtreePaintRect(b, scrollY, fixedContext), GetPaintOutset(s), GetPaintOutset(s));
        var viewport = new SKRect(-256, -256, clientSize.Width + 256, clientSize.Height + 256);
        if (!Intersects(subtree, viewport)) return;

        ApplyTransform(canvas, b, s, sy, out var hasTransform);

        SKPaint? layerPaint = null;
        SKImageFilter? imageFilter = null;
        var hasLayer = false;
        float? savedOpacity = null;
        var opacityLayer = s is { Opacity: < 0.999f } && b.Children.Count > 0;
        var filterLayer = s is { FilterBlur: > 0 };

        try
        {
            if (opacityLayer || filterLayer)
            {
                layerPaint = new SKPaint
                {
                    Color = SKColors.White.WithAlpha((byte)(255 * (opacityLayer ? s!.Opacity : 1f))),
                    IsAntialias = true,
                };

                if (filterLayer)
                {
                    var sigma = Math.Max(0.1f, s!.FilterBlur);
                    imageFilter = SKImageFilter.CreateBlur(sigma, sigma);
                    layerPaint.ImageFilter = imageFilter;
                }

                var layerBounds = Inflate(GetSubtreePaintRect(b, scrollY, fixedContext), (s?.FilterBlur ?? 0) * 3 + 16, (s?.FilterBlur ?? 0) * 3 + 16);
                canvas.SaveLayer(layerBounds, layerPaint);
                hasLayer = true;

                if (opacityLayer)
                {
                    savedOpacity = s!.Opacity;
                    s.Opacity = 1f;
                }
            }

            DrawSelf(canvas, b, r, s, sy);

            if (b.Children.Count > 0)
            {
                var clipped = BeginOverflowClip(canvas, r, s);
                foreach (var child in PaintOrderedChildren(b))
                    RenderBox(canvas, child, clientSize, scrollY, fixedContext);
                if (clipped) canvas.Restore();
            }
        }
        finally
        {
            if (savedOpacity.HasValue) s!.Opacity = savedOpacity.Value;
            if (hasLayer) canvas.Restore();
            imageFilter?.Dispose();
            layerPaint?.Dispose();
            if (hasTransform) canvas.Restore();
        }
    }

    private static IEnumerable<LayoutBox> PaintOrderedChildren(LayoutBox b)
    {
        return b.Children
            .Select((child, index) => (child, index))
            .OrderBy(item => PaintRank(item.child))
            .ThenBy(item => item.child.Style?.ZIndex ?? 0)
            .ThenBy(item => item.index)
            .Select(item => item.child);
    }

    private static int PaintRank(LayoutBox b)
    {
        var s = b.Style;
        if (s == null) return 1;
        var positioned = s.Position is "relative" or "absolute" or "fixed" or "sticky";
        if (positioned && s.ZIndex < 0) return 0;
        if (positioned || s.Opacity < 1 || s.Filter != "none") return s.ZIndex > 0 ? 3 : 2;
        return 1;
    }

    private static bool BeginOverflowClip(SKCanvas canvas, SKRect r, BoxStyle? s)
    {
        if (s == null) return false;
        if (!IsClippingOverflow(s.Overflow) && !IsClippingOverflow(s.OverflowX) && !IsClippingOverflow(s.OverflowY)) return false;

        canvas.Save();
        var clip = GetBoxArea(r, s, "padding-box");
        if (HasBorderRadius(s))
        {
            using var path = MakeRoundRectPath(clip, s);
            canvas.ClipPath(path, SKClipOperation.Intersect, true);
        }
        else
        {
            canvas.ClipRect(clip, SKClipOperation.Intersect, true);
        }

        return true;
    }

    private static bool IsClippingOverflow(string value) => value is "hidden" or "clip" or "scroll" or "auto";

    private static SKRect GetSubtreePaintRect(LayoutBox b, float scrollY, bool fixedContext)
    {
        var currentFixed = fixedContext || b.IsFixed;
        var r = GetPaintRect(b, scrollY, currentFixed);
        foreach (var child in b.Children)
            r = Union(r, GetSubtreePaintRect(child, scrollY, currentFixed));
        return r;
    }

    private static SKRect GetPaintRect(LayoutBox b, float scrollY, bool fixedContext)
    {
        var sy = fixedContext ? b.Bounds.Y : b.Bounds.Y - scrollY;
        return new SKRect(b.Bounds.X, sy, b.Bounds.X + b.Bounds.Width, sy + b.Bounds.Height);
    }

    private static float GetPaintOutset(BoxStyle? s)
    {
        if (s == null) return 0;
        return Math.Max(s.ShadowBlur + Math.Abs(s.ShadowX) + Math.Abs(s.ShadowY) + Math.Abs(s.ShadowSpread), s.FilterBlur * 3);
    }

    private static SKRect Union(SKRect a, SKRect b)
    {
        if (a.IsEmpty) return b;
        if (b.IsEmpty) return a;
        return new SKRect(Math.Min(a.Left, b.Left), Math.Min(a.Top, b.Top), Math.Max(a.Right, b.Right), Math.Max(a.Bottom, b.Bottom));
    }

    private static bool Intersects(SKRect a, SKRect b) => a.Right >= b.Left && b.Right >= a.Left && a.Bottom >= b.Top && b.Bottom >= a.Top;

    private static SKRect Inflate(SKRect r, float dx, float dy) => new(r.Left - dx, r.Top - dy, r.Right + dx, r.Bottom + dy);

    private void DrawSelf(SKCanvas canvas, LayoutBox b, SKRect r, BoxStyle? s, float sy)
    {
        if (b.IsImage) { DrawImg(canvas, b, r, s); return; }
        if (b.IsHr) { DrawHr(canvas, b, r); return; }
        if (b.IsInput) { DrawInput(canvas, b, r); return; }

        DrawBox(canvas, b, r, s);
        if (b.Text.Length == 0) return;

        DrawText(canvas, b, r, s, sy);
        DrawTextDecorations(canvas, b, r, s, sy);
    }

    // ===================== TRANSFORM =====================
    private static void ApplyTransform(SKCanvas canvas, LayoutBox b, BoxStyle? s, float sy, out bool hasXf)
    {
        hasXf = s != null && (s.TransformX != 0 || s.TransformY != 0 || s.TransformRotate != 0 ||
                s.TransformScaleX != 1 || s.TransformScaleY != 1 || s.TransformSkewX != 0 || s.TransformSkewY != 0);
        if (!hasXf) return;

        canvas.Save();
        float ox = b.Bounds.Width * s!.TransformOriginX, oy = b.Bounds.Height * s.TransformOriginY;
        canvas.Translate(b.Bounds.X + ox + s.TransformX, sy + oy + s.TransformY);
        if (s.TransformRotate != 0) canvas.RotateDegrees(s.TransformRotate);
        if (s.TransformSkewX != 0 || s.TransformSkewY != 0)
        {
            var m = SKMatrix.CreateSkew(s.TransformSkewX, s.TransformSkewY);
            canvas.Concat(ref m);
        }
        canvas.Scale(s.TransformScaleX, s.TransformScaleY);
        canvas.Translate(-b.Bounds.X - ox, -sy - oy);
    }

    // ===================== PAINT HELPERS =====================
    private static SKPaint MakeFillPaint(Color color, float opacity = 1f)
    {
        var c = ToSKColor(color);
        return new SKPaint { Color = c.WithAlpha(Alpha(c.Alpha, opacity)), Style = SKPaintStyle.Fill, IsAntialias = true };
    }

    private static SKPaint MakeStrokePaint(Color color, float width, float opacity = 1f)
    {
        var c = ToSKColor(color);
        return new SKPaint
        {
            Color = c.WithAlpha(Alpha(c.Alpha, opacity)),
            StrokeWidth = width,
            Style = SKPaintStyle.Stroke,
            IsAntialias = true,
        };
    }

    private static SKPaint MakeTextPaint(Color color, string fontFamily, float fontSize, bool bold, bool italic, float opacity = 1f, string? text = null)
    {
        var c = ToSKColor(color);
        return new SKPaint
        {
            Color = c.WithAlpha(Alpha(c.Alpha, opacity)),
            TextSize = fontSize,
            IsAntialias = true,
            SubpixelText = true,
            Typeface = FontResolver.GetTypeface(fontFamily, bold, italic, text),
            LcdRenderText = true,
        };
    }

    private static SKColor ToSKColor(Color c) => new(c.R, c.G, c.B, c.A);
    private static byte Alpha(byte source, float opacity) => (byte)Math.Clamp(source * Math.Clamp(opacity, 0, 1), 0, 255);

    // ===================== BOX RENDERING =====================
    private static void DrawBox(SKCanvas canvas, LayoutBox b, SKRect r, BoxStyle? s)
    {
        if (s?.ShadowInset != true)
            DrawShadow(canvas, b, r, s);
        DrawBackground(canvas, b, r, s);
        if (s?.ShadowInset == true)
            DrawShadow(canvas, b, r, s);
        DrawBorder(canvas, b, r, s);
        DrawOutline(canvas, b, r, s);
    }

    // ===================== BACKGROUND =====================
    private static void DrawBackground(SKCanvas canvas, LayoutBox b, SKRect r, BoxStyle? s)
    {
        if (s == null && b.BgColor == null) return;

        var bgColor = b.BgColor ?? s?.BackgroundColor;
        var hasGradient = !string.IsNullOrEmpty(s?.BackgroundGradient);
        var hasImage = !string.IsNullOrEmpty(s?.BackgroundImage);
        if (bgColor == null && !hasGradient && !hasImage) return;

        float opacity = s?.Opacity ?? 1f;
        var clipArea = s != null ? GetBoxArea(r, s, s.BackgroundClip) : r;
        var saved = BeginBackgroundClip(canvas, r, clipArea, s);

        if (bgColor != null)
        {
            using var paint = MakeFillPaint(bgColor.Value, opacity);
            canvas.DrawRect(clipArea, paint);
        }

        if (hasGradient && s != null)
            DrawGradient(canvas, clipArea, s, opacity);

        if (hasImage && s != null)
            DrawBackgroundImage(canvas, b, r, s, opacity);

        if (saved) canvas.Restore();
    }

    private static bool BeginBackgroundClip(SKCanvas canvas, SKRect borderBox, SKRect clipArea, BoxStyle? s)
    {
        if (s == null) return false;

        canvas.Save();
        if (HasBorderRadius(s) && SameRect(borderBox, clipArea))
        {
            using var path = MakeRoundRectPath(borderBox, s);
            canvas.ClipPath(path, SKClipOperation.Intersect, true);
        }
        else
        {
            canvas.ClipRect(clipArea, SKClipOperation.Intersect, true);
        }

        return true;
    }

    private static SKRect GetBoxArea(SKRect r, BoxStyle s, string box)
    {
        var left = r.Left;
        var top = r.Top;
        var right = r.Right;
        var bottom = r.Bottom;

        if (box is "padding-box" or "content-box")
        {
            left += s.BorderLeft;
            top += s.BorderTop;
            right -= s.BorderRight;
            bottom -= s.BorderBottom;
        }

        if (box == "content-box")
        {
            left += s.PaddingLeft;
            top += s.PaddingTop;
            right -= s.PaddingRight;
            bottom -= s.PaddingBottom;
        }

        if (right < left) right = left;
        if (bottom < top) bottom = top;
        return new SKRect(left, top, right, bottom);
    }

    private static bool SameRect(SKRect a, SKRect b) =>
        Math.Abs(a.Left - b.Left) < 0.01f && Math.Abs(a.Top - b.Top) < 0.01f &&
        Math.Abs(a.Right - b.Right) < 0.01f && Math.Abs(a.Bottom - b.Bottom) < 0.01f;

    private static void DrawGradient(SKCanvas canvas, SKRect area, BoxStyle s, float opacity)
    {
        var g = s.BackgroundGradient.Trim();
        if (g.Contains("linear-gradient", StringComparison.OrdinalIgnoreCase))
        {
            var inner = GetFunctionInner(g, "linear-gradient");
            if (inner == null) return;

            var parts = SplitCssArgs(inner).ToList();
            if (parts.Count < 2) return;

            var direction = parts[0].Trim().ToLowerInvariant();
            var colorStart = 0;
            var angle = 180f;
            if (direction.StartsWith("to ", StringComparison.Ordinal))
            {
                angle = DirectionToAngle(direction[3..]);
                colorStart = 1;
            }
            else if (TryParseAngle(direction, out var parsedAngle))
            {
                angle = parsedAngle;
                colorStart = 1;
            }

            var colors = new List<SKColor>();
            var stops = new List<float?>();
            for (var i = colorStart; i < parts.Count; i++)
            {
                if (TryParseGradientStop(parts[i], out var color, out var stop))
                {
                    colors.Add(ApplyColorOpacity(color, opacity));
                    stops.Add(stop);
                }
            }

            if (colors.Count < 2) return;
            var resolvedStops = ResolveStops(stops);
            var (start, end) = LinearGradientPoints(area, angle);

            using var shader = SKShader.CreateLinearGradient(start, end, colors.ToArray(), resolvedStops, SKShaderTileMode.Clamp);
            using var paint = new SKPaint { Shader = shader, IsAntialias = true };
            canvas.DrawRect(area, paint);
            return;
        }

        if (g.Contains("radial-gradient", StringComparison.OrdinalIgnoreCase))
        {
            var inner = GetFunctionInner(g, "radial-gradient");
            if (inner == null) return;

            var colors = new List<SKColor>();
            var stops = new List<float?>();
            foreach (var part in SplitCssArgs(inner))
            {
                if (TryParseGradientStop(part, out var color, out var stop))
                {
                    colors.Add(ApplyColorOpacity(color, opacity));
                    stops.Add(stop);
                }
            }

            if (colors.Count < 2) return;
            using var shader = SKShader.CreateRadialGradient(
                new SKPoint(area.MidX, area.MidY),
                Math.Max(area.Width, area.Height) / 2,
                colors.ToArray(),
                ResolveStops(stops),
                SKShaderTileMode.Clamp);
            using var paint = new SKPaint { Shader = shader, IsAntialias = true };
            canvas.DrawRect(area, paint);
        }
    }

    private static void DrawBackgroundImage(SKCanvas canvas, LayoutBox b, SKRect borderBox, BoxStyle s, float opacity)
    {
        var url = ExtractCssUrl(s.BackgroundImage);
        if (string.IsNullOrWhiteSpace(url)) return;

        url = ResolveUrl(b.Source, url);
        var bitmap = GetCachedBitmap(url);
        if (bitmap == null) return;

        var origin = GetBoxArea(borderBox, s, s.BackgroundOrigin);
        if (origin.Width <= 0 || origin.Height <= 0) return;

        var tileSize = ResolveBackgroundSize(s.BackgroundSize, origin.Size, bitmap.Width, bitmap.Height);
        if (tileSize.Width <= 0 || tileSize.Height <= 0) return;

        var available = new SKSize(origin.Width - tileSize.Width, origin.Height - tileSize.Height);
        var offset = ResolveBackgroundPosition(s.BackgroundPosition, available);
        var first = new SKRect(origin.Left + offset.X, origin.Top + offset.Y, origin.Left + offset.X + tileSize.Width, origin.Top + offset.Y + tileSize.Height);

        using var paint = new SKPaint
        {
            FilterQuality = s.ImageRendering == "pixelated" ? SKFilterQuality.None : SKFilterQuality.High,
            IsAntialias = true,
            Color = SKColors.White.WithAlpha((byte)(255 * Math.Clamp(opacity, 0, 1))),
        };

        var repeat = s.BackgroundRepeat.ToLowerInvariant();
        if (repeat == "no-repeat" || tileSize.Width <= 0 || tileSize.Height <= 0)
        {
            canvas.DrawBitmap(bitmap, first, paint);
            return;
        }

        var startX = repeat == "repeat-y" ? first.Left : first.Left;
        var startY = repeat == "repeat-x" ? first.Top : first.Top;
        if (repeat is "repeat" or "repeat-x")
            while (startX > origin.Left) startX -= tileSize.Width;
        if (repeat is "repeat" or "repeat-y")
            while (startY > origin.Top) startY -= tileSize.Height;

        var endX = repeat == "repeat-y" ? first.Left + 1 : origin.Right;
        var endY = repeat == "repeat-x" ? first.Top + 1 : origin.Bottom;

        for (var y = startY; y < endY; y += tileSize.Height)
        {
            for (var x = startX; x < endX; x += tileSize.Width)
            {
                var dest = new SKRect(x, y, x + tileSize.Width, y + tileSize.Height);
                canvas.DrawBitmap(bitmap, dest, paint);
                if (repeat == "repeat-y") break;
            }
            if (repeat == "repeat-x") break;
        }
    }

    private static SKBitmap? GetCachedBitmap(string url)
    {
        if (url.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            var comma = url.IndexOf(',');
            if (comma < 0 || !url[..comma].Contains(";base64", StringComparison.OrdinalIgnoreCase)) return null;
            try
            {
                var data = Convert.FromBase64String(url[(comma + 1)..]);
                return SKBitmap.Decode(data);
            }
            catch { return null; }
        }

        return ImageLoader.GetCached(url);
    }

    private static string? ExtractCssUrl(string value)
    {
        var v = value.Trim();
        if (v.Length == 0 || v == "none") return null;
        var match = Regex.Match(v, @"url\(\s*(?:""([^""]+)""|'([^']+)'|([^)]+))\s*\)", RegexOptions.IgnoreCase);
        if (match.Success)
            return (match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Success ? match.Groups[2].Value : match.Groups[3].Value).Trim();
        return v.Contains("gradient", StringComparison.OrdinalIgnoreCase) ? null : v.Trim('\'', '"');
    }

    internal static string? ResolveBackgroundUrl(IElement? source, string value)
    {
        var url = ExtractCssUrl(value);
        return string.IsNullOrWhiteSpace(url) ? null : ResolveUrl(source, url);
    }

    private static string ResolveUrl(IElement? source, string url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var absolute)) return absolute.ToString();
        var baseUrl = "";
        var cur = source;
        while (cur != null)
        {
            var b = cur.GetAttribute("_base");
            if (b != null) { baseUrl = b; break; }
            cur = cur.ParentElement;
        }

        if (Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri) && Uri.TryCreate(baseUri, url, out var resolved))
            return resolved.ToString();
        return url;
    }

    private static SKSize ResolveBackgroundSize(string value, SKSize area, float imageWidth, float imageHeight)
    {
        var v = (value ?? "auto").Trim().ToLowerInvariant();
        if (v == "cover" || v == "contain")
        {
            var scale = v == "cover"
                ? Math.Max(area.Width / imageWidth, area.Height / imageHeight)
                : Math.Min(area.Width / imageWidth, area.Height / imageHeight);
            return new SKSize(imageWidth * scale, imageHeight * scale);
        }

        if (v == "auto" || v.Length == 0) return new SKSize(imageWidth, imageHeight);

        var parts = v.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var w = parts.Length > 0 ? ParseLengthOrPercent(parts[0], area.Width, imageWidth) : imageWidth;
        var h = parts.Length > 1 ? ParseLengthOrPercent(parts[1], area.Height, imageHeight) : float.NaN;

        if (float.IsNaN(w) && float.IsNaN(h)) return new SKSize(imageWidth, imageHeight);
        if (float.IsNaN(w)) w = h * imageWidth / imageHeight;
        if (float.IsNaN(h)) h = w * imageHeight / imageWidth;
        return new SKSize(w, h);
    }

    private static float ParseLengthOrPercent(string token, float basis, float autoValue)
    {
        token = token.Trim().ToLowerInvariant();
        if (token == "auto") return float.NaN;
        if (token.EndsWith("%", StringComparison.Ordinal) && float.TryParse(token[..^1], NumberStyles.Float, Invariant, out var pc)) return basis * pc / 100f;
        if (token.EndsWith("px", StringComparison.Ordinal) && float.TryParse(token[..^2], NumberStyles.Float, Invariant, out var px)) return px;
        if (float.TryParse(token, NumberStyles.Float, Invariant, out var n)) return n;
        return autoValue;
    }

    private static SKPoint ResolveBackgroundPosition(string value, SKSize available)
    {
        var v = string.IsNullOrWhiteSpace(value) ? "0% 0%" : value.Trim().ToLowerInvariant();
        var parts = v.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 1)
        {
            return parts[0] switch
            {
                "center" => new SKPoint(available.Width / 2, available.Height / 2),
                "right" => new SKPoint(available.Width, 0),
                "bottom" => new SKPoint(0, available.Height),
                _ => new SKPoint(ResolvePositionToken(parts[0], available.Width), 0),
            };
        }

        var xToken = parts.Length > 0 ? parts[0] : "0%";
        var yToken = parts.Length > 1 ? parts[1] : "0%";
        if (xToken is "top" or "bottom")
            (xToken, yToken) = (yToken, xToken);

        return new SKPoint(ResolvePositionToken(xToken, available.Width), ResolvePositionToken(yToken, available.Height));
    }

    private static float ResolvePositionToken(string token, float available)
    {
        token = token.Trim().ToLowerInvariant();
        return token switch
        {
            "left" or "top" => 0,
            "center" => available / 2,
            "right" or "bottom" => available,
            _ when token.EndsWith("%", StringComparison.Ordinal) && float.TryParse(token[..^1], NumberStyles.Float, Invariant, out var pc) => available * pc / 100f,
            _ when token.EndsWith("px", StringComparison.Ordinal) && float.TryParse(token[..^2], NumberStyles.Float, Invariant, out var px) => px,
            _ when float.TryParse(token, NumberStyles.Float, Invariant, out var n) => n,
            _ => 0,
        };
    }

    private static string? GetFunctionInner(string value, string name)
    {
        var start = value.IndexOf(name, StringComparison.OrdinalIgnoreCase);
        if (start < 0) return null;
        start = value.IndexOf('(', start);
        if (start < 0) return null;

        var depth = 0;
        for (var i = start; i < value.Length; i++)
        {
            if (value[i] == '(') depth++;
            else if (value[i] == ')')
            {
                depth--;
                if (depth == 0) return value[(start + 1)..i];
            }
        }

        return null;
    }

    private static IEnumerable<string> SplitCssArgs(string value)
    {
        var start = 0;
        var depth = 0;
        for (var i = 0; i < value.Length; i++)
        {
            var ch = value[i];
            if (ch == '(') depth++;
            else if (ch == ')' && depth > 0) depth--;
            else if (ch == ',' && depth == 0)
            {
                var part = value[start..i].Trim();
                if (part.Length > 0) yield return part;
                start = i + 1;
            }
        }

        var last = value[start..].Trim();
        if (last.Length > 0) yield return last;
    }

    private static bool TryParseGradientStop(string input, out SKColor color, out float? stop)
    {
        color = SKColors.Black;
        stop = null;
        input = input.Trim();
        if (input.Length == 0) return false;

        var colorText = ReadColorToken(input, out var rest);
        if (string.IsNullOrEmpty(colorText)) return false;

        color = ParseGradientColor(colorText);
        var match = Regex.Match(rest, @"([-+]?\d*\.?\d+)%");
        if (match.Success && float.TryParse(match.Groups[1].Value, NumberStyles.Float, Invariant, out var pc))
            stop = Math.Clamp(pc / 100f, 0, 1);
        return true;
    }

    private static string ReadColorToken(string value, out string rest)
    {
        value = value.Trim();
        rest = "";
        if (value.StartsWith("#", StringComparison.Ordinal))
        {
            var i = 1;
            while (i < value.Length && Uri.IsHexDigit(value[i])) i++;
            rest = value[i..].Trim();
            return value[..i];
        }

        var func = Regex.Match(value, @"^(rgba?|hsla?)\s*\(", RegexOptions.IgnoreCase);
        if (func.Success)
        {
            var depth = 0;
            for (var i = 0; i < value.Length; i++)
            {
                if (value[i] == '(') depth++;
                else if (value[i] == ')')
                {
                    depth--;
                    if (depth == 0)
                    {
                        rest = value[(i + 1)..].Trim();
                        return value[..(i + 1)];
                    }
                }
            }
        }

        var name = Regex.Match(value, @"^[a-zA-Z]+");
        if (!name.Success) return "";
        rest = value[name.Length..].Trim();
        return name.Value;
    }

    private static float[] ResolveStops(List<float?> stops)
    {
        var n = stops.Count;
        var result = new float[n];
        for (var i = 0; i < n; i++)
            result[i] = stops[i] ?? (n == 1 ? 0 : i / (float)(n - 1));

        result[0] = stops[0] ?? 0;
        result[^1] = stops[^1] ?? 1;

        for (var i = 1; i < n; i++)
            if (result[i] < result[i - 1]) result[i] = result[i - 1];

        return result;
    }

    private static SKColor ApplyColorOpacity(SKColor color, float opacity) => color.WithAlpha(Alpha(color.Alpha, opacity));

    private static (SKPoint start, SKPoint end) LinearGradientPoints(SKRect r, float cssAngle)
    {
        var rad = cssAngle * MathF.PI / 180f;
        var dx = MathF.Sin(rad);
        var dy = -MathF.Cos(rad);
        var half = Math.Abs(dx) * r.Width / 2f + Math.Abs(dy) * r.Height / 2f;
        var center = new SKPoint(r.MidX, r.MidY);
        return (
            new SKPoint(center.X - dx * half, center.Y - dy * half),
            new SKPoint(center.X + dx * half, center.Y + dy * half));
    }

    private static float DirectionToAngle(string direction)
    {
        direction = direction.Trim().ToLowerInvariant();
        return direction switch
        {
            "top" => 0,
            "right" => 90,
            "bottom" => 180,
            "left" => 270,
            "top right" or "right top" => 45,
            "bottom right" or "right bottom" => 135,
            "bottom left" or "left bottom" => 225,
            "top left" or "left top" => 315,
            _ => 180,
        };
    }

    private static bool TryParseAngle(string value, out float angle)
    {
        angle = 0;
        value = value.Trim().ToLowerInvariant();
        if (value.EndsWith("deg", StringComparison.Ordinal) && float.TryParse(value[..^3], NumberStyles.Float, Invariant, out var deg))
        {
            angle = deg;
            return true;
        }

        if (value.EndsWith("rad", StringComparison.Ordinal) && float.TryParse(value[..^3], NumberStyles.Float, Invariant, out var rad))
        {
            angle = rad * 180f / MathF.PI;
            return true;
        }

        if (value.EndsWith("turn", StringComparison.Ordinal) && float.TryParse(value[..^4], NumberStyles.Float, Invariant, out var turn))
        {
            angle = turn * 360f;
            return true;
        }

        return false;
    }

    private static SKColor ParseGradientColor(string v)
    {
        v = v.Trim().TrimEnd(',').ToLowerInvariant();
        if (v.StartsWith('#'))
        {
            var h = v[1..];
            if (h.Length == 3) return new SKColor((byte)(Conv(h[0..1]) * 17), (byte)(Conv(h[1..2]) * 17), (byte)(Conv(h[2..3]) * 17));
            if (h.Length == 4) return new SKColor((byte)(Conv(h[0..1]) * 17), (byte)(Conv(h[1..2]) * 17), (byte)(Conv(h[2..3]) * 17), (byte)(Conv(h[3..4]) * 17));
            if (h.Length == 6) return new SKColor(Conv(h[0..2]), Conv(h[2..4]), Conv(h[4..6]));
            if (h.Length == 8) return new SKColor(Conv(h[0..2]), Conv(h[2..4]), Conv(h[4..6]), Conv(h[6..8]));
        }

        var mr = Regex.Match(v, @"rgba?\s*\(\s*([\d.]+%?)\s*,\s*([\d.]+%?)\s*,\s*([\d.]+%?)\s*(?:,\s*([\d.]+%?)\s*)?\)");
        if (mr.Success)
            return new SKColor(ParseRgbComponent(mr.Groups[1].Value), ParseRgbComponent(mr.Groups[2].Value), ParseRgbComponent(mr.Groups[3].Value), mr.Groups[4].Success ? ParseAlpha(mr.Groups[4].Value) : (byte)255);

        var mh = Regex.Match(v, @"hsla?\s*\(\s*([\d.]+)\s*,\s*([\d.]+)%\s*,\s*([\d.]+)%\s*(?:,\s*([\d.]+%?)\s*)?\)");
        if (mh.Success)
        {
            var h = float.Parse(mh.Groups[1].Value, Invariant);
            var s = float.Parse(mh.Groups[2].Value, Invariant) / 100f;
            var l = float.Parse(mh.Groups[3].Value, Invariant) / 100f;
            return Hsl(h, s, l, mh.Groups[4].Success ? ParseAlpha(mh.Groups[4].Value) : (byte)255);
        }

        return _namedColors.TryGetValue(v, out var nc) ? nc : new SKColor(0, 0, 0);

        static byte Conv(string h) => Convert.ToByte(h, 16);
    }

    private static byte ParseRgbComponent(string value)
    {
        value = value.Trim();
        if (value.EndsWith("%", StringComparison.Ordinal) && float.TryParse(value[..^1], NumberStyles.Float, Invariant, out var pct))
            return (byte)Math.Clamp(pct * 2.55f, 0, 255);
        return byte.TryParse(value, NumberStyles.Float, Invariant, out var b) ? b : (byte)0;
    }

    private static byte ParseAlpha(string value)
    {
        value = value.Trim();
        if (value.EndsWith("%", StringComparison.Ordinal) && float.TryParse(value[..^1], NumberStyles.Float, Invariant, out var pct))
            return (byte)Math.Clamp(pct * 2.55f, 0, 255);
        if (float.TryParse(value, NumberStyles.Float, Invariant, out var a))
            return (byte)Math.Clamp(a <= 1 ? a * 255 : a, 0, 255);
        return 255;
    }

    private static SKColor Hsl(float h, float s, float l, byte a = 255)
    {
        h = ((h % 360) + 360) % 360 / 360f;
        float HueToRgb(float p, float q, float t)
        {
            if (t < 0) t += 1;
            if (t > 1) t -= 1;
            if (t < 1f / 6) return p + (q - p) * 6 * t;
            if (t < 1f / 2) return q;
            if (t < 2f / 3) return p + (q - p) * (2f / 3 - t) * 6;
            return p;
        }

        if (s == 0)
        {
            var gray = (byte)(l * 255);
            return new SKColor(gray, gray, gray, a);
        }

        var q = l < 0.5f ? l * (1 + s) : l + s - l * s;
        var p = 2 * l - q;
        return new SKColor(
            (byte)(HueToRgb(p, q, h + 1f / 3) * 255),
            (byte)(HueToRgb(p, q, h) * 255),
            (byte)(HueToRgb(p, q, h - 1f / 3) * 255),
            a);
    }

    private static readonly Dictionary<string, SKColor> _namedColors = new(StringComparer.OrdinalIgnoreCase)
    {
        ["black"]=new SKColor(0,0,0),["white"]=new SKColor(255,255,255),["red"]=new SKColor(255,0,0),
        ["blue"]=new SKColor(0,0,255),["green"]=new SKColor(0,128,0),["yellow"]=new SKColor(255,255,0),
        ["gray"]=new SKColor(128,128,128),["grey"]=new SKColor(128,128,128),["orange"]=new SKColor(255,165,0),
        ["purple"]=new SKColor(128,0,128),["pink"]=new SKColor(255,192,203),["brown"]=new SKColor(165,42,42),
        ["navy"]=new SKColor(0,0,128),["teal"]=new SKColor(0,128,128),["silver"]=new SKColor(192,192,192),
        ["transparent"]=new SKColor(0,0,0,0),["aqua"]=new SKColor(0,255,255),["fuchsia"]=new SKColor(255,0,255),
        ["lime"]=new SKColor(0,255,0),["olive"]=new SKColor(128,128,0),["darkgray"]=new SKColor(169,169,169),
        ["darkgrey"]=new SKColor(169,169,169),["lightgray"]=new SKColor(211,211,211),["lightgrey"]=new SKColor(211,211,211),
        ["gold"]=new SKColor(255,215,0),["violet"]=new SKColor(238,130,238),["coral"]=new SKColor(255,127,80),
        ["tomato"]=new SKColor(255,99,71),["crimson"]=new SKColor(220,20,60),["indigo"]=new SKColor(75,0,130),
    };

    // ===================== BORDER RADIUS =====================
    private static void ApplyRoundRect(SKCanvas canvas, SKRect r, BoxStyle? s, SKPaint paint)
    {
        if (s == null || !HasBorderRadius(s))
        { canvas.DrawRect(r, paint); return; }
        using var path = MakeRoundRectPath(r, s);
        canvas.DrawPath(path, paint);
    }

    private static void DrawBorderRoundRect(SKCanvas canvas, SKRect r, BoxStyle s, SKPaint paint)
    {
        using var path = MakeRoundRectPath(r, s);
        canvas.DrawPath(path, paint);
    }

    private static SKPath MakeRoundRectPath(SKRect r, BoxStyle s)
    {
        var tl = ResolveCornerRadius(r, s.BorderTopLeftRadius, s.BorderTopLeftRadiusIsPercent, s.BorderRadius, s.BorderRadiusIsPercent);
        var tr = ResolveCornerRadius(r, s.BorderTopRightRadius, s.BorderTopRightRadiusIsPercent, s.BorderRadius, s.BorderRadiusIsPercent);
        var bl = ResolveCornerRadius(r, s.BorderBottomLeftRadius, s.BorderBottomLeftRadiusIsPercent, s.BorderRadius, s.BorderRadiusIsPercent);
        var br = ResolveCornerRadius(r, s.BorderBottomRightRadius, s.BorderBottomRightRadiusIsPercent, s.BorderRadius, s.BorderRadiusIsPercent);
        var path = new SKPath();
        path.MoveTo(r.Left + tl, r.Top);
        path.LineTo(r.Right - tr, r.Top);
        path.QuadTo(r.Right, r.Top, r.Right, r.Top + tr);
        path.LineTo(r.Right, r.Bottom - br);
        path.QuadTo(r.Right, r.Bottom, r.Right - br, r.Bottom);
        path.LineTo(r.Left + bl, r.Bottom);
        path.QuadTo(r.Left, r.Bottom, r.Left, r.Bottom - bl);
        path.LineTo(r.Left, r.Top + tl);
        path.QuadTo(r.Left, r.Top, r.Left + tl, r.Top);
        path.Close();
        return path;
    }

    private static bool HasBorderRadius(BoxStyle s) =>
        s.BorderRadius > 0 || s.BorderTopLeftRadius > 0 || s.BorderTopRightRadius > 0 ||
        s.BorderBottomLeftRadius > 0 || s.BorderBottomRightRadius > 0;

    private static float ResolveCornerRadius(SKRect r, float value, bool isPercent, float fallback, bool fallbackIsPercent)
    {
        var raw = value > 0 ? value : fallback;
        var percent = value > 0 ? isPercent : fallbackIsPercent;
        var px = percent ? raw * Math.Min(r.Width, r.Height) : raw;
        return Math.Min(px, Math.Min(r.Width / 2, r.Height / 2));
    }

    private static void DrawRoundRectByStyle(SKCanvas canvas, SKRect r, BoxStyle? s, SKPaint paint)
    {
        if (s == null || !HasBorderRadius(s))
        { canvas.DrawRect(r, paint); return; }
        using var path = MakeRoundRectPath(r, s);
        canvas.DrawPath(path, paint);
    }

    // ===================== BOX SHADOW =====================
    private static void DrawShadow(SKCanvas canvas, LayoutBox b, SKRect r, BoxStyle? s)
    {
        if (s == null) return;
        if (s.ShadowBlur <= 0 && s.ShadowSpread == 0 && s.ShadowX == 0 && s.ShadowY == 0) return;

        var alpha = s.ShadowColor.A > 0 ? s.ShadowColor.A : (byte)80;
        using var paint = new SKPaint
        {
            Color = new SKColor(s.ShadowColor.R, s.ShadowColor.G, s.ShadowColor.B, Alpha(alpha, s.Opacity)),
            Style = SKPaintStyle.Fill,
            IsAntialias = true,
        };
        if (s.ShadowBlur > 0)
            paint.MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, Math.Max(0.1f, s.ShadowBlur / 2f));

        if (s.ShadowInset)
        {
            canvas.Save();
            if (HasBorderRadius(s))
            {
                using var path = MakeRoundRectPath(r, s);
                canvas.ClipPath(path, SKClipOperation.Intersect, true);
            }
            else canvas.ClipRect(r, SKClipOperation.Intersect, true);

            var extent = Math.Max(r.Width, r.Height) + Math.Abs(s.ShadowX) + Math.Abs(s.ShadowY) + s.ShadowBlur * 4 + Math.Abs(s.ShadowSpread) + 32;
            using var insetPath = new SKPath { FillType = SKPathFillType.EvenOdd };
            insetPath.AddRect(new SKRect(r.Left - extent, r.Top - extent, r.Right + extent, r.Bottom + extent));
            var inner = new SKRect(
                r.Left + s.ShadowX + s.ShadowSpread,
                r.Top + s.ShadowY + s.ShadowSpread,
                r.Right + s.ShadowX - s.ShadowSpread,
                r.Bottom + s.ShadowY - s.ShadowSpread);
            if (inner.Right < inner.Left) (inner.Left, inner.Right) = (inner.Right, inner.Left);
            if (inner.Bottom < inner.Top) (inner.Top, inner.Bottom) = (inner.Bottom, inner.Top);
            using var innerPath = MakeRoundRectPath(inner, s);
            insetPath.AddPath(innerPath);
            canvas.DrawPath(insetPath, paint);
            canvas.Restore();
            return;
        }

        var shadowR = new SKRect(r.Left + s.ShadowX, r.Top + s.ShadowY, r.Right + s.ShadowX, r.Bottom + s.ShadowY);
        if (s.ShadowSpread != 0)
            shadowR = new SKRect(shadowR.Left - s.ShadowSpread, shadowR.Top - s.ShadowSpread, shadowR.Right + s.ShadowSpread, shadowR.Bottom + s.ShadowSpread);

        DrawRoundRectByStyle(canvas, shadowR, s, paint);
    }

    // ===================== BORDER =====================
    private static void DrawBorder(SKCanvas canvas, LayoutBox b, SKRect r, BoxStyle? s)
    {
        if (s == null) return;
        if (s.BorderTop <= 0 && s.BorderBottom <= 0 && s.BorderLeft <= 0 && s.BorderRight <= 0) return;

        float opacity = s.Opacity;

        if (HasBorderRadius(s))
        {
            using var paint = new SKPaint
            {
                StrokeWidth = Math.Max(Math.Max(s.BorderTop, s.BorderBottom), Math.Max(s.BorderLeft, s.BorderRight)),
                Style = SKPaintStyle.Stroke,
                IsAntialias = true,
                Color = ToSKColor(s.BorderColor).WithAlpha(Alpha(s.BorderColor.A, opacity)),
            };
            SetupDash(paint, s.BorderTopStyle);
            DrawBorderRoundRect(canvas, r, s, paint);
            return;
        }

        DrawBorderSide(canvas, r.Left, r.Top, r.Right, r.Top, s.BorderTop, s.BorderTopStyle, s.BorderTopColor, s.BorderColor, opacity);
        DrawBorderSide(canvas, r.Left, r.Bottom, r.Right, r.Bottom, s.BorderBottom, s.BorderBottomStyle, s.BorderBottomColor, s.BorderColor, opacity);
        DrawBorderSide(canvas, r.Left, r.Top, r.Left, r.Bottom, s.BorderLeft, s.BorderLeftStyle, s.BorderLeftColor, s.BorderColor, opacity);
        DrawBorderSide(canvas, r.Right, r.Top, r.Right, r.Bottom, s.BorderRight, s.BorderRightStyle, s.BorderRightColor, s.BorderColor, opacity);
    }

    private static void DrawBorderSide(SKCanvas canvas, float x1, float y1, float x2, float y2, float width, string style, Color sideColor, Color fallback, float opacity)
    {
        if (width <= 0 || style == "none") return;
        var color = sideColor.A > 0 ? sideColor : fallback;

        if (style == "double" && width >= 3)
        {
            using var p = MakeStrokePaint(color, Math.Max(1, width / 3f), opacity);
            canvas.DrawLine(x1, y1, x2, y2, p);
            if (Math.Abs(y1 - y2) < 0.01f) canvas.DrawLine(x1, y1 + Math.Sign(y1 == 0 ? 1 : y1) * width * 0.66f, x2, y2 + Math.Sign(y2 == 0 ? 1 : y2) * width * 0.66f, p);
            else canvas.DrawLine(x1 + Math.Sign(x1 == 0 ? 1 : x1) * width * 0.66f, y1, x2 + Math.Sign(x2 == 0 ? 1 : x2) * width * 0.66f, y2, p);
            return;
        }

        using var paint = MakeStrokePaint(color, width, opacity);
        SetupDash(paint, style);
        canvas.DrawLine(x1, y1, x2, y2, paint);
    }

    private static void SetupDash(SKPaint paint, string style)
    {
        if (style == "dashed") paint.PathEffect = SKPathEffect.CreateDash(new float[] { 5, 3 }, 0);
        else if (style == "dotted") paint.PathEffect = SKPathEffect.CreateDash(new float[] { 1, 2 }, 0);
    }

    // ===================== OUTLINE =====================
    private static void DrawOutline(SKCanvas canvas, LayoutBox b, SKRect r, BoxStyle? s)
    {
        if (s == null || s.OutlineWidth <= 0 || s.OutlineStyle == "none") return;
        var off = s.OutlineOffset;
        var or = new SKRect(r.Left - off, r.Top - off, r.Right + off, r.Bottom + off);
        using var paint = MakeStrokePaint(s.OutlineColor, s.OutlineWidth, s.Opacity);
        SetupDash(paint, s.OutlineStyle);
        DrawRoundRectByStyle(canvas, or, s, paint);
    }

    // ===================== TEXT =====================
    private static float GetBaselineY(float top, string fn, float fs, bool bold, bool italic)
    {
        using var p = MakeTextPaint(Color.Black, fn, fs, bold, italic, 1f);
        p.GetFontMetrics(out var fm);
        return top - fm.Ascent;
    }

    private static void DrawText(SKCanvas canvas, LayoutBox b, SKRect r, BoxStyle? s, float sy)
    {
        var fn = b.FontName.Length > 0 ? b.FontName : "Segoe UI";
        float opacity = s?.Opacity ?? 1f;
        var text = ApplyTextTransform(b.Text, s?.TextTransform);
        var baseLine = GetBaselineY(sy, fn, b.FontSize, b.Bold, b.Italic);
        var letterSpacing = s?.LetterSpacing ?? 0;
        var wordSpacing = s?.WordSpacing ?? 0;

        if (s != null && s.TextShadowBlur > 0)
        {
            var sc = s.TextShadowColor.A > 0
                ? Color.FromArgb(s.TextShadowColor.A, s.TextShadowColor.R, s.TextShadowColor.G, s.TextShadowColor.B)
                : Color.FromArgb(128, 0, 0, 0);
            using var sp = MakeTextPaint(sc, fn, b.FontSize, b.Bold, b.Italic, opacity, text);
            sp.MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, s.TextShadowBlur);
            DrawTextRun(canvas, text, r.Left + s.TextShadowX, baseLine + s.TextShadowY, sp, letterSpacing, wordSpacing, fn, b.Bold, b.Italic);
        }

        using var paint = MakeTextPaint(b.Color, fn, b.FontSize, b.Bold, b.Italic, opacity, text);
        DrawTextRun(canvas, text, r.Left, baseLine, paint, letterSpacing, wordSpacing, fn, b.Bold, b.Italic);
    }

    private static void DrawTextRun(SKCanvas canvas, string text, float x, float y, SKPaint paint, float letterSpacing, float wordSpacing, string fontFamily, bool bold, bool italic)
    {
        var pen = x;
        var originalTypeface = paint.Typeface;
        var elements = EnumerateTextElements(text).ToArray();
        for (var i = 0; i < elements.Length; i++)
        {
            var element = elements[i];
            paint.Typeface = FontResolver.GetTypefaceForTextElement(fontFamily, bold, italic, element, originalTypeface);
            canvas.DrawText(element, pen, y, paint);
            pen += paint.MeasureText(element);
            if (i < elements.Length - 1) pen += letterSpacing;
            if (element.All(char.IsWhiteSpace)) pen += wordSpacing;
        }
        paint.Typeface = originalTypeface;
    }

    private static float MeasureTextRun(string text, SKPaint paint, float letterSpacing, float wordSpacing, string fontFamily, bool bold, bool italic)
    {
        var width = 0f;
        var originalTypeface = paint.Typeface;
        var elements = EnumerateTextElements(text).ToArray();
        for (var i = 0; i < elements.Length; i++)
        {
            var element = elements[i];
            paint.Typeface = FontResolver.GetTypefaceForTextElement(fontFamily, bold, italic, element, originalTypeface);
            width += paint.MeasureText(element);
            if (i < elements.Length - 1) width += letterSpacing;
            if (element.All(char.IsWhiteSpace)) width += wordSpacing;
        }
        paint.Typeface = originalTypeface;
        return width;
    }

    private static IEnumerable<string> EnumerateTextElements(string text)
    {
        var enumerator = StringInfo.GetTextElementEnumerator(text);
        while (enumerator.MoveNext())
            yield return enumerator.GetTextElement();
    }

    private static string ApplyTextTransform(string text, string? transform)
    {
        return (transform ?? "none").ToLowerInvariant() switch
        {
            "uppercase" => text.ToUpperInvariant(),
            "lowercase" => text.ToLowerInvariant(),
            "capitalize" => Regex.Replace(text.ToLowerInvariant(), @"\b\p{Ll}", m => m.Value.ToUpperInvariant()),
            _ => text,
        };
    }

    private static void DrawTextDecorations(SKCanvas canvas, LayoutBox b, SKRect r, BoxStyle? s, float sy)
    {
        if (s == null) return;
        if (!b.IsLink && !s.Underline && !s.Overline && !s.LineThrough) return;

        var decoColor = s.TextDecorationColor.A > 0 ? s.TextDecorationColor : b.Color;
        float opacity = s.Opacity;
        float thickness = Math.Max(1, s.TextDecorationThickness > 0 ? s.TextDecorationThickness : 1);
        var text = ApplyTextTransform(b.Text, s.TextTransform);
        var fn = b.FontName.Length > 0 ? b.FontName : "Segoe UI";
        var baseLine = GetBaselineY(sy, fn, b.FontSize, b.Bold, b.Italic);
        using var textPaint = MakeTextPaint(b.Color, fn, b.FontSize, b.Bold, b.Italic, opacity, text);
        var width = MeasureTextRun(text, textPaint, s.LetterSpacing, s.WordSpacing, fn, b.Bold, b.Italic);
        using var p = MakeStrokePaint(decoColor, thickness, opacity);
        SetupDash(p, s.TextDecorationStyle);

        if (b.IsLink || s.Underline)
            canvas.DrawLine(r.Left, baseLine + thickness + 1, r.Left + width, baseLine + thickness + 1, p);
        if (s.Overline)
            canvas.DrawLine(r.Left, sy, r.Left + width, sy, p);
        if (s.LineThrough)
        {
            var midY = (sy + baseLine) / 2;
            canvas.DrawLine(r.Left, midY, r.Left + width, midY, p);
        }
    }

    // ===================== IMAGE =====================
    private static void DrawImg(SKCanvas canvas, LayoutBox b, SKRect r, BoxStyle? s)
    {
        DrawBox(canvas, b, r, s);

        float opacity = s?.Opacity ?? 1f;
        if (b.ImageData != null)
        {
            var fit = s?.ObjectFit ?? "fill";
            var srcW = b.ImageData.Width;
            var srcH = b.ImageData.Height;
            var destR = r;

            if (fit == "cover")
            {
                var scale = Math.Max(r.Width / srcW, r.Height / srcH);
                var sw = srcW * scale; var sh = srcH * scale;
                destR = new SKRect(r.Left - (sw - r.Width) / 2, r.Top - (sh - r.Height) / 2, r.Left + (sw + r.Width) / 2, r.Top + (sh + r.Height) / 2);
            }
            else if (fit == "contain")
            {
                var scale = Math.Min(r.Width / srcW, r.Height / srcH);
                var sw = srcW * scale; var sh = srcH * scale;
                destR = new SKRect(r.MidX - sw / 2, r.MidY - sh / 2, r.MidX + sw / 2, r.MidY + sh / 2);
            }
            else if (fit == "scale-down")
            {
                var scale = Math.Min(1f, Math.Min(r.Width / srcW, r.Height / srcH));
                var sw = srcW * scale; var sh = srcH * scale;
                destR = new SKRect(r.MidX - sw / 2, r.MidY - sh / 2, r.MidX + sw / 2, r.MidY + sh / 2);
            }

            if (s != null && HasBorderRadius(s))
            {
                canvas.Save();
                using var clipPath = MakeRoundRectPath(r, s);
                canvas.ClipPath(clipPath, SKClipOperation.Intersect, true);
            }

            using var paint = new SKPaint
            {
                FilterQuality = s?.ImageRendering == "pixelated" ? SKFilterQuality.None : SKFilterQuality.High,
                IsAntialias = true,
                Color = SKColors.White.WithAlpha((byte)(255 * Math.Clamp(opacity, 0, 1))),
            };
            canvas.DrawBitmap(b.ImageData, destR, paint);

            if (s != null && HasBorderRadius(s))
                canvas.Restore();
        }
        else
        {
            var lbl = b.ImageUrl != null ? Path.GetFileName(b.ImageUrl) : "img";
            var fn = s?.FontFamily ?? "Segoe UI";
            using var tp = MakeTextPaint(Color.Gray, fn, 9, false, false, opacity, lbl);
            var tw = tp.MeasureText(lbl);
            tp.GetFontMetrics(out var fm);
            DrawTextRun(canvas, lbl, r.MidX - tw / 2, r.MidY - fm.Ascent / 2, tp, 0, 0, fn, false, false);
        }
    }

    // ===================== HR =====================
    private static void DrawHr(SKCanvas canvas, LayoutBox b, SKRect r)
    {
        using var paint = new SKPaint
        {
            Color = ToSKColor(b.Style?.BorderColor ?? Color.Gray),
            StrokeWidth = Math.Max(1, b.Bounds.Height),
            Style = SKPaintStyle.Stroke,
            IsAntialias = true,
        };
        canvas.DrawLine(r.Left, r.MidY, r.Right, r.MidY, paint);
    }

    // ===================== INPUTS =====================
    private void DrawInput(SKCanvas canvas, LayoutBox b, SKRect r)
    {
        var t = b.InputType ?? "text";
        var focused = _focus == b;
        var s = b.Style;
        float opacity = s?.Opacity ?? 1f;

        if (t is "checkbox" or "radio")
        {
            if (t == "radio")
            {
                using var bg = new SKPaint { Color = SKColors.White, Style = SKPaintStyle.Fill, IsAntialias = true };
                canvas.DrawOval(r.MidX, r.MidY, r.Width / 2, r.Height / 2, bg);
                using var pn = new SKPaint
                { Color = focused ? SKColors.Blue : new SKColor(80, 80, 80), StrokeWidth = focused ? 2 : 1, Style = SKPaintStyle.Stroke, IsAntialias = true };
                canvas.DrawOval(r.MidX, r.MidY, r.Width / 2, r.Height / 2, pn);
                if (b.InputChecked)
                {
                    using var dot = new SKPaint { Color = SKColors.Blue, Style = SKPaintStyle.Fill, IsAntialias = true };
                    var pad = 4;
                    canvas.DrawOval(r.MidX, r.MidY, (r.Width - pad * 2) / 2, (r.Height - pad * 2) / 2, dot);
                }
            }
            else
            {
                using var bg = new SKPaint { Color = SKColors.White, Style = SKPaintStyle.Fill };
                canvas.DrawRect(r, bg);
                using var pn = new SKPaint
                { Color = focused ? SKColors.Blue : new SKColor(80, 80, 80), StrokeWidth = focused ? 2 : 1, Style = SKPaintStyle.Stroke };
                canvas.DrawRect(r, pn);
                if (b.InputChecked)
                {
                    using var ck = new SKPaint { Color = SKColors.Blue, StrokeWidth = 2, Style = SKPaintStyle.Stroke, IsAntialias = true, StrokeCap = SKStrokeCap.Round };
                    var path = new SKPath();
                    path.MoveTo(r.Left + 2, r.MidY);
                    path.LineTo(r.Left + 5, r.Bottom - 3);
                    path.LineTo(r.Right - 2, r.Top + 3);
                    canvas.DrawPath(path, ck);
                }
            }
            return;
        }

        if (t is "submit" or "button" || b.InputType == null)
        {
            var bgColor = b.BgColor ?? s?.BackgroundColor ?? Color.FromArgb(225, 225, 225);
            using var bg = new SKPaint { Color = ToSKColor(bgColor).WithAlpha(Alpha(bgColor.A, opacity)), Style = SKPaintStyle.Fill };
            canvas.DrawRect(r, bg);
            using var pn = new SKPaint { Color = focused ? SKColors.Blue : new SKColor(120, 120, 120), StrokeWidth = focused ? 2 : 1, Style = SKPaintStyle.Stroke };
            canvas.DrawRect(r, pn);

            var lbl = (b.InputValue ?? "Button").Length > 0 ? b.InputValue! : "Submit";
            var fn = b.FontName ?? s?.FontFamily ?? "Segoe UI";
            var fs = b.FontSize > 0 ? b.FontSize : (s?.FontSize > 0 ? s.FontSize : 12);
            var fg = b.Color.A > 0 ? b.Color : (s?.Color ?? Color.Black);
            lbl = ApplyTextTransform(lbl, s?.TextTransform);
            using var tp = MakeTextPaint(fg, fn, fs, b.Bold || (s?.Bold ?? false), b.Italic || (s?.Italic ?? false), opacity, lbl);
            var tw = tp.MeasureText(lbl);
            tp.GetFontMetrics(out var tfm);
            DrawTextRun(canvas, lbl, r.MidX - tw / 2, r.MidY - tfm.Ascent / 2, tp, 0, 0, fn, b.Bold || (s?.Bold ?? false), b.Italic || (s?.Italic ?? false));
            return;
        }

        var bgColor2 = Color.White;
        using var bg2 = new SKPaint { Color = ToSKColor(bgColor2), Style = SKPaintStyle.Fill };
        canvas.DrawRect(r, bg2);
        using var pn2 = new SKPaint { Color = focused ? SKColors.Blue : new SKColor(140, 140, 140), StrokeWidth = focused ? 2 : 1, Style = SKPaintStyle.Stroke };
        canvas.DrawRect(r, pn2);

        var txt = b.Text.Length > 0 ? b.Text : (b.InputValue ?? "");
        txt = ApplyTextTransform(txt, s?.TextTransform);
        if (txt.Length > 0)
        {
            var fn2 = b.FontName ?? s?.FontFamily ?? "Segoe UI";
            var fs2 = b.FontSize > 0 ? b.FontSize : (s?.FontSize > 0 ? s.FontSize : 12);
            var fg2 = b.Color.A > 0 ? b.Color : (s?.Color ?? Color.Black);
            using var tp2 = MakeTextPaint(fg2, fn2, fs2, b.Bold || (s?.Bold ?? false), b.Italic || (s?.Italic ?? false), opacity, txt);
            tp2.GetFontMetrics(out var fm2);
            DrawTextRun(canvas, txt, r.Left + 2, r.Top + 2 - fm2.Ascent, tp2, 0, 0, fn2, b.Bold || (s?.Bold ?? false), b.Italic || (s?.Italic ?? false));
        }
        if (focused)
        {
            using var tp3 = MakeTextPaint(Color.Black, "Segoe UI", 12, false, false, 1f, txt);
            var tw3 = txt.Length > 0 ? tp3.MeasureText(txt) : 0;
            using var cp = new SKPaint { Color = SKColors.Black, StrokeWidth = 1, Style = SKPaintStyle.Stroke };
            canvas.DrawLine(r.Left + 2 + tw3, r.Top + 2, r.Left + 2 + tw3, r.Bottom - 2, cp);
        }
    }
}
