using System.Security.Claims;

namespace Walos.Application.Services;

public interface IAccessTokenValidationService
{
    Task<bool> ValidateAsync(ClaimsPrincipal principal);
}
