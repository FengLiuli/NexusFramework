---
task_id: T004
title: "Luban AI 工具链接入"
status: 已完成
complexity: P3
type: 基础设施
created: 2026-09-14
updated: 2026-09-14
---

# 任务：Luban AI 工具链接入

## 📋 任务描述

**原始需求**：Luban 官方新增了 AI Native 支持（Skills / schema-json / `--errorFormat json` /
`Luban.Agent` CLI / `Luban.Mcp`），按官方指导把相关能力补充到本项目。

**AI 理解**：
- 本项目只用到了 Luban 的生成功能，且 `Tools/` 目录此前不存在（`gen.bat` 引用的工具链缺失）
- 官方 5.0+ 的四类 AI 能力可让 Agent 自助完成「加表 → 校验 → 查 schema → 生成」
- 官方文档站（datable.cn）在当前网络不可达，改以本地官方仓库源码为权威依据

## 🎯 任务类型

基础设施

## 📂 受影响文档层

- [x] 需求：创建 `docs/requirements/R005-luban-ai-toolchain.md`
- [x] 设计：创建 `docs/design/D004-luban-ai-toolchain.md`（parent: D003）
- [x] 说明：创建 `Tools/README.md`
- [x] 验证：全部阶段实测通过（见下方拆分计划的勾选项）
- [ ] 进度：追加到 `docs/progress/`（待补）

## 📁 受影响文件

- `Tools/{Luban,Luban.Agent,Luban.Mcp}/`（创建：构建产物，提交进仓库）
- `Tools/build-luban.bat`（创建：构建脚本，纯 ASCII）
- `Tools/README.md`、`Tools/LUBAN_VERSION.txt`（创建）
- `.mcp.json`（创建：project scope 注册 `luban` MCP）
- `.claude/skills/luban-{add-table,excel-fill,generate-debug,runtime-load,schema-design,validator}/SKILL.md`（创建）
- `.gitattributes`（修改：追加 `/Tools/** -text -diff`）
- `docs/requirements/R005-luban-ai-toolchain.md`（创建）
- `docs/design/D004-luban-ai-toolchain.md`（创建）

## 📊 拆分计划

### A. 构建产物 ✅

- [x] 编写 `Tools/build-luban.bat`（前置检查 → 三项目构建 → Templates 断言 → 删 pdb → 版本戳 → 冒烟自检）
- [x] 构建成功，三个目录共 310 个文件 / 约 19 MB
- [x] `dotnet Tools/Luban.Agent/Luban.Agent.dll capabilities` → 合法 JSON，退出码 0
- [x] `Tools/Luban/Templates/cs-simple-json/` 存在

**踩坑**：`.bat` 必须纯 ASCII —— cmd.exe 按 OEM 代码页（936）读取，中文注释会被解码成乱码并破坏解析。
另：`git log --format=%ci` 里的 `%c` 会被 cmd 当作变量展开，改用 `git show -s --format=%cI HEAD`。

### B. 生成不回归验证 ✅

- [x] 留档基线，以 `gen.bat` 的完全相同参数调新工具链重新生成
- [x] `Assets/Demo/Emberheart/GAS/TableClass/`（C#）与基线**逐字节一致**
- [x] `Assets/DataGenerated/Luban/Json/GAS/` 与**工作区**版本逐字节一致
- [x] 文件清单（含 91 / 10 个 `.meta`）逐条不变 → `cleanUpOutputDir` 不误删 `.meta`

**结论**：生成行为无回归。JSON 与 `HEAD` 不同属**内容演进**（工作区 JSON 早于某次 Excel 修改，且未提交）。

### C. Agent CLI / schema-json / 错误通道 ✅

- [x] `Luban.Agent validate -t all` → `{"validated": true}`，退出码 0
- [x] `Luban.Agent list-tables -t client` → 10 张表，含 `source{file}` 溯源
- [x] 主 CLI `-c schema-json` → SchemaJson v2，10 tables / 79 beans
- [x] `--errorFormat json` 错误走 **stderr**，`location` 可定位到单元格
      （实测：`Sheet:Sheet1 字段:Level 位置:[E4] NOT_A_NUMBER`）

### D. 技能适配 ✅

- [x] 6 个官方 skill 复制到 `.claude/skills/luban-*/`
- [x] frontmatter 与上游**逐字一致**（逐个脚本比对通过）
- [x] 每个文件追加上游锚点 + 「本项目适配（NexusFramework）」段
- [x] `.gitignore` 白名单生效，文件可被 git 跟踪

### E. MCP 注册 ✅

- [x] 裸协议探针验证握手：`Luban.Mcp v5.1.0`，6 个工具
      （`list_tables` / `get_schema` / `describe` / `validate` / `generate` / `search_docs`）
- [x] `claude mcp add --scope local` 注册并激活（project 作用域审批在桌面端走不通，详见 D004）
- [x] **会话内实测**全部工具：`list_tables` 返回本项目 **10 张表**、`describe` 返回 `exgas.Tbasc`
      完整结构、`validate` 通过、`get_schema` 返回 SchemaJson v2 过滤结果；
      `search_docs` 如设计优雅降级（`ok:false`）
- [x] env 生效，未回退到失效的相对路径

**注意**：MCP 工具名是 **snake_case**（`list_tables`），不是源码里的 C# 方法名（`ListTables`）。

### F. 配置与文档 ✅

- [x] `.gitattributes` 追加 `/Tools/** -text -diff`
- [x] `Tools/README.md`（含「为何必须显式 env」的推导）
- [x] `R005` / `D004` / `T004`

## ✅ 验收结果

| # | 验收项 | 结果 |
|---|--------|------|
| 1 | 工具链无配置可直跑 | ✅ `capabilities` 退出码 0 |
| 2 | 生成不回归（逐字节） | ✅ C# 逐字节一致，文件清单不变 |
| 3 | 机器可读诊断含单元格定位 | ✅ `位置:[E4]` + `fieldPath` |
| 4 | schema-json 覆盖全部 10 张表 | ✅ v2，含 `source{file}` |
| 5 | MCP `list_tables` 返回 10 张表 | ✅ |
| 6 | 6 个技能可触发 | ✅ 已出现在可用技能列表 |

## 🔧 后续可选项（未做）

1. **修复 `GASSettingAsset.RunGenBat()`**：`RedirectStandardOutput` 未消费、`ConfigProjectPath`
   默认值 `Config/exgas_config` 与实际 `Assets/Demo/Table` 失配（该菜单当前找不到 gen.bat）。
   建议绕过 `gen.bat` 直调 `Tools/Luban/Luban.dll`，顺带解决 `pause` 阻塞与无法传 `--errorFormat json`。
2. **引入 `luban-doc`**：让 MCP `search_docs` 可用。

## 关联

- 需求：[R005 Luban AI 工具链接入](../requirements/R005-luban-ai-toolchain.md)
- 设计：[D004 Luban AI 工具链](../design/D004-luban-ai-toolchain.md)
- 父设计：[D003 Luban 配置桥接层](../design/D003-luban-config-bridge.md)
