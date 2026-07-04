# better-wildcard 项目 Code Wiki

> **版本**：3.0.1.0
> **作者**：ak
> **类型**：TShock 服务器插件（Terraria）
> **平台**：.NET 8.0 / TShock 6.1（OTAPI 2.1 兼容）

---

## 1. 项目概述

`better-wildcard`（插件显示名：`bettergive`）是一个为 **TShock/Terraria** 服务器编写的批量发物品插件。它允许管理员通过简单的命令语法，快速向**单个玩家**、**多个玩家**、**全体在线玩家**发放物品，并且支持**排除指定玩家**、**给自己发物品**、**使用词缀（prefix）** 等进阶操作。

插件基于 **.NET 8.0** 与 **TShock 6.1** 开发，可跨平台部署于 Windows / Linux / macOS 等服务端环境，详见 [README.md](README.md#应用环境)。插件同时带有完整的**审计日志**与**异常记录**，便于服务器管理员追溯发放行为。

```
/workspace
├── .github/
│   └── workflows/
│       └── dotnet-desktop.yml           # GitHub Actions CI：.NET 类库构建
├── LICENSE
├── README.md                             # 项目简介与命令示例
├── CODE_WIKI.md                          # 代码 Wiki
├── EnhancedWildcardGivePlugin.cs        # 插件核心源代码（所有逻辑）
└── bettergive.csproj                     # .NET 工程文件（类库，net8.0）
```

---

## 2. 整体架构

本项目是一个标准的 **TShock 插件**，其架构可划分为三层：

| 层级 | 说明 | 对应模块 |
| --- | --- | --- |
| **插件宿主层** | 由 Terraria / TShock 运行时提供，负责加载 `TerrariaPlugin` 派生类并派发命令事件。 | `TerrariaPlugin` 基类、`TShockAPI`、`TerrariaApi.Server` |
| **命令注册层** | 插件自身负责向 `TShockAPI.Commands.ChatCommands` 注册命令处理器，并在卸载时清理。 | [EnhancedWildcardGivePlugin.Initialize](file:///workspace/EnhancedWildcardGivePlugin.cs#L34-L38)、[Dispose](file:///workspace/EnhancedWildcardGivePlugin.cs#L40-L49) |
| **业务逻辑层** | 命令解析、物品解析、目标玩家解析、物品发放、反馈与日志。 | [WGiveCommand](file:///workspace/EnhancedWildcardGivePlugin.cs#L51-L87)、[WGBoxCommand](file:///workspace/EnhancedWildcardGivePlugin.cs#L89-L157)、[TryResolveSingleItem](file:///workspace/EnhancedWildcardGivePlugin.cs#L165-L191)、[TryResolveTargets](file:///workspace/EnhancedWildcardGivePlugin.cs#L193-L268)、[ResolveTargetToken](file:///workspace/EnhancedWildcardGivePlugin.cs#L270-L320)、[TryGiveItem](file:///workspace/EnhancedWildcardGivePlugin.cs#L322-L337) |

### 2.1 核心流程图（以 `/wgive` 为例）

```
玩家输入 /wgive 物品名 目标 数量
        │
        ▼
  WGiveCommand(CommandArgs)
        │
        ├── 参数校验（至少 3 段）
        ├── 解析数量（TryParsePositiveInt）
        ├── 解析物品（TryResolveSingleItem → TShock.Utils.GetItemByIdOrName）
        ├── 解析目标（TryResolveTargets → ResolveTargetToken）
        │       ├── "*" / "all" / "@a" → 全体在线玩家
        │       ├── "me" → 命令执行者本人
        │       ├── "玩家名/ID" → 精确匹配
        │       ├── "!玩家" → 进入排除集合
        │       ├── 多目标用 "," 分隔
        │       └── 重名 → Ambiguous 状态，整体命令中止
        │
        ├── 遍历目标列表，调用 TryGiveItem（target.GiveItem）
        │       └── 异常 → 写入 TShock.Log.Error，继续发放下一位
        │
        ├── 写入审计日志（TShock.Log.Info：执行者/物品/数量/成功率）
        │
        └── 向每个目标发送成功消息，最终向执行者发送汇总消息
```

---

## 3. 主要模块职责

### 3.1 插件入口模块

- **文件**：[EnhancedWildcardGivePlugin.cs](file:///workspace/EnhancedWildcardGivePlugin.cs#L1-L339)
- **主要类型**：`EnhancedWildcardGivePlugin`（继承自 `TerrariaPlugin`）
- **职责**：
  - 在 `Initialize()` 中向 TShock 注册 `/wgive` 与 `/wgbox` 两个命令。
  - 在 `Dispose(bool)` 中按 `CommandDelegate` 移除对应命令，避免卸载后残留。
  - 声明插件元数据（`Name`、`Author`、`Description`、`Version`）。
  - 定义内部使用的 `TokenResolveStatus` 枚举与 `TokenResolveResult` 结构。
  - 定义日志前缀常量 `LogPrefix`。

### 3.2 命令处理模块

- **[WGiveCommand](file:///workspace/EnhancedWildcardGivePlugin.cs#L51-L87)**
  - 命令：`/wgive <物品ID或名称...> <目标> <数量>`
  - 语法约定：**最后一段**为数量，**倒数第二段**为目标，其余全部拼起来做物品名（因此物品名可带空格）。
  - 成功发放后写入审计日志：`TShock.Log.Info(...)`。

- **[WGBoxCommand](file:///workspace/EnhancedWildcardGivePlugin.cs#L89-L157)**
  - 命令：`/wgbox <目标> <物品ID或名称...> <数量> [词缀]`
  - 语法约定：**第一段**为目标；**可选的最后一段**若能被 `byte.TryParse` 成功则视为词缀（prefix），否则数量位于最后一段。
  - 成功发放后写入审计日志（含词缀信息）。

### 3.3 输入解析模块

- **[TryParsePositiveInt](file:///workspace/EnhancedWildcardGivePlugin.cs#L159-L163)**：通用的"必须为 >0 的正整数"解析器，用于校验数量。
- **[TryResolveSingleItem](file:///workspace/EnhancedWildcardGivePlugin.cs#L165-L191)**：使用 `TShock.Utils.GetItemByIdOrName` 解析物品文本，空、无匹配、多匹配都会给出错误消息并返回 `false`。
- **[TryResolveTargets](file:///workspace/EnhancedWildcardGivePlugin.cs#L193-L268)**：以英文逗号 `,` 分割目标串，分别加入「包含集合」或「排除集合」（以 `!` 开头为排除）。通过 `TokenResolveResult.Status` 识别 `Ambiguous` 状态，遇到重名则立即中止并给出明确提示。
- **[ResolveTargetToken](file:///workspace/EnhancedWildcardGivePlugin.cs#L270-L320)**：单个目标 token 的解析，返回 `TokenResolveResult`（`Ok` / `Ambiguous` / `NotFound`）。识别 `*`、`all`、`@a`、`me` 以及普通玩家名/ID。

### 3.4 发放与反馈模块

- **[TryGiveItem](file:///workspace/EnhancedWildcardGivePlugin.cs#L322-L337)**：对单个玩家调用 `TSPlayer.GiveItem(itemType, stack, prefix)`。`try/catch (Exception ex)` 捕获异常，写入 `TShock.Log.Error`，返回 `false`，防止单个异常玩家打断整个批量发放流程。
- 命令级反馈：在命令函数末尾，对每个目标发送 `SendSuccessMessage`，并向命令执行者发送含成功人数的汇总消息。
- **审计日志**（`TShock.Log.Info`）：记录执行者、命令、物品、数量、词缀、成功人数/总人数，便于服务器管理员追溯。

---

## 4. 关键类与函数说明

### 4.1 `EnhancedWildcardGivePlugin` 类

```csharp
[ApiVersion(2, 1)]
public class EnhancedWildcardGivePlugin : TerrariaPlugin
{
    public override string Name => "bettergive";
    public override string Author => "ak";
    public override string Description => "支持全体玩家、多目标、排除目标、me、@a、物品名空格的发物品命令";
    public override Version Version => new Version(3, 0, 1, 0);

    // 权限常量
    private const string GivePermission = "pw.give";
    private const string GBoxPermission = "pw.gbox";
    private const string LogPrefix = "[bettergive]";

    // 目标解析结果状态：用于支持重名/未找到的上层判断
    private enum TokenResolveStatus { Ok, Ambiguous, NotFound }
    private struct TokenResolveResult
    {
        public TokenResolveStatus Status;
        public List<TSPlayer> Players;
    }
}
```

- **API 版本**：`2.1`，表明该插件面向 **TShock 6.1 / OTAPI 2.1** 版本设计。
- **`TokenResolveStatus` / `TokenResolveResult`**：新增的状态传递机制。将"未找到目标"与"目标重名"两种失败分开处理，避免向玩家给出误导性的"未找到任何有效目标"错误。

#### 4.1.1 `Initialize()`

TShock 在加载插件时调用此方法。插件在此处把两个命令委托挂到全局命令表上。

#### 4.1.2 `Dispose(bool disposing)`

使用 `CommandDelegate` 做匹配而不是命令名，可以避免误删其它插件注册的同名命令。

### 4.2 命令入口函数

#### `WGiveCommand(CommandArgs args)`

- **位置**：[EnhancedWildcardGivePlugin.cs#L51-L87](file:///workspace/EnhancedWildcardGivePlugin.cs#L51-L87)
- **参数**：`args.Parameters` 按顺序表示 `<物品...> <目标> <数量>`。
- **关键约定**：
  - `args.Parameters[Count - 1]` → 数量
  - `args.Parameters[Count - 2]` → 目标
  - 前面全部项用空格连接 → 物品文本

#### `WGBoxCommand(CommandArgs args)`

- **位置**：[EnhancedWildcardGivePlugin.cs#L89-L157](file:///workspace/EnhancedWildcardGivePlugin.cs#L89-L157)
- **参数**：`<目标> <物品...> <数量> [词缀]`。
- **词缀解析**：当 `Parameters.Count >= 4` 且最后一段可解析为 `byte` 时，视为 `prefix`；否则数量位于最后一段。

### 4.3 解析工具函数

#### `TryParsePositiveInt(string text, out int value)`

- **位置**：[EnhancedWildcardGivePlugin.cs#L159-L163](file:///workspace/EnhancedWildcardGivePlugin.cs#L159-L163)
- **语义**：要求数值必须是整数且 `> 0`。

#### `TryResolveSingleItem(TSPlayer player, string itemText, out Item item)`

- **位置**：[EnhancedWildcardGivePlugin.cs#L165-L191](file:///workspace/EnhancedWildcardGivePlugin.cs#L165-L191)
- **行为**：空文本 → 未找到 → 重名 → 精确匹配。

#### `TryResolveTargets(TSPlayer sender, string targetText, out List<TSPlayer> targets)`

- **位置**：[EnhancedWildcardGivePlugin.cs#L193-L268](file:///workspace/EnhancedWildcardGivePlugin.cs#L193-L268)
- **新增逻辑**：
  1. 遍历 token 时记录 `hasAmbiguousMatch`。
  2. 若有任何 token 返回 `Ambiguous` 状态 → 立即终止并给玩家一个**明确的"重名"提示**。
  3. 否则按 `include - exclude` 方式计算最终目标列表。

#### `ResolveTargetToken(TSPlayer sender, string token)`

- **位置**：[EnhancedWildcardGivePlugin.cs#L270-L320](file:///workspace/EnhancedWildcardGivePlugin.cs#L270-L320)
- **返回类型**：`TokenResolveResult`（取代原 `List<TSPlayer>`）。
- **三种状态**：
  - `Ok` → 正常解析，`Players` 非空。
  - `Ambiguous` → 玩家名重名，`SendMultipleMatchError` 已调用。
  - `NotFound` → 未找到玩家，`SendErrorMessage` 已调用。

### 4.4 发放函数

#### `TryGiveItem(TSPlayer target, int itemType, int stack, int prefix)`

- **位置**：[EnhancedWildcardGivePlugin.cs#L322-L337](file:///workspace/EnhancedWildcardGivePlugin.cs#L322-L337)
- **改进**：
  - 原 `catch { }` 吞掉了所有异常信息。
  - 现改为 `catch (Exception ex)` 并写入 `TShock.Log.Error`，包含目标玩家名、物品参数和异常消息，便于排错。
  - 仍然返回 `false` 以便上层继续发放其他玩家。

---

## 5. 依赖关系

### 5.1 外部依赖

| 依赖 | 用途 | 提供方 |
| --- | --- | --- |
| `System`、`System.Collections.Generic`、`System.Linq` | .NET BCL（基础类库） | .NET 8 |
| `Terraria`（`Item`、`Main` 等） | Terraria 游戏对象模型 | Terraria 主程序 |
| `TerrariaApi.Server`（`TerrariaPlugin`、`ApiVersionAttribute`） | 插件宿主与生命周期 | TShock / OTAPI |
| `TShockAPI`（`Commands`、`TSPlayer`、`TShock`、`CommandArgs`、`Command`） | 命令系统与玩家对象 | TShock 服务器 |

### 5.2 内部调用关系（函数调用图）

```
EnhancedWildcardGivePlugin
├── Initialize()
│   ├── Commands.ChatCommands.Add (+wgive)
│   └── Commands.ChatCommands.Add (+wgbox)
├── Dispose(bool)
│   ├── Commands.ChatCommands.RemoveAll (wgive)
│   └── Commands.ChatCommands.RemoveAll (wgbox)
├── WGiveCommand(args)
│   ├── TryParsePositiveInt()
│   ├── TryResolveSingleItem()
│   │   └── TShock.Utils.GetItemByIdOrName()
│   ├── TryResolveTargets()
│   │   └── ResolveTargetToken() → TokenResolveResult
│   │       ├── TShock.Players
│   │       └── TSPlayer.FindByNameOrID()
│   ├── TryGiveItem()
│   │   └── TSPlayer.GiveItem() + TShock.Log.Error (on failure)
│   └── TShock.Log.Info (audit)
└── WGBoxCommand(args)
    ├── TryParsePositiveInt()
    ├── TryResolveSingleItem()
    ├── TryResolveTargets()
    ├── TryGiveItem()
    └── TShock.Log.Info (audit, with prefix)
```

---

## 6. 命令与权限

### 6.1 权限节点

| 命令 | 权限节点 | 默认拥有者 |
| --- | --- | --- |
| `/wgive` | `pw.give` | 需要管理员或专门授予 |
| `/wgbox` | `pw.gbox` | 需要管理员或专门授予 |

可通过 TShock 的 `/group addperm` / `/user addperm` 进行授权。

### 6.2 `/wgive`（普通发物品）

```
/wgive <物品ID或名称...> <目标> <数量>
```

- 支持物品名带空格（因为"目标"和"数量"从尾部取）。
- 示例：
  - `/wgive 夜刃 me 1` —— 给自己 1 把夜刃。
  - `/wgive 治疗药水 @a 999` —— 给全体在线玩家 999 瓶治疗药水。
  - `/wgive 铁剑 小明,小红,!小刚 1` —— 给"小明"和"小红"各 1 把铁剑，排除"小刚"。

### 6.3 `/wgbox`（目标前置 + 可选词缀）

```
/wgbox <目标> <物品ID或名称...> <数量> [词缀]
```

- 目标放在最前，与其他"先选人再发物"的命令习惯统一。
- 词缀（prefix）为 `byte` 类型（通常 0~255），用于附魔、前缀等。
- 示例：
  - `/wgbox all 神锤 1` —— 给全体玩家 1 把神锤。
  - `/wgbox 小明 钛金剑 1 83` —— 给"小明" 1 把词缀 `83` 的钛金剑。

### 6.4 目标语法

| 写法 | 含义 |
| --- | --- |
| `*`、`all`、`@a` | 全体在线玩家（大小写不敏感） |
| `me` | 命令执行者本人 |
| 玩家名 / 玩家 ID | 精确匹配一个玩家（重名将要求精确选择） |
| `玩家A,玩家B` | 同时包含玩家 A 与玩家 B |
| `!玩家C` | 排除玩家 C |
| `@a,!小明,!小刚` | 全体在线玩家，但排除"小明"和"小刚" |

### 6.5 审计日志示例

每次命令执行成功后，服务器日志会写入一条记录：

```
[bettergive] 执行者=admin(0) 命令=/wgive 物品=夜刃(225) 数量=1 发放成功人数=3/3
[bettergive] 执行者=admin(0) 命令=/wgbox 物品=钛金剑(489) 数量=1 词缀=83 发放成功人数=1/1
```

发放失败时会写入：

```
[bettergive] 向玩家 Alice(5) 发放物品失败: itemType=225, stack=1, prefix=0, 错误=...
```

---

## 7. 项目构建与运行方式

### 7.1 环境要求

- **开发框架**：.NET 8 SDK（工程文件配置 `net8.0`，与 TShock 6.1 配套）。
- **运行时**：TShock 服务器（要求 `TerrariaPlugin` API Version ≥ 2.1，即 TShock 6.1 系列）。
- **IDE**：Visual Studio / Rider / Visual Studio Code（均可）。

### 7.2 依赖引用配置

仓库提供了 [bettergive.csproj](file:///workspace/bettergive.csproj) 作为 .NET SDK 风格的类库工程。在首次构建前，请按如下步骤配置本地依赖：

1. 将 TShock 服务器的文件置于某个本地目录（例如 `C:\TShock`）。通常需要以下文件：
   - `TShockAPI.dll`（位于 `ServerPlugins/`）
   - `Terraria.exe`（Terraria 主程序，作为引用程序集）
   - `TerrariaServer.exe`
   - `OTAPI.dll`
2. 打开 [bettergive.csproj](file:///workspace/bettergive.csproj)，将每个 `<Reference>` 下的 `<HintPath>` 修改为指向你本地文件的实际路径。
3. 执行：

```bash
dotnet build -c Release
```

4. 将产出的 `bin/Release/bettergive.dll` 复制到 TShock 服务器的 `ServerPlugins/` 目录。
5. 重启 TShock 服务器。

### 7.3 验证插件已加载

在 TShock 控制台或游戏中输入：

```
/plugins
```

查看是否出现 `bettergive v3.0.1.0`。

### 7.4 权限授予示例

```
/group addperm superadmin pw.give
/group addperm superadmin pw.gbox
```

---

## 8. 目录结构与文件说明

```
/workspace
├── LICENSE                              # 开源许可证
├── README.md                            # 项目简介与命令预览
├── EnhancedWildcardGivePlugin.cs      # 插件主程序（~340 行）
├── bettergive.csproj                    # .NET 工程文件（类库，net8.0）
└── .github/
    └── workflows/
        └── dotnet-desktop.yml           # GitHub Actions CI：类库构建 + 上传 DLL
```

---

## 9. 版本与变更记录

| 版本 | 变更点 |
| --- | --- |
| 3.0.1.0 | 当前版本：支持 `/wgive`、`/wgbox`；支持 `*`/`all`/`@a`/`me`、多目标、`!` 排除、物品名带空格、可选词缀；**新增**审计日志、异常日志、`TokenResolveResult` 重名识别；**新增** `.csproj` 工程文件与 GitHub Actions 工作流。 |

---

## 10. 快速索引

- **主类**：[EnhancedWildcardGivePlugin](file:///workspace/EnhancedWildcardGivePlugin.cs#L11-L338)
- **命令注册/卸载**：[Initialize](file:///workspace/EnhancedWildcardGivePlugin.cs#L34-L38)、[Dispose](file:///workspace/EnhancedWildcardGivePlugin.cs#L40-L49)
- **/wgive 实现**：[WGiveCommand](file:///workspace/EnhancedWildcardGivePlugin.cs#L51-L87)
- **/wgbox 实现**：[WGBoxCommand](file:///workspace/EnhancedWildcardGivePlugin.cs#L89-L157)
- **数量解析**：[TryParsePositiveInt](file:///workspace/EnhancedWildcardGivePlugin.cs#L159-L163)
- **物品解析**：[TryResolveSingleItem](file:///workspace/EnhancedWildcardGivePlugin.cs#L165-L191)
- **目标解析**：[TryResolveTargets](file:///workspace/EnhancedWildcardGivePlugin.cs#L193-L268)、[ResolveTargetToken](file:///workspace/EnhancedWildcardGivePlugin.cs#L270-L320)
- **状态枚举/结构**：[TokenResolveStatus & TokenResolveResult](file:///workspace/EnhancedWildcardGivePlugin.cs#L22-L28)
- **发放逻辑（含异常日志）**：[TryGiveItem](file:///workspace/EnhancedWildcardGivePlugin.cs#L322-L337)
- **工程文件**：[bettergive.csproj](file:///workspace/bettergive.csproj)
- **CI 配置**：[dotnet-desktop.yml](file:///workspace/.github/workflows/dotnet-desktop.yml)
