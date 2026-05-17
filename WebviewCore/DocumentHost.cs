using AngleSharp.Dom;
using Microsoft.ClearScript;
using System.Collections;
using System.Dynamic;

namespace WebviewCore;

public class DocumentHost : DynamicObject
{
    internal string _docWrite = "";
    private IDocument? _doc;
    private Action? _onChanged;

    private readonly Dictionary<string, object?> _members = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<object>> _docEvents = new();

    public DocumentHost()
    {
        _members["write"] = new Action<string>(html => { _docWrite += html; });
        _members["writeln"] = new Action<string>(html => { _docWrite += html + "\n"; });
        _members["open"] = new Action(() => { _docWrite = ""; });
        _members["close"] = new Action(() => { });
        _members["clear"] = new Action(() => { _docWrite = ""; });
    }

    public DocumentHost(IDocument doc, Action? onChanged) : this()
    {
        _doc = doc;
        _onChanged = onChanged;
    }

    public void SetDocument(IDocument doc, JsEngine js)
    {
        _doc = doc;
        _onChanged = js.NotifyDomChanged;
    }

    // ─── DynamicObject overrides for ClearScript compatibility ──────

    public override IEnumerable<string> GetDynamicMemberNames()
    {
        foreach (var k in _members.Keys) yield return k;
        yield return "body"; yield return "head"; yield return "documentElement";
        yield return "getElementById"; yield return "querySelector"; yield return "querySelectorAll";
        yield return "createElement"; yield return "createTextNode"; yield return "createComment";
    }

    public override bool TryGetMember(GetMemberBinder binder, out object? result)
    {
        var name = binder.Name;

        if (_members.TryGetValue(name, out var val))
        {
            result = val;
            return true;
        }

        switch (name)
        {
            case "body": result = _doc?.Body != null ? new DomElementHost(_doc.Body, _doc!, _onChanged) : null; return true;
            case "head": result = _doc?.Head != null ? new DomElementHost(_doc.Head, _doc!, _onChanged) : null; return true;
            case "documentElement": result = _doc?.DocumentElement != null ? new DomElementHost(_doc.DocumentElement, _doc!, _onChanged) : null; return true;
            case "activeElement": result = _doc?.Body != null ? new DomElementHost(_doc.Body, _doc!, _onChanged) : null; return true;
            case "title": result = _doc?.Title ?? ""; return true;

            case "getElementById": result = MakeGetElementById(); return true;
            case "getElementsByTagName": result = MakeGetElementsByTagName(); return true;
            case "getElementsByClassName": result = MakeGetElementsByClassName(); return true;
            case "getElementsByName": result = MakeGetElementsByName(); return true;
            case "querySelector": result = MakeQuerySelector(); return true;
            case "querySelectorAll": result = MakeQuerySelectorAll(); return true;
            case "createElement": result = MakeCreateElement(); return true;
            case "createElementNS": result = MakeCreateElementNS(); return true;
            case "createTextNode": result = MakeCreateTextNode(); return true;
            case "createComment": result = MakeCreateComment(); return true;
            case "createDocumentFragment": result = MakeCreateDocumentFragment(); return true;

            case "addEventListener": result = MakeAddEventListener(); return true;
            case "removeEventListener": result = MakeRemoveEventListener(); return true;
            case "dispatchEvent": result = MakeDispatchEvent(); return true;

            case "implementation":
                result = new
                {
                    createHTMLDocument = new Func<string, object>(t => new DocumentHost()),
                    hasFeature = new Func<string, string, bool>((f, v) => true),
                    createDocumentType = new Func<string, string, string, object>((n, p, s) => new { name = n, publicId = p, systemId = s }),
                };
                return true;

            case "fonts":
                result = new
                {
                    ready = Task.CompletedTask, status = "loaded",
                    add = new Action(() => { }), remove = new Action(() => { }),
                    clear = new Action(() => { }), load = new Func<string, Task>(f => Task.CompletedTask),
                    check = new Func<string, bool>(f => true),
                };
                return true;

            case "featurePolicy":
                result = new
                {
                    allowsFeature = new Func<string, bool>(f => true),
                    features = new Func<string[]>(() => Array.Empty<string>()),
                    allowedFeatures = new Func<string[]>(() => Array.Empty<string>()),
                    getAllowlistForFeature = new Func<string, string[]>(f => Array.Empty<string>()),
                };
                return true;

            case "timeline": result = new { currentTime = (object?)null }; return true;
            case "doctype": result = new { name = "html", publicId = "", systemId = "", nodeType = 10, nodeName = "html" }; return true;
        }

        result = null;
        return false; // Fallback to reflection for other C# properties
    }

