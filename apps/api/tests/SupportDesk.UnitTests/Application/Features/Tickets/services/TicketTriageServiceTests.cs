using Microsoft.Extensions.Options;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Configuration;
using SupportDesk.Application.Contracts.Agents;
using SupportDesk.Application.Features.Tickets.Services;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Application.Features.Tickets.Services;

/// <summary>
/// BR-1 to BR-5 and the "keep the current agent" half of BR-7. The service is tested directly,
/// with only the agent query mocked, because create and escalate both rely on exactly these rules.
/// </summary>
public class TicketTriageServiceTests
{
    private static readonly DateTime Now = FixedClock.DefaultNow;

    private const int BillingId = 2;
    private const int OtherCategoryId = 9;

    private static readonly Customer Standard =
        new("Fabrikam", "support@fabrikam.example", null, CustomerTier.Standard, Now);

    private static readonly Customer Premium =
        new("Contoso", "support@contoso.example", null, CustomerTier.Premium, Now);

    private static readonly Category General =
        new("General", requiresSpecialist: false, forcesCriticalPriority: false);

    private static readonly Category Billing =
        new Category("Billing", requiresSpecialist: true, forcesCriticalPriority: false).WithId(BillingId);

    private static readonly Category Security =
        new Category("Security", requiresSpecialist: true, forcesCriticalPriority: true).WithId(5);

    private static SlaOptions DefaultOptions() => new()
    {
        BaseWindowHours = new Dictionary<string, int>
        {
            ["Low"] = 72,
            ["Medium"] = 24,
            ["High"] = 8,
            ["Critical"] = 4
        },
        PremiumCustomerMultiplier = 0.5,
        MinimumWindowHours = 1,
        AtRiskThresholdPercent = 25
    };

    private static TicketTriageService CreateService(SlaOptions options, params AgentDto[] agents)
    {
        var queries = new Mock<IAgentQueries>();
        queries.Setup(q => q.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(agents);

        return new TicketTriageService(Options.Create(options), queries.Object);
    }

    private static TicketTriageService CreateService(params AgentDto[] agents) =>
        CreateService(DefaultOptions(), agents);

    private static Task<TicketTriageResult> Triage(
        TicketTriageService service,
        TicketPriority? requested,
        Category category,
        Customer customer,
        int? currentAgentId = null) =>
        service.TriageAsync(requested, category, customer, Now, currentAgentId, CancellationToken.None);

    // ---- BR-2: SLA window per priority -----------------------------------------------------

    [Theory]
    [InlineData(TicketPriority.Low, 72)]
    [InlineData(TicketPriority.Medium, 24)]
    [InlineData(TicketPriority.High, 8)]
    [InlineData(TicketPriority.Critical, 4)]
    public async Task Window_ForAStandardCustomer_IsTheBaseWindow(TicketPriority priority, int hours)
    {
        var result = await Triage(CreateService(), priority, General, Standard);

        Assert.Equal(priority, result.Priority);
        Assert.Equal(Now.AddHours(hours), result.DueAtUtc);
        Assert.Equal((double)hours, result.TriageDto.SlaWindowHours);
    }

    // ---- BR-3: Premium halves the window, never below the floor -----------------------------

    [Theory]
    [InlineData(TicketPriority.Low, 36)]
    [InlineData(TicketPriority.Medium, 12)]
    [InlineData(TicketPriority.High, 4)]
    [InlineData(TicketPriority.Critical, 2)]
    public async Task Window_ForAPremiumCustomer_IsHalved(TicketPriority priority, int hours)
    {
        var result = await Triage(CreateService(), priority, General, Premium);

        Assert.Equal(Now.AddHours(hours), result.DueAtUtc);
        Assert.Equal((double)hours, result.TriageDto.SlaWindowHours);
    }

    [Theory]
    [InlineData(1, 1.0)] // half an hour would be too short: raised to the 1 hour floor
    [InlineData(2, 1.0)] // halving lands exactly on the floor
    [InlineData(3, 1.5)] // above the floor: plain halving
    public async Task Window_ForAPremiumCustomer_NeverDropsBelowTheMinimum(int baseHours, double expectedHours)
    {
        // The defaults never reach the floor (the shortest premium window is 2h), so the
        // configuration is changed to make the floor matter.
        var options = DefaultOptions();
        options.BaseWindowHours["Critical"] = baseHours;

        var result = await Triage(CreateService(options), TicketPriority.Critical, General, Premium);

        Assert.Equal(Now.AddHours(expectedHours), result.DueAtUtc);
        Assert.Equal(expectedHours, result.TriageDto.SlaWindowHours);
    }

    [Fact]
    public async Task Window_ForAStandardCustomer_IgnoresThePremiumFloor()
    {
        var options = DefaultOptions();
        options.BaseWindowHours["Critical"] = 1;
        options.MinimumWindowHours = 3;

        var result = await Triage(CreateService(options), TicketPriority.Critical, General, Standard);

        Assert.Equal(Now.AddHours(1), result.DueAtUtc);
    }

    // ---- BR-6: everything comes from configuration -----------------------------------------

    [Fact]
    public async Task Window_UsesTheConfiguredValuesRatherThanBuiltInOnes()
    {
        var options = DefaultOptions();
        options.BaseWindowHours["Medium"] = 10;
        options.PremiumCustomerMultiplier = 0.25;

        var standard = await Triage(CreateService(options), TicketPriority.Medium, General, Standard);
        var premium = await Triage(CreateService(options), TicketPriority.Medium, General, Premium);

        Assert.Equal(Now.AddHours(10), standard.DueAtUtc);
        Assert.Equal(Now.AddHours(2.5), premium.DueAtUtc);
    }

    [Fact]
    public async Task Window_WhenAPriorityIsNotConfigured_FailsLoudlyInsteadOfGuessing()
    {
        var options = DefaultOptions();
        options.BaseWindowHours.Remove("High");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Triage(CreateService(options), TicketPriority.High, General, Standard));

        Assert.Contains("High", exception.Message);
    }

