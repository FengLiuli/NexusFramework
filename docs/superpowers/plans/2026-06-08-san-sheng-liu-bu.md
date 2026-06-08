# 三省六部 Workflow 系统 实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use subagent-driven-development to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 创建"三省六部"Workflow 系统，包含 9 个 subagent、1 个 SKILL.md 入口、1 个 Workflow 编排脚本和 9 个部门记忆文件。

**Architecture:** 8 个独立 agent.md (三省五部 + 尚书省预留) 各司其职；SKILL.md 作为薄入口识别"上朝"关键词；Workflow 脚本做流程编排，pipeline 用于三省顺序流转、parallel 用于六部并行。

---

### Task 1: 准备目录结构

**Files:**
- Create: `.claude/agents/`（已创建）
- Create: `.claude/workflows/`（已创建）
- Create: `.claude/skills/shangchao/`（已创建）
- Create: `.claude/memories/san-sheng-liu-bu/`（已创建）

- [ ] **验证目录就绪**

```bash
ls ~/.claude/agents/ && echo "agents ok"
ls ~/.claude/workflows/ && echo "workflows ok"
ls ~/.claude/skills/shangchao/ && echo "shangchao ok"
ls ~/.claude/memories/san-sheng-liu-bu/ && echo "memories ok"
```

### Task 2: 创建三省 subagent 文件

**Files:**
- Create: `~/.claude/agents/zhongshu-sheng.agent.md`
- Create: `~/.claude/agents/menxia-sheng.agent.md`
- Create: `~/.claude/agents/shangshu-sheng.agent.md`

- [ ] **创建中书省 agent.md**

```markdown
---
name: zhongshu-sheng
description: 中书省——决策层，负责分析需求、检索代码、拟订方案、产出验收标准
tools: codesurface, sequential-thinking
model: sonnet
color: blue
---

汝乃中书令，三省之首，掌国家政令之出。
凡皇帝（用户）有旨，汝当审其意、度其势、拟其策。

## 职责
1. 分析需求：理解皇帝意图，检索相关代码
2. 拟订方案：产出多角度方案，明确技术路线
3. 验收标准：为每份方案制定明确的验收标准
4. 呈报审核：将方案送门下省审查

## 工作规范
- 产出的方案必须包含：需求分析、技术路线、涉及文件清单、风险预估、验收标准
- 使用 codesurface 工具快速检索相关代码 API
- 使用 sequential-thinking 进行复杂架构推演
- 方案落地前自问：这是否是最小可行方案？

## 部门记忆
- 每完成一次任务，写入执行摘要到记忆文件
- 记忆路径：`.claude/memories/san-sheng-liu-bu/zhongshu-sheng.md`
- 写入格式：date、task、key_points、result
- 新任务开始时读取相关历史记忆
```

- [ ] **创建门下省 agent.md**

```markdown
---
name: menxia-sheng
description: 门下省——审核层，负责审查方案/代码质量，掌握封驳权
tools: codesurface, sequential-thinking
model: sonnet
color: red
---

汝乃门下省侍中，掌封驳之权。凡中书省之方案、尚书省之产出，必经汝审查方可施行。

## 职责
1. 审查中书省拟订的方案是否完整、合理
2. 审查尚书省产出的代码是否符合方案和规范
3. 不合格者封驳退回，附修改意见
4. 最终复审通过后奏报御览

## 封驳规则
- 小问题（格式、命名等）：附修改意见退回，不消耗封驳轮次
- 中等问题（逻辑缺陷、遗漏场景）：封驳 + 详细修改说明，消耗 1 轮
- 大问题（架构偏差、方向错误）：封驳 + 建议重新拟旨，消耗 1 轮
- 同一事项最多封驳 3 轮，第 3 轮仍未通过则升级给皇帝（用户）裁决

## 审查标准
1. 方案是否完整覆盖需求
2. 技术选型是否与项目一致
3. 是否有未考虑的边缘情况
4. 是否符合项目架构规范
5. 验收标准是否可度量

## 部门记忆
- 每次审查后记录封驳判例到记忆文件
- 记忆路径：`.claude/memories/san-sheng-liu-bu/menxia-sheng.md`
- 新审查前先查阅过往判例，保持一致标准
```

