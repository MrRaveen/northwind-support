using Microsoft.AspNetCore.Identity;
using SupportDesk.Application.Abstractions;

namespace SupportDesk.Infrastructure.Services;

/// <summary>
/// Password hashing with ASP.NET Core Identity's PasswordHasher (PBKDF2, random salt per hash,
/// iteration count stored inside the hash so it can be raised later).
/// </summary>
public sealed class IdentityCredentialHasher : ICredentialHasher
{
    // PasswordHasher<T> needs a user object but does not read it.
    private sealed class Subject;

    private static readonly Subject Anonymous = new();

    // Used when there is no account, so the work done is the same.
    private static readonly string DummyHash =
        new PasswordHasher<Subject>().HashPassword(Anonymous, "not-a-real-password");

    private readonly PasswordHasher<Subject> _hasher = new();

    public string Hash(string password) => _hasher.HashPassword(Anonymous, password);

    public bool Verify(string? passwordHash, string password)
    {
        var result = _hasher.VerifyHashedPassword(Anonymous, passwordHash ?? DummyHash, password);

        return passwordHash is not null && result != PasswordVerificationResult.Failed;
    }
}