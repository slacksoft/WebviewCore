using AngleSharp.Dom;
using Microsoft.ClearScript;
using Microsoft.ClearScript.V8;

namespace WebviewCore;

public class JsEngine : IDisposable
{
    private V8ScriptEngine? _engine;
    private DocumentHost? _docHost;
    private LocationHost? _locHost;
    public BrowserInfo Info { get; } = new();
    public List<string> ConsoleLog { get; } = new();
    public event Action<string>? MessageLogged;
    public event Action? DomChanged;
    public event Action<string>? OpenRequested;
    public event Action? CloseRequested;
    public event Action? PrintRequested;

    public void Initialize(IDocument doc)
    {
        try
        {
            _engine = new V8ScriptEngine(V8ScriptEngineFlags.EnableTaskPromiseConversion);
            _docHost = new DocumentHost();
            _docHost.SetDocument(doc, this);

            InjectDialogs();
            InjectConsole();
            InjectTimers();
            InjectNavigator();
            InjectLocation();
            InjectHistory();
            InjectScreen();
            InjectWindowProps();
            InjectDocument();
            InjectFetch();
            InjectPerformance();
            InjectCrypto();

            ExecuteBootstrap();

            Log("JS engine initialized successfully");
        }
        catch (Exception ex)
        {
            Log($"ENGINE INIT FAIL: {ex.GetType().Name}: {ex.Message}");
            Log(ex.StackTrace ?? "");
            throw;
        }
    }

    public void NotifyDomChanged()
    {
        DomChanged?.Invoke();
    }

    private void ExecuteBootstrap()
    {
        var parts = new[]
        {
            GetGlobalRefsBoot(),
            GetEventConstructorsBoot(),
            GetXHRFetchBoot(),
            GetBlobFileBoot(),
            GetStorageBoot(),
            GetObserversBoot(),
            GetCanvasStyleBoot(),
            GetMiscBoot(),
        };

        foreach (var part in parts)
        {
            try
            {
                _engine!.Execute(part);
            }
            catch (Exception ex)
            {
                Log($"BOOTSTRAP FAIL in part: {ex.Message}");
                Log($"Part content (first 200 chars): {part.Substring(0, Math.Min(200, part.Length))}");
                // Continue with next parts even if one fails
            }
        }
    }

    // ─── Bootstrap parts ─────────────────────────────────────────────

    private static string GetGlobalRefsBoot() => @"
var __g = this;
Object.defineProperty(__g,'window',{get:function(){return __g;},configurable:true,enumerable:true});
Object.defineProperty(__g,'self',{get:function(){return __g;},configurable:true,enumerable:true});
Object.defineProperty(__g,'top',{get:function(){return __g;},configurable:true,enumerable:true});
Object.defineProperty(__g,'parent',{get:function(){return __g;},configurable:true,enumerable:true});
Object.defineProperty(__g,'globalThis',{get:function(){return __g;},configurable:true,enumerable:true});
__g.frames = __g;
__g.length = 0;
__g.frameElement = null;
__g.clientInformation = __g.navigator;
__g.event = null;
__g.scheduler = {postTask:function(fn,o){return Promise.resolve()}};
__g.chrome = {loadTimes:function(){return{}},csi:function(){return{}}};
__g.external = {};
__g.clipboardData = {};
";

