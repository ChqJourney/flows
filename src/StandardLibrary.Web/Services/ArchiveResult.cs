using StandardLibrary.Web.Models;

namespace StandardLibrary.Web.Services;

public enum DuplicateType
{
    None,
    FileHash,           // 同一 zip 文件已上传
    StandardNumber      // 同一标准号+版本已存在
}

public class ArchiveResult
{
    public bool Success { get; set; }
    public bool IsDuplicate { get; set; }
    public DuplicateType DuplicateType { get; set; }
    public StandardRecord? Record { get; set; }
    public int ExistingId { get; set; }
    public string ExistingStandardNumber { get; set; } = "";
    public string ErrorMessage { get; set; } = "";

    public static ArchiveResult Ok(StandardRecord record) =>
        new() { Success = true, Record = record };

    public static ArchiveResult DuplicateFile(int existingId, string existingStandardNumber) =>
        new() { Success = false, IsDuplicate = true, DuplicateType = DuplicateType.FileHash, ExistingId = existingId, ExistingStandardNumber = existingStandardNumber };

    public static ArchiveResult DuplicateStandardNumber(int existingId, string existingStandardNumber) =>
        new() { Success = false, IsDuplicate = true, DuplicateType = DuplicateType.StandardNumber, ExistingId = existingId, ExistingStandardNumber = existingStandardNumber };

    public static ArchiveResult Fail(string errorMessage) =>
        new() { Success = false, ErrorMessage = errorMessage };
}
