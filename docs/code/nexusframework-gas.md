---
layer: code
status: draft
task: T001
created: 2026-06-11
updated: 2026-06-13
---

# 编码文档：NexusFramework.GAS

NexusFramework.GAS（Gameplay Ability System）是一个基于 Unity ECS 的游戏能力系统，参考 Unreal Engine GAS 设计。

核心流程：**Ability（技能）→ GameplayEffect（效果）→ Modifier（属性修饰器）+ Cue（视觉反馈）**

## 子文档索引

| 文档 | 覆盖范围 | 源文件数 |
|------|---------|---------|
| [GAS 架构与服务层](gas-architecture.md) | GASArchitecture、8 个 Service、2 个 Model、系统组拓扑 | ~15 |
| [GAS ECS 组件清单](gas-ecs-components.md) | Buffer / Static / Dynamic 组件、XParam 类型参考 | ~80 |
| [GAS Effect 管线](gas-effect-pipeline.md) | Effect 七阶段状态机、堆叠、Duration、周期触发 | ~30 |
| [GAS Ability 管线](gas-ability-pipeline.md) | Ability 生命周期、AbilityLogic、TargetCatcher、冷却/消耗 | ~25 |
| [GAS Cue 管线](gas-cue-pipeline.md) | GameplayCueBase、四种内置 Cue、ECS Cue System 管线 | ~15 |
| [GAS 属性与 MMC](gas-attribute-mmc.md) | CAttributeData、BEAttrSet、属性重算、MMC 四种计算模式 | ~10 |
| [GAS Tag 与 Bridge](gas-tag-bridge.md) | GameplayTag 层级、ECS 双 Buffer 存储、事件桥接、GASEntityRef | ~15 |
| [GAS 配置系统](gas-config.md) | IConfigLoader、ConfigModel、XParam 泛型配置 | ~10 |

## 架构总览

```
GASArchitecture (继承自 Architecture)
    │
    ├── Services — World / Timer / EventBridge / Tag / Effect / Ability / Cue / Attribute
    ├── Models   — GASEntityMapModel / ConfigModel
    └── ECS Systems Pipeline
        └── SGLogic
            ├── SGlobalTimer
            ├── SGAbility     → 技能激活/取消/结束 + Tick
            ├── SGAttribute   → 属性值更新
            └── SGEffect      → 效果全生命周期
```

## 关联

- 设计文档：[GAS 设计文档](../design/D002-gas-design.md)
- 核心框架：[核心框架编码文档](nexusframework-core.md)
- 测试：[GAS 测试套件](../tests/T001-gas-test-suite.md)
