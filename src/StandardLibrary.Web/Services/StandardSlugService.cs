using System.Text.RegularExpressions;
using StandardLibrary.Web.Models;

namespace StandardLibrary.Web.Services;

public static class StandardSlugService
{
    /// <summary>
    /// 将原始标准号转换为文件系统安全的 slug。
    /// 例如 GB/T 1234.1-2020 → GB-T-1234.1-2020
    /// </summary>
    public static string ToSlug(string standardNumber)
    {
        if (string.IsNullOrWhiteSpace(standardNumber))
            return string.Empty;

        var slug = standardNumber
            .Replace('/', '-')
            .Replace('\\', '-')
            .Replace(':', '-')
            .Replace(" ", "-")
            .Replace('_', '-');

        // 仅保留字母、数字、点、连字符
        slug = Regex.Replace(slug, @"[^A-Za-z0-9\-\.]", "-");
        // 合并连续连字符
        slug = Regex.Replace(slug, @"-+", "-").Trim('-');
        return slug.ToUpperInvariant();
    }

    /// <summary>
    /// 尝试从标准号中解析出版本年号。
    /// 例如 GB/T 1234.1-2020 → 2020；IEC 61010-1:2010 → 2010
    /// </summary>
    public static string ExtractVersion(string standardNumber)
    {
        if (string.IsNullOrWhiteSpace(standardNumber))
            return string.Empty;

        // 匹配末尾的 4 位年份，前面是 - 或 : 或空格
        var match = Regex.Match(standardNumber, @"[\-\:\s](\d{4})(?:\s*$|[\-/])");
        if (match.Success)
            return match.Groups[1].Value;

        // 兜底：直接找最后 4 位数字
        var digits = Regex.Matches(standardNumber, @"\d{4}");
        if (digits.Count > 0)
            return digits[^1].Value;

        return string.Empty;
    }

    /// <summary>
    /// 从原始标准号中去掉版本号，得到用于分组的基标准号。
    /// 例如 GB/T 1234.1-2020 → GB/T 1234.1；IEC 61010-1:2010 → IEC 61010-1
    /// </summary>
    public static string ExtractBaseStandardNumber(string standardNumber)
    {
        if (string.IsNullOrWhiteSpace(standardNumber))
            return string.Empty;

        var version = ExtractVersion(standardNumber);
        if (string.IsNullOrWhiteSpace(version))
            return standardNumber.Trim();

        // 定位版本号最后一次出现的位置，并连同前面分隔符一起移除
        var index = standardNumber.LastIndexOf(version, StringComparison.Ordinal);
        if (index <= 0)
            return standardNumber.Trim();

        var prefix = standardNumber[..index];
        var suffix = standardNumber[(index + version.Length)..];

        // 如果版本号后面还有字母或数字，说明不是末尾的版本号，不做处理
        if (!string.IsNullOrEmpty(suffix) && suffix.Any(char.IsLetterOrDigit))
            return standardNumber.Trim();

        prefix = prefix.TrimEnd(' ', '-', ':', '_');

        return string.IsNullOrWhiteSpace(prefix)
            ? standardNumber.Trim()
            : prefix;
    }

    /// <summary>
    /// 尝试从上传的 zip 文件名中还原标准号。
    /// 例如 "GB-T-1234.1-2020.zip" → "GB/T 1234.1-2020"
    /// </summary>
    public static string StandardNumberFromFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return string.Empty;

        // 去掉 .zip 扩展名
        var name = fileName.Trim();
        if (name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            name = name[..^4];

        // 把下划线替换为空格
        name = name.Replace('_', ' ');

        // 尝试把开头的 XXX-T- 还原为 XXX/T（如 GB-T → GB/T）
        name = Regex.Replace(name, @"^([A-Za-z]+)-T-", m => $"{m.Groups[1].Value.ToUpperInvariant()}/T ", RegexOptions.IgnoreCase);

        // 尝试把开头的 XXX-（无 T）还原为 XXX （如 IEC-61010-1-2010 → IEC 61010-1-2010）
        name = Regex.Replace(name, @"^([A-Za-z]+)-", m => $"{m.Groups[1].Value.ToUpperInvariant()} ", RegexOptions.IgnoreCase);

        // 清理多余空格和连字符
        name = Regex.Replace(name, @"\s+", " ").Trim();
        name = Regex.Replace(name, @"-+", "-");

        return name;
    }

    /// <summary>
    /// 生成归档文件夹名称。
    /// </summary>
    public static string BuildFolderName(string standardNumber, StandardStatus status)
    {
        var slug = ToSlug(standardNumber);
        var statusSuffix = status switch
        {
            StandardStatus.Current => "current",
            StandardStatus.Superseded => "superseded",
            StandardStatus.Repealed => "repealed",
            StandardStatus.Draft => "draft",
            _ => status.ToString().ToLowerInvariant()
        };

        return $"{slug}_{statusSuffix}";
    }
}
