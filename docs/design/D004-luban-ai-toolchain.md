---
layer: design
status: draft
task: T004
created: 2026-09-14
updated: 2026-09-14
parent: D003
---

# 设计：Luban AI 工具链

## 方案概述

从 Luban 官方源码（`D:\Work\CSharp\luban` @ v5.1.0）构建三个自包含产物到 `Tools/`，随仓库提交；
把官方 6 个 Agent Skill 适配进 `.claude/skills/`；并注册 Luban MCP Server
（仓库保留 `.mcp.json`，实际以 `--scope local` 激活，理由见下）。

本设计是 [D003 Luban 配置桥接层](D003-luban-config-bridge.md) 的**工具层延伸**：
D003 描述「Luban 产物如何进入 GAS 运行时」，本设计描述「Agent 如何稳定地操作 Luban 本身」。

```
Agent / Claude Code
    │
    ├── Skill（.claude/skills/luban-*）         ← 流程知识
    │
    ├── MCP（.mcp.json → Luban.Mcp）            ← 结构化工具
    │       ├── list_tables / get_schema / describe / validate → Luban.Agent CLI
    │       └── generate                                       → 主 Luban CLI
    │
    └── 直调 CLI（Tools/Luban/Luban.dll）        ← 生成与诊断
            │
            ▼
    Assets/Demo/Emberheart/GAS/TableClass/  +  Assets/DataGenerated/Luban/Json/GAS/
            │
            ▼  Unity 菜单 NF.GAS/Generate/LubanConfigLoader
    LubanConfigLoader.cs  →  ConfigModel  →  GAS 运行时（见 D003）
```

## 关键决策：显式环境变量，而非相对路径回退

### 问题

`Luban.Mcp` 的 `LubanTools` 按以下顺序定位依赖 DLL：

1. 工具参数（如 `agentDll`）
2. 环境变量（`LUBAN_AGENT_DLL` / `LUBAN_DLL` / `LUBAN_DOC`）
3. 相对 `AppContext.BaseDirectory` 的回退：
   `../../../../Luban.Agent/bin/Release/net8.0/Luban.Agent.dll`

### 推导

设产物位于 `Tools/Luban.Mcp/`，则 `BaseDirectory` 即该目录。四层 `..` 依次为：

```
Tools\  →  NexusFramework\  →  Work\Unity\  →  Work\
```

最终解析为 `D:\Work\Luban.Agent\bin\Release\net8.0\Luban.Agent.dll` —— **不存在**。
`LUBAN_DLL`（同样 4 层）、`LUBAN_DOC`（6 层）同理。

该公式是为上游 `luban_examples/Tools/` 的目录形状定制的：只有当产物位于源码构建树
（`src/Luban.Mcp/bin/Release/net8.0/`，上溯 4 层到 `src/`）时才偶然命中。

### 选择

| 方案 | 评估 |
|------|------|
| **扁平目录 + 显式 env（选中）** | 产物自包含、与上游构建输出形状一致；env 全量覆盖，定位确定 |
| 复刻上游相对路径形状 | 需要把 `bin/Release/net8.0` 嵌套进去，反而使 4 层 `..` 多退一层；且上游用 `dotnet build -o` 产出时该公式本身也不成立 |
| 用 `${CLAUDE_PROJECT_DIR}` 展开 | project-scope 下展开时序有已知缺陷（可能展开为空或原样透传），不采用 |

失败模式是**明确的**：env 缺失时报 `Luban.Agent.dll not found. Set env LUBAN_AGENT_DLL.`，不会静默出错。

## 关键决策二：MCP 用 `--scope local`

`.mcp.json`（project 作用域）已写入仓库，但 **project 作用域的 server 需要交互式审批**
（`⏸ Pending approval`）。实机验证结论：

