# WorkflowDemo — 检测行业 AI 文档生成工作流

`WorkflowDemo` 是一个基于 **.NET 10** 的控制台应用，演示如何用多 Agent 协作工作流，把用户的检测行业技术问题自动转换为一份带 **inline SVG 图解** 的专业 Markdown 技术文档。

项目以 **Microsoft.Agents.AI / Microsoft.Extensions.AI** 为 Agent 框架，接入 DeepSeek（OpenAI 兼容 API），并通过本地标准库检索与答案缓存实现可复现、可加速的问答体验。

---

## 1. 核心能力

| 能力 | 说明 |
|------|------|
| **意图识别** | 由 `IntentDetector` Agent 提取问题领域、关键词、类型与摘要。 |
| **缓存命中** | `CacheGuardian` + `IAnswerCache` 优先返回历史答案，避免重复调用大模型。 |
| **标准检索与规划** | `ChiefPlanner` 调用标准库工具检索相关标准并生成写作计划。 |
| **领域专家起草** | `LightingExpert` 等专业 Agent 依据计划起草 Markdown 草稿。 |
| **可视化排版** | `DocumentStylist` 把草稿转换为带 inline SVG 图的最终文档。 |
| **质量审核迭代** | `ChiefReviewer` 多轮审核，不通过则退回修改，最多 `MaxReviewIterations` 轮。 |

运行示例：

```bash
export DEEPSEEK_API_KEY=your-key
dotnet run -- "LED 灯具的绝缘电阻测试步骤是什么？"
```

生成结果默认写入 `output/yyyyMMdd-HHmmss.md`。

---

## 2. 代码构造

### 2.1 目录结构

```
WorkflowDemo/
├── Program.cs                 # Host 构建、DI 注册、入口逻辑
├── WorkflowDemo.csproj        # .NET 10 控制台项目
├── appsettings.json           # AI / 标准库 / 缓存 / 工作流配置
├── agents/
│   ├── *.json                 # Agent 提示词与工具配置
│   ├── AgentConfig.cs         # Agent 配置模型
│   ├── AgentFactory.cs        # 根据 JSON 创建 AIAgent
│   └── WorkflowOrchestrator.cs# 编排 7 步工作流
├── Models/
│   └── WorkflowState.cs       # 意图/计划/审核结果 DTO
├── Services/
│   ├── IStandardLibraryStore.cs / StandardLibraryStore.cs  # 本地标准库检索
│   └── IAnswerCache.cs        / AnswerCache.cs             # JSON 文件缓存
├── Tools/
│   ├── StandardLibraryTools.cs# 标准库 MCP 风格工具
│   └── CacheTools.cs          # 缓存查询 MCP 风格工具
└── standards/                 # 本地标准库目录（运行时可配置）
```

### 2.2 整体架构图

