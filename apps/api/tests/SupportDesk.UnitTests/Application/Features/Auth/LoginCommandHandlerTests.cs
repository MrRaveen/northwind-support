using Microsoft.Extensions.Logging.Abstractions;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Auth;
using SupportDesk.Application.Exceptions;
using SupportDesk.Application.Features.Auth.Commands.Login;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Repositories;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Application.Features.Auth;

public class LoginCommandHandlerTests
{
    private readonly Mock<IAgentRepository> _agents = new();
    private readonly Mock<ICredentialHasher> _hasher = new();
    private readonly Mock<ITokenIssuer> _tokens = new();
    private readonly LoginCommandHandler _sut;

    public LoginCommandHandlerTests()
    {
        _sut = new LoginCommandHandler(
            _agents.Object,
            _hasher.Object,
            _tokens.Object,
            NullLogger<LoginCommandHandler>.Instance);
    }

    [Fact]
    public async Task HandleAsync_WithValidCredentials_ReturnsTokenAndUser()
    {
        var agent = new Agent("Alex Turner", "alex.turner@example.com", 10, FixedClock.DefaultNow);
        agent.SetPasswordHash("valid_hash");

        _agents.Setup(a => a.GetByEmailAsync("alex.turner@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);
        _hasher.Setup(h => h.Verify("valid_hash", "Password123!"))
            .Returns(true);
        _tokens.Setup(t => t.Issue(agent.Id, agent.FullName, agent.Email))
            .Returns(new IssuedToken("jwt_token_value", FixedClock.DefaultNow.AddHours(1)));

        var response = await _sut.HandleAsync(
            new LoginRequest("alex.turner@example.com", "Password123!"),
            CancellationToken.None);

        Assert.Equal("jwt_token_value", response.Token);
        Assert.Equal("Alex Turner", response.User.FullName);
        Assert.Equal("alex.turner@example.com", response.User.Email);
    }

    [Fact]
    public async Task HandleAsync_WithUnknownEmail_ThrowsUnauthorizedWithGenericMessage()
    {
        _agents.Setup(a => a.GetByEmailAsync("unknown@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Agent?)null);
        _hasher.Setup(h => h.Verify(null, "Password123!"))
            .Returns(false);

        var exception = await Assert.ThrowsAsync<UnauthorizedException>(() =>
            _sut.HandleAsync(new LoginRequest("unknown@example.com", "Password123!"), CancellationToken.None));

        Assert.Equal(LoginCommandHandler.InvalidCredentialsMessage, exception.Message);
        _tokens.Verify(t => t.Issue(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WithWrongPassword_ThrowsUnauthorizedWithGenericMessage()
    {
        var agent = new Agent("Alex Turner", "alex.turner@example.com", 10, FixedClock.DefaultNow);
        agent.SetPasswordHash("valid_hash");

        _agents.Setup(a => a.GetByEmailAsync(agent.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);
        _hasher.Setup(h => h.Verify("valid_hash", "WrongPassword!"))
            .Returns(false);

        var exception = await Assert.ThrowsAsync<UnauthorizedException>(() =>
            _sut.HandleAsync(new LoginRequest(agent.Email, "WrongPassword!"), CancellationToken.None));

        Assert.Equal(LoginCommandHandler.InvalidCredentialsMessage, exception.Message);
        _tokens.Verify(t => t.Issue(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WithInactiveAgent_ThrowsUnauthorizedWithGenericMessage()
    {
        var agent = new Agent("Yuki Tanaka", "yuki.tanaka@example.com", 8, FixedClock.DefaultNow);
        agent.SetPasswordHash("valid_hash");
        agent.Deactivate();

        _agents.Setup(a => a.GetByEmailAsync(agent.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);
        _hasher.Setup(h => h.Verify("valid_hash", "Password123!"))
            .Returns(true);

        var exception = await Assert.ThrowsAsync<UnauthorizedException>(() =>
            _sut.HandleAsync(new LoginRequest(agent.Email, "Password123!"), CancellationToken.None));

        Assert.Equal(LoginCommandHandler.InvalidCredentialsMessage, exception.Message);
        _tokens.Verify(t => t.Issue(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }
}