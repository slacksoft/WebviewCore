# WebViewCore

一个基于 .NET 6 的轻量级 Web 浏览器引擎，使用 SkiaSharp 进行自绘渲染，支持 HTML5/CSS3 基础解析与 V8 JavaScript 引擎。

## 功能特性

- **自绘渲染引擎** — 使用 SkiaSharp 实现布局、绘制，不依赖 WebView/Chromium
- **HTML5/CSS3 解析** — 基于 AngleSharp 解析 DOM/CSS，支持 Flexbox、Position、Float 等布局
- **V8 JavaScript 引擎** — 集成 Microsoft ClearScript + V8，支持 ES6、Promise、Async/Await
- **三标签页视图** — 每个浏览器窗口内置 Render（渲染）、Console（控制台）、Source（源码）三个视图
- **多标签页管理** — 主窗口支持新增/关闭浏览器标签页
- **JS API 兼容** — 支持 `window.open` / `window.close` / `window.print`、`alert`/`confirm`/`prompt`、`console`、`localStorage`、`setTimeout` 等 Web API
- **文件下载** — 自动检测下载类型请求，弹出下载页面保存文件
- **文本选择** — 支持页面文本选择与复制（Ctrl+C）

## 项目架构

采用三层架构：

```
WebviewCore/
├── UI/          # 表现层（WinForms）
│   ├── MainForm.cs          — 主窗口，多标签页管理
│   ├── BrowserControl.cs    — 浏览器用户控件（导航栏、渲染面板、控制台、源码）
│   ├── DownloadForm.cs      — 文件下载对话框
│   └── Program.cs           — 程序入口
├── BLL/         # 业务逻辑层
│   ├── LayoutEngine.cs      — 布局引擎
│   ├── RenderEngine.cs      — 渲染引擎
│   ├── StyleComputer.cs     — 样式计算
│   ├── CssParser.cs         — CSS 解析
│   ├── JsEngine.cs          — JavaScript 引擎封装
│   ├── HtmlFetcher.cs       — HTML 获取与编码检测
│   ├── ImageLoader.cs       — 图片加载与缓存
│   ├── TextMeasurer.cs      — 文本测量
│   ├── FontResolver.cs      — 字体解析
│   └── ScriptInterop.cs     — JS 互操作工具
└── Models/      # 数据模型层
    ├── Models.cs            — 核心数据类型（LayoutBox, BoxStyle 等）
    ├── DocumentHost.cs      — DOM 文档桥接
    ├── DomElementHost.cs    — DOM 元素桥接
    ├── DomEventHost.cs      — DOM 事件桥接
    └── ComputedStyleHost.cs — 计算样式桥接
```

## 系统要求

- Windows x64
- .NET 6 SDK 或运行时
- Visual Studio 2022（可选，用于开发）

## 构建与运行

```bash
# 克隆仓库
git clone https://github.com/slacksoft/WebviewCore.git
cd WebviewCore

# 构建
dotnet build WebviewCore/WebviewCore.csproj

# 运行
dotnet run --project WebviewCore/WebviewCore.csproj

# 自检测试
dotnet run --project WebviewCore/WebviewCore.csproj -- --self-test
```

## 使用方法

- 地址栏输入 URL 后按 Enter 或点击 Go 导航
- `←` 按钮返回上一页
- `+` 按钮新建标签页，标签页上的 `×` 关闭标签页
- 内置标签页切换：Render（渲染视图）、Console（JS 控制台）、Source（HTML 源码）
- 在 Console 输入框可直接执行 JavaScript 代码

## 依赖项

- [AngleSharp](https://anglesharp.github.io/) — HTML/CSS 解析
- [Microsoft ClearScript](https://github.com/microsoft/ClearScript) — V8 JavaScript 引擎
- [SkiaSharp](https://github.com/mono/SkiaSharp) — 2D 图形渲染
- [ExCSS](https://github.com/TylerBrinks/ExCSS) — CSS 选择器解析
