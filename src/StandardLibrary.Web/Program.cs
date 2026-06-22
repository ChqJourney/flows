using Microsoft.EntityFrameworkCore;
using StandardLibrary.Web.Components;
using StandardLibrary.Web.Data;
using StandardLibrary.Web.Models;
using StandardLibrary.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// SQLite + EF Core
var dbPath = builder.Configuration.GetValue("StandardLibrary:DatabasePath", "standards.db");
var connectionString = $"Data Source={dbPath}";
builder.Services.AddDbContext<StandardDbContext>(options =>
    options.UseSqlite(connectionString));

// 业务服务
builder.Services.AddScoped<StandardArchiveService>();
builder.Services.AddScoped<StandardDomainService>();
builder.Services.AddSingleton<MarkdownRendererService>();

var app = builder.Build();

// Ensure database created
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<StandardDbContext>();
    db.Database.EnsureCreated();
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// 内部资源 API：为标准查看页面提供图片等资源
app.MapGet("/api/standards/{id:int}/resources/{*path}", async (int id, string path, StandardDbContext db) =>
{
    var record = await db.Standards.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id);
    if (record == null)
        return Results.NotFound();

    var safePath = path.Replace('/', Path.DirectorySeparatorChar).TrimStart(Path.DirectorySeparatorChar);
    var filePath = Path.GetFullPath(Path.Combine(record.StoragePath, safePath));
    var storageRoot = Path.GetFullPath(record.StoragePath);

    // 防止路径穿越
    if (!filePath.StartsWith(storageRoot + Path.DirectorySeparatorChar) && filePath != storageRoot)
        return Results.Forbid();

    if (!File.Exists(filePath))
        return Results.NotFound();

    var contentType = GetContentType(filePath);
    return Results.File(filePath, contentType);
});

app.Run();

static string GetContentType(string filePath)
{
    var ext = Path.GetExtension(filePath).ToLowerInvariant();
    return ext switch
    {
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".gif" => "image/gif",
        ".svg" => "image/svg+xml",
        ".webp" => "image/webp",
        ".bmp" => "image/bmp",
        _ => "application/octet-stream"
    };
}
