using AngleSharp.Dom;

namespace WebviewCore;

static class StyleComputer
{
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

    private static readonly Dictionary<string, Color> NamedColors = new(StringComparer.OrdinalIgnoreCase)
    {
        ["black"]=Color.Black,["white"]=Color.White,["red"]=Color.Red,["blue"]=Color.Blue,["green"]=Color.Green,
        ["yellow"]=Color.Yellow,["gray"]=Color.Gray,["grey"]=Color.Gray,["orange"]=Color.Orange,["purple"]=Color.Purple,
        ["pink"]=Color.Pink,["brown"]=Color.Brown,["navy"]=Color.Navy,["teal"]=Color.Teal,["silver"]=Color.Silver,
        ["transparent"]=Color.Transparent,["aqua"]=Color.Aqua,["fuchsia"]=Color.Fuchsia,["lime"]=Color.Lime,
        ["olive"]=Color.Olive,["darkgray"]=Color.DarkGray,["darkgrey"]=Color.DarkGray,["dimgray"]=Color.DimGray,
        ["lightgray"]=Color.LightGray,["lightgrey"]=Color.LightGray,["darkred"]=Color.DarkRed,
        ["darkgreen"]=Color.DarkGreen,["darkblue"]=Color.DarkBlue,["darkorange"]=Color.DarkOrange,
        ["gold"]=Color.Gold,["violet"]=Color.Violet,["indigo"]=Color.Indigo,["coral"]=Color.Coral,
        ["tomato"]=Color.Tomato,["salmon"]=Color.Salmon,["khaki"]=Color.Khaki,["crimson"]=Color.Crimson
    };

    public static Dictionary<IElement, BoxStyle> BuildStyleMap(IDocument doc)
    {
        var authorRules = CollectAuthorRules(doc);
        var styles = new Dictionary<IElement, BoxStyle>();

        foreach (var el in doc.All)
            styles[el] = ComputeElementStyle(el, authorRules);

        return styles;
    }

    public static BoxStyle ComputeElementStyle(IElement el)
    {
        var doc = el.Owner ?? el.Ancestors().OfType<IDocument>().FirstOrDefault();
        return ComputeElementStyle(el, doc != null ? CollectAuthorRules(doc) : new List<(string selector, Dictionary<string, string> decls)>());
    }

    private static BoxStyle ComputeElementStyle(IElement el, List<(string selector, Dictionary<string, string> decls)> authorRules)
    {
        var bs = MakeDefaultStyle(el.TagName?.ToLowerInvariant());
        foreach (var (sel, decls) in authorRules)
        {
            try
            {
                if (el.Matches(sel))
                    foreach (var (prop, val) in decls)
                        ApplyCssDecl(bs, prop, val);
            }
            catch
            {
            }
        }

        ApplyInlineStyle(bs, el.GetAttribute("style") ?? "");
        return bs;
    }

    private static List<(string selector, Dictionary<string, string> decls)> CollectAuthorRules(IDocument doc)
    {
        var authorRules = new List<(string selector, Dictionary<string, string> decls)>();
        foreach (var styleTag in doc.QuerySelectorAll("style"))
        {
            var cssText = styleTag.TextContent;
            if (!string.IsNullOrWhiteSpace(cssText))
                authorRules.AddRange(CssParser.Parse(cssText));
        }

        return authorRules;
    }