| 尝试 | 结果 |
|------|------|
| `claude mcp get luban` | `⏸ Pending approval (run claude to approve)` |
| 手工写 `~/.claude.json` 的 `enabledMcpjsonServers: ["luban"]` | **无效**，状态不变 |
| `claude mcp remove` 后重 `add` / `add-json` | 仍为 Pending |
| Claude Code 桌面端 | **不弹出审批窗口** |
| **`claude mcp add --scope local`** | ✅ 立即 `✓ Connected` |

结论：在本环境的桌面端下，project 审批这条路**实际走不通**，采用 `--scope local`。

代价：**不随仓库共享**，其他克隆者需自行执行一次注册命令（已写入 `Tools/README.md`）。
`.mcp.json` 保留，以便将来桌面端支持审批后切回共享的 project 作用域。

## 构建产物完整性

两个必须保留的细节（均已实测核对）：

| 项 | 原因 |
|----|------|
| `Templates/` 目录 | `TemplateManager.Init()` 用 `Path.GetDirectoryName(AppContext.BaseDirectory) + "/Templates"` 定位，缺失则无法生成任何代码。`dotnet build` 已自动带入 28 个模板（含本项目所需的 `cs-simple-json`），构建脚本只做完整性断言 |
| `runtimes/` 目录 | 仅含 `win`，为 Excel 读取的多字节编码（`CodePagesEncodingProvider`）依赖 |

`.pdb` 由构建脚本删除：`.gitignore` 的 `*.pdb` 规则会挡住它们，且这批 DLL 作为 CLI 使用、无附加调试需求。

## 技能适配结构：上游锚点 + 适配段

官方 skill 的 `ai/README.md` 写明「可复制到 `.cursor/skills/`」。实测其正文**不含任何 Cursor 私有机制**
（无 `globs`、`alwaysApply` 等字段），是通用格式，可直接复制。

适配采用**双层结构**，目的是让上游升级可追溯：

```
---
name / description        ← 与上游逐字一致（改动会改变触发语义）
---
<!-- upstream: focus-creative-games/luban @ v5.1.0 | ai/skills/<name>/SKILL.md -->
<上游正文，一字不改>
## 本项目适配（NexusFramework）   ← 追加段
<本项目路径、命令、约定>
```

升级时执行 `git -C D:\Work\CSharp\luban diff v5.1.0..vX -- ai/skills/` 得到精确 diff，
替换上游正文、保留适配段。

适配段写入的公共事实见各 skill 文件；核心是三条本项目特有约定：

1. **配置工程在 `Assets/Demo/Table/`**，表以 `Datas/#<module>.<name>.xlsx` 形式**批量注册**
   （文件名 `#` 前缀即代表批量注册；`__tables__.xlsx` 当前没有手写表行，其 `input` 列是一条通配规则）。
2. **`gen.bat` 含 `pause`，自动化一律绕开**，直调 `Tools/Luban/Luban.dll`。
3. **生成后必须再跑 Unity 菜单 `NF.GAS/Generate/LubanConfigLoader`**，否则桥接层不更新。

## `search_docs` 的取舍

本机无 `luban-doc` 仓库。`ResolveDocsRoot` 返回 null 时，工具返回
`{"ok": false, "message": "Docs root not found..."}` —— **不抛异常、不影响连接**。

决策：**不设置 `LUBAN_DOC`**（省去 1–2 MB 的额外仓库与维护成本），
在 `Tools/README.md` 与相关 skill 中显式声明该工具不可用。

## 输出目录清理行为（实测结论）

`LocalFileSaver.BeforeSave` 默认 `cleanUpOutputDir=true`（键名
`outputSaver.<TargetName>.cleanUpOutputDir`，见 `BuiltinOptionNames`）。

**实测**：重新生成后 `TableClass/`（91 个 `.meta`）与 `Json GAS/`（10 个 `.meta`）的
**文件清单逐条不变**。该选项只删除「上次生成过、这次不在清单里」的文件，
不触及 Unity 的 `.meta` 与手写文件。对比 D003 之前的担忧，此处结论是**无需干预**。

如需保留陈旧产物，可加 `-x outputSaver.json.cleanUpOutputDir=false`。

## 回归验证结论

