---
name: unity-pipeline
description: Drive a running Unity Editor or development Player from the command line via the unity-pipeline package — run the edit→recompile→run_tests loop, evaluate C#, bulk-construct assets with run_script, and code-reload files at runtime. Use when an agent needs to control a live Unity instance, automate Unity tests, recompile scripts headlessly, or apply runtime reloads. Covers the Unity 2022.3 back-port of the package (meta format, renamed APIs, the ConsoleWindowUtility shim). Assumes the `unity` CLI is on PATH at ~/.local/bin.
---

# Unity Pipeline (agent control)

Invoke commands with `unity command <name> [args]`. Run `unity command` with no name to
list what an instance exposes. Two servers exist: **Editor** (`7800-7849`, auto-starts with
the editor) and **Runtime** (`7900-7949`, only in a dev Player build). See the package
`README.md` "Commands" reference for the full list and parameters.

## 0. Ported packages on Unity 2022.3

`com.unity.pipeline` is published for Unity 6.0+. If a project vendors a hand-ported copy
(check `Packages/com.unity.pipeline/Editor/Console/ConsoleWindowUtilityCompat.cs` — its
presence means the port below is in place), it deviates from upstream in these ways.
Re-apply them if the package is ever re-fetched from Unity's registry:

1. `package.json` — `"unity": "6000.0"` lowered to `"2022.3"`.
2. `Runtime/Plugins/CodeAnalysis/*.dll.meta` — `PluginImporter.serializedVersion` converted
   `3` → `2`. 2022.3 silently refuses to import the v3 metas, which surfaces as ~60
   `CS0246` errors for `MetadataReader`, `PEReader`, `ILOpCode`, etc. Cross-check against
   `Runtime/Analyzers/IlInterpreterAnalyzer.dll.meta`, which ships as v2.
3. `Editor/Commands/Assets/AssetCommands.cs` — `PhysicsMaterial` → `PhysicMaterial`
   (Unity 6 renamed the type; 2022.3 only has the old name).
4. `Editor/Commands/Materials/MaterialCommands.cs` — `mat.rawRenderQueue` → `mat.renderQueue`
   (property added in Unity 6; `renderQueue` returns the effective value on 2022.3).
5. `Editor/PipelineAnalytics.cs` — analytics calls wrapped in `#if UNITY_6000_0_OR_NEWER`.
   2022.3 has no `IAnalytic` / `EditorAnalytics.SendAnalytic(IAnalytic)`. The `#endif` must
   sit *after* the type declarations but *before* the class/namespace closing braces —
   putting it last strands the braces and yields `CS1513`.
6. `Editor/Console/ConsoleWindowUtilityCompat.cs` — **added**. 2022.3 has no
   `UnityEditor.ConsoleWindowUtility` at all, so this shim reimplements it against the
   internal `UnityEditor.LogEntries` via reflection. Verified working: `console_status`
   returns live `groundTruth.consoleErrors/Warnings/Logs`. If those counts read `0` while
   the Console clearly has entries, the reflection lookup broke and this is where to look.

Known upstream bug that is *not* patched: `quit` is marked `RuntimeOnly` and is hidden from
`unity command`'s listing, but the server's execute path never checks that flag — so invoking
it in the Editor reaches a Player-only handler and fails with a `DontDestroyOnLoad ... can
only be used in play mode` error. Don't call `quit` against an Editor instance.

## 1. Install & verify

The `unity` CLI binary lives at `~/.local/bin/unity.exe` (v1.0.0-beta.2). If `unity` is not on
PATH in a fresh shell, add `~/.local/bin` to it.

> **Reinstalling or upgrading the CLI:** every Unity CDN host redirects to
> `public-cdn.cloud.unitychina.cn`, whose `latest-beta.json` manifest is pinned at
> `0.1.0-beta.3` — a build that predates the `pipeline`/`command` subcommands. The binaries
> for newer versions are present on that CDN, only the manifest is stale, so a direct
> version-pinned download works:
> `curl -sL "https://public-cdn.cloud.unitychina.cn/hub/prod/cli/1.0.0-beta.2/unity-windows-x64.exe" -o unity.exe`
> Do not run the official install script without pinning `UNITY_CLI_VERSION`; it will silently
> install the old build.

```bash
unity pipeline list               # confirm the editor instance + server are reachable
unity command editor_status       # confirm the editor server answers
```

