namespace WebviewCore;

static class ScriptInterop
{
    public static bool Invoke(object target, params object?[] args)
    {
        try
        {
            if (target is Delegate d)
            {
                d.DynamicInvoke(args);
                return true;
            }
        }
        catch
        {
            return false;
        }

        try
        {
            dynamic dyn = target;
            dyn.Invoke(false, args);
            return true;
        }
        catch
        {
        }

        try
        {
            var method = target.GetType().GetMethod("Invoke", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            if (method != null)
            {
                method.Invoke(target, new object[] { false, args });
                return true;
            }
        }
        catch
        {
        }

        return false;
    }
}
