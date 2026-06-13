---
layer: design
status: draft
task: T001
created: 2026-06-11
updated: 2026-06-11
---

# 设计：NexusFramework 核心框架

## 方案概述

NexusFramework 解决 Unity 项目中的架构问题：缺乏清晰的分层和依赖管理，导致业务逻辑耦合严重、难以测试。
设计目标是提供一个 **轻量级的多实例架构框架**，通过显式的接口权限控制和 IOC 容器，强制各层职责分离。

核心设计思路：
- **小接口组合**（Interface Segregation）：将能力拆分为 `ICanGetService`、`ICanSendCommand` 等最小接口，通过 `interface` 组合声明类的能力
- **扩展方法注入**：在小接口上通过静态扩展方法提供真正的 API 实现，子类只需声明实现哪个接口
- **多实例隔离**：同一进程内运行多个 `Architecture` 实例，通过 `byte` ID 区分，各自拥有独立的容器和事件总线
- **DataCarrier 子系统**：作为通用的实体-特征数据管理方案，与架构深度绑定

## 架构设计

### 四层抽象

```
Controller          — 表现层（入口）：全权限
    Service         — 服务层：业务逻辑
        Model       — 数据层：数据状态（权限最小）
    Utility         — 工具层：纯功能（无状态）
```

每层通过**显式的 interface 组合**声明权限（而非继承），使得：
- 编译器层面阻止越权访问
- 无需在运行时做权限检查
- 阅读接口声明即可知道类的全部能力

### 核心架构

```plaintext
Application
    │
    ├─ ArchitectureFactory (静态)
    │      注册类型 → 创建实例 → 管理生命周期
    │
    └─ Architecture (抽象基类, 多实例)
           ├─ ArchitectureId: byte
           ├─ State: ArchitectureState
           │
           ├─ IOCContainer
           │     ├─ IService  (服务)
           │     ├─ IModel    (数据)
           │     └─ IUtility  (工具)
           │
           ├─ TypeEventService
           │     └─ Dictionary<Type, List<object>>
           │         支持 Register<T> / Send<T> / Unregister<T>
           │
           ├─ CarrierManager (内置)
           │     └─ 数据载体子系统: CRUD + Trait + 序列化
           │
           └─ Lifecycle Events
                 BeforeInit → OnInit → AfterInit
                 BeforePause → OnPause
                 BeforeResume → OnResume
                 BeforeShutdown → OnShutdown → AfterShutdown
```

### 接口权限体系

```csharp
// 小接口权限层次（从最小到最大）
interface ICanInit { }                        // 可初始化（所有组件都有）
interface ICanGetUtility { }                  // 可获取工具
interface ICanGetModel : IBelongToArchitecture { }
interface ICanGetService : IBelongToArchitecture { }
interface ICanSendEvent : IBelongToArchitecture { }
interface ICanRegisterEvent : IBelongToArchitecture { }
interface ICanSendCommand : IBelongToArchitecture { }
interface ICanSendQuery : IBelongToArchitecture { }
interface ICanGetCarrier : IBelongToArchitecture { }    // 只读
interface ICanCreateCarrier : IBelongToArchitecture { }  // 创建/销毁
interface ICanManageCarrierTrait : ICanGetCarrier { }    // 特征管理

// 四层通过组合小接口定义自己的权限
interface IUtility : ICanInit { }
interface IModel : IBelongToArchitecture, ICanGetUtility, ICanSendEvent, ICanInit { }
interface IService : IModel + ICanGetService + ICanGetModel + ICanRegisterEvent + ICanGetCarrier + ICanCreateCarrier + ICanManageCarrierTrait { }
interface IController : IService + ICanSendCommand + ICanSendQuery { }
interface ICommand<TResult> : ICanGetService + ICanGetModel + ICanGetUtility + ICanSendEvent/Command/Query + ICanGet/CanCreate/ManageCarrierTrait { }
interface IQuery<TResult> : ICanGetModel + ICanGetService + ICanGetUtility + ICanSendQuery + ICanGetCarrier (只读) { }
```

