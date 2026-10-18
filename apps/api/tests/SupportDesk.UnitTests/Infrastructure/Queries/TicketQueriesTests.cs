using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SupportDesk.Application.Configuration;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Infrastructure.Data;
using SupportDesk.Infrastructure.Queries;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Infrastructure.Queries;

/// <summary>
/// The ticket list and escalation history queries, run against a real (in-memory SQLite)
/// database so the filters are proven to happen in SQL, before counting and paging.
/// The SLA status filter is not covered here: it uses SQL Server's DATEDIFF, which SQLite does
/// not have, so it is checked against the running API instead.
/// </summary>
public sealed class TicketQueriesTests : IDisposable
{
    private static readonly DateTime Now = FixedClock.DefaultNow;

    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly SupportDbContext _db;
    private readonly TicketQueries _sut;

    private readonly Customer _contoso = new("Contoso", "support@contoso.example", null, CustomerTier.Premium, Now);
    private readonly Customer _fabrikam = new("Fabrikam", "support@fabrikam.example", null, CustomerTier.Standard, Now);
    private readonly Category _general = new("General", requiresSpecialist: false, forcesCriticalPriority: false);
    private readonly Category _billing = new("Billing", requiresSpecialist: true, forcesCriticalPriority: false);
    private readonly Agent _sara = new("Sara Lindqvist", "sara@example.com", 6, Now);

    public TicketQueriesTests()
    {
        _connection.Open();
        _db = new SupportDbContext(new DbContextOptionsBuilder<SupportDbContext>().UseSqlite(_connection).Options);
        _db.Database.EnsureCreated();

        Seed();

        var options = Options.Create(new SlaOptions { AtRiskThresholdPercent = 25 });
        _sut = new TicketQueries(_db, new FixedClock(), options);
    }

    private void Seed()
    {
        _db.AddRange(_contoso, _fabrikam, _general, _billing, _sara);
        _db.SaveChanges();

        var inProgress = Raise("TCK-0003", "Cannot log in", _fabrikam, _general, TicketPriority.High);
        inProgress.ChangeStatus(TicketStatus.InProgress, Now);

        _db.AddRange(
            Raise("TCK-0001", "Printer offline", _contoso, _general, TicketPriority.High, _sara.Id),
            Raise("TCK-0002", "Invoice total wrong", _fabrikam, _billing, TicketPriority.Low),
            inProgress,
            Raise("TCK-0004", "Refund request", _contoso, _billing, TicketPriority.Low, _sara.Id));
        _db.SaveChanges();
    }

    private static Ticket Raise(
        string reference,
        string title,
        Customer customer,
        Category category,
        TicketPriority priority,
        int? agentId = null) =>
        Ticket.Raise(
            reference, title, "A description long enough to be realistic.",
            customer.Id, category.Id, priority, forcesCriticalPriority: false,
            dueAtUtc: Now.AddHours(24), assignedAgentId: agentId, Now);

    private async Task<string[]> References(TicketQuery query)
    {
        var result = await _sut.GetPagedAsync(query, CancellationToken.None);

        return result.Items.Select(item => item.Reference).OrderBy(reference => reference).ToArray();
    }

    // ---- FL-1: exact matches ---------------------------------------------------------------

    [Fact]
    public async Task Priority_ReturnsOnlyTicketsOfThatPriority()
    {
        var high = await _sut.GetPagedAsync(new TicketQuery { Priority = TicketPriority.High }, CancellationToken.None);

        Assert.Equal(2, high.TotalCount);
        Assert.All(high.Items, item => Assert.Equal(TicketPriority.High, item.Priority));
    }

    [Fact]
    public async Task Priority_Low_DoesNotReturnTheWholeTable()
    {
        var references = await References(new TicketQuery { Priority = TicketPriority.Low });

        Assert.Equal(new[] { "TCK-0002", "TCK-0004" }, references);
    }

    [Fact]
    public async Task Status_ReturnsOnlyTicketsInThatStatus()
    {
        var references = await References(new TicketQuery { Status = TicketStatus.InProgress });

        Assert.Equal(new[] { "TCK-0003" }, references);
    }

    [Fact]
    public async Task Category_ReturnsOnlyTicketsInThatCategory()
    {
        var references = await References(new TicketQuery { CategoryId = _billing.Id });

        Assert.Equal(new[] { "TCK-0002", "TCK-0004" }, references);
    }

    [Fact]
    public async Task Customer_ReturnsOnlyThatCustomersTickets()
    {
        var references = await References(new TicketQuery { CustomerId = _contoso.Id });

        Assert.Equal(new[] { "TCK-0001", "TCK-0004" }, references);
    }

