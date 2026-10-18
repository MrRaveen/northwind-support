using System.Globalization;
using System.Security.Claims;

namespace SupportDesk.Presentation.Security;

public static class AgentClaimsExtensions
{
    public static int GetAgentId(this ClaimsPrincipal user) =>
        int.Parse(
            user.FindFirst(AuthClaims.Subject)?.Value
                ?? throw new InvalidOperationException("The token has no subject claim."),
            CultureInfo.InvariantCulture);

    public static string GetAgentName(this ClaimsPrincipal user) =>
        user.FindFirst(AuthClaims.Name)?.Value
            ?? throw new InvalidOperationException("The token has no name claim.");
}