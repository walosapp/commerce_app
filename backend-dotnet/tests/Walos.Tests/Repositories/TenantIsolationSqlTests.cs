namespace Walos.Tests.Repositories;

public class TenantIsolationSqlTests
{
    private static string ReadRepositoryFile(string fileName)
    {
        var path = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "src", "Walos.Infrastructure", "Repositories", fileName));

        Assert.True(File.Exists(path), $"No se encontro el archivo esperado: {path}");
        return File.ReadAllText(path);
    }

    [Fact]
    public void InventoryRepository_Should_Join_SalesTables_And_OrderItems_By_CompanyId()
    {
        var source = ReadRepositoryFile("InventoryRepository.cs");

        Assert.Contains("INNER JOIN sales.tables t ON o.table_id = t.id AND t.company_id = o.company_id", source);
        Assert.Contains("INNER JOIN sales.order_items oi ON oi.order_id = o.id AND oi.company_id = o.company_id", source);
    }

    [Fact]
    public void SalesRepository_Should_Join_Products_And_Orders_By_CompanyId()
    {
        var source = ReadRepositoryFile("SalesRepository.cs");

        Assert.Contains("INNER JOIN sales.orders o ON oi.order_id = o.id AND o.company_id = oi.company_id", source);
        Assert.Contains("LEFT JOIN inventory.products p ON oi.product_id = p.id AND p.company_id = oi.company_id", source);
    }

    [Fact]
    public void FinanceRepository_Should_Join_Categories_And_Branches_By_CompanyId()
    {
        var source = ReadRepositoryFile("FinanceRepository.cs");

        Assert.Contains("INNER JOIN finance.categories c ON c.id = e.category_id AND c.company_id = e.company_id", source);
        Assert.Contains("LEFT JOIN core.branches b ON b.id = e.branch_id AND b.company_id = e.company_id", source);
    }

    [Fact]
    public void CompanyRepository_Should_Restrict_Updates_To_Requested_Company()
    {
        var source = ReadRepositoryFile("CompanyRepository.cs");

        Assert.Contains("WHERE id = @Id AND deleted_at IS NULL", source);
        Assert.Contains("WHERE id = @CompanyId AND deleted_at IS NULL", source);
    }
}
