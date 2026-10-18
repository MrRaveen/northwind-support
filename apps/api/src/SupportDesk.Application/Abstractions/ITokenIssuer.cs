namespace SupportDesk.Application.Abstractions;

public sealed record IssuedToken(string Token, DateTime ExpiresAtUtc);

public interface ITokenIssuer
{
    IssuedToken Issue(int agentId, string fullName, string email);
}