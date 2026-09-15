---
name: luban-add-table
description: Adds a new Luban config table (Excel + __tables__ registration + regenerate). Use when creating a new table, registering reward/item tables, or when the user asks to add a Luban table.
---

<!-- upstream: focus-creative-games/luban @ v5.1.0 | ai/skills/luban-add-table/SKILL.md | 本地适配见文末 -->

# Luban: 加一张表

## 规则

- Schema 是契约：先登记表，再填数据。
- Excel sheet 仅当 A1 以 `##` 开头才被识别。
- `read_schema_from_file=true` 时不要在 `__beans__` 重复定义同名 bean。

## 步骤

1. 在 `dataDir`（常为 `Data`）新建 `xxx.xlsx`/`csv`：
   - `##var`：字段名
   - `##type`：类型（如 `int` / `string`）
   - 可选 `##group`：`c`/`s`
   - 其后为数据行
2. 在 `Data/__tables__.xlsx`（或 XML schema）增加一行：
   - `full_name`：如 `TbReward`
   - `value_type`：如 `Reward`
   - `read_schema_from_file`：`true`（从表头推断字段）时常用
   - `input`：相对 dataDir 的文件名
   - `index`：主键字段（map 表）
3. 运行项目 `gen.bat` / `gen.sh`，或：

```bash
dotnet Luban.dll --conf luban.conf -t all -d json -x outputDataDir=output
```

4. 确认输出出现新表；若有 `-c`，确认生成了对应类型。

## 常见失败

| 现象 | 处理 |
|------|------|
| 新文件无输出 | 是否写入 `__tables__` |
| sheet 被忽略 | A1 是否 `##` 开头 |
| bean 冲突 | 关闭重复的 `__beans__` 定义 |

## 参考

- 文档：加一张表、Excel 基础、Excel Schema
- 需要查现有表时用 MCP `ListTables` / `GetSchema`

## 本项目适配（NexusFramework）

本项目的表定义有两种方式，**先确认走哪一种**：

1. **批量注册（本项目当前用法）**：`Datas/#<module>.<name>.xlsx` —— 文件名以 `#` 开头即代表
   整个文件按"表清单"批量注册，表定义写在该 xlsx 自己的 `##var`/`##type` 表头里。
   `__tables__.xlsx` 的 `input` 列是一条 `文件列表` 通配规则，**当前没有任何手写表行**。
   新增表 = 新增一个 `Datas/#<module>.<name>.xlsx`，无需改 `__tables__.xlsx`。
2. **显式登记**：在 `__tables__.xlsx` 增加数据行（注意表头下有两行 `##` 注释行，
   数据从第 4 行开始），或写进 `Defines/*.xml`。

- 配置工程：`Assets/Demo/Table/`（`luban.conf` + `Datas/#*.xlsx` + `Defines/builtin.xml`）；`dataDir=Datas`，`topModule=cfg`，targets 为 `client`/`server`/`all`。
- 本项目的 sheet 表头形如：第 1 行 `##var`、第 2 行 `##type`、第 3 行 `##` 注释，数据从第 4 行起。
- 生成物：C# → `Assets/Demo/Emberheart/GAS/TableClass/`（`namespace cfg` / `cfg.exgas`，如 `Tables.Tbasc`、`exgas.ability`）；JSON → `Assets/DataGenerated/Luban/Json/GAS/`（`exgas_tbasc.json` 等）。
- **下游链路（关键）**：Luban 生成后必须**再**触发 Unity 菜单 `NF.GAS/Generate/LubanConfigLoader`，重新生成 `Assets/Demo/Emberheart/GAS/Generated/LubanConfigLoader.cs`。该文件强依赖 `cfg.Tables`、`cfg.AbilityLogicBase`、`cfg.GameplayCueBase`、`cfg.ModMagnitudeCalculationBase`、`cfg.TagRequirementData`。顺序不能反：改表 → Luban 生成 → GAS 生成器。
- 新增表后，除了跑 Luban，还要确认 `LubanConfigLoader.cs` 是否需要同步支持该表
  （见 `luban-runtime-load` 技能）。
