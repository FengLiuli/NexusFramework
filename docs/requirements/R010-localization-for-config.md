---
layer: requirements
status: draft
topic: localization-config
origin:
  - docs/brainstorms/2026-08-01-content-pack-quadruple-kickoff.md
  - docs/requirements/R009-localization.md
  - docs/requirements/R006-data-pack-config.md
created: 2026-11-17
updated: 2026-11-17
---

# 需求：LocalizationForConfig 配置表适配

## 0. 定位

本文定义 `NexusFramework.LocalizationForConfig`：把数据包里的**多语言宽表**转换成对 [Localization 核心](R009-localization.md) 的注册调用，并提供键校验、孤儿键与覆盖率报告。

依赖方向：

```text
LocalizationForConfig → NexusFramework.Localization + NexusFramework.Config
Localization            → NexusFramework（零 Config 依赖）
```

拆分的硬理由：**孤儿检测与覆盖率需要读取目标内容包的表**，而核心库不能依赖 Config。适配器拥有"表"的视野，因此校验与报告都落在这里。

---

## 一、背景

- 现有 [l10n.json](../../Assets/Demo/Table/Datas/l10n.json) 已经是"一表多语言"形态（`key / en / zh`），但它是 Demo 私有格式：没有 Schema、没有注册链路、没有校验；
- 宽表对策划与译者最友好：一屏对照多语言，改动集中在一行；
- 新增语言是罕见事件（加列 + 重生成），新增内容包是常态事件（加文件）——因此**本地化子包按支持的内容包划分**，一表多语言（内容包模型 C7）；
- 核心库只接受注册，不知道"目标包里有没有这个键"，因此表相关的校验只能在适配器完成。

---

## 二、目标

1. 一次调用完成一个本地化子包（一组 target + 一张宽表）的全部注册；
2. 键、列、目标不合法时在加载期定位到**表名 + 键/列**，不静默降级；
3. 孤儿键与覆盖率可导出，供翻译工作流与 CI 使用；
4. 不引入第二套语言选择或链排序逻辑（复用 R009）。

---

## 三、术语

| 术语 | 定义 |
|---|---|
| **宽表** | 一列文本键 + 若干语言列（+ 可选说明列）的配置表 |
| **列映射** | 列名 → 语言标签（`zh_cn` → `zh-CN`），由子包显式声明 |
| **本地化子包** | 一组 targets + 一张宽表 + priority，对应一次 `RegisterFromTable` |
| **孤儿键** | 宽表中存在、但目标内容包没有对应键的条目 |
| **覆盖率** | 某语言列非空条目数 / 总条目数 |

---

## 四、需求

### A. 表结构

- **RC1.** 宽表至少包含两列：`ID`（文本键）与一个以上语言列。允许存在说明列（`Context`、`Note`），注册时忽略，不参与校验。
- **RC2.** `ID` 为 `string`，遵循 [R009](R009-localization.md) RL1 的键格式；同一张表内重复 `ID` 拒绝（`DuplicateKey`）。
- **RC3.** 列名必须是合法 C# 标识符（`zh_cn` 而非 `zh-CN`）；**列到语言标签的映射由子包声明提供，不做隐式推断**——语言标签出错必须在注册期暴露，而不是运行期静默失配。
- **RC4.** 不在映射中的列（说明列或未登记语言列）必须被明确忽略；若一列看起来像语言列但未声明映射，输出告警（提示漏配），不自动纳入。
- **RC5.** 宽表由内容包自己的数据包提供。mod 不携带行类型（R006 Q9），因此宽表的行类型来自本体的数据包程序集；`LocalizationForConfig` 对具体行类型零知识，经 `ITableReader<TRow>` 与映射函数读取。

### B. 注册

- **RC6.** 注册入口：

  ```csharp
  LocalizationRegistrationResult RegisterFromTable<TRow>(
      string sourcePackId,
      IReadOnlyList<string> targets,
      int priority,
      ITableReader<TRow> table,
      IReadOnlyDictionary<string, string> columnToLanguage,  // "zh_cn" -> "zh-CN"
      Func<TRow, string> keySelector,
      Func<TRow, IReadOnlyDictionary<string, string>> rowSelector, // 语言列 -> 文本
      string tableName = null);
  ```

