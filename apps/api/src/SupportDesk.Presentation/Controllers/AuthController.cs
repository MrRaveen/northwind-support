using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SupportDesk.Application.Contracts.Auth;
using SupportDesk.Application.Features.Auth.Commands.Login;
using SupportDesk.Application.Features.Auth.Queries.GetCurrentUser;
using SupportDesk.Presentation.Security;

namespace SupportDesk.Presentation.Controllers;

[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public sealed class AuthController : ControllerBase
{
    /// <summary>Signs an agent in. Open to everyone; every other endpoint needs the token.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public Task<LoginResponse> Login(
        LoginRequest request,
        [FromServices] LoginCommandHandler handler,
        CancellationToken ct) =>
        handler.HandleAsync(request, ct);

    /// <summary>The signed-in agent.</summary>
    [HttpGet("me")]
    public Task<CurrentUserDto> Me(
        [FromServices] GetCurrentUserQueryHandler handler,
        CancellationToken ct) =>
        handler.HandleAsync(User.GetAgentId(), ct);
}