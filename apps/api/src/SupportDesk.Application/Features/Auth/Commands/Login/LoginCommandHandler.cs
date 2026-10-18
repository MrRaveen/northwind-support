using Microsoft.Extensions.Logging;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Auth;
using SupportDesk.Application.Exceptions;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.Application.Features.Auth.Commands.Login;

public sealed class LoginCommandHandler(
    IAgentRepository agents,
    ICredentialHasher hasher,
    ITokenIssuer tokens,
    ILogger<LoginCommandHandler> logger)
{
    /// <summary>One message for every failure, so an attacker learns nothing about which part was wrong.</summary>
    public const string InvalidCredentialsMessage = "Invalid email or password.";

    /// <exception cref="UnauthorizedException">Unknown email, wrong password or inactive agent.</exception>
    public async Task<LoginResponse> HandleAsync(LoginRequest request, CancellationToken ct)
    {
        var agent = await agents.GetByEmailAsync(request.Email.Trim(), ct);

        // Always verify, even when there is no agent, so the timing does not give the answer away.
        var passwordMatches = hasher.Verify(agent?.PasswordHash, request.Password);

        if (agent is null || !passwordMatches || !agent.IsActive)
        {
            logger.LogWarning("A sign-in attempt was rejected.");
            throw new UnauthorizedException(InvalidCredentialsMessage);
        }

        var issued = tokens.Issue(agent.Id, agent.FullName, agent.Email);

        logger.LogInformation("Agent {AgentId} signed in.", agent.Id);

        return new LoginResponse(
            issued.Token,
            issued.ExpiresAtUtc,
            new CurrentUserDto(agent.Id, agent.FullName, agent.Email));
    }
}