- **RC7.** 内部按语言列拆成多次 R009 注册：同一 `sourcePackId`、同一 `targets`、同一 `priority`，仅 `language` 与条目不同。
- **RC8.** 键校验：命名空间段必须属于 `targets`、格式必须合法；违规拒绝**该表**并给出表名、键与期望 targets。已注册的其他表不受影响。
- **RC9.** 空单元格 == 未提供：跳过该语言条目（与 R009 RL7 一致），查找时继续回退。
- **RC10.** 注册结果按表（而非按行）报告成功/失败，包含行数、按语言列的有效条目数与被忽略的列。

### C. 校验与报告

- **RC11.** 孤儿键检测：按 target 分组，比对"本表有、目标内容包已注册表无"的键；只告警，不拒绝。目标包未注册时跳过该 target 的检测并记录"待校验"。
- **RC12.** 覆盖率报告：按 `(子包, target, 语言)` 统计非空条目比例。
- **RC13.** 报告在启动完成后统一输出，不阻断启动；提供结构化导出（供 Editor / CI 读取），文本日志仅作摘要。
- **RC14.** 孤儿与覆盖率检测是**只读**操作，不改变已注册文本。

---

## 五、验收示例

- **CE1. 一次注册（RC6, RC7）**：一张含 `en / zh_cn / ja` 三列的宽表，一次调用后三个语言均可在 R009 查询到；`zh-CN` 请求命中 `zh_cn` 列。
- **CE2. 键校验（RC8）**：表中一行键为 `com.other:ui.ok` 而 `targets = ["core"]` → 整表拒绝，诊断为"表 l10n.core 第 N 行键命名空间不属于 targets"。
- **CE3. 空值（RC9）**：`ja` 列全空 → `ja` 语言下全部回退到上位/默认语言，不产生空字符串条目。
- **CE4. 孤儿（RC11）**：表中有 `core:ui.removed`，但 core 数据包已无该键 → 报告列出；文本仍注册成功，只是永远不会被查询到。
- **CE5. 覆盖率（RC12）**：输出 `zh_cn: 812/900 (90.2%)` 形式的统计。
- **CE6. 未声明列（RC4）**：表中多出一列 `fr` 但映射未登记 → 输出告警，不纳入注册。

---

## 六、范围边界

### 不做

- 表编辑器、翻译导入导出、机翻工作流（Editor 工具，后续）。
- 数据加载器：宽表从哪读由内容包自己的数据源决定（R006 RP28–RP30），本库只接收 `ITableReader<TRow>`。
- 语言列自动发现：必须显式声明映射。
- 语言选择、链排序、UI（R009 与编排层）。
- 多表合并：一个子包一张表；一个内容包有多张表时，由编排层分别调用。

---

## 七、依赖与假设

| # | 依赖 | 状态 |
|---|---|---|
| D1 | [R009 Localization](R009-localization.md) 核心 | 需求定稿，零代码 |
| D2 | [R006 Config](R006-data-pack-config.md) 的 `ITableReader<TRow>` | 需求定稿，零代码 |
| D3 | 本地化子包清单（targets / table / columns / priority） | 属 R008（未写）；本期可用代码参数传入 |
| D4 | 键格式与命名空间约定 | 与 R006 RP9a 共用 |
| D5 | 受支持语言标签子集 | 与 R009 Q1 共用同一张表 |

---

## 八、待决问题

- **Q1.** 列映射声明的位置：子包清单（数据驱动，热更友好）还是代码参数（类型安全，但不便配置）？建议清单声明、代码校验一致性。
- **Q2.** 一张宽表覆盖多个 target 时，键前缀混合的组织方式：一张表内混排，还是按 target 拆表？当前模型允许混排，但孤儿检测需要按前缀分组。
- **Q3.** 报告输出格式：结构化 JSON（CI 友好）还是 Unity 日志摘要 + 导出文件。
- **Q4.** 是否提供 Editor 侧的"表 → 预览"工具，在 gen 前发现问题而不是启动后。

---

## 九、关联

- 本地化核心：[R009 Localization](R009-localization.md)
- 键与表：[R006 数据包子系统](R006-data-pack-config.md)
- 内容包模型：[内容包模型结论](../brainstorms/2026-08-01-content-pack-quadruple-kickoff.md)（C7 多语言宽表、C10 能力库清单）
- 内容包需求：R008（待写）
