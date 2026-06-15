---
task_id: T003
title: "LubanConfigLoader 生成器"
status: 进行中
complexity: P2
type: 新功能
created: 2026-06-13
updated: 2026-06-13
---

# 任务：LubanConfigLoader 生成器

## 📋 任务描述

**原始需求**：编写 LubanConfigLoader 生成器，根据 Luban 实际使用时创建的表格类型生成桥接代码。

**AI 理解**：
- 现有的 IConfigLoader 接口定义了 Parse/GameplayEffect/ParseAbility 等方法，但 JsonConfigLoader 未完整实现
- Luban 生成的 cfg.Tables 类型（如 Tbability、TbgameplayEffect）是项目级的，框架本身无法预知
- 需要一个 Editor 代码生成器，扫描 cfg.* 命名空间，自动生成将表格数据转为 GAS 运行时配置的代码

## 🎯 任务类型

新功能

## 📂 受影响文档层

- [x] 需求：创建 `docs/requirements/R003-luban-config-loader-helper-methods.md`（配套运行时类型映射）
- [x] 设计：创建 `docs/design/D003-luban-config-bridge.md`（Luban 配置桥接设计）
- [x] 编码：更新 `docs/code/gas-config.md`（运行时类型映射章节）
- [x] 测试：创建 `docs/tests/T003-luban-config-loader-generator.md`（测试计划 + 测试代码模板）
- [x] 验证：生成结果在 Unity 中编译通过（0 errors / 0 warnings）
- [x] 进度：追加到 `docs/progress/`

## 📁 受影响文件

- `Assets/NexusFramework.GAS/Editor/CodeGen/IndentedWriter.cs`（创建）
- `Assets/NexusFramework.GAS/Editor/CodeGen/LubanConfigLoaderGenerator.cs`（创建）
- `Assets/NexusFramework.GAS/Editor/GASSettingAsset.cs`（修改：添加输出路径配置）
- `Assets/NexusFramework.GAS/ECS/System/GasMmcHelper.cs`（修改：添加 MMC 类型映射）
- `Assets/NexusFramework.GAS/ECS/Ability/Component/AbilityLogic/AbilityLogicFactory.cs`（修改：添加 XParam 类型映射）
- `Assets/NexusFramework.GAS/Services/CueService.cs`（修改：MMC 注册时推断 XParam 类型）
- `Assets/NexusFramework.GAS/Services/AbilityService.cs`（修改：AbilityLogic 注册时推断 XParam 类型）
- `docs/requirements/R003-luban-config-loader-helper-methods.md`（创建：需求文档）
- `docs/design/D003-luban-config-bridge.md`（创建：设计文档）

## 📊 拆分计划

- [x] 了解项目结构、gasForUnity 参考实现
- [x] 创建 IndentedWriter 工具类
- [x] 创建 LubanConfigLoaderGenerator 生成器
- [x] 修改 GASSettingAsset 添加输出路径设置
- [x] 修正生成器中字段映射与实际 Conf* 类的兼容性
- [x] 添加生成器的单元测试（`docs/tests/T003-luban-config-loader-generator.md`）
- [x] 在 Unity Editor 中验证生成结果可编译

## 🔗 关联任务

- 前置依赖：T001（GAS 框架搭建）- 已提供 Conf* 组件
- 后续任务：无

## ⚠️ 已知问题

- ~~GasMmcHelper 缺少 GetMmcType/GetMmcParamType 方法，生成器使用后备方案~~ — 已实现
- ~~AbilityLogicFactory 缺少 GetAbilityLogicParamType/GetAbilityTaskParamType 方法，需运行时通过 CueHelper 等替代~~ — 已实现

## 进度记录

> **追加不覆盖**：每次会话在末尾追加新条目，不修改已有条目。

---

### [2026-06-13] 补全文档链（需求审核 + 设计文档 + 编码文档）

**完成：**
- 审核需求文档 `docs/requirements/R003-luban-config-loader-helper-methods.md`（draft → reviewed）
- 创建设计文档 `docs/design/D003-luban-config-bridge.md`（Luban 配置桥接设计）
- 更新编码文档 `docs/code/gas-config.md`（新增"运行时类型映射"章节）

**待验证：**
- Unity Editor 中检查生成的 `LubanConfigLoader.cs` 是否通过编译
- 功能回归验证（加载配置、授予技能、触发效果等现有路径不受影响）

---

### [2026-06-13] 实现缺失的运行时类型映射方法

**完成：**
- 创建需求文档 `docs/requirements/R003-luban-config-loader-helper-methods.md`
- `GasMmcHelper` 新增 `RegisterMmc`/`GetMmcType`/`GetMmcParamType` 方法
- `AbilityLogicFactory` 新增 `RegisterAbilityLogicParam`/`GetAbilityLogicParamType` + `RegisterAbilityTaskParam`/`GetAbilityTaskParamType` 方法
- `CueService.ScanAndRegisterAll()` 在 MMC 注册时同时推断 XParam 参数类型并写入 `GasMmcHelper`
- `AbilityService.ScanAndRegisterAll()` 在 AbilityLogic 注册时补充 `InferParamType` + 写入 `AbilityLogicFactory`
- `InferParamType` 反射模式与 `CueService` 中已存在的实现一致

**待验证：**
- Unity Editor 中检查生成的 `LubanConfigLoader.cs` 是否通过编译
- 功能回归验证（加载配置、授予技能、触发效果等现有路径不受影响）

---

### [2026-06-13] 修复生成器字段映射兼容性问题 + 编译验证通过

**完成：**
- 修复 6 个编译错误：
  1. `LoadTablesForEditor` 移除对 `GASSettingAsset` 的依赖（使用 `string jsonDir` 参数）
  2. GrantedAbility 枚举字段添加显式类型转换（`(GrantedAbilityActivationPolicy)` 等）
  3. ConfStacking 字段名修正（PascalCase `EffectDurationRefreshPolicy` + camelCase `denyOverflowApplication`）
  4. `typeof(MMCNone)` → `typeof(ECS.MMCNone)` 消除命名空间歧义
  5. Tag 层级 `t.ID` → `t.Id`，`Children` 使用 `Array.Empty<int>()`
  6. TimelineAbility 方法注释掉（运行时暂不支持），保留 stub 方法体备用
- 打破鸡生蛋编译循环：先手动修复生成的 cs 文件 → 主程序集编译通过 → Editor 程序集重编译 → 生成器用新代码运行
- 在 Unity Editor 中验证：重新生成 + 编译，0 errors / 0 warnings

**已确认：**
- 生成器产生的 `LubanConfigLoader.cs` 编译通过
- 所有 6 个修复已从生成器源代码中产生正确的输出

**已知限制：**
- TimelineAbility 配置查询暂不实现（`WriteTimelineAbilityMethod` 调用已注释），需要运行时 `XParamTimeline` 支持后再开启