    private static string GetEventConstructorsBoot() => @"
function Event(type, opts) {
  this.type = type || '';
  this.target = null;
  this.currentTarget = null;
  this.eventPhase = 0;
  this.bubbles = !!(opts && opts.bubbles);
  this.cancelable = !!(opts && opts.cancelable);
  this.composed = !!(opts && opts.composed);
  this.defaultPrevented = false;
  this.timeStamp = Date.now();
  this.isTrusted = false;
  this.srcElement = null;
  this.returnValue = true;
  this.cancelBubble = false;
}
Event.prototype.stopPropagation = function() { this.cancelBubble = true; };
Event.prototype.stopImmediatePropagation = function() { this.cancelBubble = true; };
Event.prototype.preventDefault = function() { this.defaultPrevented = true; this.returnValue = false; };
Event.prototype.initEvent = function(t,b,c) { this.type=t; this.bubbles=b; this.cancelable=c; };

function CustomEvent(type, opts) { Event.call(this, type, opts); this.detail = (opts && opts.detail) || null; }
CustomEvent.prototype = Object.create(Event.prototype);

function MouseEvent(type, opts) {
  Event.call(this, type, opts);
  this.screenX = (opts && opts.screenX) || 0;
  this.clientX = (opts && opts.clientX) || 0;
  this.clientY = (opts && opts.clientY) || 0;
  this.button = (opts && opts.button) || 0;
  this.ctrlKey = !!(opts && opts.ctrlKey);
  this.shiftKey = !!(opts && opts.shiftKey);
  this.altKey = !!(opts && opts.altKey);
  this.metaKey = !!(opts && opts.metaKey);
}
MouseEvent.prototype = Object.create(Event.prototype);

function KeyboardEvent(type, opts) {
  Event.call(this, type, opts);
  this.key = (opts && opts.key) || '';
  this.keyCode = (opts && opts.keyCode) || 0;
  this.ctrlKey = !!(opts && opts.ctrlKey);
  this.shiftKey = !!(opts && opts.shiftKey);
  this.altKey = !!(opts && opts.altKey);
  this.metaKey = !!(opts && opts.metaKey);
}
KeyboardEvent.prototype = Object.create(Event.prototype);

function FocusEvent(type, opts) { Event.call(this, type, opts); this.relatedTarget = null; }
FocusEvent.prototype = Object.create(Event.prototype);

function UIEvent(type, opts) { Event.call(this, type, opts); this.detail = (opts && opts.detail) || 0; this.view = __g; }
UIEvent.prototype = Object.create(Event.prototype);

function WheelEvent(type, opts) {
  MouseEvent.call(this, type, opts);
  this.deltaX = (opts && opts.deltaX) || 0;
  this.deltaY = (opts && opts.deltaY) || 0;
  this.deltaZ = 0;
  this.deltaMode = 0;
}
WheelEvent.prototype = Object.create(MouseEvent.prototype);

function PointerEvent(type, opts) {
  MouseEvent.call(this, type, opts);
  this.pointerId = (opts && opts.pointerId) || 1;
  this.pointerType = (opts && opts.pointerType) || 'mouse';
  this.width = 1; this.height = 1; this.pressure = 0.5; this.isPrimary = true;
}
PointerEvent.prototype = Object.create(MouseEvent.prototype);

function TouchEvent(type, opts) { Event.call(this, type, opts); this.touches = []; this.targetTouches = []; this.changedTouches = []; }
TouchEvent.prototype = Object.create(Event.prototype);

function DragEvent(type, opts) { MouseEvent.call(this, type, opts); this.dataTransfer = {dropEffect:'none',effectAllowed:'none',files:[],items:[],types:[]}; }
DragEvent.prototype = Object.create(MouseEvent.prototype);

function ProgressEvent(type, opts) { Event.call(this, type, opts); this.lengthComputable = !!(opts && opts.lengthComputable); this.loaded = (opts && opts.loaded) || 0; this.total = (opts && opts.total) || 0; }
ProgressEvent.prototype = Object.create(Event.prototype);
";

    private static string GetXHRFetchBoot() => @"
function XMLHttpRequest() {
  this.readyState = 0; this.status = 0; this.statusText = '';
  this.responseText = ''; this.response = ''; this.responseType = '';
  this.responseXML = null; this.timeout = 0; this.withCredentials = false;
  this.onload = null; this.onerror = null; this.onreadystatechange = null;
  var _t = this;
  this.abort = function() { _t.readyState = 0; };
  this.getAllResponseHeaders = function() { return ''; };
  this.getResponseHeader = function(n) { return null; };
  this.setRequestHeader = function(n,v) {};
  this.open = function(m,u,a) { _t._method = m; _t._url = u; _t.readyState = 1; };
  this.send = function(d) {
    _t.readyState = 4; _t.status = 200; _t.statusText = 'OK';
    _t.responseText = ''; _t.response = '';
    setTimeout(function() {
      if (_t.onreadystatechange) _t.onreadystatechange();
      if (_t.onload) _t.onload();
    }, 0);
  };
}
XMLHttpRequest.UNSENT = 0; XMLHttpRequest.OPENED = 1; XMLHttpRequest.DONE = 4;

function Headers() { this.get=function(){return null}; this.has=function(){return false}; this.forEach=function(){}; }
function Request(u,o) { this.url=u||''; this.method=(o&&o.method)||'GET'; this.headers=new Headers(); }
function Response(b,i) {
  this.ok = (i&&i.status>=200&&i.status<300);
  this.status = (i&&i.status)||200;
  this.statusText = (i&&i.statusText)||'OK';
  this.headers = new Headers();
  this.url = '';
  this.text = function() { return Promise.resolve(''); };
  this.json = function() { return Promise.resolve({}); };
  this.blob = function() { return Promise.resolve(new Blob()); };
  this.arrayBuffer = function() { return Promise.resolve(new ArrayBuffer(0)); };
  this.clone = function() { return this; };
}
function FormData() { this.append = function(){}; this.delete = function(){}; this.get = function(){return null}; this.has = function(){return false}; this.set = function(){}; }
";

    private static string GetBlobFileBoot() => @"
function Blob(p, o) { this.size = (p&&p.length)||0; this.type = (o&&o.type)||''; this.slice = function() { return new Blob(); }; }
function File(p, n, o) { Blob.call(this, p, o); this.name = n||''; this.lastModified = (o&&o.lastModified)||Date.now(); }
File.prototype = Object.create(Blob.prototype);

function FileReader() {
  this.readyState = 0; this.result = null; this.error = null;
  this.onload = null; this.onerror = null;
  var _t = this;
  this.readAsArrayBuffer = function(b) { _t.readyState = 2; _t.result = new ArrayBuffer(0); setTimeout(function(){_t.readyState = 3; if(_t.onload)_t.onload();},0); };
  this.readAsText = function(b,e) { _t.readyState = 2; _t.result = ''; setTimeout(function(){_t.readyState = 3; if(_t.onload)_t.onload();},0); };
  this.readAsDataURL = function(b) { _t.readyState = 2; _t.result = 'data:;base64,'; setTimeout(function(){_t.readyState = 3; if(_t.onload)_t.onload();},0); };
  this.abort = function() { _t.readyState = 1; };
}
FileReader.EMPTY = 0; FileReader.LOADING = 1; FileReader.DONE = 2;
";

