using AngleSharp.Dom;
using Microsoft.ClearScript;
using System.Collections;
using System.Dynamic;

namespace WebviewCore;

public class DomElementHost : DynamicObject
{
    private readonly IElement _element;
    private readonly IDocument _doc;
    private readonly Action? _onChanged;
    private readonly Dictionary<string, object?> _props = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<object> _children = new();
    private readonly List<object> _childList = new();
    private readonly Dictionary<string, List<object>> _events = new();

    public DomElementHost(IElement element, IDocument doc, Action? onChanged)
    {
        _element = element;
        _doc = doc;
        _onChanged = onChanged;

        var tag = element.TagName;
        _props["nodeType"] = 1;
        _props["nodeName"] = tag.ToUpperInvariant();
        _props["tagName"] = tag.ToUpperInvariant();
        _props["localName"] = tag.ToLowerInvariant();
        _props["id"] = element.Id ?? "";
        _props["className"] = element.ClassName ?? "";
        _props["innerHTML"] = element.InnerHtml;
        _props["outerHTML"] = element.OuterHtml;
        _props["textContent"] = element.TextContent;
        _props["innerText"] = element.TextContent;
        _props["style"] = new DomStyleHost(element, _onChanged);
        _props["attributes"] = new DomAttributesHost(element);
        _props["classList"] = new DomClassListHost(element);
        _props["dataset"] = new ExpandoObject();
        _props["childNodes"] = new DomCollectionHost(_children);
        _props["children"] = new DomCollectionHost(_childList);
        _props["offsetWidth"] = 100;
        _props["offsetHeight"] = 20;
        _props["clientWidth"] = 100;
        _props["clientHeight"] = 20;
        _props["scrollWidth"] = 100;
        _props["scrollHeight"] = 20;
        _props["scrollTop"] = 0;
        _props["scrollLeft"] = 0;
        _props["value"] = GetElementValue();
        _props["checked"] = element.HasAttribute("checked");
        _props["selected"] = element.HasAttribute("selected");
        _props["selectedIndex"] = tag.Equals("SELECT", StringComparison.OrdinalIgnoreCase) ? GetSelectedIndex() : -1;
        _props["options"] = tag.Equals("SELECT", StringComparison.OrdinalIgnoreCase)
            ? new DomCollectionHost(element.QuerySelectorAll("option").Select(e => (object)new DomElementHost(e, doc, onChanged)))
            : new DomCollectionHost(Array.Empty<object>());
        _props["disabled"] = element.HasAttribute("disabled");
        _props["src"] = element.GetAttribute("src") ?? "";
        _props["href"] = element.GetAttribute("href") ?? "";
        _props["alt"] = element.GetAttribute("alt") ?? "";
        _props["title"] = element.GetAttribute("title") ?? "";
        _props["lang"] = "";
        _props["dir"] = "";
        _props["hidden"] = element.HasAttribute("hidden");
        _props["tabIndex"] = -1;
        _props["isContentEditable"] = false;
        _props["contentEditable"] = "inherit";
        _props["namespaceURI"] = "http://www.w3.org/1999/xhtml";
        _props["prefix"] = null;

        // Fill children from actual DOM
        foreach (var child in element.ChildNodes)
        {
            var wrapped = WrapNode(child, doc, onChanged);
            if (wrapped != null)
            {
                _children.Add(wrapped);
                if (child is IElement) _childList.Add(wrapped);
            }
        }
    }

    public static object? WrapNode(INode? node, IDocument doc, Action? onChanged)
    {
        if (node == null) return null;
        if (node is IElement el) return new DomElementHost(el, doc, onChanged);
        if (node is IText text) return new DomTextHost(text, doc, onChanged);
        return null;
    }

    public override IEnumerable<string> GetDynamicMemberNames()
    {
        return _props.Keys;
    }

