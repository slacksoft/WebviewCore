using AngleSharp.Dom;

namespace WebviewCore;

class LayoutEngine
{
    private readonly float _width;
    private float _y, _x, _lineStartX, _lineHeight;
    private readonly Dictionary<IElement, BoxStyle> _styles;
    private BoxStyle _cur = new();
    private readonly Stack<int> _olCounters = new();
    private readonly Stack<float> _blockWidths = new();
    private readonly List<LayoutBox> _lineBoxes = new();
    private float _lineMaxX;

    // Float tracking
    private readonly List<(float top, float bottom, float left, float right, string side)> _floats = new();

    public LayoutEngine(float width, Dictionary<IElement, BoxStyle> styles) { _width = width; _styles = styles; }
    public List<string> PendingImages { get; } = new();

    public LayoutBox Layout(IElement root)
    {
        var r = new LayoutBox { IsBlock = true, Bounds = new RectangleF(0, 0, _width, 0) };
        Walk(root, r); NewLine();
        r.Bounds = new RectangleF(0, 0, _width, _y);
        // Render positioned absolute/fixed children
        foreach (var (el, style, parentBox) in _absolutes)
            LayoutAbsolute(el, style, parentBox);
        return r;
    }

    private readonly List<(IElement el, BoxStyle style, LayoutBox parent)> _absolutes = new();

    private void Style(IElement? e) { if (e != null && _styles.TryGetValue(e, out var s)) _cur = s; else _cur = new BoxStyle(); }

