using ExCSS;

namespace WebviewCore;

static class CssParser
{
    private static readonly StylesheetParser Parser = new();

    public static List<(string selector, Dictionary<string, string> decls)> Parse(string cssText)
    {
        var result = new List<(string, Dictionary<string, string>)>();
        if (string.IsNullOrWhiteSpace(cssText)) return result;

        try
        {
            var sheet = Parser.Parse(cssText);
            foreach (var rule in sheet.StyleRules)
            {
                var sel = rule.SelectorText;
                if (string.IsNullOrWhiteSpace(sel)) continue;

                var decls = new Dictionary<string, string>();
                var style = rule.Style;
                if (style == null) continue;

                foreach (var decl in style)
                {
                    if (decl == null) continue;
                    var prop = decl.Name?.ToLowerInvariant();
                    var val = decl.Value?.Trim();
                    if (!string.IsNullOrEmpty(prop) && !string.IsNullOrEmpty(val))
                        decls[prop] = val;
                }

                if (decls.Count > 0)
                    result.Add((sel, decls));
            }
        }
        catch { }

        return result;
    }
}
