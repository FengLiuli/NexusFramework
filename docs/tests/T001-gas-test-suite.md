---
layer: test
status: draft
task: T001
source_files:
  - Assets/Tests/Smoke/GASSmokeTests.cs
  - Assets/Tests/ECS/AbilityPipelineTests.cs
  - Assets/Tests/ECS/EffectPipelineTests.cs
  - Assets/Tests/ECS/EffectStackingTests.cs
  - Assets/Tests/ECS/EffectTagTests.cs
  - Assets/Tests/ECS/CuePipelineTests.cs
  - Assets/Tests/Services/AbilityServiceTests.cs
  - Assets/Tests/Services/EffectServiceTests.cs
  - Assets/Tests/Services/TagServiceTests.cs
  - Assets/Tests/Services/AttributeServiceTests.cs
  - Assets/Tests/Runtime/Events/EventBridgeTests.cs
  - Assets/Tests/Config/ConfigLoaderTests.cs
  - Assets/Tests/Config/ConfigWorkflowTests.cs
  - Assets/Tests/Editor/EditorWorkflowTests.cs
  - Assets/Tests/Stress/MemorySafetyTests.cs
  - Assets/Tests/EntityGameObjectBindingTests.cs
  - Assets/Tests/Infrastructure/TestArchitecture.cs
  - Assets/Tests/Infrastructure/MockConfigLoader.cs
  - Assets/Tests/Infrastructure/NullAbilityLogic.cs
created: 2026-06-11
updated: 2026-06-11
---

# 测试文档：NexusFramework.GAS 测试套件

## 概述

NexusFramework.GAS 的测试采用 **EditMode 测试** 架构，基于 NUnit + Unity Test Framework。
所有测试在 `Tests.asmdef` 程序集中定义，依赖 `NexusFramework.GAS` 和 `Unity.Entities`。

**测试基础设施**：
- `TestArchitecture` — 继承自 `GASArchitecture`，使用 `MockConfigLoader` 替代 JSON 加载
- `MockConfigLoader` — 在代码中直接填充配置数据，不依赖磁盘文件
- `NullAbilityLogic` — 空的 AbilityLogic 实现，用于测试管线

## 测试计划

### 测试范围

| 模块 | 覆盖情况 | 说明 |
|------|---------|------|
| 架构初始化 | ✅ 已覆盖 | 架构创建、状态检查、服务可访问性 |
| Carrier ↔ Entity 绑定 | ✅ 已覆盖 | 创建/销毁 Carrier、ECS 组件预置 |
| ECS Ability 管线 | ✅ 已覆盖 | 授权→激活→取消→结束全流程 |
| ECS Effect 管线 | ✅ 已覆盖 | 瞬时/持续 Effect 生命周期、属性修改、移除后回落 |
| ECS Effect 堆叠 | ✅ 已覆盖 | 同 Code 堆叠、LimitCount 限制、溢出拒绝、不同 Code 不堆叠 |
| ECS Effect 标签 | ✅ 已覆盖 | 需求标签检查、免疫标签检查、授予标签 |
| ECS Cue 管线 | ✅ 已覆盖 | Cue 创建/播放状态、Kill 后销毁 |
| Service 层 | ✅ 已覆盖 | AbilityService 容错、TagService 读写、EffectService、AttributeService |
| 事件桥接 | ✅ 已覆盖 | Runtime ECS ↔ Framework 事件桥接 |
| Config 加载 | ✅ 已覆盖 | JSON 配置加载、ConfigModel 填充 |
| 编辑器工作流 | ✅ 已覆盖 | 编辑器上下文中的 GAS 操作 |
| 实体-GameObject 绑定 | ✅ 已覆盖 | ECS Entity ↔ GameObject 双向绑定 |
| 内存安全 | ✅ 已覆盖 | NativeContainer 回收验证 |

### 不覆盖的范围

- **PlayMode 测试** — 当前全部为 EditMode 测试，不涉及场景加载和运行时渲染管线
- **Demo 层代码** — `Assets/Demo/` 下的 Emberheart 项目特定实现不纳入单元测试
- **性能基准测试** — 未集成 `com.unity.test-framework.performance` 的 `PerformanceTest` 属性
- **UI 层** — 无 UI 相关的测试

### 测试用例

#### Smoke 测试组 (`GASSmokeTests`)

