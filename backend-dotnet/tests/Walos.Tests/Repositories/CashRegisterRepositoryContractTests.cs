using System.Runtime.CompilerServices;

namespace Walos.Tests.Repositories;

public class CashRegisterRepositoryContractTests
{
    [Fact]
    public void Active_Register_Lookup_Is_Scoped_By_Company_And_Branch_Not_Opening_User()
    {
        var source = ReadBackendSource(
            "src", "Walos.Infrastructure", "Repositories", "CashRegisterRepository.cs");
        var method = Slice(source, "public async Task<CashRegister?> GetActiveAsync", "public async Task<CashRegister?> GetByIdAsync");

        Assert.Contains("cr.company_id = @CompanyId", method);
        Assert.Contains("cr.branch_id = @BranchId", method);
        Assert.Contains("cr.status = 'open'", method);
        Assert.DoesNotContain("opened_by = @UserId", method);
    }

    [Fact]
    public void Opening_Is_Serialized_Per_Company_And_Branch_And_Rejects_Duplicates()
    {
        var source = ReadBackendSource(
            "src", "Walos.Infrastructure", "Repositories", "CashRegisterRepository.cs");
        var method = Slice(source, "public async Task<CashRegister> OpenAsync", "public async Task<CashRegister?> GetActiveAsync");

        Assert.Contains("pg_advisory_xact_lock", method);
        Assert.Contains("company_id = @CompanyId", method);
        Assert.Contains("branch_id = @BranchId", method);
        Assert.Contains("status = 'open'", method);
        Assert.Contains("cash_register_already_open", method);
    }

    [Fact]
    public void Restaurant_And_PosDeli_Sales_Use_Branch_Register_Without_Opening_User_Filter()
    {
        var checkout = ReadBackendSource(
            "src", "Walos.Infrastructure", "Repositories", "CheckoutRepository.cs");
        var lockMethod = Slice(checkout, "private static async Task<long?> LockActiveRegisterAsync", "private Task<IReadOnlyList<InventoryMovementPlan>>");
        var updateMethod = Slice(checkout, "private static async Task UpdateCashRegisterAsync", "private static async Task<CheckoutCreditRow?>");
        var posDeli = ReadBackendSource(
            "src", "Walos.API", "Controllers", "PosDeliController.cs");

        Assert.DoesNotContain("opened_by = @UserId", lockMethod);
        Assert.DoesNotContain("opened_by = @UserId", updateMethod);
        Assert.DoesNotContain("opened_by = @UserId", posDeli);
    }

    private static string ReadBackendSource(
        string first,
        params string[] parts)
    {
        var path = Path.Combine(new[] { BackendRoot(), first }.Concat(parts).ToArray());
        Assert.True(File.Exists(path), $"No se encontro el archivo esperado: {path}");
        return File.ReadAllText(path);
    }

    private static string BackendRoot([CallerFilePath] string testSourceFile = "")
    {
        var testSourceDirectory = Path.GetDirectoryName(testSourceFile)
            ?? throw new DirectoryNotFoundException(testSourceFile);
        return Path.GetFullPath(Path.Combine(testSourceDirectory, "..", "..", ".."));
    }

    private static string Slice(string source, string startMarker, string endMarker)
    {
        var start = source.IndexOf(startMarker, StringComparison.Ordinal);
        Assert.True(start >= 0, $"No se encontro: {startMarker}");
        var end = source.IndexOf(endMarker, start + startMarker.Length, StringComparison.Ordinal);
        Assert.True(end > start, $"No se encontro: {endMarker}");
        return source[start..end];
    }
}
