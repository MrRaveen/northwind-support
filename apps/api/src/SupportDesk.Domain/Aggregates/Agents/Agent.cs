using SupportDesk.Domain.Common;

namespace SupportDesk.Domain.Aggregates.Agents;

/// <summary>
/// A member of the support team, together with the categories they are qualified to handle.
/// </summary>
public sealed class Agent : AggregateRoot
{
    private readonly List<AgentSpecialization> _specializations = [];

    private Agent()
    {
        // For EF Core.
    }

    public Agent(string fullName, string email, int maxOpenTickets, DateTime createdAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullName);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxOpenTickets);

        FullName = fullName;
        Email = email;
        IsActive = true;
        MaxOpenTickets = maxOpenTickets;
        CreatedAtUtc = createdAtUtc;
    }

    public string FullName { get; private set; } = string.Empty;

    public string Email { get; private set; } = string.Empty;

    /// <summary>Inactive agents keep their history but take no new work.</summary>
    public bool IsActive { get; private set; }

    /// <summary>How many open tickets this agent is willing to hold at once.</summary>
    public int MaxOpenTickets { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }
    
    /// <summary>Salted hash of the agent's password. Never the password itself.</summary>
    public string? PasswordHash { get; private set; }

    public void SetPasswordHash(string passwordHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);

        PasswordHash = passwordHash;
    }
    /// <summary>The ticket categories this agent is qualified for.</summary>
    public IReadOnlyCollection<AgentSpecialization> Specializations => _specializations;

    /// <summary>Records that the agent is qualified for a category. Adding one twice is a no-op.</summary>
    public void AddSpecialization(int categoryId)
    {
        if (_specializations.Any(s => s.CategoryId == categoryId))
        {
            return;
        }

        _specializations.Add(new AgentSpecialization(categoryId));
    }

    /// <summary>Stops the agent from taking new work. Their history is kept.</summary>
    public void Deactivate() => IsActive = false;
}
