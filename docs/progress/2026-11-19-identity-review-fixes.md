---
layer: progress
status: done
topic: identity-review-fixes
created: 2026-11-19
updated: 2026-11-19
---

# 修复记录：Identity / Carrier ID 方案审查问题

来源：2026-11-19 对 D008 / R011 落地实现的审查（Identity 三原语 + Carrier 迁移）。

本轮只处理审查中确认的实现缺陷；两项需要单独排期的内容列在文末。

## 已完成

### P0-1 Carrier 类型 ID 从 `ushort` 改为 `int`

- `ICarrierManager` / `CarrierManager`：`RegisterType`、`GetTypeId`、`GetTypeName`、
  `CreateCarrier`、`GetCarrierTypeId`、`GetCarriersByType`、`GetCarrierCount` 全部改 `int`。
- `carrier.type` 的 `KeyIndex` 不再设置 `maxValue = ushort.MaxValue`，
  类型数量只受 `int` 限制（RID43）。
- `Framework.cs` 的扩展方法、`Tests/Stress` 的 helper 同步改 `int`。
- 仓库内已无 `ushort` 引用。

### P0-2 Tag 注册失败不再挂载 slot 0

- `TagRuntimeIndex` 新增 `TryIntern`；`Intern` 保留为测试便捷入口，失败返回 default。
- `CarrierTagService.AddTagInternal` 使用 `TryIntern`，失败返回 `Invalid`，
  不再把 `RuntimeTagSlot(0)` 写入 `tagsByCarrier`。
- `EnsureAncestors` 跳过注册失败的祖先，不把 0 写入祖先链。
- 修复后 `GetTags` 不会因 0 槽位抛 `ArgumentException`（RID10/RID17）。

### P0-3 `KeyIndex` 支持关闭命名空间解析

- `KeyIndexOptions` 新增 `parseNamespace` 参数与 `DisableNamespaceParsing` 存储位。
  用「关闭位」而非「开启位」存储，保证 `default(KeyIndexOptions)` 与
  `new KeyIndexOptions()` 语义一致（都解析命名空间）。
- Tag 索引（`core.tag`）以 `parseNamespace: false` 创建，冒号按普通字符处理（RID40）。
- `IdRegistry.GetOrCreateIndex` 在同名域选项不一致时告警，不再静默忽略。

### P1-1 移除未实现的错误分支

- `KeyIndexError` 删除 `DuplicateKey` 与 `AuthorityUnavailable`（从不产生）。
- `KeyIndexDiagnostics.ConflictCount` 更名为 `NamespaceRejectedCount`，
  新增 `AuthorityUnavailableCount`（authority 缺失时放行并计数，RID13）。
- D008 同步说明：重复键当前为幂等语义，来源判定不在本期。

### P1-2 构造期快速失败

- `CarrierManager` 构造时若 `carrier.type` / `carrier.instance` 域被其它原语占用，
  直接抛 `InvalidOperationException`，不再把 null 留到首次调用才 NRE。

### P1-3 Tag 服务不再清空架构级索引

- `CarrierTagService` 记录索引是否自建：自建时 `Clear()`，架构注入时只 `ClearAncestors()`。
- `core.tag` 的 `KeyIndex` 归 `IdRegistry`，生命周期由架构统一管理，服务销毁不再误清。
- `CarrierTagServiceDisposeTests` 相应改为：Dispose 清挂载关系，key→slot 映射保留。

### P2-1 机械替换残留

- `EcsTagService.GetAllints()` → `GetAllCarrierIds()`。
- `CarrierIndex.ValueIsint` → `ValueIsCarrierId`；`CarrierGroupIndex.KeyIsint` → `KeyIsCarrierId`。
- 清理 `ArchitectureFactory` / `ArchitectureFactoryTests` 中已失效的「框架位」注释。

### P2-2 CarrierManager 的竞态与分配

- `AddTrait` / `RemoveTrait` / `GetTrait` / `HasTrait` / `GetAllTraits` / `GetTraitTypes` /
  `SerializeCarrier` 改为锁内 `TryGetValue`，消除「锁外判活 → 锁内索引」的 TOCTOU。
- `DestroyCarrier` 在索引表为空时使用 `Array.Empty<ICarrierIndex>()`，
  去掉每销毁一个载体分配一个空数组的开销。

### 测试

新增：

