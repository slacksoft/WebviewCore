namespace WebviewCore;

public class BrowserInfo
{
    // ===== Navigator =====
    public string UserAgent { get; set; } = "WebViewCore/1.0";
    public string AppName { get; set; } = "WebViewCore";
    public string AppVersion { get; set; } = "1.0";
    public string Platform { get; set; } = "Win32";
    public string Language { get; set; } = "zh-CN";
    public string[] Languages { get; set; } = new[] { "zh-CN", "en" };
    public bool CookieEnabled { get; set; }
    public string Product { get; set; } = "WebViewCore";
    public string ProductSub { get; set; } = "1.0";
    public string Vendor { get; set; } = "";
    public string VendorSub { get; set; } = "";
    public bool OnLine { get; set; } = true;
    public int HardwareConcurrency { get; set; } = Environment.ProcessorCount;
    public int MaxTouchPoints { get; set; }
    public bool Webdriver { get; set; }
    public int DeviceMemory { get; set; } = 8;
    public string Oscpu { get; set; } = "Windows NT 10.0";
    public string BuildID { get; set; } = "20240101";
    public string DoNotTrack { get; set; } = "unspecified";

    // ===== Screen =====
    public int ScreenWidth { get; set; } = 1024;
    public int ScreenHeight { get; set; } = 768;
    public int AvailWidth { get; set; } = 1024;
    public int AvailHeight { get; set; } = 768;
    public int AvailLeft { get; set; }
    public int AvailTop { get; set; }
    public int ColorDepth { get; set; } = 24;
    public int PixelDepth { get; set; } = 24;

    // ===== Window (mutable from JS/Resize) =====
    public int InnerWidth { get; set; } = 1024;
    public int InnerHeight { get; set; } = 768;
    public int OuterWidth { get; set; } = 1024;
    public int OuterHeight { get; set; } = 768;
    public int ScreenX { get; set; }
    public int ScreenY { get; set; }
    public double DevicePixelRatio { get; set; } = 1.0;

    // ===== Location (mutable from JS) =====
    public string Href { get; set; } = "";
    public string Protocol { get; set; } = "http:";
    public string Host { get; set; } = "";
    public string Hostname { get; set; } = "";
    public string Port { get; set; } = "";
    public string Pathname { get; set; } = "/";
    public string Search { get; set; } = "";
    public string Hash { get; set; } = "";
    public string Origin { get; set; } = "";
}
