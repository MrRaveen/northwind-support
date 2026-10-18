using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Tickets;

namespace SupportDesk.Application.Features.Tickets.Queries.GetEscalations;

public sealed class GetEscalationsQueryHandler(ITicketQueries queries)
{
    public Task<IReadOnlyList<TicketEscalationDto>> HandleAsync(int ticketId, CancellationToken ct) =>
        queries.GetEscalationsAsync(ticketId, ct);
}