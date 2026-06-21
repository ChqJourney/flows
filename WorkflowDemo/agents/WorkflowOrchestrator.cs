using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Logging;
using WorkflowDemo.Models;
using WorkflowDemo.Services;

namespace WorkflowDemo.Agents;

public sealed class WorkflowOrchestrator
{
    private readonly AgentFactory _agentFactory;
    private readonly IAnswerCache _cache;
    private readonly ILogger<WorkflowOrchestrator> _logger;
    private readonly int _maxReviewIterations;

    public WorkflowOrchestrator(AgentFactory agentFactory, IAnswerCache cache, ILogger<WorkflowOrchestrator> logger, int maxReviewIterations = 2)
    {
        _agentFactory = agentFactory;
        _cache = cache;
        _logger = logger;
        _maxReviewIterations = maxReviewIterations;
    }

    public async Task<string> RunAsync(string question, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("=== Workflow started for: {Question}", question);

        // 1. Intent detection
        var intent = await DetectIntentAsync(question, cancellationToken);
        _logger.LogInformation("Detected intent: {Domain} / {Type} / {Summary}", intent.Domain, intent.QuestionType, intent.Summary);

        // 2. Cache hit
        var cached = await TryCacheHitAsync(question, cancellationToken);
        if (cached is not null)
        {
            _logger.LogInformation("Cache hit, returning cached document.");
            return cached;
        }

        // 3. Chief expert planning
        var plan = await PlanAsync(question, intent, cancellationToken);
        _logger.LogInformation("Plan created. Standards: {Standards}", string.Join(", ", plan.RelevantStandardIds));

        // 4. Domain expert draft
        var draft = await DraftAsync(question, intent, plan, cancellationToken);
        _logger.LogInformation("Draft generated ({Length} chars).", draft.Length);

        // 5. Document stylist
        var document = await StyleAsync(draft, cancellationToken);
        _logger.LogInformation("Document styled ({Length} chars).", document.Length);

        // 6. Chief review loop
        for (int i = 0; i < _maxReviewIterations; i++)
        {
            var review = await ReviewAsync(question, document, cancellationToken);
            _logger.LogInformation("Review #{Iteration}: approved={Approved}", i + 1, review.Approved);

            if (review.Approved)
                break;

            document = await ReviseAsync(document, review.Comments, cancellationToken);
        }

        // 7. Save to cache and return
        _cache.Save(question, document);
        _logger.LogInformation("Workflow completed and cached.");
        return document;
    }

    private async Task<IntentResult> DetectIntentAsync(string question, CancellationToken cancellationToken)
    {
        var agent = _agentFactory.CreateAgent("IntentDetector");
        var response = await agent.RunAsync(question, cancellationToken: cancellationToken);
        var json = ExtractJson(response.Text);
        return JsonSerializer.Deserialize<IntentResult>(json, JsonOptions) ?? new IntentResult();
    }

    private async Task<string?> TryCacheHitAsync(string question, CancellationToken cancellationToken)
    {
        // Use CacheGuardian agent for the workflow role, but also do a direct lookup for reliability.
        var direct = _cache.Query(question);
        if (direct is not null)
            return direct.Document;

        var agent = _agentFactory.CreateAgent("CacheGuardian");
        var response = await agent.RunAsync(question, cancellationToken: cancellationToken);
        var text = response.Text;

        if (text.Contains("CACHE_HIT") && text.Contains("\n\n"))
        {
            var idx = text.IndexOf("\n\n", StringComparison.Ordinal);
            return text[(idx + 2)..].Trim();
        }

        return null;
    }

    private async Task<PlanResult> PlanAsync(string question, IntentResult intent, CancellationToken cancellationToken)
    {
        var agent = _agentFactory.CreateAgent("ChiefPlanner");
        var prompt = $"用户问题：{question}\n\n意图分析：\n" +
                     $"- 领域：{intent.Domain}\n- 类型：{intent.QuestionType}\n- 关键词：{string.Join(", ", intent.Keywords)}\n- 摘要：{intent.Summary}\n\n" +
                     "请检索相关标准并输出 JSON 计划。";

        var response = await agent.RunAsync(prompt, cancellationToken: cancellationToken);
        var json = ExtractJson(response.Text);
        return JsonSerializer.Deserialize<PlanResult>(json, JsonOptions) ?? new PlanResult();
    }

