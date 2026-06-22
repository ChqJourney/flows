namespace StandardLibrary.Web.Models;

public class StandardDomain
{
    public int Id { get; set; }

    // 领域名称，如 "照明电器领域"
    public string Name { get; set; } = "";

    // 排序权重，越小越靠前
    public int DisplayOrder { get; set; }

    public DateTime CreatedAt { get; set; }

    // 导航属性
    public ICollection<StandardRecord> Standards { get; set; } = [];
}
