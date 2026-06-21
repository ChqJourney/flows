using System.Text.Json.Serialization;

namespace WorkflowDemo.Models;

public sealed class IntentResult
{
    [JsonPropertyName("domain")]
    public string Domain { get; set; } = string.Empty;

    [JsonPropertyName("keywords")]
    public IReadOnlyList<string> Keywords { get; set; } = Array.Empty<string>();

    [JsonPropertyName("questionType")]
    public string QuestionType { get; set; } = string.Empty;

    [JsonPropertyName("summary")]
    public string Summary { get; set; } = string.Empty;
}

public sealed class PlanResult
{
    [JsonPropertyName("relevantStandardIds")]
    public IReadOnlyList<string> RelevantStandardIds { get; set; } = Array.Empty<string>();

    [JsonPropertyName("outline")]
    public IReadOnlyList<string> Outline { get; set; } = Array.Empty<string>();

    [JsonPropertyName("keyPoints")]
    public IReadOnlyList<string> KeyPoints { get; set; } = Array.Empty<string>();

    [JsonPropertyName("domainAgent")]
    public string DomainAgent { get; set; } = "LightingExpert";
}

public sealed class ReviewResult
{
    [JsonPropertyName("approved")]
    public bool Approved { get; set; }

    [JsonPropertyName("comments")]
    public string Comments { get; set; } = string.Empty;
}
