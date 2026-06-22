namespace StandardLibrary.Web.Models;

public class StandardUploadRequest
{
    public string StandardNumber { get; set; } = "";

    // 去版本号后的标准号，用于把同一标准的多个版本归为一组
    public string BaseStandardNumber { get; set; } = "";

    public StandardStatus Status { get; set; } = StandardStatus.Current;
}