- `KeyIndexTests`：多冒号键（关闭命名空间解析）、authority 缺失计数、命名空间拒绝计数、选项暴露。
- `CarrierTagServiceTests`：多冒号 Tag 键、索引拒绝时不挂载 slot 0 且 `GetTags` 不抛。
- `CarrierTraitSerializationTests`：只有 `carrierId` 的旧存档被拒绝。
- `ArchitectureCarrierNamespaceTests`：架构重启后旧 carrier id 失效、新架构从 1 重新发号。

## 验证

### 静态编译（Roslyn）

- `Identity/`、core（含 DataCarrier）、GAS、除 Editor 外的全部测试程序集：0 警告 0 错误。
- Identity 冒烟（KeyIndex/TryRegister/IdAllocator/TypedAllocator 路径）：全部通过。

### Unity Editor 实测（2026-11-19）

- 重编译：`recompile_status` = completed、`compilationFailed` = false。
  控制台唯一报错来自 PlasticSCM 工作区提示，与代码无关。
- EditMode 测试：324 个用例全部通过，0 失败。
  - Core：KeyIndex 14、Carrier* 113、Id* 17、Tag* 22、Architecture* 11、
    MultiArchitectureIsolation 1、TraitReverseIndex 3。
  - GAS：Services 41、ECS 25、Smoke 10、Config 23、Stress 6、Editor 10、
    EntityGameObjectBinding 24、EventBridge 4。
- 性能基准（`CarrierPerformanceTests`，三次取最小值，Mono/Editor）：
  - CreateCarrier 1M：449.4 ns/op（11-17 基线 558.2，−20%）
  - DestroyCarrier 1M：412.5 ns/op（基线 458.1，−10%）
  - DestroyCarrier 100k：403.6 ns/op（基线 447.2，−10%）
  - DestroyCarrier 100k indexes=32：6.697 µs/op（基线 6.461，+3.7%）
  - GetCarriersByType 10k：179.5 µs/op（基线 234.6，−23%）
  - 结果文件：`TestResults/carrier-perf-run1-2026-11-19.txt`、
    `carrier-perf-run2-2026-11-19.txt`、`TestResults/carrier-perf.txt`（第三次）。

核心路径均优于基线（RID50）。索引扇出 indexes=1/8 三次波动较大
（668–1050 ns、2.06–2.55 µs），取最小值相对基线约 +12%；销毁期的索引快照
每次都会分配一个数组，后续可改为可复用缓冲。销毁路径仍有约 10.8 B/op
的摊分分配，来源是 `IdAllocator` 空闲表扩容（容量增长的一次性成本），
不是每次销毁都分配。

## 第二轮（同日）

### GAS Tag 接入 KeyIndex（RID52 第 5 批）

- `KeyIndex` 新增 `TryRegister(key, id)`：显式绑定配置驱动的稳定 id，冲突返回
  `KeyIndexError.IdConflict`；自动发号跳过显式注册的 id，`IsAlive` 以映射表为唯一真相。
- `EcsTagService` 删除自有 `mIdByKey` / `mKeyById`，改用 `KeyIndex("gas.tag")`；
  `RebuildTagMap` 按配置 Code 显式注册，Tag id 与配置保持一致。
- `GASArchitecture` 从架构 `IdRegistry` 取 `gas.tag` 并注入 `EcsTagService`；
  注入索引的生命周期归架构，服务只负责重建映射。
- 新增测试：配置 Code 往返（`Faction.Player` → 11）、KeyIndex 显式注册冲突与发号跳过。

### Trait 内 ID 引用（DQ5）

- 新增 `IIdRefTrait`（`CaptureIdRefs` / `ApplyIdRefs`）与 `IdRef`。
- `TraitSerializationData` 增加 `idRefs`（domain + key）：写档时 Trait 把 id 字段置 0，
  框架把 id 转 key 写入；读档时按 key 解析新 id 后写回。id 不进入存档。
- 新增测试 `IdRefTestTrait`：跨架构读档后 id 按 key 重新编号，普通字段仍正常往返。

## 未完成（需单独排期）

1. **IL2CPP Player 侧复测（RID51，可选）**：当前结论基于 Editor + Mono。
2. **索引扇出销毁的缓冲复用**：`DestroyCarrier` 每次分配索引快照数组；
   indexes=1/8 的噪声区间略高于基线，可用可复用缓冲进一步收敛。