    private static string GetStorageBoot() => @"
(function() {
  var _st = {}, _ss = {};
  __g.localStorage = {
    getItem: function(k) { return _st[k] !== undefined ? String(_st[k]) : null; },
    setItem: function(k,v) { _st[k] = String(v); },
    removeItem: function(k) { delete _st[k]; },
    clear: function() { _st = {}; },
    get length() { return Object.keys(_st).length; },
    key: function(i) { return Object.keys(_st)[i] || null; }
  };
  __g.sessionStorage = {
    getItem: function(k) { return _ss[k] !== undefined ? String(_ss[k]) : null; },
    setItem: function(k,v) { _ss[k] = String(v); },
    removeItem: function(k) { delete _ss[k]; },
    clear: function() { _ss = {}; },
    get length() { return Object.keys(_ss).length; },
    key: function(i) { return Object.keys(_ss)[i] || null; }
  };
})();
";

    private static string GetObserversBoot() => @"
function MutationObserver(cb) { this.observe = function(t,c){}; this.disconnect = function(){}; this.takeRecords = function() { return []; }; }
function ResizeObserver(cb) { this.observe = function(t){}; this.disconnect = function(){}; this.unobserve = function(t){}; }
function IntersectionObserver(cb,o) { this.root = null; this.observe = function(t){}; this.disconnect = function(){}; this.unobserve = function(t){}; this.takeRecords = function() { return []; }; }

function AbortController() {
  this.signal = {
    aborted: false,
    onabort: null,
    reason: undefined,
    throwIfAborted: function() {},
    addEventListener: function(t,h) {
      if (!this._ev) this._ev = {};
      if (!this._ev[t]) this._ev[t] = [];
      this._ev[t].push(h);
    },
    removeEventListener: function(t,h) {
      if (this._ev && this._ev[t]) {
        var i = this._ev[t].indexOf(h);
        if (i >= 0) this._ev[t].splice(i,1);
      }
    }
  };
  var _t = this;
  this.abort = function(r) {
    _t.signal.aborted = true;
    _t.signal.reason = r;
    if (_t.signal.onabort) _t.signal.onabort();
    if (_t.signal._ev && _t.signal._ev['abort']) {
      for (var i = 0; i < _t.signal._ev['abort'].length; i++) {
        try { _t.signal._ev['abort'][i].call(null); } catch(ex) {}
      }
    }
  };
}
";

    private static string GetCanvasStyleBoot() => @"
function HTMLCanvasElement() { this.width = 300; this.height = 150; this.getContext = function(t) { if (t==='2d') return _ctx2d; return null; }; }
var _ctx2d = {
  canvas: null, fillStyle: '#000', strokeStyle: '#000', lineWidth: 1,
  font: '10px sans-serif', textAlign: 'start', textBaseline: 'alphabetic',
  globalAlpha: 1, globalCompositeOperation: 'source-over',
  save: function(){}, restore: function(){}, scale: function(){}, rotate: function(){}, translate: function(){},
  clearRect: function(){}, fillRect: function(){}, strokeRect: function(){}, fillText: function(){}, strokeText: function(){},
  measureText: function(t) { return {width: t.length * 6}; },
  beginPath: function(){}, closePath: function(){}, moveTo: function(){}, lineTo: function(){}, arc: function(){},
  fill: function(){}, stroke: function(){}, clip: function(){}, drawImage: function(){},
  createImageData: function(w,h) { return {width:w||1, height:h||1, data:new Uint8ClampedArray((w||1)*(h||1)*4)}; },
  getImageData: function() { return {width:1, height:1, data:new Uint8ClampedArray(4)}; },
  putImageData: function(){}, setLineDash: function(){}, getLineDash: function() { return []; },
  createLinearGradient: function() { return {addColorStop: function(){}}; },
  createRadialGradient: function() { return {addColorStop: function(){}}; },
  createPattern: function() { return null; }
};
__g.CSS = { escape: function(s) { return s; }, supports: function(p,v) { return true; } };
";

