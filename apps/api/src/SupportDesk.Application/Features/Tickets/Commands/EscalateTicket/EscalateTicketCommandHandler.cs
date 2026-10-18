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

        var nextPriority = ticket.NextPriority();

        var customer = await customers.GetByIdAsync(ticket.CustomerId, ct)
            ?? throw new NotFoundException("Customer", ticket.CustomerId);
        var category = await categories.GetByIdAsync(ticket.CategoryId, ct)
            ?? throw new NotFoundException("Category", ticket.CategoryId);

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