using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Application.Features.Tickets.Commands;

public class RaiseTicketCommandHandlerTests
{
    private static CreateTicketRequest Request(TicketPriority? priority = null) =>
        new("Cannot log in to the portal", "Happens for every user since this morning.", 1, 3, priority);

    [Fact]
    public async Task HandleAsync_StampsReferenceStatusAndTimestamps()
    {
        var context = new TicketCommandTestContext();

        await context.RaiseTicket.HandleAsync(Request(TicketPriority.High), CancellationToken.None);

        var created = context.AddedTicket;
        Assert.NotNull(created);
        Assert.Equal("TCK-0041", created.Reference);
        Assert.Equal(TicketStatus.New, created.Status);
        Assert.Equal(TicketPriority.High, created.Priority);
        Assert.Equal(FixedClock.DefaultNow, created.CreatedAtUtc);
        Assert.Equal(FixedClock.DefaultNow, created.UpdatedAtUtc);
        context.VerifySaved(Times.Once());
    }

    [Fact]
    public async Task HandleAsync_WithoutRequestedPriority_FallsBackToMedium()
    {
        var context = new TicketCommandTestContext();

        await context.RaiseTicket.HandleAsync(Request(), CancellationToken.None);

        Assert.NotNull(context.AddedTicket);
        Assert.Equal(TicketPriority.Medium, context.AddedTicket.Priority);
    }

    [Fact]
    public async Task HandleAsync_WhenCustomerDoesNotExist_Throws()
    {
        var context = new TicketCommandTestContext();
        context.Customers
            .Setup(c => c.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Customer?)null);

        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => context.RaiseTicket.HandleAsync(Request(), CancellationToken.None));

        Assert.Contains("Customer", exception.Message);
        context.VerifySaved(Times.Never());
    }

    [Fact]
    public async Task HandleAsync_WhenCategoryDoesNotExist_Throws()
    {
        var context = new TicketCommandTestContext();
        context.Categories
            .Setup(c => c.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Category?)null);

        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => context.RaiseTicket.HandleAsync(Request(), CancellationToken.None));

        Assert.Contains("Category", exception.Message);
        context.VerifySaved(Times.Never());
    }

    // ---- Triage on create (BR-1 to BR-5) ---------------------------------------------------

    [Fact]
    public async Task HandleAsync_AppliesTriage_PriorityDueDateOwnerAndTheReasonsBehindThem()
    {
        var context = new TicketCommandTestContext();
        context.AgentQueries
            .Setup(a => a.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                AgentDtoFactory.Create(1, open: 5, max: 10),
                AgentDtoFactory.Create(4, open: 2, max: 6),
                AgentDtoFactory.Create(5, open: 6, max: 6)
            });

        var result = await context.RaiseTicket.HandleAsync(Request(), CancellationToken.None);

        var created = context.AddedTicket;
        Assert.NotNull(created);
        Assert.Equal(TicketPriority.Medium, created.Priority);
        Assert.Equal(FixedClock.DefaultNow.AddHours(24), created.DueAtUtc); // Medium, Standard customer
        Assert.Equal(4, created.AssignedAgentId); // fewest open tickets among eligible agents

        Assert.NotNull(result.Triage);
        Assert.Equal("Medium", result.Triage.AppliedPriority);
        Assert.Equal(24d, result.Triage.SlaWindowHours);
        Assert.Contains("Fewest open tickets", result.Triage.AssignmentReason);
    }

    [Fact]
    public async Task HandleAsync_ForAPremiumCustomer_HalvesTheWindow()
    {
        var context = new TicketCommandTestContext();
        context.Customers
            .Setup(c => c.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Customer("Contoso", "support@contoso.example", null, CustomerTier.Premium, FixedClock.DefaultNow));

        await context.RaiseTicket.HandleAsync(Request(TicketPriority.High), CancellationToken.None);

        Assert.NotNull(context.AddedTicket);
        Assert.Equal(FixedClock.DefaultNow.AddHours(4), context.AddedTicket.DueAtUtc); // High is 8h, halved
    }

    [Fact]
    public async Task HandleAsync_ForACategoryThatForcesCritical_IgnoresTheRequestedPriorityAndUsesASpecialist()
    {
        const int securityId = 5;
        var context = new TicketCommandTestContext();
        context.Categories
            .Setup(c => c.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Category("Security", requiresSpecialist: true, forcesCriticalPriority: true).WithId(securityId));
        context.AgentQueries
            .Setup(a => a.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                AgentDtoFactory.Create(4, open: 0),
                AgentDtoFactory.Create(3, open: 1, specializations: [securityId])
            });

        var result = await context.RaiseTicket.HandleAsync(Request(TicketPriority.Low), CancellationToken.None);

        Assert.NotNull(context.AddedTicket);
        Assert.Equal(TicketPriority.Critical, context.AddedTicket.Priority);
        Assert.Equal(FixedClock.DefaultNow.AddHours(4), context.AddedTicket.DueAtUtc);
        Assert.Equal(3, context.AddedTicket.AssignedAgentId);
        Assert.NotNull(result.Triage);
        Assert.Contains("Security", result.Triage.PriorityReason);
    }

    [Fact]
    public async Task HandleAsync_WhenNoAgentIsEligible_StillCreatesTheTicketUnassignedWithAReason()
    {
        var context = new TicketCommandTestContext(); // no agents at all

        var result = await context.RaiseTicket.HandleAsync(Request(), CancellationToken.None);

        Assert.NotNull(context.AddedTicket);
        Assert.Null(context.AddedTicket.AssignedAgentId);
        Assert.Equal(FixedClock.DefaultNow.AddHours(24), context.AddedTicket.DueAtUtc);
        Assert.NotNull(result.Triage);
        Assert.Contains("No available agent", result.Triage.AssignmentReason);
        context.VerifySaved(Times.Once());
    }
}