> If the package is already vendored in `Packages/`, skip `unity pipeline install` — re-running
> it pulls the Unity 6 build from the registry and undoes the port above.

## 2. Autonomous edit loop (Editor)

This is the core agent workflow: keep the editor alive, change code, recompile, test.

```bash
# 1. Keep the editor ticking even when unfocused/minimized. REQUIRED before headless work —
#    Unity otherwise throttles or stalls update/compile when it isn't the active app.
unity command set_autotick --enable true

# 2. Edit C# source files on disk normally.

# 3. Recompile (async: triggers a domain reload, then poll until done).
unity command recompile
unity command recompile_status    # repeat until "completed" or "up_to_date"
#   Tolerate connection errors while the domain reload is in flight — that is expected.
#   If recompile_status reports failed=true, read its "errors" array and fix before testing.

# 4. (Optional) List available tests without running them.
unity command list_tests --mode editor            # mode: all | editor | playmode

# 5. Run tests. ALWAYS use --async_tests under 2022.3 — see the caveat below.
unity command run_tests --mode editor --filter MyFixture.MyTest --async_tests true
unity command test_status        # poll until status is "completed"
```

`run_tests` modes: `all` | `editor` | `playmode`. `filter_type`: `testName` | `assembly` |
`category`. Abort a run with `unity command cancel_tests`.

> **`--async_tests true` is mandatory here, not an optimization.** `run_tests` is
> `MainThreadRequired`, so a synchronous run holds the main thread until every selected test
> finishes. Once the CLI's 30s budget expires the trigger call fails — and because the main
> thread is still occupied, *every* other command times out too, including `editor_status`.
> The editor looks hung; it recovers on its own when the run ends. `--async_tests` returns in
> ~1s (`result: "running"`), then `test_status` reports the outcome.
> Async mode requires `--mode editor` or `--mode playmode` — it rejects `all`.

> **`--timeout` does not help.** The server honours a command's own `timeout` argument only for
> `eval` / `eval_file` / `run_script`; every other command is bounded at a hard 30s. Do not reach
> for `unity command --timeout 300` to fix a slow command — it has no effect.

> **Known caveat:** when any selected test *fails*, `run_tests` may surface an opaque
> result instead of the failure details. Re-run a narrower `--filter`, or inspect the
> editor's Test Runner / logs to get the real failure.

**Reading exit codes.** They separate "rewrite the invocation" from "the Editor failed", which
is the distinction this loop depends on:

| Exit | Meaning | What to do |
|------|---------|------------|
| `2` | Bad arguments — a misspelled flag, a wrong type, too many positionals. Nothing ran. | Fix the command line and retry. The error names the problem and often suggests the right flag. |
| `6` | The command ran and failed, or the Editor could not service it. | Read the error; retrying the same invocation will not help. |

Do not retry an exit 2 unchanged, and do not rewrite a command line on an exit 6.

## 3. Runtime code reload

Change gameplay code in a **running** game with no domain reload. The game must be live:
enter Editor Play Mode (`unity command editor_play`) or run a dev Player. A
The runtime Pipeline driver (enabled via Project Settings > Pipeline > Runtime) auto-discovers tagged methods on `Awake` (no manual
registration). Mono only — Editor Play Mode and Mono desktop dev builds, not IL2CPP. The token
is auto-injected for local requests.

### `reload_file`

Edit the method body directly; no separate file, no boilerplate.

1. Tag the method `[CodeReload]` on the MonoBehaviour.
2. Edit its body on disk.
3. Apply (re-run to iterate):
   ```bash
   unity command reload_file --filename Assets/Spinner.cs
   ```
   Add `--pdb` to make it debuggable — emits a portable PDB mapped to your source so breakpoints in
   the original file bind (attach the IDE + enable Editor Attaching; compiles unoptimized):
   ```bash
   unity command reload_file --filename Assets/Spinner.cs --pdb
   ```

Constraints: `void` instance methods only; **public** members only; debugging requires `--pdb`
(the default emits no symbols).

`unity command codereload_status` shows active overrides. `reload_file` options: `--timeout <ms>`
(default 30000), `--assemblyDir <dir>` (persist DLLs instead of in-memory);
`cleanup_codereload --assemblyDir <dir>` clears old DLLs.

## 4. Bulk construction: `run_script` (the builder pattern)