    private async Task<string> DraftAsync(string question, IntentResult intent, PlanResult plan, CancellationToken cancellationToken)
    {
        var agent = _agentFactory.CreateAgent(plan.DomainAgent);
        var prompt = $"请根据以下信息起草技术文档草稿。\n\n用户问题：{question}\n\n" +
                     $"意图：{intent.Summary}\n\n" +
                     $"相关标准：{string.Join(", ", plan.RelevantStandardIds)}\n\n" +
                     $"大纲：\n{string.Join("\n", plan.Outline.Select((o, i) => $"{i + 1}. {o}"))}\n\n" +
                     $"必须覆盖的技术要点：\n{string.Join("\n", plan.KeyPoints.Select(k => $"- {k}"))}\n\n" +
                     "请输出 Markdown 草稿。";

        var response = await agent.RunAsync(prompt, cancellationToken: cancellationToken);
        return response.Text;
    }

    private async Task<string> StyleAsync(string draft, CancellationToken cancellationToken)
    {
        var agent = _agentFactory.CreateAgent("DocumentStylist");
        var prompt = $"请将以下草稿转换为带有 inline SVG 图的最终 Markdown 文档。\n\n{draft}";
        var response = await agent.RunAsync(prompt, cancellationToken: cancellationToken);
        return response.Text;
    }

    private async Task<ReviewResult> ReviewAsync(string question, string document, CancellationToken cancellationToken)
    {
        var agent = _agentFactory.CreateAgent("ChiefReviewer");
        var prompt = $"原始问题：{question}\n\n候选文档：\n\n{document}\n\n" +
                     "请严格审核并只输出 JSON：{ \"approved\": true/false, \"comments\": \"...\" }";

        var response = await agent.RunAsync(prompt, cancellationToken: cancellationToken);
        var json = ExtractJson(response.Text);
        return JsonSerializer.Deserialize<ReviewResult>(json, JsonOptions) ?? new ReviewResult { Approved = true };
    }

    private async Task<string> ReviseAsync(string document, string comments, CancellationToken cancellationToken)
    {
        var agent = _agentFactory.CreateAgent("DocumentStylist");
        var prompt = $"请根据审核意见修改以下 Markdown 文档。\n\n审核意见：\n{comments}\n\n原始文档：\n\n{document}\n\n" +
                     "请输出修改后的完整 Markdown 文档，保留 inline SVG 图。";
        var response = await agent.RunAsync(prompt, cancellationToken: cancellationToken);
        return response.Text;
    }

    private static string ExtractJson(string text)
    {
        var trimmed = text.Trim();

        // Remove markdown code fences if present.
        if (trimmed.StartsWith("```"))
        {
            var lines = trimmed.Split('\n');
            var withoutFence = lines
                .SkipWhile(l => l.Trim().StartsWith("```"))
                .Reverse()
                .SkipWhile(l => l.Trim().StartsWith("```"))
                .Reverse();
            trimmed = string.Join("\n", withoutFence).Trim();
        }

        if (trimmed.StartsWith("`") && trimmed.EndsWith("`"))
            trimmed = trimmed.Trim('`').Trim();

        // If the response still has explanatory text, try to locate the JSON object/array.
        if (!trimmed.StartsWith("{") && !trimmed.StartsWith("["))
        {
            var startObject = trimmed.IndexOf('{');
            var startArray = trimmed.IndexOf('[');
            int start;
            if (startObject >= 0 && startArray >= 0)
                start = Math.Min(startObject, startArray);
            else
                start = Math.Max(startObject, startArray);

            if (start >= 0)
            {
                var end = Math.Max(trimmed.LastIndexOf('}'), trimmed.LastIndexOf(']'));
                if (end > start)
                    trimmed = trimmed[start..(end + 1)];
            }
        }

        return trimmed;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
}
