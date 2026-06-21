namespace WorkflowDemo.Services;

public record SearchResult(
    string Id,
    string Title,
    string FilePath,
    double Score,
    string Snippet);

public interface IStandardLibraryStore
{
    IReadOnlyList<SearchResult> Search(string query, int topN = 5);
    string? GetDetail(string standardId);
    string? ListSections(string standardId);
}
