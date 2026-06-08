# Rider 开发与项目骨架

> 创建日期: 2026-06-08 09:52
> 最后更新: 2026-06-08 16:31
> 作者: Adsicmes

## 概述

本文档记录当前工作区的 .NET 10 / WPF 项目骨架、Rider 打开方式、项目分层和基础验证命令。当前项目已从 Windows 文件交互 POC 进入素材库 MVP：支持素材库注册与切换、导入、搜索、同步、预览、i18n 和基础架构边界测试。

## 开发环境

- SDK: .NET SDK 10.0.300
- IDE: JetBrains Rider
- 主程序框架: WPF, `net10.0-windows`
- 解决方案文件: `AssetManager.sln`
- SDK 锁定文件: `global.json`

在 Rider 中打开项目时，直接打开工作区根目录下的 `AssetManager.sln`。

## 项目结构

```text
asset-manager/
├── AssetManager.sln
├── global.json
├── src/
│   ├── AssetManager.Desktop/
│   ├── AssetManager.Application/
│   ├── AssetManager.Domain/
│   ├── AssetManager.Infrastructure.Storage/
│   ├── AssetManager.Infrastructure.Windows/
│   ├── AssetManager.Plugin.Abstractions/
│   ├── AssetManager.Plugin.Sdk/
│   ├── AssetManager.Plugin.Host/
│   └── AssetManager.Plugin.Worker/
└── tests/
    └── AssetManager.Tests/
```

## 分层说明

| 项目 | 目标框架 | 责任 |
|------|----------|------|
| `src/AssetManager.Desktop` | `net10.0-windows` | WPF 主程序、窗口、MVVM、后续 BlazorWebView 承载 |
| `src/AssetManager.Application` | `net10.0` | 素材库用例、导入、搜索、预览、类型 resolver 端口和应用级注册表端口 |
| `src/AssetManager.Domain` | `net10.0` | 素材、标签、素材库位置、素材类型标识和领域模型 |
| `src/AssetManager.Infrastructure.Storage` | `net10.0` | SQLite、FTS5、迁移、缩略图缓存和库内管理状态 |
| `src/AssetManager.Infrastructure.Windows` | `net10.0-windows` | DragDrop、Clipboard、IFileOperation 和 Shell 集成 |
| `src/AssetManager.Plugin.Abstractions` | `net10.0` | 插件公共契约 |
| `src/AssetManager.Plugin.Sdk` | `net10.0` | 插件作者使用的 SDK、默认基类和构建器 |
| `src/AssetManager.Plugin.Host` | `net10.0` | 插件注册表、贡献聚合；后续扩展插件加载、UI slot registry 和能力校验 |
| `src/AssetManager.Plugin.Worker` | `net10.0` | 进程外插件 worker 原型 |
| `tests/AssetManager.Tests` | `net10.0` | 单元测试和集成测试入口 |

## 项目引用方向

```text
Desktop
  -> Application
  -> Domain
  -> Infrastructure.Storage
  -> Infrastructure.Windows
  -> Plugin.Host

Application
  -> Domain

Infrastructure.Storage
  -> Application
  -> Domain

Infrastructure.Windows
  -> Application
  -> Domain

Plugin.Host
  -> Application
  -> Plugin.Abstractions
  -> Plugin.Sdk

Plugin.Sdk
  -> Plugin.Abstractions

Plugin.Worker
  -> Plugin.Abstractions
```

## Rider 使用方式

1. 打开 Rider。
2. 选择 `Open`。
3. 打开 `D:\UserFiles\Development\Projects\asset-manager\AssetManager.sln`。
4. 等待 Rider 完成 NuGet restore 和项目索引。
5. 运行配置选择 `AssetManager.Desktop`。

调试拖放、剪贴板和 QQ 投递时，Rider 建议普通权限启动，不要用管理员权限启动。Windows 的权限隔离可能导致普通权限的资源管理器或 QQ 无法和管理员权限的应用正常拖放。

## 验证命令

```powershell
dotnet --version
dotnet restore .\AssetManager.sln
dotnet build .\AssetManager.sln --no-restore
dotnet test .\AssetManager.sln --no-build
```

当前验证结果：

- `dotnet build .\AssetManager.sln --no-restore`: 通过，0 警告，0 错误。
- `dotnet test .\AssetManager.sln --no-build`: 通过，17 个测试通过。

## 当前关键入口

当前代码已经不是单纯 POC。Rider 中常看的入口如下：

