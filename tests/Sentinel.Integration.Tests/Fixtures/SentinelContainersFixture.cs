using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sentinel.Infrastructure;
using Sentinel.Infrastructure.Persistence;
using StackExchange.Redis;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;
using Xunit;

namespace Sentinel.Integration.Tests.Fixtures;

public sealed class SentinelContainersFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("sentineldb")
        .WithUsername("sentinel")
        .WithPassword("sentinel_test_pass")
        .Build();

    private readonly RedisContainer _redis = new RedisBuilder("redis:7-alpine")
        .Build();

    public string PostgresConnectionString => _postgres.GetConnectionString();
    public string RedisConnectionString => _redis.GetConnectionString();

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _redis.StartAsync());

        await using var provider = BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SentinelDbContext>();
        await db.Database.MigrateAsync();
    }

    public ServiceProvider BuildServiceProvider()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:sentineldb"] = PostgresConnectionString,
                ["ConnectionStrings:redis"] = RedisConnectionString,
                ["AgentPlanner:Provider"] = "Mock",
                ["AgentPlanner:SimulatedLatencyMs"] = "0"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSentinelInfrastructure(config, registerBackgroundWorker: false);
        return services.BuildServiceProvider();
    }

    public SentinelDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SentinelDbContext>()
            .UseNpgsql(PostgresConnectionString)
            .Options;

        return new SentinelDbContext(options);
    }

    public IConnectionMultiplexer CreateRedisConnection() =>
        ConnectionMultiplexer.Connect(RedisConnectionString);

    public async Task DisposeAsync()
    {
        await Task.WhenAll(
            _postgres.DisposeAsync().AsTask(),
            _redis.DisposeAsync().AsTask());
    }
}

[CollectionDefinition(Name)]
public sealed class SentinelContainersCollection : ICollectionFixture<SentinelContainersFixture>
{
    public const string Name = "SentinelContainers";
}
