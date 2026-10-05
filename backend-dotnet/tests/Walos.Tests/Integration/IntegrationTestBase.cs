using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Walos.Application.Services;
using Walos.Domain.Interfaces;
using Walos.Infrastructure.Data;
using Walos.Infrastructure.Inventory;
using Walos.Infrastructure.Repositories;

namespace Walos.Tests.Integration;

public abstract class IntegrationTestBase : IDisposable
{
    private readonly IntegrationTestDataScope _dataScope = new();

    protected readonly IDbConnectionFactory ConnectionFactory;
    protected readonly IAuthRepository AuthRepository;
    protected readonly ICompanyRepository CompanyRepository;
    protected readonly IInventoryRepository InventoryRepository;
    protected readonly ISalesRepository SalesRepository;
    protected readonly IFinanceRepository FinanceRepository;
    protected readonly IUsersRepository UsersRepository;
    protected readonly IDeliveryRepository DeliveryRepository;
    protected readonly ISuppliersRepository SuppliersRepository;
    protected readonly IPurchaseOrderRepository PurchaseOrderRepository;
    protected readonly ICreditRepository CreditRepository;
    protected readonly IRefundRepository RefundRepository;
    protected readonly ICashRegisterRepository CashRegisterRepository;
    protected readonly IOrderPaymentRepository OrderPaymentRepository;
    protected readonly ICheckoutRepository CheckoutRepository;
    protected readonly ICatalogRepository CatalogRepository;
    protected readonly IRecipeRepository RecipeRepository;

    protected IntegrationTestBase()
    {
        var config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.Test.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = config.GetConnectionString("TestConnection")
            ?? Environment.GetEnvironmentVariable("WALOS_TEST_CONNECTION");

        // Skip integration tests if no DB connection configured
        Skip.If(string.IsNullOrWhiteSpace(connectionString), 
            "Integration tests skipped: No test database connection configured. Set WALOS_TEST_CONNECTION env var or appsettings.Test.json");

        var allowWrites = Environment.GetEnvironmentVariable("WALOS_TEST_ALLOW_WRITES");
        Skip.If(!string.Equals(allowWrites, "true", StringComparison.OrdinalIgnoreCase),
            "Integration tests skipped: verify a disposable local database and explicitly set WALOS_TEST_ALLOW_WRITES=true.");
        IntegrationTestDatabaseSafety.RequireSafeTarget(connectionString!, allowWrites);

        // The safety guard must run before any connection is opened.
        TestConnection(connectionString!);

        ConnectionFactory = new TestConnectionFactory(connectionString);

        // Create repositories with null logger for integration tests
        AuthRepository = new AuthRepository(ConnectionFactory);
        CompanyRepository = new CompanyRepository(ConnectionFactory, NullLogger<CompanyRepository>.Instance);
        InventoryRepository = new InventoryRepository(ConnectionFactory, NullLogger<InventoryRepository>.Instance);
        SalesRepository = new SalesRepository(ConnectionFactory, NullLogger<SalesRepository>.Instance);
        FinanceRepository = new FinanceRepository(ConnectionFactory, NullLogger<FinanceRepository>.Instance);
        UsersRepository = new UsersRepository(ConnectionFactory, NullLogger<UsersRepository>.Instance);
        DeliveryRepository = new DeliveryRepository(ConnectionFactory, NullLogger<DeliveryRepository>.Instance);
        SuppliersRepository = new SuppliersRepository(ConnectionFactory, NullLogger<SuppliersRepository>.Instance);
        PurchaseOrderRepository = new PurchaseOrderRepository(ConnectionFactory);
        CreditRepository = new CreditRepository(ConnectionFactory, NullLogger<CreditRepository>.Instance);
        RefundRepository = new RefundRepository(ConnectionFactory, NullLogger<RefundRepository>.Instance);
        CashRegisterRepository = new CashRegisterRepository(ConnectionFactory, NullLogger<CashRegisterRepository>.Instance);
        OrderPaymentRepository = new OrderPaymentRepository(ConnectionFactory, NullLogger<OrderPaymentRepository>.Instance);
        CheckoutRepository = new CheckoutRepository(
            ConnectionFactory,
            NullLogger<CheckoutRepository>.Instance,
            new SaleInventoryPlanBuilder(),
            new InventoryTransactionWriter());
        CatalogRepository = new CatalogRepository(ConnectionFactory);
        RecipeRepository = new RecipeRepository(ConnectionFactory);
    }

    private static void TestConnection(string connectionString)
    {
        try
        {
            using var conn = new NpgsqlConnection(connectionString);
            conn.Open();
            using var cmd = new NpgsqlCommand("SELECT 1", conn);
            cmd.ExecuteScalar();
        }
        catch (NpgsqlException)
        {
            throw new InvalidOperationException("Cannot connect to the approved local test database. Check the test connection configuration.");
        }
    }