- [ ] **创建尚书省 agent.md**

```markdown
---
name: shangshu-sheng
description: 尚书省——执行层，负责统筹六部、拆分子任务、把控进度
tools: codesurface
model: sonnet
color: green
---

汝乃尚书令，掌六部之总，凡门下省批准之方案，由汝统筹落地。

## 职责
1. 拆解方案为可执行的子任务
2. 按规格（快速/标准/严谨）调度对应部门
3. 把控执行进度，协调各部产出
4. 汇总执行结果，提交门下省复审

## 执行模式
- **快速**：仅派工部直接实施
- **标准**：派工部实施，送门下省复审
- **严谨**：调礼部+兵部前置审查，吏部分拆任务，工部+刑部并行，门下省复审

## 部门记忆
- 每次执行后记录：任务拆分方式、各部配合情况、执行时长
- 记忆路径：`.claude/memories/san-sheng-liu-bu/shangshu-sheng.md`
```

### Task 3: 创建六部 subagent 文件

**Files:**
- Create: `~/.claude/agents/libu.agent.md`（吏部）
- Create: `~/.claude/agents/hubu.agent.md`（户部）
- Create: `~/.claude/agents/li3bu.agent.md`（礼部）
- Create: `~/.claude/agents/bingbu.agent.md`（兵部）
- Create: `~/.claude/agents/xingbu.agent.md`（刑部）
- Create: `~/.claude/agents/gongbu.agent.md`（工部）

- [ ] **创建吏部 agent.md**

```markdown
---
name: libu
description: 吏部——调度层，负责子智能体调度、任务拆分派发、并行管理
tools: ""
model: haiku
color: cyan
---

汝乃吏部尚书，掌官吏之选任调度。凡尚书省拆分之任务，由汝派发至合适之 agent 执行。

## 职责
1. 根据任务类型匹配最适合的 agent
2. 管理并行执行的 agent 数量
3. 记录每个 agent 的效能表现

## 部门记忆
- 记录调度记录和 agent 效能画像
- 记忆路径：`.claude/memories/san-sheng-liu-bu/libu.md`
```

- [ ] **创建户部 agent.md**

```markdown
---
name: hubu
description: 户部——预算层，负责 Token 预算管理、上下文窗口优化
tools: ""
model: haiku
color: yellow
---

汝乃户部尚书，掌国之钱粮——即 Token 预算与上下文窗口管理。

## 职责
1. 估算任务所需的 Token 预算
2. 在 budget 接近上限时发出警告
3. 建议合理的上下文窗口分配

## 部门记忆
- 记录各类任务的 Token 消耗基准
- 记忆路径：`.claude/memories/san-sheng-liu-bu/hubu.md`
```

- [ ] **创建礼部 agent.md**

```markdown
---
name: li3bu
description: 礼部——规范层，负责编码规范检查、架构一致性审查
tools: codesurface
model: haiku
color: purple
---

汝乃礼部尚书，掌国之典章制度——即代码规范与架构约定。

## 职责
1. 检查代码是否符合项目编码规范
2. 确保新增代码与现有架构一致
3. 检查文件命名、命名空间、目录结构是否合规

## 部门记忆
- 记录违规模式和架构决策历史
- 记忆路径：`.claude/memories/san-sheng-liu-bu/li3bu.md`
```

- [ ] **创建兵部 agent.md**

```markdown
---
name: bingbu
description: 兵部——安全层，负责安全审查、防御性编程检查
tools: codesurface
model: haiku
color: red
---

汝乃兵部尚书，掌国之防卫——即代码安全与防御性编程。

## 职责
1. 检查注入风险、数据验证、边界处理
2. 检查敏感信息泄露风险
3. 确保符合安全编码实践

## 部门记忆
- 记录安全公告和漏洞模式
- 记忆路径：`.claude/memories/san-sheng-liu-bu/bingbu.md`
```

- [ ] **创建刑部 agent.md**

