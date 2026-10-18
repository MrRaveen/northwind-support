using SupportDesk.Application.Contracts.Agents;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Exceptions;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Application.Features.Tickets.Commands;

public class EscalateTicketCommandHandlerTests
{
    private const string Reason = "Customer reports production impact";

    private static EscalateTicketRequest Request(string reason = Reason, string by = "alex.lead") => new(reason, by);

    private static void WithAgents(TicketCommandTestContext context, params AgentDto[] agents) =>
        context.AgentQueries
            .Setup(a => a.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(agents);

    private static void WithPremiumCustomer(TicketCommandTestContext context) =>
        context.Customers
            .Setup(c => c.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Customer("Contoso", "support@contoso.example", null, CustomerTier.Premium, context.Clock.UtcNow));

    // ---- Not found and rejections ----------------------------------------------------------

    [Fact]
    public async Task HandleAsync_WhenTheTicketDoesNotExist_Throws()
    {
        var context = new TicketCommandTestContext();

        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => context.EscalateTicket.HandleAsync(999, Request(), CancellationToken.None));

        Assert.Contains("Ticket", exception.Message);
        context.VerifySaved(Times.Never());
    }

    [Fact]
    public async Task HandleAsync_ForACriticalTicket_IsRejectedAndSaysSo()
    {
        var context = new TicketCommandTestContext();
        var ticket = context.ExistingTicket(new TicketBuilder().WithPriority(TicketPriority.Critical).Build());

        var exception = await Assert.ThrowsAsync<BusinessRuleViolationException>(
            () => context.EscalateTicket.HandleAsync(ticket.Id, Request(), CancellationToken.None));

        Assert.Contains("Critical", exception.Message);
        Assert.Empty(ticket.Escalations);
        context.VerifySaved(Times.Never());
    }

    [Theory]
    [InlineData(TicketStatus.Resolved)]
    [InlineData(TicketStatus.Closed)]
    public async Task HandleAsync_ForAFinishedTicket_IsRejectedAndNamesTheStatus(TicketStatus status)
    {
        var context = new TicketCommandTestContext();
        var ticket = context.ExistingTicket(new TicketBuilder().WithStatus(status).Build());

        var exception = await Assert.ThrowsAsync<BusinessRuleViolationException>(
            () => context.EscalateTicket.HandleAsync(ticket.Id, Request(), CancellationToken.None));

        Assert.Contains(status.ToString(), exception.Message);
        Assert.Empty(ticket.Escalations);
        context.VerifySaved(Times.Never());
    }

    // ---- Priority and due date -------------------------------------------------------------

    [Theory]
    [InlineData(TicketPriority.Low, TicketPriority.Medium)]
    [InlineData(TicketPriority.Medium, TicketPriority.High)]
    [InlineData(TicketPriority.High, TicketPriority.Critical)]
    public async Task HandleAsync_RaisesThePriorityByExactlyOneLevel(TicketPriority before, TicketPriority after)
    {
        var context = new TicketCommandTestContext();
        var ticket = context.ExistingTicket(new TicketBuilder().WithPriority(before).Build());

        await context.EscalateTicket.HandleAsync(ticket.Id, Request(), CancellationToken.None);

        Assert.Equal(after, ticket.Priority);
        context.VerifySaved(Times.Once());
    }

    [Fact]
    public async Task HandleAsync_RecomputesTheDueDateFromTheMomentOfEscalation()
    {
        var context = new TicketCommandTestContext();
        var ticket = context.ExistingTicket(new TicketBuilder().WithPriority(TicketPriority.Medium).Build());
        context.Clock.Advance(TimeSpan.FromHours(5));

        await context.EscalateTicket.HandleAsync(ticket.Id, Request(), CancellationToken.None);

        // High is 8h for a Standard customer, counted from now, not from when the ticket was raised.
        Assert.Equal(context.Clock.UtcNow.AddHours(8), ticket.DueAtUtc);
        Assert.NotEqual(FixedClock.DefaultNow.AddHours(8), ticket.DueAtUtc);
        Assert.Equal(context.Clock.UtcNow, ticket.SlaWindowStartUtc);
    }

    [Fact]
    public async Task HandleAsync_ForAPremiumCustomer_UsesTheHalvedWindowOfTheNewPriority()
    {
        var context = new TicketCommandTestContext();
        WithPremiumCustomer(context);
        var ticket = context.ExistingTicket(new TicketBuilder().WithPriority(TicketPriority.Low).Build());

        await context.EscalateTicket.HandleAsync(ticket.Id, Request(), CancellationToken.None);

        // Low becomes Medium: 24h, halved for Premium.
        Assert.Equal(context.Clock.UtcNow.AddHours(12), ticket.DueAtUtc);
    }

