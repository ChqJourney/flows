using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using StandardLibrary.Web.Data;
using StandardLibrary.Web.Models;

namespace StandardLibrary.Web.Services;

public class StandardArchiveService
{
    private readonly StandardDbContext _db;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<StandardArchiveService> _logger;

    public StandardArchiveService(StandardDbContext db, IWebHostEnvironment env, ILogger<StandardArchiveService> logger)
    {
        _db = db;
        _env = env;
        _logger = logger;
    }

    /// <summary>
    /// 归档根目录，配置中读取，默认为项目根目录下的 standards/
    /// </summary>
    private string StorageRoot => Path.Combine(_env.ContentRootPath, "standards");

    /// <summary>
    /// 上传一个 zip 包，包内需包含一个 .md 文件及可选的 images/ 文件夹。
    /// 当 skipDuplicateCheck 为 false 时，若文件哈希或标准号+版本已存在会返回 IsDuplicate=true。
    /// 用户确认后可将 skipDuplicateCheck 设为 true 强制入库。
    /// </summary>
    public async Task<ArchiveResult> ArchiveFromZipAsync(StandardUploadRequest request, Stream zipStream, bool skipDuplicateCheck = false, CancellationToken ct = default)
    {
        var slug = StandardSlugService.ToSlug(request.StandardNumber);
        var version = StandardSlugService.ExtractVersion(request.StandardNumber);
        var baseStandardNumber = string.IsNullOrWhiteSpace(request.BaseStandardNumber)
            ? StandardSlugService.ExtractBaseStandardNumber(request.StandardNumber)
            : request.BaseStandardNumber.Trim();

        if (string.IsNullOrWhiteSpace(slug))
            return ArchiveResult.Fail("标准号不能为空或无法识别");

        // 检查同标准号同版本是否已存在
        var existing = await _db.Standards
            .FirstOrDefaultAsync(s => s.StandardNumberSlug == slug && s.Version == version, ct);

        if (existing != null && !skipDuplicateCheck)
            return ArchiveResult.DuplicateStandardNumber(existing.Id, existing.StandardNumber);

        // 创建目标文件夹
        var folderName = StandardSlugService.BuildFolderName(request.StandardNumber, request.Status);
        var targetDir = Path.Combine(StorageRoot, folderName);

        if (Directory.Exists(targetDir))
        {
            // 如果文件夹已存在（不同版本或不同状态），加序号
            targetDir = MakeUniqueDirectory(targetDir);
            folderName = Path.GetFileName(targetDir) ?? folderName;
        }

        Directory.CreateDirectory(targetDir);

        // Blazor Server 传入的 zipStream 不支持同步读取，先异步复制到本地临时文件
        var tempZipPath = Path.Combine(targetDir, $"__upload_{Guid.NewGuid():N}.zip");
        string fileHash;
        try
        {
            await using (var fileStream = new FileStream(tempZipPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 81920, useAsync: true))
            {
                await zipStream.CopyToAsync(fileStream, ct);
            }

            await zipStream.DisposeAsync();

            // 计算 zip 文件 SHA256
            await using (var hashStream = new FileStream(tempZipPath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 81920, useAsync: true))
            using (var sha256 = SHA256.Create())
            {
                var hashBytes = await sha256.ComputeHashAsync(hashStream, ct);
                fileHash = Convert.ToHexString(hashBytes);
            }
        }
        catch (Exception ex)
        {
            TryDelete(tempZipPath);
            return ArchiveResult.Fail($"读取 zip 文件失败：{ex.Message}");
        }

        // 文件哈希查重
        if (!skipDuplicateCheck)
        {
            var duplicate = await _db.Standards
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.SourceFileHash == fileHash, ct);

            if (duplicate != null)
                return ArchiveResult.DuplicateFile(duplicate.Id, duplicate.StandardNumber);
        }