    private void Walk(INode node, LayoutBox parent)
    {
        if (node.NodeType == NodeType.Text)
        { Append(((IText)node).Text, parent, node); return; }

        if (node is not IElement el) return;
        Style(el); if (_cur.DisplayNone) return;

        var tag = el.TagName.ToLowerInvariant();
        if (tag is "title" or "meta" or "link" or "base" or "style" or "script" or "head" or "noscript") return;

        // --- Special elements ---
        if (tag == "img") { Img(el, parent); return; }
        if (tag == "hr") { Hr(el, parent); return; }
        if (tag is "input" or "button" or "textarea" or "select") { FormCtrl(el, parent); return; }
        if (tag == "br") { NewLine(); return; }
        if (tag == "ol") { _olCounters.Push(0); foreach (var c in el.ChildNodes) Walk(c, parent); _olCounters.Pop(); return; }
        if (tag == "ul") { foreach (var c in el.ChildNodes) Walk(c, parent); return; }
        if (tag == "li") { Li(el, parent); return; }
        if (tag is "sup" or "sub") { SupSub(el, parent, tag); return; }

        // --- Tables ---
        if (_cur.IsTable) { LayoutTable(el, parent); return; }

        // --- Position: absolute/fixed — defer to after normal flow ---
        var pos = _cur.Position;
        if (pos == "absolute" || pos == "fixed")
        {
            _absolutes.Add((el, _cur, parent));
            return;
        }

        // --- Link: track current link element for all child text nodes ---
        var prevLink = _currentLink;
        if (tag == "a") _currentLink = el;

        // --- Save style from BEFORE this element for sibling processing ---
        var prevStyle = _cur;

        // --- Block vs inline ---
        var myStyle = _cur;

        if (myStyle.DisplayBlock)
        {
            var isInlineBlock = myStyle.DisplayInlineBlock;

            if (!isInlineBlock)
            {
                if (!LineEmpty) NewLine();
                if (_y > 0) _y += myStyle.MarginTop;
            }

            float startX = isInlineBlock ? _x + myStyle.MarginLeft : myStyle.MarginLeft;
            float contentX = startX + myStyle.PaddingLeft + myStyle.BorderLeft;

            // Handle float: shift start position
            var floatSide = myStyle.Float;
            bool isFloat = floatSide is "left" or "right";

            if (isFloat)
            {
                // For floats, position at left/right edge of container (not middle of line)
                if (!isInlineBlock && !LineEmpty) NewLine();
                var containerW = _blockWidths.Count > 0 ? _blockWidths.Peek() : _width;
                if (floatSide == "left")
                {
                    startX = 0;
                }
                else
                {
                    startX = containerW - (myStyle.HasWidth ? myStyle.Width : 200);
                }
                contentX = startX + myStyle.PaddingLeft + myStyle.BorderLeft;
            }

            _x = contentX;
            _lineStartX = contentX;

            float startY = _y;
            float bw = myStyle.HasWidth ? myStyle.Width : (_width - startX);
            float contentWidth = bw - myStyle.PaddingLeft - myStyle.PaddingRight - myStyle.BorderLeft - myStyle.BorderRight;
            if (contentWidth < 0) contentWidth = 10;

            // Inline-block without explicit width: use large content width to measure shrink-to-fit
            if (isInlineBlock && !myStyle.HasWidth)
                contentWidth = 100000f;

            var savedLineY = _y;
            var savedLineHeight = _lineHeight;
            var savedLineMax = _lineMaxX;
            var savedLineBoxes = isInlineBlock ? new List<LayoutBox>(_lineBoxes) : null;

            if (isInlineBlock)
            {
                // Inline-block: children layout starts inside the box
                _y = startY + myStyle.PaddingTop + myStyle.BorderTop;
                _lineHeight = 0;
                _lineStartX = contentX;
                _lineMaxX = contentX;
            }

            _blockWidths.Push(contentWidth);
            var blockBox = new LayoutBox { IsBlock = true, Style = myStyle, Source = el, BgColor = myStyle.BackgroundColor };

            foreach (var c in el.ChildNodes) Walk(c, blockBox);

            _blockWidths.Pop();
            _cur = myStyle;

            if (isInlineBlock)
            {
                // Inline-block: calculate height, place inline, restore outer line state
                float childrenH = Math.Max(0, _y - (startY + myStyle.PaddingTop + myStyle.BorderTop));
                float ibBh = myStyle.HasHeight ? myStyle.Height : childrenH + myStyle.PaddingBottom + myStyle.BorderBottom;

                // Shrink-to-fit width from children content
                if (!myStyle.HasWidth)
                {
                    float maxRight = contentX;
                    foreach (var child in blockBox.Children)
                    {
                        float r = child.Bounds.X + child.Bounds.Width;
                        if (r > maxRight) maxRight = r;
                    }
                    bw = (maxRight - contentX) + myStyle.PaddingRight + myStyle.BorderRight;
                    if (bw < 0) bw = 0;
                }

                blockBox.Bounds = new RectangleF(startX, startY, bw, ibBh);
                parent.Children.Add(blockBox);

                _x = startX + bw + myStyle.MarginRight;
                _y = savedLineY;
                _lineHeight = Math.Max(savedLineHeight, ibBh);
                _lineMaxX = Math.Max(savedLineMax, _x);
                _lineBoxes.Clear();
                _lineBoxes.AddRange(savedLineBoxes!);

                _cur = prevStyle;
                if (tag == "a") _currentLink = prevLink;
                return;
            }

            // Handle clear
            if (myStyle.Clear != "none")
            {
                var cleared = _floats.FindAll(f =>
                    myStyle.Clear == "both" || f.side == myStyle.Clear);
                foreach (var f in cleared)
                {
                    if (f.bottom + 4 > _y)
                        _y = f.bottom + 4;
                }
            }

            NewLine();
            _y += _cur.MarginBottom + _cur.PaddingBottom + _cur.BorderBottom;

            float bh = myStyle.HasHeight ? myStyle.Height : Math.Max(0, _y - startY);
            blockBox.Bounds = new RectangleF(startX, startY, bw, bh);

            // Position: relative — offset after normal flow
            if (pos == "relative")
            {
                float dx = myStyle.PositionLeft - myStyle.PositionRight;
                float dy = myStyle.PositionTop - myStyle.PositionBottom;
                if (dx != 0 || dy != 0)
                {
                    blockBox.Bounds = new RectangleF(
                        blockBox.Bounds.X + dx, blockBox.Bounds.Y + dy,
                        blockBox.Bounds.Width, blockBox.Bounds.Height);
                }
            }

            // Register float
            if (isFloat)
            {
                _floats.Add((startY, startY + bh, startX, startX + bw, floatSide));
                // Line start shifts past left floats
                if (floatSide == "left")
                    _lineStartX = Math.Max(_lineStartX, startX + bw);
            }

            parent.Children.Add(blockBox);

            _cur = prevStyle;
            if (tag == "a") _currentLink = prevLink;
            return;
        }

        foreach (var c in el.ChildNodes) Walk(c, parent);

        _cur = prevStyle;
        if (tag == "a") _currentLink = prevLink;
    }

