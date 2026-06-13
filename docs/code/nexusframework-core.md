---
layer: code
status: draft
task: T001
source_files:
  - Assets/NexusFramework/Framework.cs
  - Assets/NexusFramework/FrameworkArchitecture.cs
  - Assets/NexusFramework/ArchitectureFactory.cs
  - Assets/NexusFramework/DataCarrier/CarrierId.cs
  - Assets/NexusFramework/DataCarrier/CarrierManager.cs
  - Assets/NexusFramework/DataCarrier/DataTrait.cs
created: 2026-06-11
updated: 2026-06-11
---

# 编码文档：NexusFramework 核心框架

## 概述

NexusFramework 是一个轻量级、面向 Unity 的多实例架构框架，采用 **分层职责接口** + **IOC 容器** 的设计模式。
支持在同一进程内运行多个独立的 `Architecture` 实例（通过 `ArchitectureId` 区分），每个实例拥有独立的 Service、Model、Utility 注册和 Event 系统。

框架还提供 **DataCarrier（数据载体）** 子系统，作为通用的实体-特征数据管理方案。

## 架构分层

```
┌───────────────────────────────────────────────┐
│                   Controller                    │  IController（最全权限）
│  ┌───────────────────────────────────────────┐ │
│  │               Service                      │ │  IService（服务层）
│  │  ┌─────────────────────────────────────┐  │ │
│  │  │              Model                    │  │ │  IModel（数据层，权限最小）
│  │  │  ┌───────────────────────────────┐  │  │ │
│  │  │  │           Utility             │  │  │ │  IUtility（纯工具）
│  │  │  └───────────────────────────────┘  │  │ │
│  │  └─────────────────────────────────────┘  │ │
│  └───────────────────────────────────────────┘ │
└───────────────────────────────────────────────┘
```

## 文件清单

### Architecture 核心

| 文件 | 职责 |
|------|------|
| `Framework.cs` | 架构状态枚举（`ArchitectureState`）、生命周期事件、**核心接口体系**（`IArchitecture` / `IController` / `IService` / `IModel` / `IQuery` 等）、扩展方法、事件系统（`EasyEvent` 系列）、`BindableProperty` |
| `FrameworkArchitecture.cs` | `Architecture` 抽象基类实现、`IOCContainer` 容器、`TypeEventService` 类型事件服务 |
| `ArchitectureFactory.cs` | 架构工厂（注册/创建/销毁架构实例，支持多实例管理） |

### DataCarrier 子系统

| 文件 | 职责 |
|------|------|
| `DataCarrier/CarrierId.cs` | 64 位紧凑 ID 结构体（8 位框架 ID + 16 位类型 ID + 40 位唯一 ID） |
| `DataCarrier/CarrierManager.cs` | `ICarrierManager` 接口 + `CarrierManager` 实现（类型注册、载体 CRUD、特征管理、序列化） |
| `DataCarrier/DataTrait.cs` | `IDataTrait` 接口 + `DataTrait` / `ScriptableObjectDataTrait` 抽象基类 |

## 关键类/方法

### 核心接口体系 (`Framework.cs`)

#### `IArchitecture`

**职责**：架构核心接口，定义生命周期和组件访问契约。

**关键方法**：

| 方法 | 签名 | 说明 |
|------|------|------|
| `Initialize` | `void Initialize()` | 初始化架构，触发生命周期事件 |
| `Pause/Resume` | `void Pause()/Resume()` | 暂停/恢复架构 |
| `Shutdown` | `void Shutdown()` | 关闭架构，清理所有资源 |
| `RegisterService<T>` | `void RegisterService<T>(T)` | 注册服务组件 |
| `GetService<T>` | `T GetService<T>()` | 获取已注册的服务 |
| `SendCommand<T>` | `void SendCommand<T>(T)` | 发送命令（CQRS 命令模式） |
| `SendQuery<T>` | `TResult SendQuery<TResult>(IQuery<TResult>)` | 发送查询（CQRS 查询模式） |
| `SendEvent<T>` | `void SendEvent<T>(T)` | 发送类型事件 |

#### 权限接口体系

框架设计了多层权限接口，通过**显式接口组合**控制各层职责：

| 接口 | 组合的权限 | 使用方 |
|------|-----------|--------|
| `IUtility` | 无（只有 Init） | 纯工具类 |
| `IModel` | `IGetUtility` + `ISendEvent` + `IInit` | 数据层 |
| `IService` | `IGetModel` + `IGetService` + `IGetUtility` + `IRegisterEvent` + `ISendEvent` + `IInit` + 载体读/写 | 服务层 |
| `ICommand` | `IGetService` + `IGetModel` + `IGetUtility` + `ISendEvent/Command/Query` + 载体读/写 | 命令 |
| `IQuery<T>` | `IGetModel` + `IGetService` + `IGetUtility` + `ISendQuery` + 载体只读 | 查询 |
| `IController` | 所有权限 + 载体读/写/管理 | 表现层 |

