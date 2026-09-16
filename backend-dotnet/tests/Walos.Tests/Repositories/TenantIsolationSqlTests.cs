using System.Runtime.CompilerServices;

namespace Walos.Tests.Repositories;

public class TenantIsolationSqlTests
{
    private static string ReadRepositoryFile(
        string fileName,
        [CallerFilePath] string testSourceFile = "")
    {
        var testSourceDirectory = Path.GetDirectoryName(testSourceFile)
            ?? throw new DirectoryNotFoundException(
                $"No se pudo resolver el directorio fuente de {testSourceFile}.");
        var backendRoot = Path.GetFullPath(Path.Combine(
            testSourceDirectory, "..", "..", ".."));
        var path = Path.Combine(
            backendRoot, "src", "Walos.Infrastructure", "Repositories", fileName);

        Assert.True(File.Exists(path), $"No se encontro el archivo esperado: {path}");
        return File.ReadAllText(path);
    }

    [Fact]
    public void InventoryRepository_Should_Join_SalesTables_And_OrderItems_By_CompanyId()
    {
        var source = ReadRepositoryFile(Path.Combine("..", "Inventory", "CommittedInventorySql.cs"));

        Assert.Contains("JOIN sales.tables t", source);
        Assert.Contains("t.id = o.table_id AND t.company_id = o.company_id", source);
        Assert.Contains("JOIN sales.order_items oi", source);
        Assert.Contains("oi.order_id = o.id AND oi.company_id = o.company_id", source);
    }

    [Fact]
    public void SaleCatalogRecipeValidity_Is_TenantScoped_And_Fails_Closed()
    {
        var source = ReadRepositoryFile("InventoryRepository.cs");

        Assert.Contains("recipe.company_id = p.company_id", source);
        Assert.Contains("ingredient.company_id = recipe.company_id", source);
        Assert.Contains("ingredient.is_active = FALSE", source);
        Assert.Contains("ingredient.deleted_at IS NOT NULL", source);
        Assert.Contains("END AS HasValidRecipe", source);
    }

    [Fact]
    public void SalesRepository_Should_Join_Products_And_Orders_By_CompanyId()
    {
        var source = ReadRepositoryFile("SalesRepository.cs");

        Assert.Contains("INNER JOIN sales.orders o ON oi.order_id = o.id AND o.company_id = oi.company_id", source);
        Assert.Contains("LEFT JOIN inventory.products p ON oi.product_id = p.id AND p.company_id = oi.company_id", source);
    }

    [Fact]
    public void SalesRepository_CancelActiveTable_Is_Atomic_And_Requires_Open_Pending_Sale()
    {
        var source = ReadRepositoryFile("SalesRepository.cs");

        Assert.Contains("public async Task<bool> CancelActiveTableAsync", source);
        Assert.Contains("t.status = 'open'", source);
        Assert.Contains("t.deleted_at IS NULL", source);
        Assert.Contains("o.status = 'pending'", source);
        Assert.Contains("o.deleted_at IS NULL", source);
        Assert.Contains("FOR UPDATE OF t, o", source);
        Assert.Contains("cancelled_orders AS", source);
        Assert.Contains("cancelled_table AS", source);
        Assert.Contains("SELECT EXISTS (SELECT 1 FROM cancelled_table)", source);
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
