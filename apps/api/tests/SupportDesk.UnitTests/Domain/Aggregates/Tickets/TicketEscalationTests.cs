using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Exceptions;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Domain.Aggregates.Tickets;

/// <summary>The escalation rules that belong to the ticket itself (BR-7).</summary>
public class TicketEscalationTests
{
    private static readonly DateTime Later = FixedClock.DefaultNow.AddHours(3);
    private static readonly DateTime NewDue = Later.AddHours(8);

    [Theory]
    [InlineData(TicketPriority.Low, TicketPriority.Medium)]
    [InlineData(TicketPriority.Medium, TicketPriority.High)]
    [InlineData(TicketPriority.High, TicketPriority.Critical)]
    public void NextPriority_IsExactlyOneLevelUp(TicketPriority before, TicketPriority after)
    {
        var ticket = new TicketBuilder().WithPriority(before).Build();

        Assert.Equal(after, ticket.NextPriority());
        // Asking does not change anything.
        Assert.Equal(before, ticket.Priority);
        Assert.Empty(ticket.Escalations);
    }

    [Fact]
    public void NextPriority_ForACriticalTicket_ThrowsAndSaysWhy()
    {
        var ticket = new TicketBuilder().WithPriority(TicketPriority.Critical).Build();

        var exception = Assert.Throws<BusinessRuleViolationException>(() => ticket.NextPriority());

        Assert.Contains("Critical", exception.Message);
    }

    [Theory]
    [InlineData(TicketStatus.Resolved)]
    [InlineData(TicketStatus.Closed)]
    public void NextPriority_ForAFinishedTicket_ThrowsAndNamesTheStatus(TicketStatus status)
    {
        var ticket = new TicketBuilder().WithStatus(status).Build();

        var exception = Assert.Throws<BusinessRuleViolationException>(() => ticket.NextPriority());

        Assert.Contains(status.ToString(), exception.Message);
    }

    [Fact]
    public void Escalate_StepsUpPriority_AppliesTheNewDueDateAndOwner_AndRestartsTheSlaWindow()
    {
        var ticket = new TicketBuilder().WithPriority(TicketPriority.Medium).AssignedTo(2).Build();

        ticket.Escalate("Customer reports production impact", "alex.lead", NewDue, newAgentId: 4, Later);

        Assert.Equal(TicketPriority.High, ticket.Priority);
        Assert.Equal(NewDue, ticket.DueAtUtc);
        Assert.Equal(4, ticket.AssignedAgentId);
        Assert.Equal(Later, ticket.SlaWindowStartUtc);
        Assert.Equal(Later, ticket.UpdatedAtUtc);
        // Escalating is not creating: the ticket still remembers when it was raised.
        Assert.Equal(FixedClock.DefaultNow, ticket.CreatedAtUtc);
    }

    [Fact]
    public void Escalate_RecordsOneHistoryRowWithTheBeforeAndAfterValues()
    {
        var oldDue = FixedClock.DefaultNow.AddHours(24);
        var ticket = Ticket.Raise(
            "TCK-0042", "Cannot log in", "Happens for every user.", 1, 1,
            TicketPriority.Medium, forcesCriticalPriority: false,
            dueAtUtc: oldDue, assignedAgentId: 2, FixedClock.DefaultNow);

        ticket.Escalate("Customer reports production impact", "alex.lead", NewDue, newAgentId: 4, Later);

        var row = Assert.Single(ticket.Escalations);
        Assert.Equal(TicketPriority.Medium, row.FromPriority);
        Assert.Equal(TicketPriority.High, row.ToPriority);
        Assert.Equal(2, row.FromAgentId);
        Assert.Equal(4, row.ToAgentId);
        Assert.Equal(oldDue, row.FromDueAtUtc);
        Assert.Equal(NewDue, row.ToDueAtUtc);
        Assert.Equal("Customer reports production impact", row.Reason);
        Assert.Equal("alex.lead", row.EscalatedBy);
        Assert.Equal(Later, row.EscalatedAtUtc);
    }

    [Fact]
    public void Escalate_Twice_KeepsBothHistoryRowsInOrder()
    {
        var ticket = new TicketBuilder().WithPriority(TicketPriority.Low).Build();

        ticket.Escalate("First reason", "alex.lead", NewDue, null, Later);
        ticket.Escalate("Second reason", "ben.osei", NewDue.AddHours(1), null, Later.AddHours(1));

        var rows = ticket.Escalations.ToList();
        Assert.Equal(2, rows.Count);
        Assert.Equal((TicketPriority.Low, TicketPriority.Medium), (rows[0].FromPriority, rows[0].ToPriority));
        Assert.Equal((TicketPriority.Medium, TicketPriority.High), (rows[1].FromPriority, rows[1].ToPriority));
        Assert.Equal(TicketPriority.High, ticket.Priority);
    }

    [Fact]
    public void Escalate_ForACriticalTicket_ThrowsAndRecordsNothing()
    {
        var ticket = new TicketBuilder().WithPriority(TicketPriority.Critical).Build();

        Assert.Throws<BusinessRuleViolationException>(
            () => ticket.Escalate("Customer reports production impact", "alex.lead", NewDue, null, Later));

        Assert.Empty(ticket.Escalations);
        Assert.Equal(TicketPriority.Critical, ticket.Priority);
    }

    [Theory]
    [InlineData(TicketStatus.Resolved)]
    [InlineData(TicketStatus.Closed)]
    public void Escalate_ForAFinishedTicket_ThrowsAndChangesNothing(TicketStatus status)
    {
        var ticket = new TicketBuilder().WithPriority(TicketPriority.Medium).WithStatus(status).Build();

        Assert.Throws<BusinessRuleViolationException>(
            () => ticket.Escalate("Customer reports production impact", "alex.lead", NewDue, null, Later));

        Assert.Empty(ticket.Escalations);
        Assert.Equal(TicketPriority.Medium, ticket.Priority);
        Assert.Null(ticket.DueAtUtc);
    }
}