        // 解压 zip
        var extractDir = Path.Combine(targetDir, "__upload");
        Directory.CreateDirectory(extractDir);
        try
        {
            await using (var fileStream = new FileStream(tempZipPath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 81920, useAsync: false))
            using (var archive = new ZipArchive(fileStream, ZipArchiveMode.Read, leaveOpen: false))
            {
                archive.ExtractToDirectory(extractDir, overwriteFiles: true);
            }

            // 查找 .md 文件
            var mdFiles = Directory.GetFiles(extractDir, "*.md", SearchOption.AllDirectories);
            if (mdFiles.Length == 0)
                return ArchiveResult.Fail("上传的 zip 中没有找到 .md 文件");

            // 如果有多个 md，取最上层或按字母第一个；优先取非 README 的
            var mdFile = mdFiles
                .OrderBy(f => f.Count(c => c == Path.DirectorySeparatorChar))
                .ThenBy(f => Path.GetFileName(f).StartsWith("README", StringComparison.OrdinalIgnoreCase))
                .ThenBy(f => f)
                .First();

            // 移动 md 文件到目标目录根
            var targetMdPath = Path.Combine(targetDir, "standard.md");
            File.Move(mdFile, targetMdPath);

            // 处理图片文件夹：查找解压后所有 images 文件夹，合并到目标目录
            var imagesDirs = Directory.GetDirectories(extractDir, "images", SearchOption.AllDirectories)
                .Concat(Directory.GetDirectories(extractDir, "Images", SearchOption.AllDirectories))
                .ToList();

            var targetImagesDir = Path.Combine(targetDir, "images");
            Directory.CreateDirectory(targetImagesDir);

            foreach (var imgDir in imagesDirs)
            {
                foreach (var file in Directory.GetFiles(imgDir, "*", SearchOption.AllDirectories))
                {
                    var relative = Path.GetRelativePath(imgDir, file);
                    var dest = Path.Combine(targetImagesDir, relative);
                    Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                    File.Copy(file, dest, overwrite: true);
                }
            }

            // 修正 markdown 中的图片路径为相对路径
            await NormalizeImagePathsAsync(targetMdPath, targetImagesDir, ct);

            // 保存数据库记录
            var record = new StandardRecord
            {
                StandardNumber = request.StandardNumber.Trim(),
                StandardNumberSlug = slug,
                Version = version,
                BaseStandardNumber = baseStandardNumber,
                Status = request.Status,
                SourceFileHash = fileHash,
                StoragePath = targetDir,
                MarkdownFileName = "standard.md",
                ImagesFolderName = "images",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _db.Standards.Add(record);
            await _db.SaveChangesAsync(ct);

            _logger.LogInformation("已归档标准 {StandardNumber} 到 {Path}", record.StandardNumber, record.StoragePath);

            return ArchiveResult.Ok(record);
        }
        finally
        {
            // 清理临时解压目录和临时 zip
            if (Directory.Exists(extractDir))
            {
                try { Directory.Delete(extractDir, recursive: true); } catch { /* ignore */ }
            }
            TryDelete(tempZipPath);
        }
    }

    /// <summary>
    /// 删除标准及其归档文件夹。
    /// </summary>
    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var record = await _db.Standards.FindAsync(new object[] { id }, ct);
        if (record == null)
            throw new InvalidOperationException("标准不存在");

        _db.Standards.Remove(record);
        await _db.SaveChangesAsync(ct);

        if (Directory.Exists(record.StoragePath))
        {
            try
            {
                Directory.Delete(record.StoragePath, recursive: true);
                _logger.LogInformation("已删除标准 {StandardNumber} 的归档目录", record.StandardNumber);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "删除标准 {StandardNumber} 的归档目录失败: {Path}", record.StandardNumber, record.StoragePath);
            }
        }
    }

    private static void TryDelete(string path)
    {
        if (string.IsNullOrEmpty(path))
            return;
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch { /* ignore */ }
    }

    /// <summary>
    /// 如果目标目录已存在，追加序号。
    /// </summary>
    private static string MakeUniqueDirectory(string path)
    {
        if (!Directory.Exists(path))
            return path;

        var parent = Path.GetDirectoryName(path)!;
        var name = Path.GetFileName(path);
        var counter = 1;
        string candidate;
        do
        {
            candidate = Path.Combine(parent, $"{name}_{counter}");
            counter++;
        } while (Directory.Exists(candidate));

        return candidate;
    }

    /// <summary>
    /// 将 markdown 中指向临时或深层目录的图片路径统一修正为相对 images/ 的路径。
    /// </summary>
    private static async Task NormalizeImagePathsAsync(string mdFilePath, string imagesDir, CancellationToken ct)
    {
        if (!File.Exists(mdFilePath))
            return;

        var content = await File.ReadAllTextAsync(mdFilePath, ct);

        // 匹配 ![](...images/xxx...) 或 <img src="...images/xxx...">
        content = Regex.Replace(content, @"(!\[.*?\]\()([^)]+images[\\/])([^)]+)(\))", m =>
        {
            var filename = m.Groups[3].Value.TrimStart('/', '\\');
            return $"{m.Groups[1].Value}images/{filename}{m.Groups[4].Value}";
        }, RegexOptions.IgnoreCase);

        content = Regex.Replace(content, @"(<img[^>]+src=[""'])([^""']+images[\\/])([^""']+)([""'])", m =>
        {
            var filename = m.Groups[3].Value.TrimStart('/', '\\');
            return $"{m.Groups[1].Value}images/{filename}{m.Groups[4].Value}";
        }, RegexOptions.IgnoreCase);

        await File.WriteAllTextAsync(mdFilePath, content, ct);
    }
}
