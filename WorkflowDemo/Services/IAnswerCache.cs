namespace WorkflowDemo.Services;

public interface IAnswerCache
{
    CacheEntry? Query(string question);
    void Save(string question, string document);
}

public sealed record CacheEntry(
    string Question,
    string NormalizedQuestion,
    string Document,
    DateTimeOffset CreatedAt);
