using SupportDesk.Domain.Common;
using SupportDesk.Domain.Exceptions;

namespace SupportDesk.Domain.Aggregates.Tickets;

/// <summary>
/// A support request raised by a customer. Every change to a ticket goes through one of its
/// methods, so its rules cannot be bypassed.
/// </summary>
public sealed class Ticket : AggregateRoot
{
    private readonly List<TicketEscalation> _escalations = [];
    private Ticket()
    {
        // For EF Core.
    }

    public string Reference { get; private set; } = string.Empty;

    public string Title { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public int CustomerId { get; private set; }

    public int CategoryId { get; private set; }

    public int? AssignedAgentId { get; private set; }

    public TicketPriority Priority { get; private set; }

    public TicketStatus Status { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    public DateTime? DueAtUtc { get; private set; }

    public DateTime? ResolvedAtUtc { get; private set; }

    public bool IsOpen => Status is not (TicketStatus.Resolved or TicketStatus.Closed);

    public IReadOnlyCollection<TicketEscalation> Escalations => _escalations;

    /// <summary>
    /// When the current SLA window started. Set to CreatedAtUtc on raise and reset to the
    /// escalation time each time the ticket is escalated (because escalation recomputes DueAtUtc
    /// from 'now', so the window size would be wrong if we still measured from CreatedAtUtc).
    /// </summary>
    public DateTime SlaWindowStartUtc { get; private set; }

    /// <summary>
    /// Raises a new ticket. Triage rules (assignee, due date, priority override) are applied here.
    /// </summary>
    public static Ticket Raise(
        string reference,
        string title,
        string description,
        int customerId,
        int categoryId,
        TicketPriority requestedPriority,
        bool forcesCriticalPriority,
        DateTime? dueAtUtc,
        int? assignedAgentId,
        DateTime nowUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        return new Ticket
        {
            Reference = reference,
            Title = title.Trim(),
            Description = description.Trim(),
            CustomerId = customerId,
            CategoryId = categoryId,
            Priority = forcesCriticalPriority ? TicketPriority.Critical : requestedPriority,
            Status = TicketStatus.New,
            DueAtUtc = dueAtUtc,
            AssignedAgentId = assignedAgentId,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc,
            SlaWindowStartUtc = nowUtc
        };
    }

    public void ChangeStatus(TicketStatus status, DateTime nowUtc)
    {
        if (Status == TicketStatus.Closed && status != TicketStatus.Closed)
        {
            throw new BusinessRuleViolationException(
                $"Ticket {Reference} is closed and cannot be moved to {status}.");
        }

        ResolvedAtUtc = status switch
        {
            TicketStatus.Resolved => nowUtc,
            TicketStatus.Closed => ResolvedAtUtc ?? nowUtc,
            _ => null
        };

        Status = status;
        UpdatedAtUtc = nowUtc;
    }

    public void AssignTo(int? agentId, DateTime nowUtc)
    {
        if (Status == TicketStatus.Closed)
        {
            throw new BusinessRuleViolationException(
                $"Ticket {Reference} is closed and cannot be reassigned.");
        }

        AssignedAgentId = agentId;
        UpdatedAtUtc = nowUtc;
    }

    /// <summary>
    /// Returns the priority one step above the current, or throws <see cref="BusinessRuleViolationException"/>
    /// when the ticket is in a terminal state (Critical, Resolved, Closed). This is the single
    /// source of truth for escalation guard rules and for callers that need the next priority
    /// before actually escalating (e.g. to recompute SLA windows).
    /// </summary>
    public TicketPriority NextPriority()
    {
        if (Status is TicketStatus.Resolved or TicketStatus.Closed)
        {
            throw new BusinessRuleViolationException(
                $"Ticket {Reference} is {Status} and cannot be escalated.");
        }

        if (Priority == TicketPriority.Critical)
        {
            throw new BusinessRuleViolationException(
                $"Ticket {Reference} is already Critical and cannot be escalated further.");
        }

        return Priority switch
        {
            TicketPriority.Low => TicketPriority.Medium,
            TicketPriority.Medium => TicketPriority.High,
            TicketPriority.High => TicketPriority.Critical,
            _ => TicketPriority.Critical
        };
    }

    /// <summary>
    /// Escalates the ticket (BR-7). Calls <see cref="NextPriority"/> — which enforces the guard
    /// rules — then steps up priority, resets the SLA window, accepts recomputed due date and
    /// agent, and records the immutable history row.
    /// </summary>
    public void Escalate(
        string reason,
        string escalatedBy,
        DateTime newDueAtUtc,
        int? newAgentId,
        DateTime nowUtc)
    {
        // Guard rules are enforced in NextPriority; this also gives us the new priority.
        var newPriority = NextPriority();

        var oldPriority = Priority;
        var oldAgentId = AssignedAgentId;
        var oldDueAtUtc = DueAtUtc;

        Priority = newPriority;
        DueAtUtc = newDueAtUtc;
        AssignedAgentId = newAgentId;
        SlaWindowStartUtc = nowUtc; // restart the window so AtRisk % is correct after escalation
        UpdatedAtUtc = nowUtc;

        _escalations.Add(new TicketEscalation(
            Id,
            oldPriority,
            Priority,
            oldAgentId,
            AssignedAgentId,
            oldDueAtUtc,
            DueAtUtc,
            reason,
            escalatedBy,
            nowUtc));
    }
}