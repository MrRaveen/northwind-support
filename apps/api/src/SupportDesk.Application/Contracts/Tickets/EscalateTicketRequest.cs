namespace SupportDesk.Application.Contracts.Tickets;

public sealed record EscalateTicketRequest(string Reason, string EscalatedBy);