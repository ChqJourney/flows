using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using WorkflowDemo.Tools;

namespace WorkflowDemo.Agents;

public sealed class AgentFactory
{
    private readonly IChatClient _chatClient;
    private readonly IReadOnlyDictionary<string, AIFunction> _tools;

    public AgentFactory(IChatClient chatClient, StandardLibraryTools standardTools, CacheTools cacheTools)
    {
        _chatClient = chatClient;
        _tools = new Dictionary<string, AIFunction>(StringComparer.OrdinalIgnoreCase)
        {
            ["SearchStandards"] = AIFunctionFactory.Create(standardTools.SearchStandards),
            ["GetStandardDetail"] = AIFunctionFactory.Create(standardTools.GetStandardDetail),
            ["ListStandardSections"] = AIFunctionFactory.Create(standardTools.ListStandardSections),
            ["QueryCache"] = AIFunctionFactory.Create(cacheTools.QueryCache),
        };
    }

    public AIAgent CreateAgent(string configName)
    {
        var path = Path.Combine("agents", $"{configName}.json");
        if (!File.Exists(path))
            throw new FileNotFoundException($"Agent config not found: {path}");

        var config = JsonSerializer.Deserialize<AgentConfig>(File.ReadAllText(path), new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new InvalidOperationException($"Failed to deserialize agent config: {path}");

        var tools = config.Tools
            .Select(name => _tools.TryGetValue(name, out var tool) ? tool : throw new InvalidOperationException($"Unknown tool '{name}' in {configName}.json"))
            .Cast<AITool>()
            .ToList();

        var options = new ChatClientAgentOptions
        {
            Name = config.Name,
            ChatOptions = new ChatOptions
            {
                Instructions = config.Instructions,
                Temperature = config.Temperature,
                MaxOutputTokens = config.MaxOutputTokens,
                Tools = tools.Count > 0 ? tools : null,
            }
        };

        return _chatClient.AsAIAgent(options);
    }
}
