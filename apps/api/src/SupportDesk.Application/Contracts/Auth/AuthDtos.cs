namespace SupportDesk.Application.Contracts.Auth;

/// <summary>Body of POST /api/auth/login.</summary>
public sealed record LoginRequest(string Email, string Password);

public sealed record CurrentUserDto(int Id, string FullName, string Email);

public sealed record LoginResponse(string Token, DateTime ExpiresAtUtc, CurrentUserDto User);