    public override bool TrySetMember(SetMemberBinder binder, object? value)
    {
        var name = binder.Name;
        switch (name)
        {
            case "title": if (_doc != null) _doc.Title = value?.ToString() ?? ""; return true;
            case "cookie": return true;
            case "URL": case "documentURI": case "referrer": case "domain": case "charset": case "characterSet":
                return true;
        }
        return false; // Fallback to reflection
    }

    public override bool TryInvokeMember(InvokeMemberBinder binder, object?[]? args, out object? result)
    {
        var name = binder.Name;

        if (_members.TryGetValue(name, out var fn))
        {
            if (fn is Delegate d && args != null)
            {
                result = d.DynamicInvoke(args);
                return true;
            }
        }

        result = null;
        return false; // Fallback to reflection for C# methods
    }

    // ─── Delegate factories for DOM methods ──────────────────────────

    private Func<string, object?> MakeGetElementById() => (id) =>
    {
        var el = _doc?.GetElementById(id);
        if (el != null) return new DomElementHost(el, _doc!, _onChanged);
        return null;
    };

    private Func<string, object> MakeGetElementsByTagName() => (tag) =>
    {
        var elements = _doc?.GetElementsByTagName(tag);
        if (elements == null) return new DomCollectionHost(Array.Empty<object>());
        return new DomCollectionHost(elements.Select(e => (object)new DomElementHost(e, _doc!, _onChanged)));
    };

    private Func<string, object> MakeGetElementsByClassName() => (cls) =>
    {
        var elements = _doc?.GetElementsByClassName(cls);
        if (elements == null) return new DomCollectionHost(Array.Empty<object>());
        return new DomCollectionHost(elements.Select(e => (object)new DomElementHost(e, _doc!, _onChanged)));
    };

    private Func<string, object> MakeGetElementsByName() => (name) =>
    {
        var elements = _doc?.GetElementsByName(name);
        if (elements == null) return new DomCollectionHost(Array.Empty<object>());
        return new DomCollectionHost(elements.Select(e => (object)new DomElementHost(e, _doc!, _onChanged)));
    };

    private Func<string, object?> MakeQuerySelector() => (sel) =>
    {
        var el = _doc?.QuerySelector(sel);
        if (el != null) return new DomElementHost(el, _doc!, _onChanged);
        return null;
    };

    private Func<string, object> MakeQuerySelectorAll() => (sel) =>
    {
        var elements = _doc?.QuerySelectorAll(sel);
        if (elements == null) return new DomCollectionHost(Array.Empty<object>());
        return new DomCollectionHost(elements.Select(e => (object)new DomElementHost(e, _doc!, _onChanged)));
    };

    private Func<string, object> MakeCreateElement() => (tag) =>
    {
        if (_doc != null)
        {
            var el = _doc.CreateElement(tag ?? "div");
            return new DomElementHost(el, _doc, _onChanged);
        }
        return MakeFakeElement(tag ?? "div");
    };

    private Func<string?, string?, object> MakeCreateElementNS() => (ns, tag) =>
    {
        if (_doc != null && tag != null)
        {
            var el = _doc.CreateElement(ns ?? "", tag ?? "div");
            return new DomElementHost(el, _doc, _onChanged);
        }
        return MakeFakeElement(tag ?? "div");
    };

    private Func<string, object> MakeCreateTextNode() => (text) =>
    {
        if (_doc != null)
        {
            var tn = _doc.CreateTextNode(text ?? "");
            return new DomTextHost(tn, _doc, _onChanged);
        }
        return MakeFakeTextNode(text ?? "");
    };

    private Func<string, object> MakeCreateComment() => (data) => MakeFakeComment(data ?? "");

    private Func<object> MakeCreateDocumentFragment() => () => MakeFakeElement("");

    private Func<string, object, object> MakeAddEventListener() => (type, handler) =>
    {
        if (!_docEvents.ContainsKey(type)) _docEvents[type] = new List<object>();
        _docEvents[type].Add(handler);
        return null!;
    };

