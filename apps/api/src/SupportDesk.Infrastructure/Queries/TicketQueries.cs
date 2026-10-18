using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Configuration;
using SupportDesk.Application.Contracts.Common;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Infrastructure.Data;

namespace SupportDesk.Infrastructure.Queries;

/// <summary>
/// Ticket reads. Projected in the database and never tracked. Aggregates hold only each
/// other's ids, so the customer, category and agent labels are joined in here, on the read side.
/// </summary>
public sealed class TicketQueries(SupportDbContext db, IClock clock, IOptions<SlaOptions> slaOptions) : ITicketQueries
{
    public async Task<PagedResult<TicketListItemDto>> GetPagedAsync(TicketQuery query, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var atRiskThreshold = slaOptions.Value.AtRiskThresholdPercent / 100.0;

        var tickets = TicketsWithLabels();

        // Apply the filters before counting or sorting.
        tickets = ApplyFilters(tickets, query, now, atRiskThreshold);

        // Count only the filtered rows.
        var totalCount = await tickets.CountAsync(ct);

        var items = await ApplySort(tickets, query)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(x => new TicketListItemDto
            {
                Id = x.Ticket.Id,
                Reference = x.Ticket.Reference,
                Title = x.Ticket.Title,
                Status = x.Ticket.Status,
                Priority = x.Ticket.Priority,
                Customer = new CustomerSummaryDto(x.Customer.Id, x.Customer.Name, x.Customer.Tier),
                Category = new CategorySummaryDto(x.Category.Id, x.Category.Name),
                AssignedAgent = x.Agent == null ? null : new AgentSummaryDto(x.Agent.Id, x.Agent.FullName),
                CreatedAtUtc = x.Ticket.CreatedAtUtc,
                UpdatedAtUtc = x.Ticket.UpdatedAtUtc,
                DueAtUtc = x.Ticket.DueAtUtc,
                ResolvedAtUtc = x.Ticket.ResolvedAtUtc,
                // SlaWindowStartUtc (set on raise and reset on escalation) gives the correct window
                // size even after escalation restarts the due date (BR-8).
                SlaStatus = SlaEvaluator.Evaluate(
                    x.Ticket.SlaWindowStartUtc,
                    x.Ticket.DueAtUtc,
                    x.Ticket.ResolvedAtUtc,
                    now,
                    atRiskThreshold)
            })
            .ToListAsync(ct);

        return new PagedResult<TicketListItemDto>(items, query.Page, query.PageSize, totalCount);
    }

