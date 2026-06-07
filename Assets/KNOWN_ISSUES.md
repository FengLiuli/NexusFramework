# 已知问题与待办事项

> 以下问题在完成项目迁移至独立仓库后需要处理。
> 迁移前已完成的清理: 移除 Sirenix.OdinInspector 依赖、清理旧项目名引用。

---

## 一、未实现功能 (TODO)

### 1.1 Entity ↔ GameObject 桥接未注入

文件位于 `Assets/NexusFramework.GAS/ECS/`：

| 文件 | 行号 | 问题 |
|---|---|---|
| `Cue/Common/CuePlaySound.cs` | 26 | Entity → GameObject 解析未接入架构注入 |
| `Cue/Common/CuePlayAnimator.cs` | 13 | 同上 |
| `Cue/Common/CueMountPrefab.cs` | 40 | 同上 |
| `Ability/TargetCatcher/CatchAreaBox3D.cs` | 25 | Entity → Transform 解析未接入架构注入 |

所有桥接方法均暂存在 `GASInternalBridge` 中，需通过 Architecture 注入正式实现。

### 1.2 ConfigLoader 功能不完整

| 文件 | 行号 | 问题 |
|---|---|---|
| `Ability/Component/AbilityLogic/CommonAbilityLogic/ALApplyEffect.cs` | 21 | 效果配置查找未接入 IConfigLoader |
| `Cue/CueHelper.cs` | 40, 62 | Cue 类型码查询和参数类型查询未实现（返回默认值/null） |
| `Config/JsonConfigLoader.cs` | 24 | JSON 反序列化仍未实现 |

### 1.3 效果系统功能占位

| 文件 | 行号 | 问题 |
|---|---|---|
| `System/GameplayEffect/Operation/CheckApply/SCheckImmunityTags.cs` | 42 | 免疫 Cue 触发逻辑未实现 |
| `System/GameplayEffect/Operation/CheckApply/SCheckApplicationCondition.cs` | 23 | 应用条件判断逻辑未实现 |

### 1.4 GeneralGasChoiceHelper 不完整

`Config/GeneralGasChoiceHelper.cs` — `AttrSets()` 方法返回空列表，属性集下拉选项尚未填充。

---

## 二、已知限制与技术债务

### 2.1 外部依赖

| 模块 | 依赖 | 备注 |
|---|---|---|
| NexusFramework (Core) | 无 | 仅依赖 Unity 引擎，可独立使用 |
| NexusFramework.GAS | Unity.Entities, Unity.Mathematics, Unity.Collections, Unity.Burst | DOTS 包，需要 Unity 2022.3 + Entities 包 |
| NexusFramework.GAS.Editor | NexusFramework.GAS + Unity Editor API | 仅 Editor 平台 |

### 2.2 序列化与配置

- 数据配置依赖 Luban (Excel → JSON) 外部工具链，框架本身不包含配置导出逻辑
- `GASSettingAsset.cs` 中 `DEFAULT_CONFIG_PROJECT_PATH` 已改为通用占位值 `Config/exgas_config`，迁移后需根据实际仓库路径调整
- Luban 配置文件的 `Datas/`、`gen.bat`、`gen.sh` 不在框架包内

### 2.3 编辑器工具

- `GASSettingsWindow.cs` 使用原生 `EditorWindow` + `SerializedObject`，无第三方依赖
- `GASSettingAsset.cs` 使用 `ScriptableObject` 存储设置

### 2.4 测试覆盖

- 测试位于 `Assets/Tests/` 目录，迁移时需一并搬运
- 测试依赖 `Unity Test Framework` + `nunit.framework.dll`
- `Tests.asmdef` 引用了 NexusFramework、NexusFramework.GAS、NexusFramework.GAS.Editor

---

## 三、建议的后续开发路径

1. **项目迁移**
   - 创建独立 Git 仓库
   - 搬运 `Assets/NexusFramework/` 和 `Assets/NexusFramework.GAS/`
   - 搬运测试目录 `Assets/_Test/`
   - 重建 Unity 项目并安装所需 DOTS 包

2. **功能补全**
   - 实现 `IConfigLoader` 完整管线
   - 实现 Entity ↔ GameObject 桥接注入
   - 补全 `SCheckImmunityTags`、`SCheckApplicationCondition` 等占位逻辑
   - 实现 `GeneralGasChoiceHelper.AttrSets()`

3. **质量提升**
   - 补充单元测试
   - 添加 CI/CD 流程
   - 编写英文 README 和 API 文档
