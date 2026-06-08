# 刑部记忆

## [2026-06-08] Entity↔GameObject 绑定单元测试

- **任务**：为 GASEntityMapModel 的 IGASEntityResolver 实现编写单元测试
- **测试文件**：`Assets/Tests/EntityGameObjectBindingTests.cs`，22 个用例
- **覆盖范围**：

| 类别 | 用例数 | 覆盖 |
|------|--------|------|
| 接口可用性 | 1 | Model 实现 IGASEntityResolver |
| 前向查询 | 4 | Bind→Get 正常、未绑定→null、Entity.Null→null、过期 GO→null |
| 反向查询 | 3 | 绑定后正常返回、未绑定→Null、null→Null |
| 检查方法 | 3 | IsEntityBound 前后状态、IsGameObjectBound |
| 解绑 | 2 | 正常解绑后查询为 null、未绑定时不抛异常 |
| 重复/冲突 | 2 | 重复绑定覆盖旧值、同一 GO 绑定不同 Entity 被拒绝 |
| 空值防御 | 2 | Entity.Null 不抛异常、null GO 不抛异常 |
| GASEntityRef | 2 | 自动附加+字段正确、解绑后标记 Entity.Null |
| DestroyGASCarrier | 1 | 自动解绑 |
| 便捷方法 | 2 | BindGameObjectForCarrier、CreateGASCarrier(go) |
| 多架构隔离 | 1 | 两个架构实例绑定独立 |
| OnDestroy 兜底 | 1 | GASEntityRef.OnDestroy 自动解绑 |

- **耗时/状态**：成功