    private Func<string, object, object> MakeRemoveEventListener() => (type, handler) =>
    {
        if (_docEvents.TryGetValue(type, out var list)) list.Remove(handler);
        return null!;
    };

    private Func<object, bool> MakeDispatchEvent() => (e) =>
    {
        DomEventHost.Normalize(e, _doc);
        if (_docEvents.TryGetValue(DomEventHost.GetTypeName(e), out var list))
        {
            foreach (var handler in list.ToArray())
                InvokeHandler(handler, e);
        }

        return !DomEventHost.DefaultPrevented(e);
    };

    // ─── Simple properties (accessed via reflection fallback) ────────

    public int nodeType => 9;
    public string nodeName => "#document";

    public string URL { get; set; } = "";
    public string documentURI { get; set; } = "";
    public string referrer { get; set; } = "";
    public string domain { get; set; } = "";
    public string cookie { get; set; } = "";
    public string title { get => _doc?.Title ?? ""; set { if (_doc != null) _doc.Title = value; } }
    public string readyState { get; set; } = "complete";
    public string compatMode { get; set; } = "CSS1Compat";
    public string charset { get; set; } = "UTF-8";
    public string characterSet { get; set; } = "UTF-8";
    public string contentType { get; set; } = "text/html";

    public object doctype => new { name = "html", publicId = "", systemId = "", nodeType = 10, nodeName = "html" };
    public object[] embeds => Array.Empty<object>();
    public object[] plugins => Array.Empty<object>();
    public object[] scripts => Array.Empty<object>();
    public object[] links => Array.Empty<object>();
    public object[] forms => Array.Empty<object>();
    public object[] images => Array.Empty<object>();
    public object[] anchors => Array.Empty<object>();
    public object[] applets => Array.Empty<object>();
    public object[] all => Array.Empty<object>();
    public object[] styleSheets => Array.Empty<object>();

    public object? documentElement => _doc?.DocumentElement != null ? Wrap(_doc.DocumentElement) : null;
    public object? body => _doc?.Body != null ? Wrap(_doc.Body) : null;
    public object? head => _doc?.Head != null ? Wrap(_doc.Head) : null;
    public object? defaultView => null;
    public object? ownerDocument => null;

    public object implementation => new
    {
        createHTMLDocument = new Func<string, object>(t => new DocumentHost()),
        hasFeature = new Func<string, string, bool>((f, v) => true),
        createDocumentType = new Func<string, string, string, object>((n, p, s) => new { name = n, publicId = p, systemId = s }),
    };

    public object? activeElement => body;
    public object? currentScript => null;
    public bool hidden => false;
    public string visibilityState => "visible";
    public bool wasDiscarded => false;
    public bool isConnected => true;
    public string origin => "";
    public string lastModified => DateTime.Now.ToShortDateString();
    public object timeline => new { currentTime = (object?)null };

    public object fonts => new
    {
        ready = Task.CompletedTask,
        status = "loaded",
        add = new Action(() => { }),
        remove = new Action(() => { }),
        clear = new Action(() => { }),
        load = new Func<string, Task>(f => Task.CompletedTask),
        check = new Func<string, bool>(f => true),
    };

    public object? fullscreenElement => null;
    public bool fullscreenEnabled => false;
    public object? pointerLockElement => null;
    public object featurePolicy => new
    {
        allowsFeature = new Func<string, bool>(f => true),
        features = new Func<string[]>(() => Array.Empty<string>()),
        allowedFeatures = new Func<string[]>(() => Array.Empty<string>()),
        getAllowlistForFeature = new Func<string, string[]>(f => Array.Empty<string>()),
    };

    private readonly Dictionary<string, List<object>> _ev = new();

    public void addEventListener(string type, object handler)
    {
        if (!_ev.ContainsKey(type)) _ev[type] = new List<object>();
        _ev[type].Add(handler);
    }

    public void removeEventListener(string type, object handler)
    {
        if (_ev.TryGetValue(type, out var list)) list.Remove(handler);
    }

    public bool dispatchEvent(object e)
    {
        DomEventHost.Normalize(e, _doc);
        if (_ev.TryGetValue(DomEventHost.GetTypeName(e), out var list))
        {
            foreach (var handler in list.ToArray())
                InvokeHandler(handler, e);
        }

        return !DomEventHost.DefaultPrevented(e);
    }
    public bool hasFocus() => true;
    public bool hasStorageAccess() => true;
    public void requestStorageAccess() { }