    public override bool TryGetMember(GetMemberBinder binder, out object? result)
    {
        var name = binder.Name;
        switch (name.ToLowerInvariant())
        {
            case "value":
                result = GetElementValue();
                return true;
            case "checked":
                result = _element.HasAttribute("checked");
                return true;
            case "selected":
                result = _element.HasAttribute("selected");
                return true;
            case "selectedindex":
                result = GetSelectedIndex();
                return true;
            case "disabled":
                result = _element.HasAttribute("disabled");
                return true;
            case "options":
                result = new DomCollectionHost(_element.QuerySelectorAll("option").Select(e => (object)new DomElementHost(e, _doc, _onChanged)));
                return true;
            case "getelementsbytagname":
                result = new Func<string, object>(getElementsByTagName);
                return true;
            case "getelementsbyclassname":
                result = new Func<string, object>(getElementsByClassName);
                return true;
            case "getelementsbytagnamens":
                result = new Func<string?, string, object>(getElementsByTagNameNS);
                return true;
        }

        if (_props.TryGetValue(name, out var val))
        {
            result = val;
            return true;
        }

        // Lazy tree-traversal properties — resolved on access to avoid infinite recursion
        switch (name.ToLowerInvariant())
        {
            case "parentnode":
                result = _element.Parent != null ? WrapNode(_element.Parent, _doc, _onChanged) : null;
                return true;
            case "parentelement":
                result = _element.ParentElement != null ? new DomElementHost(_element.ParentElement, _doc, _onChanged) : null;
                return true;
            case "firstchild":
                result = _element.FirstChild != null ? WrapNode(_element.FirstChild, _doc, _onChanged) : null;
                return true;
            case "lastchild":
                result = _element.LastChild != null ? WrapNode(_element.LastChild, _doc, _onChanged) : null;
                return true;
            case "nextsibling":
                result = _element.NextSibling != null ? WrapNode(_element.NextSibling, _doc, _onChanged) : null;
                return true;
            case "previoussibling":
                result = _element.PreviousSibling != null ? WrapNode(_element.PreviousSibling, _doc, _onChanged) : null;
                return true;
            case "ownerdocument":
                result = new DocumentHost(_doc, _onChanged);
                return true;
        }

        result = null;
        return false;
    }

    public override bool TrySetMember(SetMemberBinder binder, object? value)
    {
        var name = binder.Name.ToLowerInvariant();
        var strVal = value?.ToString() ?? "";

        switch (name)
        {
            case "textcontent":
            case "innertext":
                _element.TextContent = strVal;
                _props["textContent"] = strVal;
                _props["innerText"] = strVal;
                _onChanged?.Invoke();
                return true;

            case "innerhtml":
                _element.InnerHtml = strVal;
                _props["innerHTML"] = strVal;
                _props["textContent"] = _element.TextContent;
                _props["innerText"] = _element.TextContent;
                _onChanged?.Invoke();
                return true;

            case "outerhtml":
                _element.OuterHtml = strVal;
                _props["outerHTML"] = strVal;
                _onChanged?.Invoke();
                return true;

            case "id":
                _element.Id = strVal;
                _props["id"] = strVal;
                _onChanged?.Invoke();
                return true;

            case "classname":
                _element.ClassName = strVal;
                _props["className"] = strVal;
                _onChanged?.Invoke();
                return true;

            case "value":
                SetElementValue(strVal);
                _props["value"] = GetElementValue();
                _props["selectedIndex"] = GetSelectedIndex();
                _onChanged?.Invoke();
                return true;

            case "checked":
                var isChecked = value is bool b ? b : (value != null && value.ToString() != "false" && value.ToString() != "0");
                if (isChecked) _element.SetAttribute("checked", "");
                else _element.RemoveAttribute("checked");
                _props["checked"] = isChecked;
                _onChanged?.Invoke();
                return true;

            case "selected":
                var isSelected = value is bool bs ? bs : (value != null && value.ToString() != "false" && value.ToString() != "0");
                if (isSelected) _element.SetAttribute("selected", "");
                else _element.RemoveAttribute("selected");
                _props["selected"] = isSelected;
                _onChanged?.Invoke();
                return true;

            case "selectedindex":
                if (int.TryParse(strVal, out var index))
                {
                    SetSelectedIndex(index);
                    _props["selectedIndex"] = GetSelectedIndex();
                    _props["value"] = GetElementValue();
                    _onChanged?.Invoke();
                }
                return true;

            case "src":
                _element.SetAttribute("src", strVal);
                _props["src"] = strVal;
                _onChanged?.Invoke();
                return true;

            case "href":
                _element.SetAttribute("href", strVal);
                _props["href"] = strVal;
                _onChanged?.Invoke();
                return true;

            case "disabled":
                if (value is bool bdis && bdis) _element.SetAttribute("disabled", "");
                else if (value != null && value.ToString() != "false") _element.SetAttribute("disabled", "");
                else _element.RemoveAttribute("disabled");
                _props["disabled"] = value is bool b2 ? b2 : true;
                _onChanged?.Invoke();
                return true;

            case "title":
                _element.SetAttribute("title", strVal);
                _props["title"] = strVal;
                _onChanged?.Invoke();
                return true;

            case "style":
                if (value is IDictionary<string, object> styleDict)
                {
                    foreach (var kv in styleDict)
                        _element.SetAttribute("style", $"{kv.Key}:{kv.Value}");
                }
                else if (value is string styleStr)
                {
                    _element.SetAttribute("style", styleStr);
                }
                _props["style"] = new DomStyleHost(_element, _onChanged);
                _onChanged?.Invoke();
                return true;
        }

        _props[name] = value;
        return true;
    }