    public Task<TicketDetailDto?> GetDetailAsync(int id, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var atRiskThreshold = slaOptions.Value.AtRiskThresholdPercent / 100.0;

        return TicketsWithLabels()
            .Where(x => x.Ticket.Id == id)
            .Select(x => new TicketDetailDto
            {
                Id = x.Ticket.Id,
                Reference = x.Ticket.Reference,
                Title = x.Ticket.Title,
                Description = x.Ticket.Description,
                Status = x.Ticket.Status,
                Priority = x.Ticket.Priority,
                Customer = new CustomerContactDto(
                    x.Customer.Id, x.Customer.Name, x.Customer.Email, x.Customer.Phone, x.Customer.Tier),
                Category = new CategoryDto(x.Category.Id, x.Category.Name, x.Category.RequiresSpecialist),
                AssignedAgent = x.Agent == null ? null : new AgentSummaryDto(x.Agent.Id, x.Agent.FullName),
                CreatedAtUtc = x.Ticket.CreatedAtUtc,
                UpdatedAtUtc = x.Ticket.UpdatedAtUtc,
                DueAtUtc = x.Ticket.DueAtUtc,
                ResolvedAtUtc = x.Ticket.ResolvedAtUtc,
                SlaStatus = SlaEvaluator.Evaluate(
                    x.Ticket.SlaWindowStartUtc,
                    x.Ticket.DueAtUtc,
                    x.Ticket.ResolvedAtUtc,
                    now,
                    atRiskThreshold)
            })
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<TicketListItemDto>> GetForCustomerAsync(int customerId, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var atRiskThreshold = slaOptions.Value.AtRiskThresholdPercent / 100.0;

        return await TicketsWithLabels()
            .Where(x => x.Ticket.CustomerId == customerId)
            .OrderByDescending(x => x.Ticket.CreatedAtUtc)
            .ThenByDescending(x => x.Ticket.Id)
            .Select(x => new TicketListItemDto
            {
                Id = x.Ticket.Id,
                Reference = x.Ticket.Reference,
                Title = x.Ticket.Title,
                Status = x.Ticket.Status,
                Priority = x.Ticket.Priority,
                Customer = new CustomerSummaryDto(x.Customer.Id, x.Customer.Name, x.Customer.Tier),
                Category = new CategorySummaryDto(x.Category.Id, x.Category.Name),
                AssignedAgent = x.Agent == null ? null : new AgentSummaryDto(x.Agent.Id, x.Agent.FullName),
                CreatedAtUtc = x.Ticket.CreatedAtUtc,
                UpdatedAtUtc = x.Ticket.UpdatedAtUtc,
                DueAtUtc = x.Ticket.DueAtUtc,
                ResolvedAtUtc = x.Ticket.ResolvedAtUtc,
                SlaStatus = SlaEvaluator.Evaluate(
                    x.Ticket.SlaWindowStartUtc,
                    x.Ticket.DueAtUtc,
                    x.Ticket.ResolvedAtUtc,
                    now,
                    atRiskThreshold)
            })
            .ToListAsync(ct);
    }

    /// <summary>
    /// Returns escalation history for a ticket, newest first. Agent names are joined in the
    /// same query (no N+1) using a left join in the projection.
    /// </summary>
    public async Task<IReadOnlyList<TicketEscalationDto>> GetEscalationsAsync(int ticketId, CancellationToken ct)
    {
        // Join agents in a single query rather than issuing one sub-query per escalation row.
        var results = await (
            from e in db.Set<TicketEscalation>().AsNoTracking()
            where e.TicketId == ticketId
            join fromAgent in db.Set<Agent>() on e.FromAgentId equals fromAgent.Id into fromAgents
            from fa in fromAgents.DefaultIfEmpty()
            join toAgent in db.Set<Agent>() on e.ToAgentId equals toAgent.Id into toAgents
            from ta in toAgents.DefaultIfEmpty()
            orderby e.EscalatedAtUtc descending
            select new TicketEscalationDto(
                e.Id,
                e.TicketId,
                e.FromPriority,
                e.ToPriority,
                fa == null ? null : new AgentSummaryDto(fa.Id, fa.FullName),
                ta == null ? null : new AgentSummaryDto(ta.Id, ta.FullName),
                e.FromDueAtUtc,
                e.ToDueAtUtc,
                e.Reason,
                e.EscalatedBy,
                e.EscalatedAtUtc)
        ).ToListAsync(ct);

        return results;
    }

    private IQueryable<TicketWithLabels> TicketsWithLabels() =>
        from ticket in db.Set<Ticket>().AsNoTracking()
        join customer in db.Set<Customer>() on ticket.CustomerId equals customer.Id
        join category in db.Set<Category>() on ticket.CategoryId equals category.Id
        from agent in db.Set<Agent>().Where(a => a.Id == ticket.AssignedAgentId).DefaultIfEmpty()
        select new TicketWithLabels { Ticket = ticket, Customer = customer, Category = category, Agent = agent };

    private static IQueryable<TicketWithLabels> ApplyFilters(
        IQueryable<TicketWithLabels> source,
        TicketQuery query,
        DateTime nowUtc,
        double atRiskThreshold)
    {
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            source = source.Where(x =>
                x.Ticket.Title.Contains(search) ||
                x.Ticket.Reference.Contains(search) ||
                x.Customer.Name.Contains(search));
        }

        if (query.Status.HasValue)
        {
            source = source.Where(x => x.Ticket.Status == query.Status.Value);
        }

        if (query.Priority.HasValue)
        {
            source = source.Where(x => x.Ticket.Priority == query.Priority.Value);
        }

        if (query.CategoryId.HasValue)
        {
            source = source.Where(x => x.Ticket.CategoryId == query.CategoryId.Value);
        }

        if (query.CustomerId.HasValue)
        {
            source = source.Where(x => x.Ticket.CustomerId == query.CustomerId.Value);
        }

        if (query.AssignedAgentId.HasValue)
        {
            source = source.Where(x => x.Ticket.AssignedAgentId == query.AssignedAgentId.Value);
        }

        if (query.UnassignedOnly)
        {
            source = source.Where(x => x.Ticket.AssignedAgentId == null);
        }

        if (query.SlaStatus.HasValue)
        {
            var thresholdPercent = (int)Math.Round(atRiskThreshold * 100);

            source = query.SlaStatus.Value switch
            {
                SlaStatus.NotApplicable =>
                    source.Where(x => x.Ticket.DueAtUtc == null),

                SlaStatus.Met =>
                    source.Where(x =>
                        x.Ticket.ResolvedAtUtc != null &&
                        x.Ticket.DueAtUtc != null &&
                        x.Ticket.ResolvedAtUtc <= x.Ticket.DueAtUtc),

                SlaStatus.Breached =>
                    source.Where(x =>
                        x.Ticket.DueAtUtc != null &&
                        ((x.Ticket.ResolvedAtUtc != null && x.Ticket.ResolvedAtUtc > x.Ticket.DueAtUtc) ||
                         (x.Ticket.ResolvedAtUtc == null && nowUtc > x.Ticket.DueAtUtc))),

                // AtRisk: open, not past due, and timeRemaining <= threshold% of the window.
                // Using minutes to avoid float precision issues in SQL.
                // Minutes remaining * 100 <= window minutes * thresholdPercent
                SlaStatus.AtRisk =>
                    source.Where(x =>
                        x.Ticket.ResolvedAtUtc == null &&
                        x.Ticket.DueAtUtc != null &&
                        nowUtc <= x.Ticket.DueAtUtc &&
                        EF.Functions.DateDiffSecond(nowUtc, x.Ticket.DueAtUtc) * 100 <=
                        EF.Functions.DateDiffSecond(x.Ticket.SlaWindowStartUtc, x.Ticket.DueAtUtc) * thresholdPercent),

                // WithinSla: open, not past due, and timeRemaining > threshold% of the window.
                SlaStatus.WithinSla =>
                    source.Where(x =>
                        x.Ticket.ResolvedAtUtc == null &&
                        x.Ticket.DueAtUtc != null &&
                        nowUtc <= x.Ticket.DueAtUtc &&
                        EF.Functions.DateDiffSecond(nowUtc, x.Ticket.DueAtUtc) * 100 >
                        EF.Functions.DateDiffSecond(x.Ticket.SlaWindowStartUtc, x.Ticket.DueAtUtc) * thresholdPercent),

                _ => source
            };
        }

        return source;
    }

