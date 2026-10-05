using Npgsql;

namespace Walos.Tests.Integration;

/// <summary>
/// V1 fixtures share the base ownership-scoped company seeding and cleanup.
/// They retain a separate user helper for random test credentials.
/// </summary>
public abstract class V1IntegrationTestBase : IntegrationTestBase
{
    protected new async Task<long> SeedUserAsync(
        long companyId,
        long? branchId,
        string? email = null,
        string? password = null)
    {
        EnsureOwnedCompany(companyId);
        email ??= $"user-{Guid.NewGuid():N}@test.local";
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(
            password ?? $"V1-test-{Guid.NewGuid():N}");
        using var connection = await ConnectionFactory.CreateConnectionAsync();

        using var roleCommand = new NpgsqlCommand(@"
            INSERT INTO core.roles (company_id, code, name, is_active)
            VALUES (@companyId, 'manager', 'Manager', TRUE)
            ON CONFLICT (company_id, code) DO UPDATE SET name = 'Manager'
            RETURNING id", (NpgsqlConnection)connection);
        roleCommand.Parameters.AddWithValue("companyId", companyId);
        var roleId = (long)(await roleCommand.ExecuteScalarAsync()
            ?? throw new InvalidOperationException("Failed to seed V1 test role"));

        using var command = new NpgsqlCommand(@"
            INSERT INTO core.users
                (company_id, branch_id, role_id, first_name, last_name, email,
                 password_hash, is_active, email_verified, created_by)
            VALUES
                (@companyId, @branchId, @roleId, 'Test', 'User', @email,
                 @passwordHash, TRUE, TRUE, 1)
            RETURNING id", (NpgsqlConnection)connection);
        command.Parameters.AddWithValue("companyId", companyId);
        command.Parameters.AddWithValue("branchId", branchId ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("roleId", roleId);
        command.Parameters.AddWithValue("email", email);
        command.Parameters.AddWithValue("passwordHash", passwordHash);

        return (long)(await command.ExecuteScalarAsync()
            ?? throw new InvalidOperationException("Failed to seed V1 test user"));
    }

}
