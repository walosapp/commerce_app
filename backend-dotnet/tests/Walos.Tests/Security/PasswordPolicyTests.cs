using Walos.Application.Security;
using Walos.Domain.Exceptions;

namespace Walos.Tests.Security;

public sealed class PasswordPolicyTests
{
    [Theory]
    [InlineData("Short1!")]
    [InlineData("alllowercase1!")]
    [InlineData("ALLUPPERCASE1!")]
    [InlineData("NoNumber!")]
    [InlineData("NoSpecial1")]
    [InlineData("Has Space1!")]
    [InlineData("Has\tTab1!")]
    [InlineData("HasLine1!\n")]
    [InlineData("Ábcdef1!")]
    [InlineData("Abcdef1😀")]
    public void Rejects_Password_Outside_FrozenV1Policy(string password)
    {
        Assert.Throws<ValidationException>(() => PasswordPolicy.Validate(password));
    }

    [Fact]
    public void Accepts_EightCharacter_ComplexPassword()
    {
        PasswordPolicy.Validate("Valid1!x");
    }

    [Theory]
    [InlineData("Valid1!x")]
    [InlineData("A1!bcdef")]
    [InlineData("Zz9#1234")]
    public void Accepts_OnlyPrintableAsciiPasswordsMatchingEveryRequiredClass(string password)
    {
        PasswordPolicy.Validate(password);
    }
}
