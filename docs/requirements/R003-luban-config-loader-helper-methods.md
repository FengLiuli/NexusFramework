---
layer: requirements
status: reviewed
task: T003
created: 2026-06-13
updated: 2026-06-13
reviewed: 2026-06-13
reviewer: AI自审（确认需求完整、验收标准可验证、实现已到位）
---

# 需求：LubanConfigLoader 生成器配套运行时类型映射

## 1. 背景

已完成 `LubanConfigLoaderGenerator`（`LubanConfigLoaderGenerator.cs`），该生成器扫描运行时程序集中的 `cfg.*` 类型，
自动生成 `LubanConfigLoader` 桥接代码，将 Luban 强类型配置表转换为 GAS 的 ECS 运行时 ConfigModel 格式。

生成的桥接代码引用了四个尚未实现的运行时辅助方法，导致编译失败：

| 方法 | 所属类 | 用途 |
|------|--------|------|
| `GetMmcType(string)` | `GasMmcHelper` | MMC 类型名 → Type |
| `GetMmcParamType(string)` | `GasMmcHelper` | MMC 类型名 → XParam 子类 Type |
| `GetAbilityLogicParamType(string)` | `AbilityLogicFactory` | AbilityLogic 类型名 → XParam 子类 Type |
| `GetAbilityTaskParamType(string)` | `AbilityLogicFactory` | AbilityTask 类型名 → XParam 子类 Type |

此外，现有 `CueService.ScanAndRegisterAll()` 已为 GameplayCue 和 MMC 执行程序集扫描注册，
但 MMC 的 **param 类型映射** 未被存储；`AbilityService.ScanAndRegisterAll()` 仅注册逻辑类型名 → Type，
未注册 param 类型映射。

## 2. 需求

### 2.1 添加运行时类型映射字典

**GasMmcHelper** – 增加 MMC 类型名到 Type/XParam 类型的映射：

- `RegisterMmc(string typeName, Type mmcType, Type paramType)` – 注册
- `GetMmcType(string typeName) → Type` – 查询 MMC 类型
- `GetMmcParamType(string typeName) → Type` – 查询 MMC 对应的 XParam 类型

**AbilityLogicFactory** – 增加 AbilityLogic/AbilityTask 的 XParam 类型映射：

- `RegisterAbilityLogicParam(string typeName, Type paramType)` – 注册
- `GetAbilityLogicParamType(string typeName) → Type` – 查询
- `RegisterAbilityTaskParam(string typeName, Type paramType)` – 注册
- `GetAbilityTaskParamType(string typeName) → Type` – 查询

### 2.2 自动注册入口

在现有 `CueService.ScanAndRegisterAll()` 中，对已推断出的 MMC param 类型，额外调用 `GasMmcHelper.RegisterMmc()`。

在现有 `AbilityService.ScanAndRegisterAll()` 中，补充 `InferParamType` 逻辑，对能推断出 XParam 类型的 AbilityLogic，额外调用 `AbilityLogicFactory.RegisterAbilityLogicParam()`。

### 2.3 AbilityTask 的 XParam 映射

当前 GAS 框架中不存在 `AbilityTaskBase<T>` 泛型基类，Luban 生成的 `cfg.AbilityTaskBase` 子类不携带 XParam 泛型参数。因此 `GetAbilityTaskParamType` 始终返回 `null`（由生成器代码后备为 `XParamNone`）。

## 3. 非功能性需求

- 遵循 `CueHelper` 已有的 `Register* + Get*` 显式注册模式
- 自动注册使用已存在的 `InferParamType` 反射推断模式（与 CueService 一致）
- 注册入口位于相应 Service 的 `OnInit()` 中，确保 ConfigModel 加载前映射就绪

## 4. 验收标准

1. `GasMmcHelper.GetMmcType("MMCNone")` 返回 `typeof(MMCNone)`
2. `GasMmcHelper.GetMmcParamType("MMCNone")` 返回 `typeof(XParamNone)`
3. `AbilityLogicFactory.GetAbilityLogicParamType("ALApplyEffect")` 返回 `typeof(XParamEffectIDs)`
4. `AbilityLogicFactory.GetAbilityTaskParamType("TaskDoNothing")` 返回 `null`（暂不支持）
5. 生成的 `LubanConfigLoader.cs` 在 Unity Editor 中通过编译
6. 现有 GAS 功能不受影响（回归验证）

## 5. 关联

- 设计：[D003 Luban 配置桥接设计](../design/D003-luban-config-bridge.md)
- 设计前置：[D002 GAS 设计文档](../design/D002-gas-design.md)（配置系统 -> XParam 泛型配置）
- 任务：[T003](../tasks/T003-luban-config-loader-generator.md)
- 编码：[GAS 配置系统](../code/gas-config.md)（运行时类型映射）
