using SupportDesk.Application.Contracts.Auth;
using SupportDesk.Application.Exceptions;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.Application.Features.Auth.Queries.GetCurrentUser;

public sealed class GetCurrentUserQueryHandler(IAgentRepository agents)
{
    /// <exception cref="UnauthorizedException">The agent no longer exists or is inactive.</exception>
    public async Task<CurrentUserDto> HandleAsync(int agentId, CancellationToken ct)
    {
        var agent = await agents.GetByIdAsync(agentId, ct);

        if (agent is null || !agent.IsActive)
        {
            throw new UnauthorizedException("The session is no longer valid.");
        }

        return new CurrentUserDto(agent.Id, agent.FullName, agent.Email);
    }
}