    private string GetElementValue()
    {
        var tag = _element.TagName.ToLowerInvariant();
        if (tag == "select")
        {
            var option = GetSelectedOption() ?? _element.QuerySelector("option");
            if (option == null) return "";
            return option.GetAttribute("value") ?? option.TextContent;
        }

        if (tag == "textarea")
            return _element.GetAttribute("value") ?? _element.TextContent ?? "";

        return _element.GetAttribute("value") ?? "";
    }

    private void SetElementValue(string value)
    {
        var tag = _element.TagName.ToLowerInvariant();
        if (tag == "select")
        {
            var options = _element.QuerySelectorAll("option").ToArray();
            var matched = false;
            foreach (var option in options)
            {
                var optionValue = option.GetAttribute("value") ?? option.TextContent;
                var selected = !matched && optionValue == value;
                if (selected) { option.SetAttribute("selected", ""); matched = true; }
                else option.RemoveAttribute("selected");
            }
            return;
        }

        if (tag == "textarea") _element.TextContent = value;
        _element.SetAttribute("value", value);
    }

    private IElement? GetSelectedOption()
    {
        if (!_element.TagName.Equals("select", StringComparison.OrdinalIgnoreCase)) return null;
        return _element.QuerySelectorAll("option").FirstOrDefault(o => o.HasAttribute("selected"));
    }

    private int GetSelectedIndex()
    {
        var options = _element.QuerySelectorAll("option").ToArray();
        for (var i = 0; i < options.Length; i++)
            if (options[i].HasAttribute("selected")) return i;
        return options.Length > 0 ? 0 : -1;
    }

    private void SetSelectedIndex(int index)
    {
        var options = _element.QuerySelectorAll("option").ToArray();
        for (var i = 0; i < options.Length; i++)
        {
            if (i == index) options[i].SetAttribute("selected", "");
            else options[i].RemoveAttribute("selected");
        }
    }

    public override bool TryInvokeMember(InvokeMemberBinder binder, object?[]? args, out object? result)
    {
        result = null;
        return false; // Rely on real C# methods below for ClearScript compatibility
    }

    // ─── Real C# methods for ClearScript reflection fallback ─────────

    public object appendChild(object child)
    {
        if (child == null) return null!;
        if (child is INode node) _element.AppendChild(node);
        else if (child is DomElementHost deh) _element.AppendChild(deh._element);
        else if (child is DomTextHost dth) _element.AppendChild(dth.TextNode);
        else
        {
            var text = child.ToString() ?? "";
            var tn = _doc.CreateTextNode(text);
            _element.AppendChild(tn);
        }
        _children.Add(child);
        _childList.Add(child);
        _onChanged?.Invoke();
        return child;
    }