    private IElement? _currentLink;

    // ============ ABSOLUTE/FIXED LAYOUT ============
    private void LayoutAbsolute(IElement el, BoxStyle style, LayoutBox parent)
    {
        _cur = style;
        bool isFixed = style.Position == "fixed";

        // Containing block: for absolute, use parent element; for fixed, use viewport (root)
        float cbX = 0, cbY = 0, cbW = _width, cbH = _y;
        if (!isFixed)
        {
            // Use nearest positioned ancestor or root
            // Simplified: use parent's first child LayoutBox bounds if available
            if (parent.Bounds.Width > 0) { cbX = parent.Bounds.X; cbW = parent.Bounds.Width; }
            if (parent.Bounds.Height > 0) { cbY = parent.Bounds.Y; cbH = parent.Bounds.Height; }
        }

        float x = cbX + style.PositionLeft;
        if (style.PositionRight > 0) x = cbX + cbW - style.PositionRight - (style.HasWidth ? style.Width : 0);
        float y = cbY + style.PositionTop;
        if (style.PositionBottom > 0) y = cbY + cbH - style.PositionBottom - (style.HasHeight ? style.Height : 0);

        float bw = style.HasWidth ? style.Width : 200;
        float bh = style.HasHeight ? style.Height : 100;

        var blockBox = new LayoutBox
        {
            IsBlock = true, Style = style, Source = el, BgColor = style.BackgroundColor,
            Bounds = new RectangleF(x, y, bw, bh),
        };

        // Layout children inside the absolute box
        var savedState = (_y, _x, _lineStartX, _lineHeight);
        _y = y + style.PaddingTop + style.BorderTop;
        _x = x + style.PaddingLeft + style.BorderLeft;
        _lineStartX = _x; _lineHeight = 0;
        float contentW = bw - style.PaddingLeft - style.PaddingRight - style.BorderLeft - style.BorderRight;
        if (contentW < 0) contentW = 10;
        _blockWidths.Push(contentW);
        foreach (var c in el.ChildNodes) Walk(c, blockBox);
        _blockWidths.Pop();
        _cur = style;

        float ch = Math.Max(0, _y - (y + style.PaddingTop + style.BorderTop));
        float finalH = style.HasHeight ? style.Height : ch + style.PaddingBottom + style.BorderBottom;
        blockBox.Bounds = new RectangleF(x, y, bw, finalH);

        // Fixed boxes are marked for no-scroll in renderer
        if (isFixed) blockBox.IsFixed = true;

        parent.Children.Add(blockBox);
        (_y, _x, _lineStartX, _lineHeight) = savedState;
    }

