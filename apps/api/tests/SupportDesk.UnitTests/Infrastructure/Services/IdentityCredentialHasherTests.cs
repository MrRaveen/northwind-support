using SupportDesk.Infrastructure.Services;

namespace SupportDesk.UnitTests.Infrastructure.Services;

public class IdentityCredentialHasherTests
{
    private readonly IdentityCredentialHasher _sut = new();

    [Fact]
    public void Hash_ProducesDistinctSaltsForIdenticalPasswords()
    {
        var password = "LocalDevOnly!123";

        var hash1 = _sut.Hash(password);
        var hash2 = _sut.Hash(password);

        Assert.NotEqual(hash1, hash2);
        Assert.True(_sut.Verify(hash1, password));
        Assert.True(_sut.Verify(hash2, password));
    }

    [Fact]
    public void Verify_WithInvalidPassword_ReturnsFalse()
    {
        var hash = _sut.Hash("CorrectPassword!1");

        var result = _sut.Verify(hash, "WrongPassword!1");

        Assert.False(result);
    }

    [Fact]
    public void Verify_WithNullHash_ReturnsFalseWithoutThrowing()
    {
        var result = _sut.Verify(null, "AnyPassword!1");

        Assert.False(result);
    }
}