    public object removeChild(object child)
    {
        if (child is INode node) _element.RemoveChild(node);
        else if (child is DomElementHost deh) _element.RemoveChild(deh._element);
        else if (child is DomTextHost dth) _element.RemoveChild(dth.TextNode);
        _children.Remove(child);
        _childList.Remove(child);
        _onChanged?.Invoke();
        return child;
    }

    public object insertBefore(object newChild, object refChild)
    {
        INode? nc = null, rc = null;
        if (newChild is INode n1) nc = n1;
        else if (newChild is DomElementHost deh1) nc = deh1._element;
        else if (newChild is DomTextHost dth1) nc = dth1.TextNode;
        if (refChild is INode r1) rc = r1;
        else if (refChild is DomElementHost deh2) rc = deh2._element;
        else if (refChild is DomTextHost dth2) rc = dth2.TextNode;
        if (nc != null)
        {
            if (rc != null) _element.InsertBefore(nc, rc);
            else _element.AppendChild(nc);
        }
        _onChanged?.Invoke();
        return newChild;
    }

    public object replaceChild(object newChild, object oldChild)
    {
        INode? nc = null, oc = null;
        if (newChild is INode n1) nc = n1;
        else if (newChild is DomElementHost deh1) nc = deh1._element;
        if (oldChild is INode o1) oc = o1;
        else if (oldChild is DomElementHost deh2) oc = deh2._element;
        if (nc != null && oc != null) _element.ReplaceChild(nc, oc);
        _onChanged?.Invoke();
        return oldChild;
    }

    public object cloneNode(bool deep)
    {
        return new DomElementHost((IElement)_element.Clone(deep), _doc, _onChanged);
    }

    public bool hasChildNodes()
    {
        return _element.ChildNodes.Length > 0;
    }

    public void setAttribute(string name, string value)
    {
        _element.SetAttribute(name, value);
        if (name.ToLowerInvariant() == "id") _props["id"] = value;
        if (name.ToLowerInvariant() == "class") _props["className"] = value;
        if (name.ToLowerInvariant() == "value") _props["value"] = value;
        if (name.ToLowerInvariant() == "style") _props["style"] = new DomStyleHost(_element, _onChanged);
        _onChanged?.Invoke();
    }

    public string? getAttribute(string name)
    {
        return _element.GetAttribute(name);
    }

    public void removeAttribute(string name)
    {
        _element.RemoveAttribute(name);
        _onChanged?.Invoke();
    }

    public bool hasAttribute(string name)
    {
        return _element.HasAttribute(name);
    }

    public bool hasAttributes()
    {
        return _element.Attributes.Length > 0;
    }

    public void focus() { }
    public void blur() { }
    public void click()
    {
        dispatchEvent(DomEventHost.Create("click", _element));
    }

    public void addEventListener(string type, object handler)
    {
        if (!_events.ContainsKey(type)) _events[type] = new List<object>();
        _events[type].Add(handler);
    }

    public void removeEventListener(string type, object handler)
    {
        if (_events.TryGetValue(type, out var list)) list.Remove(handler);
    }

    public bool dispatchEvent(object e)
    {
        DomEventHost.Normalize(e, _element);
        if (_events.TryGetValue(DomEventHost.GetTypeName(e), out var list))
        {
            foreach (var handler in list.ToArray())
                InvokeHandler(handler, e);
        }

        return !DomEventHost.DefaultPrevented(e);
    }

    public object getBoundingClientRect()
    {
        return new { top = 0f, right = 0f, bottom = 0f, left = 0f, width = 0f, height = 0f, x = 0f, y = 0f };
    }

    public object[] getClientRects() => Array.Empty<object>();

    public void scrollIntoView() { }

    public bool matches(string selector)
    {
        return _element.Matches(selector);
    }

    public object? querySelector(string selector)
    {
        var found = _element.QuerySelector(selector);
        return found != null ? new DomElementHost(found, _doc, _onChanged) : null;
    }