    // ============ TABLE ============
    private void LayoutTable(IElement el, LayoutBox parent)
    {
        if (!LineEmpty) NewLine();
        if (_y > 0) _y += _cur.MarginTop;
        _x = _cur.MarginLeft; _lineStartX = _x;
        NewLine();

        var tableBox = new LayoutBox { IsBlock = true, Style = _cur, Source = el, BgColor = _cur.BackgroundColor };
        parent.Children.Add(tableBox);

        float tableY = _y;

        var rows = new List<List<(IElement cell, int cs)>>();
        CollectRows(el, rows);
        if (rows.Count == 0) { tableBox.Bounds = new RectangleF(_x, tableY, 0, 0); _y += _cur.MarginBottom; NewLine(); return; }

        int maxCols = rows.Max(r => { int c = 0; foreach (var (_, cs2) in r) c += cs2; return c; });
        if (maxCols == 0) maxCols = 1;

        float availW = _width - _x;
        float perCol = availW / maxCols;
        var cw = new float[maxCols];
        for (int i = 0; i < maxCols; i++) cw[i] = perCol;

        var rh = new float[rows.Count];
        for (int ri = 0; ri < rows.Count; ri++)
        {
            float maxH = 0; int ci = 0;
            foreach (var (cell, cs2) in rows[ri])
            {
                Style(cell);
                float inner = 0;
                foreach (var cn in cell.ChildNodes)
                    if (cn.NodeType == NodeType.Text) inner = Math.Max(inner, 16);
                    else if (cn is IElement ce) inner = Math.Max(inner, MeasureH(ce));
                maxH = Math.Max(maxH, inner + _cur.PaddingTop + _cur.PaddingBottom + _cur.BorderTop + _cur.BorderBottom);
                ci += cs2;
            }
            rh[ri] = Math.Max(maxH, 16);
        }

        for (int ri = 0; ri < rows.Count; ri++)
        {
            float rowY = tableY;
            for (int pri = 0; pri < ri; pri++) rowY += rh[pri];

            int ci = 0;
            foreach (var (cell, cs2) in rows[ri])
            {
                float cellX = _x;
                for (int pi = 0; pi < ci; pi++) cellX += cw[Math.Min(pi, maxCols - 1)];

                float cellW = 0;
                for (int k = 0; k < cs2; k++) cellW += cw[Math.Min(ci + k, maxCols - 1)];

                Style(cell);
                var cellBox = new LayoutBox { Bounds = new RectangleF(cellX, rowY, cellW, rh[ri]), IsBlock = true, Style = _cur, Source = cell, BgColor = _cur.BackgroundColor };
                tableBox.Children.Add(cellBox);

                var (sy, sx, slx, slh) = (_y, _x, _lineStartX, _lineHeight);
                _y = rowY + _cur.PaddingTop + _cur.BorderTop;
                _x = cellX + _cur.PaddingLeft + _cur.BorderLeft;
                _lineStartX = _x; _lineHeight = 0;

                foreach (var cn in cell.ChildNodes) Walk(cn, cellBox);
                NewLine();

                (_y, _x, _lineStartX, _lineHeight) = (sy, sx, slx, slh);
                ci += cs2;
            }
        }

        float tableH = rh.Sum();
        tableBox.Bounds = new RectangleF(_x, tableY, availW, tableH);

        _y = tableY + tableH + _cur.MarginBottom;
        NewLine();
    }

    private void CollectRows(IElement el, List<List<(IElement, int)>> rows)
    {
        foreach (var c in el.Children)
        {
            var t = c.TagName.ToLowerInvariant();
            if (t is "thead" or "tbody" or "tfoot") { CollectRows(c, rows); continue; }
            if (t == "tr")
            {
                var row = new List<(IElement, int)>();
                foreach (var td in c.Children)
                {
                    var tn = td.TagName.ToLowerInvariant();
                    if (tn is not "td" and not "th") continue;
                    var cs2 = 1;
                    var csAttr = td.GetAttribute("colspan");
                    if (int.TryParse(csAttr, out var csv) && csv > 0) cs2 = csv;
                    row.Add((td, cs2));
                }
                if (row.Count > 0) rows.Add(row);
            }
        }
    }

    private float MeasureH(IElement el)
    {
        float h = 0;
        foreach (var cn in el.ChildNodes)
        {
            if (cn.NodeType == NodeType.Text) h += 16;
            else if (cn is IElement ce)
            {
                if (ce.TagName.ToLowerInvariant() == "img")
                {
                    var ah = ce.GetAttribute("height");
                    h += int.TryParse(ah, out var pv) ? pv : 150;
                }
                else h += 16;
            }
        }
        return Math.Max(h, 16);
    }

    // ============ SPECIALS ============
    private void SupSub(IElement el, LayoutBox parent, string tag)
    {
        Style(el); var saved = (_cur, _y);
        _y += tag == "sup" ? -_cur.FontSize * 0.35f : _cur.FontSize * 0.15f;
        foreach (var c in el.ChildNodes) Walk(c, parent);
        (_cur, _y) = saved;
    }

