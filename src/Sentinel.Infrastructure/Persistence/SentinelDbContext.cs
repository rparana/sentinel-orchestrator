using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sentinel.Domain.Entities;

namespace Sentinel.Infrastructure.Persistence;

public sealed class SentinelDbContext : DbContext
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public SentinelDbContext(DbContextOptions<SentinelDbContext> options) : base(options)
    {
    }

    public DbSet<WorkflowInstance> Workflows => Set<WorkflowInstance>();
    public DbSet<WorkflowStep> WorkflowSteps => Set<WorkflowStep>();
    public DbSet<WorkflowAuditEntry> WorkflowAuditEntries => Set<WorkflowAuditEntry>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<WorkflowInstance>(entity =>
        {
            entity.ToTable("workflows");
            entity.HasKey(w => w.Id);
            entity.Property(w => w.Id).ValueGeneratedNever();

            entity.Property(w => w.Name)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(w => w.ScenarioKey)
                .HasMaxLength(100);

            entity.Property(w => w.Status)
                .HasConversion<string>()
                .HasMaxLength(40);

            // Map RowVersion to PostgreSQL system column 'xmin' for optimistic concurrency
            entity.Property(w => w.RowVersion)
                .IsRowVersion();

            entity.Ignore(w => w.DomainEvents);

            entity.HasMany(w => w.Steps)
                .WithOne()
                .HasForeignKey(s => s.WorkflowInstanceId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(w => w.AuditEntries)
                .WithOne()
                .HasForeignKey(a => a.WorkflowInstanceId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.Navigation(w => w.Steps).UsePropertyAccessMode(PropertyAccessMode.Field);
            entity.Navigation(w => w.AuditEntries).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<WorkflowStep>(entity =>
        {
            entity.ToTable("workflow_steps");
            entity.HasKey(s => s.Id);
            entity.Property(s => s.Id).ValueGeneratedNever();

            entity.Property(s => s.Name)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(s => s.Type)
                .HasConversion<string>()
                .HasMaxLength(40);

            entity.Property(s => s.Status)
                .HasConversion<string>()
                .HasMaxLength(40);

            entity.Property(s => s.DefaultRiskTier)
                .HasConversion<string>()
                .HasMaxLength(20);

            entity.Property(s => s.TargetEnvironment)
                .IsRequired()
                .HasMaxLength(60);

            entity.OwnsOne(s => s.ProposedAction, owned =>
            {
                owned.ToJson();
                owned.Property(p => p.RiskTier).HasConversion<string>();
            });

            entity.OwnsOne(s => s.PolicyDecision, owned =>
            {
                owned.ToJson();
                owned.Property(p => p.Verdict).HasConversion<string>();
            });

            entity.HasIndex(s => new { s.WorkflowInstanceId, s.Order }).IsUnique();
        });

        modelBuilder.Entity<WorkflowAuditEntry>(entity =>
        {
            entity.ToTable("workflow_audit_entries");
            entity.HasKey(a => a.Id);
            entity.Property(a => a.Id).ValueGeneratedNever();

            entity.Property(a => a.Actor)
                .IsRequired()
                .HasMaxLength(120);

            entity.Property(a => a.EventType)
                .IsRequired()
                .HasMaxLength(80);

            entity.Property(a => a.FromStatus)
                .HasConversion<string>()
                .HasMaxLength(40);

            entity.Property(a => a.ToStatus)
                .HasConversion<string>()
                .HasMaxLength(40);

            entity.Property(a => a.Details)
                .IsRequired()
                .HasMaxLength(2000);

            entity.HasIndex(a => new { a.WorkflowInstanceId, a.Timestamp });
        });

        modelBuilder.Entity<OutboxMessage>(entity =>
        {
            entity.ToTable("outbox_messages");
            entity.HasKey(o => o.Id);
            entity.Property(o => o.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(o => o.OccurredAt).HasColumnName("occurred_at");
            entity.Property(o => o.EventType).HasColumnName("event_type").IsRequired().HasMaxLength(150);
            entity.Property(o => o.PayloadJson).HasColumnName("payload_json").HasColumnType("jsonb").IsRequired();
            entity.Property(o => o.ProcessedAt).HasColumnName("processed_at");
            entity.Property(o => o.RetryCount).HasColumnName("retry_count");
            entity.Property(o => o.Error).HasColumnName("error").HasMaxLength(2000);

            entity.HasIndex(o => new { o.ProcessedAt, o.OccurredAt })
                .HasDatabaseName("ix_outbox_messages_unprocessed");
        });
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var aggregates = ChangeTracker
            .Entries<WorkflowInstance>()
            .Select(e => e.Entity)
            .Where(w => w.DomainEvents.Count > 0)
            .ToList();

        if (aggregates.Count > 0)
        {
            var outboxMessages = new List<OutboxMessage>();

            foreach (var aggregate in aggregates)
            {
                foreach (var domainEvent in aggregate.DomainEvents)
                {
                    outboxMessages.Add(new OutboxMessage
                    {
                        Id = domainEvent.EventId,
                        OccurredAt = domainEvent.OccurredAt,
                        EventType = domainEvent.GetType().Name,
                        PayloadJson = JsonSerializer.Serialize(domainEvent, domainEvent.GetType(), SerializerOptions)
                    });
                }

                aggregate.ClearDomainEvents();
            }

            await OutboxMessages.AddRangeAsync(outboxMessages, cancellationToken);
        }

        return await base.SaveChangesAsync(cancellationToken);
    }
}