    public static BoxStyle MakeDefaultStyle(string? tag)
    {
        var bs = new BoxStyle();
        if (tag != null && DefaultDisplay.TryGetValue(tag, out var disp))
        {
            bs.DisplayNone = disp == "none";
            bs.DisplayBlock = disp is "block" or "inline-block" or "table" or "table-row" or "table-cell";
            bs.DisplayInlineBlock = disp == "inline-block";
            bs.IsTable = disp == "table";
            bs.IsTableRow = disp == "table-row";
            bs.IsTableCell = disp == "table-cell";
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

        if (tag is "td" or "th")
        {
            bs.BorderTop = bs.BorderBottom = bs.BorderLeft = bs.BorderRight = 1;
            bs.BorderColor = Color.FromArgb(180, 180, 180);
        }

        return bs;
    }

    public static void ApplyInlineStyle(BoxStyle bs, string style)
    {
        if (string.IsNullOrWhiteSpace(style)) return;
        foreach (var decl in SplitCssDeclarations(style))
        {
            var p = decl.Split(':', 2, StringSplitOptions.TrimEntries);
            if (p.Length != 2) continue;
            ApplyCssDecl(bs, p[0].Trim().ToLowerInvariant(), p[1].Trim());
        }
    }

    public static void ApplyCssDecl(BoxStyle bs, string prop, string value)
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
                var d = value.ToLowerInvariant();
                bs.DisplayNone = d == "none";
                bs.DisplayBlock = d is "block" or "inline-block" or "flex" or "inline-flex" or "table" or "table-row" or "table-cell" or "grid";
                bs.DisplayInlineBlock = d == "inline-block";
                bs.DisplayFlex = d == "flex";
                bs.DisplayInlineFlex = d == "inline-flex";
                bs.DisplayGrid = d == "grid";
                bs.IsTable = d == "table"; bs.IsTableRow = d == "table-row"; bs.IsTableCell = d == "table-cell";
                bs.IsFlexContainer = d is "flex" or "inline-flex";
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
                ApplyBoxShadow(bs, value);
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
            case "flex-flow":
                var ff = value.Split(' ');
                if (ff.Length > 0) bs.FlexDirection = ff[0].ToLowerInvariant();
                if (ff.Length > 1) bs.FlexWrap = ff[1].ToLowerInvariant();
                break;
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
                foreach (var l in value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                {
                    var ll = l.ToLowerInvariant();
                    if (ll is "inside" or "outside") bs.ListStylePosition = ll;
                    else if (ll is "disc" or "circle" or "square" or "decimal" or "none") bs.ListStyleType = ll;
                }
                break;
            case "transform":
                ApplyTransform(bs, value);
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
                if (fmr.Success && float.TryParse(fmr.Groups[1].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var fb)) bs.FilterBlur = fb;
                break;
            case "mix-blend-mode": bs.MixBlendMode = value.ToLowerInvariant(); break;
            case "backdrop-filter": bs.BackdropFilter = value.ToLowerInvariant(); break;
            case "object-fit": bs.ObjectFit = value.ToLowerInvariant(); break;
            case "object-position": bs.ObjectPosition = value; break;
            case "image-rendering": bs.ImageRendering = value.ToLowerInvariant(); break;
            case "cursor": bs.Cursor = value.ToLowerInvariant(); break;
        }
    }

    public static string NormalizePropertyName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "";
        if (name.StartsWith("--", StringComparison.Ordinal)) return name;
        if (name == "cssFloat" || name == "styleFloat") return "float";

        var chars = new List<char>(name.Length + 4);
        for (var i = 0; i < name.Length; i++)
        {
            var ch = name[i];
            if (char.IsUpper(ch))
            {
                if (i > 0) chars.Add('-');
                chars.Add(char.ToLowerInvariant(ch));
            }
            else
            {
                chars.Add(ch);
            }
        }