    private void Li(IElement el, LayoutBox parent)
    {
        Style(el);
        if (!LineEmpty) NewLine(); if (_y > 0) _y += _cur.MarginTop;
        _x = _cur.MarginLeft; _lineStartX = _x;
        var isOl = el.ParentElement?.TagName?.ToLowerInvariant() == "ol";
        var num = 1;
        if (isOl && _olCounters.Count > 0) { _olCounters.Push(_olCounters.Pop() + 1); num = _olCounters.Peek(); }
        Append(isOl ? $"{num}. " : "• ", parent, el);
        foreach (var c in el.ChildNodes) Walk(c, parent);
        NewLine(); _y += _cur.MarginBottom;
    }

    private void FormCtrl(IElement el, LayoutBox parent)
    {
        Style(el);
        var tag = el.TagName.ToLowerInvariant();
        var type = tag switch
        {
            "button" => el.GetAttribute("type")?.ToLowerInvariant() ?? "submit",
            "textarea" => "textarea",
            "select" => "select",
            _ => el.GetAttribute("type")?.ToLowerInvariant() ?? "text",
        };
        if (type == "hidden") return;
        var val = GetFormValue(el, tag);
        var chk = el.HasAttribute("checked");
        var ph = el.GetAttribute("placeholder") ?? "";
        float cw, ch;
        if (_cur.HasWidth) cw = _cur.Width;
        else if (type is "checkbox" or "radio") cw = 14;
        else if (type is "submit" or "button" || tag == "button")
        { var bw = TextMeasurer.MeasureWidth((val.Length > 0 ? val : "Submit"), _cur.FontFamily, _cur.FontSize, _cur.Bold, _cur.Italic); cw = bw + _cur.PaddingLeft + _cur.PaddingRight + 8; if (cw < 60) cw = 60; }
        else if (tag == "textarea")
        { int r = int.TryParse(el.GetAttribute("rows"), out var rv) ? rv : 3; int cl = int.TryParse(el.GetAttribute("cols"), out var cv) ? cv : 20; cw = cl * 8; }
        else if (tag == "select") cw = Math.Max(120, TextMeasurer.MeasureWidth(val.Length > 0 ? val : "select", _cur.FontFamily, _cur.FontSize, _cur.Bold, _cur.Italic) + 28);
        else cw = 160;
        if (_cur.HasHeight) ch = _cur.Height;
        else if (type is "checkbox" or "radio") ch = 14;
        else if (type is "submit" or "button" || tag == "button")
        { ch = _cur.FontSize + _cur.PaddingTop + _cur.PaddingBottom + 8; if (ch < 22) ch = 22; }
        else if (tag == "textarea")
        { int r = int.TryParse(el.GetAttribute("rows"), out var rv) ? rv : 3; ch = r * 14; }
        else if (tag == "select") ch = _cur.FontSize + _cur.PaddingTop + _cur.PaddingBottom + 10;
        else ch = 22;
        if (_x + cw > _width && _x > _lineStartX) { _y += _lineHeight > 0 ? _lineHeight : 14; _x = _lineStartX; _lineHeight = 0; }
        var nameAttr = el.GetAttribute("name");
        parent.Children.Add(new LayoutBox { IsInput = true, InputType = type, InputValue = val, InputName = nameAttr, InputChecked = chk, Bounds = new RectangleF(_x, _y, cw, ch), Text = tag == "textarea" ? (val.Length > 0 ? val : ph) : "", FontSize = _cur.FontSize, FontName = _cur.FontFamily, Color = _cur.Color, Bold = _cur.Bold, Style = _cur, Source = el });
        _x += cw + 4; _lineHeight = Math.Max(_lineHeight, ch);
    }

    private static string GetFormValue(IElement el, string tag)
    {
        if (tag == "select")
        {
            var option = el.QuerySelectorAll("option").FirstOrDefault(o => o.HasAttribute("selected")) ?? el.QuerySelector("option");
            return option?.GetAttribute("value") ?? option?.TextContent.Trim() ?? "";
        }

        if (tag == "textarea")
            return el.GetAttribute("value") ?? el.TextContent;

        return el.GetAttribute("value") ?? el.TextContent.Trim();
    }

