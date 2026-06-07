# NexusFramework 开发规则与约定

本文档描述了 NexusFramework 框架的开发规则、结构和约定，以便开发者能够遵循统一的标准进行开发。

## 1. 框架概述

NexusFramework 是一个基于 Unity 的模块化框架，采用了类似 ECS 和 MVC 的设计理念。它包含以下核心概念：

- **架构 (Architecture)**: 服务的顶层容器，负责管理服务的各个组件
- **载体 (Carrier)**: 数据实体容器，用于承载各种特征数据。用于存储多实例数据，可以附加任意数量与类型的数据特征
- **特征 (Trait)**: 类似于 ECS 中的 Component，是附加到载体上的数据。
- **服务 (Service)**: 处理业务逻辑的组件，均为被动相应类，不可以是Mono脚本，不会主动运行
- **模型 (Model)**: 数据管理和存储组件，用于存储全局的单例数据，多实例数据请使用数据载体
- **控制器 (Controller)**: 用户输入处理和表现层协调组件，可以为Mono脚本，可以借助Unity时间函数运行，是程序与外部的交互口
- **命令 (Command)**: 执行特定操作的指令对象，请将可能服用的操作尽可能的封装为命令
- **查询 (Query)**: 获取数据的操作对象
- **工具 (Utility)**: 提供通用功能的工具类

## 2. 框架结构

```
NexusFramework/
├── DataCarrier/
│   ├── CarrierId.cs           # 载体ID结构定义
│   ├── CarrierManager.cs      # 载体管理器实现
│   └── DataTrait.cs           # 特征基类和接口定义
├── ArchitectureFactory.cs     # 架构工厂类
├── Framework.cs              # 框架核心接口和基类
├── FrameworkArchitecture.cs  # 架构基类实现
└── NexusFramework.asmdef     # 框架程序集定义
```

## 3. 核心组件规范

### 3.1 架构 (Architecture)

所有架构必须继承自 `Architecture` 抽象类 (FrameworkArchitecture.cs)，并实现以下要求：

1. 必须有一个带参数的构造函数，接受一个 byte 类型的 instanceId
2. 必须实现 `ArchitectureType` 属性 (Framework.cs)，返回架构类型的字符串标识
3. 必须实现 `OnInit()` 方法 (FrameworkArchitecture.cs)，用于初始化架构内的组件

示例：
```csharp
public class GameArchitecture : Architecture
{
    public override string ArchitectureType => "Game";
    
    public GameArchitecture(byte instanceId) : base(instanceId)
    {
    }
    
    protected override void OnInit()
    {
        // 注册服务、模型等组件
    }
}
```

### 3.2 载体管理 (Carrier Management)

载体是数据实体容器，具有以下特点：

1. 使用 `CarrierId` 结构 (CarrierId.cs) 作为唯一标识，包含框架ID、类型ID和唯一ID三部分
2. 通过 `ICarrierManager` 接口 (CarrierManager.cs) 管理所有载体
3. 载体可以附加多种特征 (Trait)

#### 3.2.1 CarrierId 结构

`CarrierId` (CarrierId.cs) 是一个64位结构，分为三个部分：
- 高8位：框架ID (256个框架)
- 中16位：类型ID (65536种类型)
- 低40位：唯一ID (1万亿个实例)

#### 3.2.2 特征 (Trait)

特征是附加到载体上的数据组件，类似于 ECS 中的 Component。所有特征必须：

1. 实现 `IDataTrait` 接口或继承 `DataTrait` 抽象类 (DataTrait.cs)
2. 标记为 [Serializable]
3. 提供序列化和反序列化方法

示例：
```csharp
[Serializable]
public class PositionTrait : DataTrait
{
    public Vector3 position;
    
    public PositionTrait()
    {
    }
    
    public PositionTrait(Vector3 pos)
    {
        position = pos;
    }
}
```

### 3.3 服务组件规范

框架中有多种服务组件类型，每种都有特定的职责和访问权限：

#### 3.3.1 服务 (Service)

- 接口: `IService` (Framework.cs)
- 基类: `AbstractService` (Framework.cs)
- 职责: 处理业务逻辑
- 权限: 可以获取Model、Utility，发送Event，可以初始化，可以完全访问载体服务

#### 3.3.2 模型 (Model)

- 接口: `IModel` (Framework.cs)
- 基类: `AbstractModel` (Framework.cs)
- 职责: 数据管理和存储
- 权限: 只能获取Utility，发送Event，可以初始化

#### 3.3.3 控制器 (Controller)

- 接口: `IController` (Framework.cs)
- 职责: 处理用户输入和表现层协调
- 权限: 完全访问所有服务组件和载体服务

#### 3.3.4 命令 (Command)

- 接口: `ICommand` 或 `ICommand<TResult>` (Framework.cs)
- 基类: `AbstractCommand` 或 `AbstractCommand<TResult>` (Framework.cs)
- 职责: 执行特定操作
- 权限: 完全访问所有服务组件和载体服务

#### 3.3.5 查询 (Query)

- 接口: `IQuery<TResult>` (Framework.cs)
- 基类: `AbstractQuery<T>` (Framework.cs)
- 职责: 获取数据
- 权限: 只能获取Service、Model、Utility，只能读取载体数据

#### 3.3.6 工具 (Utility)

