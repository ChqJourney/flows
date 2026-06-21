using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace WorkflowDemo.Services;

public class StandardsOptions
{
    public string LibraryPath { get; set; } = string.Empty;
}

public sealed class StandardLibraryStore : IStandardLibraryStore
{
    private sealed record Document(
        string Id,
        string Title,
        string FilePath,
        string Content,
        IReadOnlyList<(int Level, string Title)> Sections);

    private readonly IReadOnlyDictionary<string, Document> _documents;

    public StandardLibraryStore(IOptions<StandardsOptions> options)
    {
        var path = options.Value.LibraryPath;
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            _documents = new Dictionary<string, Document>(StringComparer.OrdinalIgnoreCase);
            return;
        }

        _documents = LoadDocuments(path);
    }

    private static Dictionary<string, Document> LoadDocuments(string root)
    {
        var docs = new Dictionary<string, Document>(StringComparer.OrdinalIgnoreCase);
        foreach (var dir in Directory.EnumerateDirectories(root))
        {
            var mdFiles = Directory.EnumerateFiles(dir, "*.md").ToList();
            if (mdFiles.Count == 0) continue;

            var filePath = mdFiles[0];
            var id = Path.GetFileName(dir).Trim();
            var content = File.ReadAllText(filePath, Encoding.UTF8);
            var title = ExtractTitle(content) ?? id;
            var sections = ExtractSections(content);

            docs[id] = new Document(id, title, filePath, content, sections);
        }

        return docs;
    }

    private static string? ExtractTitle(string content)
    {
        var match = Regex.Match(content, @"^#\s+(.+)$", RegexOptions.Multiline);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    private static IReadOnlyList<(int Level, string Title)> ExtractSections(string content)
    {
        var matches = Regex.Matches(content, @"^(#{1,6})\s+(.+)$", RegexOptions.Multiline);
        return matches
            .Select(m => (m.Groups[1].Value.Length, m.Groups[2].Value.Trim()))
            .ToList();
    }

    public IReadOnlyList<SearchResult> Search(string query, int topN = 5)
    {
        if (_documents.Count == 0 || string.IsNullOrWhiteSpace(query))
            return Array.Empty<SearchResult>();

        var terms = Tokenize(query).ToHashSet();
        var scores = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        foreach (var doc in _documents.Values)
        {
            double score = 0;
            var titleTokens = Tokenize(doc.Title).ToHashSet();
            var sectionTokens = Tokenize(string.Join(" ", doc.Sections.Select(s => s.Title))).ToHashSet();
            var contentTokens = Tokenize(doc.Content).ToList();

            foreach (var term in terms)
            {
                if (titleTokens.Contains(term)) score += 10;
                if (sectionTokens.Contains(term)) score += 5;
                score += contentTokens.Count(t => t == term) * 0.5;
            }

            if (score > 0)
                scores[doc.Id] = score;
        }

        return scores
            .OrderByDescending(kv => kv.Value)
            .Take(topN)
            .Select(kv =>
            {
                var doc = _documents[kv.Key];
                return new SearchResult(
                    doc.Id,
                    doc.Title,
                    doc.FilePath,
                    kv.Value,
                    Snippet(doc.Content, query));
            })
            .ToList();
    }

    public string? GetDetail(string standardId)
    {
        return _documents.TryGetValue(standardId, out var doc)
            ? $"# {doc.Title}\n\n{doc.Content}"
            : null;
    }

    public string? ListSections(string standardId)
    {
        if (!_documents.TryGetValue(standardId, out var doc)) return null;

        var sb = new StringBuilder();
        sb.AppendLine($"Standard: {doc.Title}");
        foreach (var (level, title) in doc.Sections)
        {
            sb.AppendLine($"{new string(' ', (level - 1) * 2)}- {title}");
        }

        return sb.ToString();
    }

    private static string Snippet(string content, string query)
    {
        var lowerContent = content.ToLowerInvariant();
        var lowerQuery = query.ToLowerInvariant();
        var idx = lowerContent.IndexOf(lowerQuery, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) idx = 0;
        var start = Math.Max(0, idx - 80);
        var length = Math.Min(240, content.Length - start);
        var snippet = content.Substring(start, length).Replace('\n', ' ');
        return $"...{snippet}...";
    }

    private static IEnumerable<string> Tokenize(string text)
    {
        return Regex.Split(text.ToLowerInvariant(), @"[^\w\u4e00-\u9fa5]+")
            .Where(t => t.Length > 1)
            .Distinct();
    }
}
