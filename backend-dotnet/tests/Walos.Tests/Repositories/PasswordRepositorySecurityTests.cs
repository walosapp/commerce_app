using System.Runtime.CompilerServices;

namespace Walos.Tests.Repositories;

public sealed class PasswordRepositorySecurityTests
{
    [Fact]
    public void SelfChange_IsTenantScoped_And_RevokesRefreshTokenInSameUpdate()
    {
        var source = File.ReadAllText(GetRepositoryPath("AuthRepository.cs"));
        var method = SliceMethod(source, "ChangePasswordAndRotateRefreshTokenAsync");

        Assert.Contains("company_id = @CompanyId", method, StringComparison.Ordinal);
        Assert.Contains("id = @UserId", method, StringComparison.Ordinal);
        Assert.Contains("password_hash = @ExpectedPasswordHash", method, StringComparison.Ordinal);
        Assert.Contains("refresh_token = @NewRefreshToken", method, StringComparison.Ordinal);
        Assert.Contains("refresh_token_expires_at = @RefreshTokenExpiresAt", method, StringComparison.Ordinal);
    }

    [Fact]
    public void RefreshRotation_IsCompareAndSwap_OnExpectedToken()
    {
        var source = File.ReadAllText(GetRepositoryPath("AuthRepository.cs"));
        var method = SliceMethod(source, "RotateRefreshTokenAsync");

        Assert.Contains("refresh_token = @ExpectedRefreshToken", method, StringComparison.Ordinal);
        Assert.Contains("refresh_token = @NewRefreshToken", method, StringComparison.Ordinal);
        Assert.Contains("is_active = TRUE", method, StringComparison.Ordinal);
        Assert.Contains("deleted_at IS NULL", method, StringComparison.Ordinal);
    }

    [Fact]
    public void LoginRefreshPersistence_IsCompareAndSwap_OnVerifiedPasswordHash()
    {
        var source = File.ReadAllText(GetRepositoryPath("AuthRepository.cs"));
        var method = SliceMethod(source, "SaveRefreshTokenAfterPasswordVerificationAsync");

        Assert.Contains("password_hash = @ExpectedPasswordHash", method, StringComparison.Ordinal);
        Assert.Contains("is_active = TRUE", method, StringComparison.Ordinal);
        Assert.Contains("deleted_at IS NULL", method, StringComparison.Ordinal);
    }

    [Fact]
    public void TenantAdminReset_IsTenantScoped_RevokesRefreshToken_AndProtectsDev()
    {
        var source = File.ReadAllText(GetRepositoryPath("UsersRepository.cs"));
        var method = SliceMethod(source, "ResetPasswordByTenantActorAsync");

        Assert.Contains("target.company_id = @CompanyId", method, StringComparison.Ordinal);
        Assert.Contains("actor.company_id = @CompanyId", method, StringComparison.Ordinal);
        Assert.Contains("actor_role.code = @ActorRole", method, StringComparison.Ordinal);
        Assert.Contains("target_role.code = 'super_admin'", method, StringComparison.Ordinal);
        Assert.Contains("refresh_token = NULL", method, StringComparison.Ordinal);
        Assert.Contains("refresh_token_expires_at = NULL", method, StringComparison.Ordinal);
        Assert.Contains("target_role.code <> 'dev'", method, StringComparison.Ordinal);
    }

    [Fact]
    public void PlatformTenantReset_IsSingleTarget_NoSelf_AndReturnsAuditableTargetId()
    {
        var source = File.ReadAllText(GetRepositoryPath("AdminRepository.cs"));
        var method = SliceMethod(source, "ResetTenantAdminPasswordAsync");

        Assert.Contains("u.id <> @ActorUserId", method, StringComparison.Ordinal);
        Assert.Contains("HAVING COUNT(*) = 1", method, StringComparison.Ordinal);
        Assert.Contains("RETURNING u.id", method, StringComparison.Ordinal);
        Assert.Contains("refresh_token = NULL", method, StringComparison.Ordinal);
    }

    private static string SliceMethod(string source, string methodName)
    {
        var start = source.IndexOf(methodName, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Method {methodName} not found");
        var nextMethod = source.IndexOf("\n    public async Task", start + methodName.Length, StringComparison.Ordinal);
        return nextMethod < 0 ? source[start..] : source[start..nextMethod];
    }

    private static string GetRepositoryPath(
        string fileName,
        [CallerFilePath] string testSourceFile = "")
    {
        var testSourceDirectory = Path.GetDirectoryName(testSourceFile)
            ?? throw new DirectoryNotFoundException("Could not resolve test source directory");
        var backendRoot = Path.GetFullPath(Path.Combine(testSourceDirectory, "..", "..", ".."));
        return Path.Combine(backendRoot, "src", "Walos.Infrastructure", "Repositories", fileName);
    }
}
