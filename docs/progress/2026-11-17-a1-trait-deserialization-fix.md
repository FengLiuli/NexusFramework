---
layer: progress
status: in_progress
topic: a1-trait-deserialization-fix
created: 2026-11-17
updated: 2026-11-17
related:
  - docs/progress/2026-11-17-carrier-issues-backlog.md
---

# A1 修复：Trait 反序列化

> 对应 backlog [A1](../progress/2026-11-17-carrier-issues-backlog.md#a1-trait-反序列化完全失效)（P0）。
> 状态：**代码已改，待 Unity 编译与测试验证**。

## 一、根因回顾

`DeserializeCarrier` 用 `Type.GetType(traitData.typeName)` 解析 Trait 类型，而 `typeName` 是 `GetType().Name`（短名）。`Type.GetType` 只查 mscorlib 与调用方程序集，业务程序集一律返回 `null` → Trait 被静默跳过。叠加 `BasicInfoTrait` 缺少无参构造，类型能解析时会抛 `MissingMethodException` 并导致整个载体重建失败。

## 二、修复方案

采用**「AQN 优先 + 类型注册表回退 + 未初始化对象兜底」**三段式：

| 段 | 机制 | 作用 |
|---|---|---|
| 1 | 存档新增 `assemblyQualifiedName` 字段 | 新存档可靠解析，兼容旧格式 |
| 2 | `TraitTypeRegistry`（短名 / 全名 → Type，惰性扫描已加载程序集） | 旧存档只有短名时仍可恢复 |
| 3 | `Activator` 失败回退 `FormatterServices.GetUninitializedObject` | 无无参构造的 Trait 也能恢复 |
| 4 | 单个 Trait 的类型解析/实例化/`FromJson` 全部 try 隔离 | 单个 Trait 损坏不再拖垮整个载体 |

### 文件清单

| 文件 | 改动 |
|---|---|
| `Assets/NexusFramework/DataCarrier/TraitTypeRegistry.cs` | **新增**：类型名解析注册表 |
| `Assets/NexusFramework/DataCarrier/DataTrait.cs` | `BasicInfoTrait` 增加无参构造 |
| `Assets/NexusFramework/DataCarrier/CarrierManager.cs` | `TraitSerializationData` 增加 AQN 字段；`SerializeCarrier` 写入 AQN；`DeserializeCarrier` 改用解析器 + 异常隔离；新增 `ResolveTraitType` / `CreateTraitInstance` |
| `Assets/Tests/Core/CarrierTraitSerializationTests.cs` | **新增**：6 条回归测试 |

### 兼容性

- **旧存档**：无 `assemblyQualifiedName` 字段 → 回退短名 + 注册表扫描，行为与修复前相比只会更好。
- **新存档**：同时写短名与 AQN；旧版代码读新存档时忽略未知字段，仍按短名解析。
- **序列化体积**：每个 Trait 增加约 40~80 B（AQN 字符串）。对归档频率低的物品系统可忽略；若后续成为瓶颈，可在 B5 优化时改为「短名 + 程序集短名」双字段。

## 三、IL2CPP 注意事项

- `Type.GetType(AQN)` 依赖类型未被裁剪，Player 构建需要 `link.xml` 保留 Trait 类型；本次未添加，Editor/Mono 验证不受影响。
- `TraitTypeRegistry` 的程序集扫描在 IL2CPP 下同样受裁剪影响；**显式 `Register<T>()` 应作为 Player 构建的兜底**。建议在后续的 Player 验证任务中补一条 `link.xml`。

## 四、验证

### 自动化

```bash
unity command set_autotick --enable true --project-path "D:\Work\Unity\NexusFramework"
unity command recompile --project-path "D:\Work\Unity\NexusFramework"
unity command recompile_status --project-path "D:\Work\Unity\NexusFramework"
unity command run_tests --mode editor --filter NexusFramework.Core.Tests.CarrierTraitSerializationTests --async_tests true --project-path "D:\Work\Unity\NexusFramework"
unity command test_status --project-path "D:\Work\Unity\NexusFramework"
```

### 回归测试覆盖

| 测试 | 覆盖 |
|---|---|
| `SerializeAndDeserialize_RestoresTraitValues` | AQN 路径 + 字段值 |
| `SerializeAndDeserialize_RestoresMultipleTraits` | 多 Trait |
| `Deserialize_LegacyShortName_ResolvesViaAssemblyScan` | 旧存档短名 + 注册表扫描 |
| `Deserialize_TraitWithoutParameterlessCtor_FallsBackToUninitializedObject` | `FormatterServices` 回退 |
| `Deserialize_UnknownTraitType_KeepsCarrierAndSkipsTrait` | 未知类型隔离 |
| `SerializeAndDeserialize_CarrierWithoutTraits` | 空 Trait 载体 |

### 附加验证

现有 `DataTraitPerformanceTests.DeserializeCarrier_Scaling` 会输出 `[PERF][CHECK]` 恢复数；修复前恒为 0，修复后应等于「载体数 × 每载体 Trait 数」。

## 五、待办

- [ ] Unity 编译通过
- [ ] `CarrierTraitSerializationTests` 全绿
- [ ] 运行 `DataTraitPerformanceTests.DeserializeCarrier_Scaling`，确认恢复数非 0
- [ ] 更新 backlog A1 状态为已修复
- [ ] 评估是否补 `link.xml`（IL2CPP）
