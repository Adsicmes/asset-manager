# Windows 素材管理工具技术选型

> 创建日期: 2026-06-04 10:23
> 最后更新: 2026-06-04 10:23
> 作者: Adsicmes
> 状态: 草稿
> 关联需求: [需求.md](需求.md)

## 概述

本项目推荐采用 C# / .NET 10 LTS + WPF 作为主程序技术栈，配合 Win32/OLE/Shell 原生集成、SQLite + FTS5 本地索引、WebView2 插件面板和分层插件宿主。这个方案把核心优势放在 Windows 文件互操作、成熟桌面 UI、插件扩展和本地优先数据管理上，适合仅面向 Windows 且需要与资源管理器、剪贴板、拖放和 QQ 等外部应用高频交互的素材管理工具。

## 现状分析

当前仓库尚无源码和既有框架约束，可按新项目选型。用户明确要求仅用于 Windows，且需要强文件交互，因此技术栈应优先贴近 Windows 桌面和 Shell/OLE 生态。

截至 2026-06-04，.NET 10 是 LTS 版本，官方支持到 2028-11-14；.NET 9 和 .NET 8 将在 2026-11-10 结束支持。因此新项目应直接选择 .NET 10，而不是从即将结束支持的 .NET 8/9 起步。参考: [.NET Support Policy](https://dotnet.microsoft.com/en-us/platform/support/policy)。

## 方案设计

### 整体架构

```text
WPF Desktop Shell
  |
  +-- Presentation: MVVM, virtualized lists, preview panes, command palette
  |
  +-- Application Core
  |     +-- Asset catalog service
  |     +-- Search service
  |     +-- Preview/thumbnail service
  |     +-- Plugin orchestration service
  |
  +-- Windows Integration
  |     +-- Clipboard and drag/drop DataObject
  |     +-- Shell file operations through IFileOperation
  |     +-- Explorer interop and optional shell-extension bridge
  |
  +-- Plugin Hosts
  |     +-- In-process trusted .NET plugins through AssemblyLoadContext
  |     +-- Out-of-process untrusted plugins through named pipe RPC
  |     +-- WebView2 panels for HTML/CSS/JS plugin UI
  |
  +-- Local Storage
        +-- SQLite metadata database
        +-- SQLite FTS5 search index
        +-- Thumbnail and temp materialization cache
```

### 核心技术栈

| 层级 | 选型 | 用途 |
|------|------|------|
| 运行时 | .NET 10 LTS | 主程序、服务、插件 SDK、后台任务 |
| UI | WPF + XAML + MVVM | 资源列表、标签、预览、拖放、虚拟化列表 |
| 状态与架构 | Microsoft.Extensions.Hosting / DependencyInjection / Options / Logging | 统一服务注册、配置、日志和后台任务 |
| MVVM | CommunityToolkit.Mvvm | ViewModel、命令、通知属性 |
| Windows API | Win32/OLE/Shell COM + CsWin32 生成绑定 | 剪贴板、拖放、文件操作、窗口句柄、Shell 语义 |
| 文件操作 | IFileOperation | 复制、移动、删除、重命名、进度和错误处理 |
| 拖放与剪贴板 | WPF DragDrop / DataObject + Shell Clipboard Formats | 与资源管理器、QQ 和其他 Windows 应用传递文件 |
| 数据库 | SQLite | 本地素材元数据、标签、库配置 |
| 搜索 | SQLite FTS5 | 文件名、标签、备注、元数据全文检索 |
| 缩略图缓存 | 本地文件缓存 + SQLite 索引 | 图片、视频、文档等素材预览 |
| 插件 UI | WebView2 | 插件设置页、复杂自定义面板、HTML/CSS/JS UI |
| 插件加载 | AssemblyLoadContext + AssemblyDependencyResolver | 可信 .NET 插件的依赖隔离和卸载 |
| 插件隔离 | 独立 worker 进程 + named pipe RPC | 第三方或不可信插件的安全边界 |
| 打包 | 自包含 EXE/MSI/安装器，插件放入 AppData | 避免安装目录写入限制，保留插件和缓存灵活性 |

### Windows 文件互操作

WPF 提供了可用于应用内和跨 Windows 应用的拖放基础设施，拖放和复制粘贴都围绕 DataObject 进行。参考: [WPF Drag and Drop](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/drag-and-drop)。

文件传输应按两类处理：

1. 已存在的本地文件: 使用 CF_HDROP 或 WPF 文件列表数据，让资源管理器、QQ 等目标应用拿到真实路径。
2. 虚拟素材或远端素材: 先物化到 `%LOCALAPPDATA%\AssetManager\TempDrop\`，再以真实文件路径发起拖放或剪贴板复制。后续如确需更完整的虚拟文件传输，再实现 CFSTR_FILEDESCRIPTOR + CFSTR_FILECONTENTS。

Windows Shell 官方文档将 CF_HDROP、CFSTR_FILEDESCRIPTOR、CFSTR_FILECONTENTS 归为文件系统对象和虚拟对象传输格式。参考: [Shell Clipboard Formats](https://learn.microsoft.com/en-us/windows/win32/shell/clipboard)。

复制、移动、删除、重命名等 Explorer 风格操作优先通过 IFileOperation 完成，而不是直接用 `File.Copy` / `Directory.Move` 包办所有场景。IFileOperation 支持 Shell 项复制、移动、重命名、删除、进度和错误对话框，并替代旧的 SHFileOperation。参考: [IFileOperation](https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/nn-shobjidl_core-ifileoperation)。

对 QQ 的处理应基于标准 Windows 文件拖放和真实文件路径，不为 QQ 编写专用协议。QQ 作为外部目标时，最可靠路径是把要发送的素材物化成真实文件，然后通过 CF_HDROP 拖出。

### 插件系统

插件系统分为三个层级：

1. 插件契约: `AssetManager.Abstractions` 定义稳定接口，例如 `IAssetPlugin`、`IMetadataExtractor`、`IThumbnailProvider`、`IExportTarget`、`ICommandContribution`、`IPluginPanel`。
2. 可信 .NET 插件: 使用独立 AssemblyLoadContext 加载，每个插件带自己的依赖目录和 `.deps.json`，由 AssemblyDependencyResolver 解析依赖。
3. 不可信插件: 使用独立 worker 进程运行，通过 named pipe RPC 与主程序通信。主程序只授予声明过的能力，例如读取选中素材、写入导出目录、创建临时文件、打开外部程序。

.NET 官方插件教程建议使用自定义 AssemblyLoadContext 和 AssemblyDependencyResolver 支持插件依赖隔离，但也明确指出不可信代码不能安全地加载到可信 .NET 进程中。因此第三方插件必须默认走进程外宿主。参考: [Create a .NET application with plugins](https://learn.microsoft.com/en-us/dotnet/core/tutorials/creating-app-with-plugin-support)。

插件包建议使用 `.amplugin` 压缩包，包含：

```text
plugin.json
bin/
ui/
assets/
README.md
signature.json
```

`plugin.json` 至少包含插件 ID、版本、目标 SDK 版本、入口点、能力声明、最小宿主版本和文件访问范围。插件安装目录放在 `%LOCALAPPDATA%\AssetManager\Plugins\{pluginId}\{version}\`。

### UI 与预览

主界面使用 WPF，不把整个应用做成 Electron/Tauri/Web 前端。原因是核心交互大量依赖 Windows DataObject、OLE、Shell COM、窗口句柄和本地文件语义，WPF 可以直接承接这些能力。

Web 技术只用于插件面板或复杂设置页，采用 WebView2 嵌入。WebView2 官方支持在 WPF、WinForms、WinUI 3 和 Win32 应用中使用。参考: [WebView2 Getting Started](https://learn.microsoft.com/en-us/microsoft-edge/webview2/get-started/get-started)。

### 数据与索引

SQLite 存储素材元数据、标签、集合、库配置、插件配置和扫描状态。全文搜索使用 SQLite FTS5，适合本地应用对文件名、标签、备注和抽取文本做检索。FTS5 是 SQLite 的全文搜索虚拟表模块。参考: [SQLite FTS5](https://www.sqlite.org/fts5.html)。

建议基础表：

```sql
CREATE TABLE assets (
  id TEXT PRIMARY KEY,
  path TEXT NOT NULL,
  display_name TEXT NOT NULL,
  media_type TEXT NOT NULL,
  size_bytes INTEGER NOT NULL,
  content_hash TEXT,
  created_at TEXT NOT NULL,
  updated_at TEXT NOT NULL
);

CREATE TABLE tags (
  id TEXT PRIMARY KEY,
  name TEXT NOT NULL UNIQUE
);

CREATE TABLE asset_tags (
  asset_id TEXT NOT NULL,
  tag_id TEXT NOT NULL,
  PRIMARY KEY (asset_id, tag_id)
);

CREATE VIRTUAL TABLE asset_search USING fts5(
  display_name,
  tags,
  notes,
  metadata
);
```

### 打包与分发

首版建议使用自包含 .NET 桌面安装包，不优先采用纯 MSIX。插件、缓存、缩略图和临时拖放文件必须写入 `%LOCALAPPDATA%\AssetManager\`，不要写入安装目录。

MSIX 的优势是现代安装和更新，但默认会把应用文件放在受保护位置，应用自身不能修改安装目录。参考: [MSIX containerization overview](https://learn.microsoft.com/en-us/windows/msix/msix-containerization-overview)。如果未来需要 Microsoft Store 或包身份，可以再评估 MSIX packaged with external location。参考: [Windows App SDK deployment guide](https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/deploy-unpackaged-apps)。

### 关键流程

1. 用户拖入资源管理器文件到应用，WPF Drop 事件读取 DataObject 中的文件路径，交给导入服务入库。
2. 用户从应用拖出素材到资源管理器或 QQ，应用判断素材是否为真实本地文件；如不是，先生成临时物化文件。
3. 拖放服务构造标准文件 DataObject，设置复制/移动效果，通过 WPF DragDrop.DoDragDrop 发起操作。
4. 用户执行复制、移动、删除、重命名，应用通过 IFileOperation 执行 Shell 文件操作，并同步更新 SQLite 索引。
5. 插件安装后由插件管理器校验清单和签名，可信插件加载进独立 AssemblyLoadContext，不可信插件启动独立 worker 进程。
6. 插件只通过 `AssetManager.Abstractions` 和授权能力访问宿主，避免直接引用主程序内部类型。

## 影响范围

- 需要新增的模块: Desktop.UI、Application、Domain、Infrastructure.Storage、Infrastructure.Windows、Plugin.Abstractions、Plugin.Host、Plugin.Worker。
- 对现有功能的影响: 当前无既有源码，不涉及迁移。
- 数据迁移: 首版无迁移；后续 SQLite schema 需要版本表和迁移脚本。

## 风险与缓解

| 风险 | 概率 | 影响 | 缓解措施 |
|------|------|------|----------|
| QQ 等外部软件不接受虚拟文件流 | 中 | 高 | 默认物化为真实临时文件，再用 CF_HDROP 拖放 |
| 插件在主进程内崩溃或访问越权 | 中 | 高 | 第三方插件默认进程外运行，能力声明和 RPC 边界强制校验 |
| 大量素材扫描阻塞 UI | 中 | 中 | 后台队列、增量扫描、缩略图延迟生成、WPF 虚拟化列表 |
| Shell/OLE 细节复杂 | 高 | 中 | 先做拖入、拖出、复制、粘贴 POC，沉淀 WindowsIntegration 模块 |
| MSIX 限制插件和缓存写入 | 中 | 中 | 首版使用自包含安装包，插件和缓存全部放 AppData |

## 测试策略

- 单元测试: 插件清单解析、能力校验、索引写入、搜索查询、路径规范化。
- 集成测试: SQLite 迁移、插件加载卸载、worker RPC、IFileOperation 包装层。
- 手动验证: 从资源管理器拖入文件、拖出到资源管理器、拖出到 QQ、复制/粘贴到资源管理器、虚拟素材物化后投递、插件崩溃不影响主程序。
- POC 优先级: Windows 拖放/剪贴板 POC > 插件隔离 POC > 索引/缩略图 POC > 安装包 POC。

## 相关文档

- 需求文档: [需求.md](需求.md)
- .NET 支持策略: [.NET Support Policy](https://dotnet.microsoft.com/en-us/platform/support/policy)
- WPF 拖放: [Drag and Drop - WPF](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/drag-and-drop)
- Shell 剪贴板格式: [Shell Clipboard Formats](https://learn.microsoft.com/en-us/windows/win32/shell/clipboard)
- 文件操作: [IFileOperation](https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/nn-shobjidl_core-ifileoperation)
- 插件加载: [Create a .NET application with plugins](https://learn.microsoft.com/en-us/dotnet/core/tutorials/creating-app-with-plugin-support)
- WebView2: [Getting Started tutorials](https://learn.microsoft.com/en-us/microsoft-edge/webview2/get-started/get-started)
- SQLite FTS5: [SQLite FTS5 Extension](https://www.sqlite.org/fts5.html)

---

## 修订记录

| 时间 | 作者 | 变更说明 |
|------|------|----------|
| 2026-06-04 10:23 | Adsicmes | 初始创建 |