| 文件 | 说明 |
|------|------|
| `src/AssetManager.Desktop/DesktopBootstrapper.cs` | 桌面端组合根，组装 Application 服务、Storage 实现、内置类型 resolver 和预览 renderer |
| `src/AssetManager.Desktop/MainWindow.xaml` | 主窗口布局，包含素材库选择、文件夹列表、素材列表、元数据编辑和预览区 |
| `src/AssetManager.Desktop/MainWindow.xaml.cs` | WPF 事件适配层，调用 Application 用例并更新 UI 状态 |
| `src/AssetManager.Desktop/Preview/IAssetPreviewRenderer.cs` | 桌面端预览渲染扩展点 |
| `src/AssetManager.Plugin.Abstractions/IAssetManagerPlugin.cs` | 插件最小公共契约 |
| `src/AssetManager.Plugin.Host/PluginRegistry.cs` | 进程内插件注册和贡献聚合 |
| `src/AssetManager.Plugin.Sdk/AssetManagerPluginBase.cs` | 插件作者默认基类 |
| `src/AssetManager.Application/Library/LibraryApplicationService.cs` | 素材库核心用例编排 |
| `src/AssetManager.Application/Library/IAssetTypeResolver.cs` | 素材类型识别端口 |
| `src/AssetManager.Domain/Library/AssetTypeId.cs` | 可扩展素材类型标识 |
| `src/AssetManager.Infrastructure.Storage/Library/SqliteAssetLibraryRepository.cs` | SQLite 存储、标签和搜索索引 |
| `src/AssetManager.Infrastructure.Storage/Library/FileSystemAssetContentStore.cs` | 文件复制、扫描、哈希和文本预览读取 |
| `src/AssetManager.Infrastructure.Windows/WindowsFileTransferService.cs` | 封装 `DataFormats.FileDrop` 读取、文件拖放 `DataObject` 构造和剪贴板写入 |
| `tests/AssetManager.Tests/UnitTest1.cs` | 覆盖素材库行为、i18n 资源完整性和架构边界 |

当前支持：

- 注册指定素材库位置，并在应用级注册表中持久化保存。
- 从已注册素材库下拉列表切换当前库。
- 导入文件或文件夹到当前 UI 文件夹，不自动创建 `assets` 目录。
- 搜索名称、标签、备注和基础元数据。
- 同步库内文件移动、重命名、删除和新增。
- 预览内置图片、视频、音频和文本类型。
- 使用 `zh-CN` 和 `en-US` 资源字典切换 UI 文案。
- 从素材列表拖出真实文件路径到资源管理器或 QQ，并支持写入 Windows 文件剪贴板。

仍需桌面手动验证：

- 从列表拖出到资源管理器后，资源管理器能复制文件。
- 从列表拖出到 QQ 后，QQ 能接收文件。
- 点击 Copy 后，在资源管理器中粘贴能得到文件。
- 切换 `zh-CN`/`en-US` 后，主窗口和弹窗文案实时刷新。

## 注意事项

- `AssetManager.Application` 这个命名空间会和 WPF 的 `Application` 类型同名；WPF `App.xaml.cs` 中应显式使用 `System.Windows.Application`。
- `AssetManager.Infrastructure.Windows` 目标框架必须是 `net10.0-windows`，否则后续无法干净承载 WPF/Windows 拖放、剪贴板和 Shell 相关类型。
- 当前 POC 只传递真实文件路径。后续虚拟素材或插件生成内容需要先物化到临时文件，再复用同一文件拖放通路。
- `AssetManager.Application` 不再引用 `AssetManager.Plugin.Abstractions`。插件能力接入前，应先明确稳定 contract，再由 Plugin.Host 适配到 Application 端口。
- 当前插件项目只是最小契约和注册表骨架，还未实现 DLL 扫描、隔离加载、权限模型、Worker IPC 或 WPF UI slot 注入。
- 新增素材类型时使用 `IAssetTypeResolver`，新增桌面预览时使用 `IAssetPreviewRenderer`，不要在 Domain 加扩展名表，也不要把预览 switch 写回 `MainWindow`。

## 相关文档

- [素材管理工具产品需求实现规划](../../features/2026-06-04-素材管理工具产品需求规划/实现.md)
- [Windows 素材管理工具技术选型](../../features/2026-06-04-windows素材管理工具技术选型/设计-Windows素材管理工具技术选型.md)
- [插件系统与 UI 扩展选型调整](../../features/2026-06-04-插件系统与UI扩展选型调整/设计-插件系统与UI扩展选型调整.md)

---

## 修订记录

| 时间 | 作者 | 变更说明 |
|------|------|----------|
| 2026-06-08 09:52 | Adsicmes | 初始创建 |
| 2026-06-08 10:03 | Adsicmes | 补充 Windows 文件交互 POC 当前实现和手动验证清单 |
| 2026-06-08 16:31 | Adsicmes | 翻新为当前素材库 MVP 架构入口，移除已失效 AssetTransferItem POC 说明 |
| 2026-06-08 16:31 | Adsicmes | 补充插件最小契约、SDK 基类和注册表入口 |
