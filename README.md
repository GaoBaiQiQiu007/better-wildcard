# better-wildcard

> 一个功能更强的 TShock 通配符发物品插件（`bettergive`）

基于 **.NET 平台** 与 **TShock 6.1** 开发，允许服务器管理员通过简洁的命令语法，快速向单个玩家、多个玩家或全体在线玩家批量发放物品，并支持排除指定玩家、给自己发、指定词缀等进阶操作，附带完整的审计日志与异常记录。

## 功能特性

- 支持物品名带空格（物品名 / 目标 / 数量按位置从尾部识别）。
- 支持目标通配符：`*`、`all`、`@a`、`me`、玩家名 / 玩家 ID。
- 支持多目标与排除目标：`玩家A,玩家B,!玩家C`。
- 支持可选词缀（prefix）：用于附魔、前缀等场景。
- 完整审计日志：执行者、命令、物品、数量、词缀、成功/总人数。
- 异常隔离：单个玩家发放失败不会中断整个批量流程。

## 应用环境

本插件面向 **.NET 平台 + TShock 6.1** 服务器场景，可在以下环境中部署运行：

### 运行时环境

| 项 | 要求 / 推荐 |
| --- | --- |
| .NET 运行时 | **.NET 8.0**（与 TShock 6.1 一致） |
| TShock 版本 | **TShock 6.1**（OTAPI 2.1 兼容） |
| Terraria 服务端 | 与 TShock 6.1 配套的 Terraria 服务端版本 |
| 插件 API 版本 | `ApiVersion(2, 1)` |

### 操作系统

TShock 6.1 服务端基于 .NET 8，支持跨平台部署，本插件可在以下任一系统运行：

- **Windows**：Windows Server 2016+ / Windows 10 / Windows 11
- **Linux**：Ubuntu 20.04+ / Debian 11+ / CentOS 8+ / 其他主流发行版（需安装 .NET 8 运行时）
- **macOS**：macOS 11 Big Sur 及以上版本

### 部署场景

- **本地独立服**：管理员本地搭建的 TShock 服务器，将 `bettergive.dll` 直接放入 `ServerPlugins/` 目录。
- **云服务器 / VPS**：在云厂商（AWS / 阿里云 / 腾讯云 / Vultr 等）部署的 TShock 服务器。
- **容器化部署**：基于 Docker 镜像运行的 TShock 容器，将插件 DLL 挂载或拷入镜像的 `ServerPlugins/` 目录。
- **面板管理服**：基于 TShock 的游戏面板（如 Pterodactyl 等游戏面板）托管的实例。
- **CI 自动化部署**：通过 GitHub Actions 构建产物（`bettergive.dll`）自动发布到服务器或 Release 仓库。

### 开发与构建环境

| 项 | 推荐版本 |
| --- | --- |
| .NET SDK | **8.0.x**（`net8.0` 目标框架） |
| IDE | Visual Studio 2022 / Rider / Visual Studio Code（安装 C# 扩展） |
| 构建命令 | `dotnet build bettergive.csproj -c Release` |
| 引用程序集 | `TShockAPI.dll`、`Terraria.exe`、`TerrariaServer.exe`、`OTAPI.dll`（来自本地 TShock 目录） |

## 命令一览

### `/wgive` — 普通发物品

```
/wgive <物品ID或名称...> <目标> <数量>
```

示例：

```
/wgive 夜刃 me 1
/wgive 治疗药水 @a 999
/wgive 铁剑 小明,小红,!小刚 1
```

### `/wgbox` — 目标前置 + 可选词缀

```
/wgbox <目标> <物品ID或名称...> <数量> [词缀]
```

示例：

```
/wgbox all 神锤 1
/wgbox 小明 钛金剑 1 83
```

### 目标语法

| 写法 | 含义 |
| --- | --- |
| `*` / `all` / `@a` | 全体在线玩家（大小写不敏感） |
| `me` | 命令执行者本人 |
| 玩家名 / 玩家 ID | 精确匹配一个玩家 |
| `玩家A,玩家B` | 同时包含玩家 A 与玩家 B |
| `!玩家C` | 排除玩家 C |
| `@a,!小明,!小刚` | 全体在线玩家，但排除"小明"和"小刚" |

## 权限节点

| 命令 | 权限节点 |
| --- | --- |
| `/wgive` | `pw.give` |
| `/wgbox` | `pw.gbox` |

授权示例：

```
/group addperm superadmin pw.give
/group addperm superadmin pw.gbox
```

## 安装与构建

1. 将本地 TShock 服务器目录下的引用程序集路径配置到 [bettergive.csproj](bettergive.csproj) 的 `<HintPath>` 中。
2. 执行构建：

   ```bash
   dotnet build bettergive.csproj -c Release
   ```

3. 将 `bin/Release/bettergive.dll` 复制到 TShock 服务器的 `ServerPlugins/` 目录。
4. 重启服务器，在控制台执行 `/plugins` 验证 `bettergive v3.0.1.0` 已加载。

## 项目结构

```
/workspace
├── .github/
│   └── workflows/
│       └── dotnet-desktop.yml         # GitHub Actions CI：.NET 构建 + 上传 DLL
├── LICENSE                             # GPLv3
├── README.md                           # 项目简介
├── CODE_WIKI.md                        # 详细代码 Wiki
├── EnhancedWildcardGivePlugin.cs      # 插件核心源代码
└── bettergive.csproj                   # .NET 工程文件（类库，net8.0）
```

## 许可证

本项目基于 [GPL-3.0](LICENSE) 协议开源。