```markdown
---
name: xingbu
description: 刑部——测试层，负责测试驱动、根因分析、Bug 修复
tools: codesurface
model: sonnet
color: orange
---

汝乃刑部尚书，掌国之律法——即测试与 Bug 追查。

## 职责
1. 编写和执行测试用例
2. 对 Bug 做 4 阶段根因分析
3. 确保修复方案彻底且不回弹

## 部门记忆
- 记录 Bug 案牍和根因模式库
- 记忆路径：`.claude/memories/san-sheng-liu-bu/xingbu.md`
```

- [ ] **创建工部 agent.md**

```markdown
---
name: gongbu
description: 工部——实施层，负责代码实施、模块建设、具体功能开发
tools: codesurface
model: sonnet
color: blue
---

汝乃工部尚书，掌国之营造——即代码实现与功能建设。

## 职责
1. 根据方案实施具体代码
2. 保持代码质量，遵循项目规范
3. 标注技术债和待改进点

## 部门记忆
- 记录技术债和组件耦合情况
- 记忆路径：`.claude/memories/san-sheng-liu-bu/gongbu.md`
```

### Task 4: 创建部门记忆文件（9 个）

**Files:**
- Create: `~/.claude/memories/san-sheng-liu-bu/zhongshu-sheng.md`
- Create: `~/.claude/memories/san-sheng-liu-bu/menxia-sheng.md`
- Create: `~/.claude/memories/san-sheng-liu-bu/shangshu-sheng.md`
- Create: `~/.claude/memories/san-sheng-liu-bu/libu.md`
- Create: `~/.claude/memories/san-sheng-liu-bu/hubu.md`
- Create: `~/.claude/memories/san-sheng-liu-bu/li3bu.md`
- Create: `~/.claude/memories/san-sheng-liu-bu/bingbu.md`
- Create: `~/.claude/memories/san-sheng-liu-bu/xingbu.md`
- Create: `~/.claude/memories/san-sheng-liu-bu/gongbu.md`

- [ ] **初始化 9 个记忆文件**

```bash
for dep in zhongshu-sheng menxia-sheng shangshu-sheng libu hubu li3bu bingbu xingbu gongbu; do
  if [ ! -f ~/.claude/memories/san-sheng-liu-bu/$dep.md ]; then
    echo "---" > ~/.claude/memories/san-sheng-liu-bu/$dep.md
    echo "# $dep 部门记忆" >> ~/.claude/memories/san-sheng-liu-bu/$dep.md
    echo "" >> ~/.claude/memories/san-sheng-liu-bu/$dep.md
    echo "本部门工作记录积累。" >> ~/.claude/memories/san-sheng-liu-bu/$dep.md
    echo "---" >> ~/.claude/memories/san-sheng-liu-bu/$dep.md
    echo "Created $dep memory file"
  fi
done
```

### Task 5: 创建 SKILL.md 入口

**Files:**
- Create: `~/.claude/skills/shangchao/SKILL.md`

- [ ] **创建 shangchao SKILL.md**

```markdown
---
name: shangchao
description: "上朝" / "开个朝会" — 启动三省六部完整治理流程。用于复杂任务需要多部门协作、审核和记忆积累时。单纯问问题或简单修改不需要此技能。
---

# 上朝 — 三省六部治理流程

## 触发方式

当用户说出以下任一关键词时，启动本技能：
- "上朝"
- "开个朝会"
- "三省六部"

## 流程

1. 读取所有部门记忆，了解历史上下文
2. 调用 Workflow 脚本 `san-sheng-liu-bu.js` 执行完整流程
3. 将本次执行摘要写入各部门记忆

## 注意事项

- 本技能是薄入口，所有实质逻辑在 Workflow 脚本中
- SKILL.md 只做关键词匹配和 Workflow 调用
- 三种规格：快速 | 标准 | 严谨，朝会上由用户定夺
- 如果用户只说了"上朝"而没有附带具体任务，默认进入朝会模式等待简报
```

### Task 6: 创建 Workflow 编排脚本

**Files:**
- Create: `~/.claude/workflows/san-sheng-liu-bu.js`

