using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Exceptions;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Application.Features.Tickets.Commands;

public class AssignTicketCommandHandlerTests
{
    [Fact]
    public async Task HandleAsync_GivesTheTicketToAnActiveAgent()
    {
        var context = new TicketCommandTestContext();
        var ticket = context.ExistingTicket(new TicketBuilder().Build());
        context.ExistingAgent(id: 4, fullName: "Sara Lindqvist");

        context.Clock.Advance(TimeSpan.FromMinutes(15));

        await context.AssignTicket.HandleAsync(ticket.Id, new AssignTicketRequest(4), CancellationToken.None);

        Assert.Equal(4, ticket.AssignedAgentId);
        Assert.Equal(context.Clock.UtcNow, ticket.UpdatedAtUtc);
        context.VerifySaved(Times.Once());
    }

    [Fact]
    public async Task HandleAsync_WithoutAnAgent_TakesTheTicketBack()
    {
        var context = new TicketCommandTestContext();
        var ticket = context.ExistingTicket(new TicketBuilder().AssignedTo(2).Build());

        await context.AssignTicket.HandleAsync(ticket.Id, new AssignTicketRequest(null), CancellationToken.None);

        Assert.Null(ticket.AssignedAgentId);
        context.Agents.Verify(
            a => a.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenTheAgentIsInactive_Throws()
    {
        var context = new TicketCommandTestContext();
        var ticket = context.ExistingTicket(new TicketBuilder().Build());
        context.ExistingAgent(id: 6, fullName: "Yuki Tanaka", isActive: false);

        var exception = await Assert.ThrowsAsync<ConflictException>(
            () => context.AssignTicket.HandleAsync(ticket.Id, new AssignTicketRequest(6), CancellationToken.None));

        Assert.Contains("Yuki Tanaka", exception.Message);
        Assert.Null(ticket.AssignedAgentId);
        context.VerifySaved(Times.Never());
    }
    [Fact]
    public async Task HandleAsync_WhenTicketIsResolved_ThrowsAndDoesNotSave()
    {
        var context = new TicketCommandTestContext();
        var ticket = context.ExistingTicket(new TicketBuilder().WithStatus(TicketStatus.Resolved).AssignedTo(1).Build());
        context.ExistingAgent(id: 2, fullName: "Ben Osei");

        await Assert.ThrowsAsync<BusinessRuleViolationException>(() =>
            context.AssignTicket.HandleAsync(ticket.Id, new AssignTicketRequest(2), CancellationToken.None));

        context.VerifySaved(Times.Never());
    }
}