以 `gen.bat` 的完全相同参数调用新工具链重新生成：

| 产物 | 结果 |
|------|------|
| `Assets/Demo/Emberheart/GAS/TableClass/`（C#） | **逐字节一致** |
| `Assets/DataGenerated/Luban/Json/GAS/`（JSON） | 与**工作区**版本逐字节一致 |

JSON 与 `HEAD` 提交版本不同，但这是**内容演进**而非格式回归：工作区中的 JSON 早于某次 Excel 修改
（`#exgas.asc.xlsx` 等源文件在 `97f35ba` 更新，而 JSON 停在上一个提交 `9626112`），
且工作区版本不带 BOM 而 `HEAD` 版本带 —— 说明工作区早已被重新生成过，只是未提交。

## 已知缺陷（本次不修，另开）

`GASSettingAsset.RunGenBat()` 存在两个既有缺陷：

1. `RedirectStandardOutput = true` 但**从不读取** stdout（日志量大时管道可能阻塞）；
   `RedirectStandardError` 未开启，错误信息无法结构化回显。
2. `ConfigProjectPath` 默认值 `Config/exgas_config` 与实际配置工程 `Assets/Demo/Table` **失配**，
   该菜单当前找不到 `gen.bat`。

二者都与本次变更无关，且修复涉及行为变更（绕过 `gen.bat` 直调 DLL），故**仅记录**。
修复方案见 `T004` 的「后续可选项」。

## 各子系统变更

| 子系统 | 文件 | 变更 |
|--------|------|------|
| 工具链产物 | `Tools/{Luban,Luban.Agent,Luban.Mcp}/` | **新增**（构建产物，提交进仓库） |
| 构建脚本 | `Tools/build-luban.bat` | **新增** |
| 说明 | `Tools/README.md`、`Tools/LUBAN_VERSION.txt` | **新增** |
| MCP | `.mcp.json` + local 作用域注册 | **新增**（`--scope local` 生效，`.mcp.json` 保留备用） |
| 技能 | `.claude/skills/luban-*/SKILL.md` | **新增** 6 个 |
| 行尾策略 | `.gitattributes` | **+1 行**：`/Tools/** -text -diff` |
| 文档 | `docs/{requirements/R005,design/D004,tasks/T004}-luban-ai-toolchain.md` | **新增** |

**明确不改**：`Assets/Demo/Table/gen.bat`（其 `Tools/Luban/Luban.dll` 引用与新布局天然吻合）、
`LubanConfigLoaderGenerator.cs`、Luban 上游源码。

## 风险与权衡

| 风险 | 影响 | 缓解 |
|------|------|------|
| Luban 升级改变 `cfg.*` 生成面 | `LubanConfigLoaderGenerator.cs`（约 1090 行）强依赖这些类型，可能编译失败或产出错误桥接代码 | 锁定 v5.1.0 并写入 `LUBAN_VERSION.txt`；升级走 `Tools/README.md` 的完整回归流程 |
| 产物体积约 19 MB | 仓库增大 | 删 `.pdb` 已省约 14 MB；产物自包含、可随时重建 |
| `.mcp.json` 硬编码绝对路径 | 换机/换路径失效 | README 给出重注册命令；env 缺失时错误明确 |
| MCP 用 local 作用域 | **不随仓库共享**，他人克隆后需自行注册 | 命令写入 `Tools/README.md`；`.mcp.json` 保留以便日后切回 project 共享 |
| `gen.bat` 的 `pause` | 自动化调用会挂起 | 所有自动化路径绕开它；已写入 skill 适配段与 README |
| 需要 .NET 8+ 运行时 | 他人机器需安装 | README 写明前置依赖 |

## 关联

- 需求：[R005 Luban AI 工具链接入](../requirements/R005-luban-ai-toolchain.md)
- 任务：[T004 Luban AI 工具链接入](../tasks/T004-luban-ai-toolchain.md)
- 父设计：[D003 Luban 配置桥接层](D003-luban-config-bridge.md)
