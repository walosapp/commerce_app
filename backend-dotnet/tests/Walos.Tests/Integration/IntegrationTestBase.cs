using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Walos.Domain.Interfaces;
using Walos.Infrastructure.Data;
using Walos.Infrastructure.Repositories;

namespace Walos.Tests.Integration;

public abstract class IntegrationTestBase : IDisposable
{
    protected readonly IDbConnectionFactory ConnectionFactory;
    protected readonly IAuthRepository AuthRepository;
    protected readonly ICompanyRepository CompanyRepository;
    protected readonly IInventoryRepository InventoryRepository;
    protected readonly ISalesRepository SalesRepository;
    protected readonly IFinanceRepository FinanceRepository;

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

        // Verify connection works before proceeding
        TestConnection(connectionString);

        ConnectionFactory = new TestConnectionFactory(connectionString);

        // Create repositories with null logger for integration tests
        AuthRepository = new AuthRepository(ConnectionFactory);
        CompanyRepository = new CompanyRepository(ConnectionFactory, NullLogger<CompanyRepository>.Instance);
        InventoryRepository = new InventoryRepository(ConnectionFactory, NullLogger<InventoryRepository>.Instance);
        SalesRepository = new SalesRepository(ConnectionFactory, NullLogger<SalesRepository>.Instance);
        FinanceRepository = new FinanceRepository(ConnectionFactory, NullLogger<FinanceRepository>.Instance);
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
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Cannot connect to test database. Ensure PostgreSQL is running and WALOS_TEST_CONNECTION is set. Error: {ex.Message}");
        }
    }

    protected async Task<long> SeedCompanyAsync(string name = "Test Company")
    {
        using var conn = await ConnectionFactory.CreateConnectionAsync();
        using var cmd = new NpgsqlCommand(@"
            INSERT INTO core.companies (name, email, phone, is_active, created_by)
            VALUES (@name, 'test@test.com', '123456', true, 1)
            RETURNING id", (NpgsqlConnection)conn);
        cmd.Parameters.AddWithValue("@name", name);
        var result = await cmd.ExecuteScalarAsync();
        return (long)(result ?? throw new InvalidOperationException("Failed to seed company"));
    }

    protected async Task<long> SeedBranchAsync(long companyId, string name = "Test Branch")
    {
        using var conn = await ConnectionFactory.CreateConnectionAsync();
        using var cmd = new NpgsqlCommand(@"
            INSERT INTO core.branches (company_id, name, address, is_active, created_by)
            VALUES (@companyId, @name, 'Test Address', true, 1)
            RETURNING id", (NpgsqlConnection)conn);
        cmd.Parameters.AddWithValue("@companyId", companyId);
        cmd.Parameters.AddWithValue("@name", name);
        var result = await cmd.ExecuteScalarAsync();
        return (long)(result ?? throw new InvalidOperationException("Failed to seed branch"));
    }

    protected async Task<long> SeedUserAsync(long companyId, long? branchId, string email = "user@test.com", string password = "password123")
    {
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(password);
        using var conn = await ConnectionFactory.CreateConnectionAsync();

        // First ensure role exists
        using var roleCmd = new NpgsqlCommand(@"
            INSERT INTO core.roles (company_id, code, name, is_active)
            VALUES (@companyId, 'admin', 'Administrator', true)
            ON CONFLICT (company_id, code) DO UPDATE SET name = 'Administrator'
            RETURNING id", (NpgsqlConnection)conn);
        roleCmd.Parameters.AddWithValue("@companyId", companyId);
        var roleId = (long)(await roleCmd.ExecuteScalarAsync() ?? 1);

        using var cmd = new NpgsqlCommand(@"
            INSERT INTO core.users (company_id, branch_id, role_id, first_name, last_name, email, password_hash, is_active, email_verified, created_by)
            VALUES (@companyId, @branchId, @roleId, 'Test', 'User', @email, @passwordHash, true, true, 1)
            ON CONFLICT (email) DO UPDATE SET password_hash = @passwordHash
            RETURNING id", (NpgsqlConnection)conn);
        cmd.Parameters.AddWithValue("@companyId", companyId);
        cmd.Parameters.AddWithValue("@branchId", branchId ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@roleId", roleId);
        cmd.Parameters.AddWithValue("@email", email);
        cmd.Parameters.AddWithValue("@passwordHash", passwordHash);
        var result = await cmd.ExecuteScalarAsync();
        return (long)(result ?? throw new InvalidOperationException("Failed to seed user"));
    }

    protected async Task CleanupAsync()
    {
        using var conn = await ConnectionFactory.CreateConnectionAsync();
        using var cmd = new NpgsqlCommand(@"
            -- Clean up in reverse dependency order
            DELETE FROM inventory.movements WHERE company_id > 900000;
            DELETE FROM inventory.stock WHERE company_id > 900000;
            DELETE FROM inventory.products WHERE company_id > 900000;
            DELETE FROM inventory.categories WHERE company_id > 900000;
            DELETE FROM inventory.units WHERE company_id > 900000;
            DELETE FROM finance.entries WHERE company_id > 900000;
            DELETE FROM finance.categories WHERE company_id > 900000;
            DELETE FROM sales.order_items WHERE company_id > 900000;
            DELETE FROM sales.orders WHERE company_id > 900000;
            DELETE FROM sales.tables WHERE company_id > 900000;
            DELETE FROM core.users WHERE company_id > 900000;
            DELETE FROM core.branches WHERE company_id > 900000;
            DELETE FROM core.roles WHERE company_id > 900000;
            DELETE FROM core.companies WHERE id > 900000;",
            (NpgsqlConnection)conn);
        await cmd.ExecuteNonQueryAsync();
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
