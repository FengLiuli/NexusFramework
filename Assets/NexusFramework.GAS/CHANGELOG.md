# Changelog

本文件记录 `com.nexusframework.gas` 的版本变更。版本号规则见
[D009](https://github.com/FengLiuli/NexusFramework/blob/main/docs/design/D009-upm-package-versioning.md)。

## [2.0.0] - 2026-11-20

首个正式版本。此前没有已发布版本，因此不存在从旧版本迁移的路径。

### Added

- 基于 NexusFramework Core 2.0.0 的 ECS 驱动 Gameplay Ability System。
- 属性（Attribute）、效果（Effect）、能力（Ability）、标签（Tag）、Cue、MMC 的运行时与配置加载链路。

### Changed

- `GASEntityMapModel` 改用 Core 的 Carrier 索引查询。
- Tag 服务适配 Core 的独立 Tag 运行时（`ITagService`）。
- `com.unity.burst` 依赖固定为 1.8.19。
- `package.json` 新增对 `com.nexusframework.core` 的依赖声明。

### Fixed

- 修复 GAS 数据流与 Tag 同步中的状态不一致问题。
