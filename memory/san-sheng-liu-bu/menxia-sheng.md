# 门下省记忆

## [2026-06-08] Entity↔GameObject 绑定方案审查

- **任务**：审查中书省提交的 ECS Entity↔GameObject 绑定方案
- **一审 verdict**：需小修
  - 封驳理由：`_worldToService` 静态注册表是用新全局状态替代旧全局状态；写入端调用者链路缺失
  - 补充建议：去掉 `_worldToService`、明确 Cue 注入时间线、补全契约
- **二审 verdict**：通过（附带 3 个小修建议）
  - 理由：六项封驳全部闭合，合并进 GASEntityMapModel 提高了内聚性
  - 3 个小问题：
    1. 审计所有 cue 创建路径（TryPlayCueOnAsc 不经过 TryCreateCue）
    2. GASEntityMapModel 字段名冲突（_forward/_reverse 已被 CarrierId 映射占用）
    3. GameplayCueConfig 是 Serializable，resolver 应作方法参数而非字段
- **耗时/状态**：成功