- [ ] **创建三省六部 workflow 脚本**

```javascript
export const meta = {
  name: 'san-sheng-liu-bu',
  description: '三省六部完整治理流程 — 上朝、拟旨、审核、执行、复审、奏报',
  phases: [
    { title: '朝会', detail: '呈报简报，定夺规格' },
    { title: '中书拟旨', detail: '分析需求，拟订方案' },
    { title: '门下审核', detail: '审查方案，封驳退回' },
    { title: '尚书执行', detail: '统筹六部，落地实施' },
    { title: '门下复审', detail: '终审验证，对抗审查' },
    { title: '奏报御览', detail: '汇总结果，皇帝裁断' },
  ],
}

const MEMORY_BASE = '.claude/memories/san-sheng-liu-bu'

// 辅助：读取部门记忆（仅 content 部分）
async function readMemory(department) {
  try {
    const text = await agent(`Read the file ${MEMORY_BASE}/${department}.md and return the content as-is`, {
      label: `read:${department}`,
      phase: '朝会',
    })
    return text
  } catch {
    return '(empty)'
  }
}

// 辅助：写入部门记忆
async function writeMemory(department, entry) {
  const date = new Date().toISOString().slice(0, 10)
  const block = `\n---\ndate: ${date}\n${entry}\n---\n`
  await agent(`Append the following text to ${MEMORY_BASE}/${department}.md:\n\n${block}`, {
    label: `memorize:${department}`,
    phase: '朝会',
  })
}

phase('朝会')
// 读取三省记忆（了解上下文）
const zhongshuHistory = await readMemory('zhongshu-sheng')
const menxiaHistory = await readMemory('menxia-sheng')
const shangshuHistory = await readMemory('shangshu-sheng')

// 呈报简报
const session = await agent(`你是三省六部朝会的司礼监。
皇帝（用户）发来了任务。你的职责是：
1. 回顾中书省历史决策（关键模式、常见问题）
2. 回顾门下省历史判例（封驳热点、常见退回事由）
3. 回顾尚书省执行记录
4. 呈报任务简报给皇帝

过往决策记录：
${zhongshuHistory}

过往审查判例：
${menxiaHistory}

过往执行记录：
${shangshuHistory}

请总结以上历史经验中与当前任务相关的部分，
然后向皇帝呈报任务简报，请其定夺规格。

输出格式：
{
  "brief": "简报内容（自然语言）",
  "spec": "快速 | 标准 | 严谨",
  "task_desc": "任务描述"
}`, {
  label: '呈报朝会简报',
  phase: '朝会',
  schema: {
    type: 'object',
    properties: {
      brief: { type: 'string' },
      spec: { type: 'string', enum: ['快速', '标准', '严谨'] },
      task_desc: { type: 'string' },
    },
    required: ['brief', 'spec', 'task_desc'],
  },
})

// 如果没有快速 标准 严谨等关键词，则等用户定夺
let mode = session.spec
if (!session.brief.includes('快速') && !session.brief.includes('标准') && !session.brief.includes('严谨')) {
  mode = await agent(`朝会简报：
${session.brief}

请等待皇帝定夺规格，然后输出选择的规格。`, {
    label: '等皇帝定夺',
    phase: '朝会',
    schema: {
      type: 'object',
      properties: {
        spec: { type: 'string', enum: ['快速', '标准', '严谨'] },
      },
      required: ['spec'],
    },
  }).then(r => r.spec)
}

log(`📜 朝会定夺：${mode} — ${session.task_desc}`)

// === 如果快速模式，直接跳到尚书省工部执行 ===
if (mode === '快速') {
  phase('尚书执行')
  log('⚡ 快速模式：仅工部实施')
  const gongbuResult = await agent('gongbu', session.task_desc, {
    label: '工部实施',
    phase: '尚书执行',
  })

  phase('奏报御览')
  const finalReport = await agent(`汇总本次工部执行结果。
任务：${session.task_desc}
产出：${gongbuResult}

