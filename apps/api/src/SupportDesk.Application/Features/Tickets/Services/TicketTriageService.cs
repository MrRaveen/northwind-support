using Microsoft.Extensions.Options;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Configuration;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Application.Features.Tickets.Services;

public sealed record TicketTriageResult(
    TicketPriority Priority,
    DateTime DueAtUtc,
    int? AssignedAgentId,
    TicketTriageDto TriageDto
);

/// <summary>
/// Applies BR-1 through BR-5: priority, SLA window, and agent assignment.
/// Split into focused private methods so create and escalate share identical rules.
/// </summary>
public sealed class TicketTriageService(
    IOptions<SlaOptions> slaOptions,
    IAgentQueries agentQueries)
{
    public async Task<TicketTriageResult> TriageAsync(
        TicketPriority? requestedPriority,
        Category category,
        Customer customer,
        DateTime nowUtc,
        int? currentAgentId,
        CancellationToken ct)
    {
        var options = slaOptions.Value;

        // BR-1: Priority
        var (finalPriority, priorityReason) = ResolvePriority(requestedPriority, category);

        // BR-2 & BR-3: SLA window
        var (finalHours, slaReason) = ResolveWindow(finalPriority, customer, options);

        var dueAtUtc = nowUtc.AddHours(finalHours);

        // BR-4 & BR-5: Agent assignment
        var agents = await agentQueries.GetAllAsync(ct);
        var (assignedAgentId, assignmentReason) = SelectAgent(agents, category, currentAgentId);

        return new TicketTriageResult(
            finalPriority,
            dueAtUtc,
            assignedAgentId,
            new TicketTriageDto(
                finalPriority.ToString(),
                priorityReason,
                finalHours,
                slaReason,
                assignmentReason));
    }

    // ── BR-1: Priority ────────────────────────────────────────────────────────────────────────

    private static (TicketPriority priority, string reason) ResolvePriority(
        TicketPriority? requested, Category category)
    {
        if (category.ForcesCriticalPriority)
        {
            return (TicketPriority.Critical,
                $"Category '{category.Name}' always forces Critical priority");
        }

        var priority = requested ?? TicketPriority.Medium;
        var reason = requested.HasValue ? "Requested priority applied" : "Default priority applied (Medium)";
        return (priority, reason);
    }

    // ── BR-2 & BR-3: SLA window ───────────────────────────────────────────────────────────────

    private static (double hours, string reason) ResolveWindow(
        TicketPriority priority, Customer customer, SlaOptions options)
    {
        if (!options.BaseWindowHours.TryGetValue(priority.ToString(), out var baseHours))
        {
            // Config is validated on startup (ValidateOnStart), so reaching here means the config
            // key name is misspelled. Throw explicitly so it surfaces clearly.
            throw new InvalidOperationException(
                $"SLA base window not configured for priority '{priority}'. " +
                "Ensure all four priorities are present in Sla:BaseWindowHours in appsettings.json.");
        }

        if (customer.Tier == CustomerTier.Premium)
        {
            var premiumHours = Math.Max(options.MinimumWindowHours, baseHours * options.PremiumCustomerMultiplier);
            return (premiumHours,
                $"{priority} base window {baseHours}h, halved for a Premium customer (floor {options.MinimumWindowHours}h)");
        }

        return (baseHours, $"{priority} base window {baseHours}h");
    }

    // ── BR-4 & BR-5: Agent assignment ────────────────────────────────────────────────────────

    private static (int? agentId, string reason) SelectAgent(
        IReadOnlyList<global::SupportDesk.Application.Contracts.Agents.AgentDto> agents,
        Category category,
        int? currentAgentId)
    {
        // BR-5 fix: when escalating, the ticket already belongs to currentAgent, so their open
        // count includes this ticket. A full agent (e.g. 6/6) would wrongly fail the capacity
        // check. Check the current agent's eligibility WITHOUT re-checking capacity — they already
        // hold the ticket, so assigning it to them again adds no load.
        if (currentAgentId.HasValue)
        {
            var currentAgent = agents.FirstOrDefault(a => a.Id == currentAgentId.Value);
            if (currentAgent is { IsActive: true } &&
                (!category.RequiresSpecialist || currentAgent.Specializations.Any(s => s.Id == category.Id)))
            {
                return (currentAgentId.Value, "Current agent is still eligible (no capacity re-check on escalation)");
            }
        }

        // Strictly-below capacity filter only applies to newly-assigned agents.
        var eligible = agents
            .Where(a => a.IsActive && a.OpenTicketCount < a.MaxOpenTickets)
            .ToList();

        if (category.RequiresSpecialist)
        {
            eligible = eligible
                .Where(a => a.Specializations.Any(s => s.Id == category.Id))
                .ToList();
        }

        var best = eligible
            .OrderBy(a => a.OpenTicketCount)
            .ThenBy(a => a.Id) // Deterministic tie-break by stable id
            .FirstOrDefault();

        if (best is not null)
        {
            var reason = category.RequiresSpecialist
                ? $"Fewest open tickets among {category.Name} specialists"
                : "Fewest open tickets among all active agents";
            return (best.Id, reason);
        }

        var unassignedReason = category.RequiresSpecialist
            ? $"No available agent specialised in {category.Name}"
            : "No available agent";
        return (null, unassignedReason);
    }
}