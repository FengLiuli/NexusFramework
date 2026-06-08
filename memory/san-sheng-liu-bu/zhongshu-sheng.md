# 中书省记忆

## [2026-06-08] ECS Entity↔GameObject 绑定方案设计

- **任务**：解决 `EntityGameObjectBindings` static class 在多 World 下的冲突问题，为 Cue/TargetCatcher 提供安全的 Entity↔GameObject 绑定机制
- **模式**：严谨模式，多方案统合 + 一轮封驳修订 + 合并优化
- **最终方案**：
  - 去 static，合并进已有的 `GASEntityMapModel`（一条 Model 三条映射链：CarrierId↔Entity + Entity↔GameObject）
  - 新建 `IGASEntityResolver` 接口（6方法：Bind/Unbind/GetGameObject/GetEntity/IsBound×2）
  - Cue/TargetCatcher 通过 DI 获取 resolver，写入端收敛到 `GASArchitecture.BindGameObjectForCarrier(carrierId, go)`
  - `EntityGameObjectBindings` 标记 `[Obsolete]`，内部委托给 GASEntityMapModel
- **涉及文件**：新建 1（IGASEntityResolver），修改 10（GASEntityMapModel、GameplayCueBase、CueHelper、GameplayCueConfig、TargetCatcherBase、TargetCatcherHelper、GASArchitecture、Cue 子类×3、CatchAreaBox3D），Obsolete 1（EntityGameObjectBindings）
- **耗时/状态**：成功