        return new string(chars.ToArray()).ToLowerInvariant();
    }

    public static string SerializeStyle(BoxStyle s, string? property = null)
    {
        if (!string.IsNullOrWhiteSpace(property))
            return GetComputedPropertyValue(s, property);

        var props = new[]
        {
            ("display", s.DisplayNone ? "none" : s.DisplayInlineBlock ? "inline-block" : s.DisplayFlex ? "flex" : s.DisplayGrid ? "grid" : s.IsTable ? "table" : s.DisplayBlock ? "block" : "inline"),
            ("color", ColorToCss(s.Color)),
            ("background-color", s.BackgroundColor.HasValue ? ColorToCss(s.BackgroundColor.Value) : "transparent"),
            ("font-size", FormatPx(s.FontSize)),
            ("font-family", s.FontFamily),
            ("font-weight", s.Bold ? "700" : "400"),
            ("font-style", s.Italic ? "italic" : "normal"),
            ("width", s.HasWidth ? FormatPx(s.Width) : "auto"),
            ("height", s.HasHeight ? FormatPx(s.Height) : "auto"),
            ("position", s.Position),
            ("margin-top", FormatPx(s.MarginTop)),
            ("margin-right", FormatPx(s.MarginRight)),
            ("margin-bottom", FormatPx(s.MarginBottom)),
            ("margin-left", FormatPx(s.MarginLeft)),
            ("padding-top", FormatPx(s.PaddingTop)),
            ("padding-right", FormatPx(s.PaddingRight)),
            ("padding-bottom", FormatPx(s.PaddingBottom)),
            ("padding-left", FormatPx(s.PaddingLeft)),
        };

        return string.Join("; ", props.Select(p => $"{p.Item1}: {p.Item2}"));
    }

    public static string GetComputedPropertyValue(BoxStyle s, string property)
    {
        return NormalizePropertyName(property) switch
        {
            "display" => s.DisplayNone ? "none" : s.DisplayInlineBlock ? "inline-block" : s.DisplayFlex ? "flex" : s.DisplayGrid ? "grid" : s.IsTable ? "table" : s.DisplayBlock ? "block" : "inline",
            "color" => ColorToCss(s.Color),
            "background-color" => s.BackgroundColor.HasValue ? ColorToCss(s.BackgroundColor.Value) : "transparent",
            "background-image" => !string.IsNullOrEmpty(s.BackgroundGradient) ? s.BackgroundGradient : s.BackgroundImage,
            "font-size" => FormatPx(s.FontSize),
            "font-family" => s.FontFamily,
            "font-weight" => s.Bold ? "700" : "400",
            "font-style" => s.Italic ? "italic" : "normal",
            "line-height" => s.LineHeight > 0 ? FormatPx(s.LineHeight) : "normal",
            "text-align" => s.TextAlign,
            "width" => s.HasWidth ? FormatPx(s.Width) : "auto",
            "height" => s.HasHeight ? FormatPx(s.Height) : "auto",
            "position" => s.Position,
            "left" => FormatPx(s.PositionLeft),
            "top" => FormatPx(s.PositionTop),
            "right" => FormatPx(s.PositionRight),
            "bottom" => FormatPx(s.PositionBottom),
            "margin-top" => FormatPx(s.MarginTop),
            "margin-right" => FormatPx(s.MarginRight),
            "margin-bottom" => FormatPx(s.MarginBottom),
            "margin-left" => FormatPx(s.MarginLeft),
            "padding-top" => FormatPx(s.PaddingTop),
            "padding-right" => FormatPx(s.PaddingRight),
            "padding-bottom" => FormatPx(s.PaddingBottom),
            "padding-left" => FormatPx(s.PaddingLeft),
            "border-top-width" => FormatPx(s.BorderTop),
            "border-right-width" => FormatPx(s.BorderRight),
            "border-bottom-width" => FormatPx(s.BorderBottom),
            "border-left-width" => FormatPx(s.BorderLeft),
            "opacity" => s.Opacity.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "visibility" => s.Visibility,
            _ => "",
        };
    }

    public static string ColorToCss(Color c)
    {
        if (c.A == 255) return $"rgb({c.R}, {c.G}, {c.B})";
        return $"rgba({c.R}, {c.G}, {c.B}, {(c.A / 255f).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)})";
    }

    public static string FormatPx(float value)
    {
        return value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + "px";
    }

    private static IEnumerable<string> SplitCssDeclarations(string style)
    {
        var start = 0;
        var depth = 0;
        for (var i = 0; i < style.Length; i++)
        {
            var ch = style[i];
            if (ch == '(') depth++;
            else if (ch == ')' && depth > 0) depth--;
            else if (ch == ';' && depth == 0)
            {
                var part = style[start..i].Trim();
                if (part.Length > 0) yield return part;
                start = i + 1;
            }
        }

        var last = style[start..].Trim();
        if (last.Length > 0) yield return last;
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
        if (pts.Length == 0) return;
        var w = ParsePx(pts[0]); bs.BorderTop = bs.BorderBottom = bs.BorderLeft = bs.BorderRight = w;
        if (pts.Length >= 2) { bs.BorderTop = bs.BorderBottom = ParsePx(pts[0]); bs.BorderLeft = bs.BorderRight = ParsePx(pts[1]); }
        if (pts.Length >= 3) { bs.BorderTop = ParsePx(pts[0]); bs.BorderLeft = bs.BorderRight = ParsePx(pts[1]); bs.BorderBottom = ParsePx(pts[2]); }
        if (pts.Length >= 4) { bs.BorderTop = ParsePx(pts[0]); bs.BorderRight = ParsePx(pts[1]); bs.BorderBottom = ParsePx(pts[2]); bs.BorderLeft = ParsePx(pts[3]); }
    }

    private static void ParseBorderStyles(BoxStyle bs, string value)
    {
        var pts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (pts.Length == 0) return;
        bs.BorderTopStyle = bs.BorderBottomStyle = bs.BorderLeftStyle = bs.BorderRightStyle = pts[0].ToLowerInvariant();
        if (pts.Length >= 2) { bs.BorderTopStyle = bs.BorderBottomStyle = pts[0].ToLowerInvariant(); bs.BorderLeftStyle = bs.BorderRightStyle = pts[1].ToLowerInvariant(); }
        if (pts.Length >= 3) { bs.BorderTopStyle = pts[0].ToLowerInvariant(); bs.BorderLeftStyle = bs.BorderRightStyle = pts[1].ToLowerInvariant(); bs.BorderBottomStyle = pts[2].ToLowerInvariant(); }
        if (pts.Length >= 4) { bs.BorderTopStyle = pts[0].ToLowerInvariant(); bs.BorderRightStyle = pts[1].ToLowerInvariant(); bs.BorderBottomStyle = pts[2].ToLowerInvariant(); bs.BorderLeftStyle = pts[3].ToLowerInvariant(); }
    }

    private static void ParseBorderColors(BoxStyle bs, string value)
    {
        var pts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (pts.Length == 0) return;
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

    private static void ApplyBoxShadow(BoxStyle bs, string value)
    {
        if (value.Trim().Equals("none", StringComparison.OrdinalIgnoreCase))
        {
            bs.ShadowX = bs.ShadowY = bs.ShadowBlur = bs.ShadowSpread = 0;
            bs.ShadowColor = Color.Transparent;
            bs.ShadowInset = false;
            return;
        }

        var firstShadow = SplitCssArgs(value).FirstOrDefault() ?? value;
        var lengths = new List<float>();
        bs.ShadowInset = false;
        bs.ShadowColor = Color.FromArgb(85, 0, 0, 0);

        foreach (var token in SplitCssValueTokens(firstShadow))
        {
            var lower = token.ToLowerInvariant().TrimEnd(',');
            if (lower == "inset")
            {
                bs.ShadowInset = true;
                continue;
            }

            if (IsColorToken(lower))
            {
                bs.ShadowColor = ParseCssColor(token);
                continue;
            }

            lengths.Add(ParsePx(token));
        }

        if (lengths.Count > 0) bs.ShadowX = lengths[0];
        if (lengths.Count > 1) bs.ShadowY = lengths[1];
        if (lengths.Count > 2) bs.ShadowBlur = Math.Max(0, lengths[2]);
        if (lengths.Count > 3) bs.ShadowSpread = lengths[3];
    }

    private static bool IsColorToken(string token) =>
        token.StartsWith('#') || token.StartsWith("rgb") || token.StartsWith("hsl") || NamedColors.ContainsKey(token);

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

    private static IEnumerable<string> SplitCssValueTokens(string value)
    {
        var start = 0;
        var depth = 0;
        for (var i = 0; i < value.Length; i++)
        {
            var ch = value[i];
            if (ch == '(') depth++;
            else if (ch == ')' && depth > 0) depth--;
            else if (char.IsWhiteSpace(ch) && depth == 0)
            {
                var part = value[start..i].Trim();
                if (part.Length > 0) yield return part;
                start = i + 1;
            }
        }

        var last = value[start..].Trim();
        if (last.Length > 0) yield return last;
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

    private static void ApplyTransform(BoxStyle bs, string value)
    {
        var matches = System.Text.RegularExpressions.Regex.Matches(value.ToLowerInvariant(), @"([a-z0-9]+)\(([^)]*)\)");
        foreach (System.Text.RegularExpressions.Match match in matches)
        {
            var fn = match.Groups[1].Value;
            var args = match.Groups[2].Value.Split(',', StringSplitOptions.TrimEntries);
            switch (fn)
            {
                case "translate":
                    if (args.Length > 0) bs.TransformX += ParsePx(args[0]);
                    if (args.Length > 1) bs.TransformY += ParsePx(args[1]);
                    break;
                case "translatex": bs.TransformX += ParsePx(match.Groups[2].Value); break;
                case "translatey": bs.TransformY += ParsePx(match.Groups[2].Value); break;
                case "rotate": bs.TransformRotate += ParseAngle(match.Groups[2].Value); break;
                case "scale":
                    if (args.Length > 0 && float.TryParse(args[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var sx))
                    {
                        bs.TransformScaleX *= sx;
                        bs.TransformScaleY *= sx;
                    }
                    if (args.Length > 1 && float.TryParse(args[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var sy))
                        bs.TransformScaleY = sy;
                    break;
                case "scalex":
                    if (float.TryParse(match.Groups[2].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var sx2)) bs.TransformScaleX *= sx2;
                    break;
                case "scaley":
                    if (float.TryParse(match.Groups[2].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var sy2)) bs.TransformScaleY *= sy2;
                    break;
                case "skew":
                    if (args.Length > 0) bs.TransformSkewX += ParseAngle(args[0]);
                    if (args.Length > 1) bs.TransformSkewY += ParseAngle(args[1]);
                    break;
                case "skewx": bs.TransformSkewX += ParseAngle(match.Groups[2].Value); break;
                case "skewy": bs.TransformSkewY += ParseAngle(match.Groups[2].Value); break;
            }
        }
    }

    private static float ParseAngle(string value)
    {
        var inner = value.Trim();
        if (inner.EndsWith("deg") && float.TryParse(inner[..^3], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var deg)) return deg;
        if (inner.EndsWith("rad") && float.TryParse(inner[..^3], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var rad)) return rad * 57.2958f;
        if (float.TryParse(inner, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var n)) return n;
        return 0;
    }

    public static float ParsePx(string v, float def = 0)
    {
        if (string.IsNullOrEmpty(v)) return def;
        v = v.Trim().ToLowerInvariant();
        if (v.EndsWith("px") && float.TryParse(v[..^2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var r)) return r;
        if (v.EndsWith("%") && float.TryParse(v[..^1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var pc)) return pc / 100f * def;
        if (v.EndsWith("em") && float.TryParse(v[..^2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var e)) return e * 12;
        if (v.EndsWith("rem") && float.TryParse(v[..^3], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var re)) return re * 12;
        if (v.EndsWith("pt") && float.TryParse(v[..^2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var p)) return p * 1.333f;
        if (float.TryParse(v, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var unitless)) return unitless;
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
            if (c == "serif") return "Times New Roman";
            if (c is "sans-serif" or "system-ui") return "Segoe UI";
            if (c == "monospace") return "Consolas";
            try { using var f = new Font(c, 12); return c; } catch { }
        }
        return "Segoe UI";
    }

    private static Color ParseCssColor(string? v)
    {
        if (string.IsNullOrEmpty(v)) return Color.Black;
        v = v.Trim().TrimEnd(',').ToLowerInvariant();
        if (v.StartsWith('#'))
        {
            var h = v[1..];
            if (h.Length == 3) return Color.FromArgb((byte)(Conv(h[0..1]) * 17), (byte)(Conv(h[1..2]) * 17), (byte)(Conv(h[2..3]) * 17));
            if (h.Length == 6) return Color.FromArgb(Conv(h[0..2]), Conv(h[2..4]), Conv(h[4..6]));
            if (h.Length == 8) return Color.FromArgb(Conv(h[6..8]), Conv(h[0..2]), Conv(h[2..4]), Conv(h[4..6]));
        }
        var modernRgb = System.Text.RegularExpressions.Regex.Match(v, @"rgba?\s*\(\s*([+-]?[\d.]+%?)\s+([+-]?[\d.]+%?)\s+([+-]?[\d.]+%?)(?:\s*/\s*([+-]?[\d.]+%?))?\s*\)");
        if (modernRgb.Success)
            return Color.FromArgb(ParseAlpha(modernRgb.Groups[4].Success ? modernRgb.Groups[4].Value : null), ParseRgbChannel(modernRgb.Groups[1].Value), ParseRgbChannel(modernRgb.Groups[2].Value), ParseRgbChannel(modernRgb.Groups[3].Value));

        var mr = System.Text.RegularExpressions.Regex.Match(v, @"rgba?\s*\(\s*([+-]?[\d.]+%?)\s*,\s*([+-]?[\d.]+%?)\s*,\s*([+-]?[\d.]+%?)\s*(?:,\s*([+-]?[\d.]+%?)\s*)?\)");
        if (mr.Success) return Color.FromArgb(ParseAlpha(mr.Groups[4].Success ? mr.Groups[4].Value : null), ParseRgbChannel(mr.Groups[1].Value), ParseRgbChannel(mr.Groups[2].Value), ParseRgbChannel(mr.Groups[3].Value));
        var mh = System.Text.RegularExpressions.Regex.Match(v, @"hsla?\s*\(\s*([\d.]+)\s*,\s*([\d.]+)%\s*,\s*([\d.]+)%\s*(?:,\s*([\d.]+)\s*)?\)");
        if (mh.Success) return Hsl(float.Parse(mh.Groups[1].Value), float.Parse(mh.Groups[2].Value) / 100f, float.Parse(mh.Groups[3].Value) / 100f, mh.Groups[4].Success ? (int)(float.Parse(mh.Groups[4].Value) * 255) : 255);
        return NamedColors.TryGetValue(v, out var c) ? c : Color.Black;

        static byte Conv(string h) => Convert.ToByte(h, 16);
        static int ParseRgbChannel(string value)
        {
            value = value.Trim();
            if (value.EndsWith("%", StringComparison.Ordinal) &&
                float.TryParse(value[..^1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var pct))
                return (int)Math.Clamp(MathF.Round(pct / 100f * 255f), 0, 255);
            return float.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var channel)
                ? (int)Math.Clamp(MathF.Round(channel), 0, 255)
                : 0;
        }

        static int ParseAlpha(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return 255;
            value = value.Trim();
            if (value.EndsWith("%", StringComparison.Ordinal) &&
                float.TryParse(value[..^1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var pct))
                return (int)Math.Clamp(MathF.Round(pct / 100f * 255f), 0, 255);
            if (!float.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var alpha)) return 255;
            return (int)Math.Clamp(MathF.Round(alpha * 255f), 0, 255);
        }
    }

    private static Color? ParseCssColorOrNull(string? v)
    {
        if (string.IsNullOrEmpty(v)) return null;
        var normalized = v.Trim().ToLowerInvariant();
        if (normalized is "transparent" or "rgba(0, 0, 0, 0)" or "rgba(0,0,0,0)") return null;
        return ParseCssColor(v);
    }

    private static Color Hsl(float h, float s, float l, int a = 255)
    {
        if (s == 0) { var g = (byte)(l * 255); return Color.FromArgb(a, g, g, g); }
        float H2R(float t)
        {
            if (t < 0) t += 1;
            if (t > 1) t -= 1;
            if (t < 1f / 6) return l + s * (1 - Math.Abs(2 * l - 1)) * 6 * t;
            if (t < 0.5f) return l + s * (1 - Math.Abs(2 * l - 1));
            if (t < 2f / 3) return l + s * (1 - Math.Abs(2 * l - 1)) * (2f / 3 - t) * 6;
            return l;
        }

        return Color.FromArgb(a, (byte)(H2R(h / 360 + 1f / 3) * 255), (byte)(H2R(h / 360) * 255), (byte)(H2R(h / 360 - 1f / 3) * 255));
    }
}
