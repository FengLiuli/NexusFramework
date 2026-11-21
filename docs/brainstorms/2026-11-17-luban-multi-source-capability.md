---
date: 2026-11-17
updated: 2026-11-17
topic: luban-multi-source-capability
status: verified
related:
  - docs/requirements/R006-data-pack-config.md
  - docs/requirements/R007-item-storage.md
  - docs/brainstorms/2026-08-01-content-pack-quadruple-kickoff.md
---

# Luban 多源数据能力边界验证

## Summary

为评估“多来源（核心包 / DLC / 第三方）都用 Luban 配置数据”是否可行，对项目内置的 Luban 5.1.0 做了一轮能力边界实测。
本文只记录 **Luban 本身能做什么、不能做什么**，不绑定任何尚未定稿的运行时设计（如 [D006](../design/D006-data-pack-interface-design.md)）。

核心结论：

1. **Luban 是“编译期 schema + 运行期数据注入”模型**：表集合、行类型、dataName 在生成时固定，运行时只有 `Func<string, JSONNode>` 这一个注入点。
2. **多源只能发生在数据层**：多个文件 / 目录 / 多 sheet 都可以合并成一张表；同名表的**结构**不能来自多个来源（除非用 `variant` 二选一）。
3. **数据文件被限制在主工程 `dataDir` 内**：`../` 与绝对路径都会被规范化后拼接，指向外部目录会找不到。
4. **`#` 前缀是“自动注册成独立表”的机制，与多源合并互斥**：分表文件带 `#` 会多出一张独立的表。
5. **字段类型里没有 `json`**：`-d json` 是导出格式；任意结构只能用 `string` / `text` / `map` / `bean` 表达。
6. **跨程序集的 Luban 生成类型不能共享**：同名类型会编译冲突，不同程序集的同名 `Type` 也不相等，`GetTable<TRow>` 按 Type 精确匹配会失败。
7. **多源真正的可行落点是“表级隔离 + 行级 JSON 合并”**：第三方新表走独立命名空间/程序集，核心已有表只接受行 JSON 合并。

---

## 0. 验证方法

| 项 | 内容 |
|---|---|
| 版本 | Luban `5.1.0+df34215d2e22035073d1af83d44e3ac0744dc3b3` |
| 入口 | [`Tools/Luban/Luban.dll`](../../Tools/README.md)（经 `Tools/Luban.Mcp` 调用 `validate` / `generate` / `list_tables`，工具链见 [D004](../design/D004-luban-ai-toolchain.md)） |
| 工程 | 复制 `Assets/Demo/Table` 的 conf / Defines / `__tables__` / `__beans__` 到系统临时目录，按需追加表 |
| 数据 | 用 `openpyxl` 生成 xlsx；每张表遵循 A 列标签、B 列起为字段的格式 |
| 清理 | 所有临时工程在验证后删除，仓库无残留 |

可复跑步骤：

```bash
# 1. 复制 conf 骨架
cp -r Assets/Demo/Table/{luban.conf,Defines,Datas/__tables__.xlsx} <tmp>/Tables/

# 2. 用 MCP 或直接调 DLL
dotnet Tools/Luban/Luban.dll --conf <tmp>/Tables/luban.conf -t all \
  -c cs-simple-json -d json \
  -x outputCodeDir=<tmp>/out/code -x outputDataDir=<tmp>/out/data \
  --errorFormat json
```

---

## 1. 构建期：数据来源合并

### 1.1 三种写法都成立

| 写法 | `input` 列 | 实测结果 |
|---|---|---|
| 逗号文件列表 | `items/item.weapon.xlsx,items/item.armor.xlsx` | 合并为一张表 |
| 目录 | `items` | 递归扫描目录及子目录下所有数据文件 |
| 单文件多 sheet | `items/item.multi.xlsx` | 所有带 `##var` 表头的 sheet 都被读取并合并 |

补充实测：

- 没有 `##var` 表头的 sheet（如“说明”页）会被忽略，不报错；
- 目录内的子目录会被递归读取，读取顺序不保证。

