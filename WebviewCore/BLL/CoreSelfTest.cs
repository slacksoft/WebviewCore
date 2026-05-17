using AngleSharp;
using AngleSharp.Css;
using AngleSharp.Dom;
using System.Text;
using SkiaSharp;

namespace WebviewCore;

static class CoreSelfTest
{
    private const int Width = 640;
    private const int Height = 360;

    public static async Task<int> RunAsync(string[] args)
    {
        var results = new List<(string name, bool ok, string detail)>();

        await RunCase("V8_DOM_style_event_layout_render", results, async () =>
        {
            var html = """
<!doctype html>
<html>
<head>
<style>
body { margin: 0; font-family: Segoe UI; }
#app { width: 120px; height: 40px; background-color: #112233; color: white; padding: 4px; }
.wide { width: 180px; }
</style>
</head>
<body>
<div id="app">hello</div>
<script>
var app = document.getElementById('app');
app.classList.add('wide');
app.style.backgroundColor = '#ff0000';
app.style.borderRadius = '6px';
app.addEventListener('click', function(e) {
  var child = document.createElement('span');
  child.id = 'clicked';
  child.textContent = ' clicked';
  app.appendChild(child);
  app.setAttribute('data-event', e.type);
});
app.dispatchEvent(new Event('click'));
document.body.insertAdjacentHTML('beforeend', '<p id="added" style="color: rgb(0, 128, 0)">added</p>');
document.write('<span id="written">written</span>');
console.log('computed', getComputedStyle(app).getPropertyValue('background-color'));
</script>
</body>
</html>
""";

            var doc = await CreateDocumentAsync(html);
            using var js = new JsEngine();
            var changed = false;
            js.DomChanged += () => changed = true;
            js.Initialize(doc);
            ExecuteScripts(doc, js);
            DumpJsLog(js);
            ApplyDocWrite(doc, js);

            var app = doc.GetElementById("app")!;
            Assert(app.TextContent.Contains("clicked"), "event listener should append a child");
            Assert(app.GetAttribute("data-event") == "click", "dispatchEvent should pass event.type");
            Assert(doc.GetElementById("added") != null, "insertAdjacentHTML should mutate DOM");
            Assert(doc.GetElementById("written") != null, "document.write should be applied");
            Assert((app.GetAttribute("style") ?? "").Contains("background-color:#ff0000"), "style.backgroundColor should serialize as background-color");
            Assert(changed, "DOM mutation should raise DomChanged");
            Assert(js.ConsoleLog.Any(l => l.Contains("computed")), "console.log should receive computed style output");

            var styles = StyleComputer.BuildStyleMap(doc);
            Assert(styles[app].HasWidth && Math.Abs(styles[app].Width - 180) < 0.1f, "class style should affect computed width");
            Assert(styles[app].BackgroundColor?.R == 255, "inline style should override background color");

            var layout = new LayoutEngine(Width, styles).Layout(doc.Body!);
            var appBox = FindBox(layout, app);
            Assert(appBox != null, "layout should contain #app box");
            Assert(Math.Abs(appBox!.Bounds.Width - 180) < 0.1f, "layout width should use computed style");

            using var bitmap = Render(layout);
            Assert(HasColorNear(bitmap, 255, 0, 0), "Skia render should contain red box pixels");
        });

        await RunCase("Timers_text_nodes_computed_style", results, async () =>
        {
            var html = """
<!doctype html>
<body>
<div id="box" style="width: 90px; height: 30px; color: blue"></div>
<script>
var box = document.getElementById('box');
var text = document.createTextNode('abc');
box.appendChild(text);
text.data = 'xyz';
setTimeout(function(){ box.setAttribute('data-timeout','ok'); }, 0);
var cs = getComputedStyle(box);
box.setAttribute('data-width', cs.getPropertyValue('width'));
</script>
</body>
""";

            var doc = await CreateDocumentAsync(html);
            using var js = new JsEngine();
            js.Initialize(doc);
            ExecuteScripts(doc, js);
            DumpJsLog(js);

            var box = doc.GetElementById("box")!;
            Assert(box.TextContent == "xyz", "text node data setter should update AngleSharp text");
            Assert(box.GetAttribute("data-timeout") == "ok", "setTimeout should invoke JS function objects");
            Assert(box.GetAttribute("data-width") == "90px", "getComputedStyle should expose width");
        });

        await RunCase("Console_variadic_formatting", results, async () =>
        {
            var doc = await CreateDocumentAsync("<!doctype html><body></body>");
            using var js = new JsEngine();
            js.Initialize(doc);
            js.Exec("""
console.log('UserAgent:', navigator.userAgent, {ok:true, count:2}, [1, false]);
console.log('fmt %s %d', 'value', 7);
console.assert(false, 'bad', 42);
console.count('loop');
console.count('loop');
""");
            DumpJsLog(js);

            Assert(js.ConsoleLog.Any(l => l.Contains("UserAgent:") && l.Contains("ok") && l.Contains("false")), "console.log should format multiple arguments");
            Assert(js.ConsoleLog.Any(l => l.Contains("fmt value 7")), "console.log should apply basic formatter tokens");
            Assert(js.ConsoleLog.Any(l => l.Contains("Assertion failed") && l.Contains("bad") && l.Contains("42")), "console.assert should log failed assertion details");
            Assert(js.ConsoleLog.Any(l => l.Contains("loop: 2")), "console.count should keep label counters");
        });

        await RunCase("Element_scoped_dom_queries", results, async () =>
        {
            var doc = await CreateDocumentAsync("""
<!doctype html>
<body>
<div id="root">
  <span class="x y"></span>
  <span class="x"></span>
  <em class="x y"></em>
</div>
</body>
""");
            using var js = new JsEngine();
            js.Initialize(doc);
            js.Exec("""
var root = document.getElementById('root');
document.body.setAttribute('data-query',
  [root.getElementsByTagName('span').length,
   root.getElementsByTagNameNS(null, 'em').length,
   root.getElementsByClassName('x y').length].join('|'));
""");
            DumpJsLog(js);

            var actualQuery = doc.Body!.GetAttribute("data-query") ?? "";
            Assert(actualQuery == "2|1|2", $"element-scoped DOM query methods should be callable from JavaScript, actual '{actualQuery}'");
        });

        await RunCase("Charset_and_form_value_bridge", results, async () =>
        {
            await Task.CompletedTask;
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            var cjk = "\u4e2d\u6587\u6d4b\u8bd5";
            var gbkHtml = $"<!doctype html><meta charset=\"gbk\"><body>{cjk}</body>";
            var decoded = HtmlFetcher.DecodeHtml(Encoding.GetEncoding("gb18030").GetBytes(gbkHtml), null);
            Assert(decoded.Contains(cjk), "GBK/GB18030 HTML bytes should decode to readable Chinese text");

            var utf8Html = $"<!doctype html><body>{cjk}</body>";
            var decodedUtf8 = HtmlFetcher.DecodeHtml(Encoding.UTF8.GetBytes(utf8Html), null);
            Assert(decodedUtf8.Contains(cjk), "UTF-8 HTML bytes should remain readable Chinese text");

            var doc = await CreateDocumentAsync($"""
<!doctype html>
<body>
<input id="i" value="">
<textarea id="t"></textarea>
<select id="s"><option value="a">A</option><option value="b">B</option></select>
<input id="c" type="checkbox">
</body>
""");

            using var js = new JsEngine();
            js.Initialize(doc);
            js.Exec($"""
var i = document.getElementById('i');
var t = document.getElementById('t');
var s = document.getElementById('s');
var c = document.getElementById('c');
i.value = 'typed';
t.value = '{cjk}';
s.selectedIndex = 1;
c.checked = true;
document.body.setAttribute('data-values', [i.value, t.value, s.value, c.checked, s.selectedIndex].join('|'));
""");
            DumpJsLog(js);

            var actualValues = doc.Body!.GetAttribute("data-values") ?? "";
            Assert(actualValues == $"typed|{cjk}|b|true|1", $"JavaScript should read live input/textarea/select/checkbox values, actual '{actualValues}'");
            Assert(doc.GetElementById("i")!.GetAttribute("value") == "typed", "input.value should update the DOM value attribute");
            Assert(doc.GetElementById("t")!.TextContent == cjk, "textarea.value should update text content for layout");
            Assert(doc.QuerySelector("#s option[value=b]")!.HasAttribute("selected"), "select.selectedIndex should update option selection");

            var styles = StyleComputer.BuildStyleMap(doc);
            var layout = new LayoutEngine(Width, styles).Layout(doc.Body!);
            Assert(FindBox(layout, doc.GetElementById("i")!)?.InputValue == "typed", "layout should preserve typed input values");
            Assert(FindBox(layout, doc.GetElementById("t")!)?.InputValue == cjk, "layout should preserve typed textarea values");
            Assert(FindBox(layout, doc.GetElementById("s")!)?.InputValue == "b", "layout should preserve selected option value");
        });

        await RunCase("Box_shadow_direction_and_inset", results, async () =>
        {
            await Task.CompletedTask;

            var outerStyle = new BoxStyle { BackgroundColor = Color.Red };
            StyleComputer.ApplyCssDecl(outerStyle, "box-shadow", "20px 0 0 0 rgb(0 0 0 / 1)");
            Assert(Math.Abs(outerStyle.ShadowX - 20) < 0.1f && outerStyle.ShadowY == 0, "box-shadow should parse positive X offset");
            Assert(outerStyle.ShadowColor.A == 255, "modern rgb(... / alpha) shadow colors should parse alpha");

            var insetStyle = new BoxStyle { BackgroundColor = Color.White };
            StyleComputer.ApplyCssDecl(insetStyle, "box-shadow", "inset 10px 0 0 rgba(0,0,0,1)");
            Assert(insetStyle.ShadowInset && Math.Abs(insetStyle.ShadowX - 10) < 0.1f, "inset shadow should preserve direction values");

            var layout = new LayoutBox { IsBlock = true, Bounds = new RectangleF(0, 0, Width, Height) };
            layout.Children.Add(new LayoutBox
            {
                IsBlock = true,
                Bounds = new RectangleF(80, 80, 40, 40),
                Style = outerStyle,
                BgColor = Color.Red,
            });
            layout.Children.Add(new LayoutBox
            {
                IsBlock = true,
                Bounds = new RectangleF(200, 80, 60, 60),
                Style = insetStyle,
                BgColor = Color.White,
            });

            using var bitmap = Render(layout);
            Assert(IsPixelNear(bitmap, 130, 100, 0, 0, 0, 8), "positive outer X shadow should render to the right of the box");
            Assert(IsPixelNear(bitmap, 70, 100, 255, 255, 255, 8), "outer shadow should not render on the opposite side");
            Assert(IsPixelNear(bitmap, 205, 110, 0, 0, 0, 8), "positive inset X shadow should be visible on the inner left edge");
            Assert(IsPixelNear(bitmap, 255, 110, 255, 255, 255, 8), "inset shadow should not fill the entire input area");
        });

        await RunCase("RenderEngine_css_painting", results, async () =>
        {
            await Task.CompletedTask;
            var cjkText = "\u4e2d\u6587\u6d4b\u8bd5";
            var cjkTypeface = FontResolver.GetTypeface("Consolas", false, false, cjkText);
            using (var cjkFont = new SKFont(cjkTypeface, 16))
                Assert(cjkFont.ContainsGlyphs("\u4e2d\u6587"), "font resolver should choose a CJK-capable fallback for Chinese text");
            var cjkElementTypeface = FontResolver.GetTypefaceForTextElement("Segoe UI", false, false, "\u4e2d");
            using (var cjkElementFont = new SKFont(cjkElementTypeface, 16))
                Assert(cjkElementFont.ContainsGlyphs("\u4e2d"), "per-glyph font fallback should resolve Chinese glyphs");

            var layout = new LayoutBox { IsBlock = true, Bounds = new RectangleF(0, 0, Width, Height) };

            var clip = new LayoutBox
            {
                IsBlock = true,
                Bounds = new RectangleF(10, 10, 50, 50),
                Style = new BoxStyle { Overflow = "hidden", OverflowX = "hidden", OverflowY = "hidden", BackgroundColor = Color.White },
                BgColor = Color.White,
            };
            clip.Children.Add(new LayoutBox
            {
                IsBlock = true,
                Bounds = new RectangleF(40, 40, 80, 80),
                Style = new BoxStyle { BackgroundColor = Color.Red },
                BgColor = Color.Red,
            });
            layout.Children.Add(clip);

            layout.Children.Add(new LayoutBox
            {
                IsBlock = true,
                Bounds = new RectangleF(100, 10, 60, 60),
                Style = new BoxStyle { Position = "relative", ZIndex = 5, BackgroundColor = Color.Red },
                BgColor = Color.Red,
            });
            layout.Children.Add(new LayoutBox
            {
                IsBlock = true,
                Bounds = new RectangleF(110, 20, 60, 60),
                Style = new BoxStyle { Position = "relative", ZIndex = 1, BackgroundColor = Color.Blue },
                BgColor = Color.Blue,
            });

            var parsedGradient = new BoxStyle();
            StyleComputer.ApplyCssDecl(parsedGradient, "background-image", "linear-gradient(90deg, red 0%, blue 100%)");
            Assert(parsedGradient.BackgroundGradient.Contains("linear-gradient"), "background-image gradients should be parsed as gradients");

            layout.Children.Add(new LayoutBox
            {
                IsBlock = true,
                Bounds = new RectangleF(200, 10, 100, 40),
                Style = parsedGradient,
            });

            var opacity = new LayoutBox
            {
                IsBlock = true,
                Bounds = new RectangleF(320, 10, 60, 60),
                Style = new BoxStyle { Opacity = 0.5f },
            };
            opacity.Children.Add(new LayoutBox
            {
                IsBlock = true,
                Bounds = new RectangleF(320, 10, 60, 60),
                Style = new BoxStyle { BackgroundColor = Color.Red },
                BgColor = Color.Red,
            });
            layout.Children.Add(opacity);

            var percentRadius = new BoxStyle { BackgroundColor = Color.Red };
            StyleComputer.ApplyCssDecl(percentRadius, "border-radius", "50%");
            Assert(percentRadius.BorderTopLeftRadiusIsPercent, "border-radius percent values should be preserved for renderer sizing");
            layout.Children.Add(new LayoutBox
            {
                IsBlock = true,
                Bounds = new RectangleF(420, 10, 80, 80),
                Style = percentRadius,
                BgColor = Color.Red,
            });

            using var bitmap = Render(layout);
            Assert(IsPixelNear(bitmap, 45, 45, 255, 0, 0, 8), "overflow child should draw inside parent clip");
            Assert(IsPixelNear(bitmap, 80, 80, 255, 255, 255, 8), "overflow:hidden should clip child pixels outside parent");
            Assert(IsPixelNear(bitmap, 120, 30, 255, 0, 0, 8), "higher z-index positioned box should paint on top");
            Assert(IsRedDominant(bitmap.GetPixel(205, 30)), "linear-gradient(90deg) should be red on the left");
            Assert(IsBlueDominant(bitmap.GetPixel(295, 30)), "linear-gradient(90deg) should be blue on the right");
            var blended = bitmap.GetPixel(350, 40);
            Assert(blended.Red > 240 && blended.Green is > 110 and < 150 && blended.Blue is > 110 and < 150, "opacity should apply to the full child subtree");
            Assert(IsPixelNear(bitmap, 421, 11, 255, 255, 255, 8), "border-radius:50% should clip div background corners");
            Assert(IsPixelNear(bitmap, 460, 50, 255, 0, 0, 8), "border-radius:50% should preserve the div center fill");
        });

        foreach (var (name, ok, detail) in results)
        {
            var status = ok ? "PASS" : "FAIL";
            Console.WriteLine($"{status} {name}{(detail.Length > 0 ? ": " + detail : "")}");
        }

        var failed = results.Count(r => !r.ok);
        Console.WriteLine($"Self-test summary: {results.Count - failed}/{results.Count} passed");
        return failed == 0 ? 0 : 1;
    }