    private static string GetMiscBoot() => @"
__g.Image = function(w,h) {
  this.width = w||0; this.height = h||0; this.src = ''; this.alt = '';
  this.complete = false; this.naturalWidth = 0; this.naturalHeight = 0;
  this.onload = null; this.onerror = null;
  var _t = this;
  setTimeout(function() {
    _t.complete = true; _t.naturalWidth = _t.width; _t.naturalHeight = _t.height;
    if (_t.onload) _t.onload();
  }, 0);
};

__g.Worker = function(u) { this.onmessage = null; this.onerror = null; this.postMessage = function(){}; this.terminate = function(){}; };
__g.DOMException = function(m,n) { this.message = m||''; this.name = n||'Error'; this.code = 0; };

__g.URL = function(u,b) {
  this.href = u||''; this.protocol = 'http:'; this.host = ''; this.hostname = '';
  this.port = ''; this.pathname = '/'; this.search = ''; this.hash = '';
  this.origin = '';
  var m = (u||'').match(/^(https?:)\/\/([^\/]+)(\/.*)?$/);
  if (m) {
    this.protocol = m[1];
    this.hostname = m[2];
    this.host = m[2];
    this.origin = m[1] + '//' + m[2];
    this.pathname = m[3] || '/';
    var q = this.pathname.indexOf('?');
    if (q >= 0) { this.search = this.pathname.substring(q); this.pathname = this.pathname.substring(0, q); }
    var h = (this.pathname + this.search).indexOf('#');
    if (h >= 0) { this.hash = (this.pathname + this.search).substring(h); }
  }
};
__g.URL.createObjectURL = function(b) { return 'blob:' + Date.now(); };
__g.URL.revokeObjectURL = function(u) {};

__g.MessageChannel = function() {
  var c = this;
  this.port1 = { postMessage: function(m) { setTimeout(function() { if (c.port1.onmessage) c.port1.onmessage({data:m}); }, 0); }, onmessage: null, close: function(){}, start: function(){} };
  this.port2 = { postMessage: function(m) { setTimeout(function() { if (c.port2.onmessage) c.port2.onmessage({data:m}); }, 0); }, onmessage: null, close: function(){}, start: function(){} };
};

__g.WebSocket = function(u,p) {
  this.url = u||''; this.readyState = 3; this.protocol = '';
  this.bufferedAmount = 0; this.binaryType = 'blob';
  this.onopen = null; this.onclose = null; this.onerror = null; this.onmessage = null;
  this.close = function() { this.readyState = 3; };
  this.send = function(d) {};
};
__g.WebSocket.CONNECTING = 0; __g.WebSocket.OPEN = 1; __g.WebSocket.CLOSING = 2; __g.WebSocket.CLOSED = 3;

__g.NodeList = function() { this.length = 0; this.item = function(i) { return this[i] || null; }; };
__g.HTMLCollection = function() { this.length = 0; this.item = function(i) { return this[i] || null; }; };
__g.DOMTokenList = function() { this.length = 0; this.contains = function() { return false; }; this.add = function(){}; };
__g.DOMRect = function(x,y,w,h) { this.x = x||0; this.y = y||0; this.width = w||0; this.height = h||0; };

__g.Window = function() { return __g; };
__g.Document = function() { return __g.document; };
__g.Element = function() {};
__g.Node = function() {};
__g.HTMLElement = function() {};
__g.HTMLDocument = function() { return __g.document; };
";

    // ─── Simple global functions ──────────────────────────────────────