    public object createElement(string tag)
    {
        if (_doc != null)
        {
            var el = _doc.CreateElement(tag ?? "div");
            return Wrap(el);
        }
        return MakeFakeElement(tag ?? "div");
    }

    public object createElementNS(string ns, string tag)
    {
        if (_doc != null)
        {
            var el = _doc.CreateElement(ns, tag ?? "div");
            return Wrap(el);
        }
        return MakeFakeElement(tag ?? "div");
    }

    public object createTextNode(string text)
    {
        if (_doc != null)
        {
            var tn = _doc.CreateTextNode(text ?? "");
            return Wrap(tn);
        }
        return MakeFakeTextNode(text ?? "");
    }

    public object createComment(string data) => MakeFakeComment(data ?? "");
    public object createDocumentFragment() => MakeFakeElement("");
    public object createEvent(string type) => DomEventHost.Create(type ?? "Event", _doc);
    public object createRange() => MakeFakeRange();
    public object createNodeIterator(object root, int whatToShow) => MakeFakeNodeIterator(root);
    public object createTreeWalker(object root, int whatToShow) => MakeFakeTreeWalker(root);

    public object? getElementById(string id)
    {
        var el = _doc?.GetElementById(id);
        if (el != null) return Wrap(el);
        return null;
    }

    public object getElementsByTagName(string tag)
    {
        var elements = _doc?.GetElementsByTagName(tag);
        if (elements == null) return new DomCollectionHost(Array.Empty<object>());
        return new DomCollectionHost(elements.Select(e => Wrap(e)));
    }

    public object getElementsByClassName(string cls)
    {
        var elements = _doc?.GetElementsByClassName(cls);
        if (elements == null) return new DomCollectionHost(Array.Empty<object>());
        return new DomCollectionHost(elements.Select(e => Wrap(e)));
    }

    public object getElementsByName(string name)
    {
        var elements = _doc?.GetElementsByName(name);
        if (elements == null) return new DomCollectionHost(Array.Empty<object>());
        return new DomCollectionHost(elements.Select(e => Wrap(e)));
    }

    public object? querySelector(string sel)
    {
        var el = _doc?.QuerySelector(sel);
        if (el != null) return Wrap(el);
        return null;
    }

    public object querySelectorAll(string sel)
    {
        var elements = _doc?.QuerySelectorAll(sel);
        if (elements == null) return new DomCollectionHost(Array.Empty<object>());
        return new DomCollectionHost(elements.Select(e => Wrap(e)));
    }

    public object? evaluate(string expr) => null;

    public object adoptNode(object node) => node;
    public object importNode(object node, bool deep) => node;
    public void normalizeDocument() { }

    private object Wrap(INode node)
    {
        if (node is IElement el)
            return WrapElement(el);
        if (node is IText text)
            return WrapText(text);
        return MakeFakeElement("div");
    }

    internal object WrapElement(IElement el)
    {
        return new DomElementHost(el, _doc!, _onChanged);
    }

    private object WrapText(IText text)
    {
        return new DomTextHost(text, _doc!, _onChanged);
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

    private static object WrapAttributes(IElement el)
    {
        return new DomAttributesHost(el);
    }

    private static object MakeFakeClassList(IElement el)
    {
        return new DomClassListHost(el);
    }

    internal static object MakeFakeElement(string tag)
    {
        var t = (tag ?? "div").ToUpperInvariant();
        var lt = (tag ?? "div").ToLowerInvariant();
        IDictionary<string, object> el = new ExpandoObject();

