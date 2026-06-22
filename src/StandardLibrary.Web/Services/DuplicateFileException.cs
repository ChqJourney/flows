namespace StandardLibrary.Web.Services;

public class DuplicateFileException : Exception
{
    public string FileHash { get; }
    public int ExistingId { get; }
    public string ExistingStandardNumber { get; }

    public DuplicateFileException(string fileHash, int existingId, string existingStandardNumber)
        : base($"该文件已作为 {existingStandardNumber} 上传")
    {
        FileHash = fileHash;
        ExistingId = existingId;
        ExistingStandardNumber = existingStandardNumber;
    }
}
