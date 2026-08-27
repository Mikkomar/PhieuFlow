using Microsoft.EntityFrameworkCore;
using PhieuFlow.Core.Entities;

namespace PhieuFlow.Persistence;

public class HubDbContext(DbContextOptions<HubDbContext> options) : DbContext(options)
{
    public DbSet<Form> Forms => Set<Form>();
    public DbSet<FormVersion> FormVersions => Set<FormVersion>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(HubDbContext).Assembly);
    }
}