    private void InjectDialogs()
    {
        try
        {
            _engine!.AddHostObject("alert", new Action<object?>(m =>
            {
                var msg = m?.ToString() ?? "undefined";
                Log($"[alert] {msg}");
                MessageBox.Show(msg, "WebViewCore", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }));
            _engine.AddHostObject("confirm", new Func<object?, bool>(m =>
            {
                var msg = m?.ToString() ?? "undefined";
                Log($"[confirm] {msg}");
                return MessageBox.Show(msg, "WebViewCore", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
            }));
            _engine.AddHostObject("prompt", new Func<object?, object?, string>((m, d) =>
            {
                var msg = m?.ToString() ?? "Prompt";
                var def = d?.ToString() ?? "";
                using var f = new Form { Text = "WebViewCore", ClientSize = new Size(380, 120), FormBorderStyle = FormBorderStyle.FixedDialog, StartPosition = FormStartPosition.CenterParent, MaximizeBox = false, MinimizeBox = false, ShowInTaskbar = false };
                var l = new Label { Text = msg, Location = new Point(12, 12), AutoSize = true };
                var t = new TextBox { Text = def, Location = new Point(12, l.Bottom + 8), Width = 350 };
                var b = new Button { Text = "OK", DialogResult = DialogResult.OK, Location = new Point(200, t.Bottom + 8), Size = new Size(75, 26) };
                var c = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Location = new Point(281, t.Bottom + 8), Size = new Size(75, 26) };
                f.Controls.Add(l); f.Controls.Add(t); f.Controls.Add(b); f.Controls.Add(c); f.AcceptButton = b; f.CancelButton = c;
                var result = f.ShowDialog() == DialogResult.OK ? t.Text : "";
                Log($"[prompt] result: {result}");
                return result;
            }));
        }
        catch (Exception ex) { Log($"DIALOGS FAIL: {ex.Message}"); }
    }

    private void InjectConsole()
    {
        try
        {
            _engine!.AddHostObject("__consoleWrite", new Action<string, string>((level, line) => Log(line)));
            _engine.AddHostObject("__consoleCount", new Func<string?, string>((k) =>
            {
                var key = string.IsNullOrEmpty(k) ? "default" : k!;
                _counts.TryGetValue(key, out var v);
                _counts[key] = v + 1;
                return key + ": " + (v + 1);
            }));
            _engine.AddHostObject("__consoleTime", new Action<string?>(k => _times[string.IsNullOrEmpty(k) ? "default" : k!] = Environment.TickCount64));
            _engine.AddHostObject("__consoleTimeEnd", new Action<string?>(k =>
            {
                var key = string.IsNullOrEmpty(k) ? "default" : k!;
                if (_times.TryGetValue(key, out var s))
                {
                    Log(key + ": " + (Environment.TickCount64 - s) + "ms");
                    _times.Remove(key);
                }
            }));

            _engine.Execute(@"
(function(g) {
  function seenPush(seen, value) {
    if (!value || (typeof value !== 'object' && typeof value !== 'function')) return false;
    if (seen.indexOf(value) >= 0) return true;
    seen.push(value);
    return false;
  }

  function formatValue(value, seen) {
    if (value === undefined) return 'undefined';
    if (value === null) return 'null';
    var type = typeof value;
    if (type === 'string') return value;
    if (type === 'number' || type === 'boolean' || type === 'bigint' || type === 'symbol') return String(value);
    if (type === 'function') return 'function ' + (value.name || '') + '()';
    if (seenPush(seen, value)) return '[Circular]';

    try {
      if (value instanceof Error) return value.stack || (value.name + ': ' + value.message);
    } catch (_) {}

    try {
      if (Array.isArray(value)) {
        return '[' + value.map(function(item) { return formatValue(item, seen.slice()); }).join(', ') + ']';
      }
    } catch (_) {}

    try {
      var json = JSON.stringify(value);
      if (json !== undefined) return json;
    } catch (_) {}

    try { return String(value); } catch (_) { return '[object]'; }
  }

  function applyFormat(first, rest) {
    var index = 0;
    var formatted = String(first).replace(/%[sdifoOc]/g, function(token) {
      if (index >= rest.length) return token;
      var value = rest[index++];
      if (token === '%c') return '';
      if (token === '%d' || token === '%i') return parseInt(value, 10).toString();
      if (token === '%f') return parseFloat(value).toString();
      return formatValue(value, []);
    });
    var tail = rest.slice(index).map(function(value) { return formatValue(value, []); });
    return tail.length ? formatted + ' ' + tail.join(' ') : formatted;
  }

  function formatArgs(args) {
    var arr = Array.prototype.slice.call(args);
    if (arr.length === 0) return '';
    if (typeof arr[0] === 'string' && /%[sdifoOc]/.test(arr[0])) return applyFormat(arr[0], arr.slice(1));
    return arr.map(function(value) { return formatValue(value, []); }).join(' ');
  }

  var consoleObject = {
    log: function() { __consoleWrite('log', formatArgs(arguments)); },
    info: function() { __consoleWrite('info', formatArgs(arguments)); },
    warn: function() { __consoleWrite('warn', formatArgs(arguments)); },
    error: function() { __consoleWrite('error', formatArgs(arguments)); },
    debug: function() { __consoleWrite('debug', formatArgs(arguments)); },
    table: function() { __consoleWrite('table', formatArgs(arguments)); },
    dir: function() { __consoleWrite('dir', formatArgs(arguments)); },
    clear: function() {},
    count: function(label) { __consoleWrite('count', __consoleCount(label == null ? 'default' : String(label))); },
    time: function(label) { __consoleTime(label == null ? 'default' : String(label)); },
    timeEnd: function(label) { __consoleTimeEnd(label == null ? 'default' : String(label)); },
    trace: function() {
      var stack = '';
      try { stack = (new Error()).stack || ''; } catch (_) {}
      __consoleWrite('trace', stack);
    },
    assert: function(condition) {
      if (condition) return;
      var rest = Array.prototype.slice.call(arguments, 1);
      __consoleWrite('assert', 'Assertion failed' + (rest.length ? ': ' + formatArgs(rest) : ''));
    }
  };

  Object.defineProperty(g, 'console', { value: consoleObject, writable: true, configurable: true, enumerable: false });
})(this);
");
        }
        catch (Exception ex) { Log($"CONSOLE FAIL: {ex.Message}"); }
    }

    private readonly Dictionary<string, long> _times = new();
    private readonly Dictionary<string, int> _counts = new();

    private void InjectTimers()
    {
        try
        {
            _engine!.AddHostObject("setTimeout", new Func<object, int, int>((fn, ms) =>
            {
                if (fn is string s) try { _engine.Execute(s); } catch (Exception ex) { Log($"setTimeout exec fail: {ex.Message}"); }
                else if (!ScriptInterop.Invoke(fn)) Log("setTimeout inv fail");
                return 0;
            }));
            _engine.AddHostObject("setInterval", new Func<object, int, int>((fn, ms) =>
            {
                if (fn is string s) try { _engine.Execute(s); } catch { }
                else if (ms == 0) ScriptInterop.Invoke(fn);
                return 0;
            }));
            _engine.AddHostObject("clearTimeout", new Action<int>(_ => { }));
            _engine.AddHostObject("clearInterval", new Action<int>(_ => { }));
            _engine.AddHostObject("requestAnimationFrame", new Func<object, int>(fn =>
            {
                try
                {
                    ScriptInterop.Invoke(fn, (double)Environment.TickCount64);
                }
                catch { }
                return 0;
            }));
            _engine.AddHostObject("cancelAnimationFrame", new Action<int>(_ => { }));
        }
        catch (Exception ex) { Log($"TIMERS FAIL: {ex.Message}"); }
    }

    // ─── Navigator ────────────────────────────────────────────────────

    private void InjectNavigator()
    {
        try
        {
            _engine!.AddHostObject("navigator", new NavigatorHost(Info));
        }
        catch (Exception ex) { Log($"NAVIGATOR FAIL: {ex.Message}"); }
    }

    // ─── Location ─────────────────────────────────────────────────────

    private void InjectLocation()
    {
        try
        {
            _locHost = new LocationHost(Info);
            _engine!.AddHostObject("location", _locHost);
        }
        catch (Exception ex) { Log($"LOCATION FAIL: {ex.Message}"); }
    }

    private void InjectHistory()
    {
        try
        {
            _engine!.AddHostObject("history", new HistoryHost());
        }
        catch (Exception ex) { Log($"HISTORY FAIL: {ex.Message}"); }
    }

    // ─── Screen ───────────────────────────────────────────────────────

    private void InjectScreen()
    {
        try
        {
            _engine!.AddHostObject("screen", new ScreenHost(Info));
        }
        catch (Exception ex) { Log($"SCREEN FAIL: {ex.Message}"); }
    }

    // ─── Window properties ────────────────────────────────────────────

    private void InjectWindowProps()
    {
        // Use _engine.Script to set primitive global properties (AddHostObject fails for value types)
        try { _engine!.Execute($"innerWidth = {Info.InnerWidth};"); } catch (Exception ex) { Log($"innerWidth FAIL: {ex.Message}"); }
        try { _engine!.Execute($"innerHeight = {Info.InnerHeight};"); } catch (Exception ex) { Log($"innerHeight FAIL: {ex.Message}"); }
        try { _engine!.Execute($"outerWidth = {Info.OuterWidth};"); } catch (Exception ex) { Log($"outerWidth FAIL: {ex.Message}"); }
        try { _engine!.Execute($"outerHeight = {Info.OuterHeight};"); } catch (Exception ex) { Log($"outerHeight FAIL: {ex.Message}"); }
        try { _engine!.Execute($"screenX = {Info.ScreenX}; screenLeft = {Info.ScreenX};"); } catch (Exception ex) { Log($"screenX FAIL: {ex.Message}"); }
        try { _engine!.Execute($"screenY = {Info.ScreenY}; screenTop = {Info.ScreenY};"); } catch (Exception ex) { Log($"screenY FAIL: {ex.Message}"); }
        try { _engine!.Execute("pageXOffset = 0; pageYOffset = 0; scrollX = 0; scrollY = 0;"); } catch (Exception ex) { Log($"scroll FAIL: {ex.Message}"); }
        try { _engine!.Execute($"devicePixelRatio = {Info.DevicePixelRatio};"); } catch (Exception ex) { Log($"dpr FAIL: {ex.Message}"); }
        try { _engine!.Execute("fullScreen = false; closed = false; name = ''; status = ''; origin = ''; isSecureContext = true;"); } catch (Exception ex) { Log($"props FAIL: {ex.Message}"); }

        try { _engine!.AddHostObject("scrollTo", new Action<object, object>((x, y) => { })); } catch (Exception ex) { Log($"scrollTo FAIL: {ex.Message}"); }
        try { _engine!.AddHostObject("scroll", new Action<object, object>((x, y) => { })); } catch (Exception ex) { Log($"scroll FAIL: {ex.Message}"); }
        try { _engine!.AddHostObject("scrollBy", new Action<object, object>((x, y) => { })); } catch (Exception ex) { Log($"scrollBy FAIL: {ex.Message}"); }

        try
        {
            _engine!.AddHostObject("open", new Func<string, string>(url =>
            {
                OpenRequested?.Invoke(url);
                return "opened";
            }));
        }
        catch (Exception ex) { Log($"open FAIL: {ex.Message}"); }

        try { _engine!.AddHostObject("close", new Action(() => { CloseRequested?.Invoke(); })); } catch (Exception ex) { Log($"close FAIL: {ex.Message}"); }
        try { _engine!.AddHostObject("focus", new Action(() => { })); } catch (Exception ex) { Log($"focus FAIL: {ex.Message}"); }
        try { _engine!.AddHostObject("blur", new Action(() => { })); } catch (Exception ex) { Log($"blur FAIL: {ex.Message}"); }
        try { _engine!.AddHostObject("stop", new Action(() => { })); } catch (Exception ex) { Log($"stop FAIL: {ex.Message}"); }
        try { _engine!.AddHostObject("print", new Action(() => { PrintRequested?.Invoke(); })); } catch (Exception ex) { Log($"print FAIL: {ex.Message}"); }
        try { _engine!.AddHostObject("postMessage", new Action<object, string>((msg, target) => { })); } catch (Exception ex) { Log($"postMsg FAIL: {ex.Message}"); }

        try { _engine!.AddHostObject("matchMedia", new Func<string, object>(q => new MatchMediaHost(q))); } catch (Exception ex) { Log($"matchMedia FAIL: {ex.Message}"); }

        try
        {
            _engine!.AddHostObject("__getComputedStyleHost", new Func<object, object>(el => new ComputedStyleHost(el)));
            _engine.Execute("getComputedStyle = function(el, pseudo) { return __getComputedStyleHost(el); };");
        }
        catch (Exception ex) { Log($"computedStyle FAIL: {ex.Message}"); }

        try
        {
            _engine!.AddHostObject("getSelection", new Func<object>(() => new
            {
                toString = new Func<string>(() => ""), rangeCount = 0, type = "None", isCollapsed = true,
                removeAllRanges = new Action(() => { }),
            }));
        }
        catch (Exception ex) { Log($"selection FAIL: {ex.Message}"); }

        try
        {
            _engine!.AddHostObject("visualViewport", new
            {
                width = 1024, height = 768, offsetLeft = 0, offsetTop = 0,
                pageLeft = 0, pageTop = 0, scale = 1.0,
            });
        }
        catch (Exception ex) { Log($"viewport FAIL: {ex.Message}"); }
    }

    // ─── Document ─────────────────────────────────────────────────────

    private void InjectDocument()
    {
        try
        {
            _engine!.AddHostObject("document", _docHost!);
        }
        catch (Exception ex) { Log($"DOCUMENT FAIL: {ex.Message}"); }
    }

    // ─── Fetch ────────────────────────────────────────────────────────

    private void InjectFetch()
    {
        try
        {
            _engine!.AddHostObject("fetch", new Func<string, object>(url => new
            {
                ok = true, status = 200, statusText = "OK",
                headers = new { get = new Func<string, string?>(_ => null) },
                text = new Func<Task<string>>(() => Task.FromResult("")),
                json = new Func<Task<object>>(() => Task.FromResult<object>(new { })),
                blob = new Func<Task<object>>(() => Task.FromResult<object>(new { })),
                arrayBuffer = new Func<Task<object>>(() => Task.FromResult<object>(new byte[0])),
            }));
        }
        catch (Exception ex) { Log($"FETCH FAIL: {ex.Message}"); }
    }

    private void InjectPerformance()
    {
        try
        {
            var start = DateTime.UtcNow;
            _engine!.AddHostObject("performance", new
            {
                now = new Func<double>(() => (DateTime.UtcNow - start).TotalMilliseconds),
                timing = new
                {
                    navigationStart = (long)(start - DateTime.UnixEpoch).TotalMilliseconds,
                    fetchStart = (long)(start - DateTime.UnixEpoch).TotalMilliseconds,
                    domLoading = (long)(start - DateTime.UnixEpoch).TotalMilliseconds,
                    domInteractive = (long)(start - DateTime.UnixEpoch).TotalMilliseconds,
                    domComplete = (long)(start - DateTime.UnixEpoch).TotalMilliseconds,
                    loadEventStart = (long)(start - DateTime.UnixEpoch).TotalMilliseconds,
                    loadEventEnd = (long)(start - DateTime.UnixEpoch).TotalMilliseconds,
                },
                navigation = new { type = 0, redirectCount = 0 },
                getEntries = new Func<object[]>(() => Array.Empty<object>()),
                getEntriesByType = new Func<string, object[]>(_ => Array.Empty<object>()),
                getEntriesByName = new Func<string, object[]>(_ => Array.Empty<object>()),
                mark = new Action<string>(_ => { }),
                measure = new Action<string, string?, string?>((_, __, ___) => { }),
            });
        }
        catch (Exception ex) { Log($"PERFORMANCE FAIL: {ex.Message}"); }
    }

    private void InjectCrypto()
    {
        try
        {
            var rng = new Random();
            _engine!.AddHostObject("crypto", new
            {
                getRandomValues = new Func<object, object>((arr) =>
                {
                    if (arr is Array a)
                        for (int i = 0; i < a.Length; i++)
                            a.SetValue(Convert.ChangeType(rng.Next(256), a.GetValue(0)?.GetType() ?? typeof(int)), i);
                    return arr;
                }),
                randomUUID = new Func<string>(() => Guid.NewGuid().ToString()),
            });
            _engine.AddHostObject("atob", new Func<string, string>(s =>
            {
                try { return System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(s)); }
                catch { return ""; }
            }));
            _engine.AddHostObject("btoa", new Func<string, string>(s => Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(s ?? ""))));
        }
        catch (Exception ex) { Log($"CRYPTO FAIL: {ex.Message}"); }
    }

