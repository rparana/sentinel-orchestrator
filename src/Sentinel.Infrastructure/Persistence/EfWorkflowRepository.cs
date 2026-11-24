using Microsoft.EntityFrameworkCore;
using Sentinel.Application.Abstractions;
using Sentinel.Domain.Entities;
using Sentinel.Domain.Enums;

namespace Sentinel.Infrastructure.Persistence;

public sealed class EfWorkflowRepository : IWorkflowRepository
{
    private readonly SentinelDbContext _db;

    public EfWorkflowRepository(SentinelDbContext db)
    {
        _db = db;
    }

    public Task<WorkflowInstance?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _db.Workflows
            .Include(w => w.Steps.OrderBy(s => s.Order))
            .Include(w => w.AuditEntries.OrderBy(a => a.Timestamp))
            .FirstOrDefaultAsync(w => w.Id == id, cancellationToken);

    public async Task<IReadOnlyList<WorkflowInstance>> ListAsync(
        WorkflowStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Workflows
            .AsNoTracking()
            .Include(w => w.Steps)
            .AsQueryable();

        if (status.HasValue)
        {
            query = query.Where(w => w.Status == status.Value);
        }

        return await query
            .OrderByDescending(w => w.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public Task<bool> AnyAsync(CancellationToken cancellationToken = default) =>
        _db.Workflows.AnyAsync(cancellationToken);

    public async Task AddAsync(WorkflowInstance workflow, CancellationToken cancellationToken = default) =>
        await _db.Workflows.AddAsync(workflow, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _db.SaveChangesAsync(cancellationToken);
}
