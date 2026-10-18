using System.Globalization;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using SupportDesk.Application.Abstractions;

namespace SupportDesk.Presentation.Security;

public sealed class JwtTokenIssuer(IOptions<JwtOptions> options, IClock clock) : ITokenIssuer
{
    public IssuedToken Issue(int agentId, string fullName, string email)
    {
        var jwt = options.Value;
        var now = clock.UtcNow;
        var expiresAtUtc = now.AddMinutes(jwt.LifetimeMinutes);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = jwt.Issuer,
            Audience = jwt.Audience,
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(AuthClaims.Subject, agentId.ToString(CultureInfo.InvariantCulture)),
                new Claim(AuthClaims.Name, fullName),
                new Claim(AuthClaims.Email, email)
            }),
            NotBefore = now,
            Expires = expiresAtUtc,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
                SecurityAlgorithms.HmacSha256)
        };

        return new IssuedToken(new JsonWebTokenHandler().CreateToken(descriptor), expiresAtUtc);
    }
}