### 1.2 配置示例

`Datas/__tables__.xlsx` 数据区（**A 列是标签列，数据行必须从 B 列开始**）：

| full_name | value_type | read_schema_from_file | input | index | mode | group |
|---|---|---|---|---|---|---|
| `Tbitem` | `item` | `true` | `items` | `ID` | `map` | `c` |

```text
Datas/
├─ items/
│   ├─ item.weapon.xlsx
│   ├─ item.armor.xlsx
│   └─ consumable/
│       └─ item.potion.xlsx
└─ __tables__.xlsx
```

### 1.3 合并的硬约束

| 约束 | 实测错误码 | 说明 |
|---|---|---|
| 所有来源表头必须一致 | `error.data.load_table_file_fail` | 列名 / 类型（`##var`、`##type`）不一致即失败 |
| 主键合并后必须全局唯一 | `error.data.duplicate_key` | 报错会列出两份数据各自的来源文件 |
| 分表文件不能带 `#` 前缀 | 无错误，但多出一张表 | `#item.part2.xlsx` 会被注册成 `item.Tbpart2`，不进 `Tbitem` |
| `input` 不支持通配符 | `error.schema.input_not_found` | `item.*.xlsx` 不可用，用目录代替 |
| `input` 不支持 `@sheet` | `error.schema.input_not_found` | 多 sheet 本来就会全读，无需指定 |
| `input` 不能指向 `dataDir` 外 | `error.schema.input_not_found` | `../ThirdParty/x.xlsx` 被规范化为 `ThirdParty/x.xlsx`；绝对路径被拼成 `Datas/C:/...` |

### 1.4 `#` 前缀的实际含义

`Datas/#unit.xlsx` → 表 `Tbunit`；`Datas/#exgas.ability.xlsx` → 表 `exgas.Tbability`。
规则是：文件名去掉 `#` 与扩展名，按 `module.name` 拆分，name 部分加 `Tb` 前缀。

**子目录会参与命名**：`Datas/items/#item.extra.xlsx` → 表 `items.item.Tbextra`。
因此多源分表文件不要使用 `#`，否则会同时生成一张额外的独立表。

---

## 2. 构建期：表定义的多来源

### 2.1 `schemaFiles` 可以挂多份 `__tables__.xlsx`

```json
"schemaFiles": [
  {"fileName": "Defines", "type": ""},
  {"fileName": "Datas/__tables__.xlsx", "type": "table"},
  {"fileName": "../ThirdParty/__tables__.xlsx", "type": "table"}
]
```

实测：第三方定义文件可以在 `dataDir` 之外被加载，`list_tables` 的 `source.file` 会显示 `../ThirdParty/__tables__.xlsx`。
但该表的数据文件仍必须在主工程 `dataDir` 内（见 1.3）。

### 2.2 同名表的两种结果

| 写法 | 结果 |
|---|---|
| 两处定义同名表，均无 `variant` | `error.def.table.variant_fallback_duplicate` |
| 同一处定义同名表，分别带 `variant=v1` / `v2` | 通过；必须用 `--variant Tbitem=v1` 选择 |

不选择 variant 时：`error.def.table.variant_not_set`，提示 `--variant Tbitem=<name>` 或 `--variant default=<name>`。
选中后生成物只包含被选中的那套结构（实测 `v1` 生成 `damage` 列；`v2` 的定义为 `defense` 列，未单独生成验证）。

**variant 是“二选一”，不是数据叠加**，不能用于多源合并。

### 2.3 跨来源引用（ref）在构建期有效

第三方表定义 `int#ref=Tbunit` 引用主工程自动注册的表，`validate` 通过。
前提是两张表出现在**同一次** Luban 运行中。

### 2.4 输出过滤与分包

| 参数 | 行为 |
|---|---|
| `output` 列写 `thirdparty/tbthird` | 生成 `data/thirdparty/tbthird.json`，支持子目录 |
| `-o Tbunit` | 数据只输出 `tbunit.json`；`Tables.cs` 只包含 `Tbunit` 成员 |
| 同上，代码目录 | **仍全量生成所有表与 bean 的 `.cs`**，`-o` 不过滤代码 |

