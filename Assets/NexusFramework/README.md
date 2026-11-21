# NexusFramework Core

`com.nexusframework.core` —— 模块化 MVCS + CQRS 游戏框架核心，提供架构生命周期、服务/模型容器、事件、数据载体（Carrier / Trait）、Tag 与 Identity 子系统。

## 安装

要求 Unity 2022.3.0f1 或更高版本。UPM 只支持精确版本，不支持 `^`、`~` 或范围表达式。

在宿主工程的 `Packages/manifest.json` 中添加：

```json
{
  "dependencies": {
    "com.nexusframework.core": "https://github.com/FengLiuli/NexusFramework.git?path=/Assets/NexusFramework#core-v2.0.0"
  }
}
```

将 `#core-v2.0.0` 替换为目标版本 tag（命名规则：`core-v<version>`）。同一仓库的 GAS 包必须单独声明，UPM 不会根据 `?path=` 自动解析同仓库依赖。

## 依赖与要求

| 项 | 要求 |
|---|---|
| Unity | 2022.3.0f1 或更高 |
| 第三方依赖 | 无 |

## 最小示例

以下代码可直接放在宿主工程的普通脚本目录中编译。

```csharp
using NexusFramework;
using NexusFramework.DataCarrier;
using UnityEngine;

[System.Serializable]
public sealed class PositionTrait : DataTrait
{
    public Vector3 Position;
}

public sealed class PlayerModel : AbstractModel
{
    public int PlayerCarrier { get; set; }

    protected override void OnInit() { }
}

public sealed class PlayerService : AbstractService
{
    public int PlayerCarrier { get; private set; }

    protected override void OnInit()
    {
        var typeId = this.RegisterCarrierType("core:player");
        PlayerCarrier = this.CreateCarrier(typeId);
        this.AddTrait(PlayerCarrier, new PositionTrait { Position = Vector3.one });
        this.GetModel<PlayerModel>().PlayerCarrier = PlayerCarrier;
    }

    protected override void OnDeinit() { }
}

public sealed class GameArchitecture : Architecture
{
    public override string ArchitectureType => "Game";

    public GameArchitecture(byte instanceId) : base(instanceId) { }

    protected override void OnInit()
    {
        RegisterModel(new PlayerModel());
        RegisterService(new PlayerService());
    }
}

public sealed class GameBootstrap : MonoBehaviour
{
    private IArchitecture mArchitecture;
    private IUnRegister mTagSubscription;

    private void Awake()
    {
        ArchitectureFactory.RegisterArchitecture<GameArchitecture>("Game");
        mArchitecture = ArchitectureFactory.CreateArchitecture("Game");
        if (mArchitecture == null) return;

        mTagSubscription = mArchitecture.RegisterEvent<CarrierTagAddedEvent>(
            e => Debug.Log($"carrier {e.Carrier} +tag {e.Key}"));

        var service = mArchitecture.GetService<PlayerService>();
        var trait = mArchitecture.GetCarrierManager()
            .GetTrait<PositionTrait>(service.PlayerCarrier);
        Debug.Log(trait.Position);

        mArchitecture.GetTagService().AddTag(service.PlayerCarrier, "entity.player");
    }

    private void OnDestroy()
    {
        mTagSubscription?.UnRegister();
        if (mArchitecture != null)
            ArchitectureFactory.DestroyArchitecture(mArchitecture.ArchitectureId);
    }
}
```

## 核心概念

- **Architecture**：服务、模型与工具的顶层容器，负责生命周期（Initialize / Pause / Resume / Shutdown）与事件分发。
- **Service / Model / Utility**：按权限分层注册的业务组件，统一实现 `ICanInit`，由容器在架构初始化时按顺序初始化。
- **Carrier / Trait**：多实例数据容器。Carrier 实例是架构内唯一的 `int` 句柄，Trait 按 `typeof(T)` 精确挂载。
- **Tag**：点分层字符串（如 `entity.player`），支持父子层级匹配；由 `ITagService` 独立管理。
- **Identity**：架构内命名空间化的 ID 分配（`KeyIndex` / `IdAllocator` / `TypedAllocator`），由 `IArchitecture.GetIdRegistry()` 获取。

## 关键约束

- `ArchitectureFactory` 通过 `Activator.CreateInstance(type, byte)` 创建架构，子类必须有 `public` 的 `(byte instanceId)` 构造函数。
- `CreateArchitecture` 返回前已经调用 `Initialize()`；直接 `new` 架构则必须自行调用 `Initialize()`。
- Carrier 实例是 `int` 句柄，只在所属架构内有效，`0` 表示无效；不同架构的 id 可以重复。
- Carrier 类型键是命名空间化字符串（如 `core:player`）；Tag 是点分层字符串（如 `entity.player`）。
- `CarrierManager` 与 `ITagService` 不实现 `IService`，必须通过 `GetCarrierManager()` / `GetTagService()` 获取。
- `ArchitectureAfterShutdownEvent` 在监听器清理之后发送，实际不可观测；需要收尾逻辑时使用 `ArchitectureBeforeShutdownEvent`。

## 文档与链接

- [CHANGELOG.md](./CHANGELOG.md)
- [核心框架设计 D001](https://github.com/FengLiuli/NexusFramework/blob/main/docs/design/D001-core-framework.md)
- [Carrier 索引注册 D005](https://github.com/FengLiuli/NexusFramework/blob/main/docs/design/D005-carrier-index-registry.md)
- [Identity 接口设计 D008](https://github.com/FengLiuli/NexusFramework/blob/main/docs/design/D008-identity-interface-design.md)
- [包版本策略 D009](https://github.com/FengLiuli/NexusFramework/blob/main/docs/design/D009-upm-package-versioning.md)

## 过渡 API

<!-- BEGIN TRANSITIONAL APIS（由扫描器生成，勿手改） -->

## Transitional APIs

| Deprecated API | Replacement | Since | Remove in |
|---|---|---|---|

<!-- END TRANSITIONAL APIS -->

## 与关联包的兼容关系

| 关联包 | 版本 | 说明 |
|---|---|---|
| `com.nexusframework.gas` | 2.0.0 | 依赖本包 2.0.0；Unity 2022.3 |