    // ---- BR-1: priority --------------------------------------------------------------------

    [Fact]
    public async Task Priority_UsesTheRequestedPriority()
    {
        var result = await Triage(CreateService(), TicketPriority.High, General, Standard);

        Assert.Equal(TicketPriority.High, result.Priority);
        Assert.Contains("Requested", result.TriageDto.PriorityReason);
    }

    [Fact]
    public async Task Priority_WithoutARequest_DefaultsToMedium()
    {
        var result = await Triage(CreateService(), requested: null, General, Standard);

        Assert.Equal(TicketPriority.Medium, result.Priority);
        Assert.Contains("Default", result.TriageDto.PriorityReason);
    }

    [Theory]
    [InlineData(TicketPriority.Low)]
    [InlineData(TicketPriority.High)]
    [InlineData(null)]
    public async Task Priority_ForACategoryThatForcesCritical_IsAlwaysCritical(TicketPriority? requested)
    {
        var result = await Triage(CreateService(), requested, Security, Standard);

        Assert.Equal(TicketPriority.Critical, result.Priority);
        Assert.Equal(Now.AddHours(4), result.DueAtUtc);
        Assert.Contains("Security", result.TriageDto.PriorityReason);
    }

    // ---- BR-5: agent selection -------------------------------------------------------------

    [Fact]
    public async Task Assignment_PicksTheAgentWithTheFewestOpenTickets()
    {
        var service = CreateService(
            AgentDtoFactory.Create(1, open: 5, max: 10),
            AgentDtoFactory.Create(2, open: 3),
            AgentDtoFactory.Create(3, open: 4));

        var result = await Triage(service, TicketPriority.Medium, General, Standard);

        Assert.Equal(2, result.AssignedAgentId);
        Assert.Contains("Fewest open tickets", result.TriageDto.AssignmentReason);
    }

    [Fact]
    public async Task Assignment_BreaksTiesByLowestIdWhateverTheOrderOfTheAgents()
    {
        var first = CreateService(
            AgentDtoFactory.Create(7, open: 2),
            AgentDtoFactory.Create(3, open: 2),
            AgentDtoFactory.Create(5, open: 2));
        var second = CreateService(
            AgentDtoFactory.Create(3, open: 2),
            AgentDtoFactory.Create(5, open: 2),
            AgentDtoFactory.Create(7, open: 2));

        var a = await Triage(first, TicketPriority.Medium, General, Standard);
        var b = await Triage(second, TicketPriority.Medium, General, Standard);

        Assert.Equal(3, a.AssignedAgentId);
        Assert.Equal(3, b.AssignedAgentId);
    }

    [Fact]
    public async Task Assignment_RequiresAgentsToBeStrictlyBelowTheirLimit()
    {
        var service = CreateService(
            AgentDtoFactory.Create(1, open: 6, max: 6), // at the limit: not eligible
            AgentDtoFactory.Create(2, open: 5, max: 6)); // one below the limit: eligible

        var result = await Triage(service, TicketPriority.Medium, General, Standard);

        Assert.Equal(2, result.AssignedAgentId);
    }

    [Fact]
    public async Task Assignment_SkipsInactiveAgentsEvenWhenTheyHaveTheFewestTickets()
    {
        var service = CreateService(
            AgentDtoFactory.Create(1, open: 0, active: false),
            AgentDtoFactory.Create(2, open: 7));

        var result = await Triage(service, TicketPriority.Medium, General, Standard);

        Assert.Equal(2, result.AssignedAgentId);
    }

    [Fact]
    public async Task Assignment_ForASpecialistCategory_OnlyConsidersSpecialists()
    {
        var service = CreateService(
            AgentDtoFactory.Create(1, open: 0), // not a specialist, however free
            AgentDtoFactory.Create(2, open: 6, specializations: [BillingId]),
            AgentDtoFactory.Create(3, open: 1, specializations: [OtherCategoryId]));

        var result = await Triage(service, TicketPriority.Medium, Billing, Standard);

        Assert.Equal(2, result.AssignedAgentId);
        Assert.Contains("Billing specialists", result.TriageDto.AssignmentReason);
    }

