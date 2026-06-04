# 插件系统与 UI 扩展选型调整

> 创建日期: 2026-06-04 10:33
> 最后更新: 2026-06-04 10:33
> 作者: Adsicmes
> 状态: 草稿
> 关联需求: [需求.md](需求.md)

## 概述

在上一版 C# / .NET 10 LTS + WPF + Windows Shell 集成的基础上，插件 UI 技术调整为 WPF Shell + Blazor Hybrid 插件组件 + WebView2 底层承载。插件作者优先使用 C# + Razor 编写扩展，通过 `AssetManager.Plugin.Sdk` 声明文件类型、预览器、处理动作和 UI 贡献点。宿主通过 UI slot registry 控制插件可以出现的位置，使插件能灵活增加界面元素，同时避免插件直接修改 WPF VisualTree。

## 现状分析

上一版方案中的 WebView2 插件面板可以承载复杂 UI，但如果要求插件作者直接写 HTML/JavaScript、处理宿主通信和文件权限，门槛仍偏高。当前需求强调插件编写简单，并要求扩展预览、处理、独立面板和主界面元素，因此需要把插件 SDK 做成“声明式 + C# 组件化”的开发体验。

.NET 10 的 WPF Blazor Hybrid 支持在 WPF 应用中使用 `Microsoft.AspNetCore.Components.WebView.Wpf` 承载 Razor 组件，官方教程要求 WPF 项目使用 `Microsoft.NET.Sdk.Razor` 并加入 BlazorWebView。参考: [Build a WPF Blazor app](https://learn.microsoft.com/en-us/aspnet/core/blazor/hybrid/tutorials/wpf?view=aspnetcore-10.0)。

Razor Class Library 可以封装 Razor 组件和静态资源，并可打包复用。参考: [Reusable Razor UI in class libraries](https://learn.microsoft.com/en-us/aspnet/core/razor-pages/ui-class?view=aspnetcore-10.0)。

## 方案设计

### 调整后的整体架构

```text
WPF Desktop Shell
  |
  +-- Native Shell UI
  |     +-- main navigation
  |     +-- asset grid/list
  |     +-- preview container
  |     +-- inspector sidebar
  |     +-- toolbar/context menu/status bar
  |
  +-- UI Slot Registry
  |     +-- native command contributions rendered by WPF
  |     +-- Razor component panels rendered by BlazorWebView
  |     +-- isolated web panels rendered by WebView2
  |
  +-- Plugin SDK
  |     +-- file type handlers
  |     +-- preview providers
  |     +-- metadata extractors
  |     +-- asset actions and batch processors
  |     +-- UI contributions
  |
  +-- Plugin Runtime
        +-- trusted .NET plugin AssemblyLoadContext
        +-- Razor Class Library component loader
        +-- untrusted worker process and WebView2 sandbox
```

### 调整后的核心技术栈

| 层级 | 调整后选型 | 用途 |
|------|------------|------|
| 主程序 UI | WPF + MVVM | 主窗口、资源列表、资源管理器互操作、原生控件风格 |
| 插件 UI 主路径 | Blazor Hybrid for WPF | 用 Razor 组件承载插件预览、面板、设置页和详情区 |
| 插件 UI 包 | Razor Class Library | 插件组件和静态资源打包复用 |
| 插件 UI 底层 | WebView2 | BlazorWebView 底层承载，以及不可信 Web 面板沙箱 |
| 插件 SDK | `AssetManager.Plugin.Sdk` NuGet 包 | 插件契约、属性标注、默认基类、宿主服务接口 |
| 插件模板 | `dotnet new am-plugin` | 生成最小插件、预览插件、处理插件、面板插件 |
| 插件清单 | source generator + `plugin.json` | 从 C# 属性生成默认清单，打包时校验 |
| 插件加载 | AssemblyLoadContext + AssemblyDependencyResolver | 可信 .NET 插件依赖隔离 |
| 插件隔离 | worker process + named pipe RPC | 第三方不可信插件的安全边界 |

### 插件能力模型

插件不直接实现一个大而全的接口，而是按能力注册：

| 能力 | 接口 | 说明 |
|------|------|------|
| 文件类型识别 | `IFileTypeHandler` | 根据扩展名、MIME、文件头签名识别素材 |
| 元数据提取 | `IMetadataExtractor` | 读取尺寸、时长、颜色、EXIF、自定义属性等 |
| 缩略图生成 | `IThumbnailProvider` | 为新格式生成缩略图 |
| 预览渲染 | `IPreviewProvider` | 返回内置预览、Razor 组件预览或临时文件预览 |
| 单项操作 | `IAssetAction` | 对选中素材执行导出、转换、清理、发送等动作 |
| 批处理 | `IBatchProcessor` | 对一组素材执行批量处理 |
| UI 贡献 | `IUiContribution` | 向宿主 slot 添加按钮、菜单、面板、详情分区等 |
| 设置页 | `IPluginSettingsPage` | 提供插件配置 UI |

### UI 插槽设计

“任意增加 UI 元素”不设计为插件直接修改宿主窗口，而是设计为宿主暴露足够细的 UI slot。插件可以选择 slot，并声明排序、可见条件、文件类型条件和权限要求。

| Slot ID | 位置 | 适合的插件贡献 |
|---------|------|----------------|
| `app.toolbar.primary` | 主工具栏 | 按钮、下拉菜单、批处理入口 |
| `app.commandPalette` | 命令面板 | 命令、快捷操作 |
| `library.sidebar.section` | 左侧库导航 | 插件分类、智能集合 |
| `asset.grid.card.badge` | 素材卡片 | 格式徽标、状态标识 |
| `asset.grid.contextMenu` | 素材右键菜单 | 转换、导出、发送、打开方式 |
| `asset.preview.main` | 主预览区 | 文件格式专属预览组件 |
| `asset.preview.overlay` | 预览覆盖层 | 标注工具、播放控制、颜色取样 |
| `asset.inspector.section` | 右侧详情栏 | 元数据、插件专属属性、快捷工具 |
| `asset.operationPanel` | 独立操作面板 | 格式专属处理台、批量处理面板 |
| `search.filterBar` | 搜索过滤栏 | 插件提供的过滤条件 |
| `settings.pluginPages` | 设置页 | 插件设置界面 |
| `statusbar.right` | 状态栏 | 后台任务、同步状态、插件状态 |

简单按钮、菜单和徽标由 WPF 按宿主设计系统渲染；复杂面板和预览区域用 Razor 组件渲染。

### 预览扩展协议

`IPreviewProvider` 返回 `PreviewDescriptor`，宿主根据描述选择渲染方式：

```csharp
public interface IPreviewProvider
{
    ValueTask<bool> CanPreviewAsync(AssetContext asset, CancellationToken cancellationToken);

    ValueTask<PreviewDescriptor> CreatePreviewAsync(
        AssetContext asset,
        PreviewRequest request,
        CancellationToken cancellationToken);
}

public sealed record PreviewDescriptor
{
    public string Title { get; init; } = "";
    public PreviewKind Kind { get; init; }
    public Type? RazorComponentType { get; init; }
    public string? MaterializedFilePath { get; init; }
    public IReadOnlyDictionary<string, object?> Parameters { get; init; }
        = new Dictionary<string, object?>();
}
```

插件可返回三类预览：

1. 内置预览: 使用宿主已有图片、视频、文本、Markdown、PDF 预览器。
2. Razor 组件预览: 插件提供 Razor 组件，宿主放入 `asset.preview.main`。
3. 临时文件预览: 插件先生成可预览文件，宿主用内置预览器打开。

### 插件编写体验

插件作者使用模板创建项目：

```powershell
dotnet new am-plugin -n PsdTools --preview --panel --actions
```

最小插件代码形态：

```csharp
using AssetManager.Plugin.Sdk;

[AssetPlugin("com.example.psd-tools", Name = "PSD Tools", Version = "1.0.0")]
[FileType(".psd", MediaType = "image/vnd.adobe.photoshop")]
public sealed class PsdToolsPlugin : AssetPlugin
{
    public override void Configure(IPluginBuilder builder)
    {
        builder.AddPreview<PsdPreviewProvider>();
        builder.AddMetadataExtractor<PsdMetadataExtractor>();

        builder.AddAction("psd.exportPng", "导出 PNG")
            .ForExtensions(".psd")
            .Run(ExportPngAsync);

        builder.AddPanel<PsdToolsPanel>()
            .At(PluginSlots.AssetOperationPanel)
            .ForExtensions(".psd");
    }
}
```

Razor 面板示例：

```razor
@inherits AssetPluginPanel

<section class="plugin-panel">
    <h3>@Asset.DisplayName</h3>
    <button @onclick="ExportPngAsync">导出 PNG</button>
</section>

@code {
    private Task ExportPngAsync()
        => Host.Actions.ExecuteAsync("psd.exportPng", Asset.Id);
}
```

清单可由 source generator 从属性和 `Configure` 调用生成默认值；插件作者只在需要高级权限、兼容性约束或 marketplace 信息时手写 `plugin.json`。

### 插件包结构

```text
PsdTools.amplugin
├── plugin.json
├── bin/
│   ├── PsdTools.dll
│   ├── PsdTools.deps.json
│   └── dependency assemblies
├── wwwroot/
│   ├── PsdTools.styles.css
│   └── static assets
└── signature.json
```

`plugin.json` 示例：

```json
{
  "id": "com.example.psd-tools",
  "name": "PSD Tools",
  "version": "1.0.0",
  "sdkVersion": "1.0",
  "entryAssembly": "PsdTools.dll",
  "trustLevel": "trustedLocal",
  "fileTypes": [
    {
      "extensions": [".psd"],
      "mediaType": "image/vnd.adobe.photoshop"
    }
  ],
  "capabilities": [
    "asset.read",
    "temp.write",
    "ui.panel",
    "asset.process"
  ],
  "contributions": [
    {
      "kind": "panel",
      "slot": "asset.operationPanel",
      "component": "PsdTools.Components.PsdToolsPanel",
      "when": "asset.extension == '.psd'"
    }
  ]
}
```

### 安全与隔离

插件分为两个信任等级：

1. `trustedLocal`: 用户本地开发或明确信任的 .NET 插件，可以进程内加载，获得更好的性能和更简单的 Razor 组件体验。
2. `untrustedExternal`: 市场下载或来源不明插件，默认进程外运行，UI 使用隔离 WebView2 面板，处理逻辑通过 worker RPC 调用。

.NET 官方插件教程指出，自定义 AssemblyLoadContext 可以支持插件依赖隔离，但不可信代码不能安全加载到可信 .NET 进程中。参考: [Create a .NET application with plugins](https://learn.microsoft.com/en-us/dotnet/core/tutorials/creating-app-with-plugin-support)。

宿主所有文件能力都通过 `IAssetFileAccess`、`ITempFileService`、`IExportService` 这类受控服务暴露。插件拿到的是授权后的素材句柄或临时路径，不是任意磁盘访问权限。

### 关键流程

1. 插件安装时，宿主读取 `plugin.json`，校验 SDK 版本、签名、文件类型、能力声明和入口程序集。
2. 插件加载时，宿主建立插件上下文，并让插件通过 `IPluginBuilder` 注册能力。
3. UI 启动时，宿主把插件贡献项注册到 UI slot registry。
4. 用户选择素材后，宿主按文件类型、优先级和可见条件筛选可用预览器、动作和面板。
5. 用户打开预览时，宿主调用 `IPreviewProvider`，再渲染内置预览、Razor 组件或临时文件预览。
6. 用户点击插件动作时，宿主先校验能力，再调用对应插件动作或 worker RPC。

## 影响范围

- 上一版技术栈保留: .NET 10 LTS、WPF、Windows Shell 集成、SQLite/FTS5、AssemblyLoadContext、进程外 worker。
- 技术栈新增: Blazor Hybrid for WPF、Razor Class Library、插件 SDK source generator、`dotnet new` 插件模板、UI slot registry。
- 需要新增的模块: `Plugin.Sdk`、`Plugin.Sdk.Generators`、`Plugin.Templates`、`Desktop.UI.PluginSlots`、`Desktop.UI.BlazorHost`、`Plugin.Packaging`。

## 风险与缓解

| 风险 | 概率 | 影响 | 缓解措施 |
|------|------|------|----------|
| 插件任意 UI 扩展破坏主程序布局 | 高 | 高 | 只允许注册到宿主 slot，复杂 UI 受容器尺寸和主题变量约束 |
| 进程内 Razor 插件有安全风险 | 中 | 高 | 仅允许 trustedLocal；untrustedExternal 走进程外 worker 和隔离 WebView2 |
| 插件 SDK 版本变更破坏生态 | 中 | 高 | SDK 契约版本化，插件清单记录 sdkVersion，主程序提供兼容层 |
| BlazorWebView 与 WPF 布局边界复杂 | 中 | 中 | 只在预览、面板、设置页等矩形区域承载，不用于高频列表项 |
| 插件作者仍觉得复杂 | 中 | 中 | 提供 `dotnet new` 模板、默认基类、属性标注、示例插件和打包命令 |

## 测试策略

- 单元测试: 插件清单生成、slot 注册规则、文件类型匹配、能力校验、预览优先级排序。
- 集成测试: AssemblyLoadContext 加载卸载、Razor 组件面板渲染、worker RPC、插件崩溃恢复。
- 手动验证: 新增未知扩展名预览、素材右键菜单动作、详情栏插件分区、独立操作面板、插件设置页、禁用插件后 UI 贡献消失。
- POC 优先级: `dotnet new am-plugin` 最小插件 > Razor 预览组件 > UI slot registry > 进程外插件 worker。

## 相关文档

- 需求文档: [需求.md](需求.md)
- 上一版选型: [../2026-06-04-windows素材管理工具技术选型/设计-Windows素材管理工具技术选型.md](../2026-06-04-windows素材管理工具技术选型/设计-Windows素材管理工具技术选型.md)
- WPF Blazor Hybrid: [Build a WPF Blazor app](https://learn.microsoft.com/en-us/aspnet/core/blazor/hybrid/tutorials/wpf?view=aspnetcore-10.0)
- Razor Class Library: [Reusable Razor UI in class libraries](https://learn.microsoft.com/en-us/aspnet/core/razor-pages/ui-class?view=aspnetcore-10.0)
- .NET 插件加载: [Create a .NET application with plugins](https://learn.microsoft.com/en-us/dotnet/core/tutorials/creating-app-with-plugin-support)
- WebView2 原生互操作: [Interop of native and web code](https://learn.microsoft.com/en-us/microsoft-edge/webview2/how-to/communicate-btwn-web-native)
- dotnet 模板: [Custom templates for dotnet new](https://learn.microsoft.com/en-us/dotnet/core/tools/custom-templates)

---

## 修订记录

| 时间 | 作者 | 变更说明 |
|------|------|----------|
| 2026-06-04 10:33 | Adsicmes | 初始创建 |