    protected async Task<long> SeedCompanyAsync(string name = "Test Company")
    {
        var suffix = Guid.NewGuid().ToString("N");
        using var conn = await ConnectionFactory.CreateConnectionAsync();
        using var cmd = new NpgsqlCommand(@"
            INSERT INTO core.companies (name, legal_name, tax_id, email, phone, is_active, created_by)
            VALUES (@name, @name, @taxId, @email, '123456', true, 1)
            RETURNING id", (NpgsqlConnection)conn);
        cmd.Parameters.AddWithValue("@name", name);
        cmd.Parameters.AddWithValue("@taxId", $"TEST-{suffix}");
        cmd.Parameters.AddWithValue("@email", $"company-{suffix}@test.local");
        var result = await cmd.ExecuteScalarAsync();
        var companyId = (long)(result ?? throw new InvalidOperationException("Failed to seed company"));
        _dataScope.RegisterCreatedCompany(companyId);
        return companyId;
    }

    protected void EnsureOwnedCompany(long companyId) => _dataScope.EnsureOwnedCompany(companyId);

    protected async Task<long> SeedBranchAsync(long companyId, string name = "Test Branch")
    {
        EnsureOwnedCompany(companyId);
        using var conn = await ConnectionFactory.CreateConnectionAsync();
        using var cmd = new NpgsqlCommand(@"
            INSERT INTO core.branches (
                company_id, name, code, branch_type, address, city, is_active, created_by
            )
            VALUES (
                @companyId, @name, @code, 'store', 'Test Address', 'Test City', true, 1
            )
            RETURNING id", (NpgsqlConnection)conn);
        cmd.Parameters.AddWithValue("@companyId", companyId);
        cmd.Parameters.AddWithValue("@name", name);
        cmd.Parameters.AddWithValue("@code", $"T{Guid.NewGuid():N}"[..20]);
        var result = await cmd.ExecuteScalarAsync();
        return (long)(result ?? throw new InvalidOperationException("Failed to seed branch"));
    }

    protected async Task<long> SeedUserAsync(long companyId, long? branchId, string? email = null, string password = "password123")
    {
        EnsureOwnedCompany(companyId);
        email ??= $"user-{companyId}-{Guid.NewGuid():N}@test.local";
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(password);
        using var conn = await ConnectionFactory.CreateConnectionAsync();

        // First ensure role exists
        using var roleCmd = new NpgsqlCommand(@"
            INSERT INTO core.roles (company_id, code, name, is_active)
            VALUES (@companyId, 'manager', 'Manager', true)
            ON CONFLICT (company_id, code) DO UPDATE SET name = 'Manager'
            RETURNING id", (NpgsqlConnection)conn);
        roleCmd.Parameters.AddWithValue("@companyId", companyId);
        var roleId = (long)(await roleCmd.ExecuteScalarAsync() ?? 1);

        using var cmd = new NpgsqlCommand(@"
            INSERT INTO core.users (company_id, branch_id, role_id, first_name, last_name, email, password_hash, is_active, email_verified, created_by)
            VALUES (@companyId, @branchId, @roleId, 'Test', 'User', @email, @passwordHash, true, true, 1)
            RETURNING id", (NpgsqlConnection)conn);
        cmd.Parameters.AddWithValue("@companyId", companyId);
        cmd.Parameters.AddWithValue("@branchId", branchId ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@roleId", roleId);
        cmd.Parameters.AddWithValue("@email", email);
        cmd.Parameters.AddWithValue("@passwordHash", passwordHash);
        var result = await cmd.ExecuteScalarAsync();
        return (long)(result ?? throw new InvalidOperationException("Failed to seed user"));
    }

    protected async Task<DateTime> GetDatabaseUtcNowAsync()
    {
        using var conn = await ConnectionFactory.CreateConnectionAsync();
        using var cmd = new NpgsqlCommand("SELECT CURRENT_TIMESTAMP", (NpgsqlConnection)conn);
        var result = await cmd.ExecuteScalarAsync();
        return ((DateTime)(result ?? throw new InvalidOperationException("Failed to read database clock"))).ToUniversalTime();
    }

    protected async Task CleanupAsync()
    {
        if (_dataScope.Count == 0)
            return;

        using var conn = await ConnectionFactory.CreateConnectionAsync();
        using var transaction = ((NpgsqlConnection)conn).BeginTransaction();
        using var cmd = _dataScope.CreateCleanupCommand((NpgsqlConnection)conn, transaction)
            ?? throw new InvalidOperationException("No fixture-owned companies to clean up.");
        await cmd.ExecuteNonQueryAsync();
        await transaction.CommitAsync();
        _dataScope.Clear();
    }

    public virtual void Dispose()
    {
        CleanupAsync().GetAwaiter().GetResult();
    }
}

public class TestConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;

    public TestConnectionFactory(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<System.Data.IDbConnection> CreateConnectionAsync()
    {
        var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        return connection;
    }
}