结论：`-o` 可用于“按表分包导出数据”，不能用于“按表分包导出代码”。

### 2.5 本版本没有 patch

在 `Luban.Schema.Builtin` 的表定义字段中未找到任何 patch 相关字段（`input` / `output` / `read_schema_from_file`
都能检索到，`patch` 不能）；`Luban.Core` 中只残留 patch 相关错误串。
因此跨来源覆盖/废除需要在 Luban 之外完成。

---

## 3. 类型系统边界

### 3.1 没有 `json` 字段类型

实测：`##type` 写 `json` → `error.def.field.invalid_type`。

源码依据：

- `src/Luban.Core/Types/` 只有 16 个类型：`TArray TBean TBool TByte TDateTime TDouble TEnum TFloat TInt TList TLong TMap TSet TShort TString TType`；
- `src/Luban.Core/Defs/DefAssembly.cs` 的类型分支只有 `bool / byte / short / int / long / bigint / float / double / string / text / time / datetime`，容器只有 `array / list / set / map`。

`-d json`、`-c cs-simple-json` 是**导出格式**，与字段类型无关。

### 3.2 “任意结构”的四种合法表达

| 需求 | 类型写法 | 生成结果 |
|---|---|---|
| 任意 JSON 文本 | `string` / `text` | `string`，运行期自行 `JSON.Parse` |
| 动态键值包 | `(map#sep=;),string,string` | `Dictionary<string,string>` |
| 固定嵌套结构 | `bean` | 强类型类 |
| 同构列表 | `(list#sep=;),string` | `List<string>` |

`text` 只是 `string` 加了本地化标签，底层仍是 `TString`。
map 的分隔符语法需按实际数据调整（实测 `a,1;b,2` 会被当成单个条目，说明条目内键值分隔需要另配）。

---

## 4. 运行时加载模型

### 4.1 唯一的注入点

```csharp
// 生成物：cfg.Tables
public Tables(System.Func<string, JSONNode> loader)
{
    Tbunit = new Tbunit(loader("tbunit"));
    ...
    ResolveRef();
}
```

- 表成员、行类型、dataName 都是生成期固定的；
- 运行时只能替换 `loader`，不能新增表成员；
- `loader` 的 key 是 dataName（默认 `<module>_<name>` 小写，如 `tbunit`），不是文件路径；
- `ResolveRef()` 在构造函数末尾执行，引用解析依赖**同一份已合并的数据**。

### 4.2 多源合并 loader

```csharp
// 每个 dataName 第一次被请求时，按优先级合并所有来源
public static void LoadInto(IConfigLoader target, IReadOnlyList<IConfigSource> sources)
{
    target.LoadTables(dataName =>
    {
        var rows = new List<JSONNode>();
        var index = new Dictionary<string, int>();   // 主键 -> 下标

        foreach (var src in sources)
        {
            var incoming = src.GetRows(dataName);     // 没有该表返回 null
            if (incoming == null || !incoming.IsArray) continue;

            foreach (var row in incoming.Children)
            {
                var key = row[PrimaryKeyOf(dataName)]?.Value;
                if (key != null && index.TryGetValue(key, out var at))
                    rows[at] = row;                   // 同主键覆盖
                else
                {
                    index[key] = rows.Count;
                    rows.Add(row);
                }
            }
        }

        return ToJsonArray(rows);                     // 空表返回 []
    });
}
```

四条契约：

| 契约 | 依据 |
|---|---|
| 缺表返回 `[]`，不能返回 `null` | 生成的表构造直接访问 `_buf.Count` / `_buf.Children`（代码阅读，未单独跑空值实验） |
| 合并必须发生在 `new Tables(...)` 之前 | `ResolveRef()` 在构造末尾执行 |
| dataName 必须与生成代码里的字面量一致 | `loader("tbunit")` |
| `Tables` 构造后只读，热更需要重建 | 现有 `LoadTables` 实现里有 `if (_tables != null) return;` |

