---
name: luban-schema-design
description: Designs Luban schema (beans, enums, polymorphism, collections, groups). Use when modeling config structures, inheritance, nested beans, or choosing Excel vs XML schema.
---

<!-- upstream: focus-creative-games/luban @ v5.1.0 | ai/skills/luban-schema-design/SKILL.md | 本地适配见文末 -->

# Luban: Schema 设计

## 原则

- 程序维护 Schema；策划填 Data。
- 复杂 GamePlay（技能/行为树）优先 OOP 继承/多态，而不是塞字符串。
- 客户端敏感字段用 `s` group，不要泄漏到 `c`。

## 选型

| 需求 | 建议 |
|------|------|
| 扁平行表 | Excel + `read_schema_from_file` |
| 多模块/复用 bean | XML `Defines/*.xml` 或 `__beans__` |
| 多态配置 | 抽象 bean + 子类；Excel 填类型名/别名 |
| 一对多嵌套 | `list,Bean` / 多行 / sep 紧凑格式 |

## 类型要点

- 容器：`list,T` / `map,K,V`；**元素不可写 `list,int?`**
- 可空：`T?`；多态非空必须给具体子类
- 引用：`int#ref=module.TbX`
- 字段名建议 `snake_case`，生成时按语言转风格

## 检查清单

1. 主键与 `mode`（map/list/one）是否匹配
2. group 是否覆盖 client/server 需求
3. 多态子类是否都已定义且可区分
4. 用 `-c schema-json` 或 MCP `GetSchema` 复核结构

## 参考

- 类型系统、XML Schema、多态、分组 targets

## 本项目适配（NexusFramework）

**本项目既有模式（改动前先读这里）**

- 公共 bean 定义在 `Defines/builtin.xml`：`vector2/vector3/vector4`、`AttributeInSet`、`GrantedAbility` 等，
  均为 `valueType="1"` 的 CSV 紧凑 bean（`sep=","` 或 `sep=";"`）。
- **GAS 配置大量使用继承 + 多态**，这是本项目的核心模式：

  | 基类 | 用途 | 生成到 |
  |------|------|--------|
  | `AbilityLogicBase` | 技能逻辑（如 `ALApplyEffect`、`ALMoveTo`） | `cfg.AbilityLogicBase` |
  | `GameplayCueBase` | 表现（Cue） | `cfg.GameplayCueBase` |
  | `ModMagnitudeCalculationBase` | 属性计算（MMC） | `cfg.ModMagnitudeCalculationBase` |
  | `AbilityTaskBase` | 技能任务 | `cfg.AbilityTaskBase` |

  Excel 里填具体子类名，运行时由 `CueHelper` / `GasMmcHelper` / `AbilityLogicFactory`
  反射映射回类型并构造对应 `XParam`。
- 表定义：`Datas/#<module>.<name>.xlsx` 的 `#` 前缀 = 批量注册为表；
  `__tables__.xlsx` 当前**没有**手写表行（其 `input` 列是一条通配规则）。
- 复核结构用 `-c schema-json`（输出 `SchemaJson` v2，含 `tables`/`beans`/`enums` 与 `source{file}` 溯源），
  或 MCP `GetSchema`。
- 分组语义：本项目 `luban.conf` 里 `c`/`s`/`e` 三个 group 均为 `default`；`gen.bat` 走 `-t client`，因此服务端 `s` 字段不进客户端产物。