    public object querySelectorAll(string selector)
    {
        return new DomCollectionHost(_element.QuerySelectorAll(selector).Select(e => (object)new DomElementHost(e, _doc, _onChanged)));
    }

    public object getElementsByTagName(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag)) return new DomCollectionHost(Array.Empty<object>());
        try
        {
            return new DomCollectionHost(_element.QuerySelectorAll(tag.Trim()).Select(e => (object)new DomElementHost(e, _doc, _onChanged)));
        }
        catch
        {
            return new DomCollectionHost(Array.Empty<object>());
        }
    }

    public object getElementsByTagNameNS(string? ns, string tag)
    {
        return getElementsByTagName(tag);
    }

    public object getElementsByClassName(string className)
    {
        if (string.IsNullOrWhiteSpace(className)) return new DomCollectionHost(Array.Empty<object>());
        var selector = string.Join("", className.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(c => "." + EscapeCssIdent(c)));
        if (selector.Length == 0) return new DomCollectionHost(Array.Empty<object>());
        try
        {
            return new DomCollectionHost(_element.QuerySelectorAll(selector).Select(e => (object)new DomElementHost(e, _doc, _onChanged)));
        }
        catch
        {
            return new DomCollectionHost(Array.Empty<object>());
        }
    }

    public object? closest(string selector)
    {
        var found = _element.Closest(selector);
        return found != null ? new DomElementHost(found, _doc, _onChanged) : null;
    }

    public bool contains(object other)
    {
        if (other is DomElementHost deh) return _element.Contains(deh._element);
        if (other is DomTextHost dth) return _element.Contains(dth.TextNode);
        return false;
    }

    public void remove()
    {
        _element.Remove();
        _onChanged?.Invoke();
    }

    public void before(object n)
    {
        if (_element.ParentElement == null) return;
        var node = ToNode(n);
        if (node != null) _element.ParentElement.InsertBefore(node, _element);
        _onChanged?.Invoke();
    }

    public void after(object n)
    {
        if (_element.ParentElement == null) return;
        var node = ToNode(n);
        if (node != null) _element.ParentElement.InsertBefore(node, _element.NextSibling);
        _onChanged?.Invoke();
    }

    public void replaceWith(object n)
    {
        if (_element.ParentElement == null) return;
        var node = ToNode(n);
        if (node != null) _element.ParentElement.ReplaceChild(node, _element);
        _onChanged?.Invoke();
    }

    public void prepend(object n)
    {
        var node = ToNode(n);
        if (node != null) _element.InsertBefore(node, _element.FirstChild);
        _onChanged?.Invoke();
    }

    public void append(object n)
    {
        var node = ToNode(n);
        if (node != null) _element.AppendChild(node);
        _onChanged?.Invoke();
    }

    public object animate()
    {
        return new { play = new Action(() => { }), pause = new Action(() => { }), finish = new Action(() => { }), cancel = new Action(() => { }), finished = Task.CompletedTask, playState = "idle" };
    }

    public void insertAdjacentHTML(string position, string html)
    {
        var pos = position.ToLowerInvariant() switch
        {
            "beforebegin" => AdjacentPosition.BeforeBegin,
            "afterbegin" => AdjacentPosition.AfterBegin,
            "beforeend" => AdjacentPosition.BeforeEnd,
            "afterend" => AdjacentPosition.AfterEnd,
            _ => AdjacentPosition.BeforeEnd,
        };
        _element.Insert(pos, html);
        _onChanged?.Invoke();
    }

    public void insertAdjacentElement(string position, object element)
    {
        var node = ToNode(element);
        if (node != null) InsertAdjacentNode(position, node);
    }

    public void insertAdjacentText(string position, object text)
    {
        InsertAdjacentNode(position, _doc.CreateTextNode(text?.ToString() ?? ""));
    }

    internal IElement Element => _element;

    private static string EscapeCssIdent(string value)
    {
        if (value.Length == 0) return value;
        var sb = new System.Text.StringBuilder();
        foreach (var ch in value)
        {
            if (char.IsLetterOrDigit(ch) || ch is '-' or '_')
                sb.Append(ch);
            else
                sb.Append('\\').Append(((int)ch).ToString("x")).Append(' ');
        }
        return sb.ToString();
    }

    private INode? ToNode(object? value)
    {
        if (value == null) return null;
        if (value is INode n) return n;
        if (value is DomElementHost deh) return deh._element;
        if (value is DomTextHost dth) return dth.TextNode;
        return _doc.CreateTextNode(value.ToString() ?? "");
    }

    private void InsertAdjacentNode(string position, INode node)
    {
        switch (position.ToLowerInvariant())
        {
            case "beforebegin":
                _element.ParentElement?.InsertBefore(node, _element);
                break;
            case "afterbegin":
                _element.InsertBefore(node, _element.FirstChild);
                break;
            case "afterend":
                _element.ParentElement?.InsertBefore(node, _element.NextSibling);
                break;
            default:
                _element.AppendChild(node);
                break;
        }

        _onChanged?.Invoke();
    }

    private static void InvokeHandler(object handler, object evt)
    {
        try
        {
            ScriptInterop.Invoke(handler, evt);
        }
        catch
        {
        }
    }

    // Expose to ClearScript via indexer
    public object? this[string key]
    {
        get
        {
            if (_props.TryGetValue(key, out var v)) return v;
            TryGetMember(new GetMemberBinderSimple(key), out var m);
            return m;
        }
        set => TrySetMember(new SetMemberBinderSimple(key), value);
    }

    private class GetMemberBinderSimple : GetMemberBinder
    {
        public GetMemberBinderSimple(string name) : base(name, false) { }
        public override DynamicMetaObject FallbackGetMember(DynamicMetaObject target, DynamicMetaObject? errorSuggestion) => errorSuggestion ?? throw new NotSupportedException();
    }

    private class SetMemberBinderSimple : SetMemberBinder
    {
        public SetMemberBinderSimple(string name) : base(name, false) { }
        public override DynamicMetaObject FallbackSetMember(DynamicMetaObject target, DynamicMetaObject value, DynamicMetaObject? errorSuggestion) => errorSuggestion ?? throw new NotSupportedException();
    }
}