For bulk work — creating many objects, wiring fields, generating content — put the code in a
**versioned project script** and run a named static entry point with `run_script`. No domain
reload, no code carried through the protocol; iterating costs an in-memory compile (< ~2s), not
a 15–20s recompile.

```bash
# 1. Write the builder OUTSIDE Assets/ (so the write triggers no asset import / domain reload),
#    e.g. AgentScripts/Build.cs:  public static class Build { public static int All() { ... } }
# 2. Run it — relative paths resolve against the project root (the parent of Assets/):
unity command run_script --file AgentScripts/Build.cs --entry Build.All
# 3. Iterate: edit the file, re-run. Useful extras:
unity command run_script --file AgentScripts/Build.cs --dry_run true   # compile-only check: diagnostics, nothing loaded or executed
unity command run_script --file AgentScripts/Build.cs --entry Build.All --args '[3, "Green"]'
```

Rules of thumb:

- **Code goes in files on disk via `run_script`; `eval` is for genuinely ad-hoc one-liners.**
  Never ship multi-line escaped C# strings through `eval` — write the file, run the entry.
- Compiles see the project's **active editor defines** (`UNITY_EDITOR`, version/platform symbols);
  `--defines` appends extra symbols on top.
- Entry points may be `async Task`/`Task<T>` — they are awaited asynchronously (the editor keeps
  pumping, so awaits resuming on Unity's context work naturally) and `Task<T>.Result` is returned.
  The wait is bounded by `timeout_ms`; on expiry the task keeps running detached.
- Runtime exceptions come back with `file:line` mapped to your source (a source-mapped PDB is
  always emitted for executing runs; they compile unoptimized).
- `--mode hotpatch` instead applies `[CodeReload]` in-place method replacements (delegates to
  `reload_file`); `entry`/`args`/`dry_run` are rejected there and `references`/`defines` don't apply.

## 5. Quick C# eval (Runtime)

For genuinely ad-hoc one-liners only — anything longer belongs in a file run via `run_script`.

```bash
unity command eval "return 2 + 2;"
# Or evaluate a .cs file on disk (contents run through the same path):
unity command eval_file Assets/Scratch.cs
```

## 5. Project audit (Editor)

Static-analysis scan via Project Auditor, producing a CSV of issues to fix. Trigger, poll, read.

```bash
unity command audit                              # optional: --categories Code,ProjectSetting --output my.csv
unity command audit_status                       # repeat until terminal status
```

`audit_status` is terminal on `completed` (with `csvPath` + `issueCount`), `failed`, `unavailable`, or
`interrupted` (a domain reload killed the scan — just re-run `audit`). Polling stays responsive while a
Code-category scan compiles assemblies and holds the main thread. One scan at a time: a second `audit`
returns `busy`. There is no cancel — stop polling to abandon a scan.

CSV columns: `Category, Severity, Areas, Description, RelativePath, Line, DescriptorId, Recommendation`
(diagnostics only, so every row is something to fix; `Recommendation` says how).

> **Requires Project Auditor plus its rules.** `unavailable` means the Editor has no Project Auditor,
> or it has no analysis rules — in a built-in-module Editor the rules live in the separate
> `com.unity.project-auditor-rules` package (`unity command package_add --identifier
> com.unity.project-auditor-rules --confirm true`). Read the `message` field; the command never reports
> an empty `completed` scan that would look like a clean project.

## Gotchas

- **`set_autotick` first.** Without it, recompile and tests can hang while the editor is
  unfocused. The package's watchdog relies on the tick loop staying alive.
- **A stuck command may mean a modal dialog is open**, not a hang — a dialog blocks the main
  thread until dismissed. If a command runs long, check `unity command editor_status` (answers
  instantly even when blocked); `status: "blocked_by_dialog"` means stop retrying and tell the
  human what's blocking (its `dialog.title`/`message`/`buttons`) — it can't be clicked over CLI.
- **Code reload needs the game running.** `reload_file` applies to a live
  game — enter Editor Play Mode (`unity command editor_play`) first, or run a dev Player.
- **Player-only commands need a dev Player.** `log`, `set_timescale`, `runtime_status`, etc. hit
  the Runtime server, which does not run in the Editor.
- **Async commands poll.** `recompile`→`recompile_status`, `run_tests --async_tests`→
  `test_status`. Never assume completion from the trigger call's response.
- **Target a specific instance** with `--instance host:port` or `--project-path <path>`
  when more than one is running.
