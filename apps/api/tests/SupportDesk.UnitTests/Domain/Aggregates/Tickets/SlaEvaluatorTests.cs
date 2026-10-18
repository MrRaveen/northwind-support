using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Domain.Aggregates.Tickets;

public class SlaEvaluatorTests
{
    private static readonly DateTime Now = FixedClock.DefaultNow;

    // ── NotApplicable ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Evaluate_WithoutADueDate_IsNotApplicable() =>
        Assert.Equal(SlaStatus.NotApplicable,
            SlaEvaluator.Evaluate(Now, dueAtUtc: null, resolvedAtUtc: null, Now));

    // ── Resolved cases ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void Evaluate_ResolvedExactlyOnDue_IsMet() =>
        // resolved == due → Met (boundary: on time is Met, not Breached)
        Assert.Equal(SlaStatus.Met,
            SlaEvaluator.Evaluate(Now, dueAtUtc: Now.AddHours(1), resolvedAtUtc: Now.AddHours(1), Now));

    [Fact]
    public void Evaluate_ResolvedBeforeDue_IsMet() =>
        Assert.Equal(SlaStatus.Met,
            SlaEvaluator.Evaluate(Now, dueAtUtc: Now.AddDays(1), resolvedAtUtc: Now, Now.AddDays(2)));

    [Fact]
    public void Evaluate_ResolvedOneMinuteAfterDue_IsBreached() =>
        Assert.Equal(SlaStatus.Breached,
            SlaEvaluator.Evaluate(Now, dueAtUtc: Now, resolvedAtUtc: Now.AddMinutes(1), Now));

    // ── Open and past due ────────────────────────────────────────────────────────────────────

    [Fact]
    public void Evaluate_OpenAndOneMinutePastDue_IsBreached() =>
        Assert.Equal(SlaStatus.Breached,
            SlaEvaluator.Evaluate(Now.AddMinutes(-61), dueAtUtc: Now.AddMinutes(-1), resolvedAtUtc: null, Now));

    // ── Open and exact boundary: now == due is NOT Breached ──────────────────────────────────

    [Fact]
    public void Evaluate_OpenAndNowExactlyEqualsDue_IsNotBreached()
    {
        // now == dueAtUtc: the condition is nowUtc > dueAtUtc, so exactly equal is NOT Breached.
        // With 25 % threshold and a 4h window, 0 minutes remaining ≤ 60 minutes → AtRisk.
        var windowStart = Now;
        var due = Now;  // now == due
        var result = SlaEvaluator.Evaluate(windowStart, due, resolvedAtUtc: null, Now);
        Assert.NotEqual(SlaStatus.Breached, result);
        Assert.Equal(SlaStatus.AtRisk, result);  // 0 remaining ≤ 25% of 0 window → AtRisk
    }

    // ── AtRisk boundaries ────────────────────────────────────────────────────────────────────

    [Fact]
    public void Evaluate_OpenAndExactlyAtRiskThreshold_IsAtRisk()
    {
        // Window = 4h (240 min). 25% = 60 min. At exactly 60 min remaining → AtRisk (≤).
        var windowStart = Now;
        var due = Now.AddHours(4);
        var nowAtBoundary = due.AddMinutes(-60);
        Assert.Equal(SlaStatus.AtRisk,
            SlaEvaluator.Evaluate(windowStart, due, resolvedAtUtc: null, nowAtBoundary, atRiskThreshold: 0.25));
    }

    [Fact]
    public void Evaluate_OpenAndOneMinuteAboveAtRiskThreshold_IsWithinSla()
    {
        // Window = 4h (240 min). 25% = 60 min. At 61 min remaining → WithinSla (>).
        var windowStart = Now;
        var due = Now.AddHours(4);
        var nowJustAbove = due.AddMinutes(-61);
        Assert.Equal(SlaStatus.WithinSla,
            SlaEvaluator.Evaluate(windowStart, due, resolvedAtUtc: null, nowJustAbove, atRiskThreshold: 0.25));
    }

    [Fact]
    public void Evaluate_OpenAndBeforeDue_WithHighThreshold_IsWithinSla()
    {
        // Still within SLA when due is in the future and we are comfortably inside the window.
        Assert.Equal(SlaStatus.WithinSla,
            SlaEvaluator.Evaluate(Now, dueAtUtc: Now.AddHours(1), resolvedAtUtc: null, Now));
    }

    // ── Custom atRiskThreshold ───────────────────────────────────────────────────────────────

    [Fact]
    public void Evaluate_WithCustomThreshold_UsesItInsteadOf25Percent()
    {
        // Window = 100 min, threshold = 50% (0.5), now at 49 min remaining → AtRisk.
        var windowStart = Now;
        var due = Now.AddMinutes(100);
        var nowAt49 = due.AddMinutes(-49);
        Assert.Equal(SlaStatus.AtRisk,
            SlaEvaluator.Evaluate(windowStart, due, resolvedAtUtc: null, nowAt49, atRiskThreshold: 0.5));
    }
}