    private static IQueryable<TicketWithLabels> ApplySort(IQueryable<TicketWithLabels> source, TicketQuery query)
    {
        var descending = !string.Equals(query.SortDirection, "asc", StringComparison.OrdinalIgnoreCase);

        var sorted = query.SortBy?.ToLowerInvariant() switch
        {
            "updatedatutc" => descending
                ? source.OrderByDescending(x => x.Ticket.UpdatedAtUtc)
                : source.OrderBy(x => x.Ticket.UpdatedAtUtc),
            "dueatutc" => descending
                ? source.OrderByDescending(x => x.Ticket.DueAtUtc)
                : source.OrderBy(x => x.Ticket.DueAtUtc),
            "priority" => descending
                ? source.OrderByDescending(x => x.Ticket.Priority)
                : source.OrderBy(x => x.Ticket.Priority),
            "status" => descending
                ? source.OrderByDescending(x => x.Ticket.Status)
                : source.OrderBy(x => x.Ticket.Status),
            _ => descending
                ? source.OrderByDescending(x => x.Ticket.CreatedAtUtc)
                : source.OrderBy(x => x.Ticket.CreatedAtUtc)
        };

        return sorted.ThenByDescending(x => x.Ticket.Id);
    }

    private sealed class TicketWithLabels
    {
        public required Ticket Ticket { get; init; }
        public required Customer Customer { get; init; }
        public required Category Category { get; init; }
        public Agent? Agent { get; init; }
    }
}