    // ─── Public API ───────────────────────────────────────────────────

    public void Exec(string code)
    {
        if (_engine == null) return;
        code = code.Trim();
        if (code.Length == 0) return;
        try
        {
            _engine.Execute(code);
        }
        catch (ScriptEngineException ex)
        {
            Log($"JS ERROR: {ex.Message}");
            if (ex.ErrorDetails != null) Log($"Details: {ex.ErrorDetails}");
        }
        catch (Exception ex)
        {
            Log($"EXEC FAIL: {ex.GetType().Name}: {ex.Message}");
        }
    }

    public async Task ExecUrl(string url)
    {
        if (_engine == null) return;
        try
        {
            var c = await HtmlFetcher.FetchAsync(url);
            _engine.Execute(c);
        }
        catch (Exception ex)
        {
            Log($"FETCH URL FAIL: {url} - {ex.Message}");
        }
    }

    public void ExecClick(string code)
    {
        if (_engine == null) return;
        code = code.Trim();
        if (code.Length == 0) return;
        try { _engine.Execute(code); }
        catch (Exception ex) { Log($"CLICK FAIL: {ex.Message}"); }
    }

    public string GetAndClearDocWrite()
    {
        return _docHost?.GetAndClear() ?? "";
    }

    private void Log(object? msg)
    {
        var line = msg?.ToString() ?? "undefined";
        ConsoleLog.Add(line);
        MessageLogged?.Invoke(line);
    }

