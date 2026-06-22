using Markdig;
using Markdig.Extensions.Mathematics;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using StandardLibrary.Web.Models;

namespace StandardLibrary.Web.Services;

public class MarkdownRendererService
{
    private readonly LinkGenerator _linkGenerator;

    public MarkdownRendererService(LinkGenerator linkGenerator)
    {
        _linkGenerator = linkGenerator;
    }

    /// <summary>
    /// 将标准对应的 markdown 文件渲染为 HTML，并把图片路径替换为内部 API 可访问的 URL。
    /// </summary>
    public string RenderToHtml(StandardRecord record)
    {
        var mdPath = Path.Combine(record.StoragePath, record.MarkdownFileName);
        if (!File.Exists(mdPath))
            return $"<p class=\"alert alert-danger\">找不到 Markdown 文件：{System.Net.WebUtility.HtmlEncode(mdPath)}</p>";

        var markdown = File.ReadAllText(mdPath);

        // 使用 Markdig 管道，启用数学公式扩展（$...$ / $$...$$）
        var pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .UseMathematics()
            .Build();

        var document = Markdown.Parse(markdown, pipeline);

        foreach (var link in document.Descendants<LinkInline>().Where(l => l.IsImage))
        {
            var originalUrl = link.Url;
            if (string.IsNullOrWhiteSpace(originalUrl))
                continue;

            // 只处理相对路径的图片（绝对路径和外部链接不处理）
            if (Uri.IsWellFormedUriString(originalUrl, UriKind.Absolute))
                continue;

            var normalized = originalUrl.Replace('\\', '/').TrimStart('/');
            var apiUrl = $"/api/standards/{record.Id}/resources/{normalized}";
            link.Url = apiUrl;
        }

        return document.ToHtml(pipeline);
    }
}