public class DomCollectionHost : DynamicObject
{
    private readonly IList<object> _items;

    public DomCollectionHost(IEnumerable<object> items)
    {
        _items = items as IList<object> ?? items.ToList();
    }

    public int length => _items.Count;

    public object? item(int index)
    {
        return index >= 0 && index < _items.Count ? _items[index] : null;
    }

    public object? namedItem(string name)
    {
        foreach (var item in _items)
        {
            if (item is DomElementHost element)
            {
                var el = element.Element;
                if (el.Id == name || el.GetAttribute("name") == name) return item;
            }
        }
        return null;
    }

    public override bool TryGetMember(GetMemberBinder binder, out object? result)
    {
        var name = binder.Name;
        switch (name.ToLowerInvariant())
        {
            case "length":
                result = length;
                return true;
            case "item":
                result = new Func<int, object?>(item);
                return true;
            case "nameditem":
                result = new Func<string, object?>(namedItem);
                return true;
        }

        if (int.TryParse(name, out var index))
        {
            result = item(index);
            return true;
        }

        result = namedItem(name);
        return result != null;
    }

    public override bool TryGetIndex(GetIndexBinder binder, object[] indexes, out object? result)
    {
        if (indexes.Length == 1)
        {
            if (indexes[0] is int i)
            {
                result = item(i);
                return true;
            }

            if (int.TryParse(indexes[0]?.ToString(), out var parsed))
            {
                result = item(parsed);
                return true;
            }
        }

        result = null;
        return false;
    }
}

public class DomTextHost : DynamicObject
{
    public IText TextNode { get; }
    private readonly IDocument _doc;
    private readonly Action? _onChanged;
    private readonly Dictionary<string, object?> _props = new();

    public DomTextHost(IText text, IDocument doc, Action? onChanged)
    {
        TextNode = text;
        _doc = doc;
        _onChanged = onChanged;

        _props["nodeType"] = 3;
        _props["nodeName"] = "#text";
        _props["textContent"] = text.Text ?? "";
        _props["data"] = text.Text ?? "";
        _props["nodeValue"] = text.Text ?? "";
        _props["length"] = (text.Text ?? "").Length;
    }

