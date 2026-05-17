using AngleSharp;
using AngleSharp.Css;
using AngleSharp.Dom;
using System.Globalization;
using System.Text;
using SkiaSharp;

namespace WebviewCore;

public class BrowserControl : UserControl
{
    public event EventHandler<string>? TitleChanged;
    public event EventHandler<string>? NewTabRequested;
    public event EventHandler? CloseRequested;
    public event EventHandler? PrintRequested;
    public event EventHandler<FetchResult>? DownloadRequested;

    private readonly record struct TextHit(LayoutBox Box, int Offset);

    private LayoutBox? _layout;
    private IDocument? _doc;
    private JsEngine? _jsEngine;
    private string _rawHtml = "";
    private string _currentUrl = "";
    private float _scrollY;
    private LayoutBox? _focusedInput;
    private readonly Stack<string> _history = new();
    private const string DefaultUrl = "about:blank";
    private readonly Dictionary<IElement, (string val, bool chk)> _formState = new();
    private bool _isSelectingText;
    private bool _suppressNextClick;
    private TextHit? _selectionAnchor;
    private TextHit? _selectionFocus;
    private string _selectedText = "";
#pragma warning disable CS0414
    private bool _domChanged;
#pragma warning restore CS0414

    private readonly TextBox _urlBox;
    private readonly Button _backBtn, _goBtn;
    private readonly TabControl _viewTabs;
    private readonly Panel _renderPanel;
    private readonly VScrollBar _scrollBar;
    private readonly TextBox _consoleBox, _sourceBox;
    private readonly TextBox _consoleInput;