    public void Dispose()
    {
        try
        {
            _engine?.Dispose();
            _engine = null;
        }
        catch { }
    }
}

// ─── Host classes for ClearScript (must be public with auto-properties, NOT expression-bodied delegates) ───

public class NavigatorHost
{
    public string userAgent { get; set; }
    public string appName { get; set; }
    public string appVersion { get; set; }
    public string platform { get; set; }
    public string language { get; set; }
    public string[] languages { get; set; }
    public bool cookieEnabled { get; set; }
    public string product { get; set; }
    public string vendor { get; set; }
    public bool onLine { get; set; }
    public int hardwareConcurrency { get; set; }
    public int maxTouchPoints { get; set; }
    public bool webdriver { get; set; }
    public int deviceMemory { get; set; }
    public string oscpu { get; set; }
    public string doNotTrack { get; set; }

    public NavigatorHost(BrowserInfo info)
    {
        userAgent = info.UserAgent;
        appName = info.AppName;
        appVersion = info.AppVersion;
        platform = info.Platform;
        language = info.Language;
        languages = info.Languages;
        cookieEnabled = info.CookieEnabled;
        product = info.Product;
        vendor = info.Vendor;
        onLine = info.OnLine;
        hardwareConcurrency = info.HardwareConcurrency;
        maxTouchPoints = info.MaxTouchPoints;
        webdriver = info.Webdriver;
        deviceMemory = info.DeviceMemory;
        oscpu = info.Oscpu;
        doNotTrack = info.DoNotTrack;
    }

