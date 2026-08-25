using Walos.Domain.Exceptions;
using Walos.Domain.Policies;

namespace Walos.Tests.Policies;

public class PaymentPolicyTests
{
    [Theory]
    [InlineData("cash", "cash")]
    [InlineData(" CARD ", "card")]
    [InlineData("TRANSFER", "transfer")]
    [InlineData("other", "other")]
    [InlineData("NeQuI", "nequi")]
    public void NormalizeMethod_Accepts_Current_Contract(string input, string expected)
    {
        Assert.Equal(expected, PaymentPolicy.NormalizeMethod(input));
    }

    [Fact]
    public void Canonical_Accounting_Methods_Do_Not_Treat_Nequi_As_Category()
    {
        Assert.Equal(["card", "cash", "other", "transfer"], PaymentPolicy.CanonicalMethods.Order());
        Assert.Equal("transfer", PaymentPolicy.ToAccountingMethod("nequi"));
    }

    [Fact]
    public void Unknown_Method_Is_Rejected_Instead_Of_Becoming_Other()
    {
        Assert.Throws<ValidationException>(() => PaymentPolicy.NormalizeMethod("crypto"));
    }

    [Theory]
    [InlineData(10.005, 10.01)]
    [InlineData(10.004, 10.00)]
    [InlineData(10.015, 10.02)]
    public void Money_Uses_Two_Decimals_Away_From_Zero(double input, double expected)
    {
        Assert.Equal((decimal)expected, PaymentPolicy.RoundMoney((decimal)input));
    }

    [Fact]
    public void Reconciliation_Accepts_Exactly_One_Cent_Difference()
    {
        PaymentPolicy.ValidatePaymentTotal(100m, [99.99m]);
    }

    [Fact]
    public void Reconciliation_Rejects_More_Than_One_Cent_Difference()
    {
        Assert.Throws<ValidationException>(() => PaymentPolicy.ValidatePaymentTotal(100m, [99.98m]));
    }

    [Fact]
    public void Amount_That_Rounds_To_Zero_Is_Rejected()
    {
        Assert.Throws<ValidationException>(() => PaymentPolicy.NormalizePositiveAmount(0.004m));
    }
}