    private static async Task<IDocument> CreateDocumentAsync(string html)
    {
        var cfg = Configuration.Default.WithCss();
        var ctx = BrowsingContext.New(cfg);
        var doc = await ctx.OpenAsync(req => req.Content(html));
        doc.DocumentElement?.SetAttribute("_base", "about:self-test");
        return doc;
    }

    private static void ExecuteScripts(IDocument doc, JsEngine js)
    {
        foreach (var script in doc.QuerySelectorAll("script").ToArray())
            js.Exec(script.TextContent ?? "");
    }

    private static void DumpJsLog(JsEngine js)
    {
        if (Environment.GetEnvironmentVariable("WEBVIEWCORE_DEBUG_SCRIPT") != "1") return;
        foreach (var line in js.ConsoleLog)
            Console.WriteLine("JSLOG " + line);
    }

    private static void ApplyDocWrite(IDocument doc, JsEngine js)
    {
        var dw = js.GetAndClearDocWrite();
        if (dw.Length == 0 || doc.Body == null) return;

        var temp = doc.CreateElement("div");
        temp.InnerHtml = dw;
        foreach (var child in temp.ChildNodes.ToArray())
            doc.Body.AppendChild(child);
    }

    private static SKBitmap Render(LayoutBox layout)
    {
        var bitmap = new SKBitmap(Width, Height, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        new RenderEngine(layout).Render(canvas, new Size(Width, Height), 0);
        return bitmap;
    }

    private static bool HasColorNear(SKBitmap bitmap, byte r, byte g, byte b)
    {
        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                var c = bitmap.GetPixel(x, y);
                if (Math.Abs(c.Red - r) <= 8 && Math.Abs(c.Green - g) <= 8 && Math.Abs(c.Blue - b) <= 8)
                    return true;
            }
        }

        return false;
    }

    private static bool IsPixelNear(SKBitmap bitmap, int x, int y, byte r, byte g, byte b, int tolerance)
    {
        var c = bitmap.GetPixel(x, y);
        return Math.Abs(c.Red - r) <= tolerance && Math.Abs(c.Green - g) <= tolerance && Math.Abs(c.Blue - b) <= tolerance;
    }

    private static bool IsRedDominant(SKColor c) => c.Red > 180 && c.Red > c.Blue + 80 && c.Red > c.Green + 80;
    private static bool IsBlueDominant(SKColor c) => c.Blue > 180 && c.Blue > c.Red + 80 && c.Blue > c.Green + 80;

    private static LayoutBox? FindBox(LayoutBox box, IElement source)
    {
        if (box.Source == source) return box;
        foreach (var child in box.Children)
        {
            var found = FindBox(child, source);
            if (found != null) return found;
        }

        return null;
    }

    private static async Task RunCase(string name, List<(string name, bool ok, string detail)> results, Func<Task> test)
    {
        try
        {
            await test();
            results.Add((name, true, ""));
        }
        catch (Exception ex)
        {
            results.Add((name, false, ex.Message));
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
