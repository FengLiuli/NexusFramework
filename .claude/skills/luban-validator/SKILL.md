---
name: luban-validator
description: Writes Luban field validators (ref, range, path, size, set, regex, non-default). Use when adding validation rules on types or Excel ##type cells.
---

<!-- upstream: focus-creative-games/luban @ v5.1.0 | ai/skills/luban-validator/SKILL.md | 本地适配见文末 -->

# Luban: 校验器

校验在生成期执行。发布建议 `--strict`。

## 常用写法

| 能力 | 示例 |
|------|------|
| 非默认 | `int!`、`int?!` |
| 引用 | `int#ref=item.TbItem` |
| 引用可跳过 0 | `int#ref=item.TbItem?` |
| 范围 | `int#range=[1,100]` |
| 路径 | `string#path=unity` + `-x pathValidator.rootDir=...` |
| 集合大小 | `(list#size=4),int` |
| 允许值 | `int#set=1;2;3` |
| 正则 | `string#regex=^[a-z]+$` |

写在 XML `type="..."` 或 Excel `##type`。

## 步骤

1. 确认被引用表全名（可用 schema-json / MCP）。
2. 改字段类型字符串，保留原有 group/注释。
3. `dotnet Luban.dll ... -f --strict --errorFormat json` 验证。

## 注意

- 容器约束加在容器上：`(list#size=n),T`
- 可空为 null 时多数引用类校验会跳过
- 不要用削弱校验的方式「修好」坏数据

## 本项目适配（NexusFramework）

**本项目现状**

- 当前表普遍使用裸类型（`int` / `string` / `(array#sep=;),int`），**尚未使用 `#ref=` / `#range=` 等校验器**。
  加校验器属于增强，注意它会立刻暴露既有数据中的问题，需同步修数据。
- 本项目的 group 全部为 `default`，`-t client` 生成时服务端字段不参与，**不要**指望用 group 来隔离校验。
- 验证命令（JSON 诊断走 stderr，含 `fieldPath` 与单元格位置）：

  ```bash
  dotnet Tools/Luban/Luban.dll -t client -c cs-simple-json -d json \
    --conf Assets/Demo/Table/luban.conf \
    -x outputCodeDir=Assets/Demo/Emberheart/GAS/TableClass \
    -x outputDataDir=Assets/DataGenerated/Luban/Json/GAS \
    -f --strict --errorFormat json 2>diag.json
  ```

  只做校验不落盘更快：`dotnet Tools/Luban.Agent/Luban.Agent.dll validate --conf Assets/Demo/Table/luban.conf -t all`
- 若使用了 `#path=` 校验器，需要额外配置 `-x pathValidator.rootDir=...`（本项目当前未配置）。
- 引用类校验（`#ref=`）的目标表全名形如 `exgas.Tbability`；可用
  `Luban.Agent list-tables` 或 MCP `ListTables` 确认后填写。
