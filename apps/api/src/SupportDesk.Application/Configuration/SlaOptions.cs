using System.ComponentModel.DataAnnotations;

namespace SupportDesk.Application.Configuration;

/// <summary>
/// Values from the "Sla" section of appsettings.json. Validated on startup (ValidateOnStart)
/// so a misconfigured environment fails fast rather than silently producing wrong SLAs.
/// </summary>
public sealed class SlaOptions
{
    /// <summary>Base SLA window in hours per priority name (Low, Medium, High, Critical).</summary>
    [Required]
    public Dictionary<string, int> BaseWindowHours { get; set; } = new();

    /// <summary>Fraction applied to the base window for Premium customers (e.g. 0.5 = half). Must be in (0, 1].</summary>
    [Range(0.01, 1.0)]
    public double PremiumCustomerMultiplier { get; set; }

    /// <summary>Minimum SLA window in hours (floor for the Premium halving). Must be positive.</summary>
    [Range(1, int.MaxValue)]
    public int MinimumWindowHours { get; set; }

    /// <summary>Percentage of the window remaining at which a ticket switches to AtRisk (1-99).</summary>
    [Range(1, 99)]
    public int AtRiskThresholdPercent { get; set; }
}