    public BrowserControl()
    {
        _backBtn = new Button { Text = "←", Location = new Point(8, 7), Size = new Size(28, 28), Enabled = false };
        _backBtn.Click += (_, _) => GoBack();
        _urlBox = new TextBox { Text = DefaultUrl, Location = new Point(_backBtn.Right + 4, 8), Size = new Size(ClientSize.Width - _backBtn.Right - 56, 26), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right, Font = new Font("Segoe UI", 10) };
        _goBtn = new Button { Text = "Go", Location = new Point(ClientSize.Width - 48, 7), Size = new Size(48, 28), Anchor = AnchorStyles.Top | AnchorStyles.Right };
        _goBtn.Click += (_, _) => Navigate(_urlBox.Text.Trim());
        _urlBox.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { Navigate(_urlBox.Text.Trim()); e.SuppressKeyPress = true; } };
        _scrollBar = new VScrollBar { Dock = DockStyle.Right, Visible = false, SmallChange = 20, LargeChange = 80 };
        _scrollBar.ValueChanged += (_, _) => { _scrollY = _scrollBar.Value; _renderPanel!.Invalidate(); };
        _renderPanel = new DoubleBufferedPanel { BackColor = Color.White, Dock = DockStyle.Fill };
        _renderPanel.Paint += RenderPanel_Paint!;
        _renderPanel.MouseDown += RenderPanel_MouseDown!;
        _renderPanel.MouseClick += RenderPanel_Click!;
        _renderPanel.MouseMove += RenderPanel_MouseMove!;
        _renderPanel.MouseUp += RenderPanel_MouseUp!;
        _renderPanel.KeyPress += RenderPanel_KeyPress!;
        _renderPanel.KeyDown += RenderPanel_KeyDown!;
        _renderPanel.MouseWheel += RenderPanel_MouseWheel!;
        var rc = new Panel { Dock = DockStyle.Fill }; rc.Controls.Add(_renderPanel); rc.Controls.Add(_scrollBar);
        _consoleBox = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, WordWrap = false, Dock = DockStyle.Fill, Font = new Font("Consolas", 9), BackColor = Color.FromArgb(30, 30, 30), ForeColor = Color.FromArgb(0, 200, 0) };
        _consoleInput = new TextBox { Dock = DockStyle.Bottom, Height = 24, Font = new Font("Consolas", 9), BackColor = Color.FromArgb(20, 20, 20), ForeColor = Color.FromArgb(0, 200, 0), BorderStyle = BorderStyle.FixedSingle };
        _consoleInput.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { ExecuteConsoleJs(); e.SuppressKeyPress = true; } };
        var consolePanel = new Panel { Dock = DockStyle.Fill };
        consolePanel.Controls.Add(_consoleBox);
        consolePanel.Controls.Add(_consoleInput);
        _sourceBox = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, WordWrap = false, Dock = DockStyle.Fill, Font = new Font("Consolas", 9), BackColor = Color.White, ForeColor = Color.Black };
        _viewTabs = new TabControl { Location = new Point(0, _urlBox.Bottom + 4), Size = new Size(ClientSize.Width, ClientSize.Height - _urlBox.Bottom - 4), Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right };
        _viewTabs.TabPages.Add("Render", "Render"); _viewTabs.TabPages.Add("Console", "Console"); _viewTabs.TabPages.Add("Source", "Source");
        _viewTabs.TabPages["Render"]!.Controls.Add(rc); _viewTabs.TabPages["Console"]!.Controls.Add(consolePanel); _viewTabs.TabPages["Source"]!.Controls.Add(_sourceBox);
        Controls.Add(_backBtn); Controls.Add(_urlBox); Controls.Add(_goBtn); Controls.Add(_viewTabs);
        DoubleBuffered = true; BackColor = Color.White;
        Resize += (_, _) => RelayoutOrResize();
        Load += (_, _) => RelayoutOrResize();
        _ = LoadPageAsync(DefaultUrl);
    }

    public string CurrentUrl => _currentUrl;
    public string Title { get; private set; } = "WebviewCore";

    private int RenderWidth => _renderPanel.ClientSize.Width > 16 ? _renderPanel.ClientSize.Width - 16 : 800;
    private int RenderHeight => _renderPanel.ClientSize.Height > 0 ? _renderPanel.ClientSize.Height : 600;

    public void GoBack() { if (_history.Count == 0) return; _backBtn.Enabled = _history.Count > 0; _ = LoadPageAsync(_history.Pop()); }

    public void Navigate(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        if (!url.Contains("://")) { if (url.StartsWith("//")) url = "http:" + url; else if (url.StartsWith('/')) url = "file:///" + url; else url = "http://" + url; }
        if (_currentUrl.Length > 0 && url != _currentUrl) { _history.Push(_currentUrl); _backBtn.Enabled = true; }
        _ = LoadPageAsync(url);
    }

    public async Task LoadPageAsync(string url)
    {
        _jsEngine?.Dispose(); _jsEngine = null; _currentUrl = url;
        try
        {
            Title = "WebviewCore - Loading..."; _urlBox.Text = url; _viewTabs.SelectedIndex = 0;

            if (url == "about:blank" || url == "about:test")
            {
                _rawHtml = url == "about:test" ? TestPage.Html : "<!DOCTYPE html><html><head><title>Blank</title></head><body></body></html>";
                _sourceBox.Text = _rawHtml;
            }
            else
            {
                var result = await HtmlFetcher.FetchResultAsync(url);
                if (result.IsDownload)
                {
                    DownloadRequested?.Invoke(this, result);
                    return;
                }
                _rawHtml = result.Html ?? "";
                _sourceBox.Text = _rawHtml;
            }

            var cfg = Configuration.Default.WithCss();
            var ctx = BrowsingContext.New(cfg);
            _doc = await ctx.OpenAsync(req => req.Content(_rawHtml));
            _doc.DocumentElement?.SetAttribute("_base", HtmlFetcher.BaseUrl);

            Title = "WebviewCore";

            _consoleBox.Clear(); _jsEngine = new JsEngine();
            _jsEngine.MessageLogged += msg => { if (_consoleBox.IsHandleCreated) BeginInvoke(() => { _consoleBox.AppendText(msg + "\r\n"); _consoleBox.SelectionStart = _consoleBox.TextLength; _consoleBox.ScrollToCaret(); }); };
            _jsEngine.DomChanged += () => _domChanged = true;
            _jsEngine.OpenRequested += url2 => { if (IsHandleCreated) BeginInvoke(() => NewTabRequested?.Invoke(this, url2)); };
            _jsEngine.CloseRequested += () => { if (IsHandleCreated) BeginInvoke(() => CloseRequested?.Invoke(this, EventArgs.Empty)); };
            _jsEngine.PrintRequested += () => { if (IsHandleCreated) BeginInvoke(() => PrintRequested?.Invoke(this, EventArgs.Empty)); };
            try
            {
                _jsEngine.Initialize(_doc);
            }
            catch (Exception ex)
            {
                Title = "WebviewCore - JS Init Error";
                _layout = CreateErrorLayout($"JS Init Error: {ex.Message}\n{ex.StackTrace}");
                _renderPanel.Invalidate();
                return;
            }

            foreach (var script in _doc.QuerySelectorAll("script").ToArray())
            {
                var src = script.GetAttribute("src");
                if (!string.IsNullOrEmpty(src))
                {
                    var uri = new Uri(new Uri(HtmlFetcher.BaseUrl), src);
                    await _jsEngine.ExecUrl(uri.ToString());
                }
                else
                {
                    _jsEngine.Exec(script.TextContent ?? "");
                }

                var dw = _jsEngine.GetAndClearDocWrite();
                if (dw.Length > 0 && script.ParentElement != null)
                {
                    var pos = script.NextSibling;
                    var temp = _doc.CreateElement("div");
                    temp.InnerHtml = dw;
                    var children = temp.Children.ToArray();
                    foreach (var child in children)
                        script.ParentElement.InsertBefore(child, pos);
                    _domChanged = true;
                }
            }

            _sourceBox.Text = _doc.DocumentElement?.OuterHtml ?? _rawHtml;
            Title = _doc.QuerySelector("title")?.TextContent?.Trim() ?? "WebviewCore";

            var styles = StyleComputer.BuildStyleMap(_doc);

            _formState.Clear();
            var eng = new LayoutEngine(RenderWidth, styles);
            QueueBackgroundImages(styles, eng.PendingImages);
            _layout = _doc.Body != null ? eng.Layout(_doc.Body) : null; _scrollY = 0; UpdateScrollbar();
            _domChanged = false;
            _renderPanel.Invalidate();
            _ = LoadImagesAsync(eng.PendingImages);
        }
        catch (Exception ex) { Title = "WebviewCore - Error"; _layout = CreateErrorLayout($"Error: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace?.Split('\n').FirstOrDefault() ?? ""}"); _renderPanel.Invalidate(); }
        finally { TitleChanged?.Invoke(this, Title); }
    }

    private static readonly Dictionary<string, string> DefaultDisplay = new(StringComparer.OrdinalIgnoreCase)
    {
        ["html"]="block",["body"]="block",["div"]="block",["p"]="block",
        ["h1"]="block",["h2"]="block",["h3"]="block",["h4"]="block",["h5"]="block",["h6"]="block",
        ["ul"]="block",["ol"]="block",["li"]="block",
        ["dl"]="block",["dt"]="block",["dd"]="block",
        ["table"]="table",["tr"]="table-row",["td"]="table-cell",["th"]="table-cell",
        ["thead"]="table-row-group",["tbody"]="table-row-group",["tfoot"]="table-row-group",
        ["form"]="block",["fieldset"]="block",["legend"]="block",
        ["article"]="block",["section"]="block",["nav"]="block",["aside"]="block",
        ["header"]="block",["footer"]="block",["main"]="block",["figure"]="block",["figcaption"]="block",
        ["blockquote"]="block",["center"]="block",["pre"]="block",
        ["hr"]="block",["address"]="block",
        ["br"]="inline",["span"]="inline",["a"]="inline",["img"]="inline",
        ["strong"]="inline",["b"]="inline",["em"]="inline",["i"]="inline",
        ["u"]="inline",["ins"]="inline",["del"]="inline",["s"]="inline",["strike"]="inline",
        ["sub"]="inline",["sup"]="inline",["small"]="inline",["mark"]="inline",
        ["code"]="inline",["kbd"]="inline",["samp"]="inline",["tt"]="inline",
        ["var"]="inline",["cite"]="inline",["dfn"]="inline",["abbr"]="inline",
        ["input"]="inline-block",["button"]="inline-block",["textarea"]="inline-block",["select"]="inline-block",
        ["label"]="inline",
        ["style"]="none",["script"]="none",["head"]="none",["meta"]="none",["link"]="none",["base"]="none",["title"]="none",
    };

    private static BoxStyle MakeDefaultStyle(string? tag)
    {
        var bs = new BoxStyle();
        if (tag != null && DefaultDisplay.TryGetValue(tag, out var disp))
        {
            bs.DisplayNone = disp == "none";
            bs.DisplayBlock = disp is "block" or "inline-block" or "table" or "table-row" or "table-cell";
            bs.IsTable = disp == "table"; bs.IsTableRow = disp == "table-row"; bs.IsTableCell = disp == "table-cell";
        }
        if (tag == "h1") { bs.FontSize = 24; bs.Bold = true; }
        else if (tag == "h2") { bs.FontSize = 20; bs.Bold = true; }
        else if (tag == "h3") { bs.FontSize = 16; bs.Bold = true; }
        else if (tag is "h4" or "h5" or "h6") { bs.FontSize = 14; bs.Bold = true; }
        else if (tag is "strong" or "b" or "th" or "dt") bs.Bold = true;
        else if (tag is "i" or "em" or "cite" or "var" or "dfn") bs.Italic = true;
        else if (tag is "u" or "ins") bs.Underline = true;
        else if (tag is "s" or "del" or "strike") bs.LineThrough = true;
        else if (tag == "mark") { bs.BackgroundColor = Color.Yellow; bs.Color = Color.Black; }
        else if (tag == "small") bs.FontSize = 10;
        else if (tag == "a") { bs.Color = Color.Blue; bs.Underline = true; }
        if (tag is "td" or "th") { bs.BorderTop = bs.BorderBottom = bs.BorderLeft = bs.BorderRight = 1; bs.BorderColor = Color.FromArgb(180, 180, 180); }
        return bs;
    }

    private static void ApplyInlineStyle(BoxStyle bs, string style)
    {
        if (string.IsNullOrWhiteSpace(style)) return;
        foreach (var decl in style.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var p = decl.Split(':', 2, StringSplitOptions.TrimEntries);
            if (p.Length != 2) continue;
            ApplyCssDecl(bs, p[0].Trim().ToLowerInvariant(), p[1].Trim());
        }
    }

    private static void ApplyCssDecl(BoxStyle bs, string prop, string value)
    {
        value = value.Trim();
        switch (prop)
        {
            case "color": bs.Color = ParseCssColor(value); break;
            case "background-color": bs.BackgroundColor = ParseCssColorOrNull(value); break;
            case "background": ParseBackground(bs, value); break;
            case "background-image":
                if (value.Contains("linear-gradient", StringComparison.OrdinalIgnoreCase) || value.Contains("radial-gradient", StringComparison.OrdinalIgnoreCase))
                    bs.BackgroundGradient = value;
                else if (!value.Equals("none", StringComparison.OrdinalIgnoreCase))
                    bs.BackgroundImage = value.Trim('\'', '"');
                break;
            case "background-repeat": bs.BackgroundRepeat = value.ToLowerInvariant(); break;
            case "background-position": bs.BackgroundPosition = value; break;
            case "background-size": bs.BackgroundSize = value.ToLowerInvariant(); break;
            case "background-clip": bs.BackgroundClip = value.ToLowerInvariant(); break;
            case "background-origin": bs.BackgroundOrigin = value.ToLowerInvariant(); break;
            case "background-attachment": bs.BackgroundAttachment = value.ToLowerInvariant(); break;
            case "font-size": bs.FontSize = ParsePx(value, 12); break;
            case "font-weight": bs.Bold = value is "bold" or "700" or "800" or "900"; break;
            case "font-style": bs.Italic = value is "italic" or "oblique"; break;
            case "font-family": bs.FontFamily = ParseFont(value); break;
            case "font-variant": bs.SmallCaps = value == "small-caps"; break;
            case "font-stretch": bs.FontStretch = value.ToLowerInvariant(); break;
            case "font-kerning": bs.FontKerning = value.ToLowerInvariant(); break;
            case "line-height": bs.LineHeight = ParseLineH(value, bs.FontSize); break;
            case "letter-spacing": bs.LetterSpacing = ParsePx(value); break;
            case "word-spacing": bs.WordSpacing = ParsePx(value); break;
            case "text-transform": bs.TextTransform = value.ToLowerInvariant(); break;
            case "text-indent": bs.TextIndent = ParsePx(value); break;
            case "text-decoration":
                bs.Underline = value.Contains("underline");
                bs.LineThrough = value.Contains("line-through");
                bs.Overline = value.Contains("overline");
                bs.TextDecorationLine = value;
                foreach (var tp in value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                {
                    var tl = tp.ToLowerInvariant();
                    if (tl.StartsWith('#') || tl.StartsWith("rgb")) bs.TextDecorationColor = ParseCssColor(tp);
                    if (tl is "solid" or "double" or "dotted" or "dashed" or "wavy") bs.TextDecorationStyle = tl;
                }
                break;
            case "text-decoration-color": bs.TextDecorationColor = ParseCssColor(value); break;
            case "text-decoration-style": bs.TextDecorationStyle = value.ToLowerInvariant(); break;
            case "text-decoration-line":
                bs.Underline = value.Contains("underline");
                bs.LineThrough = value.Contains("line-through");
                bs.Overline = value.Contains("overline");
                break;
            case "text-decoration-thickness": bs.TextDecorationThickness = ParsePx(value); break;
            case "text-shadow":
                if (value.ToLowerInvariant() == "none") { bs.TextShadowBlur = 0; break; }
                var ts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (ts.Length > 0) bs.TextShadowX = ParsePx(ts[0]);
                if (ts.Length > 1) bs.TextShadowY = ParsePx(ts[1]);
                if (ts.Length > 2) bs.TextShadowBlur = ParsePx(ts[2]);
                for (int i = ts.Length - 1; i >= 0; i--)
                    if (ts[i].StartsWith('#') || ts[i].StartsWith("rgb") || NamedColors.ContainsKey(ts[i].ToLowerInvariant()))
                    { bs.TextShadowColor = ParseCssColor(ts[i]); break; }
                break;
            case "white-space": bs.WhiteSpace = value.ToLowerInvariant(); break;
            case "text-align": bs.TextAlign = value.ToLowerInvariant(); break;
            case "vertical-align": bs.VerticalAlign = value.ToLowerInvariant(); break;
            case "direction": bs.Direction = value.ToLowerInvariant(); break;
            case "word-break": bs.WordBreak = value.ToLowerInvariant(); break;
            case "overflow-wrap": bs.OverflowWrap = value.ToLowerInvariant(); break;
            case "word-wrap": bs.WordWrap = value.ToLowerInvariant(); break;
            case "text-overflow": bs.TextOverflow = value.ToLowerInvariant(); break;
            case "tab-size": bs.TabSize = ParsePx(value, 8); break;
            case "hyphens": bs.Hyphens = value.ToLowerInvariant(); break;
            case "display":
                var d = value.ToLowerInvariant(); var db = d;
                bs.DisplayNone = db == "none";
                bs.DisplayBlock = db is "block" or "inline-block" or "flex" or "inline-flex" or "table" or "table-row" or "table-cell";
                bs.DisplayInlineBlock = db == "inline-block";
                bs.DisplayFlex = db == "flex";
                bs.DisplayInlineFlex = db == "inline-flex";
                bs.DisplayGrid = db == "grid";
                bs.IsTable = db == "table"; bs.IsTableRow = db == "table-row"; bs.IsTableCell = db == "table-cell";
                bs.IsFlexContainer = db is "flex" or "inline-flex";
                break;
            case "opacity":
                if (float.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var op))
                    bs.Opacity = Math.Max(0, Math.Min(1, op));
                break;
            case "visibility": bs.Visibility = value.ToLowerInvariant(); break;
            case "overflow":
                var ov = value.ToLowerInvariant();
                bs.Overflow = bs.OverflowX = bs.OverflowY = ov;
                break;
            case "overflow-x": bs.OverflowX = value.ToLowerInvariant(); break;
            case "overflow-y": bs.OverflowY = value.ToLowerInvariant(); break;
            case "pointer-events": bs.PointerEvents = value.ToLowerInvariant(); break;
            case "user-select": bs.UserSelect = value.ToLowerInvariant(); break;
            case "width": bs.Width = ParsePx(value); bs.HasWidth = bs.Width > 0; break;
            case "height": bs.Height = ParsePx(value); bs.HasHeight = bs.Height > 0; break;
            case "min-width": bs.MinWidth = ParsePx(value); break;
            case "max-width": bs.MaxWidth = ParsePx(value); break;
            case "min-height": bs.MinHeight = ParsePx(value); break;
            case "max-height": bs.MaxHeight = ParsePx(value); break;
            case "box-sizing": bs.BoxSizing = value.ToLowerInvariant(); break;
            case "margin": ApplyFour(bs, "margin", value); break;
            case "margin-top": bs.MarginTop = ParsePx(value); break;
            case "margin-bottom": bs.MarginBottom = ParsePx(value); break;
            case "margin-left": bs.MarginLeft = ParsePx(value); break;
            case "margin-right": bs.MarginRight = ParsePx(value); break;
            case "padding": ApplyFour(bs, "padding", value); break;
            case "padding-top": bs.PaddingTop = ParsePx(value); break;
            case "padding-bottom": bs.PaddingBottom = ParsePx(value); break;
            case "padding-left": bs.PaddingLeft = ParsePx(value); break;
            case "padding-right": bs.PaddingRight = ParsePx(value); break;
            case "border": ParseBorder(bs, value); break;
            case "border-top": ParseBorderSide(bs, "top", value); break;
            case "border-bottom": ParseBorderSide(bs, "bottom", value); break;
            case "border-left": ParseBorderSide(bs, "left", value); break;
            case "border-right": ParseBorderSide(bs, "right", value); break;
            case "border-width": ParseBorderWidths(bs, value); break;
            case "border-style": ParseBorderStyles(bs, value); break;
            case "border-color": ParseBorderColors(bs, value); break;
            case "border-top-width": bs.BorderTop = ParsePx(value); break;
            case "border-bottom-width": bs.BorderBottom = ParsePx(value); break;
            case "border-left-width": bs.BorderLeft = ParsePx(value); break;
            case "border-right-width": bs.BorderRight = ParsePx(value); break;
            case "border-top-color": bs.BorderTopColor = ParseCssColor(value); break;
            case "border-bottom-color": bs.BorderBottomColor = ParseCssColor(value); break;
            case "border-left-color": bs.BorderLeftColor = ParseCssColor(value); break;
            case "border-right-color": bs.BorderRightColor = ParseCssColor(value); break;
            case "border-top-style": bs.BorderTopStyle = value.ToLowerInvariant(); break;
            case "border-bottom-style": bs.BorderBottomStyle = value.ToLowerInvariant(); break;
            case "border-left-style": bs.BorderLeftStyle = value.ToLowerInvariant(); break;
            case "border-right-style": bs.BorderRightStyle = value.ToLowerInvariant(); break;
            case "border-radius": ApplyBorderRadius(bs, value); break;
            case "border-top-left-radius": SetCornerRadius(bs, "top-left", value); break;
            case "border-top-right-radius": SetCornerRadius(bs, "top-right", value); break;
            case "border-bottom-left-radius": SetCornerRadius(bs, "bottom-left", value); break;
            case "border-bottom-right-radius": SetCornerRadius(bs, "bottom-right", value); break;
            case "outline":
                var oparts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                foreach (var o in oparts)
                {
                    if (o is "solid" or "dotted" or "dashed" or "double" or "none") bs.OutlineStyle = o;
                    else if (o.StartsWith('#') || NamedColors.ContainsKey(o.ToLowerInvariant()) || o.StartsWith("rgb")) bs.OutlineColor = ParseCssColor(o);
                    else { var ow = ParsePx(o); if (ow > 0) bs.OutlineWidth = ow; }
                }
                break;
            case "outline-width": bs.OutlineWidth = ParsePx(value); break;
            case "outline-style": bs.OutlineStyle = value.ToLowerInvariant(); break;
            case "outline-color": bs.OutlineColor = ParseCssColor(value); break;
            case "outline-offset": bs.OutlineOffset = ParsePx(value); break;
            case "box-shadow":
                if (value.ToLowerInvariant() == "none") { bs.ShadowBlur = 0; break; }
                var sh = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                bs.ShadowInset = value.Contains("inset");
                var sidx = bs.ShadowInset ? 1 : 0;
                if (sh.Length > sidx) bs.ShadowX = ParsePx(sh[sidx]);
                if (sh.Length > sidx + 1) bs.ShadowY = ParsePx(sh[sidx + 1]);
                if (sh.Length > sidx + 2) bs.ShadowBlur = ParsePx(sh[sidx + 2]);
                if (sh.Length > sidx + 3) bs.ShadowSpread = ParsePx(sh[sidx + 3]);
                for (int i = sh.Length - 1; i >= (bs.ShadowInset ? 1 : 0); i--)
                    if (sh[i].StartsWith('#') || sh[i].StartsWith("rgb") || NamedColors.ContainsKey(sh[i].ToLowerInvariant().TrimEnd(',')))
                    { bs.ShadowColor = ParseCssColor(sh[i]); break; }
                break;
            case "position": bs.Position = value.ToLowerInvariant(); break;
            case "left": bs.PositionLeft = ParsePx(value); break;
            case "right": bs.PositionRight = ParsePx(value); break;
            case "top": bs.PositionTop = ParsePx(value); break;
            case "bottom": bs.PositionBottom = ParsePx(value); break;
            case "z-index": { int zi = 0; int.TryParse(value, out zi); bs.ZIndex = zi; } break;
            case "float": bs.Float = value.ToLowerInvariant(); break;
            case "clear": bs.Clear = value.ToLowerInvariant(); break;
            case "flex-direction": bs.FlexDirection = value.ToLowerInvariant(); break;
            case "flex-wrap": bs.FlexWrap = value.ToLowerInvariant(); break;
            case "flex-flow": var ff = value.Split(' '); if (ff.Length > 0) bs.FlexDirection = ff[0].ToLowerInvariant(); if (ff.Length > 1) bs.FlexWrap = ff[1].ToLowerInvariant(); break;
            case "justify-content": bs.JustifyContent = value.ToLowerInvariant(); break;
            case "align-items": bs.AlignItems = value.ToLowerInvariant(); break;
            case "align-content": bs.AlignContent = value.ToLowerInvariant(); break;
            case "flex":
                var fparts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (fparts.Length > 0 && fparts[0] != "none") float.TryParse(fparts[0], out bs.FlexGrow);
                if (fparts.Length > 1) float.TryParse(fparts[1], out bs.FlexShrink);
                if (fparts.Length > 2) bs.FlexBasis = fparts[2];
                break;
            case "flex-grow": float.TryParse(value, out bs.FlexGrow); break;
            case "flex-shrink": float.TryParse(value, out bs.FlexShrink); break;
            case "flex-basis": bs.FlexBasis = value.ToLowerInvariant(); break;
            case "align-self": bs.AlignSelf = value.ToLowerInvariant(); break;
            case "order": int.TryParse(value, out bs.Order); break;
            case "grid-template-columns": bs.GridTemplateColumns = value.ToLowerInvariant(); break;
            case "grid-template-rows": bs.GridTemplateRows = value.ToLowerInvariant(); break;
            case "grid-column": bs.GridColumn = value.ToLowerInvariant(); break;
            case "grid-row": bs.GridRow = value.ToLowerInvariant(); break;
            case "grid-gap": case "gap": bs.GridGap = value.ToLowerInvariant(); break;
            case "border-collapse": bs.BorderCollapse = value.ToLowerInvariant(); break;
            case "border-spacing": bs.BorderSpacing = ParsePx(value); break;
            case "caption-side": bs.CaptionSide = value.ToLowerInvariant(); break;
            case "empty-cells": bs.EmptyCells = value.ToLowerInvariant(); break;
            case "table-layout": bs.TableLayout = value.ToLowerInvariant(); break;
            case "list-style-type": bs.ListStyleType = value.ToLowerInvariant(); break;
            case "list-style-position": bs.ListStylePosition = value.ToLowerInvariant(); break;
            case "list-style":
                var lsp = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                foreach (var l in lsp)
                { var ll = l.ToLowerInvariant(); if (ll is "inside" or "outside") bs.ListStylePosition = ll; else if (ll is "disc" or "circle" or "square" or "decimal" or "none") bs.ListStyleType = ll; }
                break;
            case "transform":
                var tv = value.Trim().ToLowerInvariant();
                if (tv.StartsWith("translate("))
                { var inner = tv[10..^1]; var pts = inner.Split(',', StringSplitOptions.TrimEntries); if (pts.Length > 0) bs.TransformX = ParsePx(pts[0]); if (pts.Length > 1) bs.TransformY = ParsePx(pts[1]); }
                else if (tv.StartsWith("translatex(")) bs.TransformX = ParsePx(tv[11..^1]);
                else if (tv.StartsWith("translatey(")) bs.TransformY = ParsePx(tv[11..^1]);
                else if (tv.StartsWith("rotate("))
                { var inner = tv[7..^1].Trim(); if (inner.EndsWith("deg") && float.TryParse(inner[..^3], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var rr)) bs.TransformRotate = rr; else if (inner.EndsWith("rad") && float.TryParse(inner[..^3], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var rr2)) bs.TransformRotate = rr2 * 57.2958f; }
                else if (tv.StartsWith("scale("))
                { var inner = tv[6..^1]; var pts = inner.Split(',', StringSplitOptions.TrimEntries); if (pts.Length > 0) { float.TryParse(pts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out bs.TransformScaleX); bs.TransformScaleY = bs.TransformScaleX; } if (pts.Length > 1) float.TryParse(pts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out bs.TransformScaleY); }
                else if (tv.StartsWith("scalex(")) { float.TryParse(tv[7..^1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out bs.TransformScaleX); }
                else if (tv.StartsWith("scaley(")) { float.TryParse(tv[7..^1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out bs.TransformScaleY); }
                else if (tv.StartsWith("skew("))
                { var inner = tv[5..^1]; var pts = inner.Split(',', StringSplitOptions.TrimEntries); if (pts.Length > 0) float.TryParse(pts[0].Replace("deg", ""), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out bs.TransformSkewX); if (pts.Length > 1) float.TryParse(pts[1].Replace("deg", ""), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out bs.TransformSkewY); }
                else if (tv.StartsWith("skewx(")) float.TryParse(tv[6..^1].Replace("deg", ""), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out bs.TransformSkewX);
                else if (tv.StartsWith("skewy(")) float.TryParse(tv[6..^1].Replace("deg", ""), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out bs.TransformSkewY);
                break;
            case "transform-origin":
                var toparts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (toparts.Length > 0) { if (toparts[0] == "center") bs.TransformOriginX = 0.5f; else if (toparts[0] is "left" or "top") bs.TransformOriginX = 0; else if (toparts[0] is "right" or "bottom") bs.TransformOriginX = 1f; else bs.TransformOriginX = ParsePx(toparts[0]) / 100f; }
                if (toparts.Length > 1) { if (toparts[1] == "center") bs.TransformOriginY = 0.5f; else if (toparts[1] is "left" or "top") bs.TransformOriginY = 0; else if (toparts[1] is "right" or "bottom") bs.TransformOriginY = 1f; else bs.TransformOriginY = ParsePx(toparts[1]) / 100f; }
                break;
            case "filter":
                if (value.ToLowerInvariant() == "none") { bs.Filter = "none"; bs.FilterBlur = 0; break; }
                bs.Filter = value.ToLowerInvariant();
                var fmr = System.Text.RegularExpressions.Regex.Match(value, @"blur\(([\d.]+)px?\)");
                if (fmr.Success) { float fb = 0; if (float.TryParse(fmr.Groups[1].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out fb)) bs.FilterBlur = fb; }
                break;
            case "mix-blend-mode": bs.MixBlendMode = value.ToLowerInvariant(); break;
            case "backdrop-filter": bs.BackdropFilter = value.ToLowerInvariant(); break;
            case "object-fit": bs.ObjectFit = value.ToLowerInvariant(); break;
            case "object-position": bs.ObjectPosition = value; break;
            case "image-rendering": bs.ImageRendering = value.ToLowerInvariant(); break;
            case "cursor": bs.Cursor = value.ToLowerInvariant(); break;
        }
    }

    private static void ApplyFour(BoxStyle bs, string which, string value)
    {
        var pts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(x => ParsePx(x, bs.FontSize)).ToArray();
        if (pts.Length == 0) return;
        float t, r, b, l;
        if (which == "margin") { t = bs.MarginTop; r = bs.MarginRight; b = bs.MarginBottom; l = bs.MarginLeft; }
        else { t = bs.PaddingTop; r = bs.PaddingRight; b = bs.PaddingBottom; l = bs.PaddingLeft; }
        t = b = l = r = pts[0];
        if (pts.Length >= 2) { t = b = pts[0]; l = r = pts[1]; }
        if (pts.Length >= 3) { t = pts[0]; l = r = pts[1]; b = pts[2]; }
        if (pts.Length >= 4) { t = pts[0]; r = pts[1]; b = pts[2]; l = pts[3]; }
        if (which == "margin") { bs.MarginTop = t; bs.MarginRight = r; bs.MarginBottom = b; bs.MarginLeft = l; }
        else { bs.PaddingTop = t; bs.PaddingRight = r; bs.PaddingBottom = b; bs.PaddingLeft = l; }
    }

    private static void ParseBorder(BoxStyle bs, string value)
    {
        var parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        foreach (var p in parts)
        {
            var pl = p.ToLowerInvariant();
            if (pl is "solid" or "dotted" or "dashed" or "double" or "none" or "inset" or "outset" or "ridge" or "groove")
            { bs.BorderTopStyle = bs.BorderBottomStyle = bs.BorderLeftStyle = bs.BorderRightStyle = pl; }
            else if (pl.StartsWith('#') || pl.StartsWith("rgb") || NamedColors.ContainsKey(pl))
            { var c = ParseCssColor(p); bs.BorderColor = bs.BorderTopColor = bs.BorderBottomColor = bs.BorderLeftColor = bs.BorderRightColor = c; }
            else { var w = ParsePx(p); if (w > 0) bs.BorderTop = bs.BorderBottom = bs.BorderLeft = bs.BorderRight = w; }
        }
    }

    private static void ParseBorderSide(BoxStyle bs, string side, string value)
    {
        var parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        foreach (var p in parts)
        {
            var pl = p.ToLowerInvariant();
            if (pl is "solid" or "dotted" or "dashed" or "double" or "none")
            { if (side == "top") bs.BorderTopStyle = pl; else if (side == "bottom") bs.BorderBottomStyle = pl; else if (side == "left") bs.BorderLeftStyle = pl; else bs.BorderRightStyle = pl; }
            else if (pl.StartsWith('#') || pl.StartsWith("rgb") || NamedColors.ContainsKey(pl))
            { var c = ParseCssColor(p); if (side == "top") bs.BorderTopColor = c; else if (side == "bottom") bs.BorderBottomColor = c; else if (side == "left") bs.BorderLeftColor = c; else bs.BorderRightColor = c; bs.BorderColor = c; }
            else { var w = ParsePx(p); if (w > 0) { if (side == "top") bs.BorderTop = w; else if (side == "bottom") bs.BorderBottom = w; else if (side == "left") bs.BorderLeft = w; else bs.BorderRight = w; } }
        }
    }

    private static void ParseBorderWidths(BoxStyle bs, string value)
    {
        var pts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var w = ParsePx(pts[0]); bs.BorderTop = bs.BorderBottom = bs.BorderLeft = bs.BorderRight = w;
        if (pts.Length >= 2) { bs.BorderTop = bs.BorderBottom = ParsePx(pts[0]); bs.BorderLeft = bs.BorderRight = ParsePx(pts[1]); }
        if (pts.Length >= 3) { bs.BorderTop = ParsePx(pts[0]); bs.BorderLeft = bs.BorderRight = ParsePx(pts[1]); bs.BorderBottom = ParsePx(pts[2]); }
        if (pts.Length >= 4) { bs.BorderTop = ParsePx(pts[0]); bs.BorderRight = ParsePx(pts[1]); bs.BorderBottom = ParsePx(pts[2]); bs.BorderLeft = ParsePx(pts[3]); }
    }

    private static void ParseBorderStyles(BoxStyle bs, string value)
    {
        var pts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        bs.BorderTopStyle = bs.BorderBottomStyle = bs.BorderLeftStyle = bs.BorderRightStyle = pts[0].ToLowerInvariant();
        if (pts.Length >= 2) { bs.BorderTopStyle = bs.BorderBottomStyle = pts[0].ToLowerInvariant(); bs.BorderLeftStyle = bs.BorderRightStyle = pts[1].ToLowerInvariant(); }
        if (pts.Length >= 3) { bs.BorderTopStyle = pts[0].ToLowerInvariant(); bs.BorderLeftStyle = bs.BorderRightStyle = pts[1].ToLowerInvariant(); bs.BorderBottomStyle = pts[2].ToLowerInvariant(); }
        if (pts.Length >= 4) { bs.BorderTopStyle = pts[0].ToLowerInvariant(); bs.BorderRightStyle = pts[1].ToLowerInvariant(); bs.BorderBottomStyle = pts[2].ToLowerInvariant(); bs.BorderLeftStyle = pts[3].ToLowerInvariant(); }
    }

    private static void ParseBorderColors(BoxStyle bs, string value)
    {
        var pts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var c = ParseCssColor(pts[0]);
        bs.BorderColor = bs.BorderTopColor = bs.BorderBottomColor = bs.BorderLeftColor = bs.BorderRightColor = c;
        if (pts.Length >= 2) { bs.BorderTopColor = bs.BorderBottomColor = ParseCssColor(pts[0]); bs.BorderLeftColor = bs.BorderRightColor = ParseCssColor(pts[1]); }
        if (pts.Length >= 3) { bs.BorderTopColor = ParseCssColor(pts[0]); bs.BorderLeftColor = bs.BorderRightColor = ParseCssColor(pts[1]); bs.BorderBottomColor = ParseCssColor(pts[2]); }
        if (pts.Length >= 4) { bs.BorderTopColor = ParseCssColor(pts[0]); bs.BorderRightColor = ParseCssColor(pts[1]); bs.BorderBottomColor = ParseCssColor(pts[2]); bs.BorderLeftColor = ParseCssColor(pts[3]); }
    }

    private static void ApplyBorderRadius(BoxStyle bs, string value)
    {
        var horizontal = value.Split('/', 2)[0].Trim();
        var parts = horizontal.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return;

        var tl = ParseRadius(parts[0]);
        var tr = parts.Length >= 2 ? ParseRadius(parts[1]) : tl;
        var br = parts.Length >= 3 ? ParseRadius(parts[2]) : tl;
        var bl = parts.Length >= 4 ? ParseRadius(parts[3]) : tr;

        bs.BorderRadius = tl.value;
        bs.BorderRadiusIsPercent = tl.isPercent;
        SetCornerRadius(bs, "top-left", tl);
        SetCornerRadius(bs, "top-right", tr);
        SetCornerRadius(bs, "bottom-right", br);
        SetCornerRadius(bs, "bottom-left", bl);
    }

    private static void SetCornerRadius(BoxStyle bs, string corner, string value)
    {
        var first = value.Split('/', 2)[0].Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        if (string.IsNullOrEmpty(first)) return;
        SetCornerRadius(bs, corner, ParseRadius(first));
    }

    private static void SetCornerRadius(BoxStyle bs, string corner, (float value, bool isPercent) radius)
    {
        switch (corner)
        {
            case "top-left":
                bs.BorderTopLeftRadius = radius.value;
                bs.BorderTopLeftRadiusIsPercent = radius.isPercent;
                break;
            case "top-right":
                bs.BorderTopRightRadius = radius.value;
                bs.BorderTopRightRadiusIsPercent = radius.isPercent;
                break;
            case "bottom-right":
                bs.BorderBottomRightRadius = radius.value;
                bs.BorderBottomRightRadiusIsPercent = radius.isPercent;
                break;
            case "bottom-left":
                bs.BorderBottomLeftRadius = radius.value;
                bs.BorderBottomLeftRadiusIsPercent = radius.isPercent;
                break;
        }
    }

    private static (float value, bool isPercent) ParseRadius(string value)
    {
        value = value.Trim().ToLowerInvariant();
        if (value.EndsWith("%", StringComparison.Ordinal) &&
            float.TryParse(value[..^1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var pct))
            return (Math.Max(0, pct / 100f), true);

        return (Math.Max(0, ParsePx(value)), false);
    }

    private static void ParseBackground(BoxStyle bs, string value)
    {
        if (value.Contains("linear-gradient") || value.Contains("radial-gradient"))
        {
            bs.BackgroundGradient = value;
            return;
        }
        var parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        foreach (var p in parts)
        {
            var pl = p.ToLowerInvariant();
            if (pl.StartsWith('#') || pl.StartsWith("rgb") || NamedColors.ContainsKey(pl)) bs.BackgroundColor = ParseCssColor(p);
            else if (pl.StartsWith("url(")) bs.BackgroundImage = pl[4..^1].Trim('\'', '"');
            else if (pl is "repeat" or "repeat-x" or "repeat-y" or "no-repeat") bs.BackgroundRepeat = pl;
            else if (pl is "cover" or "contain") bs.BackgroundSize = pl;
            else if (pl is "border-box" or "padding-box" or "content-box") { if (bs.BackgroundOrigin == "padding-box") bs.BackgroundOrigin = pl; else bs.BackgroundClip = pl; }
            else if (pl is "scroll" or "fixed" or "local") bs.BackgroundAttachment = pl;
        }
    }

    private static float ParsePx(string v, float def = 0)
    {
        if (string.IsNullOrEmpty(v)) return def; v = v.Trim().ToLowerInvariant();
        if (v.EndsWith("px") && float.TryParse(v[..^2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var r)) return r;
        if (v.EndsWith("em") && float.TryParse(v[..^2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var e)) return e * 12;
        if (v.EndsWith("pt") && float.TryParse(v[..^2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var p)) return p * 1.333f;
        if (v == "0" || v == "0px") return 0;
        return def;
    }

    private static float ParseLineH(string v, float fs)
    {
        if (string.IsNullOrEmpty(v) || v == "normal") return 0;
        if (float.TryParse(v, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var n)) return n * fs;
        return ParsePx(v, fs * 1.4f);
    }

    private static string ParseFont(string v)
    {
        if (string.IsNullOrEmpty(v)) return "Segoe UI";
        foreach (var p in v.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var c = p.Trim('\'', '"').ToLowerInvariant();
            if (c == "serif") return "Times New Roman"; if (c is "sans-serif" or "system-ui") return "Segoe UI"; if (c == "monospace") return "Consolas";
            try { using var f = new Font(c, 12); return c; } catch { continue; }
        }
        return "Segoe UI";
    }

    private static readonly Dictionary<string, Color> NamedColors = new(StringComparer.OrdinalIgnoreCase)
    { ["black"]=Color.Black,["white"]=Color.White,["red"]=Color.Red,["blue"]=Color.Blue,["green"]=Color.Green,["yellow"]=Color.Yellow,["gray"]=Color.Gray,["grey"]=Color.Gray,["orange"]=Color.Orange,["purple"]=Color.Purple,["pink"]=Color.Pink,["brown"]=Color.Brown,["navy"]=Color.Navy,["teal"]=Color.Teal,["silver"]=Color.Silver,["transparent"]=Color.Transparent,["aqua"]=Color.Aqua,["fuchsia"]=Color.Fuchsia,["lime"]=Color.Lime,["olive"]=Color.Olive,["darkgray"]=Color.DarkGray,["darkgrey"]=Color.DarkGray,["dimgray"]=Color.DimGray,["lightgray"]=Color.LightGray,["lightgrey"]=Color.LightGray,["darkred"]=Color.DarkRed,["darkgreen"]=Color.DarkGreen,["darkblue"]=Color.DarkBlue,["darkorange"]=Color.DarkOrange,["gold"]=Color.Gold,["violet"]=Color.Violet,["indigo"]=Color.Indigo,["coral"]=Color.Coral,["tomato"]=Color.Tomato,["salmon"]=Color.Salmon,["khaki"]=Color.Khaki,["crimson"]=Color.Crimson };

    private static Color ParseCssColor(string? v)
    {
        if (string.IsNullOrEmpty(v)) return Color.Black; v = v.Trim().ToLowerInvariant();
        if (v.StartsWith('#'))
        {
            var h = v[1..];
            if (h.Length == 3) return Color.FromArgb((byte)(Conv(h[0..1]) * 17), (byte)(Conv(h[1..2]) * 17), (byte)(Conv(h[2..3]) * 17));
            if (h.Length == 6) return Color.FromArgb(Conv(h[0..2]), Conv(h[2..4]), Conv(h[4..6]));
            if (h.Length == 8) return Color.FromArgb(Conv(h[6..8]), Conv(h[0..2]), Conv(h[2..4]), Conv(h[4..6]));
        }
        var mr = System.Text.RegularExpressions.Regex.Match(v, @"rgba?\s*\(\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)\s*(?:,\s*([\d.]+)\s*)?\)");
        if (mr.Success) return Color.FromArgb(mr.Groups[4].Success ? (int)(float.Parse(mr.Groups[4].Value) * 255) : 255, int.Parse(mr.Groups[1].Value), int.Parse(mr.Groups[2].Value), int.Parse(mr.Groups[3].Value));
        var mh = System.Text.RegularExpressions.Regex.Match(v, @"hsla?\s*\(\s*([\d.]+)\s*,\s*([\d.]+)%\s*,\s*([\d.]+)%\s*(?:,\s*([\d.]+)\s*)?\)");
        if (mh.Success) return Hsl(float.Parse(mh.Groups[1].Value), float.Parse(mh.Groups[2].Value) / 100f, float.Parse(mh.Groups[3].Value) / 100f, mh.Groups[4].Success ? (int)(float.Parse(mh.Groups[4].Value) * 255) : 255);
        return NamedColors.TryGetValue(v, out var c) ? c : Color.Black;
        static byte Conv(string h) => Convert.ToByte(h, 16);
    }

    private static Color? ParseCssColorOrNull(string? v)
    {
        if (string.IsNullOrEmpty(v) || v.Trim().ToLowerInvariant() is "transparent" or "rgba(0, 0, 0, 0)") return null;
        return ParseCssColor(v);
    }

    private static Color Hsl(float h, float s, float l, int a = 255)
    {
        if (s == 0) { var g = (byte)(l * 255); return Color.FromArgb(a, g, g, g); }
        float H2R(float t) { if (t < 0) t += 1; if (t > 1) t -= 1; if (t < 1f / 6) return l + s * (1 - Math.Abs(2 * l - 1)) * 6 * t; if (t < 0.5f) return l + s * (1 - Math.Abs(2 * l - 1)); if (t < 2f / 3) return l + s * (1 - Math.Abs(2 * l - 1)) * (2f / 3 - t) * 6; return l; }
        return Color.FromArgb(a, (byte)(H2R(h / 360 + 1f / 3) * 255), (byte)(H2R(h / 360) * 255), (byte)(H2R(h / 360 - 1f / 3) * 255));
    }

    private void SaveFormState()
    {
        _formState.Clear();
        if (_layout == null) return;
        foreach (var b in AllBoxesFlat(_layout))
            if (b.IsInput && b.Source != null)
                _formState[b.Source] = (b.InputValue ?? "", b.InputChecked);
    }

    private void RestoreFormState()
    {
        if (_layout == null || _formState.Count == 0) return;
        foreach (var b in AllBoxesFlat(_layout))
            if (b.IsInput && b.Source != null && _formState.TryGetValue(b.Source, out var st))
            {
                b.InputValue = st.val;
                b.InputChecked = st.chk;
            }
    }

    private static IEnumerable<LayoutBox> AllBoxesFlat(LayoutBox root)
    {
        yield return root;
        foreach (var c in root.Children)
            foreach (var cc in AllBoxesFlat(c))
                yield return cc;
    }

    private void UpdateScrollbar()
    {
        if (_layout == null) { _scrollBar.Visible = false; return; }
        var max = (int)Math.Max(0, _layout.Bounds.Height - RenderHeight);
        if (max <= 0) { _scrollBar.Visible = false; return; }
        _scrollBar.Visible = true; _scrollBar.Minimum = 0; _scrollBar.Maximum = max;
        _scrollBar.Value = Math.Min((int)_scrollY, max);
    }

    private void RelayoutOrResize()
    {
        if (_doc == null) return;

        SaveFormState();
        var oldFocusSource = _focusedInput?.Source;

        var styles = StyleComputer.BuildStyleMap(_doc);
        var eng = new LayoutEngine(RenderWidth, styles);
        QueueBackgroundImages(styles, eng.PendingImages);
        _layout = _doc.Body != null ? eng.Layout(_doc.Body) : null;
        _domChanged = false;
        RestoreFormState();

        _focusedInput = oldFocusSource != null && _layout != null
            ? AllBoxesFlat(_layout).FirstOrDefault(b => b.IsInput && b.Source == oldFocusSource)
            : null;

        if (_layout != null)
            _scrollY = Math.Min(_scrollY, Math.Max(0, _layout.Bounds.Height - RenderHeight));
        UpdateScrollbar(); _renderPanel.Invalidate();
        if (eng.PendingImages.Count > 0) _ = LoadImagesAsync(eng.PendingImages);
    }

    private async Task LoadImagesAsync(List<string> urls)
    {
        foreach (var u in urls)
        {
            var img = await ImageLoader.FetchAsync(u);
            if (img != null && _layout != null)
            {
                void Upd(LayoutBox b) { if (b.IsImage && b.ImageUrl == u) { b.ImageData = img; b.Bounds = new RectangleF(b.Bounds.X, b.Bounds.Y, img.Width, img.Height); } foreach (var c in b.Children) Upd(c); }
                Upd(_layout); BeginInvoke(() => _renderPanel.Invalidate());
            }
        }
    }

    private static void QueueBackgroundImages(Dictionary<IElement, BoxStyle> styles, List<string> pending)
    {
        foreach (var (el, style) in styles)
        {
            if (string.IsNullOrWhiteSpace(style.BackgroundImage) || style.BackgroundImage == "none") continue;
            var url = RenderEngine.ResolveBackgroundUrl(el, style.BackgroundImage);
            if (string.IsNullOrWhiteSpace(url)) continue;
            if (ImageLoader.GetCached(url) != null) continue;
            if (!pending.Contains(url)) pending.Add(url);
        }
    }

    private void RenderPanel_Click(object sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left || _layout == null) return;
        if (_suppressNextClick) { _suppressNextClick = false; return; }
        _renderPanel.Focus(); var pt = new PointF(e.X, e.Y + _scrollY);
        var box = HitTest(_layout, pt);
        if (box == null || (!box.IsLink && !box.IsInput)) { _focusedInput = null; _renderPanel.Invalidate(); if (box == null) return; }
        if (box.IsLink && !string.IsNullOrEmpty(box.Href)) { _focusedInput = null; try { Navigate(new Uri(new Uri(_currentUrl), box.Href).ToString()); } catch { Navigate(box.Href); } return; }
        if (box.IsInput) { HandleInput(box); CheckAndApplyDocWrite(); return; }
        if (box.Source != null) { ExecOnClick(box.Source); CheckAndApplyDocWrite(); }
    }

    private void HandleInput(LayoutBox box)
    {
        if (box.Source != null) ExecOnClick(box.Source);
        switch (box.InputType ?? "text")
        {
            case "checkbox":
                box.InputChecked = !box.InputChecked;
                SyncInputToDom(box, fireInput: true, fireChange: true);
                _renderPanel.Invalidate();
                break;
            case "radio":
                if (_layout != null)
                    UncheckRadioGroup(_layout, box.InputName);
                box.InputChecked = true;
                SyncRadioGroupToDom(box.InputName);
                SyncInputToDom(box, fireInput: true, fireChange: true);
                _renderPanel.Invalidate();
                break;
            case "select":
                SelectNextOption(box);
                SyncInputToDom(box, fireInput: true, fireChange: true);
                _renderPanel.Invalidate();
                break;
            case "submit": case "button": if (box.Source != null) HandleForm(box.Source); break;
            default: _focusedInput = box; SyncInputToDom(box); _renderPanel.Invalidate(); break;
        }
    }

    private static void UncheckRadioGroup(LayoutBox root, string? name)
    {
        if (root.IsInput && root.InputType == "radio" && root.InputName == name)
            root.InputChecked = false;
        foreach (var c in root.Children)
            UncheckRadioGroup(c, name);
    }

    private void SyncRadioGroupToDom(string? name)
    {
        if (_layout == null) return;
        foreach (var b in AllBoxesFlat(_layout).Where(b => b.IsInput && b.InputType == "radio" && b.InputName == name))
            SyncInputToDom(b);
    }

    private void SelectNextOption(LayoutBox box)
    {
        if (box.Source == null) return;
        var options = box.Source.QuerySelectorAll("option").ToArray();
        if (options.Length == 0) return;
        var current = Array.FindIndex(options, o => (o.GetAttribute("value") ?? o.TextContent.Trim()) == (box.InputValue ?? ""));
        var next = (current + 1 + options.Length) % options.Length;
        foreach (var option in options) option.RemoveAttribute("selected");
        options[next].SetAttribute("selected", "");
        box.InputValue = options[next].GetAttribute("value") ?? options[next].TextContent.Trim();
    }

    private void SyncInputToDom(LayoutBox box, bool fireInput = false, bool fireChange = false)
    {
        var el = box.Source;
        if (el == null) return;
        var tag = el.TagName.ToLowerInvariant();
        var type = box.InputType ?? "text";

        if (type is "checkbox" or "radio")
        {
            if (box.InputChecked) el.SetAttribute("checked", "");
            else el.RemoveAttribute("checked");
        }
        else if (type == "select")
        {
            var value = box.InputValue ?? "";
            foreach (var option in el.QuerySelectorAll("option"))
            {
                var optionValue = option.GetAttribute("value") ?? option.TextContent.Trim();
                if (optionValue == value) option.SetAttribute("selected", "");
                else option.RemoveAttribute("selected");
            }
        }
        else
        {
            var value = box.InputValue ?? "";
            el.SetAttribute("value", value);
            if (tag == "textarea")
            {
                el.TextContent = value;
                box.Text = value;
            }
        }

        if (fireInput) ExecInlineHandler(el, "oninput");
        if (fireChange) ExecInlineHandler(el, "onchange");
    }

    private void ExecInlineHandler(IElement el, string attr)
    {
        var code = el.GetAttribute(attr);
        if (!string.IsNullOrWhiteSpace(code))
            _jsEngine?.ExecClick(code);
    }

    private void HandleForm(IElement el)
    {
        var form = el.Ancestors().OfType<IElement>().FirstOrDefault(e => e.TagName.Equals("FORM", StringComparison.OrdinalIgnoreCase));
        if (form == null) return; var action = form.GetAttribute("action");
        if (string.IsNullOrEmpty(action)) return;
        try { Navigate(new Uri(new Uri(_currentUrl), action).ToString()); } catch { }
    }

    private void ExecOnClick(IElement? el)
    {
        while (el != null) { var oc = el.GetAttribute("onclick"); if (!string.IsNullOrEmpty(oc)) { _jsEngine?.ExecClick(oc); AppendConsole($"click: {oc}"); return; } el = el.ParentElement; }
    }

    private void AppendConsole(string m) { if (_consoleBox.IsHandleCreated) BeginInvoke(() => { _consoleBox.AppendText(m + "\r\n"); _consoleBox.SelectionStart = _consoleBox.TextLength; _consoleBox.ScrollToCaret(); }); }

    private void CheckAndApplyDocWrite()
    {
        if (_jsEngine == null) return;
        var dw = _jsEngine.GetAndClearDocWrite();
        if (dw.Length > 0 && _doc?.Body != null)
        {
            var temp = _doc.CreateElement("div");
            temp.InnerHtml = dw;
            foreach (var child in temp.Children.ToArray())
                _doc.Body.AppendChild(child);
            _sourceBox.Text = _doc.DocumentElement?.OuterHtml ?? _rawHtml;
        }
        RelayoutOrResize();
    }

    private void ExecuteConsoleJs()
    {
        if (_jsEngine == null || string.IsNullOrWhiteSpace(_consoleInput.Text)) return;
        var code = _consoleInput.Text.Trim();
        AppendConsole($"> {code}");
        _consoleInput.Clear();
        _jsEngine.Exec(code);
        CheckAndApplyDocWrite();
    }

    private static LayoutBox? HitTest(LayoutBox root, PointF pt) { LayoutBox? f = null; void Rec(LayoutBox b) { if (f != null) return; if (b.Bounds.Contains(pt) && (b.Text.Length > 0 || b.IsImage || b.IsInput)) f = b; foreach (var c in b.Children) Rec(c); } Rec(root); return f; }

    private void RenderPanel_MouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left || _layout == null) return;
        _renderPanel.Focus();
        var pt = new PointF(e.X, e.Y + _scrollY);
        var hit = HitTestText(_layout, pt, allowNearest: false);
        if (hit.HasValue)
        {
            _isSelectingText = true;
            _selectionAnchor = hit;
            _selectionFocus = hit;
            _selectedText = "";
            _focusedInput = null;
            _renderPanel.Cursor = Cursors.IBeam;
            _renderPanel.Invalidate();
            return;
        }

        ClearTextSelection();
    }

    private void RenderPanel_MouseMove(object sender, MouseEventArgs e)
    {
        if (_layout == null) return;
        var pt = new PointF(e.X, e.Y + _scrollY);

        if (_isSelectingText && _selectionAnchor.HasValue)
        {
            var hit = HitTestText(_layout, pt, allowNearest: true);
            if (hit.HasValue)
            {
                _selectionFocus = hit;
                _selectedText = BuildSelectedText();
                _renderPanel.Invalidate();
            }
            _renderPanel.Cursor = Cursors.IBeam;
            return;
        }

        var textHit = HitTestText(_layout, pt, allowNearest: false);
        if (textHit.HasValue) { _renderPanel.Cursor = Cursors.IBeam; return; }

        var b = HitTest(_layout, pt);
        _renderPanel.Cursor = (b != null && (b.IsLink || b.IsInput)) ? Cursors.Hand : Cursors.Default;
    }

    private void RenderPanel_MouseUp(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        if (_isSelectingText)
        {
            _isSelectingText = false;
            _selectedText = BuildSelectedText();
            _suppressNextClick = _selectedText.Length > 0;
            _renderPanel.Invalidate();
        }
    }

    private void ClearTextSelection()
    {
        if (_selectionAnchor == null && _selectionFocus == null && _selectedText.Length == 0) return;
        _selectionAnchor = null;
        _selectionFocus = null;
        _selectedText = "";
        _renderPanel.Invalidate();
    }

    private static IEnumerable<LayoutBox> SelectableTextBoxes(LayoutBox root)
    {
        foreach (var box in AllBoxesFlat(root))
        {
            if (box.IsInput || string.IsNullOrEmpty(box.Text)) continue;
            if (box.Style?.UserSelect == "none") continue;
            yield return box;
        }
    }

    private static TextHit? HitTestText(LayoutBox root, PointF pt, bool allowNearest)
    {
        var boxes = SelectableTextBoxes(root).ToList();
        if (boxes.Count == 0) return null;

        foreach (var box in boxes)
        {
            var expanded = box.Bounds;
            expanded.Inflate(2, 2);
            if (expanded.Contains(pt))
                return new TextHit(box, TextOffsetFromX(box, pt.X));
        }

        var line = boxes
            .Where(b => pt.Y >= b.Bounds.Top && pt.Y <= b.Bounds.Bottom)
            .OrderBy(b => b.Bounds.Left)
            .ToList();
        if (line.Count > 0)
        {
            var first = line[0];
            var last = line[^1];
            if (pt.X <= first.Bounds.Left) return new TextHit(first, 0);
            if (pt.X >= last.Bounds.Right) return new TextHit(last, last.Text.Length);

            var nearest = line
                .OrderBy(b => pt.X < b.Bounds.Left ? b.Bounds.Left - pt.X : pt.X - b.Bounds.Right)
                .First();
            return new TextHit(nearest, TextOffsetFromX(nearest, pt.X));
        }

        if (!allowNearest) return null;

        var ordered = boxes.OrderBy(b => b.Bounds.Top).ThenBy(b => b.Bounds.Left).ToList();
        if (pt.Y <= ordered[0].Bounds.Top) return new TextHit(ordered[0], 0);
        if (pt.Y >= ordered[^1].Bounds.Bottom) return new TextHit(ordered[^1], ordered[^1].Text.Length);

        var closest = ordered
            .OrderBy(b => Math.Abs(pt.Y - (b.Bounds.Top + b.Bounds.Bottom) / 2f))
            .ThenBy(b => Math.Abs(pt.X - (b.Bounds.Left + b.Bounds.Right) / 2f))
            .First();
        return new TextHit(closest, TextOffsetFromX(closest, pt.X));
    }

    private static int TextOffsetFromX(LayoutBox box, float x)
    {
        if (x <= box.Bounds.Left) return 0;
        if (x >= box.Bounds.Right) return box.Text.Length;

        var pen = box.Bounds.Left;
        var offset = 0;
        foreach (var element in EnumerateTextElements(box.Text))
        {
            var width = TextMeasurer.MeasureWidth(element, box.FontName, box.FontSize, box.Bold, box.Italic);
            if (x < pen + width / 2f) return offset;
            pen += width;
            offset += element.Length;
        }

        return box.Text.Length;
    }

    private string BuildSelectedText()
    {
        if (_layout == null || !_selectionAnchor.HasValue || !_selectionFocus.HasValue) return "";

        var boxes = SelectableTextBoxes(_layout).ToList();
        var anchorIndex = boxes.FindIndex(b => ReferenceEquals(b, _selectionAnchor.Value.Box));
        var focusIndex = boxes.FindIndex(b => ReferenceEquals(b, _selectionFocus.Value.Box));
        if (anchorIndex < 0 || focusIndex < 0) return "";

        var start = _selectionAnchor.Value;
        var end = _selectionFocus.Value;
        var startIndex = anchorIndex;
        var endIndex = focusIndex;
        if (startIndex > endIndex || (startIndex == endIndex && start.Offset > end.Offset))
        {
            (start, end) = (end, start);
            (startIndex, endIndex) = (endIndex, startIndex);
        }

        if (startIndex == endIndex && start.Offset == end.Offset) return "";

        var sb = new StringBuilder();
        for (var i = startIndex; i <= endIndex; i++)
        {
            var box = boxes[i];
            var from = i == startIndex ? Math.Clamp(start.Offset, 0, box.Text.Length) : 0;
            var to = i == endIndex ? Math.Clamp(end.Offset, 0, box.Text.Length) : box.Text.Length;
            if (to > from) sb.Append(box.Text[from..to]);

            if (i < endIndex && IsVisualLineBreak(box, boxes[i + 1]) && (sb.Length == 0 || !char.IsWhiteSpace(sb[^1])))
                sb.AppendLine();
        }

        return sb.ToString();
    }

    private static bool IsVisualLineBreak(LayoutBox current, LayoutBox next)
        => next.Bounds.Top > current.Bounds.Top + Math.Max(4, current.Bounds.Height * 0.55f);

    private static float MeasureTextPrefix(LayoutBox box, int charOffset)
    {
        if (charOffset <= 0) return 0;
        if (charOffset >= box.Text.Length) return TextMeasurer.MeasureWidth(box.Text, box.FontName, box.FontSize, box.Bold, box.Italic);

        var width = 0f;
        var offset = 0;
        foreach (var element in EnumerateTextElements(box.Text))
        {
            if (offset + element.Length > charOffset) break;
            width += TextMeasurer.MeasureWidth(element, box.FontName, box.FontSize, box.Bold, box.Italic);
            offset += element.Length;
        }
        return width;
    }

    private void DrawTextSelection(SKCanvas canvas)
    {
        if (_layout == null || !_selectionAnchor.HasValue || !_selectionFocus.HasValue || _selectedText.Length == 0) return;

        var boxes = SelectableTextBoxes(_layout).ToList();
        var anchorIndex = boxes.FindIndex(b => ReferenceEquals(b, _selectionAnchor.Value.Box));
        var focusIndex = boxes.FindIndex(b => ReferenceEquals(b, _selectionFocus.Value.Box));
        if (anchorIndex < 0 || focusIndex < 0) return;

        var start = _selectionAnchor.Value;
        var end = _selectionFocus.Value;
        var startIndex = anchorIndex;
        var endIndex = focusIndex;
        if (startIndex > endIndex || (startIndex == endIndex && start.Offset > end.Offset))
        {
            (start, end) = (end, start);
            (startIndex, endIndex) = (endIndex, startIndex);
        }

        using var paint = new SKPaint { Color = new SKColor(0, 95, 213, 95), Style = SKPaintStyle.Fill, IsAntialias = false };
        for (var i = startIndex; i <= endIndex; i++)
        {
            var box = boxes[i];
            var from = i == startIndex ? Math.Clamp(start.Offset, 0, box.Text.Length) : 0;
            var to = i == endIndex ? Math.Clamp(end.Offset, 0, box.Text.Length) : box.Text.Length;
            if (to <= from) continue;

            var left = box.Bounds.Left + MeasureTextPrefix(box, from);
            var right = box.Bounds.Left + MeasureTextPrefix(box, to);
            var top = box.Bounds.Top - _scrollY;
            var bottom = box.Bounds.Bottom - _scrollY;
            canvas.DrawRect(new SKRect(left, top, right, bottom), paint);
        }
    }

    private static IEnumerable<string> EnumerateTextElements(string text)
    {
        var enumerator = StringInfo.GetTextElementEnumerator(text);
        while (enumerator.MoveNext())
            yield return enumerator.GetTextElement();
    }

    private void RenderPanel_KeyPress(object? sender, KeyPressEventArgs e)
    {
        if (_focusedInput == null) return; var fi = _focusedInput; var t = fi.InputType ?? "text";
        if (t is "checkbox" or "radio" or "submit" or "button") return;
        if (!char.IsControl(e.KeyChar)) { fi.InputValue += e.KeyChar; if (fi.InputType == "textarea") fi.Text = fi.InputValue; SyncInputToDom(fi, fireInput: true); _renderPanel.Invalidate(); }
        e.Handled = true;
    }

    private void RenderPanel_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Control && e.KeyCode == Keys.C && _selectedText.Length > 0)
        {
            try { Clipboard.SetText(_selectedText); }
            catch { }
            e.Handled = true;
            e.SuppressKeyPress = true;
            return;
        }

        if (_focusedInput == null) return;
#pragma warning disable CS8602
        switch (e.KeyCode)
        {
            case Keys.Back: if (_focusedInput.InputValue.Length > 0) _focusedInput.InputValue = _focusedInput.InputValue[..^1]; if (_focusedInput.InputType == "textarea") _focusedInput.Text = _focusedInput.InputValue; SyncInputToDom(_focusedInput, fireInput: true); _renderPanel.Invalidate(); e.Handled = true; break;
            case Keys.Enter: if (_focusedInput.InputType == "textarea") { _focusedInput.InputValue += "\n"; _focusedInput.Text = _focusedInput.InputValue; SyncInputToDom(_focusedInput, fireInput: true); _renderPanel.Invalidate(); } else { SyncInputToDom(_focusedInput, fireChange: true); _focusedInput = null; } e.Handled = true; break;
            case Keys.Escape: _focusedInput = null; _renderPanel.Invalidate(); e.Handled = true; break;
        }
#pragma warning restore CS8602
    }

    private void RenderPanel_MouseWheel(object? sender, MouseEventArgs e)
    {
        if (_layout == null || _viewTabs.SelectedIndex != 0) return;
        if (!_renderPanel.ClientRectangle.Contains(_renderPanel.PointToClient(Cursor.Position))) return;
        _scrollY = Math.Max(0, Math.Min(_scrollY - e.Delta / 3, Math.Max(0, _layout.Bounds.Height - RenderHeight)));
        if (_scrollBar.Visible) _scrollBar.Value = (int)_scrollY; _renderPanel.Invalidate();
    }

    private LayoutBox CreateErrorLayout(string message)
    {
        var root = new LayoutBox { IsBlock = true, Bounds = new RectangleF(0, 0, RenderWidth, 0) };
        var y = 8f; var x = 8f; var words = message.Split(' ');
        for (int i = 0; i < words.Length; i++)
        {
            var w = words[i] + (i < words.Length - 1 ? " " : ""); if (w.Trim().Length == 0) continue;
            var (mw, mh) = TextMeasurer.Measure(w, "Segoe UI", 12); var sz = new SizeF(mw, mh);
            if (x + sz.Width > RenderWidth && x > 8) { y += sz.Height + 2; x = 8; }
            root.Children.Add(new LayoutBox { Text = w, Bounds = new RectangleF(x, y, sz.Width, sz.Height), FontSize = 12 }); x += sz.Width;
        }
        root.Bounds = new RectangleF(0, 0, RenderWidth, y + 30); return root;
    }

    private void RenderPanel_Paint(object? sender, PaintEventArgs e)
    {
        var w = _renderPanel.ClientSize.Width;
        var h = _renderPanel.ClientSize.Height;
        if (w <= 0 || h <= 0) return;

        using var skBitmap = new SKBitmap(w, h, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(skBitmap);

        if (_layout == null)
        {
            canvas.Clear(SKColors.White);
            using var p = new SKPaint { Color = new SKColor(128, 128, 128), TextSize = 14, IsAntialias = true, Typeface = SKTypeface.FromFamilyName("Segoe UI") };
            var tw = p.MeasureText("Loading...");
            p.GetFontMetrics(out var fm);
            canvas.DrawText("Loading...", (w - tw) / 2, h / 2 - fm.Ascent / 2, p);
        }
        else
        {
            var re = new RenderEngine(_layout);
            re.SetFocus(_focusedInput);
            re.Render(canvas, _renderPanel.ClientSize, _scrollY);
            DrawTextSelection(canvas);
        }

        using var gdiBmp = new Bitmap(w, h, skBitmap.RowBytes, System.Drawing.Imaging.PixelFormat.Format32bppArgb, skBitmap.GetPixels());
        e.Graphics.DrawImage(gdiBmp, 0, 0);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _jsEngine?.Dispose();
            _jsEngine = null;
        }
        base.Dispose(disposing);
    }
}

class DoubleBufferedPanel : Panel { public DoubleBufferedPanel() { DoubleBuffered = true; } }