    private void Hr(IElement el, LayoutBox parent)
    {
        Style(el); if (!LineEmpty) NewLine(); if (_y > 0) _y += _cur.MarginTop;
        var w = _cur.HasWidth ? _cur.Width : _width; var h = Math.Max(_cur.BorderTop > 0 ? _cur.BorderTop : 1, 1);
        parent.Children.Add(new LayoutBox { IsHr = true, Bounds = new RectangleF(_x, _y, w, h), Style = _cur, Source = el });
        _y += h + _cur.MarginBottom; NewLine();
    }

    private void Img(IElement el, LayoutBox parent)
    {
        var src = el.GetAttribute("src"); if (string.IsNullOrEmpty(src)) return;
        var abs = ResolveAbs(el, src);
        float w = _cur.HasWidth ? _cur.Width : (int.TryParse(el.GetAttribute("width"), out var pw) ? pw : 200);
        float h = _cur.HasHeight ? _cur.Height : (int.TryParse(el.GetAttribute("height"), out var ph) ? ph : w * 0.75f);
        // Avoid floats
        AdjustXForFloats(w);
        if (_x + w > _width && _x > _lineStartX) { _y += _lineHeight > 0 ? _lineHeight : 14; _x = _lineStartX; _lineHeight = 0; }
        var c = ImageLoader.GetCached(abs); if (c != null) { w = c.Width; h = c.Height; } else if (!PendingImages.Contains(abs)) PendingImages.Add(abs);
        parent.Children.Add(new LayoutBox { IsImage = true, ImageUrl = abs, ImageData = c, Bounds = new RectangleF(_x, _y, w, h), Source = el, Style = _cur, BgColor = _cur.BackgroundColor });
        _x += w; _lineHeight = Math.Max(_lineHeight, h);
    }

    private static string ResolveAbs(IElement el, string src)
    {
        var baseUrl = ""; var cur = el;
        while (cur != null) { var b = cur.GetAttribute("_base"); if (b != null) { baseUrl = b; break; } cur = cur.ParentElement; }
        try { return new Uri(new Uri(baseUrl), src).ToString(); } catch { return src; }
    }

    private bool LineEmpty => _lineHeight <= 0 && _x <= _lineStartX;

    private void AdjustXForFloats(float itemWidth)
    {
        foreach (var f in _floats)
        {
            if (_y >= f.top && _y < f.bottom)
            {
                if (f.side == "left" && _x >= f.left && _x < f.right)
                    _x = f.right;
                if (f.side == "right" && _x + itemWidth > f.left && _x < f.right)
                    _x = f.left - itemWidth;
            }
        }
        _lineStartX = Math.Max(_lineStartX, _floats
            .Where(f => _y >= f.top && _y < f.bottom && f.side == "left")
            .Select(f => f.right)
            .DefaultIfEmpty(_lineStartX)
            .Max());
    }

    private void NewLine()
    {
        if (_lineBoxes.Count > 0)
        {
            var availW = _blockWidths.Count > 0 ? _blockWidths.Peek() : _width;
            var contentW = _lineMaxX - _lineStartX;
            var remain = availW - contentW;
            if (remain > 0)
            {
                float shift = _cur.TextAlign switch
                {
                    "center" => remain / 2f,
                    "right" => remain,
                    _ => 0,
                };
                if (shift > 0)
                    foreach (var b in _lineBoxes)
                        b.Bounds = new RectangleF(b.Bounds.X + shift, b.Bounds.Y, b.Bounds.Width, b.Bounds.Height);
            }
        }
        _lineBoxes.Clear();
        _lineMaxX = _lineStartX;

        var lh = _cur.LineHeight > 0 ? _cur.LineHeight : (_lineHeight > 0 ? _lineHeight : _cur.FontSize * 1.4f);
        _y += lh; _x = _lineStartX; _lineHeight = 0;

        // After newline, check if floats still apply — adjust line start past left floats
        _lineStartX = Math.Max(_lineStartX, _floats
            .Where(f => _y >= f.top && _y < f.bottom && f.side == "left")
            .Select(f => f.right)
            .DefaultIfEmpty(_lineStartX)
            .Max());
        _x = _lineStartX;
    }

