using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Sentinel.Infrastructure.Persistence;

public sealed class SentinelDbContextFactory : IDesignTimeDbContextFactory<SentinelDbContext>
{
    public SentinelDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<SentinelDbContext>();
        optionsBuilder.UseNpgsql("Host=localhost;Port=5432;Database=sentineldb;Username=sentinel;Password=sentinel_dev_pass");
        return new SentinelDbContext(optionsBuilder.Options);
    }
}