    [Fact]
    public async Task AssignedAgent_ReturnsOnlyTicketsOwnedByThatAgent()
    {
        var references = await References(new TicketQuery { AssignedAgentId = _sara.Id });

        Assert.Equal(new[] { "TCK-0001", "TCK-0004" }, references);
    }

    // ---- FL-2: unassigned only -------------------------------------------------------------

    [Fact]
    public async Task UnassignedOnly_ReturnsOnlyTicketsWithNoAgent()
    {
        var references = await References(new TicketQuery { UnassignedOnly = true });

        Assert.Equal(new[] { "TCK-0002", "TCK-0003" }, references);
    }

    // ---- FL-3: search ----------------------------------------------------------------------

    [Fact]
    public async Task Search_MatchesTheCustomerName()
    {
        var references = await References(new TicketQuery { Search = "Fabrikam" });

        Assert.Equal(new[] { "TCK-0002", "TCK-0003" }, references);
    }

    [Fact]
    public async Task Search_MatchesTheReference()
    {
        var references = await References(new TicketQuery { Search = "TCK-0004" });

        Assert.Equal(new[] { "TCK-0004" }, references);
    }

    [Fact]
    public async Task Search_MatchesPartOfTheTitle()
    {
        var references = await References(new TicketQuery { Search = "Invoice" });

        Assert.Equal(new[] { "TCK-0002" }, references);
    }

    [Fact]
    public async Task Search_IsTrimmed_AndABlankSearchIsIgnored()
    {
        var trimmed = await References(new TicketQuery { Search = "  Fabrikam  " });
        var blank = await References(new TicketQuery { Search = "   " });

        Assert.Equal(new[] { "TCK-0002", "TCK-0003" }, trimmed);
        Assert.Equal(4, blank.Length);
    }

    // ---- FL-4: filters combine with AND ----------------------------------------------------

    [Fact]
    public async Task Filters_CombineWithAnd()
    {
        var references = await References(new TicketQuery
        {
            Priority = TicketPriority.High,
            CustomerId = _fabrikam.Id
        });

        Assert.Equal(new[] { "TCK-0003" }, references);
    }

    [Fact]
    public async Task Filters_StatusUnassignedAndSearchTogether_NarrowTheListFurther()
    {
        var references = await References(new TicketQuery
        {
            Status = TicketStatus.InProgress,
            UnassignedOnly = true,
            Search = "log in"
        });

        Assert.Equal(new[] { "TCK-0003" }, references);
    }

    [Fact]
    public async Task Filters_AnAgentAndUnassignedOnlyTogether_MatchNothing()
    {
        var references = await References(new TicketQuery { AssignedAgentId = _sara.Id, UnassignedOnly = true });

        Assert.Empty(references);
    }

    // ---- FL-5: filtered before counting and paging -----------------------------------------

    [Fact]
    public async Task TheCountAndPagesDescribeTheFilteredSet_NotTheWholeTable()
    {
        var secondPage = await _sut.GetPagedAsync(
            new TicketQuery { Priority = TicketPriority.High, PageSize = 1, Page = 2 },
            CancellationToken.None);

        Assert.Equal(2, secondPage.TotalCount);
        Assert.Equal(2, secondPage.TotalPages);
        Assert.Single(secondPage.Items);
    }

    // ---- Escalation history ----------------------------------------------------------------

    [Fact]
    public async Task GetEscalationsAsync_ReturnsTheNewestFirstWithAgentNames()
    {
        var ticket = _db.Set<Ticket>().Single(t => t.Reference == "TCK-0002");
        ticket.Escalate("First reason", "alex.lead", Now.AddHours(9), _sara.Id, Now.AddHours(1));
        ticket.Escalate("Second reason", "ben.osei", Now.AddHours(10), _sara.Id, Now.AddHours(2));
        await _db.SaveChangesAsync();

        var history = await _sut.GetEscalationsAsync(ticket.Id, CancellationToken.None);

        Assert.Equal(2, history.Count);

        Assert.Equal("Second reason", history[0].Reason);
        Assert.Equal(TicketPriority.Medium, history[0].FromPriority);
        Assert.Equal(TicketPriority.High, history[0].ToPriority);

        Assert.Equal("First reason", history[1].Reason);
        Assert.Null(history[1].FromAgent); // the ticket was unassigned before the first escalation
        Assert.Equal("Sara Lindqvist", history[1].ToAgent?.FullName);
    }

    [Fact]
    public async Task GetEscalationsAsync_ForATicketNeverEscalated_IsEmpty()
    {
        var ticket = _db.Set<Ticket>().Single(t => t.Reference == "TCK-0001");

        var history = await _sut.GetEscalationsAsync(ticket.Id, CancellationToken.None);

        Assert.Empty(history);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }
}
