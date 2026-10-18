using SupportDesk.Domain.Aggregates.Tickets;
using System.Text.Json.Serialization;
namespace SupportDesk.Application.Contracts.Tickets;

/// <summary>
/// A single ticket, with the description and contact details the detail screen shows.
/// </summary>
public sealed class TicketDetailDto
{
    public int Id { get; set; }

    public string Reference { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public TicketStatus Status { get; set; }

    public TicketPriority Priority { get; set; }

    public CustomerContactDto Customer { get; set; } = null!;

    public CategoryDto Category { get; set; } = null!;

    public AgentSummaryDto? AssignedAgent { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public DateTime? DueAtUtc { get; set; }

    public DateTime? ResolvedAtUtc { get; set; }

    /// <summary>Derived from the dates above; never stored.</summary>
    public SlaStatus SlaStatus { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public TicketTriageDto? Triage { get; set; }
}
