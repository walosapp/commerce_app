using Walos.Domain.Entities;

namespace Walos.Application.Services;

public interface IAuthService
{
    Task<LoginResult> LoginAsync(string username, string password, string? ipAddress);
    Task<TokenResult> RefreshTokenAsync(string refreshToken);
    Task LogoutAsync(long userId);
}

public class LoginResult
{
    public string Token { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public UserInfo User { get; set; } = null!;
}

public class TokenResult
{
    public string Token { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
}

public class UserInfo
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = "user";
    public string? RoleName { get; set; }
    public long CompanyId { get; set; }
    public string? CompanyName { get; set; }
    public long? BranchId { get; set; }
    public string? BranchName { get; set; }
    public string? Language { get; set; }
    public string? AvatarUrl { get; set; }
}