| 编号 | 场景 | 输入 | 预期输出 | 类型 |
|------|------|------|---------|------|
| SMK-01 | 架构初始化无错误 | 创建 `TestArchitecture` | `State == Initialized` | 单元 |
| SMK-02 | 架构类型检查 | 创建 `TestArchitecture` | `ArchitectureType == "GAS"` | 单元 |
| SMK-03 | ECS World 创建 | 架构初始化后 | `World.IsCreated == true` | 单元 |
| SMK-04 | 所有服务可访问 | 通过 Architecture 获取 | 7 个 Service 均非 null | 单元 |
| SMK-05 | EntityMapModel 可访问 | 通过 Architecture 获取 | Model 非 null | 单元 |
| SMK-06 | Carrier 创建并映射 Entity | `CreateGASCarrier("TestUnit")` | Carrier 有效 + Entity 非 Null | 集成 |
| SMK-07 | 创建 Carrier 预置必备 Buffer | `CreateGASCarrier("TestUnit")` | 6 种 Buffer/Component 齐全 | 集成 |
| SMK-08 | 销毁 Carrier 清理 Entity | `DestroyGASCarrier()` | Carrier 不存 + Entity 被销毁 | 集成 |
| SMK-09 | Dispose 关闭 World | 架构 `Dispose()` | `WorldService.IsInitialized == false` | 集成 |
| SMK-10 | 多 Carrier 不冲突 | 创建 2 个 Carrier | 两个 Entity 不同且均有效 | 集成 |

#### Ability 管线测试组 (`AbilityPipelineTests` + `AbilityServiceTests`)

| 编号 | 场景 | 输入 | 预期输出 | 类型 |
|------|------|------|---------|------|
| ABL-01 | 授权→激活→Active | `GrantAbility` + `TryActivate` + ECS Tick | `IsActive == true` | 集成 |
| ABL-02 | 授权→结束 | `TryEnd` | `CAbilityInTryEnd` 标记出现 | 集成 |
| ABL-03 | 授权→取消 | `TryCancel` | `CAbilityInTryCancel` 标记出现 | 集成 |
| ABL-04 | 不存在的配置不抛错 | `GrantAbility(code=99999)` | 无异常 | 容错 |
| ABL-05 | 无效 Carrier 激活返回 false | `TryActivate(Invalid Carrier)` | 返回 `false` | 容错 |
| ABL-06 | 无效 Carrier 查询活跃返回 false | `IsActive(Invalid Carrier)` | 返回 `false` | 容错 |
| ABL-07 | 手动创建 Entity 后激活 | `CreateMinimalAbilityEntity` + `TryActivate` | `CAbilityInTryActivate` 标记 | 单元 |
| ABL-08 | 激活后查询活跃状态 | 添加 `CAbilityActive` | `IsActive == true` | 单元 |
| ABL-09 | 移除 Ability 销毁 Entity | `RemoveAbility` | Entity 不存在 | 单元 |

#### Effect 管线测试组 (`EffectPipelineTests`)

| 编号 | 场景 | 输入 | 预期输出 | 类型 |
|------|------|------|---------|------|
| EFF-01 | 瞬时 GE 创建 Wip 组件 | `ApplyEffect(configId:1)` | 出现 `WipInstantiateEffect` 等 | 集成 |
| EFF-02 | 瞬时 GE 完整生命周期至销毁 | `ApplyEffect` + 5 帧 Tick | GE Entity 被销毁 | 集成 |
| EFF-03 | 持续 GE 属性修改 | `ApplyEffect(configId:2)` → 属性 100→110 | `CurrentValue == 110` | 集成 |
| EFF-04 | 移除 GE 属性回落 | GE 移除 + 标记脏 + Tick | `CurrentValue == 100` | 集成 |

#### Effect 堆叠测试组 (`EffectStackingTests`)

| 编号 | 场景 | 输入 | 预期输出 | 类型 |
|------|------|------|---------|------|
| STK-01 | 同 Code GE 堆叠层数=2 | 施加 2 次 `configId:3` | 1 个实体 + `StackCount == 2` | 集成 |
| STK-02 | 3 次施加达到 LimitCount | 施加 3 次 `configId:3` | `StackCount == 3` | 集成 |
| STK-03 | 不同 Code GE 不堆叠 | `configId:2` + `configId:3` | 2 个独立实体 | 集成 |
| STK-04 | 超出 Limit 拒绝叠加 | 施加 4 次 Limit=3 的 GE | `StackCount == 3`（第 4 次被拒绝） | 集成 |

#### Effect 标签测试组 (`EffectTagTests`)

