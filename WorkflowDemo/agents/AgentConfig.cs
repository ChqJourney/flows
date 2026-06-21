namespace WorkflowDemo.Agents;

public sealed class AgentConfig
{
    public string Name { get; set; } = string.Empty;
    public string? ModelId { get; set; }
    public float Temperature { get; set; } = 0.3f;
    public int MaxOutputTokens { get; set; } = 2048;
    public string Instructions { get; set; } = string.Empty;
    public IReadOnlyList<string> Tools { get; set; } = Array.Empty<string>();
}