    [Fact]
    public async Task Assignment_ForASpecialistCategory_PicksTheFewestOpenAmongSpecialists()
    {
        var service = CreateService(
            AgentDtoFactory.Create(1, open: 4, specializations: [BillingId]),
            AgentDtoFactory.Create(2, open: 3, specializations: [BillingId]));

        var result = await Triage(service, TicketPriority.Medium, Billing, Standard);

        Assert.Equal(2, result.AssignedAgentId);
    }

    [Fact]
    public async Task Assignment_ForAnOrdinaryCategory_DoesNotNeedASpecialization()
    {
        var service = CreateService(AgentDtoFactory.Create(1, open: 2));

        var result = await Triage(service, TicketPriority.Medium, General, Standard);

        Assert.Equal(1, result.AssignedAgentId);
    }

    [Fact]
    public async Task Assignment_WhenNoSpecialistIsAvailable_LeavesTheTicketUnassignedWithAReason()
    {
        var service = CreateService(
            AgentDtoFactory.Create(1, open: 0),
            AgentDtoFactory.Create(2, open: 6, max: 6, specializations: [BillingId])); // full

        var result = await Triage(service, TicketPriority.Medium, Billing, Standard);

        Assert.Null(result.AssignedAgentId);
        Assert.Contains("Billing", result.TriageDto.AssignmentReason);
        // Not an error: the priority and due date are still decided.
        Assert.Equal(Now.AddHours(24), result.DueAtUtc);
    }

    [Fact]
    public async Task Assignment_WhenEveryoneIsFullOrInactive_LeavesTheTicketUnassigned()
    {
        var service = CreateService(
            AgentDtoFactory.Create(1, open: 8),
            AgentDtoFactory.Create(2, open: 0, active: false));

        var result = await Triage(service, TicketPriority.Medium, General, Standard);

        Assert.Null(result.AssignedAgentId);
        Assert.Contains("No available agent", result.TriageDto.AssignmentReason);
    }

    [Fact]
    public async Task Assignment_WithNoAgentsAtAll_LeavesTheTicketUnassigned()
    {
        var result = await Triage(CreateService(), TicketPriority.Medium, General, Standard);

        Assert.Null(result.AssignedAgentId);
    }

    // ---- BR-7: owner on escalation ---------------------------------------------------------

    [Fact]
    public async Task Escalation_KeepsTheCurrentAgentWhoIsStillEligible_EvenIfSomeoneHasFewerTickets()
    {
        var service = CreateService(
            AgentDtoFactory.Create(3, open: 4),
            AgentDtoFactory.Create(4, open: 2));

        var result = await Triage(service, TicketPriority.High, General, Standard, currentAgentId: 3);

        Assert.Equal(3, result.AssignedAgentId);
        Assert.Contains("still eligible", result.TriageDto.AssignmentReason);
    }

    [Fact]
    public async Task Escalation_KeepsAFullAgentWhoAlreadyHoldsTheTicket()
    {
        // The agent's open count includes this ticket, so escalating it adds no load.
        var service = CreateService(
            AgentDtoFactory.Create(5, open: 6, max: 6),
            AgentDtoFactory.Create(4, open: 2, max: 6));

        var result = await Triage(service, TicketPriority.High, General, Standard, currentAgentId: 5);

        Assert.Equal(5, result.AssignedAgentId);
    }

    [Fact]
    public async Task Escalation_ReassignsWhenTheCurrentAgentIsInactive()
    {
        var service = CreateService(
            AgentDtoFactory.Create(1, open: 3, active: false),
            AgentDtoFactory.Create(3, open: 4),
            AgentDtoFactory.Create(4, open: 2));

        var result = await Triage(service, TicketPriority.High, General, Standard, currentAgentId: 1);

        Assert.Equal(4, result.AssignedAgentId);
    }

    [Fact]
    public async Task Escalation_ReassignsWhenTheCurrentAgentLacksTheRequiredSpecialization()
    {
        var service = CreateService(
            AgentDtoFactory.Create(3, open: 0),
            AgentDtoFactory.Create(4, open: 1, specializations: [BillingId]));

        var result = await Triage(service, TicketPriority.High, Billing, Standard, currentAgentId: 3);

        Assert.Equal(4, result.AssignedAgentId);
    }

    [Fact]
    public async Task Escalation_ReassignsWhenTheCurrentAgentIsNoLongerKnown()
    {
        var service = CreateService(AgentDtoFactory.Create(4, open: 2));

        var result = await Triage(service, TicketPriority.High, General, Standard, currentAgentId: 99);

        Assert.Equal(4, result.AssignedAgentId);
    }

    [Fact]
    public async Task Escalation_WhenTheCurrentAgentIsIneligibleAndNobodyElseIs_LeavesTheTicketUnassigned()
    {
        var service = CreateService(AgentDtoFactory.Create(1, open: 0, active: false));

        var result = await Triage(service, TicketPriority.High, General, Standard, currentAgentId: 1);

        Assert.Null(result.AssignedAgentId);
    }
}
