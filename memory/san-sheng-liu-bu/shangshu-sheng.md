# 尚书省记忆

## [2026-06-08] Entity↔GameObject 绑定重构实施

### 新建文件（3 个）

| 文件 | 状态 |
|------|------|
| `ECS/Bridge/IGASEntityResolver.cs` | ✅ 6个方法接口 |
| `ECS/Bridge/GASEntityRef.cs` | ✅ MonoBehaviour + OnDestroy 兜底解绑 |

### 重写文件（2 个）

| 文件 | 状态 |
|------|------|
| `Models/GASEntityMapModel.cs` | ✅ 实现 IGASEntityResolver，新增 _entityToGameObject/_gameObjectToEntity 字典，字段重命名 _carrierToEntity/_entityToCarrier |
| `ECS/Bridge/EntityGameObjectBindings.cs` | ✅ [Obsolete]，内部委托给 GASEntityMapModel |

### 修改文件（8 个）

| 文件 | 状态 |
|------|------|
| `ECS/Cue/Base/GameplayCueBase.cs` | ✅ _entityResolver + GetTargetAscGameObject() |
| `ECS/Cue/CueHelper.cs` | ✅ TryCreateCue 加 resolver 参数 |
| `ECS/Cue/GameplayCueConfig.cs` | ✅ CreateCue 加 resolver 参数 |
| `ECS/Cue/Common/CuePlayAnimator.cs` | ✅ GetTargetAscGameObject() |
| `ECS/Cue/Common/CuePlaySound.cs` | ✅ GetTargetAscGameObject() |
| `ECS/Cue/Common/CueMountPrefab.cs` | ✅ GetTargetAscGameObject() |
| `ECS/Ability/TargetCatcher/TargetCatcherBase.cs` | ✅ _entityResolver + SetEntityResolver |
| `ECS/Ability/TargetCatcher/TargetCatcherHelper.cs` | ✅ TryCreateTargetCatcher 加 resolver 参数 |
| `ECS/Ability/TargetCatcher/CatchAreaBox3D.cs` | ✅ _entityResolver + GASEntityRef 反向查找 |
| `GASArchitecture.cs` | ✅ BindGameObjectForCarrier + CreateGASCarrier(go) + DestroyGASCarrier 自动 Unbind |
| `Effect/Component/Static/ConfCueBase.cs` | ✅ CreateCueEntityArray 加 resolver 参数透传 |
| `KNOWN_ISSUES.md` | ✅ 标记 1.1 已解决 |

### 测试文件（1 个）

| 文件 | 状态 |
|------|------|
| `Tests/EntityGameObjectBindingTests.cs` | ✅ 22 个测试用例，覆盖 Bind/Unbind/Get/重复/冲突/空值/过期/多架构/GASEntityRef自动附加/OnDestroy兜底/DestroyGASCarrier自动解绑/BindGameObjectForCarrier/CreateGASCarrier(go) |

### 全量搜索验证

- 残留 `EntityGameObjectBindings.` 调用：0 处
- Demo 文件夹受影响的文件：0 个

### 3 个门下建议的处理

1. Cue 创建路径审计：✅ `CreateCueEntityArray` 已透传 resolver，`GameplayCueConfig.CreateCue` 已加参数
2. 字段重命名：✅ _forward→_carrierToEntity，_reverse→_entityToCarrier
3. GameplayCueConfig Serializable：✅ resolver 作为方法参数传入，不存字段
