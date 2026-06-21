using System.ClientModel;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenAI;
using OpenAI.Chat;
using WorkflowDemo.Agents;
using WorkflowDemo.Services;
using WorkflowDemo.Tools;

var builder = Host.CreateApplicationBuilder(args);

// Ensure config files in the project root are picked up even when running from output dir.
builder.Configuration.Sources.Clear();
builder.Configuration
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

builder.Services.Configure<AiOptions>(builder.Configuration.GetSection("AI"));
builder.Services.Configure<StandardsOptions>(builder.Configuration.GetSection("Standards"));
builder.Services.Configure<CacheOptions>(builder.Configuration.GetSection("Cache"));
builder.Services.Configure<WorkflowOptions>(builder.Configuration.GetSection("Workflow"));

var aiConfig = builder.Configuration.GetSection("AI").Get<AiOptions>() ?? new AiOptions();
var apiKey = Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY") ?? aiConfig.ApiKey;

if (string.IsNullOrWhiteSpace(apiKey))
{
    Console.WriteLine("Error: DEEPSEEK_API_KEY environment variable is not set.");
    Console.WriteLine("Set it before running, e.g.: export DEEPSEEK_API_KEY=your-key");
    return 1;
}

var openAIClient = new OpenAIClient(
    new ApiKeyCredential(apiKey),
    new OpenAIClientOptions { Endpoint = new Uri(aiConfig.Endpoint) });
var chatClient = openAIClient.GetChatClient(aiConfig.ModelId).AsIChatClient();
builder.Services.AddSingleton(chatClient);

builder.Services.AddSingleton<IStandardLibraryStore, StandardLibraryStore>();
builder.Services.AddSingleton<IAnswerCache, AnswerCache>();
builder.Services.AddSingleton<StandardLibraryTools>();
builder.Services.AddSingleton<CacheTools>();
builder.Services.AddSingleton<AgentFactory>();
builder.Services.AddSingleton<WorkflowOrchestrator>(sp =>
{
    var factory = sp.GetRequiredService<AgentFactory>();
    var cache = sp.GetRequiredService<IAnswerCache>();
    var logger = sp.GetRequiredService<ILogger<WorkflowOrchestrator>>();
    var workflowOptions = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<WorkflowOptions>>().Value;
    return new WorkflowOrchestrator(factory, cache, logger, workflowOptions.MaxReviewIterations);
});

var app = builder.Build();

var orchestrator = app.Services.GetRequiredService<WorkflowOrchestrator>();
var workflowOptions = app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<WorkflowOptions>>().Value;

string question;
if (args.Length > 0)
{
    question = string.Join(" ", args);
}
else
{
    Console.Write("请输入检测行业技术问题: ");
    question = Console.ReadLine() ?? string.Empty;
}

if (string.IsNullOrWhiteSpace(question))
{
    Console.WriteLine("问题不能为空。");
    return 2;
}

var document = await orchestrator.RunAsync(question);

Directory.CreateDirectory(workflowOptions.OutputDirectory);
var fileName = $"{DateTime.Now:yyyyMMdd-HHmmss}.md";
var outputPath = Path.Combine(workflowOptions.OutputDirectory, fileName);
await File.WriteAllTextAsync(outputPath, document);

Console.WriteLine($"\n文档已生成: {outputPath}");
return 0;

public sealed class AiOptions
{
    public string Provider { get; set; } = "OpenAI";
    public string Endpoint { get; set; } = "https://api.deepseek.com";
    public string ModelId { get; set; } = "deepseek-v4-flash";
    public string ApiKey { get; set; } = string.Empty;
    public int MaxIterations { get; set; } = 3;
}

public sealed class WorkflowOptions
{
    public string OutputDirectory { get; set; } = "output";
    public int MaxReviewIterations { get; set; } = 2;
}