    public bool javaEnabled() => false;
    public object[] plugins => Array.Empty<object>();
    public object[] mimeTypes => Array.Empty<object>();
    public object connection => new { effectiveType = "4g", downlink = 10.0, rtt = 50, saveData = false, type = "wifi" };
    public object geolocation => new GeolocationHost();
    public object clipboard => new ClipboardHost();
    public object serviceWorker => new ServiceWorkerHost();
    public object mediaDevices => new { getUserMedia = new Func<string, object>(c => throw new Exception("NotSupportedError")) };
    public object permissions => new { query = new Func<string, object>(n => new { state = "granted" }) };
}

public class GeolocationHost
{
    public void getCurrentPosition(object success, object error, object options)
    {
        try
        {
            if (success is Delegate s)
                s.DynamicInvoke(new { coords = new { latitude = 39.9, longitude = 116.4, accuracy = 100.0, altitude = (double?)null, altitudeAccuracy = (double?)null, heading = (double?)null, speed = (double?)null }, timestamp = (double)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() });
        }
        catch { }
    }
}

public class ClipboardHost
{
    public Task<string> readText() => Task.FromResult("");
    public Task<object[]> read() => Task.FromResult(Array.Empty<object>());
}

public class ServiceWorkerHost
{
    public object? controller => null;
    public object ready => Task.FromResult(new { active = (object?)null });
}

public class HistoryHost
{
    public int length => 1;
    public object? state => null;
    public string scrollRestoration => "auto";

    public void back() { }
    public void forward() { }
    public void go(int delta) { }
    public void pushState(object? state, string title, string? url) { }
    public void replaceState(object? state, string title, string? url) { }
}

public class ScreenHost
{
    public int width { get; set; }
    public int height { get; set; }
    public int availWidth { get; set; }
    public int availHeight { get; set; }
    public int availLeft { get; set; }
    public int availTop { get; set; }
    public int colorDepth { get; set; }
    public int pixelDepth { get; set; }
    public object orientation { get; set; }

    public ScreenHost(BrowserInfo info)
    {
        width = info.ScreenWidth;
        height = info.ScreenHeight;
        availWidth = info.AvailWidth;
        availHeight = info.AvailHeight;
        availLeft = info.AvailLeft;
        availTop = info.AvailTop;
        colorDepth = info.ColorDepth;
        pixelDepth = info.PixelDepth;
        orientation = new { type = "landscape-primary", angle = 0 };
    }
}

// ─── Location host ────────────────────────────────────────────────────

public class LocationHost
{
    public string href { get; set; } = "";
    public string protocol { get; set; } = "http:";
    public string host { get; set; } = "";
    public string hostname { get; set; } = "";
    public string port { get; set; } = "";
    public string pathname { get; set; } = "/";
    public string search { get; set; } = "";
    public string hash { get; set; } = "";
    public string origin { get; set; } = "";
    public object[] ancestorOrigins => Array.Empty<object>();

    public LocationHost(BrowserInfo info)
    {
        href = info.Href;
        protocol = info.Protocol;
        host = info.Host;
        hostname = info.Hostname;
        port = info.Port;
        pathname = info.Pathname;
        search = info.Search;
        hash = info.Hash;
        origin = info.Origin;
    }

    public void reload() { }
    public void replace(string u) { if (u != null) href = u; }
    public void assign(string u) { if (u != null) href = u; }
    public override string ToString() => href;
}

public class WindowHost
{
    public WindowHost(string url, object doc, object loc)
    {
        Document = doc;
        Location = loc;
    }

    public bool closed { get; set; }
    public void close() { closed = true; }
    public void focus() { }
    public void blur() { }
    public object Document { get; }
    public object Location { get; }
}

public class MatchMediaHost
{
    public MatchMediaHost(string q) { media = q; }
    public bool matches { get; set; }
    public string media { get; }
    public void addEventListener(string t, object h) { }
    public void removeEventListener(string t, object h) { }
}
