using Microsoft.EntityFrameworkCore;
using StandardLibrary.Web.Models;

namespace StandardLibrary.Web.Data;

public class StandardDbContext : DbContext
{
    public StandardDbContext(DbContextOptions<StandardDbContext> options) : base(options)
    {
    }

    public DbSet<StandardRecord> Standards => Set<StandardRecord>();

    public DbSet<StandardDomain> Domains => Set<StandardDomain>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<StandardRecord>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.StandardNumber).HasMaxLength(200).IsRequired();
            entity.Property(e => e.StandardNumberSlug).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Version).HasMaxLength(50).IsRequired();
            entity.Property(e => e.SourceFileHash).HasMaxLength(64).IsRequired();
            entity.Property(e => e.TitleCn).HasMaxLength(500);
            entity.Property(e => e.TitleEn).HasMaxLength(500);
            entity.Property(e => e.SourceFileName).HasMaxLength(260);
            entity.Property(e => e.StoragePath).HasMaxLength(1000);
            entity.Property(e => e.MarkdownFileName).HasMaxLength(100);
            entity.Property(e => e.ImagesFolderName).HasMaxLength(100);
            entity.Property(e => e.Replaces).HasMaxLength(500);
            entity.Property(e => e.ReplacedBy).HasMaxLength(200);
            entity.Property(e => e.Notes).HasMaxLength(2000);
            entity.Property(e => e.BaseStandardNumber).HasMaxLength(200);

            entity.HasOne(e => e.Domain)
                  .WithMany(d => d.Standards)
                  .HasForeignKey(e => e.DomainId)
                  .OnDelete(DeleteBehavior.SetNull);

            // 同一标准号同一版本只能有一条记录
            entity.HasIndex(e => new { e.StandardNumberSlug, e.Version }).IsUnique();
            // 文件哈希查重
            entity.HasIndex(e => e.SourceFileHash);
        });

        modelBuilder.Entity<StandardDomain>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
            entity.Property(e => e.DisplayOrder).HasDefaultValue(0);
            entity.HasIndex(e => e.Name).IsUnique();
        });
    }
}
