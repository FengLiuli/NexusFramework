---
name: luban-generate-debug
description: Diagnoses Luban generation and validation failures. Use when gen.bat fails, schema/data errors appear, or the user pastes Luban logs / --errorFormat json reports.
---

<!-- upstream: focus-creative-games/luban @ v5.1.0 | ai/skills/luban-generate-debug/SKILL.md | 本地适配见文末 -->

# Luban: 生成失败排查

## 优先动作

1. 用同一命令加 `--errorFormat json`，解析 `errors[]`。
2. 看 `category`：`schema` / `data` / `validation` / `codegen` / `cli`。
3. 有 `file` + `location` + `fieldPath` 时先修对应单元格/字段。

## 排查顺序

1. **CLI**：`--conf` 路径、`-t` 是否存在、`-c`/`-d` 是否匹配。
2. **Schema**：目标 group、表 value_type、继承关系。
3. **Data**：类型解析、枚举名、必填、分隔符。
4. **Validation**：`ref` / `range` / `path`（path 需 `pathValidator.rootDir`）。
5. **Codegen**：关键字冲突、非法标识符。

## 有用命令

```bash
# 只校验（推荐 Agent CLI）
dotnet Luban.Agent.dll validate --conf luban.conf -t all

# 或主 CLI
dotnet Luban.dll --conf luban.conf -t all -f --strict --errorFormat json -x outputSaver=null

# 导出 / 查询 schema
dotnet Luban.Agent.dll schema --conf luban.conf -t all
dotnet Luban.dll --conf luban.conf -t all -c schema-json -x outputCodeDir=./schema-out
```

## 常见坑

- 未登记 `__tables__`
- Sheet 无 `##` 头
- 引用 ID 不存在（`#ref=`）
- 输出目录被清掉了手写文件（目录选错）

## 原则

修好数据或按程序意图改 schema；禁止为通过生成而削弱校验（除非用户明确要求且说明风险）。

## 本项目适配（NexusFramework）

**本项目排查入口（按推荐顺序）**

```bash
# 1. 只校验，不落盘 —— 最快
dotnet Tools/Luban.Agent/Luban.Agent.dll validate --conf Assets/Demo/Table/luban.conf -t all

# 2. 需要定位到单元格时，用 JSON 诊断（JSON 走 stderr）
dotnet Tools/Luban/Luban.dll -t client -c cs-simple-json -d json \
  --conf Assets/Demo/Table/luban.conf \
  -x outputCodeDir=Assets/Demo/Emberheart/GAS/TableClass \
  -x outputDataDir=Assets/DataGenerated/Luban/Json/GAS \
  --errorFormat json 2>diag.json

# 3. 查结构
dotnet Tools/Luban.Agent/Luban.Agent.dll list-tables --conf Assets/Demo/Table/luban.conf -t client
dotnet Tools/Luban.Agent/Luban.Agent.dll schema      --conf Assets/Demo/Table/luban.conf -t client
```

**JSON 诊断实测样例**（`location` 可直接定位到单元格）：

```json
{
  "category": "data",
  "code": "error.data.parse_failed",
  "message": "NOT_A_NUMBER 不是 int 类型值",
  "file": ".../Datas/#exgas.asc.xlsx",
  "location": "Sheet:Sheet1 字段:Level 位置:[E4] NOT_A_NUMBER",
  "fieldPath": "{exgas.asc}.Level"
}
```

**本项目的坑**

- `gen.bat` 末尾有 `pause`，**别在自动化里调它**；直接调 `Tools/Luban/Luban.dll`。
- 生成的 C# 与 JSON 是两个独立 target（`-c cs-simple-json` / `-d json`），报错时先确认是哪一个失败。
- 输出目录默认清理旧文件（`outputSaver.json.cleanUpOutputDir`，默认 `true`）：
  只删"上次生成过、这次不在清单里"的文件，不会删 `.meta` 或手写文件。
  如需保留陈旧文件，加 `-x outputSaver.json.cleanUpOutputDir=false`。
- Luban 生成成功 ≠ 项目可用：还要跑 Unity 菜单 `NF.GAS/Generate/LubanConfigLoader`。
