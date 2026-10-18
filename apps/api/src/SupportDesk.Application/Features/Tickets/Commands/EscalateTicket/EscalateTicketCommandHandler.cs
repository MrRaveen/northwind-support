using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;
using SupportDesk.Application.Features.Tickets.Queries.GetTicket;
using SupportDesk.Application.Features.Tickets.Services;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.Application.Features.Tickets.Commands.EscalateTicket;

public sealed class EscalateTicketCommandHandler(
    ITicketRepository tickets,
    ICustomerRepository customers,
    ICategoryRepository categories,
    TicketTriageService triageService,
    IUnitOfWork unitOfWork,
    IClock clock,
    GetTicketQueryHandler getTicket)
{
    public async Task<TicketDetailDto> HandleAsync(int id, EscalateTicketRequest request, CancellationToken ct)
    {
        var ticket = await tickets.GetByIdAsync(id, ct)
            ?? throw new NotFoundException("Ticket", id);

        // Guard checks are centrally enforced by Ticket.NextPriority(), which throws
        // BusinessRuleViolationException (→ HTTP 409) for Critical or Resolved/Closed.
        // We also throw ConflictException explicitly here so the handler returns 409 directly
        // with the exact message the spec requires, while Ticket.Escalate() double-checks
        // via NextPriority() as its own guard (single source of truth in the aggregate).
        if (ticket.Status is TicketStatus.Resolved or TicketStatus.Closed)
        {
            throw new ConflictException($"Ticket {ticket.Reference} is {ticket.Status} and cannot be escalated.");
        }

        if (ticket.Priority == TicketPriority.Critical)
        {
            throw new ConflictException($"Ticket {ticket.Reference} is already Critical and cannot be escalated further.");
        }

        var customer = await customers.GetByIdAsync(ticket.CustomerId, ct)
            ?? throw new NotFoundException("Customer", ticket.CustomerId);
        var category = await categories.GetByIdAsync(ticket.CategoryId, ct)
            ?? throw new NotFoundException("Category", ticket.CategoryId);

        // Use Ticket.NextPriority() — the single source of truth for the step-up rule — to get
        // the next priority for triage (SLA window recomputation). Ticket.Escalate() will call
        // NextPriority() again internally to set Priority; same result, no duplication of logic.
        var nextPriority = ticket.NextPriority();

        var triage = await triageService.TriageAsync(
            nextPriority,
            category,
            customer,
            clock.UtcNow,
            ticket.AssignedAgentId,
            ct);

        ticket.Escalate(
            request.Reason,
            request.EscalatedBy,
            triage.DueAtUtc,
            triage.AssignedAgentId,
            clock.UtcNow);

        await unitOfWork.SaveChangesAsync(ct);

        return await getTicket.HandleAsync(ticket.Id, ct);
    }
}