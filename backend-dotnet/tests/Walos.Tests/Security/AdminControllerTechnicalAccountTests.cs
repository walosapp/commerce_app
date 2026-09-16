using Microsoft.AspNetCore.Mvc;
using Moq;
using System.Runtime.CompilerServices;
using Walos.API.Controllers;
using Walos.Application.DTOs.Common;
using Walos.Application.Security;
using Walos.Application.Services;
using Walos.Domain.Entities;
using Walos.API.Services;
using Microsoft.Extensions.Logging;

namespace Walos.Tests.Security;

public sealed class AdminControllerTechnicalAccountTests
{
    private readonly Mock<IAdminService> _adminService = new(MockBehavior.Strict);
    private readonly Mock<IUsersRepository> _usersRepository = new(MockBehavior.Strict);
    private readonly Mock<ILogger<AdminController>> _logger = new();

    [Fact]
    public async Task SetUserStatus_RejectsDevTechnicalAccountWithoutMutation()
    {
        _usersRepository.Setup(repository => repository.GetByIdAsync(7, 1))
            .ReturnsAsync(DevUser());
        var controller = CreateController();

        var result = await controller.SetUserStatus(7, 1, false);

        var forbidden = Assert.IsType<ObjectResult>(result);
        Assert.Equal(403, forbidden.StatusCode);
        var response = Assert.IsType<ApiResponse>(forbidden.Value);
        Assert.Equal("protected_technical_account", response.Code);
        _usersRepository.Verify(repository => repository.SetActiveAsync(
            It.IsAny<long>(), It.IsAny<long>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public async Task ResetUserPassword_RejectsDevTechnicalAccountWithoutMutation()
    {
        _usersRepository.Setup(repository => repository.GetByIdAsync(7, 1))
            .ReturnsAsync(DevUser());
        var controller = CreateController();

        var result = await controller.ResetUserPassword(
            7, 1, new AdminController.ResetPasswordRequest("new-password-123"));

        var forbidden = Assert.IsType<ObjectResult>(result);
        Assert.Equal(403, forbidden.StatusCode);
        var response = Assert.IsType<ApiResponse>(forbidden.Value);
        Assert.Equal("protected_technical_account", response.Code);
        _usersRepository.Verify(repository => repository.ResetPasswordByPlatformActorAsync(
            It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ResetUserPassword_RejectsSelfWithoutRepositoryAccess()
    {
        var controller = CreateController();

        var result = await controller.ResetUserPassword(
            99, 1, new AdminController.ResetPasswordRequest("Changed2@"));

        Assert.IsType<BadRequestObjectResult>(result);
        _usersRepository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ResetTenantPassword_PassesAuthenticatedActorForBoundedAudit()
    {
        _adminService.Setup(service => service.ResetTenantAdminPasswordAsync(7, 99, "Changed2@"))
            .ReturnsAsync(123);
        var controller = CreateController();

        var result = await controller.ResetAdminPassword(
            7, new AdminController.ResetPasswordRequest("Changed2@"));

        Assert.IsType<OkObjectResult>(result);
        _adminService.VerifyAll();
    }

    [Fact]
    public async Task ResetUserPassword_LogsActorTargetAndTenantAfterAtomicSuccess()
    {
        _usersRepository.Setup(repository => repository.GetByIdAsync(7, 1))
            .ReturnsAsync(new User { Id = 7, CompanyId = 1, RoleCode = WalosRoles.Cashier });
        _usersRepository.Setup(repository => repository.ResetPasswordByPlatformActorAsync(
                99, 7, 1, It.IsAny<string>()))
            .ReturnsAsync(true);
        var controller = CreateController();

        var result = await controller.ResetUserPassword(
            7, 1, new AdminController.ResetPasswordRequest("Changed2@"));

        Assert.IsType<OkObjectResult>(result);
        var logText = string.Join(' ', _logger.Invocations
            .SelectMany(invocation => invocation.Arguments)
            .Select(argument => argument?.ToString()));
        Assert.Contains("99", logText, StringComparison.Ordinal);
        Assert.Contains("7", logText, StringComparison.Ordinal);
        Assert.Contains("1", logText, StringComparison.Ordinal);
        Assert.DoesNotContain("Changed2@", logText, StringComparison.Ordinal);
    }

    [Fact]
    public void RepositoryMutations_DefendDevAccountAtTheSqlBoundary()
    {
        var source = File.ReadAllText(GetRepositoryPath(
            "src", "Walos.Infrastructure", "Repositories", "UsersRepository.cs"));

        Assert.True(CountOccurrences(source, "r.code <> 'dev'") >= 2,
            "Create and update writes must reject assignment of the dev role in SQL.");
        Assert.True(CountOccurrences(source, "r.code = 'dev'") >= 3,
            "Update, status and soft-delete writes must reject an existing dev account in SQL.");
        Assert.True(CountOccurrences(source, "target_role.code <> 'dev'") >= 2,
            "Both tenant and platform password reset writes must reject an existing dev account in SQL.");
    }

    private AdminController CreateController() =>
        new(
            _adminService.Object,
            _usersRepository.Object,
            new TenantContext
            {
                IsAuthenticated = true,
                UserId = 99,
                CompanyId = 1,
                Role = WalosRoles.PlatformAdmin
            },
            _logger.Object);

    private static User DevUser() => new()
    {
        Id = 7,
        CompanyId = 1,
        RoleCode = WalosRoles.Dev,
        Email = "technical@example.invalid"
    };

    private static string GetRepositoryPath(
        string part1,
        string part2,
        string part3,
        string part4,
        [CallerFilePath] string testSourceFile = "")
    {
        var testSourceDirectory = Path.GetDirectoryName(testSourceFile)
            ?? throw new DirectoryNotFoundException("No se pudo resolver el directorio de tests.");
        var backendRoot = Path.GetFullPath(Path.Combine(testSourceDirectory, "..", "..", ".."));
        return Path.Combine(backendRoot, part1, part2, part3, part4);
    }

    private static int CountOccurrences(string source, string value) =>
        (source.Length - source.Replace(value, string.Empty, StringComparison.Ordinal).Length) / value.Length;
}
