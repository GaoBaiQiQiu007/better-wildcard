# better-wildcard 项目 Code Wiki

> **版本**：3.0.1.0
> **作者**：ak
> **类型**：TShock 服务器插件（Terraria）

---

## 1. 项目概述

`better-wildcard`（插件显示名：`bettergive`）是一个为 **TShock/Terraria** 服务器编写的批量发物品插件。它允许管理员通过简单的命令语法，快速向**单个玩家**、**多个玩家**、**全体在线玩家**发放物品，并且支持**排除指定玩家**、**给自己发物品**、**使用词缀（prefix）** 等进阶操作。

仓库结构极简，核心代码集中在单个 C# 源文件中：

```
/workspace
├── .github/
│   └── workflows/
│       └── dotnet-desktop.yml   # GitHub Actions CI 模板（.NET Desktop 默认模板，未针对本项目配置）
├── LICENSE
├── README.md                     # 项目简介与命令示例
└── bettergive.cs                 # 插件核心源代码（所有逻辑）
```

---

## 2. 整体架构

本项目是一个标准的 **TShock 插件**，其架构可划分为三层：

| 层级 | 说明 | 对应模块 |
| --- | --- | --- |
| **插件宿主层** | 由 Terraria / TShock 运行时提供，负责加载 `TerrariaPlugin` 派生类并派发命令事件。 | `TerrariaPlugin` 基类、`TShockAPI`、`TerrariaApi.Server` |
| **命令注册层** | 插件自身负责向 `TShockAPI.Commands.ChatCommands` 注册命令处理器，并在卸载时清理。 | [EnhancedWildcardGivePlugin.Initialize](file:///workspace/bettergive.cs#L25-L29)、[Dispose](file:///workspace/bettergive.cs#L31-L40) |
| **业务逻辑层** | 命令解析、物品解析、目标玩家解析、物品发放与反馈。 | [WGiveCommand](file:///workspace/bettergive.cs#L42-L77)、[WGBoxCommand](file:///workspace/bettergive.cs#L79-L144)、[TryResolveSingleItem](file:///workspace/bettergive.cs#L152-L178)、[TryResolveTargets](file:///workspace/bettergive.cs#L180-L242)、[TryGiveItem](file:///workspace/bettergive.cs#L290-L304) |

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
        │       └── 多目标用 "," 分隔
        │
        └── 遍历目标列表，调用 TryGiveItem（target.GiveItem）
                并向每个目标发送成功消息，最终向执行者发送汇总消息
```

---

## 3. 主要模块职责

整个插件只有一个命名空间与一个主类，但在逻辑上可拆分为以下模块：

### 3.1 插件入口模块

- **文件**：[bettergive.cs](file:///workspace/bettergive.cs#L1-L306)
- **主要类型**：`EnhancedWildcardGivePlugin`（继承自 `TerrariaPlugin`）
- **职责**：
  - 在 `Initialize()` 中向 TShock 注册 `/wgive` 与 `/wgbox` 两个命令。
  - 在 `Dispose(bool)` 中按 `CommandDelegate` 移除对应命令，避免卸载后残留。
  - 声明插件元数据（`Name`、`Author`、`Description`、`Version`）。

### 3.2 命令处理模块

- **[WGiveCommand](file:///workspace/bettergive.cs#L42-L77)**
  - 命令：`/wgive <物品ID或名称...> <目标> <数量>`
  - 语法约定：**最后一段**为数量，**倒数第二段**为目标，其余全部拼起来做物品名（因此物品名可带空格）。
- **[WGBoxCommand](file:///workspace/bettergive.cs#L79-L144)**
  - 命令：`/wgbox <目标> <物品ID或名称...> <数量> [词缀]`
  - 语法约定：**第一段**为目标；**可选的最后一段**若能被 `byte.TryParse` 成功则视为词缀（prefix），否则与倒数第二段一起作为数量。
  - 与 `/wgive` 区别：目标位置固定在开头，方便按"先选人再发物"的习惯操作，同时支持词缀。

### 3.3 输入解析模块

- **[TryParsePositiveInt](file:///workspace/bettergive.cs#L146-L150)**：通用的"必须为 >0 的正整数"解析器，用于校验数量。
- **[TryResolveSingleItem](file:///workspace/bettergive.cs#L152-L178)**：使用 `TShock.Utils.GetItemByIdOrName` 解析物品文本，空、无匹配、多匹配都会给出错误消息并返回 `false`。
- **[TryResolveTargets](file:///workspace/bettergive.cs#L180-L242)**：以英文逗号 `,` 分割目标串，分别加入「包含集合」或「排除集合」（以 `!` 开头为排除），最终输出 `include - exclude` 的结果列表。
- **[ResolveTargetToken](file:///workspace/bettergive.cs#L244-L288)**：单个目标 token 的解析逻辑，识别 `*`、`all`、`@a`、`me` 以及普通玩家名/ID。

### 3.4 发放与反馈模块

- **[TryGiveItem](file:///workspace/bettergive.cs#L290-L304)**：对单个玩家调用 `TSPlayer.GiveItem(itemType, stack, prefix)`，用 `try/catch` 吞掉异常，返回布尔值表示是否发放成功。
- 在命令处理函数中，对每个成功发放的目标分别发送 `SendSuccessMessage`，并在结尾向命令执行者发送汇总消息（含发放人数、物品、可选词缀）。

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
}
```

- **继承**：`TerrariaPlugin`（由 `TerrariaApi.Server` 提供）。
- **API 版本**：`2.1`，表明该插件面向 **TShock 5.x / OTAPI 2.1** 版本设计。
- **权限常量**：
  - `GivePermission = "pw.give"`：使用 `/wgive` 的权限节点。
  - `GBoxPermission = "pw.gbox"`：使用 `/wgbox` 的权限节点。

#### 4.1.1 `Initialize()`

```csharp
public override void Initialize()
{
    Commands.ChatCommands.Add(new Command(GivePermission, WGiveCommand, "wgive"));
    Commands.ChatCommands.Add(new Command(GBoxPermission, WGBoxCommand, "wgbox"));
}
```

TShock 在加载插件时会调用此方法。插件在此处把两个命令委托挂到全局命令表上。

#### 4.1.2 `Dispose(bool disposing)`

```csharp
protected override void Dispose(bool disposing)
{
    if (disposing)
    {
        Commands.ChatCommands.RemoveAll(c => c.CommandDelegate == WGiveCommand);
        Commands.ChatCommands.RemoveAll(c => c.CommandDelegate == WGBoxCommand);
    }
    base.Dispose(disposing);
}
```

使用 `CommandDelegate` 做匹配而不是命令名，可以避免误删其它插件注册的同名命令。

### 4.2 命令入口函数

#### `WGiveCommand(CommandArgs args)`

- **位置**：[bettergive.cs#L42-L77](file:///workspace/bettergive.cs#L42-L77)
- **参数**：`args.Parameters` 按顺序表示：`<物品...> <目标> <数量>`。
- **关键约定**：
  - `args.Parameters[Count - 1]` → 数量
  - `args.Parameters[Count - 2]` → 目标
  - 前面全部项用空格连接 → 物品文本
- **返回/副作用**：
  - 参数不足时发送错误用法消息并直接 `return`。
  - 解析完毕后遍历目标列表逐个发放，统计 `success` 数并向执行者汇报。

#### `WGBoxCommand(CommandArgs args)`

- **位置**：[bettergive.cs#L79-L144](file:///workspace/bettergive.cs#L79-L144)
- **参数**：`<目标> <物品...> <数量> [词缀]`。
- **词缀解析**：当 `Parameters.Count >= 4` 且最后一段可解析为 `byte` 时，视为 `prefix`；否则数量位于最后一段。

### 4.3 解析工具函数

#### `TryParsePositiveInt(string text, out int value)`

- **位置**：[bettergive.cs#L146-L150](file:///workspace/bettergive.cs#L146-L150)
- **语义**：要求数值必须是整数且 `> 0`。返回值表示解析成功与否。
- **注意**：`int.TryParse` 本身允许前导符号，这对于"数量"是合理的，但 `value > 0` 过滤掉了 0 与负数。

#### `TryResolveSingleItem(TSPlayer player, string itemText, out Item item)`

- **位置**：[bettergive.cs#L152-L178](file:///workspace/bettergive.cs#L152-L178)
- **行为**：
  1. 空文本 → `false` 并发送错误消息。
  2. 调用 `TShock.Utils.GetItemByIdOrName(itemText)` 获得候选列表。
  3. 候选为 0 → `false` 并提示"未找到物品"。
  4. 候选多于 1 → `false` 并调用 `player.SendMultipleMatchError` 让玩家精确选择。
  5. 候选恰好 1 → 返回 `true`，`out item` 指向唯一结果。

#### `TryResolveTargets(TSPlayer sender, string targetText, out List<TSPlayer> targets)`

- **位置**：[bettergive.cs#L180-L242](file:///workspace/bettergive.cs#L180-L242)
- **核心数据结构**：两个 `HashSet<TSPlayer>` —— `include`、`exclude`。
- **语法**：`target1,target2,!exclude1,!exclude2`。每个 token 可选以 `!` 开头表示排除。
- **特殊情况**：
  - 若 `include` 为空（例如只写排除项） → 报错"未找到任何有效目标"。
  - 最终结果 `include - exclude` 为空 → 报错"目标列表为空"。

#### `ResolveTargetToken(TSPlayer sender, string token)`

- **位置**：[bettergive.cs#L244-L288](file:///workspace/bettergive.cs#L244-L288)
- **关键字 token**：
  - `*`、`all`、`@a`（大小写不敏感）→ 返回所有 `TShock.Players` 中 `Active && != null` 的玩家。
  - `me` → 若命令执行者 `Active`，返回他自己。
  - 其他 → 调用 `TSPlayer.FindByNameOrID(token)`，做同样的 0/1/N 重名处理。
- **注意**：当重名时会发送多条匹配提示，但**仍然返回空列表**，需要调用者（`TryResolveTargets`）靠 `include.Count == 0` 来决定是否中止。

### 4.4 发放函数

#### `TryGiveItem(TSPlayer target, int itemType, int stack, int prefix)`

- **位置**：[bettergive.cs#L290-L304](file:///workspace/bettergive.cs#L290-L304)
- **实现要点**：
  - 先校验 `target` 非空且 `Active`。
  - 调用 `target.GiveItem(itemType, stack, prefix)`（TShock 提供的发放 API，内部会处理背包容量——超出部分掉落到世界中）。
  - 用 `try/catch { }` 吞掉所有异常，简单返回 `false`。这防止了单个异常玩家导致整个命令中断。
  - **缺点**：异常信息被完全丢弃，不利于排错。建议至少在日志输出一条 `TShockAPI.Log.Error`。

---

## 5. 依赖关系

### 5.1 外部依赖

| 依赖 | 用途 | 提供方 |
| --- | --- | --- |
| `System`、`System.Collections.Generic`、`System.Linq` | .NET BCL（基础类库） | .NET Framework / .NET |
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
│   │   └── ResolveTargetToken()
│   │       ├── TShock.Players
│   │       └── TSPlayer.FindByNameOrID()
│   └── TryGiveItem()
│       └── TSPlayer.GiveItem()
└── WGBoxCommand(args)
    ├── TryParsePositiveInt()
    ├── TryResolveSingleItem()
    ├── TryResolveTargets()
    └── TryGiveItem()
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

---

## 7. 项目构建与运行方式

### 7.1 环境要求

- **开发框架**：.NET（建议使用与所运行 TShock 版本对应的 `.NET Framework 4.x` 或 `.NET 6/7/8`，取决于 TShock 发行版）。
- **运行时**：TShock 服务器（要求 `TerrariaPlugin` API Version ≥ 2.1）。
- **SDK**：对应 .NET SDK（如果用 .csproj 管理）。
- **IDE**：Visual Studio / Rider / Visual Studio Code（均可）。

### 7.2 依赖引用

由于仓库**没有 `.csproj` / `.sln` 工程文件**，需要手动配置编译：

1. 创建一个类库项目（Class Library），目标框架与 TShock 一致。
2. 添加以下程序集引用（来自 TShock 服务器目录）：
   - `TShockAPI.dll`
   - `TerrariaServer.exe` / `Terraria.dll`（对应 Terraria 主程序）
   - `OTAPI.dll` / `TerrariaApi.Server.dll`（视 TShock 版本而定）
3. 将 [bettergive.cs](file:///workspace/bettergive.cs) 加入项目。
4. 执行 `dotnet build -c Release` 或在 IDE 中按 Release 编译。
5. 将产出的 `EnhancedWildcardGivePlugin.dll`（或你项目定义的输出文件名）复制到 TShock 服务器的 `ServerPlugins/` 目录。
6. 重启 TShock 服务器；日志中应出现 `bettergive v3.0.1.0` 加载提示。

> **注意**：仓库中的 [dotnet-desktop.yml](file:///workspace/.github/workflows/dotnet-desktop.yml) 是一个 `.NET Core Desktop` 的默认模板，包含 WPF/WinForms 打包相关步骤，**并不适用于本项目**。使用前需替换为类库（classlib）构建工作流。

### 7.3 验证插件已加载

在 TShock 控制台或游戏中输入：

```
/plugins
```

查看是否出现 `bettergive` 条目。

### 7.4 权限授予示例

```
/group addperm superadmin pw.give
/group addperm superadmin pw.gbox
```

---

## 8. 目录结构与文件说明

```
/workspace
├── LICENSE                   # 开源许可证
├── README.md                 # 项目简短说明（中文）
├── bettergive.cs             # 插件主源码（命名空间 EnhancedWildcardGivePlugin）
└── .github/
    └── workflows/
        └── dotnet-desktop.yml  # GitHub Actions 默认模板（需自行替换）
```

### 8.1 文件角色表

| 文件 | 角色 |
| --- | --- |
| [README.md](file:///workspace/README.md) | 项目简介与命令预览 |
| [bettergive.cs](file:///workspace/bettergive.cs) | 插件主程序：插件类、两个命令处理函数、解析工具、发放工具 |
| [dotnet-desktop.yml](file:///workspace/.github/workflows/dotnet-desktop.yml) | GitHub Actions 模板（.NET Desktop），需要按实际项目重写 |

---

## 9. 代码优化建议

> 下面的建议仅供参考，均为非破坏性改进：

### 9.1 `TryGiveItem`：异常日志

当前 `catch { }` 吞掉了全部异常，异常玩家不会留下任何线索。建议改为：

```csharp
private bool TryGiveItem(TSPlayer target, int itemType, int stack, int prefix)
{
    if (target == null || !target.Active)
        return false;
    try
    {
        target.GiveItem(itemType, stack, prefix);
        return true;
    }
    catch (Exception ex)
    {
        TShock.Log.Error($"[bettergive] 给玩家 {target.Name}({target.Index}) 发放物品失败: {ex.Message}");
        return false;
    }
}
```

### 9.2 `ResolveTargetToken`：重名时明确告知上层

当前当重名时它会发送多条匹配错误消息，但返回空列表导致上层走"未找到目标"分支，对玩家不够友好。可以返回一个枚举（`Found / Ambiguous / NotFound`）或让上层在遇到"已发送错误消息"时直接 `return`。

### 9.3 使用 `.csproj` 工程文件

仓库目前缺少工程文件，不利于自动化构建。建议新增一个 `bettergive.csproj`（示例）：

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net6.0</TargetFramework>
    <AssemblyName>bettergive</AssemblyName>
    <RootNamespace>EnhancedWildcardGivePlugin</RootNamespace>
    <GenerateAssemblyInfo>false</GenerateAssemblyInfo>
    <AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath>
    <AppendRuntimeIdentifierToOutputPath>false</AppendRuntimeIdentifierToOutputPath>
  </PropertyGroup>
  <ItemGroup>
    <!-- 指向你本地 TShock 的 DLL 目录 -->
    <Reference Include="TShockAPI, Version=5.0.0.0, Culture=neutral">
      <HintPath>..\..\TShock\ServerPlugins\TShockAPI.dll</HintPath>
      <Private>False</Private>
    </Reference>
    <Reference Include="Terraria">
      <HintPath>..\..\TShock\Terraria.exe</HintPath>
      <Private>False</Private>
    </Reference>
    <Reference Include="TerrariaApi.Server">
      <HintPath>..\..\TShock\TerrariaServer.exe</HintPath>
      <Private>False</Private>
    </Reference>
  </ItemGroup>
</Project>
```

### 9.4 更新 GitHub Actions

当前 `.github/workflows/dotnet-desktop.yml` 针对桌面应用。建议替换为以下类库构建流程（极简版本）：

```yaml
name: .NET Build

on:
  push:
    branches: [ main ]
  pull_request:
    branches: [ main ]

jobs:
  build:
    runs-on: windows-latest
    strategy:
      matrix:
        configuration: [ Debug, Release ]
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: 6.0.x
      - run: dotnet restore
      - run: dotnet build -c ${{ matrix.configuration }} --no-restore
      - uses: actions/upload-artifact@v4
        with:
          name: bettergive-${{ matrix.configuration }}
          path: bin/${{ matrix.configuration }}/net6.0/bettergive.dll
```

### 9.5 添加配置/日志

- 可扩展支持通过配置文件自定义权限节点、默认词缀、禁用某些目标关键字等。
- 重要发放事件（例如向全体玩家发放）写入 `TShock.Log.Info`，便于审计。

---

## 10. 版本与变更记录

| 版本 | 变更点 |
| --- | --- |
| 3.0.1.0 | 当前版本：支持 `/wgive`、`wgbox` 两个命令；支持 `*`/`all`/`@a`/`me`、多目标、`!` 排除、物品名带空格、可选词缀。 |

---

## 11. 快速索引

- **主类**：[EnhancedWildcardGivePlugin](file:///workspace/bettergive.cs#L11-L305)
- **命令注册**：[Initialize](file:///workspace/bettergive.cs#L25-L29)
- **命令卸载**：[Dispose](file:///workspace/bettergive.cs#L31-L40)
- **/wgive 实现**：[WGiveCommand](file:///workspace/bettergive.cs#L42-L77)
- **/wgbox 实现**：[WGBoxCommand](file:///workspace/bettergive.cs#L79-L144)
- **物品解析**：[TryResolveSingleItem](file:///workspace/bettergive.cs#L152-L178)
- **目标解析**：[TryResolveTargets](file:///workspace/bettergive.cs#L180-L242)、[ResolveTargetToken](file:///workspace/bettergive.cs#L244-L288)
- **发放逻辑**：[TryGiveItem](file:///workspace/bettergive.cs#L290-L304)
- **数量解析**：[TryParsePositiveInt](file:///workspace/bettergive.cs#L146-L150)
