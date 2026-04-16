using Microsoft.Extensions.Logging;
using Moq;
using Walos.Application.DTOs.Company;
using Walos.Application.Services;
using Walos.Domain.Entities;
using Walos.Domain.Exceptions;
using Walos.Domain.Interfaces;

namespace Walos.Tests.Services;

public class CompanyServiceTests
{
    private readonly Mock<ICompanyRepository> _repoMock;
    private readonly Mock<ILogger<CompanyService>> _loggerMock;
    private readonly CompanyService _service;

    private const long CompanyId = 1;
    private const long UserId = 100;

    public CompanyServiceTests()
    {
        _repoMock = new Mock<ICompanyRepository>();
        _loggerMock = new Mock<ILogger<CompanyService>>();
        _service = new CompanyService(_repoMock.Object, _loggerMock.Object);
    }

    // ── GetSettingsAsync ──

    [Fact]
    public async Task GetSettings_ThrowsNotFound_WhenMissing()
    {
        _repoMock.Setup(r => r.GetCompanySettingsAsync(CompanyId)).ReturnsAsync((CompanySettings?)null);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.GetSettingsAsync(CompanyId));
    }

    [Fact]
    public async Task GetSettings_FallsBackToName_WhenDisplayNameEmpty()
    {
        _repoMock.Setup(r => r.GetCompanySettingsAsync(CompanyId))
            .ReturnsAsync(new CompanySettings { Id = CompanyId, Name = "Walos Corp", DisplayName = "" });

        var result = await _service.GetSettingsAsync(CompanyId);

        Assert.Equal("Walos Corp", result.DisplayName);
    }

    [Fact]
    public async Task GetSettings_UsesDisplayName_WhenPresent()
    {
        _repoMock.Setup(r => r.GetCompanySettingsAsync(CompanyId))
            .ReturnsAsync(new CompanySettings { Id = CompanyId, Name = "Walos Corp", DisplayName = "Mi Tienda" });

        var result = await _service.GetSettingsAsync(CompanyId);

        Assert.Equal("Mi Tienda", result.DisplayName);
    }

    // ── UpdateSettingsAsync ──

    [Fact]
    public async Task UpdateSettings_ThrowsNotFound_WhenMissing()
    {
        _repoMock.Setup(r => r.GetCompanySettingsAsync(CompanyId)).ReturnsAsync((CompanySettings?)null);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.UpdateSettingsAsync(CompanyId, UserId, new UpdateCompanySettingsRequest { DisplayName = "X", ThemePreference = "light" }));
    }

    [Fact]
    public async Task UpdateSettings_ThrowsValidation_WhenNameEmpty()
    {
        _repoMock.Setup(r => r.GetCompanySettingsAsync(CompanyId))
            .ReturnsAsync(new CompanySettings { Id = CompanyId });

        await Assert.ThrowsAsync<ValidationException>(() =>
            _service.UpdateSettingsAsync(CompanyId, UserId, new UpdateCompanySettingsRequest { DisplayName = "", ThemePreference = "light" }));
    }

    [Fact]
    public async Task UpdateSettings_ThrowsValidation_WhenInvalidTheme()
    {
        _repoMock.Setup(r => r.GetCompanySettingsAsync(CompanyId))
            .ReturnsAsync(new CompanySettings { Id = CompanyId });

        await Assert.ThrowsAsync<ValidationException>(() =>
            _service.UpdateSettingsAsync(CompanyId, UserId, new UpdateCompanySettingsRequest { DisplayName = "Test", ThemePreference = "rainbow" }));
    }

    [Fact]
    public async Task UpdateSettings_Success()
    {
        _repoMock.Setup(r => r.GetCompanySettingsAsync(CompanyId))
            .ReturnsAsync(new CompanySettings { Id = CompanyId, Name = "Old" });
        _repoMock.Setup(r => r.UpdateCompanySettingsAsync(It.IsAny<CompanySettings>()))
            .ReturnsAsync((CompanySettings s) => s);

        var result = await _service.UpdateSettingsAsync(CompanyId, UserId, new UpdateCompanySettingsRequest
        {
            DisplayName = "Mi Negocio",
            ThemePreference = "dark",
            Email = "test@example.com"
        });

        Assert.Equal("Mi Negocio", result.DisplayName);
        Assert.Equal("dark", result.ThemePreference);
        Assert.Equal("test@example.com", result.Email);
        Assert.Equal(UserId, result.UpdatedBy);
    }

    // ── UpdateOperationsSettingsAsync ──

    [Fact]
    public async Task UpdateOperations_ThrowsValidation_WhenPercentOutOfRange()
    {
        await Assert.ThrowsAsync<ValidationException>(() =>
            _service.UpdateOperationsSettingsAsync(CompanyId, new UpdateCompanyOperationsSettingsRequest { MaxDiscountPercent = 101 }));
    }

    [Fact]
    public async Task UpdateOperations_ThrowsValidation_WhenNegativeAmount()
    {
        await Assert.ThrowsAsync<ValidationException>(() =>
            _service.UpdateOperationsSettingsAsync(CompanyId, new UpdateCompanyOperationsSettingsRequest { MaxDiscountPercent = 10, MaxDiscountAmount = -5 }));
    }

    [Fact]
    public async Task UpdateOperations_ThrowsValidation_WhenThresholdExceedsMax()
    {
        await Assert.ThrowsAsync<ValidationException>(() =>
            _service.UpdateOperationsSettingsAsync(CompanyId, new UpdateCompanyOperationsSettingsRequest
            {
                MaxDiscountPercent = 10,
                MaxDiscountAmount = 100,
                DiscountOverrideThresholdPercent = 15
            }));
    }

    [Fact]
    public async Task UpdateOperations_Success()
    {
        _repoMock.Setup(r => r.UpdateCompanyOperationsSettingsAsync(It.IsAny<CompanyOperationsSettings>()))
            .ReturnsAsync((CompanyOperationsSettings s) => s);

        var result = await _service.UpdateOperationsSettingsAsync(CompanyId, new UpdateCompanyOperationsSettingsRequest
        {
            ManualDiscountEnabled = true,
            MaxDiscountPercent = 20,
            MaxDiscountAmount = 50000,
            DiscountRequiresOverride = true,
            DiscountOverrideThresholdPercent = 15
        });

        Assert.True(result.ManualDiscountEnabled);
        Assert.Equal(20, result.MaxDiscountPercent);
        Assert.True(result.DiscountRequiresOverride);
    }

    // ── UploadLogoAsync ──

    [Fact]
    public async Task UploadLogo_ThrowsValidation_WhenInvalidContentType()
    {
        await Assert.ThrowsAsync<ValidationException>(() =>
            _service.UploadLogoAsync(CompanyId, UserId, Stream.Null, "file.gif", "image/gif"));
    }

    [Fact]
    public async Task UploadLogo_ThrowsNotFound_WhenCompanyMissing()
    {
        _repoMock.Setup(r => r.GetCompanySettingsAsync(CompanyId)).ReturnsAsync((CompanySettings?)null);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.UploadLogoAsync(CompanyId, UserId, Stream.Null, "logo.png", "image/png"));
    }
}
