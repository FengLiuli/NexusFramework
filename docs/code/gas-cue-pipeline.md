---
layer: code
status: draft
task: T001
source_files:
  - Assets/NexusFramework.GAS/ECS/Cue/Base/GameplayCueBase.cs
  - Assets/NexusFramework.GAS/ECS/Cue/Base/GameplayCueParameters.cs
  - Assets/NexusFramework.GAS/ECS/Cue/Common/*.cs
  - Assets/NexusFramework.GAS/ECS/Cue/Component/*.cs
  - Assets/NexusFramework.GAS/ECS/System/SCueStart.cs
  - Assets/NexusFramework.GAS/ECS/System/SCueTick.cs
  - Assets/NexusFramework.GAS/ECS/System/SCueEnd.cs
  - Assets/NexusFramework.GAS/ECS/System/SCueDestroy.cs
  - Assets/NexusFramework.GAS/Services/CueService.cs
created: 2026-06-12
updated: 2026-06-12
---

# 编码文档：GAS Cue 管线

## 概述

GameplayCue 负责所有视觉效果、音效、UI 反馈等表现层逻辑。Cue 的触发时机由 GE 配置中的 `CCueOnXxx` 组件决定，生命周期由 ECS System 管线驱动。

## 架构

```
GE 配置中的 CCueOnActivate / CCueOnAdd / CCueOnTick / CCueOnDeactivate / CCueOnRemove
    │
    ▼ 触发
GameplayCueConfig → CueHelper.TryCreateCue → GameplayCueBase
    │
    ├── 创建 ECS Cue Entity
    │   ├── ECCuePlayable (enable/disable 控制播放/停止)
    │   ├── ECCuePlaying (标记正在播放)
    │   ├── ECKillCue (标记需要销毁)
    │   └── MCCue (Cue 类型和参数配置 Buffer)
    │
    ├── SCueStart → ECCuePlayable true → ECCuePlaying true
    ├── SCueTick → 更新 Cue 状态
    ├── SCueEnd → ECCuePlayable false → ECCuePlaying false
    └── SCueDestroy → ECKillCue true → 销毁 Entity
```

---

## GameplayCueBase 生命周期

```csharp
public abstract class GameplayCueBase
{
    // 注入 Entity-GameObject 解析器（在 InitParameters 之前调用）
    public void SetEntityResolver(IGASEntityResolver resolver);

    // 获取目标 ASC 对应的 GameObject（内部处理了 null 检查和过期检测）
    protected GameObject GetTargetAscGameObject();

    // 参数初始化
    public abstract void InitParameters(XParam xParam);

    // ── 生命周期回调 ──
    public virtual void OnAdd(float time);           // Cue 被添加到目标 ASC 时
    public virtual void OnRemove(float time);         // Cue 从目标 ASC 移除时
    public virtual void OnActivate(float time);       // Cue 被激活（播放）时
    public virtual void OnDeactivate(float time);     // Cue 被停止时
    public virtual void OnTick(float time);           // 每帧/每回合 Tick
    public virtual void OnDestroy(float time);        // Cue 被销毁时

    // ── 控制方法 ──
    public void Play(bool replay = false);            // 播放 Cue
    public void Stop(bool immediate = false);         // 停止 Cue
    public void StopImmediate();                      // 立即停止
    public void KillSelf();                           // 标记销毁自己
    public void RemoveSelf();                         // 停止并从目标移除
    public virtual void Reset();                      // 重置状态

    // ── 关联查询 ──
    public Entity GetEffectEntity();                  // 获取来源 GE Entity
}

// 泛型版本（推荐使用）
public abstract class GameplayCueBase<T> : GameplayCueBase where T : XParam
{
    public T Parameter { get; private set; }
}
```

---

## 内置 Cue 实现

### CuePlaySound

```csharp
public class CuePlaySound : GameplayCueBase<XParamPlaySound>
{
    // OnAdd: 加载 AudioClip，查找/创建 AudioSource
    // OnActivate: 播放音效
    // OnTick: 非循环音效播完后自动 RemoveSelf + KillSelf
    // OnDeactivate: 停止播放
    // OnRemove: 清理资源
}

public class XParamPlaySound : XParam
{
    public string AudioClipPath;        // Resources 路径
    public string AudioSourceNodePath;  // AudioSource 挂载节点
    public float Volume = 1f;
    public float Speed = 1f;            // 播放速度（影响 pitch）
    public bool Loop;
}
```

### CueMountPrefab

```csharp
public class CueMountPrefab : GameplayCueBase<XParamMountPrefab>
{
    // OnAdd: 查找挂载点
    // OnActivate: 加载并实例化 Prefab
    // OnTick: 处理延迟销毁
    // OnDeactivate: 停止粒子系统，按配置销毁
    // OnRemove: 强制销毁

    public GameObject Instance { get; }
    public Transform MountPoint { get; }
    public void SetPosition(Vector3 position);
    public void SetRotation(Quaternion rotation);
    public void SetScale(Vector3 scale);
    public void PlayParticles();
    public void StopParticles();
}

public class XParamMountPrefab : XParam
{
    public string PrefabPath;              // Resources 路径
    public string MountPointPath;          // 挂载节点路径
    public Vector3 LocalPosition;
    public Vector3 LocalRotation;
    public Vector3 LocalScale = Vector3.one;
    public bool UseWorldSpace;
    public bool FollowHost;
    public bool DestroyWithHost;
    public bool DestroyOnStop;
    public float DestroyDelay;
    public int Layer = -1;
    public bool RecursiveLayer;
    public int SortingOrder;
    public string SortingLayerName;
    public bool AutoPlayParticle = true;
    public bool StopParticleOnDeactivate = true;
    public ParticleSystemStopAction ParticleStopAction;
}
```

### CuePlayAnimator

```csharp
public class CuePlayAnimator : GameplayCueBase<XParamAnimator>
{
    // 控制目标 ASC GameObject 上的 Animator 组件
}

public class XParamAnimator : XParam
{
    public string StateName;
    public int Layer;
    public float NormalizedTime;
    public float TransitionDuration;
}
```

### CueLog / CueLogging

```csharp
public class CueLog : GameplayCueBase<XParamLogging>
{
    // 在 OnActivate 时输出 Debug.Log
}
```

---

## Cue ECS System 管线

```
SCueStart    — 检测 ECCuePlayable && !ECCuePlaying → OnActivate + Set ECCuePlaying=true
SCueTick     — ECCuePlaying → OnTick
SCueEnd      — !ECCuePlayable && ECCuePlaying → OnDeactivate + Set ECCuePlaying=false
SCueDestroy  — ECKillCue → OnDestroy + OnRemove + DestroyEntity
```

---

## Cue 注册

```csharp
// 方式 1：自动扫描（推荐）
// CueService.ScanAndRegisterAll() 自动扫描 Architecture 所在程序集
// 所有 GameplayCueBase 子类自动注册，注册名为类型名

// 方式 2：手动注册
cueService.RegisterCueType("MyCustomCue", typeof(MyCustomCue), typeof(MyCustomParam));
cueService.RegisterCueType<MyCustomCue>("MyCustomCue", typeof(MyCustomParam));

// 方式 3：通过 CueHelper 直接注册
CueHelper.RegisterCue("MyCustomCue", typeof(MyCustomCue), typeof(MyCustomParam));
```

---

## 关联

- Effect 管线：[GAS Effect 管线](gas-effect-pipeline.md)
- ECS 组件：[GAS ECS 组件清单](gas-ecs-components.md)
- 架构：[GAS 架构与服务层](gas-architecture.md)
- 设计文档：[GAS 设计 - Cue 管线](../design/D002-gas-design.md)