    public override bool TryGetMember(GetMemberBinder binder, out object? result)
    {
        if (_props.TryGetValue(binder.Name, out var val))
        {
            result = val;
            return true;
        }
        result = null;
        return true;
    }

    public override bool TrySetMember(SetMemberBinder binder, object? value)
    {
        var str = value?.ToString() ?? "";
        _props[binder.Name] = str;
        switch (binder.Name.ToLowerInvariant())
        {
            case "textcontent":
            case "data":
            case "nodevalue":
                TextNode.TextContent = str;
                _props["textContent"] = str;
                _props["data"] = str;
                _props["nodeValue"] = str;
                _props["length"] = str.Length;
                _onChanged?.Invoke();
                break;
        }
        return true;
    }

    public void remove()
    {
        TextNode.Remove();
        _onChanged?.Invoke();
    }

    public object splitText(int offset)
    {
        var text = TextNode.Text ?? "";
        offset = Math.Max(0, Math.Min(offset, text.Length));
        var tail = text[offset..];
        TextNode.TextContent = text[..offset];
        var newNode = _doc.CreateTextNode(tail);
        TextNode.Parent?.InsertBefore(newNode, TextNode.NextSibling);
        _onChanged?.Invoke();
        return new DomTextHost(newNode, _doc, _onChanged);
    }
}

public class DomStyleHost : DynamicObject
{
    private readonly IElement _element;
    private readonly Dictionary<string, string> _styles = new();
    private readonly Action? _onChanged;

    // JS → CSS property name mapping (e.g. cssFloat → float)
    private static readonly Dictionary<string, string> JsToCss = new(StringComparer.OrdinalIgnoreCase)
    {
        ["cssFloat"] = "float",
        ["styleFloat"] = "float",
    };

    // CSS → JS property name mapping (e.g. float → cssFloat)
    private static readonly Dictionary<string, string> CssToJs = new(StringComparer.OrdinalIgnoreCase)
    {
        ["float"] = "cssFloat",
    };

    public DomStyleHost(IElement element, Action? onChanged = null)
    {
        _element = element;
        _onChanged = onChanged;
        var styleAttr = element.GetAttribute("style") ?? "";
        foreach (var part in styleAttr.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = part.Split(':', 2, StringSplitOptions.TrimEntries);
            if (kv.Length == 2) _styles[StyleComputer.NormalizePropertyName(kv[0])] = kv[1];
        }
    }

    public override bool TryGetMember(GetMemberBinder binder, out object? result)
    {
        var name = binder.Name;
        if (name == "cssFloat" || name == "styleFloat") name = "float";
        var lowerName = StyleComputer.NormalizePropertyName(name);

        if (string.Equals(binder.Name, "cssText", StringComparison.OrdinalIgnoreCase))
        {
            result = _element.GetAttribute("style") ?? "";
            return true;
        }
        if (string.Equals(binder.Name, "setProperty", StringComparison.OrdinalIgnoreCase))
        {
            result = new Action<string, string, string?>((prop, val, pri) =>
            {
                _styles[StyleComputer.NormalizePropertyName(prop)] = val;
                SyncStyleAttribute();
                _onChanged?.Invoke();
            });
            return true;
        }
        if (string.Equals(binder.Name, "getPropertyValue", StringComparison.OrdinalIgnoreCase))
        {
            result = new Func<string, string>(prop =>
                _styles.TryGetValue(StyleComputer.NormalizePropertyName(prop), out var value) ? value : "");
            return true;
        }
        if (string.Equals(binder.Name, "removeProperty", StringComparison.OrdinalIgnoreCase))
        {
            result = new Func<string, string>(prop =>
            {
                var key = StyleComputer.NormalizePropertyName(prop);
                var old = _styles.TryGetValue(key, out var value) ? value : "";
                _styles.Remove(key);
                SyncStyleAttribute();
                _onChanged?.Invoke();
                return old;
            });
            return true;
        }
        if (_styles.TryGetValue(lowerName, out var v))
        {
            result = v;
            return true;
        }
        result = "";
        return true;
    }

