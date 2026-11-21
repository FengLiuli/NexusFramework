# Changelog

本文件记录 `com.nexusframework.core` 的版本变更。版本号规则见
[D009](https://github.com/FengLiuli/NexusFramework/blob/main/docs/design/D009-upm-package-versioning.md)。

## [2.0.0] - 2026-11-20

首个正式版本。此前没有已发布版本，因此不存在从旧版本迁移的路径。

### Added

- Identity 子系统：命名空间化标识、KeyIndex、IdAllocator、TypedAllocator。
- Carrier 索引注册机制：`CarrierIndex`、`CarrierGroupIndex`、`TagRuntimeIndex`。
- `ArchitectureFactory` 架构 ID 分配器与 Carrier 命名空间隔离。
- 独立 Tag 运行时：`ITagService`、`CarrierTagService`。
- 过渡 API 基础设施：`TransitionalApiAttribute`。

### Changed

- 删除 `CarrierId`，Carrier 改用命名空间化字符串键标识。
- Tag 职责从 `CarrierManager` 剥离，改由 `ITagService` / `CarrierTagService` 承担。
- `CarrierManager` 内部结构与查询路径重构。
- `Framework` / `FrameworkArchitecture` 的服务注册与访问路径调整。

### Fixed

- 修复 Trait 反序列化顺序导致的状态丢失。