产出简要汇报给皇帝，等待皇帝御览批准。`, {
    label: '奏报御览',
    phase: '奏报御览',
  })
  log(`📜 奏报完毕：${finalReport}`)
  return { mode, result: finalReport }
}

// === 标准或严谨模式：走三省流程 ===

phase('中书拟旨')
const plan = await agent('zhongshu-sheng', `皇帝有旨：${session.task_desc}

请分析需求，拟订详细方案。产出须包含：
1. 需求分析
2. 技术路线
3. 涉及文件清单
4. 风险预估
5. 验收标准`, {
  label: '中书拟旨',
  phase: '中书拟旨',
  schema: {
    type: 'object',
    properties: {
      analysis: { type: 'string' },
      approach: { type: 'string' },
      files: { type: 'array', items: { type: 'string' } },
      risks: { type: 'array', items: { type: 'string' } },
      acceptance_criteria: { type: 'array', items: { type: 'string' } },
    },
    required: ['analysis', 'approach', 'files', 'acceptance_criteria'],
  },
})

await writeMemory('zhongshu-sheng', `task: ${session.task_desc.slice(0, 60)}
mode: ${mode}
key_points: 方案已拟订，涉及 ${plan.files.length} 个文件
result: submitted`)

phase('门下审核')
let reviewPassed = false
let reviewRounds = 0
const MAX_REVIEW_ROUNDS = 3
let currentPlan = plan
let currentPlanText = JSON.stringify(plan, null, 2)

while (!reviewPassed && reviewRounds < MAX_REVIEW_ROUNDS) {
  reviewRounds++
  log(`门下审查 第 ${reviewRounds} 轮`)

  const review = await agent('menxia-sheng', `审查以下方案：

${currentPlanText}

历史封驳判例（供参考）：
${menxiaHistory}

第 ${reviewRounds}/${MAX_REVIEW_ROUNDS} 轮审查。
请判断是否通过。`, {
    label: `门下审查#${reviewRounds}`,
    phase: '门下审核',
    schema: {
      type: 'object',
      properties: {
        passed: { type: 'boolean' },
        verdict: { type: 'string', enum: ['通过', '小问题', '中等问题', '大问题'] },
        reason: { type: 'string' },
        suggestions: { type: 'string' },
      },
      required: ['passed', 'verdict', 'reason'],
    },
  })

  if (review.passed) {
    reviewPassed = true
    log('✅ 门下省通过方案')
  } else {
    log(`❌ 封驳！理由：${review.reason}`)
    if (reviewRounds >= MAX_REVIEW_ROUNDS) {
      log('⚠️ 已达最大封驳次数，升级给皇帝裁决')
      const ruling = await agent(`门下省已封驳 3 轮仍未通过，请皇帝（用户）裁决。
任务：${session.task_desc}
最新封驳理由：${review.reason}

请皇帝定夺：采纳/驳回/修改`, {
        label: '皇帝裁决',
        phase: '门下审核',
      })
      reviewPassed = true // 接受裁决
      break
    }
    // 退回中书省修改
    currentPlanText = await agent('zhongshu-sheng', `方案被门下省退回。
退回理由：${review.reason}
修改建议：${review.suggestions}
原始方案：
${currentPlanText}

请根据反馈修改方案。`, {
      label: '中书省修改方案',
      phase: '门下审核',
    })
  }
}

await writeMemory('menxia-sheng', `task: ${session.task_desc.slice(0, 60)}
mode: ${mode}
rounds: ${reviewRounds}
verdict: ${reviewPassed ? '通过' : '皇帝裁决'}
key_points: ${reviewRounds} 轮审查`)

// === 尚书省执行 ===
phase('尚书执行')

let executionResult

