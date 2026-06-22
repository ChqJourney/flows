namespace StandardLibrary.Web.Models;

public class StandardRecord
{
    public int Id { get; set; }

    // 原始标准号，如 "GB/T 1234.1-2020"
    public string StandardNumber { get; set; } = "";

    // 标准化后的标识，如 "GB-T-1234.1-2020"
    public string StandardNumberSlug { get; set; } = "";

    // 版本号，优先从标准号解析，也可手动覆盖
    public string Version { get; set; } = "";

    // 去版本号后的标准号，用于把同一标准的多个版本归为一组
    public string BaseStandardNumber { get; set; } = "";

    public StandardStatus Status { get; set; }

    public string TitleCn { get; set; } = "";

    public string TitleEn { get; set; } = "";

    public DateTime? PublishDate { get; set; }

    public DateTime? EffectiveDate { get; set; }

    // 替代标准（逗号分隔多个）
    public string Replaces { get; set; } = "";

    // 被哪个标准替代
    public string ReplacedBy { get; set; } = "";

    // zip 文件 SHA256 哈希，用于查重
    public string SourceFileHash { get; set; } = "";

    // 原始上传文件名（保留字段，兼容旧数据）
    public string SourceFileName { get; set; } = "";

    // 归档文件夹绝对路径
    public string StoragePath { get; set; } = "";

    // MD 文件名（通常为 standard.md）
    public string MarkdownFileName { get; set; } = "standard.md";

    // 图片文件夹名（通常为 images）
    public string ImagesFolderName { get; set; } = "images";

    public string Notes { get; set; } = "";

    // 所属领域分类
    public int? DomainId { get; set; }

    public StandardDomain? Domain { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public string StatusDisplay => Status switch
    {
        StandardStatus.Current => "现行",
        StandardStatus.Superseded => "被替代",
        StandardStatus.Repealed => "废止",
        StandardStatus.Draft => "草案",
        _ => Status.ToString()
    };

    public string FolderDisplayName => $"{StandardNumberSlug}_{StatusDisplay}";
}