### 4.3 跨包 ref 是静默 null

实测生成的引用解析代码：

```csharp
public void ResolveRef(Tables tables)
{
    BId_Ref = tables.Tbtb.GetOrDefault(BId);
}
```

用的是 `GetOrDefault`，因此当被引用表在当前的 `Tables` 里为空时：

- 不报错；
- `_Ref` 为 `null`；
- 故障推迟到业务层访问引用字段时。

这决定了“每个来源各 new 一个 Tables”的边界：**第三方表无法引用另一个来源的表**，除非核心另外提供运行期查询入口。

---

## 5. 多来源类型隔离方案评估

### 5.1 方案对比

| 维度 | 形态 1：每包独立生成 + 独立 Tables | 形态 2：每包只交 JSON，核心统一构造 | 形态 3：混合 |
|---|---|---|---|
| 编译期冲突 | 无（`topModule` / `manager` 改独立命名空间） | 无（只有 JSON） | 无 |
| 类型唯一性 | ❌ 跨程序集同名 `Type` 不相等 | ✅ | 按表分流 |
| 跨包 ref | ❌ `GetOrDefault` 静默 null | ✅ 合并后解析 | 核心表 ✅ / 外来表 ❌ |
| 核心强类型访问 | ❌ 只能是 `object` / 接口 | ✅ `cfg.Tbitem` | 按表分流 |
| 第三方新增表 | ✅ | ❌（没有行类型） | ✅ |
| 第三方扩核心表 | ❌ | ✅ | ✅ |
| 代码体积 | 每包一份全量 schema 代码 | 无 | 按需 |
| schema 变更 | 各自重建 | 主工程重建 | 按表重建 |

形态 1 的隔离手段（全部来自实测/语言规则）：

- `topModule` 改成第三方命名空间（如 `cfgvendor`），避免 `cfg.*` 撞名；
- `manager` 改成 `VendorTables`，避免第二个 `cfg.Tables`；
- 生成物放独立程序集，主工程不引用它的 `cfg`；
- 行类型通过核心定义的接口暴露（生成代码是 `partial class`，第三方可在**自己的程序集内**补接口实现）。

### 5.2 为什么“共享 Luban 类型”不可行

| 问题 | 机制 |
|---|---|
| 同一程序集两份生成物 | 编译期重复定义（CS0101：`cfg.Tables`、`cfg.Tbitem` 各两份） |
| 不同程序集、同命名空间 | 引用歧义（CS0433：类型同时存在于两个程序集） |
| 不同程序集、不同命名空间 | 能编译，但 `typeof(cfg.item) != typeof(cfgvendor.item)`；D006 式 `GetTable<TRow>` 精确类型匹配会失败 |
| 跨程序集补接口 | C# `partial` 只能在同一程序集内合并，主工程无法给第三方的行类型补实现 |
| 全量 `Tables` | 即使只用一个包，构造 `Tables` 也会请求所有表；缺表必须给 `[]` |

### 5.3 Unity 平台限制

| 路径 | 可行性 | 原因 |
|---|---|---|
| 运行期加载第三方程序集（`Assembly.Load(byte[])`） | ❌ | IL2CPP 不支持运行期 JIT；移动端平台限制（平台常识，未在本仓库实测） |
| 运行期动态生成行类型（`Reflection.Emit` / Roslyn） | ❌ | 同上 |
| 运行期新表走通用 JSON 视图 | ✅ | 自建 `DynamicTable`（`Dictionary<string, object>`），失去强类型与校验 |
| 运行期新表走预留 blob 列 | ✅ | 主 schema 里放 `string` / `map` 列，第三方塞整表 JSON |

---

## 6. 能力边界总表