        el["nodeType"] = 1;
        el["nodeName"] = t;
        el["tagName"] = t;
        el["localName"] = lt;
        el["id"] = "";
        el["className"] = "";
        el["classList"] = MakeFakeClassList();
        el["innerHTML"] = "";
        el["outerHTML"] = "";
        el["textContent"] = "";
        el["innerText"] = "";
        el["style"] = new ExpandoObject();
        el["attributes"] = new ExpandoObject();
        el["dataset"] = new ExpandoObject();
        el["childNodes"] = new List<object>();
        el["children"] = new List<object>();
        el["parentNode"] = null;
        el["parentElement"] = null;
        el["firstChild"] = null;
        el["lastChild"] = null;
        el["nextSibling"] = null;
        el["previousSibling"] = null;
        el["offsetWidth"] = 0;
        el["offsetHeight"] = 0;
        el["clientWidth"] = 0;
        el["clientHeight"] = 0;
        el["scrollWidth"] = 0;
        el["scrollHeight"] = 0;
        el["scrollTop"] = 0;
        el["scrollLeft"] = 0;
        el["value"] = "";
        el["checked"] = false;
        el["disabled"] = false;
        el["src"] = "";
        el["href"] = "";
        el["alt"] = "";
        el["title"] = "";
        el["lang"] = "";
        el["dir"] = "";
        el["hidden"] = false;
        el["tabIndex"] = -1;
        el["isContentEditable"] = false;
        el["contentEditable"] = "inherit";
        el["namespaceURI"] = "http://www.w3.org/1999/xhtml";
        el["prefix"] = null;

        el["appendChild"] = new Func<object, object>(child =>
        {
            var nodes = (IList)el["childNodes"];
            var children = (IList)el["children"];
            nodes.Add(child);
            children.Add(child);
            if (nodes.Count == 1) el["firstChild"] = child;
            el["lastChild"] = child;
            return child;
        });

        el["removeChild"] = new Func<object, object>(child =>
        {
            ((IList)el["childNodes"]).Remove(child);
            ((IList)el["children"]).Remove(child);
            return child;
        });

        el["insertBefore"] = new Func<object, object, object>((child, refNode) =>
            ((Func<object, object>)el["appendChild"])(child));

        el["replaceChild"] = new Func<object, object, object>((child, oldChild) =>
        {
            var nodes = (IList)el["childNodes"];
            var i = nodes.IndexOf(oldChild);
            if (i >= 0) nodes[i] = child;
            var children = (IList)el["children"];
            var ci = children.IndexOf(oldChild);
            if (ci >= 0) children[ci] = child;
            return oldChild;
        });

        el["cloneNode"] = new Func<bool, object>(deep => MakeFakeElement(lt));
        el["hasChildNodes"] = new Func<bool>(() => ((IList)el["childNodes"]).Count > 0);
        el["setAttribute"] = new Action<string, string>((n, v) => { if (n == "id") el["id"] = v; if (n == "class") el["className"] = v; });
        el["getAttribute"] = new Func<string, string?>(n => null);
        el["removeAttribute"] = new Action<string>(n => { });
        el["hasAttribute"] = new Func<string, bool>(n => false);
        el["hasAttributes"] = new Func<bool>(() => false);
        el["focus"] = new Action(() => { });
        el["blur"] = new Action(() => { });
        el["click"] = new Action(() => { });

        el["addEventListener"] = new Action<string, object>((type, handler) => { });
        el["removeEventListener"] = new Action<string, object>((type, handler) => { });
        el["dispatchEvent"] = new Func<object, bool>(e => true);
        el["getBoundingClientRect"] = new Func<object>(() => new { top = 0f, right = 0f, bottom = 0f, left = 0f, width = 0f, height = 0f, x = 0f, y = 0f });
        el["getClientRects"] = new Func<object[]>(() => new object[0]);
        el["scrollIntoView"] = new Action(() => { });
        el["scrollIntoViewIfNeeded"] = new Action(() => { });
        el["matches"] = new Func<string, bool>(sel => false);
        el["querySelector"] = new Func<string, object?>(sel => null);
        el["querySelectorAll"] = new Func<string, object[]>(sel => Array.Empty<object>());
        el["closest"] = new Func<string, object?>(sel => null);
        el["contains"] = new Func<object, bool>(other => false);
        el["remove"] = new Action(() => { });
        el["before"] = new Action<object>(n => { });
        el["after"] = new Action<object>(n => { });
        el["replaceWith"] = new Action<object>(n => { });
        el["prepend"] = new Action<object>(n => ((Func<object, object>)el["appendChild"])(n));
        el["append"] = new Action<object>(n => ((Func<object, object>)el["appendChild"])(n));
        el["animate"] = new Func<object>(() => new { play = new Action(() => { }), pause = new Action(() => { }), finish = new Action(() => { }), cancel = new Action(() => { }), finished = Task.CompletedTask, playState = "idle" });
        el["insertAdjacentHTML"] = new Action<string, string>((p, h) => { });
        el["insertAdjacentElement"] = new Action<string, object>((p, e) => { });
        el["insertAdjacentText"] = new Action<string, string>((p, t) => { });

