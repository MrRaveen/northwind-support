namespace SupportDesk.Application.Abstractions;

public interface ICredentialHasher
{
    string Hash(string password);

    /// <summary>
    /// Checks a password. A null hash (no such account) still does the full hashing work and
    /// returns false, so a missing account takes as long as a wrong password.
    /// </summary>
    bool Verify(string? passwordHash, string password);
}