| 能力 | 结论 |
|---|---|
| 多文件 / 目录 / 多 sheet 合并为一张表 | ✅ |
| 多份 `__tables__.xlsx` 作为 schema 来源（可在 dataDir 外） | ✅ |
| 跨来源 `ref` 构建期校验 | ✅ |
| `output` 写子目录 | ✅ |
| `-o` 按表导出数据 | ✅（代码不过滤） |
| 同名表多定义 | ✅ 仅 `variant`，且单选 |
| 同名表普通定义两处 | ❌ `variant_fallback_duplicate` |
| 数据文件指向 dataDir 外 | ❌ |
| `input` 通配符 | ❌ |
| `input` `@sheet` | ❌ |
| patch / 增量覆盖 | ❌（本版本无该列） |
| 字段类型 `json` | ❌ |
| 跨工程类型互操作 | ❌ |
| 运行期新增表（强类型） | ❌ |
| 运行期多源合并数据 | ✅ 需自己实现 loader 合并 |

---

## 7. 未验证 / 待验证

| # | 问题 | 建议验证方式 |
|---|---|---|
| V1 | 两个独立 Luban 工程用同一份 schema 导出的 JSON，是否逐字段一致 | 分别 `gen` 到两个目录，做 JSON 结构 diff |
| V2 | 字符串主键在 `index` / `ref` / `variant` 下是否都成立 | 把 `index` 改为 `string` 键，跑 `validate` |
| V3 | 空表返回 `null` 的实际异常类型 | 在 Unity 里用一个返回 `null` 的 loader 构造 `Tables` |
| V4 | 多源合并在大表（10 万行级）下的耗时与内存 | 生成大表 + 分层合并，计时 |
| V5 | IL2CPP 下第三方程序集的加载限制 | 真机包实测 `Assembly.Load` / 反射行为 |
| V6 | `variant` 与 `input` 目录混用时，非选中 variant 的文件是否仍被扫描 | 目录内放两套 variant 数据，检查加载结果 |

---

## 8. 对后续设计的影响（2026-11-17 已收口）

本文的实测结论已被需求文档吸收，对应关系如下：

| 本文结论 | 落点 |
|---|---|
| 多源只能发生在数据层；无 patch | 覆盖/废除由 `TableModel` 合并层实现（R006 RP23–RP27） |
| 主键合并后必须全局唯一；`int` 在多源下不可治理 | 已定为字符串全局键 `<packId>:<localKey>`（R006 RP9a）；**V2 成为开工第一验证项**（R006 D5） |
| 跨来源 `ref` 只能在同一份合并数据上解析 | Config **不构造 `cfg.Tables`**（R006 RP35）；跨表引用由 `ITableView` 自行解析并记录缺失警告（RP17） |
| 跨程序集行类型不可互操作；IL2CPP 无法运行期加载 | 行类型只由本体提供；内容包分三条通道（数据 / 编译期 / 运行期）；运行期代码包仅 Mono（内容包模型 §三） |
| 第三方交付物只能是行 JSON 或独立程序集 | 所有内容包都带程序集；"程序包"指提供 NF 组件（服务实现）的部分；契约包不含行类型（内容包模型 C3/C4/C5） |
| `#` 前缀、`dataDir` 限制、`-o` 不过滤代码 | 写入模板规范与 schema 校验脚本（R006 RP13） |
| 字段类型无 `json` | 词条等任意结构用 `list + bean` 表达（R007 RI12） |

三条仍需保留的提醒：

1. **`cfg.Tables` 的 loader 契约（缺表返回 `[]`）与 `IDataPackDataSource`（缺表返回 `null`）是两套语义**，不得混用；
2. **生成的行类型 `Deserialize{TypeName}` 是唯一被 Config 使用的生成物**——其余生成代码（`Tables`、`ResolveRef`）不进入运行时路径；
3. **`variant` 是二选一，不是多源叠加**，不能用于 DLC/mod 合并。

> 本文只记录能力事实，不承载设计决策。设计决策见 [内容包模型](2026-08-01-content-pack-quadruple-kickoff.md) 与 [R006](../requirements/R006-data-pack-config.md) / [R007](../requirements/R007-item-storage.md)。
