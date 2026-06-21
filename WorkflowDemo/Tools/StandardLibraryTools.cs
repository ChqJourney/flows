using System.ComponentModel;
using System.Text;
using WorkflowDemo.Services;

namespace WorkflowDemo.Tools;

public sealed class StandardLibraryTools
{
    private readonly IStandardLibraryStore _store;

    public StandardLibraryTools(IStandardLibraryStore store)
    {
        _store = store;
    }

    [Description("Search the standards library for documents relevant to the given query. Returns a ranked list of standard IDs, titles, and snippets.")]
    public string SearchStandards(
        [Description("The user question or keywords to search for.")] string query,
        [Description("Maximum number of results to return. Default is 5.")] int topN = 5)
    {
        var results = _store.Search(query, topN);
        if (results.Count == 0) return "No matching standards found.";

        var sb = new StringBuilder();
        sb.AppendLine($"Found {results.Count} standard(s):");
        foreach (var r in results)
        {
            sb.AppendLine($"- ID: {r.Id}");
            sb.AppendLine($"  Title: {r.Title}");
            sb.AppendLine($"  Snippet: {r.Snippet}");
        }

        return sb.ToString();
    }

    [Description("Get the full Markdown content of a specific standard by its ID.")]
    public string GetStandardDetail(
        [Description("The standard ID returned by SearchStandards, usually the folder name.")] string standardId)
    {
        var detail = _store.GetDetail(standardId);
        return detail ?? $"Standard '{standardId}' not found.";
    }

    [Description("List the main sections (headings) of a specific standard by its ID.")]
    public string ListStandardSections(
        [Description("The standard ID returned by SearchStandards.")] string standardId)
    {
        var sections = _store.ListSections(standardId);
        return sections ?? $"Standard '{standardId}' not found.";
    }
}
