---
layer: requirements
status: draft
task: T004
created: 2026-09-14
updated: 2026-09-14
---

# 需求：Luban AI 工具链接入

## 背景

Luban 自 **5.0.0** 起提供 AI Native 能力，面向 AI Agent / IDE 提供官方 Skills、机器可读的 schema 与报错、
`Luban.Agent` CLI，以及 MCP Server。

本项目目前只使用了 Luban 的**生成**能力：`Assets/Demo/Table/gen.bat` 调 `Tools/Luban/Luban.dll`
导出 C# 代码与 JSON 数据。但 `Tools/` 目录此前并不存在于仓库中，且上述 AI 能力完全未接入。

结果是：Agent 无法自助完成「加表 → 校验 → 查 schema → 生成」这条链路 —— 既不知道表结构，
也拿不到可定位到单元格的结构化报错。

## 目标

接入 Luban 官方 AI Native 能力，使配置表工作流可由 Agent 稳定驱动。

## 交付内容

| # | 交付 | 说明 |
|---|------|------|
| 1 | **工具链产物** | `Tools/{Luban,Luban.Agent,Luban.Mcp}/` 三个自包含目录，随仓库提交 |
| 2 | **官方 Skill** | 6 个官方 skill 适配到 `.claude/skills/luban-*/`，保留上游文本并附加本项目上下文 |
| 3 | **Luban MCP** | 项目级 `.mcp.json` 注册 `luban` server，暴露 6 个工具 |
| 4 | **文档** | 本需求、`D004` 设计、`T004` 任务、`Tools/README.md` |

## 验收标准

1. **工具链可直跑**：`dotnet Tools/Luban.Agent/Luban.Agent.dll capabilities` 在无额外配置下输出合法 JSON、退出码 0。
2. **生成不回归**：用 `gen.bat` 的相同参数调新工具链重新生成，`Assets/Demo/Emberheart/GAS/TableClass/`
   与 `Assets/DataGenerated/Luban/Json/GAS/` 与改造前**逐字节一致**；文件清单（含 `.meta`）不变。
3. **机器可读诊断**：`--errorFormat json` 在失败时向 **stderr** 输出 `DiagnosticReport`，
   含 `category` / `code` / `file` / `location`（可定位到单元格，如 `位置:[E4]`）/ `fieldPath`。
4. **schema 可导出**：`-c schema-json` 产出 SchemaJson v2 文档，覆盖本项目全部 10 张表，
   且每张表带 `source{file}` 溯源。
5. **MCP 可用**：`list_tables` 返回本项目全部 10 张表；`describe` 能取到单表结构；`validate` 复用 Agent 链路。
6. **技能可触发**：6 个 `luban-*` skill 出现在可用技能列表中。

## 非目标

- **不修改 Luban 上游源码**（只构建，不打补丁）。
- **不引入 `luban-doc` 仓库**（`search_docs` 工具在本仓库不可用是已知且可接受的）。
- **不改变既有生成格式与产物路径**。
- **不升级 Luban 版本**（锁定 v5.1.0；升级另开任务，见 `Tools/README.md` 升级流程）。
- **不改造 `GASSettingAsset.RunGenBat()`**（其既有缺陷仅记录，见 `D004`）。

## 关联

- 设计：[D004 Luban AI 工具链](../design/D004-luban-ai-toolchain.md)
- 任务：[T004 Luban AI 工具链接入](../tasks/T004-luban-ai-toolchain.md)
- 相关设计：[D003 Luban 配置桥接层](../design/D003-luban-config-bridge.md)（本需求是其工具层延伸）