    public override bool TrySetMember(SetMemberBinder binder, object? value)
    {
        var name = binder.Name;
        var strVal = value?.ToString() ?? "";

        if (name == "cssFloat" || name == "styleFloat") name = "float";
        var lowerName = StyleComputer.NormalizePropertyName(name);

        if (string.Equals(binder.Name, "cssText", StringComparison.OrdinalIgnoreCase))
        {
            _element.SetAttribute("style", strVal);
            _styles.Clear();
            foreach (var part in strVal.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var kv = part.Split(':', 2, StringSplitOptions.TrimEntries);
                if (kv.Length == 2) _styles[StyleComputer.NormalizePropertyName(kv[0])] = kv[1];
            }
        }
        else
        {
            _styles[lowerName] = strVal;
            SyncStyleAttribute();
        }
        _onChanged?.Invoke();
        return true;
    }

    private void SyncStyleAttribute()
    {
        var cssText = string.Join(";", _styles
            .Where(kv => kv.Key != "csstext" && kv.Key != "setproperty")
            .Select(kv => $"{kv.Key}:{kv.Value}"));
        _element.SetAttribute("style", cssText);
    }
}

public class DomAttributesHost : DynamicObject
{
    private readonly IElement _element;

    public DomAttributesHost(IElement element) { _element = element; }

    public override bool TryGetMember(GetMemberBinder binder, out object? result)
    {
        var name = binder.Name.ToLowerInvariant();

        if (name == "getnameditem")
        {
            result = new Func<string, object?>((n) =>
            {
                var v = _element.GetAttribute(n);
                return v != null ? new { name = n, value = v, specified = true } : null;
            });
            return true;
        }

        var val = _element.GetAttribute(binder.Name);
        if (val != null)
        {
            result = val;
            return true;
        }
        result = null;
        return true;
    }

    public override bool TrySetMember(SetMemberBinder binder, object? value)
    {
        _element.SetAttribute(binder.Name, value?.ToString() ?? "");
        return true;
    }
}

public class DomClassListHost : DynamicObject
{
    private readonly IElement _element;
    private List<string> _classes;

    public DomClassListHost(IElement element)
    {
        _element = element;
        _classes = new List<string>(element.ClassList);
    }

    public int length => _classes.Count;

    public void add(string c)
    {
        if (!_classes.Contains(c))
        {
            _classes.Add(c);
            _element.ClassList.Add(c);
        }
    }

    public void remove(string c)
    {
        _classes.Remove(c);
        _element.ClassList.Remove(c);
    }

    public bool contains(string c) => _classes.Contains(c);

    public bool toggle(string c)
    {
        if (_classes.Contains(c))
        {
            remove(c);
            return false;
        }

        add(c);
        return true;
    }

    public void replace(string o, string n)
    {
        var i = _classes.IndexOf(o);
        if (i >= 0)
        {
            _classes[i] = n;
            _element.ClassList.Remove(o);
            _element.ClassList.Add(n);
        }
    }

    public string? item(int i) => i >= 0 && i < _classes.Count ? _classes[i] : null;

    public override string ToString() => string.Join(" ", _classes);

    public override bool TryGetMember(GetMemberBinder binder, out object? result)
    {
        switch (binder.Name.ToLowerInvariant())
        {
            case "length":
                result = length;
                return true;
            case "add":
                result = new Action<string>(add);
                return true;
            case "remove":
                result = new Action<string>(remove);
                return true;
            case "contains":
                result = new Func<string, bool>(contains);
                return true;
            case "toggle":
                result = new Func<string, bool>(toggle);
                return true;
            case "replace":
                result = new Action<string, string>(replace);
                return true;
            case "item":
                result = new Func<int, string?>(item);
                return true;
            case "tostring":
                result = new Func<string>(ToString);
                return true;
        }
        result = null;
        return true;
    }
}
