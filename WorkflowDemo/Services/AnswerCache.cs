using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace WorkflowDemo.Services;

public class CacheOptions
{
    public string FilePath { get; set; } = "cache/outputs.json";
}

public sealed class AnswerCache : IAnswerCache
{
    private readonly string _filePath;
    private readonly List<CacheEntry> _entries;
    private readonly object _lock = new();

    public AnswerCache(IOptions<CacheOptions> options)
    {
        _filePath = options.Value.FilePath;
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_filePath))!);
        _entries = Load();
    }

    private List<CacheEntry> Load()
    {
        if (!File.Exists(_filePath)) return new List<CacheEntry>();
        try
        {
            var json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<List<CacheEntry>>(json) ?? new List<CacheEntry>();
        }
        catch
        {
            return new List<CacheEntry>();
        }
    }

    private void Persist()
    {
        var json = JsonSerializer.Serialize(_entries, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_filePath, json);
    }

    public CacheEntry? Query(string question)
    {
        var normalized = Normalize(question);
        lock (_lock)
        {
            return _entries.FirstOrDefault(e => e.NormalizedQuestion == normalized);
        }
    }

    public void Save(string question, string document)
    {
        var normalized = Normalize(question);
        lock (_lock)
        {
            var existing = _entries.FirstOrDefault(e => e.NormalizedQuestion == normalized);
            if (existing != null)
            {
                _entries.Remove(existing);
            }

            _entries.Insert(0, new CacheEntry(question, normalized, document, DateTimeOffset.Now));
            Persist();
        }
    }

    private static string Normalize(string question)
    {
        var cleaned = Regex.Replace(question.ToLowerInvariant(), @"[^\w\u4e00-\u9fa5]", " ");
        return Regex.Replace(cleaned, @"\s+", " ").Trim();
    }
}
