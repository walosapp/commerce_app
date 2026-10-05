using System.Data;
using System.Runtime.CompilerServices;
using Npgsql;
using NpgsqlTypes;
using Walos.Tests.Integration;

namespace Walos.Tests.Security;

public sealed class IntegrationTestSafetyTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("false")]
    [InlineData("1")]
    public void Writes_Require_Explicit_OptIn(string? allowWrites)
    {
        Assert.Throws<InvalidOperationException>(() =>
            IntegrationTestDatabaseSafety.RequireSafeTarget(
                "Host=localhost;Database=walos_test", allowWrites));
    }

    [Theory]
    [InlineData("localhost", "walos_test")]
    [InlineData("127.0.0.1", "walos_test_run_123")]
    [InlineData("::1", "walos_test_isolated")]
    public void Explicit_OptIn_Allows_Local_Test_Targets(string host, string database)
    {
        var connectionString = new NpgsqlConnectionStringBuilder
        {
            Host = host,
            Database = database,
        }.ConnectionString;

        IntegrationTestDatabaseSafety.RequireSafeTarget(connectionString, "true");
    }

    [Theory]
    [InlineData("Host=db.example.com;Database=walos_test")]
    [InlineData("Host=localhost,db.example.com;Database=walos_test")]
    [InlineData("Host=localhost;Database=postgres")]
    [InlineData("Host=localhost;Database=walos")]
    [InlineData("Host=localhost;Database=walos_test_production;Database=production")]
    [InlineData("Host=localhost;Database=walos_test_")]
    [InlineData("Host=localhost")]
    public void OptIn_Does_Not_Allow_Remote_Or_NonTest_Targets(string connectionString)
    {
        Assert.Throws<InvalidOperationException>(() =>
            IntegrationTestDatabaseSafety.RequireSafeTarget(connectionString, "true"));
    }

    [Fact]
    public void Invalid_Configuration_Does_Not_Expose_Credentials()
    {
        var error = Assert.Throws<InvalidOperationException>(() =>
            IntegrationTestDatabaseSafety.RequireSafeTarget(
                "UnknownOption=sensitive-value", "true"));

        Assert.DoesNotContain("sensitive-value", error.Message);
    }

    [Fact]
    public void Empty_Fixture_Does_Not_Create_A_Cleanup_Command()
    {
        var scope = new IntegrationTestDataScope();
        using var connection = new NpgsqlConnection();

        Assert.Null(scope.CreateCleanupCommand(connection));
        Assert.Equal(ConnectionState.Closed, connection.State);
    }

    [Fact]
    public void Cleanup_Parameters_Contain_Only_Companies_Owned_By_This_Fixture()
    {
        var owner = new IntegrationTestDataScope();
        var otherFixture = new IntegrationTestDataScope();
        owner.RegisterCreatedCompany(155);
        owner.RegisterCreatedCompany(900_001);
        owner.RegisterCreatedCompany(155);
        otherFixture.RegisterCreatedCompany(900_002);
        using var connection = new NpgsqlConnection();
        using var command = owner.CreateCleanupCommand(connection);

        Assert.NotNull(command);
        var parameter = command!.Parameters["companyIds"];
        Assert.Equal(NpgsqlDbType.Array | NpgsqlDbType.Bigint, parameter.NpgsqlDbType);
        var companyIds = Assert.IsType<long[]>(parameter.Value);
        Assert.Equal(new long[] { 155, 900_001 }, companyIds.OrderBy(id => id).ToArray());
        Assert.DoesNotContain(900_002L, companyIds);
        Assert.Equal(ConnectionState.Closed, connection.State);
        Assert.Throws<InvalidOperationException>(() => owner.EnsureOwnedCompany(900_002));

        owner.Clear();
        Assert.Null(owner.CreateCleanupCommand(connection));
        Assert.Equal(1, otherFixture.Count);
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    public void Invalid_Created_Company_Ids_Are_Not_Registered(long companyId)
    {
        var scope = new IntegrationTestDataScope();

        Assert.Throws<ArgumentOutOfRangeException>(() => scope.RegisterCreatedCompany(companyId));
        Assert.Equal(0, scope.Count);
    }

    [Fact]
    public void Every_Cleanup_Delete_Is_Restricted_To_Parameterized_Owned_Ids()
    {
        var statements = IntegrationTestDataScope.CleanupSql
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        Assert.NotEmpty(statements);
        Assert.All(statements, statement =>
        {
            Assert.Contains("DELETE FROM", statement);
            Assert.Contains("WHERE", statement);
            Assert.Contains("ANY(@companyIds)", statement);
            Assert.DoesNotContain("900000", statement);
        });
    }

    [Fact]
    public void Fixture_Guards_Before_Opening_And_Registers_Actual_Inserted_Ids()
    {
        var source = ReadFixture("IntegrationTestBase.cs");
        var guard = source.IndexOf("IntegrationTestDatabaseSafety.RequireSafeTarget", StringComparison.Ordinal);
        var connect = source.IndexOf("TestConnection(connectionString", StringComparison.Ordinal);

        Assert.True(guard >= 0 && guard < connect);
        Assert.Contains("_dataScope.RegisterCreatedCompany(companyId)", source);
        Assert.Contains("if (_dataScope.Count == 0)", source);
        Assert.DoesNotContain("ReservedCompanyIdFloor", source);
        Assert.DoesNotContain("Interlocked.Increment", source);
        Assert.DoesNotContain("ON CONFLICT (email)", source);

        Assert.DoesNotContain("override void Dispose", ReadFixture("V1IntegrationTestBase.cs"));
        Assert.DoesNotContain("override void Dispose", ReadFixture("PurchaseOrderRepositoryIntegrationTests.cs"));
    }

    private static string ReadFixture(string file, [CallerFilePath] string testSource = "")
        => File.ReadAllText(Path.Combine(Path.GetDirectoryName(testSource)!, "..", "Integration", file));
}
