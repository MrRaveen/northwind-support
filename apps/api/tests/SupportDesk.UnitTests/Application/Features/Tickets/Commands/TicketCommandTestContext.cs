using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Configuration;
using SupportDesk.Application.Contracts.Agents;
using SupportDesk.Application.Contracts.Categories;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Features.Tickets.Commands.AssignTicket;
using SupportDesk.Application.Features.Tickets.Commands.ChangeTicketStatus;
using SupportDesk.Application.Features.Tickets.Commands.EscalateTicket;
using SupportDesk.Application.Features.Tickets.Commands.RaiseTicket;
using SupportDesk.Application.Features.Tickets.Queries.GetTicket;
using SupportDesk.Application.Features.Tickets.Services;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Repositories;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Application.Features.Tickets.Commands;

/// <summary>
/// Shared arrangement for the ticket command tests: mocked repositories and queries, a fixed
/// clock, and command handlers wired to all of them.
/// </summary>
internal sealed class TicketCommandTestContext
{
    /// <summary>
    /// Default SLA options used when no custom options are needed. Mirror the values in
    /// appsettings.json so tests reflect realistic configuration.
    /// </summary>
    public static readonly SlaOptions DefaultSlaOptions = new()
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

    public TicketCommandTestContext() : this(DefaultSlaOptions) { }

    public TicketCommandTestContext(SlaOptions slaOptions)
    {
        // Customer: return a Standard customer by default so triage uses the base window.
        Customers.Setup(c => c.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Customer("Fabrikam", "support@fabrikam.example", null, CustomerTier.Standard, Clock.UtcNow));

        // Category: return a plain General category (no specialist, no forced critical) by default.
        Categories.Setup(c => c.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Category("General", requiresSpecialist: false, forcesCriticalPriority: false));

        Tickets.Setup(t => t.NextReferenceAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("TCK-0041");

        Tickets.Setup(t => t.AddAsync(It.IsAny<Ticket>(), It.IsAny<CancellationToken>()))
            .Callback<Ticket, CancellationToken>((ticket, _) => AddedTicket = ticket)
            .Returns(Task.CompletedTask);

        // Agent queries: return an empty list by default (ticket will be unassigned).
        AgentQueries.Setup(a => a.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<AgentDto>());

        // Every command returns the ticket as re-read once its change has been saved.
        TicketQueries.Setup(q => q.GetDetailAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TicketDetailDto
            {
                Id = 41,
                Reference = "TCK-0041",
                Customer = new CustomerContactDto(1, "Fabrikam", "support@fabrikam.example", null, CustomerTier.Standard),
                Category = new CategoryDto(1, "General", false)
            });

        var options = Options.Create(slaOptions);
        var triageService = new TicketTriageService(options, AgentQueries.Object);
        var getTicket = new GetTicketQueryHandler(TicketQueries.Object);

        RaiseTicket = new RaiseTicketCommandHandler(
            Tickets.Object, Customers.Object, Categories.Object, triageService,
            UnitOfWork.Object, Clock, getTicket,
            NullLogger<RaiseTicketCommandHandler>.Instance);

        ChangeTicketStatus = new ChangeTicketStatusCommandHandler(Tickets.Object, UnitOfWork.Object, Clock, getTicket);

        AssignTicket = new AssignTicketCommandHandler(
            Tickets.Object, Agents.Object, UnitOfWork.Object, Clock, getTicket,
            NullLogger<AssignTicketCommandHandler>.Instance);

        EscalateTicket = new EscalateTicketCommandHandler(
            Tickets.Object, Customers.Object, Categories.Object, triageService,
            UnitOfWork.Object, Clock, getTicket);
    }

    public Mock<ITicketRepository> Tickets { get; } = new();

    public Mock<ICustomerRepository> Customers { get; } = new();

    public Mock<ICategoryRepository> Categories { get; } = new();

    public Mock<IAgentRepository> Agents { get; } = new();

    public Mock<IAgentQueries> AgentQueries { get; } = new();

    public Mock<IUnitOfWork> UnitOfWork { get; } = new();

    public Mock<ITicketQueries> TicketQueries { get; } = new();

    public FixedClock Clock { get; } = new();

    public RaiseTicketCommandHandler RaiseTicket { get; }

    public ChangeTicketStatusCommandHandler ChangeTicketStatus { get; }

    public AssignTicketCommandHandler AssignTicket { get; }

    public EscalateTicketCommandHandler EscalateTicket { get; }

    /// <summary>The ticket handed to the repository by the last create call.</summary>
    public Ticket? AddedTicket { get; private set; }

    /// <summary>Makes <see cref="ITicketRepository.GetByIdAsync"/> return this ticket.</summary>
    public Ticket ExistingTicket(Ticket ticket)
    {
        Tickets.Setup(t => t.GetByIdAsync(ticket.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ticket);

        return ticket;
    }

    public void ExistingAgent(int id, string fullName = "Alex Turner", bool isActive = true)
    {
        var agent = new Agent(fullName, $"agent{id}@example.com", maxOpenTickets: 10, Clock.UtcNow);

        if (!isActive)
        {
            agent.Deactivate();
        }

        Agents.Setup(a => a.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(agent);
    }

    public void VerifySaved(Times times) =>
        UnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), times);
}