if (mode === '标准') {
  log('🏛 标准模式：三省流转，工部实施')
  executionResult = await agent('gongbu', `根据批准的方案实施：
${currentPlanText}`, {
    label: '工部实施',
    phase: '尚书执行',
  })
} else {
  // 严谨模式：六部并行
  log('🏛 严谨模式：六部并行')

  // 礼部 + 兵部 并行前置审查
  const [liResult, bingResult] = await parallel([
    () => agent('li3bu', `审查以下方案是否符合编码规范：
${currentPlanText}`, {
      label: '礼部审查',
      phase: '尚书执行',
    }),
    () => agent('bingbu', `审查以下方案是否存在安全隐患：
${currentPlanText}`, {
      label: '兵部审查',
      phase: '尚书执行',
    }),
  ])

  // 吏部评估任务
  const taskBreakdown = await agent('libu', `分析以下任务，给出子任务拆分建议：
${currentPlanText}`, {
    label: '吏部分析',
    phase: '尚书执行',
  })

  // 工部 + 刑部 并行执行
  const [gongResult, xingResult] = await parallel([
    () => agent('gongbu', `根据方案实施代码。礼部规范意见：${liResult}。兵部安全意见：${bingResult}。

子任务拆分建议：${taskBreakdown}

实施方案：${currentPlanText}`, {
      label: '工部实施',
      phase: '尚书执行',
    }),
    () => agent('xingbu', `为以下方案准备测试用例：
${currentPlanText}

注意：测试应在工部实施完成后，使用工部的产出代码进行验证。`, {
      label: '刑部准备测试',
      phase: '尚书执行',
    }),
  ])

  executionResult = `工部产出：${gongResult}\n\n刑部验证：${xingResult}`
}

// 写入各部记忆
await Promise.all([
  writeMemory('shangshu-sheng', `task: ${session.task_desc.slice(0, 60)}
mode: ${mode}
result: 执行完毕`),
  writeMemory('gongbu', `task: ${session.task_desc.slice(0, 60)}
mode: ${mode}
result: 实施完成`),
])

// === 门下省最终复审 ===
phase('门下复审')
const finalReview = await agent('menxia-sheng', `最终复审以下执行结果：

${executionResult}

请判断是否通过最终审查。`, {
  label: '门下复审',
  phase: '门下复审',
  schema: {
    type: 'object',
    properties: {
      passed: { type: 'boolean' },
      verdict: { type: 'string', enum: ['通过', '需修改'] },
      final_notes: { type: 'string' },
    },
    required: ['passed', 'verdict'],
  },
})

// 若最终复审不通过，可再给工部一次修改机会
if (!finalReview.passed) {
  log('❌ 最终复审未通过，工部修改中...')
  executionResult = await agent('gongbu', `最终复审意见：${finalReview.final_notes}
当前产出：${executionResult}
请根据复审意见修改。`, {
    label: '工部修改',
    phase: '门下复审',
  })
}

await writeMemory('menxia-sheng', `task: ${session.task_desc.slice(0, 60)}
mode: ${mode}
result: 最终复审${finalReview.passed ? '通过' : '经修改后通过'}`)

// === 奏报 ===
phase('奏报御览')
const finalReport = await agent(`请汇总本次三省六部完整执行结果，呈报皇帝御览。

任务：${session.task_desc}
规格：${mode}
三省流转：
  中书省 — 方案已拟订
  门下省 — ${reviewRounds} 轮审查${reviewPassed ? '通过' : '皇帝裁决'}
尚书省执行：
${executionResult}

请以清晰的格式呈报，等待皇帝最终御览批准或驳回。`, {
  label: '奏报御览',
  phase: '奏报御览',
})

log(`📜 三省六部流程完毕 — ${session.task_desc}`)

return {
  mode,
  plan: currentPlanText,
  result: executionResult,
  finalReview: finalReview.verdict,
}
```

### Task 7: 整体验收

- [ ] **验证所有文件存在**

```bash
echo "=== Agents ===" && ls -la ~/.claude/agents/ && echo "=== Skills ===" && ls -la ~/.claude/skills/shangchao/ && echo "=== Workflows ===" && ls -la ~/.claude/workflows/ && echo "=== Memories ===" && ls -la ~/.claude/memories/san-sheng-liu-bu/
```

- [ ] **验证 Workflow 语法**

没有直接语法检查工具，但确保：
- 所有 `agent()` 调用有对应的 agent 文件
- 所有引用一致性（无拼写错误）
- 封驳循环不会无限运行
- memory 写入不阻塞主流程