| 编号 | 场景 | 输入 | 预期输出 | 类型 |
|------|------|------|---------|------|
| TAG-01 | 需求标签存在 → 施加成功 | 目标有 tag 10 + GE 要求 tag 10 | GE 实体存活 | 集成 |
| TAG-02 | 需求标签缺失 → GE 销毁 | 目标无 tag 10 + GE 要求 tag 10 | GE 实体被销毁 | 集成 |
| TAG-03 | 免疫标签存在 → GE 销毁 | 目标有 tag 20 + GE 检查免疫 20 | GE 被销毁 | 集成 |
| TAG-04 | 免疫标签缺失 → GE 保留 | 目标无免疫标签 | GE 正常存活 | 集成 |
| TAG-05 | GE 激活后授予标签 | `ApplyEffect(configId:12)` → 授予 tag 10 | 目标 `BTemporaryTag` 含 tag 10 | 集成 |

#### Cue 管线测试组 (`CuePipelineTests`)

| 编号 | 场景 | 输入 | 预期输出 | 类型 |
|------|------|------|---------|------|
| CUE-01 | GE 带 Cue → Cue 创建并播放 | `ApplyEffect(configId:20)` + Tick | `ECCuePlaying == true` | 集成 |
| CUE-02 | Cue 被 Kill → 实体销毁 | 启用 `ECKillCue` + Tick | Entity 不存在 | 集成 |

#### Service 层测试组 (`AbilityServiceTests` / `TagServiceTests` / `EffectServiceTests` / `AttributeServiceTests`)

| 编号 | 场景 | 输入 | 预期输出 | 类型 |
|------|------|------|---------|------|
| SVC-01 | 无标签查询返回 false | `HasTag(carrier, 1)` | `false` | 单元 |
| SVC-02 | 无效 Carrier 标签查询 | `HasTag(Invalid, 1)` | `false` | 容错 |
| SVC-03 | 添加固定标签 | `AddFixedTag(tag:42)` | Buffer 中出现 tag 42 | 单元 |
| SVC-04 | 移除固定标签 | `Add` → `RemoveAt` | Buffer 中无 tag 42 | 单元 |
| SVC-05 | 添加临时标签 | `AddTemporaryTag(tag:99)` | Buffer 中出现 tag 99 | 单元 |
| SVC-06 | Effect 容错：不存在的配置 | `ApplyEffect(configId:99999)` | 无异常 | 容错 |
| SVC-07 | Effect 容错：无效 Carrier | `ApplyEffect(configId:0, invalid)` | 无异常 | 容错 |
| SVC-08 | Attribute GetCurrentValue | `Setup(100/75)` → 读取 | `75` | 单元 |
| SVC-09 | Attribute GetBaseValue | `Setup(200/200)` → 读取 | `200` | 单元 |
| SVC-10 | HasAttribute 存在返回 true | `Setup(1,10,100/100)` → 检查 | `true` | 单元 |
| SVC-11 | HasAttribute 缺失返回 false | 不存在的 (99,99) | `false` | 单元 |
| SVC-12 | SetBaseValue 更新值 | `SetBaseValue(50→999)` | 读取 = `999` | 单元 |
| SVC-13 | SetBaseValue 标记脏 | 设置后 | `CAttributeIsDirty` 出现 | 单元 |
| SVC-14 | SetCurrentValue | `SetCurrentValue(10→42)` | 读取 = `42` | 单元 |
| SVC-15 | 无效 Carrier 属性接口返回零 | 所有接口传入 Invalid | `0` / `false` | 容错 |

#### Entity ↔ GameObject 绑定测试组 (`EntityGameObjectBindingTests`)

