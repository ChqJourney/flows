using Microsoft.EntityFrameworkCore;
using StandardLibrary.Web.Data;
using StandardLibrary.Web.Models;

namespace StandardLibrary.Web.Services;

public class StandardDomainService(StandardDbContext db)
{
    public Task<List<StandardDomain>> ListAsync()
        => db.Domains
            .AsNoTracking()
            .OrderBy(d => d.DisplayOrder)
            .ThenBy(d => d.Name)
            .ToListAsync();

    public async Task<StandardDomain> CreateAsync(string name)
    {
        var domain = new StandardDomain
        {
            Name = name.Trim(),
            DisplayOrder = 0,
            CreatedAt = DateTime.UtcNow
        };
        db.Domains.Add(domain);
        await db.SaveChangesAsync();
        return domain;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var domain = await db.Domains.FindAsync(id);
        if (domain == null) return false;

        await db.Standards
            .Where(s => s.DomainId == id)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.DomainId, (int?)null));

        db.Domains.Remove(domain);
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> AssignStandardAsync(int standardId, int? domainId)
    {
        var record = await db.Standards.FindAsync(standardId);
        if (record == null) return false;

        record.DomainId = domainId;
        record.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return true;
    }
}
