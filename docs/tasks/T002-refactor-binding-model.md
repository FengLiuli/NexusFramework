---
task_id: T002
title: "重构 GAS 游戏对象绑定 Model"
status: 已完成
complexity: P2
type: 代码整洁
created: 2026-06-11
updated: 2026-06-11
---

# 重构方案：GAS 游戏对象绑定 Model 整理

## 📋 任务描述

**原始需求**：代码整洁 — 删除废弃的 `EntityGameObjectBindings`，统一 Entity↔GameObject 查询入口，消除 CatchAreaBox3D 中绕过接口的回退路径。

**AI 理解**：
- `EntityGameObjectBindings` 已被 `GASEntityMapModel` + `IGASEntityResolver` 接口替代，0 处 C# 引用
- `TargetCatcherBase` 缺少类似 `GameplayCueBase.GetTargetAscGameObject()` 的便捷方法
- `CatchAreaBox3D` 中存在 `GetComponentInParent<GASEntityRef>()` 回退路径，违反多架构隔离原则

## 🎯 任务类型

代码整洁

## 📂 受影响文档层

- [x] 编码：更新 `docs/code/nexusframework-gas.md`（Bridge 表格删除 EntityGameObjectBindings）
- [x] 进度：追加到 `docs/progress/`
- [ ] 需求：无需变更（纯重构，不影响功能）
- [ ] 设计：无需变更（设计方案已在前序任务中确定）
- [ ] 测试：无需变更（测试已覆盖绑定路径）

**每层变更理由**：
- 编码层：Bridge 表格中仍列出已删除的 EntityGameObjectBindings，需同步更新
- 需求/设计/测试层：本任务是代码清理，不涉及新功能或架构变更

## 📁 受影响文件

- `Assets/NexusFramework.GAS/ECS/Bridge/EntityGameObjectBindings.cs`（删除）
- `Assets/NexusFramework.GAS/ECS/Ability/TargetCatcher/TargetCatcherBase.cs`（添加便捷方法）
- `Assets/NexusFramework.GAS/ECS/Ability/TargetCatcher/CatchAreaBox3D.cs`（统一查询入口）
- `docs/code/nexusframework-gas.md`（更新文件清单）

## 📊 拆分计划

- [x] Step 1: 删除 `EntityGameObjectBindings.cs` 及 `.meta`
- [x] Step 2: `TargetCatcherBase` 添加 `ResolveGameObject()` / `ResolveEntity()` 便捷方法
- [x] Step 3: `CatchAreaBox3D` 统一查询入口，移除 `GetComponentInParent<GASEntityRef>()` 回退路径
- [x] Step 4: 更新 `docs/code/nexusframework-gas.md` 和 `Assets/KNOWN_ISSUES.md`

## 🔗 关联任务

- 前置依赖：T001（NexusFramework 文档体系搭建）
- 后续任务：无

## 🧠 决策记录

| 时间 | 决策 | 原因 |
|------|------|------|
| 2026-06-11 23:00 | 直接删除而非标记 `[Obsolete]` | 0 处 C# 引用，保留 Obsolete 徒增困惑 |
| 2026-06-11 23:00 | Resolve 方法放在 TargetCatcherBase 而非独立工具类 | 与 GameplayCueBase.GetTargetAscGameObject() 保持一致的封装模式 |

## ⚠️ 已知问题

- `GASEntityRef` 依赖 MonoBehaviour 的 `OnDestroy` 兜底解绑，若 GameObject 通过 `DestroyImmediate` 销毁可能不触发

## ❌ 失败路径

（暂无）

## 关联
- 设计文档：[GAS 设计文档](../design/D002-gas-design.md)
- 编码文档：[GAS 编码文档](../code/nexusframework-gas.md)
