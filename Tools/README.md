# Tools — Luban AI 工具链

本目录存放从 [Luban](https://github.com/focus-creative-games/luban) 官方源码构建的**自包含**工具链产物，
用于把配置表的「加表 → 校验 → 查 schema → 生成」链路交给 AI Agent / IDE 完成。

产物**提交进仓库**，克隆后即可用，无需 .NET SDK 即可运行（运行需要 .NET 8+ 运行时）。

## 目录

| 目录 | 用途 | 入口 |
|------|------|------|
| `Luban/` | 主生成器 | `dotnet Tools/Luban/Luban.dll` |
| `Luban.Agent/` | Agent CLI（校验 / 查表 / describe） | `dotnet Tools/Luban.Agent/Luban.Agent.dll` |
| `Luban.Mcp/` | MCP Server（stdio） | 由 `.mcp.json` 自动启动 |

版本与来源记录在 [`LUBAN_VERSION.txt`](LUBAN_VERSION.txt)。

## 重建

```bat
Tools\build-luban.bat
```

默认从 `D:\Work\CSharp\luban` 构建，可传入自定义源码目录：

```bat
Tools\build-luban.bat D:\path\to\luban
```

前置依赖：.NET SDK 8+。

脚本会依次：构建三个项目 → 断言 `Templates/` 完整性 → 删除 `.pdb` → 写版本戳 → 跑 `capabilities` 冒烟自检。

> `build-luban.bat` **刻意保持纯 ASCII**。cmd.exe 按 OEM 代码页（本机 936/GBK）读取 .bat，
> 非 ASCII 注释会被解码成乱码并破坏解析。修改时请勿加入中文。

## 为什么三个路径必须显式配置（不要依赖相对回退）

`Luban.Mcp` 的 `LubanTools.ResolveAgentDll()` 回退链是：

```
AppContext.BaseDirectory + "../../../../Luban.Agent/bin/Release/net8.0/Luban.Agent.dll"
```

产物放在 `Tools/Luban.Mcp/` 时，四层 `..` 依次是 `Tools\` → `NexusFramework\` → `Work\Unity\` → `Work\`，
最终解析到 `D:\Work\Luban.Agent\bin\Release\net8.0\Luban.Agent.dll` —— **不存在**。
`LUBAN_DLL`（同样 4 层）与 `LUBAN_DOC`（6 层）同理失效。

该公式是为上游 `luban_examples/Tools/` 的目录形状定制的，与本仓库布局不兼容。
因此 `.mcp.json` 的 `env` **显式给出绝对路径**，不依赖回退。
（env 缺失时报错是明确的：`Luban.Agent.dll not found. Set env LUBAN_AGENT_DLL.`）

## MCP 注册

**克隆到新机器或更换仓库路径后**需重新注册（绝对路径会失效）：

```bash
claude mcp add --scope local luban \
  -e "LUBAN_AGENT_DLL=<repo>/Tools/Luban.Agent/Luban.Agent.dll" \
  -e "LUBAN_DLL=<repo>/Tools/Luban/Luban.dll" \
  -- dotnet "<repo>/Tools/Luban.Mcp/Luban.Mcp.dll"
```

> **为什么用 `--scope local` 而不是 `--scope project`？**
>
> 仓库里同时保留了 `.mcp.json`（project 作用域），但 **project 作用域的 server 需要交互式审批**才能启用
> （`claude mcp get luban` 会显示 `⏸ Pending approval`）。经验证，Claude Code 桌面端**不会弹出该审批窗口**，
> 且 `~/.claude.json` 里的 `enabledMcpjsonServers` 字段对本版本无效——这条路实际上走不通。
>
> `--scope local` 绕开这道门，直接写入本机配置，状态立即变为 `✓ Connected`。
> 代价：**它只存在于你的机器上，不随仓库共享**——其他人克隆后需要自己执行一次上面的命令。
>
> `.mcp.json` 保留是为了将来桌面端支持审批后可直接切回 project 作用域（共享给团队）。
> 两者同名时以 `local` 为准。

暴露的工具：`list_tables`、`get_schema`、`describe`、`validate`、`generate`、`search_docs`。

调用约定（以本仓库为例）：

| 工具 | 参数 |
|------|------|
| `list_tables` / `validate` | `conf: <repo>/Assets/Demo/Table/luban.conf`、`target: client`（或 `all`） |
| `get_schema` | 同上，可选 `nameFilter` |
| `describe` | 同上，加 `name: exgas.Tbasc`（表全名 / bean 名 / 枚举名） |
| `generate` | 传完整 CLI 参数；会**自动补** `--errorFormat json` |

### 关于 `search_docs`

**本仓库不可用**（未设置 `LUBAN_DOC`）。调用返回：

```json
{ "ok": false, "message": "Docs root not found. Set env LUBAN_DOC to luban-doc/docs or pass docsRoot." }
```

这是预期行为，不是故障。需要查 Luban 行为时，改读 `Tools/Luban/Templates/**.sbn`（模板即产出结构）
或用 `-c schema-json` 导出结构。

## 升级流程

Luban 升级会改变 `cfg.*` 的生成面，而 `Assets/NexusFramework.GAS/Editor/CodeGen/LubanConfigLoaderGenerator.cs`
强依赖这些类型。**升级必须走完整回归**：

1. 更新 `D:\Work\CSharp\luban` 源码到目标版本。
2. 重跑 `Tools\build-luban.bat`，确认 `LUBAN_VERSION.txt` 已更新。
3. 跑配置生成（见下节命令），`git diff` 检查 `Assets/Demo/Emberheart/GAS/TableClass/` 与
   `Assets/DataGenerated/Luban/Json/GAS/`。
4. Unity 编译；跑菜单 `NF.GAS/Generate/LubanConfigLoader`；比对
   `Assets/Demo/Emberheart/GAS/Generated/LubanConfigLoader.cs` 的 diff。
5. 跑一次 PlayMode 测试。

同步更新 `.claude/skills/luban-*/SKILL.md`：各文件头部有上游锚点
（`<!-- upstream: focus-creative-games/luban @ vX.Y.Z ... -->`）。用

```bash
git -C D:\Work\CSharp\luban diff v5.1.0..vX.Y.Z -- ai/skills/
```

得到上游 diff，把正文替换为新版，**保留文末的「本项目适配（NexusFramework）」段**。

## 配置生成命令

`Assets/Demo/Table/gen.bat` **末尾有 `pause`，仅供人工手动执行**。
Agent / MCP / 任何自动化请直接调 DLL：

```bash
dotnet Tools/Luban/Luban.dll -t client -c cs-simple-json -d json \
  --conf Assets/Demo/Table/luban.conf \
  -x outputCodeDir=Assets/Demo/Emberheart/GAS/TableClass \
  -x outputDataDir=Assets/DataGenerated/Luban/Json/GAS \
  -x l10n.provider=default \
  -x "l10n.textFile.path=*@<repo>/Assets/Demo/Table/Datas/l10n.json" \
  -x l10n.textFile.keyFieldName=key \
  -x l10n.textFile.languageFieldName=zh \
  -x l10n.convertTextKeyToValue=1
```

`l10n.textFile.path` 需要绝对路径。

生成成功后**还要**触发 Unity 菜单 `NF.GAS/Generate/LubanConfigLoader`，否则 GAS 桥接层不会更新。