- 接口: `IUtility` (Framework.cs)
- 基类: 无特定基类要求
- 职责: 提供通用功能
- 权限: 最基础组件，不直接访问其他组件

### 3.4 事件服务

框架提供了类型安全的事件服务：

1. 发送事件: `SendEvent<T>(T e)`
2. 注册事件: `RegisterEvent<T>(Action<T> onEvent)`
3. 注销事件: `UnregisterEvent<T>(Action<T> onEvent)`

所有事件都是结构体类型，确保性能和类型安全。

框架还提供了生命周期事件，可以在架构的不同阶段监听：

1. `ArchitectureBeforeInitEvent` - 架构初始化之前触发
2. `ArchitectureAfterInitEvent` - 架构初始化完成之后触发
3. `ArchitectureBeforePauseEvent` - 架构暂停之前触发
4. `ArchitectureBeforeResumeEvent` - 架构恢复之前触发
5. `ArchitectureBeforeShutdownEvent` - 架构关闭之前触发
6. `ArchitectureAfterShutdownEvent` - 架构关闭之后触发

示例：
```csharp
// 在任何实现了 ICanRegisterEvent 接口的组件中注册事件
this.RegisterEvent<ArchitectureAfterInitEvent>(e => {
    // 架构初始化完成后执行的逻辑
    // 可以通过 e.ArchitectureId 区分不同的架构实例
});
```

### 3.5 可绑定属性

框架提供了 `BindableProperty<T>` 类 (Framework.cs)，用于创建可观察的属性变化：

```csharp
public class MyModel : AbstractModel
{
    public BindableProperty<int> score = new(0);
    
    protected override void OnInit()
    {
        score.Register(newScore => {
            // 处理分数变化
        });
    }
}
```

## 4. 架构工厂 (ArchitectureFactory)

`ArchitectureFactory` 类 (ArchitectureFactory.cs) 用于管理架构的注册和创建：

1. 注册架构类型: `RegisterArchitecture<T>(string typeName)`
2. 创建架构实例: `CreateArchitecture(string typeName, byte? instanceId)`
3. 获取架构实例: `GetArchitecture<T>(byte architectureId)`
4. 销毁架构实例: `DestroyArchitecture(byte architectureId)`

## 5. 编码规范

### 5.1 命名规范

1. 接口名使用大写字母 'I' 开头，后跟帕斯卡命名法 (如: IService, IModel)
2. 抽象类名使用 'Abstract' 开头 (如: AbstractService)
3. 具体实现类名描述其功能 (如: GameManager, PlayerModel)
4. 私有字段使用驼峰命名法，前面加 'm' 前缀 (如: mIsInitialized)

### 5.2 注释规范

1. 所有公共类型和成员必须有 XML 文档注释
2. 注释描述用途、参数和返回值
3. 公共 API 注释建议中/英文均可，与项目团队语言习惯一致

### 5.3 依赖关系

组件间的依赖遵循以下原则：

```
Controller > Command/Query > Service > Model > Utility
     \         \             \         \       \
      \         \             \         \       \
       \         \             \         \       \
        ------> Carrier Management <-------------
```

箭头表示访问权限方向，左边的组件可以访问右边的组件。

## 6. 最佳实践

1. **单一职责原则**: 每个组件应该只有一个改变的理由
2. **依赖倒置原则**: 组件之间通过接口交互，而不是具体实现。所有可替换组件均需使用接口交互，关键点在于组件功能是否存在迭代可能性
3. **开闭原则**: 对扩展开放，对修改关闭
4. **使用扩展方法**: 利用框架提供的扩展方法简化代码
5. **合理使用载体服务**: 将相关数据根据单例与否组织成模型或载体和特征的形式
6. **事件预留**: 组件间交互根据情况选用使用接口定义方法或事件。在关键节点预留事件
7. **避免过度工程化**: 根据实际需求设计组件复杂度，在权限控制精度与使用复杂度之间取得平衡，防止增加不必要的学习和维护成本
8. **合理选择数据存储方式**: 载体服务适用于需要复杂查询和管理的实体数据，对于瞬时数据或简单数据结构，应优先考虑使用传统数据结构（如List、Dictionary等）
9. **关注性能**: 对于高频操作，应考虑性能影响，避免不必要的对象创建和复杂查询
10. **模块化注册**: 当有多个同功能模块的组件需要被注册到框架中时，应只注册功能模块主体，将其他组件放置到功能模块初始化中进行注册，以保持框架代码可读性

## 7. 示例用法

### 7.1 创建和注册架构

```csharp
// 在应用程序启动时注册架构类型
ArchitectureFactory.RegisterArchitecture<GameArchitecture>("Game");

// 创建架构实例
var gameArch = ArchitectureFactory.CreateArchitecture("Game", 1) as GameArchitecture;
```

### 7.2 创建服务组件

```csharp
public class GameService : AbstractService
{
    protected override void OnInit()
    {
        // 服务初始化逻辑
    }
}

// 在架构中注册服务
gameArch.RegisterService(new GameService());
```

### 7.3 使用载体和特征

```csharp
// 创建载体类型
var playerType = this.RegisterCarrierType("Player");

// 创建载体
var playerCarrier = this.CreateCarrier(playerType);

// 添加特征
var positionTrait = new PositionTrait(new Vector3(0, 0, 0));
this.AddTrait(playerCarrier, positionTrait);

// 查询特征
var position = this.GetTrait<PositionTrait>(playerCarrier);
```
