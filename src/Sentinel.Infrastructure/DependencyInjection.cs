using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Sentinel.Application.Abstractions;
using Sentinel.Domain.Policies;
using Sentinel.Infrastructure.Execution;
using Sentinel.Infrastructure.Notifications;
using Sentinel.Infrastructure.Outbox;
using Sentinel.Infrastructure.Persistence;
using Sentinel.Infrastructure.Planners;
using StackExchange.Redis;

namespace Sentinel.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddSentinelInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        bool registerBackgroundWorker = true)
    {
        var postgresConnectionString = configuration.GetConnectionString("sentineldb")
            ?? throw new InvalidOperationException("Connection string 'sentineldb' is not configured.");

        var redisConnectionString = configuration.GetConnectionString("redis")
            ?? throw new InvalidOperationException("Connection string 'redis' is not configured.");

        services.AddDbContext<SentinelDbContext>(options =>
            options.UseNpgsql(postgresConnectionString, npgsql =>
                npgsql.MigrationsAssembly(typeof(SentinelDbContext).Assembly.FullName)));

        services.AddSingleton<IConnectionMultiplexer>(_ =>
            ConnectionMultiplexer.Connect(redisConnectionString));

        services.Configure<AgentPlannerOptions>(configuration.GetSection(AgentPlannerOptions.SectionName));
        services.Configure<OutboxOptions>(configuration.GetSection(OutboxOptions.SectionName));

        services.AddHttpClient(OllamaAgentPlanner.HttpClientName, (sp, client) =>
        {
            var opts = sp.GetRequiredService<IOptions<AgentPlannerOptions>>().Value;
            client.BaseAddress = new Uri(opts.OllamaEndpoint);
        });

        var plannerProvider = configuration[$"{AgentPlannerOptions.SectionName}:Provider"];
        if (string.Equals(plannerProvider, "Ollama", StringComparison.OrdinalIgnoreCase))
        {
            services.AddScoped<IAgentPlanner, OllamaAgentPlanner>();
        }
        else
        {
            services.AddScoped<IAgentPlanner, DeterministicMockPlanner>();
        }

        services.AddSingleton<IPolicyEvaluator, DeterministicPolicyEngine>();
        services.AddScoped<IWorkflowRepository, EfWorkflowRepository>();
        services.AddScoped<IStepExecutor, IdempotentStepExecutor>();
        services.TryAddSingleton<IWorkflowNotificationPublisher, NoOpWorkflowNotificationPublisher>();

        if (registerBackgroundWorker)
        {
            services.AddHostedService<OutboxProcessorWorker>();
        }
        else
        {
            services.AddSingleton<OutboxProcessorWorker>();
        }

        return services;
    }
}
