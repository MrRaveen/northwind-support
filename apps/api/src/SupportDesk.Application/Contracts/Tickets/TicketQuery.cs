using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Application.Contracts.Tickets;

public sealed record TicketQuery
{
    public const int MaxPageSize = 100;

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 20;

    public string? Search { get; init; }

    public TicketStatus? Status { get; init; }

    public TicketPriority? Priority { get; init; }

    public int? CategoryId { get; init; }

    public int? CustomerId { get; init; }

    public int? AssignedAgentId { get; init; }

    public bool UnassignedOnly { get; init; }

    public SlaStatus? SlaStatus { get; init; }

    public string SortBy { get; init; } = "createdAtUtc";
    public string SortDirection { get; init; } = "desc";

    public TicketQuery Normalized() => this with
    {
        Page = Page < 1 ? 1 : Page,
        PageSize = PageSize < 1 ? 20 : Math.Min(PageSize, MaxPageSize)
    };
}