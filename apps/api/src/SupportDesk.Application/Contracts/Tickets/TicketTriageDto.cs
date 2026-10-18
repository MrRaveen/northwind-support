namespace SupportDesk.Application.Contracts.Tickets;

public sealed record TicketTriageDto(
    string AppliedPriority,
    string PriorityReason,
    double SlaWindowHours,
    string SlaReason,
    string AssignmentReason
);