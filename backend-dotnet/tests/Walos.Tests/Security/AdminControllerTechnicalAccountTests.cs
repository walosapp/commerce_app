using Microsoft.AspNetCore.Mvc;
using Moq;
using System.Runtime.CompilerServices;
using Walos.API.Controllers;
using Walos.Application.DTOs.Common;
using Walos.Application.Security;
using Walos.Application.Services;
using Walos.Domain.Entities;

namespace Walos.Tests.Security;

public sealed class AdminControllerTechnicalAccountTests
{
    private readonly Mock<IAdminService> _adminService = new(MockBehavior.Strict);
    private readonly Mock<IUsersRepository> _usersRepository = new(MockBehavior.Strict);

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
        _usersRepository.Verify(repository => repository.ResetPasswordAsync(
            It.IsAny<long>(), It.IsAny<long>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void RepositoryMutations_DefendDevAccountAtTheSqlBoundary()
    {
        var source = File.ReadAllText(GetRepositoryPath(
            "src", "Walos.Infrastructure", "Repositories", "UsersRepository.cs"));

        Assert.True(CountOccurrences(source, "r.code <> 'dev'") >= 2,
            "Create and update writes must reject assignment of the dev role in SQL.");
        Assert.True(CountOccurrences(source, "r.code = 'dev'") >= 4,
            "Update, status, password and soft-delete writes must reject an existing dev account in SQL.");
    }

    private AdminController CreateController() =>
        new(_adminService.Object, _usersRepository.Object);

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
