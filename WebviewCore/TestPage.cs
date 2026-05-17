namespace WebviewCore;

static class TestPage
{
    public static string Html => @"<!DOCTYPE html>
<html lang=""zh-CN"">
<head>
<meta charset=""UTF-8"">
<title>WebViewCore HTML5 兼容性测试</title>
<style>
  * { box-sizing: border-box; }
  body { font-family: 'Segoe UI', Arial, sans-serif; margin: 20px; background: #f5f5f5; color: #333; }
  h1 { color: #2c3e50; border-bottom: 3px solid #3498db; padding-bottom: 8px; }
  h2 { color: #2980b9; margin-top: 30px; border-left: 4px solid #3498db; padding-left: 10px; }
  h3 { color: #555; }
  .section { background: #fff; border-radius: 8px; padding: 16px 20px; margin: 16px 0; box-shadow: 0 2px 8px rgba(0,0,0,0.1); }
  .test-item { margin: 8px 0; padding: 8px; border: 1px dashed #ddd; border-radius: 4px; }
  .test-item:hover { background: #f0f8ff; }
  .highlight { background: #ffffaa; padding: 2px 6px; border-radius: 3px; }
  .box { display: inline-block; width: 80px; height: 40px; margin: 4px; text-align: center; line-height: 40px; color: #fff; font-weight: bold; border-radius: 4px; }
  table.test { border-collapse: collapse; width: 100%; }
  table.test th, table.test td { border: 1px solid #999; padding: 8px; text-align: left; }
  table.test th { background: #3498db; color: #fff; }
  table.test tr:nth-child(even) { background: #f2f2f2; }
  .btn { display: inline-block; padding: 6px 16px; margin: 4px; background: #3498db; color: #fff; border: none; border-radius: 4px; cursor: pointer; font-size: 14px; }
  .btn:hover { background: #2980b9; }
  .btn.green { background: #27ae60; }
  .btn.green:hover { background: #219a52; }
  .btn.red { background: #e74c3c; }
  .btn.red:hover { background: #c0392b; }
  .output { background: #1e1e1e; color: #0f0; padding: 8px 12px; border-radius: 4px; font-family: Consolas, monospace; font-size: 13px; min-height: 20px; margin: 8px 0; white-space: pre-wrap; }
  .flex-demo { display: flex; gap: 8px; padding: 12px; background: #ecf0f1; border-radius: 6px; }
  .flex-demo > div { background: #3498db; color: #fff; padding: 12px; border-radius: 4px; flex: 1; text-align: center; }
  .shadow-demo { width: 120px; height: 60px; margin: 12px; display: inline-flex; align-items: center; justify-content: center; background: #fff; border-radius: 6px; font-size: 12px; }
  .ibox { display: inline-block; width: 80px; height: 50px; margin: 4px; text-align: center; line-height: 50px; color: #fff; font-weight: bold; border-radius: 4px; font-size: 14px; }
  .transform-demo { display: inline-block; width: 80px; height: 80px; margin: 12px 20px; background: #e74c3c; color: #fff; text-align: center; line-height: 80px; border-radius: 8px; }
  .badge { display: inline-block; padding: 2px 8px; border-radius: 10px; font-size: 11px; font-weight: bold; }
  .badge.pass { background: #27ae60; color: #fff; }
  .badge.fail { background: #e74c3c; color: #fff; }
</style>
</head>
<body>
<h1>🌐 WebViewCore HTML5 兼容性测试</h1>
<p>测试页面版本: 1.0 | 引擎: WebViewCore | <span id=""ua""></span></p>

<!-- ==================== JS: 环境检测 ==================== -->
<script>
document.getElementById('ua').textContent = navigator.userAgent;
console.log('=== WebViewCore 测试开始 ===');
console.log('UserAgent:', navigator.userAgent);
console.log('平台:', navigator.platform, '| 语言:', navigator.language);
console.log('Cookie:', navigator.cookieEnabled ? '启用' : '禁用');
console.log('屏幕:', screen.width + 'x' + screen.height);
console.log('硬件并发:', navigator.hardwareConcurrency);
</script>

<!-- ==================== 1. 文本标签 ==================== -->
<div class=""section"">
<h2>1. 文本标签</h2>
<div class=""test-item""><b>粗体</b> <strong>strong</strong> <i>斜体</i> <em>em</em> <u>下划线</u> <s>删除线</s> <mark>高亮</mark> <small>小字</small> <code>code</code> <kbd>Ctrl+C</kbd></div>
<div class=""test-item"">上标: x<sup>2</sup> + y<sup>2</sup> = z<sup>2</sup> &nbsp; 下标: H<sub>2</sub>O &nbsp; <abbr title=""World Wide Web"">WWW</abbr> &nbsp; <dfn>HTML</dfn></div>
<div class=""test-item""><blockquote>这是一段引用（blockquote），应该有缩进。</blockquote></div>
<div class=""test-item""><pre>预格式化文本:
  保留    空格
  和换行</pre></div>
</div>

<!-- ==================== 2. 标题 ==================== -->
<div class=""section"">
<h2>2. 标题（h1-h6）</h2>
<h1>h1. 标题一</h1>
<h2>h2. 标题二</h2>
<h3>h3. 标题三</h3>
<h4>h4. 标题四</h4>
<h5>h5. 标题五</h5>
<h6>h6. 标题六</h6>
</div>

<!-- ==================== 3. 列表 ==================== -->
<div class=""section"">
<h2>3. 列表</h2>
<div style=""display:flex;gap:40px;"">
<div><b>无序列表</b><ul><li>苹果</li><li>香蕉<ul><li>海南香蕉</li><li>进口香蕉</li></ul></li><li>橙子</li></ul></div>
<div><b>有序列表</b><ol><li>第一步</li><li>第二步<ol><li>子步骤 2.1</li><li>子步骤 2.2</li></ol></li><li>第三步</li></ol></div>
<div><b>定义列表</b><dl><dt>HTML</dt><dd>超文本标记语言</dd><dt>CSS</dt><dd>层叠样式表</dd></dl></div>
</div>
</div>

<!-- ==================== 4. 表格 ==================== -->
<div class=""section"">
<h2>4. 表格</h2>
<table class=""test"">
<thead><tr><th>姓名</th><th>年龄</th><th>城市</th><th>操作</th></tr></thead>
<tbody>
<tr><td>张三</td><td>28</td><td>北京</td><td><button class=""btn"" onclick=""alert('编辑: 张三')"">编辑</button></td></tr>
<tr><td>李四</td><td>35</td><td>上海</td><td><button class=""btn green"" onclick=""alert('编辑: 李四')"">编辑</button></td></tr>
<tr><td>王五</td><td>22</td><td>深圳</td><td><button class=""btn red"" onclick=""alert('编辑: 王五')"">编辑</button></td></tr>
</tbody>
</table>
</div>

<!-- ==================== 5. 表单 ==================== -->
<div class=""section"">
<h2>5. 表单</h2>
<form id=""testForm"" onsubmit=""alert('表单已提交');return false"">
<div style=""display:grid;grid-template-columns:1fr 1fr;gap:12px;"">
<div>
<label>文本输入:</label><br><input type=""text"" name=""name"" placeholder=""请输入姓名"" value=""测试用户"" style=""width:95%;padding:6px;border:1px solid #ccc;border-radius:4px;"">
</div>
<div>
<label>密码:</label><br><input type=""password"" name=""pwd"" placeholder=""密码"" style=""width:95%;padding:6px;border:1px solid #ccc;border-radius:4px;"">
</div>
<div>
<label>数字:</label><br><input type=""number"" name=""age"" value=""25"" min=""0"" max=""150"" style=""width:95%;padding:6px;border:1px solid #ccc;border-radius:4px;"">
</div>
<div>
<label>邮箱:</label><br><input type=""email"" name=""email"" placeholder=""email@example.com"" style=""width:95%;padding:6px;border:1px solid #ccc;border-radius:4px;"">
</div>
<div>
<label>选择:</label><br><select style=""width:95%;padding:6px;border:1px solid #ccc;border-radius:4px;""><option>选项一</option><option>选项二</option><option>选项三</option></select>
</div>
<div>
<label>文本域:</label><br><textarea rows=""2"" style=""width:95%;padding:6px;border:1px solid #ccc;border-radius:4px;"">多行文本</textarea>
</div>
</div>
<fieldset style=""margin-top:12px;padding:12px;border:1px solid #ccc;border-radius:6px;"">
<legend>兴趣爱好（多选）</legend>
<label><input type=""checkbox"" name=""hobby"" value=""read"" checked> 阅读</label>&nbsp;&nbsp;
<label><input type=""checkbox"" name=""hobby"" value=""music""> 音乐</label>&nbsp;&nbsp;
<label><input type=""checkbox"" name=""hobby"" value=""sport""> 运动</label>
</fieldset>
<fieldset style=""margin-top:8px;padding:12px;border:1px solid #ccc;border-radius:6px;"">
<legend>性别（单选）</legend>
<label><input type=""radio"" name=""gender"" value=""male"" checked> 男</label>&nbsp;&nbsp;
<label><input type=""radio"" name=""gender"" value=""female""> 女</label>
</fieldset>
<div style=""margin-top:12px;text-align:center;"">
<input type=""submit"" class=""btn"" value=""提交表单"">
<input type=""button"" class=""btn green"" value=""弹窗测试"" onclick=""alert('按钮点击测试通过 ✓')"">
</div>
</form>
</div>

<!-- ==================== 6. CSS 样式测试 ==================== -->
<div class=""section"">
<h2>6. CSS 样式</h2>

<h3>6.1 颜色与背景</h3>
<div class=""box"" style=""background:#e74c3c;"">#e74c3c</div>
<div class=""box"" style=""background:#3498db;"">#3498db</div>
<div class=""box"" style=""background:#27ae60;"">#27ae60</div>
<div class=""box"" style=""background:#f39c12;"">#f39c12</div>
<div class=""box"" style=""background:#9b59b6;"">#9b59b6</div>
<div class=""box"" style=""background:linear-gradient(45deg,#e74c3c,#3498db);"">渐变</div>

<h3>6.2 边框</h3>
<div style=""display:flex;gap:12px;flex-wrap:wrap;"">
<div style=""border:2px solid #e74c3c;padding:10px;border-radius:0;"">实线边框</div>
<div style=""border:2px dashed #3498db;padding:10px;"">虚线边框</div>
<div style=""border:2px dotted #27ae60;padding:10px;"">点线边框</div>
<div style=""border:2px solid #e74c3c;padding:10px;border-radius:12px;"">圆角 12px</div>
<div style=""border:2px solid #3498db;padding:10px;border-radius:50%;width:80px;height:80px;text-align:center;display:flex;align-items:center;justify-content:center;"">圆形</div>
<div style=""outline:3px solid #f39c12;outline-offset:2px;padding:10px;"">轮廓线</div>
</div>

<h3>6.3 阴影</h3>
<div style=""display:flex;gap:16px;flex-wrap:wrap;"">
<div class=""shadow-demo"" style=""box-shadow:4px 4px 8px rgba(0,0,0,0.3);"">box-shadow</div>
<div class=""shadow-demo"" style=""box-shadow:0 4px 12px rgba(52,152,219,0.4);"">蓝色阴影</div>
<div class=""shadow-demo"" style=""box-shadow:inset 0 2px 8px rgba(0,0,0,0.2);"">内阴影</div>
</div>

<h3>6.4 变换</h3>
<div style=""display:flex;gap:24px;flex-wrap:wrap;align-items:center;min-height:100px;"">
<div class=""transform-demo"" style=""transform:rotate(15deg);"">旋转 15°</div>
<div class=""transform-demo"" style=""transform:translate(10px,-10px);background:#3498db;"">平移</div>
<div class=""transform-demo"" style=""transform:scale(0.8);background:#27ae60;"">缩放 0.8</div>
<div class=""transform-demo"" style=""transform:rotate(-10deg) scale(0.9);background:#9b59b6;"">组合</div>
</div>

<h3>6.5 Flexbox</h3>
<div class=""flex-demo"">
<div>Flex 1</div>
<div style=""flex:2;"">Flex 2</div>
<div>Flex 1</div>
</div>

<h3>6.6 内外边距</h3>
<div style=""background:#ecf0f1;padding:8px;border-radius:4px;"">
<div style=""margin:16px;padding:12px;background:#3498db;color:#fff;border-radius:4px;"">margin: 16px; padding: 12px</div>
<div style=""margin:8px 32px;padding:8px 16px;background:#27ae60;color:#fff;border-radius:4px;"">margin: 8px 32px</div>
</div>

<h3>6.7 文本样式</h3>
<div class=""test-item"" style=""text-align:left;"">左对齐 (text-align: left)</div>
<div class=""test-item"" style=""text-align:center;"">居中 (text-align: center)</div>
<div class=""test-item"" style=""text-align:right;"">右对齐 (text-align: right)</div>
<div class=""test-item"" style=""letter-spacing:4px;"">字间距 4px (letter-spacing)</div>
<div class=""test-item"" style=""line-height:2.0;"">行高 2.0 (line-height)</div>
<div class=""test-item"" style=""text-indent:32px;"">首行缩进32px (text-indent)</div>
</div>

<!-- ==================== 7. 链接 ==================== -->
<div class=""section"">
<h2>7. 超链接</h2>
<a href=""#"" onclick=""alert('链接点击 ✓');return false;"">点击此链接（触发 onclick）</a><br><br>
<a href=""http://info.cern.ch/hypertext/WWW/TheProject.html"" target=""_blank"">CERN 第一个网页（外部链接）</a>
</div>

<!-- ==================== 8. JavaScript API 测试 ==================== -->
<div class=""section"">
<h2>8. JavaScript API 测试</h2>

<h3>8.1 弹窗</h3>
<button class=""btn"" onclick=""alert('alert 测试 ✓')"">alert</button>
<button class=""btn green"" onclick=""var r=confirm('确认测试');document.getElementById('confirmResult').textContent=r?'已确认':'已取消'"">confirm</button>
<span id=""confirmResult"" style=""margin-left:8px;""></span>
<button class=""btn red"" onclick=""var r=prompt('请输入内容','默认值');document.getElementById('promptResult').textContent=r||'已取消'"">prompt</button>
<span id=""promptResult"" style=""margin-left:8px;""></span>

<h3>8.2 Navigator</h3>
<div class=""output"" id=""navOutput"">
appName: <span id=""navAppName""></span>
appVersion: <span id=""navAppVersion""></span>
platform: <span id=""navPlatform""></span>
userAgent: <span id=""navUA""></span>
language: <span id=""navLang""></span>
cookieEnabled: <span id=""navCookie""></span>
onLine: <span id=""navOnline""></span>
hardwareConcurrency: <span id=""navCpu""></span>
</div>
<script>
document.getElementById('navAppName').textContent = navigator.appName;
document.getElementById('navAppVersion').textContent = navigator.appVersion;
document.getElementById('navPlatform').textContent = navigator.platform;
document.getElementById('navUA').textContent = navigator.userAgent;
document.getElementById('navLang').textContent = navigator.language;
document.getElementById('navCookie').textContent = navigator.cookieEnabled;
document.getElementById('navOnline').textContent = navigator.onLine;
document.getElementById('navCpu').textContent = navigator.hardwareConcurrency;
</script>

<h3>8.3 Console / 计时器</h3>
<button class=""btn"" onclick=""console.log('console.log 测试 ✓');console.warn('console.warn 测试 ✓');console.error('console.error 测试 ✓');alert('已输出到 Console Tab')"">console 测试</button>
<button class=""btn green"" onclick=""console.time('timer1');var c=0;for(var i=0;i<100000;i++)c+=i;console.timeEnd('timer1');alert('计时完成，查看 Console Tab')"">performance 计时</button>

<h3>8.4 Storage</h3>
<button class=""btn"" onclick=""localStorage.setItem('test','Hello Storage!');alert('已写入 localStorage')"">写入 localStorage</button>
<button class=""btn green"" onclick=""var v=localStorage.getItem('test');alert('读取: '+(v||'未找到'))"">读取 localStorage</button>
<button class=""btn red"" onclick=""sessionStorage.setItem('sessTest','Session Value');alert('已写入 sessionStorage')"">写入 sessionStorage</button>
<button class=""btn"" onclick=""var v=sessionStorage.getItem('sessTest');alert('读取: '+(v||'未找到'))"">读取 sessionStorage</button>

<h3>8.5 Screen / Location / History</h3>
<button class=""btn"" onclick=""alert('屏幕: '+screen.width+'x'+screen.height+'\n色彩深度: '+screen.colorDepth+'\n可用区域: '+screen.availWidth+'x'+screen.availHeight)"">屏幕信息</button>
<button class=""btn green"" onclick=""alert('当前 URL: '+location.href+'\n协议: '+location.protocol+'\n路径: '+location.pathname+'\n哈希: '+location.hash)"">Location 信息</button>
<button class=""btn red"" onclick=""history.pushState({page:1},'','?page=1');alert('已 pushState → 查看 URL 栏')"">History pushState</button>

<h3>8.6 定时器</h3>
<button class=""btn"" onclick=""setTimeout(function(){alert('setTimeout 延迟执行 ✓')},500)"">setTimeout (500ms)</button>
<span id=""timerDisplay""></span>
<button class=""btn green"" onclick=""var c=0;var id=setInterval(function(){c++;document.getElementById('timerDisplay').textContent='计数: '+c;if(c>=5){clearInterval(id);document.getElementById('timerDisplay').textContent+=' (完成)'}},500)"">setInterval x5</button>

<h3>8.7 DOM 操作</h3>
<button class=""btn"" onclick=""var d=document.createElement('div');d.textContent='动态创建的元素 ✓';d.style.cssText='padding:8px;background:#d5f5e3;border-radius:4px;margin-top:8px;';this.parentNode.appendChild(d)"">createElement</button>
<button class=""btn green"" onclick=""var r=document.querySelector('.section');alert('querySelector 找到: '+r?.querySelector('h2')?.textContent)"">querySelector</button>
<button class=""btn red"" onclick=""document.getElementById('domCounter').textContent=parseInt(document.getElementById('domCounter').textContent||'0')+1"">DOM 计数器</button>
<span id=""domCounter"" style=""margin-left:8px;font-size:24px;font-weight:bold;"">0</span>

<h3>8.8 Fetch / XHR</h3>
<button class=""btn"" onclick=""fetch('data:text/plain,Hello%20Fetch').then(function(r){return r.text()}).then(function(t){alert('Fetch 结果: '+t)})"">fetch 测试</button>
<button class=""btn green"" onclick=""var xhr=new XMLHttpRequest();xhr.open('GET','data:text/plain,XHR%20OK');xhr.onload=function(){alert('XHR 状态: '+xhr.status+' 响应: '+xhr.responseText)};xhr.send()"">XHR 测试</button>

<h3>8.9 Image / Blob / FileReader</h3>
<button class=""btn"" onclick=""var img=new Image();img.src='data:image/gif;base64,R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7';img.onload=function(){alert('Image 创建 ✓ 尺寸: '+img.width+'x'+img.height)};img.onerror=function(){alert('Image 加载失败')}"">Image 对象</button>
<button class=""btn green"" onclick=""var b=new Blob(['Hello Blob'],{type:'text/plain'});var fr=new FileReader();fr.onload=function(){alert('FileReader 读取: '+fr.result)};fr.readAsText(b)"">Blob + FileReader</button>

<h3>8.10 Promise / async</h3>
<button class=""btn"" onclick=""Promise.resolve('Promise ✓').then(function(v){alert(v)})"">Promise 测试</button>
<button class=""btn green"" onclick=""(async function(){var r=await Promise.resolve('Async/Await ✓');alert(r)})()"">async/await 测试</button>

<h3>8.11 JSON / atob / URL</h3>
<button class=""btn"" onclick=""var o={name:'test',value:123};alert('JSON: '+JSON.stringify(o)+'\nbtoa: '+btoa('hello')+'\natob: '+atob('aGVsbG8='))"">JSON/btoa/atob</button>
<button class=""btn green"" onclick=""var u=new URL('https://example.com/path?q=1#hash');alert('protocol: '+u.protocol+'\nhostname: '+u.hostname+'\npathname: '+u.pathname+'\nsearch: '+u.search+'\nhash: '+u.hash)"">URL 解析</button>

<h3>8.12 MutationObserver / AbortController</h3>
<button class=""btn"" onclick=""var o=new MutationObserver(function(m){alert('MutationObserver 触发 ✓ 变更数: '+m.length)});var t=document.getElementById('observerTarget');o.observe(t,{attributes:true});t.setAttribute('data-test','done')"">MutationObserver</button>
<span id=""observerTarget"" data-test=""init"">观察目标</span>
<button class=""btn green"" onclick=""var ac=new AbortController();setTimeout(function(){ac.abort();alert('AbortController ✓')},100);ac.signal.addEventListener('abort',function(){console.log('abort signal received')})"">AbortController</button>

<h3>8.13 WebSocket</h3>
<button class=""btn"" onclick=""var ws=new WebSocket('wss://echo.example.com');alert('WebSocket 对象创建 ✓ readyState: '+ws.readyState)"">WebSocket 创建</button>

<h3>8.14 Event 系统</h3>
<button id=""eventTestBtn"" class=""btn"" onclick=""alert('onclick 原生事件 ✓')"">onclick 事件</button>
<button class=""btn green"" id=""addeventBtn"">addEventListener 事件</button>
<script>
document.getElementById('addeventBtn').addEventListener('click',function(e){
  alert('addEventListener 触发 ✓\n事件类型: '+e.type+'\ntarget: '+e.target.tagName);
});
</script>

<h3>8.15 窗口属性</h3>
<button class=""btn"" onclick=""alert('innerWidth: '+window.innerWidth+'\ninnerHeight: '+window.innerHeight+'\ndevicePixelRatio: '+window.devicePixelRatio+'\npageXOffset: '+window.pageXOffset+'\npageYOffset: '+window.pageYOffset)"">窗口属性</button>
<button class=""btn green"" onclick=""alert('performance.now(): '+performance.now()+'\nperformance.timing.navigationStart: '+performance.timing.navigationStart)"">Performance</button>

<h3>8.16 crypto</h3>
<button class=""btn"" onclick=""var a=new Uint8Array(16);crypto.getRandomValues(a);alert('随机数: '+Array.from(a).slice(0,4).join(', ')+'\nUUID: '+crypto.randomUUID())"">crypto 随机数</button>

<h3>8.17 错误处理</h3>
<button class=""btn"" onclick=""try{throw new Error('自定义错误')}catch(e){alert('捕获错误: '+e.message+'\n栈: '+e.stack?.split('\n').slice(0,3).join('\n'))}"">try/catch 错误捕获</button>
</div>

<!-- ==================== 9. CSS 定位、浮动与 display ==================== -->
<div class=""section"">
<h2>9. CSS 定位、浮动与 display</h2>

<h3>9.1 display: inline-block</h3>
<div style=""padding:8px;background:#ecf0f1;border-radius:4px;"">
  <span style=""display:inline-block;width:80px;height:50px;background:#e74c3c;color:#fff;text-align:center;line-height:50px;border-radius:4px;"">A</span>
  <span style=""display:inline-block;width:80px;height:50px;background:#3498db;color:#fff;text-align:center;line-height:50px;border-radius:4px;"">B</span>
  <span style=""display:inline-block;width:80px;height:50px;background:#27ae60;color:#fff;text-align:center;line-height:50px;border-radius:4px;"">C</span>
  <span>后面跟随的内联文本</span>
</div>
<div class=""output"">说明: A、B、C 应为并排显示（不换行），可设置宽高</div>

<h3>9.2 shrink-to-fit 无宽度 inline-block</h3>
<div style=""padding:8px;background:#f9f9f9;border:1px solid #ddd;border-radius:4px;"">
  <div class=""btn"">中文</div>
  <div class=""btn green"">English Text</div>
  <div class=""btn red"">按钮</div>
  <span>跟随文本 — 每个btn应紧贴内容宽度</span>
</div>
<div class=""output"">说明: 三个按钮应水平并排，宽度仅包裹其内容（中文/English/按钮）</div>

<h3>9.3 position: relative</h3>
<div style=""padding:8px;background:#f9f9f9;border:1px solid #ddd;border-radius:4px;"">
  <div style=""background:#3498db;color:#fff;padding:6px 12px;border-radius:4px;"">正常位置</div>
  <div style=""position:relative;top:10px;left:20px;background:#e74c3c;color:#fff;padding:6px 12px;border-radius:4px;"">relative: top:10px; left:20px</div>
  <div style=""background:#27ae60;color:#fff;padding:6px 12px;border-radius:4px;margin-top:12px;"">后续元素不受影响</div>
</div>
<div class=""output"">说明: 红色块应相对于其正常位置向右偏移20px，向下偏移10px</div>

<h3>9.4 position: absolute</h3>
<div style=""position:relative;padding:16px;background:#f0f0f0;border:2px dashed #999;border-radius:4px;min-height:80px;"">
  包含块 (position:relative)
  <div style=""position:absolute;top:10px;right:10px;background:#e74c3c;color:#fff;padding:4px 10px;border-radius:4px;font-size:12px;"">absolute: top:10; right:10</div>
  <div style=""position:absolute;bottom:10px;left:10px;background:#3498db;color:#fff;padding:4px 10px;border-radius:4px;font-size:12px;"">absolute: bottom:10; left:10</div>
  <div style=""padding-top:30px;font-size:12px;color:#666;"">绝对定位元素脱离文档流，不占空间</div>
</div>
<div class=""output"">说明: 红色块在右上角，蓝色块在左下角，不占用正常流空间</div>

<h3>9.5 float: left / right</h3>
<div style=""padding:8px;background:#f9f9f9;border:1px solid #ddd;border-radius:4px;"">
  <div style=""float:left;width:100px;height:60px;background:#3498db;color:#fff;padding:8px;margin:4px;border-radius:4px;text-align:center;"">float:left</div>
  <div style=""float:right;width:100px;height:60px;background:#e74c3c;color:#fff;padding:8px;margin:4px;border-radius:4px;text-align:center;"">float:right</div>
  <p style=""font-size:13px;"">这是浮动元素后面的文本。文本应该环绕在浮动元素周围，左浮动在左边，右浮动在右边。这段文字用来测试文字环绕效果，看是否能正确在浮动元素之间流动。</p>
</div>
<div class=""output"">说明: 蓝色左浮动，红色右浮动，文字在中间环绕</div>

<h3>9.6 clear: both</h3>
<div style=""padding:8px;background:#f9f9f9;border:1px solid #ddd;border-radius:4px;"">
  <div style=""float:left;width:80px;height:40px;background:#3498db;color:#fff;text-align:center;line-height:40px;border-radius:4px;font-size:12px;"">浮动</div>
  <div style=""float:left;width:80px;height:40px;background:#27ae60;color:#fff;text-align:center;line-height:40px;border-radius:4px;font-size:12px;"">浮动</div>
  <div style=""clear:both;background:#e74c3c;color:#fff;padding:4px 8px;border-radius:4px;font-size:12px;"">clear:both — 换行并清除浮动</div>
</div>
<div class=""output"">说明: 红色块应在两个浮动元素下方新行显示</div>

</div>

<!-- ==================== 10. 综合测试 ==================== -->
<div class=""section"">
<h2>10. 综合渲染测试</h2>
<div style=""background:linear-gradient(135deg,#667eea 0%,#764ba2 100%);padding:20px;border-radius:12px;color:#fff;"">
<div style=""text-align:center;font-size:20px;font-weight:bold;margin-bottom:8px;"">渐变背景 + 居中文本</div>
<div style=""display:flex;gap:12px;justify-content:center;"">
<div style=""background:rgba(255,255,255,0.2);padding:12px 24px;border-radius:8px;backdrop-filter:blur(4px);"">毛玻璃效果</div>
<div style=""background:rgba(255,255,255,0.2);padding:12px 24px;border-radius:8px;backdrop-filter:blur(4px);"">半透明卡片</div>
</div>
</div>
<br>
<div style=""display:flex;gap:12px;justify-content:space-around;background:#fff;padding:16px;border-radius:8px;box-shadow:0 4px 16px rgba(0,0,0,0.1);"">
<div style=""text-align:center;""><div style=""font-size:32px;color:#e74c3c;"">❤</div><div>点赞 <span id=""likeCount"">0</span></div><button class=""btn"" onclick=""var c=document.getElementById('likeCount');c.textContent=parseInt(c.textContent)+1;console.log('点赞 +1 = '+c.textContent)"">👍</button></div>
<div style=""text-align:center;""><div style=""font-size:32px;color:#3498db;"">★</div><div>收藏 <span id=""starCount"">0</span></div><button class=""btn"" onclick=""var c=document.getElementById('starCount');c.textContent=parseInt(c.textContent)+1;console.log('收藏 +1 = '+c.textContent)"">⭐</button></div>
<div style=""text-align:center;""><div style=""font-size:32px;color:#27ae60;"">✓</div><div>完成 <span id=""doneCount"">0</span></div><button class=""btn"" onclick=""var c=document.getElementById('doneCount');c.textContent=parseInt(c.textContent)+1;console.log('完成 +1 = '+c.textContent)"">✅</button></div>
</div>
</div>

<script>
console.log('=== WebViewCore 测试完成 ===');
console.log('所有测试项已加载');
</script>
</body>
</html>";
}