| 编号 | 场景 | 输入 | 预期输出 | 类型 |
|------|------|------|---------|------|
| BND-01 | Model 实现 IGASEntityResolver | 类型检查 | `IsInstanceOf<IGASEntityResolver>` | 单元 |
| BND-02 | Bind → GetGameObject 返回 GO | `BindGameObject(entity, go)` | `result == go` | 单元 |
| BND-03 | 未绑定时 GetGameObject 返回 null | 未 Bind | `null` | 单元 |
| BND-04 | Entity.Null 查询返回 null | `GetGameObject(Entity.Null)` | `null` | 容错 |
| BND-05 | GetEntity 反向查询 | `Bind` → `GetEntity(go)` | `entity` | 集成 |
| BND-06 | 未绑定的 GO 查询 Entity | `GetEntity(new GO)` | `Entity.Null` | 单元 |
| BND-07 | Null GO 查询 | `GetEntity(null)` | `Entity.Null` | 容错 |
| BND-08 | IsEntityBound 绑定后为 true | `Bind` → 检查 | `true` | 单元 |
| BND-09 | IsEntityBound 绑定前为 false | 未 Bind | `false` | 单元 |
| BND-10 | IsGameObjectBound 绑定后 true | `Bind` → 检查 | `true` | 单元 |
| BND-11 | UnbindGameObject 清除绑定 | `Unbind` → 双向检查 | 均 null/false | 集成 |
| BND-12 | 未绑定时 Unbind 不抛 | 未 Bind 的 Entity | 无异常 | 容错 |
| BND-13 | 重新绑定替换旧绑定 | `Bind(go1)` → `Bind(go2)` | 绑定到 go2 | 集成 |
| BND-14 | 同一 GO 绑定不同 Entity 拒绝 | 第二次绑定被拒绝 | 保留第一次 | 集成 |
| BND-15 | Bind(Entity.Null) 不抛 | Bind Null Entity | 无异常 | 容错 |
| BND-16 | Bind(null GO) 不抛 | Bind null GO | 无异常 | 容错 |
| BND-17 | Bind 自动附加 GASEntityRef | Bind 后检查 | 组件自动添加 | 集成 |
| BND-18 | Unbind 标记 GASEntityRef 已解绑 | Unbind 后检查 | Entity=Null | 集成 |
| BND-19 | GO 销毁后 GetGameObject 返回 null | DestroyImmediate 后 | `null` | 集成 |
| BND-20 | DestroyGASCarrier 自动解绑 GO | 创建+销毁 | 双向清除 | 集成 |
| BND-21 | BindGameObjectForCarrier 便捷方法 | 创建后单独 Bind | 绑定正确 | 集成 |
| BND-22 | CreateGASCarrier 带 GO 自动绑定 | 创建时传入 GO | 自动绑定 | 集成 |
| BND-23 | 多架构独立绑定 | 两个架构各绑定 | 互不干扰 | 集成 |
| BND-24 | GASEntityRef OnDestroy 兜底解绑 | 销毁 GO 触发 | 绑定清除 | 集成 |

#### 内存安全测试组 (`MemorySafetyTests`)

| 编号 | 场景 | 输入 | 预期输出 | 类型 |
|------|------|------|---------|------|
| MEM-01 | 创建销毁 50 个 Carrier | 循环 50 次创建+销毁 | 无残留 | 压力 |
| MEM-02 | 50 个瞬时 GE 全部销毁 | 施加 50 个 GE + Tick | 全部销毁 | 压力 |
| MEM-03 | 重复 Init/Dispose 5 次 | 循环创建架构 | 无异常 | 压力 |
| MEM-04 | 20 个 Ability 全部清理 | 循环 Grant+Remove | 无残留 | 压力 |
| MEM-05 | NativeArray 能力实体回收 | 20 个带 NativeArray 的 Ability | 不崩溃 | 压力 |
| MEM-06 | 带属性的 Carrier 销毁 | 30 个带 BEAttrSet 的 Carrier | 不崩溃 | 压力 |

## 测试报告

### 初次执行日期：2026-06-11（Unity Editor 未连接，待执行）

### 结果摘要

| 组 | 用例数 | 通过 | 失败 | 跳过 |
|---|--------|------|------|------|
| Smoke 测试 | 10 | ⏳（待执行） | ⏳ | ⏳ |
| Ability 管线/Service | 9 | ⏳（待执行） | ⏳ | ⏳ |
| Effect 管线 | 4 | ⏳（待执行） | ⏳ | ⏳ |
| Effect 堆叠 | 4 | ⏳（待执行） | ⏳ | ⏳ |
| Effect 标签 | 5 | ⏳（待执行） | ⏳ | ⏳ |
| Cue 管线 | 2 | ⏳（待执行） | ⏳ | ⏳ |
| Service 层 | 15 | ⏳（待执行） | ⏳ | ⏳ |
| Entity↔GO 绑定 | 24 | ⏳（待执行） | ⏳ | ⏳ |
| 内存安全 | 6 | ⏳（待执行） | ⏳ | ⏳ |
| **总计** | **79** | ⏳ | ⏳ | ⏳ |

> **说明**：测试代码已在 `Assets/Tests/` 目录中存在（约 19 个文件），但需要通过 Unity Editor 的 Test Runner 执行。
> 连接 Unity MCP 后可执行 `run_tests` 工具获取真实结果。

## 关联

- 被测代码：[核心框架编码文档](../code/nexusframework-core.md)
- 被测代码：[GAS 编码文档](../code/nexusframework-gas.md)
- 设计文档：[核心框架设计](../design/D001-core-framework.md)
- 设计文档：[GAS 设计](../design/D002-gas-design.md)
