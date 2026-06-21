using System.ComponentModel;
using WorkflowDemo.Services;

namespace WorkflowDemo.Tools;

public sealed class CacheTools
{
    private readonly IAnswerCache _cache;

    public CacheTools(IAnswerCache cache)
    {
        _cache = cache;
    }

    [Description("Check whether a similar question has already been answered. If found, returns the cached Markdown document.")]
    public string QueryCache(
        [Description("The user's original question.")] string question)
    {
        var entry = _cache.Query(question);
        if (entry is null)
        {
            return "CACHE_MISS";
        }

        return $"CACHE_HIT\nCreatedAt: {entry.CreatedAt:O}\n\n{entry.Document}";
    }
}