#### 小接口组合模式

框架使用 **小接口 + 扩展方法** 模式（参考 QFramework）：
- `ICanGetService`、`ICanSendCommand` 等不含方法声明
- 通过 `CanGetServiceExtension`、`CanSendCommandExtension` 等静态类提供扩展方法
- 子类只需要声明实现哪个小接口，即可获得对应能力

### `Architecture` (`FrameworkArchitecture.cs`)

**职责**：架构抽象基类，实现 `IArchitecture`，提供 IOC 容器和事件服务。

**关键实现**：

| 组件 | 类型 | 说明 |
|------|------|------|
| `mContainer` | `IOCContainer` | 组件注册中心，按类型存储 `ICanInit` 实例 |
| `mTypeEventService` | `TypeEventService` | 按类型分发事件的轻量级事件总线 |
| `mCarrierManager` | `ICarrierManager` | 内置的数据载体管理器 |

### `ArchitectureFactory` (`ArchitectureFactory.cs`)

**职责**：静态工厂，管理架构类型的注册和实例生命周期。

| 方法 | 说明 |
|------|------|
| `RegisterArchitecture<T>(string)` | 注册架构类型 |
| `CreateArchitecture(string, byte?)` | 创建架构实例 |
| `GetArchitecture(byte)` | 根据 ID 获取实例 |
| `DestroyArchitecture(byte)` | 销毁架构实例 |
| `Reset()` | 重置工厂（清理所有实例和注册表） |

### `CarrierId` 位域设计

```
RawValue (64-bit)
┌────────┬─────────────┬──────────────────────────┐
│ 8 bit  │  16 bit     │       40 bit             │
│ FrameID│  TypeID     │      UniqueID            │
└────────┴─────────────┴──────────────────────────┘
```

支持隐式转换 `ulong ⇔ CarrierId`。

### `CarrierManager` 数据载体

**职责**：泛用实体-特征（Entity-Component）数据管理器。

| 功能 | 方法 |
|------|------|
| 类型注册 | `RegisterType` / `IsTypeRegistered` / `GetTypeId` |
| 载体创建 | `CreateCarrier(ushort typeId)` / `CreateCarrier(ushort, ulong)` |
| 特征管理 | `AddTrait<T>` / `GetTrait<T>` / `RemoveTrait<T>` / `HasTrait<T>` |
| 查询 | `FindCarriersWithTrait<T>` / `FindCarriersWithTraits` |
| 序列化 | `SerializeCarrier` / `DeserializeCarrier` / `SaveToFile` / `LoadFromFile` |

所有容器操作使用 `lock` 保证线程安全。

### 事件系统

#### `TypeEventService`
- 基于 `Dictionary<Type, List<object>>` 的轻量级事件总线
- 支持 `Register<T>` / `Send<T>` / `UnRegister<T>`
- 用于架构层内部的事件通信

#### `EasyEvent` 系列
- 泛型事件类：`EasyEvent` / `EasyEvent<T>` / `EasyEvent<T,K>` / `EasyEvent<T,K,S>`
- 通过 `CustomUnRegister` 实现安全的事件注销
- 全局单例管理器 `EasyEvents`（`mGlobalEvents`）
- 用于 `BindableProperty` 的数值变更通知

### `BindableProperty<T>`

**职责**：可监听的属性包装器，支持值变更事件。

- `Value` — 设置值时自动触发事件（带相等性比较）
- `SetValueWithoutEvent` — 不触发事件的赋值
- `Register` / `RegisterWithInitValue` — 监听值变更
- `Comparer` — 可自定义相等性比较器（Unity 基本类型已预注册）

## 依赖关系

```
ArchitectureFactory → 创建/管理 → IArchitecture (多个实例)
       │
       └── Architecture (基类)
              ├── IOCContainer → IService / IModel / IUtility
              ├── TypeEventService → struct 事件
              └── CarrierManager → DataCarrier
```

- `ArchitectureFactory` 独立于 `Architecture` 体系，通过静态 API 管理全局架构映射
- `CarrierManager` 作为内置组件自动注册

## 实现说明

1. **多架构实例隔离**：每个 `Architecture` 实例通过 `byte ArchitectureId` 唯一标识，`CarrierId` 的位域中高 8 位存储框架 ID，支持最多 256 个并发架构实例
2. **线程安全**：`CarrierManager` 的字典和集合操作全部使用 `lock` 保护
3. **事件发放在后**：架构生命周期事件在状态变更 `之后` 发送，确保监听器看到的架构状态与事件类型一致
4. **抽象基类隐藏接口方法**：`AbstractService` / `AbstractCommand` / `AbstractQuery` 等使用 **显式接口实现** + `protected abstract` 模式分离公共 API 和子类扩展点

## 关联

- 设计文档：[核心框架设计](../design/D001-core-framework.md)
- GAS 模块：[GAS 编码文档](nexusframework-gas.md)
