using Microsoft.EntityFrameworkCore;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Repositories;
using SupportDesk.Infrastructure.Data;

namespace SupportDesk.Infrastructure.Repositories;

public sealed class CustomerRepository(SupportDbContext db) : ICustomerRepository
{
    public Task<bool> ExistsAsync(int id, CancellationToken ct) =>
        db.Set<Customer>().AsNoTracking().AnyAsync(c => c.Id == id, ct);

    public Task<Customer?> GetByIdAsync(int id, CancellationToken ct) =>
        db.Set<Customer>().AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, ct);
}