    // ---- Owner (BR-7) ----------------------------------------------------------------------

    [Fact]
    public async Task HandleAsync_KeepsTheCurrentAgentWhoIsStillEligible()
    {
        var context = new TicketCommandTestContext();
        WithAgents(context,
            AgentDtoFactory.Create(3, open: 4),
            AgentDtoFactory.Create(4, open: 2)); // fewer tickets, but the current owner stays
        var ticket = context.ExistingTicket(new TicketBuilder().AssignedTo(3).Build());

        await context.EscalateTicket.HandleAsync(ticket.Id, Request(), CancellationToken.None);

        Assert.Equal(3, ticket.AssignedAgentId);
        var row = Assert.Single(ticket.Escalations);
        Assert.Equal(3, row.FromAgentId);
        Assert.Equal(3, row.ToAgentId);
    }

    [Fact]
    public async Task HandleAsync_KeepsAFullAgentWhoAlreadyHoldsTheTicket()
    {
        var context = new TicketCommandTestContext();
        WithAgents(context,
            AgentDtoFactory.Create(5, open: 6, max: 6),
            AgentDtoFactory.Create(4, open: 2, max: 6));
        var ticket = context.ExistingTicket(new TicketBuilder().AssignedTo(5).Build());

        await context.EscalateTicket.HandleAsync(ticket.Id, Request(), CancellationToken.None);

        Assert.Equal(5, ticket.AssignedAgentId);
    }

    [Fact]
    public async Task HandleAsync_ReassignsWhenTheCurrentAgentIsNoLongerEligible()
    {
        var context = new TicketCommandTestContext();
        WithAgents(context,
            AgentDtoFactory.Create(1, open: 3, active: false),
            AgentDtoFactory.Create(3, open: 4),
            AgentDtoFactory.Create(4, open: 2));
        var ticket = context.ExistingTicket(new TicketBuilder().AssignedTo(1).Build());

        await context.EscalateTicket.HandleAsync(ticket.Id, Request(), CancellationToken.None);

        Assert.Equal(4, ticket.AssignedAgentId);
        var row = Assert.Single(ticket.Escalations);
        Assert.Equal(1, row.FromAgentId);
        Assert.Equal(4, row.ToAgentId);
    }

    [Fact]
    public async Task HandleAsync_AssignsAnUnassignedTicketToTheAgentWithTheFewestOpenTickets()
    {
        var context = new TicketCommandTestContext();
        WithAgents(context,
            AgentDtoFactory.Create(3, open: 4),
            AgentDtoFactory.Create(4, open: 2));
        var ticket = context.ExistingTicket(new TicketBuilder().Build());

        await context.EscalateTicket.HandleAsync(ticket.Id, Request(), CancellationToken.None);

        Assert.Equal(4, ticket.AssignedAgentId);
        Assert.Null(Assert.Single(ticket.Escalations).FromAgentId);
    }

    [Fact]
    public async Task HandleAsync_WhenNobodyIsEligible_LeavesTheTicketUnassigned()
    {
        var context = new TicketCommandTestContext();
        WithAgents(context, AgentDtoFactory.Create(1, open: 0, active: false));
        var ticket = context.ExistingTicket(new TicketBuilder().AssignedTo(1).Build());

        await context.EscalateTicket.HandleAsync(ticket.Id, Request(), CancellationToken.None);

        Assert.Null(ticket.AssignedAgentId);
        Assert.Equal(TicketPriority.High, ticket.Priority); // still escalated
    }

    // ---- History ---------------------------------------------------------------------------

    [Fact]
    public async Task HandleAsync_WritesOneHistoryRowWithTheDetailsOfTheChange()
    {
        var context = new TicketCommandTestContext();
        WithAgents(context, AgentDtoFactory.Create(3, open: 4));
        var ticket = context.ExistingTicket(new TicketBuilder().WithPriority(TicketPriority.Medium).AssignedTo(3).Build());
        context.Clock.Advance(TimeSpan.FromHours(2));

        await context.EscalateTicket.HandleAsync(ticket.Id, Request(Reason, "ben.osei"), CancellationToken.None);

        var row = Assert.Single(ticket.Escalations);
        Assert.Equal(TicketPriority.Medium, row.FromPriority);
        Assert.Equal(TicketPriority.High, row.ToPriority);
        Assert.Equal(Reason, row.Reason);
        Assert.Equal("ben.osei", row.EscalatedBy);
        Assert.Equal(context.Clock.UtcNow, row.EscalatedAtUtc);
        Assert.Null(row.FromDueAtUtc); // the builder's ticket had no due date
        Assert.Equal(ticket.DueAtUtc, row.ToDueAtUtc);
        context.VerifySaved(Times.Once());
    }
}