        return el;
    }

    private static object MakeFakeClassList()
    {
        var cls = new List<string>();
        IDictionary<string, object> cl = new ExpandoObject();
        cl["length"] = 0;
        cl["add"] = new Action<string>(c => { if (!cls.Contains(c)) { cls.Add(c); cl["length"] = cls.Count; } });
        cl["remove"] = new Action<string>(c => { cls.Remove(c); cl["length"] = cls.Count; });
        cl["contains"] = new Func<string, bool>(c => cls.Contains(c));
        cl["toggle"] = new Func<string, bool>(c => { if (cls.Contains(c)) { cls.Remove(c); return false; } else { cls.Add(c); return true; } });
        cl["replace"] = new Action<string, string>((o, n) => { var i = cls.IndexOf(o); if (i >= 0) cls[i] = n; });
        cl["item"] = new Func<int, string?>(i => i >= 0 && i < cls.Count ? cls[i] : null);
        return cl;
    }

    private static object MakeFakeTextNode(string text)
    {
        IDictionary<string, object> tn = new ExpandoObject();
        tn["nodeType"] = 3;
        tn["nodeName"] = "#text";
        tn["textContent"] = text ?? "";
        tn["data"] = text ?? "";
        tn["nodeValue"] = text ?? "";
        tn["length"] = (text ?? "").Length;
        tn["parentNode"] = null;
        tn["parentElement"] = null;
        tn["remove"] = new Action(() => { });
        tn["splitText"] = new Func<int, object>(pos => MakeFakeTextNode(""));
        return tn;
    }

    private static object MakeFakeComment(string data)
    {
        IDictionary<string, object> c = new ExpandoObject();
        c["nodeType"] = 8;
        c["nodeName"] = "#comment";
        c["textContent"] = data ?? "";
        c["data"] = data ?? "";
        c["nodeValue"] = data ?? "";
        c["length"] = (data ?? "").Length;
        c["remove"] = new Action(() => { });
        return c;
    }

    private static object MakeFakeRange()
    {
        IDictionary<string, object> r = new ExpandoObject();
        r["setStart"] = new Action<object, int>((_, _) => { });
        r["setEnd"] = new Action<object, int>((_, _) => { });
        r["setStartBefore"] = new Action<object>(_ => { });
        r["setStartAfter"] = new Action<object>(_ => { });
        r["setEndBefore"] = new Action<object>(_ => { });
        r["setEndAfter"] = new Action<object>(_ => { });
        r["collapse"] = new Action<bool>(_ => { });
        r["selectNode"] = new Action<object>(_ => { });
        r["selectNodeContents"] = new Action<object>(_ => { });
        r["cloneContents"] = new Func<object>(() => new ExpandoObject());
        r["deleteContents"] = new Action(() => { });
        r["extractContents"] = new Func<object>(() => new ExpandoObject());
        r["cloneRange"] = new Func<object>(() => r);
        r["detach"] = new Action(() => { });
        r["toString"] = new Func<string>(() => "");
        r["compareBoundaryPoints"] = new Func<int, object, int>((how, src) => 0);
        r["intersectsNode"] = new Func<object, bool>(n => true);
        return r;
    }

    private static object MakeFakeNodeIterator(object root)
    {
        IDictionary<string, object> ni = new ExpandoObject();
        ni["root"] = root;
        ni["nextNode"] = new Func<object?>(() => null);
        ni["previousNode"] = new Func<object?>(() => null);
        ni["detach"] = new Action(() => { });
        ni["filter"] = (object?)null;
        return ni;
    }

    private static object MakeFakeTreeWalker(object root)
    {
        IDictionary<string, object> tw = new ExpandoObject();
        tw["root"] = root;
        tw["currentNode"] = root;
        tw["firstChild"] = new Func<object?>(() => null);
        tw["lastChild"] = new Func<object?>(() => null);
        tw["nextNode"] = new Func<object?>(() => null);
        tw["previousNode"] = new Func<object?>(() => null);
        tw["nextSibling"] = new Func<object?>(() => null);
        tw["previousSibling"] = new Func<object?>(() => null);
        tw["parentNode"] = new Func<object?>(() => null);
        return tw;
    }

    public string GetAndClear()
    {
        var r = _docWrite;
        _docWrite = "";
        return r;
    }
}
