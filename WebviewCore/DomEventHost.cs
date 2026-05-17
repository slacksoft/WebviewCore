using AngleSharp.Dom;
using System.Collections;
using System.Dynamic;

namespace WebviewCore;

public class DomEventHost : DynamicObject
{
    private readonly Dictionary<string, object?> _props = new(StringComparer.OrdinalIgnoreCase);

    public DomEventHost(string type, object? target = null)
    {
        _props["type"] = type;
        _props["target"] = target;
        _props["currentTarget"] = target;
        _props["srcElement"] = target;
        _props["eventPhase"] = 0;
        _props["bubbles"] = false;
        _props["cancelable"] = false;
        _props["composed"] = false;
        _props["defaultPrevented"] = false;
        _props["timeStamp"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        _props["isTrusted"] = false;
        _props["returnValue"] = true;
        _props["cancelBubble"] = false;
    }

    public static object Create(string type, object? target = null) => new DomEventHost(type, target);

    public static string GetTypeName(object? evt)
    {
        if (evt is DomEventHost deh) return deh.GetString("type");
        if (evt is IDictionary dict && dict.Contains("type")) return dict["type"]?.ToString() ?? "";
        if (TryGetScriptProperty(evt, "type", out var dynType)) return dynType?.ToString() ?? "";
        return evt?.GetType().GetProperty("type")?.GetValue(evt)?.ToString()
            ?? evt?.GetType().GetProperty("Type")?.GetValue(evt)?.ToString()
            ?? "";
    }

    public static bool DefaultPrevented(object? evt)
    {
        if (evt is DomEventHost deh) return deh.GetBool("defaultPrevented");
        if (evt is IDictionary dict && dict.Contains("defaultPrevented")) return ToBool(dict["defaultPrevented"]);
        if (TryGetScriptProperty(evt, "defaultPrevented", out var defaultPrevented)) return ToBool(defaultPrevented);
        var prop = evt?.GetType().GetProperty("defaultPrevented") ?? evt?.GetType().GetProperty("DefaultPrevented");
        return prop != null && ToBool(prop.GetValue(evt));
    }

    public static void Normalize(object? evt, object? target)
    {
        if (evt is DomEventHost deh)
        {
            deh.SetIfMissing("target", target);
            deh.SetIfMissing("currentTarget", target);
            deh.SetIfMissing("srcElement", target);
        }
        else if (evt is IDictionary dict)
        {
            if (!dict.Contains("target")) dict["target"] = target;
            if (!dict.Contains("currentTarget")) dict["currentTarget"] = target;
            if (!dict.Contains("srcElement")) dict["srcElement"] = target;
        }
        else if (evt != null)
        {
            TrySetScriptPropertyIfMissing(evt, "target", target);
            TrySetScriptPropertyIfMissing(evt, "currentTarget", target);
            TrySetScriptPropertyIfMissing(evt, "srcElement", target);
        }
    }

    public override bool TryGetMember(GetMemberBinder binder, out object? result)
    {
        switch (binder.Name.ToLowerInvariant())
        {
            case "preventdefault":
                result = new Action(preventDefault);
                return true;
            case "stoppropagation":
            case "stopimmediatepropagation":
                result = new Action(stopPropagation);
                return true;
            case "initevent":
                result = new Action<string, bool, bool>(initEvent);
                return true;
        }

        if (_props.TryGetValue(binder.Name, out var value))
        {
            result = value;
            return true;
        }

        result = null;
        return true;
    }

    public override bool TrySetMember(SetMemberBinder binder, object? value)
    {
        _props[binder.Name] = value;
        return true;
    }

    public void preventDefault()
    {
        _props["defaultPrevented"] = true;
        _props["returnValue"] = false;
    }

    public void stopPropagation()
    {
        _props["cancelBubble"] = true;
    }

    public void initEvent(string type, bool bubbles, bool cancelable)
    {
        _props["type"] = type;
        _props["bubbles"] = bubbles;
        _props["cancelable"] = cancelable;
    }

    public object? this[string key]
    {
        get => _props.TryGetValue(key, out var value) ? value : null;
        set => _props[key] = value;
    }

    private string GetString(string key) => _props.TryGetValue(key, out var value) ? value?.ToString() ?? "" : "";
    private bool GetBool(string key) => _props.TryGetValue(key, out var value) && ToBool(value);

    private void SetIfMissing(string key, object? value)
    {
        if (!_props.ContainsKey(key) || _props[key] == null)
            _props[key] = value;
    }

    private static bool ToBool(object? value)
    {
        if (value is bool b) return b;
        if (value == null) return false;
        var s = value.ToString();
        return !string.IsNullOrEmpty(s) && s != "false" && s != "0";
    }

    private static bool TryGetScriptProperty(object? target, string name, out object? value)
    {
        value = null;
        if (target == null) return false;

        try
        {
            dynamic dyn = target;
            value = name switch
            {
                "type" => dyn.type,
                "defaultPrevented" => dyn.defaultPrevented,
                "target" => dyn.target,
                "currentTarget" => dyn.currentTarget,
                "srcElement" => dyn.srcElement,
                _ => null,
            };
            return true;
        }
        catch
        {
        }

        try
        {
            var method = target.GetType().GetMethod("GetProperty", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic, null, new[] { typeof(string), typeof(object[]) }, null);
            if (method != null)
            {
                value = method.Invoke(target, new object[] { name, Array.Empty<object>() });
                return true;
            }
        }
        catch
        {
        }

        return false;
    }

    private static void TrySetScriptPropertyIfMissing(object target, string name, object? value)
    {
        if (TryGetScriptProperty(target, name, out var existing) && existing != null) return;

        try
        {
            dynamic dyn = target;
            switch (name)
            {
                case "target": dyn.target = value; break;
                case "currentTarget": dyn.currentTarget = value; break;
                case "srcElement": dyn.srcElement = value; break;
            }
            return;
        }
        catch
        {
        }

        try
        {
            var method = target.GetType().GetMethod("SetProperty", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic, null, new[] { typeof(string), typeof(object[]) }, null);
            method?.Invoke(target, new object[] { name, new object?[] { value } });
        }
        catch
        {
        }
    }
}
