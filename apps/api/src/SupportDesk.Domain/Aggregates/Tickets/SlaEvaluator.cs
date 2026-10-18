namespace SupportDesk.Domain.Aggregates.Tickets;

/// <summary>
/// Works out where a ticket stands against its due date. SLA status is always derived from
/// the ticket's dates and the current time - never stored.
/// </summary>
public static class SlaEvaluator
{
    /// <summary>
    /// Evaluates SLA status.
    /// </summary>
    /// <param name="windowStartUtc">When the current SLA window started (CreatedAtUtc on raise, or escalation time after escalation).</param>
    /// <param name="dueAtUtc">When the ticket is due; null means no SLA applies.</param>
    /// <param name="resolvedAtUtc">When the ticket was resolved; null if still open.</param>
    /// <param name="nowUtc">The current time (injected so tests are deterministic).</param>
    /// <param name="atRiskThreshold">Fraction of the window at which to switch to AtRisk, e.g. 0.25. Sourced from SlaOptions.AtRiskThresholdPercent / 100.0 by callers.</param>
    public static SlaStatus Evaluate(
        DateTime windowStartUtc,
        DateTime? dueAtUtc,
        DateTime? resolvedAtUtc,
        DateTime nowUtc,
        double atRiskThreshold = 0.25)
    {
        if (dueAtUtc is null)
        {
            return SlaStatus.NotApplicable;
        }

        if (resolvedAtUtc is not null)
        {
            return resolvedAtUtc <= dueAtUtc ? SlaStatus.Met : SlaStatus.Breached;
        }

        if (nowUtc > dueAtUtc)
        {
            return SlaStatus.Breached;
        }

        var windowDuration = dueAtUtc.Value - windowStartUtc;
        var timeRemaining = dueAtUtc.Value - nowUtc;

        if (timeRemaining <= windowDuration * atRiskThreshold)
        {
            return SlaStatus.AtRisk;
        }

        return SlaStatus.WithinSla;
    }
}