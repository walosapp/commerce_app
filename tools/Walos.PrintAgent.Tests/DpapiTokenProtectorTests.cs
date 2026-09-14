using Walos.PrintAgent.Security;

namespace Walos.PrintAgent.Tests;

public sealed class DpapiTokenProtectorTests
{
    [Fact]
    public void Protect_UsesNonPlaintextCurrentUserCiphertext()
    {
        const string token = "token-local-super-secreto";
        var protector = new DpapiTokenProtector();

        var protectedToken = protector.Protect(token);

        Assert.NotEqual(token, protectedToken);
        Assert.DoesNotContain(token, protectedToken, StringComparison.Ordinal);
        Assert.Equal(token, protector.Unprotect(protectedToken));
    }
}
