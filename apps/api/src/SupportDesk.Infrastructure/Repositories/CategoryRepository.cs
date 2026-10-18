using Microsoft.EntityFrameworkCore;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Repositories;
using SupportDesk.Infrastructure.Data;

namespace SupportDesk.Infrastructure.Repositories;

public sealed class CategoryRepository(SupportDbContext db) : ICategoryRepository
{
    public Task<bool> ExistsAsync(int id, CancellationToken ct) =>
        db.Set<Category>().AsNoTracking().AnyAsync(c => c.Id == id, ct);

    public Task<Category?> GetByIdAsync(int id, CancellationToken ct) =>
        db.Set<Category>().AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, ct);
}