using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SupportDesk.Application.Abstractions;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Infrastructure.Data;

/// <summary>
/// Development seed data: the small support desk the team uses to demo the application.
/// Mirrors <c>database/02_seed.sql</c>, including the relative timestamps, so both routes
/// produce the same database.
/// </summary>
/// <remarks>
/// Every aggregate is built through its own behaviour. The two historical values no behaviour
/// produces - a ticket's last update and its due date - are set through EF Core directly.
/// </remarks>
public sealed class SupportDbSeeder(SupportDbContext db, IClock clock, ILogger<SupportDbSeeder> logger)
{
    /// <summary>Seeds the database, unless it already has tickets in it.</summary>
    public async Task SeedAsync(CancellationToken ct = default)
    {
        if (await db.Set<Ticket>().AnyAsync(ct))
        {
            logger.LogInformation("Seed data already present; skipping.");
            return;
        }

        var now = clock.UtcNow;

        var categories = SeedCategories();
        var customers = SeedCustomers(now);
        var agents = SeedAgents(now);

        db.AddRange(categories);
        db.AddRange(customers);
        db.AddRange(agents);
        await db.SaveChangesAsync(ct);

        SeedSpecializations(agents, categories);
        await db.SaveChangesAsync(ct);

        SeedTickets(now, customers, categories, agents);
        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "Seeded {Categories} categories, {Customers} customers, {Agents} agents and {Tickets} tickets.",
            categories.Count, customers.Count, agents.Count, await db.Set<Ticket>().CountAsync(ct));
    }

    private static List<Category> SeedCategories() =>
    [
        new("General", requiresSpecialist: false, forcesCriticalPriority: false),
        new("Billing", requiresSpecialist: true, forcesCriticalPriority: false),
        new("Technical", requiresSpecialist: false, forcesCriticalPriority: false),
        new("Outage", requiresSpecialist: true, forcesCriticalPriority: true),
        new("Security", requiresSpecialist: true, forcesCriticalPriority: true)
    ];

    private static List<Customer> SeedCustomers(DateTime now) =>
    [
        new("Contoso Ltd", "support@contoso.example", "+44 20 7946 0101", CustomerTier.Premium, now.AddDays(-420)),
        new("Fabrikam Inc", "helpdesk@fabrikam.example", "+1 415 555 0102", CustomerTier.Standard, now.AddDays(-365)),
        new("Adventure Works", "it@adventure-works.example", "+61 2 5550 0103", CustomerTier.Premium, now.AddDays(-300)),
        new("Northwind Traders", "ops@northwind.example", "+1 206 555 0104", CustomerTier.Standard, now.AddDays(-260)),
        new("Tailspin Toys", "service@tailspin.example", "+353 1 555 0105", CustomerTier.Standard, now.AddDays(-180)),
        new("Wide World Importers", "support@wideworld.example", "+65 6555 0106", CustomerTier.Premium, now.AddDays(-90))
    ];

    private static List<Agent> SeedAgents(DateTime now)
    {
        var yuki = new Agent("Yuki Tanaka", "yuki.tanaka@northwind-support.example", 8, now.AddDays(-600));
        yuki.Deactivate();

        return
        [
            new("Alex Turner", "alex.turner@northwind-support.example", 10, now.AddDays(-500)),
            new("Ben Osei", "ben.osei@northwind-support.example", 8, now.AddDays(-460)),
            new("Priya Nair", "priya.nair@northwind-support.example", 8, now.AddDays(-400)),
            new("Sara Lindqvist", "sara.lindqvist@northwind-support.example", 6, now.AddDays(-280)),
            new("Marcus Doyle", "marcus.doyle@northwind-support.example", 6, now.AddDays(-200)),
            yuki
        ];
    }

    /// <summary>
    /// Who can handle what:
    /// Alex - General, Technical | Ben - Technical, Outage | Priya - Outage, Security
    /// Sara - General, Billing   | Marcus - Billing, Security | Yuki (inactive) - Technical
    /// </summary>
    private static void SeedSpecializations(List<Agent> agents, List<Category> categories)
    {
        (int Agent, int Category)[] pairs =
        [
            (1, 1), (1, 3),
            (2, 3), (2, 4),
            (3, 4), (3, 5),
            (4, 1), (4, 2),
            (5, 2), (5, 5),
            (6, 3)
        ];

        foreach (var (agent, category) in pairs)
        {
            agents[agent - 1].AddSpecialization(categories[category - 1].Id);
        }
    }

    /// <summary>
    /// One ticket per row of the seed table. <c>SlaWindowMinutes</c> is what the current
    /// response-window rules would give for that priority and customer tier, and the mix of
    /// creation times deliberately produces tickets that are comfortably inside their window,
    /// close to it, and past it.
    /// </summary>
    private void SeedTickets(
        DateTime now,
        List<Customer> customers,
        List<Category> categories,
        List<Agent> agents)
    {
        SeedTicket[] rows =
        [
            // Open, assigned.
            new("TCK-0001", "Cannot log in to the customer portal", "Users get \"invalid credentials\" even after a password reset. Started after the weekend release.", 1, 3, 1, TicketPriority.High, TicketStatus.InProgress, 1800, 240, null),
            new("TCK-0002", "Export to CSV produces empty file", "The export button downloads a 0 KB file for any date range.", 2, 3, 1, TicketPriority.Medium, TicketStatus.New, 600, 1440, null),
            new("TCK-0003", "Request for additional user seats", "We would like to add five more users to our plan.", 4, 1, 1, TicketPriority.Low, TicketStatus.Open, 6000, 4320, null),
            new("TCK-0004", "Dashboard charts fail to render in Safari", "The charts area stays blank; the console shows a script error.", 5, 3, 1, TicketPriority.High, TicketStatus.InProgress, 420, 480, null),
            new("TCK-0005", "How do I bulk-import contacts?", "Documentation link in the app returns a 404.", 6, 1, 1, TicketPriority.Medium, TicketStatus.Open, 180, 720, null),
            new("TCK-0006", "Production outage - all services unreachable", "Nothing responds from our region since 09:10 UTC. Entire team is blocked.", 3, 4, 2, TicketPriority.Critical, TicketStatus.InProgress, 60, 120, null),
            new("TCK-0007", "API returns 500 on bulk update", "PUT /v1/items/bulk fails for payloads larger than 50 items.", 2, 3, 2, TicketPriority.High, TicketStatus.Open, 450, 480, null),
            new("TCK-0008", "Webhook deliveries are delayed", "Events arrive up to 20 minutes late since Tuesday.", 4, 3, 2, TicketPriority.Medium, TicketStatus.New, 300, 1440, null),
            new("TCK-0009", "Suspicious login attempts from unknown IPs", "We see repeated failed logins from addresses we do not recognise.", 1, 5, 3, TicketPriority.Critical, TicketStatus.Open, 105, 120, null),
            new("TCK-0010", "Regional endpoint intermittently down", "Roughly one request in five times out.", 3, 4, 3, TicketPriority.Critical, TicketStatus.InProgress, 30, 120, null),
            new("TCK-0011", "Enforce SSO for all our users", "We need to block password login now that SSO is live.", 6, 5, 3, TicketPriority.High, TicketStatus.Open, 200, 240, null),
            new("TCK-0012", "Scheduled maintenance notice not received", "We did not get the maintenance email for last night.", 5, 4, 3, TicketPriority.Medium, TicketStatus.New, 1500, 1440, null),
            new("TCK-0013", "Invoice total does not match the order", "Invoice 5512 is EUR 240 higher than the confirmed order.", 2, 2, 4, TicketPriority.High, TicketStatus.InProgress, 200, 480, null),
            new("TCK-0014", "Update billing address on our account", "We moved office last month.", 4, 2, 4, TicketPriority.Low, TicketStatus.New, 1000, 4320, null),
            new("TCK-0015", "Duplicate charge on September invoice", "We were charged twice for the same subscription period.", 1, 2, 5, TicketPriority.Medium, TicketStatus.Open, 800, 720, null),
            new("TCK-0016", "Password reset emails contain a broken link", "The reset link points to localhost.", 3, 5, 5, TicketPriority.High, TicketStatus.InProgress, 60, 240, null),
            new("TCK-0017", "VAT number missing from invoices", "Our finance team cannot file these invoices.", 5, 2, 5, TicketPriority.Medium, TicketStatus.Open, 1200, 1440, null),
            new("TCK-0018", "Annual plan quote request", "Please send a quote for switching to annual billing.", 6, 2, 5, TicketPriority.Low, TicketStatus.New, 600, 2160, null),
            new("TCK-0019", "Audit log export missing entries", "Entries between 02:00 and 04:00 are absent from the export.", 4, 5, 5, TicketPriority.High, TicketStatus.Open, 240, 480, null),
            new("TCK-0020", "Timezone shown incorrectly on reports", "Reports render in UTC instead of our local timezone.", 2, 1, 5, TicketPriority.Medium, TicketStatus.InProgress, 100, 1440, null),
            // Open, waiting for an owner.
            new("TCK-0021", "Possible data exposure in shared links", "A shared link appears to be accessible without the password.", 1, 5, null, TicketPriority.Critical, TicketStatus.New, 45, 120, null),
            new("TCK-0022", "Credit note not applied to next invoice", "The credit note from August was never applied.", 4, 2, null, TicketPriority.High, TicketStatus.New, 500, 480, null),
            new("TCK-0023", "Request a walkthrough of the new UI", "Could someone show the team the new navigation?", 5, 1, null, TicketPriority.Medium, TicketStatus.New, 20, 1440, null),
            new("TCK-0024", "Mobile app crashes on cold start", "Happens on Android 14 only, about half the time.", 6, 3, null, TicketPriority.Low, TicketStatus.New, 90, 2160, null),
            // Resolved and closed.
            new("TCK-0025", "Search returns stale results", "Newly created items take minutes to appear in search.", 1, 3, 1, TicketPriority.High, TicketStatus.Resolved, 3000, 240, 200),
            new("TCK-0026", "Add a second admin to our workspace", "Please grant admin rights to our new team lead.", 2, 1, 1, TicketPriority.Medium, TicketStatus.Closed, 5000, 1440, 1000),
            new("TCK-0027", "Checkout unavailable for 40 minutes", "Customers could not complete purchases during the incident.", 3, 4, 2, TicketPriority.Critical, TicketStatus.Resolved, 2000, 120, 300),
            new("TCK-0028", "Rate limit hit during nightly sync", "Our sync job is throttled every night around 01:00.", 4, 3, 2, TicketPriority.High, TicketStatus.Closed, 4000, 480, 400),
            new("TCK-0029", "Compromised API key needs rotation", "A key was committed to a public repository and must be revoked.", 1, 5, 3, TicketPriority.Critical, TicketStatus.Resolved, 1500, 120, 90),
            new("TCK-0030", "Two-factor codes rejected intermittently", "About one code in three is rejected as invalid.", 3, 5, 3, TicketPriority.High, TicketStatus.Resolved, 2500, 240, 600),
            new("TCK-0031", "Wrong currency on renewal invoice", "Invoice arrived in USD instead of EUR.", 2, 2, 4, TicketPriority.Medium, TicketStatus.Resolved, 3500, 1440, 1200),
            new("TCK-0032", "Refund for cancelled add-on", "The add-on was cancelled but still charged.", 5, 2, 4, TicketPriority.High, TicketStatus.Closed, 4500, 480, 300),
            new("TCK-0033", "Purchase order number missing on invoice", "Our AP system rejects invoices without a PO number.", 6, 2, 5, TicketPriority.Medium, TicketStatus.Resolved, 2200, 720, 900),
            new("TCK-0034", "Request for onboarding material", "Any slides we can share internally?", 4, 1, 5, TicketPriority.Low, TicketStatus.Closed, 6000, 4320, 3000),
            new("TCK-0035", "File uploads fail above 20 MB", "Uploads stall and then fail without an error message.", 2, 3, 6, TicketPriority.Medium, TicketStatus.Closed, 5500, 1440, 1100),
            new("TCK-0036", "Notifications duplicated three times", "Every notification arrives three times.", 5, 3, 6, TicketPriority.High, TicketStatus.Resolved, 3200, 480, 420),
            new("TCK-0037", "Database failover caused brief downtime", "Roughly six minutes of errors during the failover.", 3, 4, 1, TicketPriority.Critical, TicketStatus.Resolved, 1200, 120, 100),
            new("TCK-0038", "Onboarding call follow-up questions", "Three questions from our call last week.", 6, 1, 2, TicketPriority.Medium, TicketStatus.Resolved, 2800, 720, 500),
            new("TCK-0039", "Session expires after two minutes", "Agents are logged out constantly.", 4, 5, 3, TicketPriority.High, TicketStatus.Resolved, 3300, 480, 700),
            new("TCK-0040", "Historic invoices needed for audit", "Please provide all invoices for the last two years.", 1, 2, 4, TicketPriority.Low, TicketStatus.Closed, 7000, 2160, 1500)
        ];

        foreach (var row in rows)
        {
            var createdAtUtc = now.AddMinutes(-row.CreatedMinutesAgo);
            var category = categories[row.CategoryIndex - 1];
            var assignedAgentId = row.AgentIndex is not null 
                ? agents[row.AgentIndex.Value - 1].Id 
                : (int?)null;
            var dueAtUtc = createdAtUtc.AddMinutes(row.SlaWindowMinutes);

            var ticket = Ticket.Raise(
                row.Reference,
                row.Title,
                row.Description,
                customers[row.CustomerIndex - 1].Id,
                category.Id,
                row.Priority,
                category.ForcesCriticalPriority,
                dueAtUtc,
                assignedAgentId,
                createdAtUtc);

            if (row.Status != TicketStatus.New)
            {
                // Resolving or closing stamps the resolution time; open statuses clear it.
                ticket.ChangeStatus(row.Status, createdAtUtc.AddMinutes(row.ResolvedAfterMinutes ?? 0));
            }

            var entry = db.Add(ticket);
            entry.Property(t => t.UpdatedAtUtc).CurrentValue =
                createdAtUtc.AddMinutes(row.ResolvedAfterMinutes ?? row.CreatedMinutesAgo / 2);
            entry.Property(t => t.DueAtUtc).CurrentValue = dueAtUtc;
        }
    }

    private sealed record SeedTicket(
        string Reference,
        string Title,
        string Description,
        int CustomerIndex,
        int CategoryIndex,
        int? AgentIndex,
        TicketPriority Priority,
        TicketStatus Status,
        int CreatedMinutesAgo,
        int SlaWindowMinutes,
        int? ResolvedAfterMinutes);
}