### CQRS 命令查询

```
Command (写操作):     SendCommand<T>(T command) where T : ICommand
Query  (读操作):      SendQuery<TResult>(IQuery<TResult> query)
```

- Command 可访问 Service/Model/Utility，**可修改**数据
- Query 只能访问 Model/Service，**不可修改**数据（只读权限）

### DataCarrier 子系统

#### CarrierId 位域设计

```
63        56|55      40|39                         0
┌──────────┼──────────┼────────────────────────────┐
│ FrameID  │  TypeID  │         UniqueID           │
│  8 bit   │  16 bit  │         40 bit             │
│ (256个)  │(65536种) │     (~1万亿个实例)         │
└──────────┴──────────┴────────────────────────────┘
```

设计理由：
- 64 位对齐体系，无 padding 浪费
- 隐式 `ulong ⇔ CarrierId` 转换，便于序列化和网络传输
- 位运算拆解字段，无额外内存分配

#### CarrierManager

```
CarrierManager
  ├── 类型注册: typeName → ushort typeId (双向映射)
  ├── 载体创建: typeId → CarrierId (自动递增 uniqueId)
  ├── 特征管理: CarrierId → Dictionary<Type, IDataTrait>
  ├── 查询: FindCarriersWithTrait<T>() // 遍历所有载体
  └── 序列化: JSON → File (SaveToFile / LoadFromFile)
```

线程安全：所有容器操作用 `lock` 保护。

### 事件系统

两套事件机制，分别用于不同场景：

| 机制 | 用途 | 特点 |
|------|------|------|
| `TypeEventService` | 架构内部通信 | 按 struct 类型分发，自动泛型匹配 |
| `EasyEvent` 系列 | BindableProperty | 0/1/2/3 泛型参数，支持 `CustomUnRegister` 安全注销 |

## 替代方案

| 方案 | 优点 | 缺点 | 弃用/选用理由 |
|------|------|------|-------------|
| **QFramework 原始设计** | 社区成熟、API 简洁 | 不支持多架构实例，所有组件全局共享 | 参考其小接口模式，但重构为多实例 |
| **StrangeIoC** | 完整的 IoC + MVCS | 重度依赖反射，性能差，Unity 集成复杂 | 弃用：不适合 ECS 场景 |
| **UniRx + 手动分层** | 灵活、Rx 链式编程 | 缺乏架构约束，团队协作难统一 | 弃用：NexusFramework 提供更强制约 |
| **纯 ScriptableObject 架构** | 数据驱动、编辑器友好 | 运行时灵活性差，跨实例共享数据危险 | 弃用：DataCarrier 方案兼顾两者 |
| **NexusFramework (当前)** | 多实例、轻量、CQRS、显式权限 | 新增复杂度（需理解接口组合模式） | **选用**：适合需要多架构隔离的中大型项目 |

## 风险与权衡

| 风险 | 影响 | 缓解措施 |
|------|------|---------|
| 小接口组合过多导致接口爆炸 | 每个新能力都需要创建新接口+扩展方法 | 按功能聚类，保持粒度适中 |
| 扩展方法模式难以被 IDE 发现 | 开发者可能不知道某个类有哪些能力 | 通过接口声明明确列出所有能力 |
| CarrierManager 全局 lock 可能成为瓶颈 | 高频操作下有性能隐忧 | 设计上 CarrierManager 主要用于配置/初始化阶段，运行时高频路径走 ECS |
| 序列化使用 JsonUtility | 不支持多态、字典、复杂类型 | 框架特性限制，可扩展为 Newtonsoft.Json |

## 关联

- 编码实现：[核心框架编码文档](../code/nexusframework-core.md)
- GAS 子系统：[GAS 设计文档](D002-gas-design.md)
- 测试覆盖：[测试计划](../tests/T001-gas-test-suite.md)