    // ============ INLINE TEXT ============
    private static BoxStyle TextStyle(BoxStyle s) => new BoxStyle
    {
        Color = s.Color, FontSize = s.FontSize, Bold = s.Bold, Italic = s.Italic,
        FontFamily = s.FontFamily, LineThrough = s.LineThrough, Underline = s.Underline,
        Overline = s.Overline, WhiteSpace = s.WhiteSpace, TextTransform = s.TextTransform,
        LetterSpacing = s.LetterSpacing, WordSpacing = s.WordSpacing,
        TextShadowX = s.TextShadowX, TextShadowY = s.TextShadowY, TextShadowBlur = s.TextShadowBlur,
        TextShadowColor = s.TextShadowColor, Opacity = s.Opacity, VerticalAlign = s.VerticalAlign,
        BackgroundColor = s.BackgroundColor, BackgroundGradient = s.BackgroundGradient,
        TextDecorationLine = s.TextDecorationLine, TextDecorationColor = s.TextDecorationColor,
        TextDecorationStyle = s.TextDecorationStyle, TextDecorationThickness = s.TextDecorationThickness,
    };

    private void Append(string text, LayoutBox parent, INode? source = null)
    {
        var s = _cur;
        if (s.WhiteSpace == "pre") { AppendPre(text, parent, source, s); return; }

        var isLink = _currentLink != null;
        var href = _currentLink?.GetAttribute("href");
        var color = isLink ? Color.Blue : s.Color;
        var fn = s.FontFamily;
        var fsize = s.FontSize;

        var parts = text.Split(' ');

        for (int i = 0; i < parts.Length; i++)
        {
            var w = parts[i]; if (w.Length == 0) continue;
            var sp = w + (i < parts.Length - 1 ? " " : "");
            if (sp.Trim().Length == 0) continue;
            var (mw, mh) = TextMeasurer.Measure(sp, fn, fsize, s.Bold, s.Italic);
            var sz = new SizeF(mw, mh);

            // Avoid floats
            AdjustXForFloats(sz.Width);

            if (_x + sz.Width > _width && _x > _lineStartX)
            {
                NewLine();
                // After newline, check floats again
                AdjustXForFloats(sz.Width);
            }
            var lb = new LayoutBox { Text = sp, Bounds = new RectangleF(_x, _y, sz.Width, sz.Height), FontSize = fsize, Bold = s.Bold, Italic = s.Italic, LineThrough = s.LineThrough, Color = color, IsLink = isLink, Href = href, FontName = fn, Source = source as IElement, Style = TextStyle(s), BgColor = s.BackgroundColor };
            parent.Children.Add(lb);
            _lineBoxes.Add(lb);
            _lineMaxX = Math.Max(_lineMaxX, _x + sz.Width);
            _x += sz.Width; _lineHeight = Math.Max(_lineHeight, sz.Height);
        }
    }

    private void AppendPre(string text, LayoutBox parent, INode? source, BoxStyle s)
    {
        var isLink = _currentLink != null;
        var href = _currentLink?.GetAttribute("href");
        var color = isLink ? Color.Blue : s.Color;
        var fn = s.FontFamily;
        var fsize = s.FontSize;

        var lines = text.Split('\n');
        for (int li = 0; li < lines.Length; li++)
        {
            if (li > 0) NewLine();
            var line = lines[li].Replace("\r", ""); if (line.Length == 0) continue;
            var (mw, mh) = TextMeasurer.Measure(line, fn, fsize, s.Bold, s.Italic);
            var sz = new SizeF(mw, mh);
            AdjustXForFloats(sz.Width);
            if (_x + sz.Width > _width && _x > _lineStartX) NewLine();
            var lb = new LayoutBox { Text = line, Bounds = new RectangleF(_x, _y, sz.Width, sz.Height), FontSize = fsize, Bold = s.Bold, Italic = s.Italic, LineThrough = s.LineThrough, Color = color, IsLink = isLink, Href = href, FontName = fn, Source = source as IElement, Style = TextStyle(s) };
            parent.Children.Add(lb);
            _lineBoxes.Add(lb); _lineMaxX = Math.Max(_lineMaxX, _x + sz.Width);
            _x += sz.Width; _lineHeight = Math.Max(_lineHeight, sz.Height);
        }
    }
}