<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 1000 720" width="1000" height="720">
  <defs>
    <marker id="arrow" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="8" markerHeight="8" orient="auto-start-reverse">
      <path d="M 0 0 L 10 5 L 0 10 z" fill="#64748b"/>
    </marker>
    <style>
      .box { rx:8; stroke-width:1.5; }
      .title { font-family: system-ui, -apple-system, sans-serif; font-weight:700; font-size:14px; }
      .text { font-family: system-ui, -apple-system, sans-serif; font-size:12px; }
      .small { font-family: system-ui, -apple-system, sans-serif; font-size:11px; }
      .layer { font-family: system-ui, -apple-system, sans-serif; font-weight:700; font-size:13px; fill:#475569; }
    </style>
  </defs>
  <!-- background -->
  <rect width="1000" height="720" fill="#f8fafc"/>
  <!-- layer labels (left side, aligned to each layer) -->
  <text x="20" y="70" class="layer">外部依赖</text>
  <text x="20" y="170" class="layer">入口 &amp; 基础设施</text>
  <text x="20" y="310" class="layer">服务 &amp; 工具层</text>
  <text x="20" y="510" class="layer">Agent 工作流层</text>
  <!-- external dependency -->
  <rect x="760" y="35" width="200" height="80" fill="#fee2e2" stroke="#f87171" class="box"/>
  <text x="860" y="60" text-anchor="middle" class="title" fill="#7f1d1d">DeepSeek / OpenAI</text>
  <text x="860" y="82" text-anchor="middle" class="text" fill="#7f1d1d">IChatClient</text>
  <text x="860" y="102" text-anchor="middle" class="small" fill="#991b1b">api.deepseek.com · v4-flash</text>
  <!-- entry / host / infrastructure -->
  <rect x="180" y="125" width="160" height="80" fill="#dbeafe" stroke="#60a5fa" class="box"/>
  <text x="260" y="152" text-anchor="middle" class="title" fill="#1e3a8a">Program.cs</text>
  <text x="260" y="174" text-anchor="middle" class="text" fill="#1e3a8a">Host · DI · Config</text>
  <text x="260" y="192" text-anchor="middle" class="small" fill="#1e40af">args / console input</text>
  <rect x="380" y="125" width="180" height="80" fill="#e0e7ff" stroke="#818cf8" class="box"/>
  <text x="470" y="152" text-anchor="middle" class="title" fill="#312e81">appsettings.json</text>
  <text x="470" y="174" text-anchor="middle" class="text" fill="#312e81">AI / Standards</text>
  <text x="470" y="192" text-anchor="middle" class="text" fill="#312e81">Cache / Workflow</text>
  <rect x="600" y="125" width="160" height="80" fill="#dcfce7" stroke="#4ade80" class="box"/>
  <text x="680" y="152" text-anchor="middle" class="title" fill="#14532d">DI 容器</text>
  <text x="680" y="174" text-anchor="middle" class="text" fill="#14532d">IChatClient</text>
  <text x="680" y="192" text-anchor="middle" class="text" fill="#14532d">Services / Tools / Agents</text>
  <!-- services &amp; tools -->
  <rect x="180" y="255" width="220" height="100" fill="#fef3c7" stroke="#fbbf24" class="box"/>
  <text x="290" y="282" text-anchor="middle" class="title" fill="#78350f">Services</text>
  <text x="290" y="306" text-anchor="middle" class="text" fill="#78350f">IStandardLibraryStore</text>
  <text x="290" y="324" text-anchor="middle" class="small" fill="#78350f">StandardLibraryStore</text>
  <text x="290" y="342" text-anchor="middle" class="text" fill="#78350f">IAnswerCache / AnswerCache</text>
  <rect x="430" y="255" width="240" height="100" fill="#ffedd5" stroke="#fb923c" class="box"/>
  <text x="550" y="282" text-anchor="middle" class="title" fill="#7c2d12">Tools (MCP 风格)</text>
  <text x="550" y="306" text-anchor="middle" class="text" fill="#7c2d12">SearchStandards · GetStandardDetail</text>
  <text x="550" y="324" text-anchor="middle" class="text" fill="#7c2d12">ListStandardSections · QueryCache</text>
  <rect x="710" y="255" width="220" height="100" fill="#f3e8ff" stroke="#c084fc" class="box"/>
  <text x="820" y="282" text-anchor="middle" class="title" fill="#581c87">本地数据</text>
  <text x="820" y="306" text-anchor="middle" class="text" fill="#581c87">standards/*.md</text>
  <text x="820" y="324" text-anchor="middle" class="text" fill="#581c87">cache/outputs.json</text>
  <text x="820" y="342" text-anchor="middle" class="text" fill="#581c87">output/*.md</text>
  <!-- agent workflow container -->
  <rect x="60" y="405" width="920" height="220" fill="#ecfdf5" stroke="#34d399" class="box"/>
  <text x="520" y="432" text-anchor="middle" class="title" fill="#064e3b">Agent 工作流 (WorkflowOrchestrator)</text>
  <!-- agent boxes -->
  <rect x="95" y="460" width="120" height="70" fill="#ffffff" stroke="#10b981" class="box"/>
  <text x="155" y="488" text-anchor="middle" class="text" fill="#064e3b">IntentDetector</text>
  <text x="155" y="510" text-anchor="middle" class="small" fill="#065f46">意图识别</text>
  <rect x="245" y="460" width="120" height="70" fill="#ffffff" stroke="#10b981" class="box"/>
  <text x="305" y="488" text-anchor="middle" class="text" fill="#064e3b">CacheGuardian</text>
  <text x="305" y="510" text-anchor="middle" class="small" fill="#065f46">缓存命中</text>
  <rect x="395" y="460" width="120" height="70" fill="#ffffff" stroke="#10b981" class="box"/>
  <text x="455" y="488" text-anchor="middle" class="text" fill="#064e3b">ChiefPlanner</text>
  <text x="455" y="510" text-anchor="middle" class="small" fill="#065f46">检索 + 计划</text>
  <rect x="545" y="460" width="120" height="70" fill="#ffffff" stroke="#10b981" class="box"/>
  <text x="605" y="488" text-anchor="middle" class="text" fill="#064e3b">LightingExpert</text>
  <text x="605" y="510" text-anchor="middle" class="small" fill="#065f46">领域起草</text>
  <rect x="695" y="460" width="120" height="70" fill="#ffffff" stroke="#10b981" class="box"/>
  <text x="755" y="488" text-anchor="middle" class="text" fill="#064e3b">DocumentStylist</text>
  <text x="755" y="510" text-anchor="middle" class="small" fill="#065f46">SVG 排版</text>
  <rect x="845" y="460" width="120" height="70" fill="#ffffff" stroke="#10b981" class="box"/>
  <text x="905" y="488" text-anchor="middle" class="text" fill="#064e3b">ChiefReviewer</text>
  <text x="905" y="510" text-anchor="middle" class="small" fill="#065f46">终审迭代</text>
  <!-- agent flow arrows (horizontal) -->
  <line x1="215" y1="495" x2="245" y2="495" stroke="#64748b" marker-end="url(#arrow)"/>
  <line x1="365" y1="495" x2="395" y2="495" stroke="#64748b" marker-end="url(#arrow)"/>
  <line x1="515" y1="495" x2="545" y2="495" stroke="#64748b" marker-end="url(#arrow)"/>
  <line x1="665" y1="495" x2="695" y2="495" stroke="#64748b" marker-end="url(#arrow)"/>
  <line x1="815" y1="495" x2="845" y2="495" stroke="#64748b" marker-end="url(#arrow)"/>
  <!-- cache short-circuit return -->
  <path d="M 305 460 Q 305 425 460 425 Q 615 425 615 460" fill="none" stroke="#f59e0b" stroke-dasharray="5,5" marker-end="url(#arrow)"/>
  <text x="460" y="415" text-anchor="middle" class="small" fill="#b45309">命中：直接返回缓存文档</text>
  <!-- review loop (back to stylist) -->
  <path d="M 905 460 Q 905 420 830 420 Q 755 420 755 460" fill="none" stroke="#f59e0b" stroke-dasharray="5,5" marker-end="url(#arrow)"/>
  <text x="830" y="405" text-anchor="middle" class="small" fill="#b45309">审核不通过：退回修改</text>
  <!-- final output -->
  <rect x="410" y="650" width="220" height="50" fill="#ffffff" stroke="#334155" class="box"/>
  <text x="520" y="680" text-anchor="middle" class="title" fill="#0f172a">output/yyyyMMdd-HHmmss.md</text>
  <!-- arrows between layers -->
  <line x1="260" y1="205" x2="290" y2="255" stroke="#64748b" marker-end="url(#arrow)"/>
  <line x1="680" y1="205" x2="820" y2="255" stroke="#64748b" marker-end="url(#arrow)"/>
  <line x1="400" y1="305" x2="430" y2="305" stroke="#64748b" marker-end="url(#arrow)"/>
  <line x1="670" y1="305" x2="710" y2="305" stroke="#64748b" marker-end="url(#arrow)"/>
  <line x1="520" y1="650" x2="520" y2="625" stroke="#64748b" marker-end="url(#arrow)"/>
  <!-- chat client -->
  <line x1="760" y1="165" x2="760" y2="115" stroke="#64748b" marker-end="url(#arrow)"/>
</svg>

### 2.3 关键组件说明

#### `Program.cs`
- 构建 `IHost` 与 DI 容器。
- 从 `appsettings.json` + 环境变量读取配置。
- 注册 `IChatClient`（OpenAIClient → DeepSeek 兼容端点）。
- 注册所有服务、工具、Agent 工厂与编排器。

#### `AgentFactory`
- 读取 `agents/{name}.json` 配置。
- 将 `[Description]` 装饰的 .NET 方法封装为 `AIFunction`。
- 调用 `_chatClient.AsAIAgent(options)` 创建可运行的 Agent。

#### `WorkflowOrchestrator`
按固定 7 步顺序执行：

```
1. DetectIntentAsync        → IntentResult
2. TryCacheHitAsync         → 命中则直接返回
3. PlanAsync                → PlanResult
4. DraftAsync               → Markdown 草稿
5. StyleAsync               → 带 inline SVG 的最终文档
6. ReviewAsync / ReviseAsync → 最多 MaxReviewIterations 轮
7. Save to cache            → output/*.md
```

#### `StandardLibraryStore`
- 从配置目录加载本地 `.md` 标准文档。
- 提供基于标题/章节/内容的关键词评分搜索。
- 为 Agent 提供 `SearchStandards`、`GetStandardDetail`、`ListStandardSections` 工具。

#### `AnswerCache`
- 以归一化问题为键，缓存生成的 Markdown 答案到 `cache/outputs.json`。
- 支持并发访问与持久化。

---

## 3. 运行前准备

1. 确保已安装 [.NET 10 SDK](https://dotnet.microsoft.com/)。
2. 设置 DeepSeek API Key：
   ```bash
   export DEEPSEEK_API_KEY=your-key
   ```
3. （可选）在 `appsettings.json` 中配置 `Standards:LibraryPath` 指向本地标准库目录。

---

## 4. 快速开始

```bash
cd WorkflowDemo
dotnet build
dotnet run -- "灯具接地连续性测试的判定标准是什么？"
```

结果文件示例：

```
output/20260621-233049.md
```

---

## 5. 技术栈

- **.NET 10** 控制台应用
- **Microsoft.Agents.AI / Microsoft.Agents.AI.Workflows** — Agent 抽象与工作流
- **Microsoft.Extensions.AI.OpenAI** — OpenAI 兼容聊天客户端
- **Microsoft.Extensions.Hosting / Configuration / DI** — 基础设施
- **DeepSeek API** — 大语言模型推理

---

## 6. 扩展建议

- 在 `agents/` 下新增更多领域专家 JSON（如 `ElectricalExpert`、`SafetyExpert`），并在 `PlanResult.DomainAgent` 中动态选择。
- 将 `StandardLibraryStore` 的基于关键词的评分替换为向量检索（如 ONNX Runtime + 嵌入模型）。
- 把 `ReviewAsync` 改造为并行多维度审核（技术准确性、标准引用、可视化、可读性）。
