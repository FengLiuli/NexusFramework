---
name: luban-runtime-load
description: Guides Luban runtime Tables loading patterns for C#/other languages. Use when integrating generated code, choosing loader, or hot-reload/testing config load.
---

<!-- upstream: focus-creative-games/luban @ v5.1.0 | ai/skills/luban-runtime-load/SKILL.md | 本地适配见文末 -->

# Luban: 运行时加载

## 推荐形态

```csharp
var tables = new cfg.Tables(loader);
var item = tables.TbItem.Get(1001);
```

- 一个 `Tables` 聚合所有表。
- 避免每张表静态全局单例（热更/测试困难）。

## Agent 检查点

1. `-c` 与 `-d` 格式匹配（如 `cs-bin`+`bin`，`cs-simple-json`+`json`）。
2. `topModule` / 命名空间与工程一致。
3. loader 指向实际数据目录；Unity 注意 StreamingAssets/Addressables 约定。
4. 客户端不要加载仅 `s` group 的字段/表。

## 参考

- 运行时加载、生成目标、code-style 文档
- 示例：`luban_examples/Projects`

## 本项目适配（NexusFramework）

**本项目实际情况**

- 加载入口已经存在，由编辑器生成器自动维护：
  `Assets/Demo/Emberheart/GAS/Generated/LubanConfigLoader.cs`（**生成文件，不要手改**）。

  ```csharp
  new cfg.Tables(loader)               // loader: Func<string, JSONNode>
  Tables.Tbasc.Get(id)                 // 表访问
  LubanConfigLoader.LoadTablesForEditor(jsonDir)   // Editor 下从本地 JSON 加载
  ```

- JSON 数据目录：`Assets/DataGenerated/Luban/Json/GAS/`（由 `GASSettingAsset.TableOutputPath` 指定）。
  本项目**未使用** StreamingAssets / Addressables 加载配置，走的是本地 JSON 文件。
- `-c` / `-d` 配对：本项目用 `cs-simple-json` + `json`（**不是** `cs-bin` + `bin`）。
- 桥接层把 `cfg.*` 转成 GAS 运行时配置：`LubanConfigLoader` 实现 `IConfigLoader`，
  通过 `RegisterAllConfigTo(ConfigModel)` 批量注册，再由 `GASArchitecture.CreateGASCarrier(type, ascId, go)`
  读取 `AscConfigData` / `AttrSetDef` 初始化 ECS Entity。
- 改表后若新增/改名了表或字段，**必须**重跑 `NF.GAS/Generate/LubanConfigLoader`，
  否则 `LubanConfigLoader.cs` 会因找不到 `cfg.*` 成员而编译失败。
- 分组语义：本项目 `luban.conf` 里 `c`/`s`/`e` 三个 group 均为 `default`；`gen.bat` 走 `-t client`，因此服务端 `s` 字段不进客户端产物。
