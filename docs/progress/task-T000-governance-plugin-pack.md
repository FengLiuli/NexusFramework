---
task: T000
task_status: 已完成
created: 2026-06-12
updated: 2026-06-12
sessions: 1
---

# 进度记录：NexusGovernance 插件化打包

> **追加不覆盖**：每次会话在末尾追加新条目，不修改已有条目。

---

## [2026-06-12 19:30] 会话 #1

### 完成
- 完整分析 NexusGovernance 治理系统现状（共 16 个文件：8 个规则书 + 6 个模板 + 1 个 MCP server + 1 个主入口）
- 识别关键问题：两份副本完全一致，`.mcp.json` 硬编码了绝对路径
- 验证关键技术假设：`${CLAUDE_PLUGIN_ROOT}` 在 `.mcp.json` 中受支持、CWD 就是项目根目录、Python 依赖就绪
- 创建 user-level 插件于 `~/.claude/skills/nexus-governance/`
- 迁移所有治理规则、模板到插件内
- 迁移 MCP server 到插件 `bin/` 目录，`server.py` 改用 `os.getcwd()` 定位项目根
- 清理项目中的旧副本：删除 `.claude/skills/governance/` 和 `.claude/nexus-governance/`
- 更新 `.mcp.json`：移除 doc-graph 条目（由插件提供）
- 简化 `CLAUDE.md`：移除硬编码路径，改为 `/nexus-governance` 插件引用

### 决策
- 采用 user-level 插件（非 project-level）：治理系统应跨项目复用，每个项目只需 `docs/` 目录
- `server.py` 用 `os.getcwd()` 替代 `NEXUS_PROJECT_ROOT`：已验证 CWD 就是项目根，无需环境变量
- 主 SKILL.md 直接引用 `skills/governance/` 下的规则文件，不创建 sub-skill：保持单入口

### 下一步
- 在新建项目中测试插件自动加载
- 推广到其他需要治理的项目
- 考虑推送到 GitHub 公开仓库方便安装

### 遇到的问题
- 无
