using AngleSharp.Dom;
using System.Dynamic;

namespace WebviewCore;

public class ComputedStyleHost : DynamicObject
{
    private readonly BoxStyle _style;

    public ComputedStyleHost(object element)
    {
        _style = ResolveStyle(element);
    }

    public string cssText => StyleComputer.SerializeStyle(_style);
    public int length => 0;

    public string getPropertyValue(string propertyName)
    {
        return StyleComputer.GetComputedPropertyValue(_style, propertyName);
    }

    public override bool TryGetMember(GetMemberBinder binder, out object? result)
    {
        if (string.Equals(binder.Name, "getPropertyValue", StringComparison.OrdinalIgnoreCase))
        {
            result = new Func<string, string>(getPropertyValue);
            return true;
        }

        if (string.Equals(binder.Name, "cssText", StringComparison.OrdinalIgnoreCase))
        {
            result = cssText;
            return true;
        }

        if (string.Equals(binder.Name, "length", StringComparison.OrdinalIgnoreCase))
        {
            result = length;
            return true;
        }

        result = getPropertyValue(binder.Name);
        return true;
    }

    private static BoxStyle ResolveStyle(object element)
    {
        IElement? el = element switch
        {
            DomElementHost host => host.Element,
            IElement direct => direct,
            _ => null,
        };

        return el != null ? StyleComputer.ComputeElementStyle(el) : new BoxStyle();
    }
}
