using SupportDesk.Application.Contracts.Agents;
using SupportDesk.Application.Contracts.Tickets;

namespace SupportDesk.UnitTests.TestDoubles;

/// <summary>Builds the agent rows the triage rules read, stating only what a test cares about.</summary>
public static class AgentDtoFactory
{
    public static AgentDto Create(
        int id,
        int open,
        int max = 8,
        bool active = true,
        int[]? specializations = null) =>
        new(
            id,
            $"Agent {id}",
            $"agent{id}@example.com",
            active,
            max,
            open,
            (specializations ?? Array.Empty<int>())
                .Select(categoryId => new CategorySummaryDto(categoryId, $"Category {categoryId}"))
                .ToList());
}