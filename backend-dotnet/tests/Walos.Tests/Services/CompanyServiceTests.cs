using Microsoft.Extensions.Logging;
using Moq;
using Walos.Application.DTOs.Company;
using Walos.Application.Services;
using Walos.Application.Storage;
using Walos.Domain.Entities;
using Walos.Domain.Exceptions;
using Walos.Domain.Interfaces;

namespace Walos.Tests.Services;

public class CompanyServiceTests
{
    private readonly Mock<ICompanyRepository> _repoMock;
    private readonly Mock<ILogger<CompanyService>> _loggerMock;
    private readonly Mock<IFileStorage> _fileStorageMock;
    private readonly CompanyService _service;

    private const long CompanyId = 1;
    private const long UserId = 100;

    public CompanyServiceTests()
    {
        _repoMock = new Mock<ICompanyRepository>();
        _fileStorageMock = new Mock<IFileStorage>();
        _loggerMock = new Mock<ILogger<CompanyService>>();
        _service = new CompanyService(_repoMock.Object, _fileStorageMock.Object, _loggerMock.Object);
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

    [Fact]
    public async Task GetSettings_ResolvesManagedLogoReference_ToPublicUrl()
    {
        const string objectKey = "companies/1/branding/logo.png";
        const string publicUrl = "https://storage.example/public/logo.png";
        _repoMock.Setup(r => r.GetCompanySettingsAsync(CompanyId))
            .ReturnsAsync(new CompanySettings { Id = CompanyId, Name = "Walos", LogoUrl = objectKey });
        _fileStorageMock.Setup(s => s.IsManagedReference(objectKey)).Returns(true);
        _fileStorageMock.Setup(s => s.GetPublicUrl(objectKey)).Returns(publicUrl);

        var result = await _service.GetSettingsAsync(CompanyId);

        Assert.Equal(publicUrl, result.LogoUrl);
    }

    [Fact]
    public async Task GetSettings_PreservesManagedPublicUrl()
    {
        const string publicUrl = "https://storage.example/public/companies/1/branding/logo.png";
        _repoMock.Setup(r => r.GetCompanySettingsAsync(CompanyId))
            .ReturnsAsync(new CompanySettings { Id = CompanyId, Name = "Walos", LogoUrl = publicUrl });
        _fileStorageMock.Setup(s => s.IsManagedReference(publicUrl)).Returns(true);

        var result = await _service.GetSettingsAsync(CompanyId);

        Assert.Equal(publicUrl, result.LogoUrl);
        _fileStorageMock.Verify(s => s.GetPublicUrl(It.IsAny<string>()), Times.Never);
    }

    [Theory]
    [InlineData("/uploads/branding/company_1.png")]
    [InlineData("https://legacy.example/logo.png")]
    public async Task GetSettings_PreservesUnmanagedLogoReference(string reference)
    {
        _repoMock.Setup(r => r.GetCompanySettingsAsync(CompanyId))
            .ReturnsAsync(new CompanySettings { Id = CompanyId, Name = "Walos", LogoUrl = reference });
        _fileStorageMock.Setup(s => s.IsManagedReference(reference)).Returns(false);

        var result = await _service.GetSettingsAsync(CompanyId);

        Assert.Equal(reference, result.LogoUrl);
        _fileStorageMock.Verify(s => s.GetPublicUrl(It.IsAny<string>()), Times.Never);
        _fileStorageMock.Verify(s => s.OpenReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
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

    [Fact]
    public async Task UploadLogo_StoresCanonicalKey_WithTenantScopedCas_AndDeletesOldManagedObject()
    {
        const string oldKey = "companies/1/branding/old.png";
        const string newKey = "companies/1/branding/new.png";
        const string publicUrl = "https://storage.example/public/new.png";
        _repoMock.Setup(r => r.GetCompanySettingsAsync(CompanyId))
            .ReturnsAsync(new CompanySettings { Id = CompanyId, LogoUrl = oldKey });
        _fileStorageMock.Setup(s => s.UploadImageAsync(
                It.Is<ImageUploadRequest>(request =>
                    request.CompanyId == CompanyId &&
                    request.Scope == ImageStorageScope.Branding &&
                    request.ProductId == null),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StoredFile(newKey, publicUrl, "image/png", 100, 10, 10));
        _repoMock.Setup(r => r.CompareExchangeCompanyLogoAsync(CompanyId, oldKey, newKey, UserId))
            .ReturnsAsync(true);
        _fileStorageMock.Setup(s => s.IsManagedReference(oldKey)).Returns(true);
        _fileStorageMock.Setup(s => s.DeleteIfManagedAsync(oldKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _service.UploadLogoAsync(
            CompanyId, UserId, new MemoryStream([1, 2, 3]), "logo.png", "image/png");

        Assert.Equal(publicUrl, result);
        _repoMock.Verify(r => r.CompareExchangeCompanyLogoAsync(CompanyId, oldKey, newKey, UserId), Times.Once);
        _fileStorageMock.Verify(s => s.DeleteIfManagedAsync(oldKey, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UploadLogo_DeletesNewObject_WhenCasFails()
    {
        const string newKey = "companies/1/branding/new.png";
        _repoMock.Setup(r => r.GetCompanySettingsAsync(CompanyId))
            .ReturnsAsync(new CompanySettings { Id = CompanyId, LogoUrl = "/uploads/branding/old.png" });
        _fileStorageMock.Setup(s => s.UploadImageAsync(It.IsAny<ImageUploadRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StoredFile(newKey, "https://storage.example/new.png", "image/png", 100, 10, 10));
        _repoMock.Setup(r => r.CompareExchangeCompanyLogoAsync(
                CompanyId, "/uploads/branding/old.png", newKey, UserId))
            .ReturnsAsync(false);
        _fileStorageMock.Setup(s => s.IsManagedReference(newKey)).Returns(true);
        _fileStorageMock.Setup(s => s.DeleteIfManagedAsync(newKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await Assert.ThrowsAsync<BusinessException>(() => _service.UploadLogoAsync(
            CompanyId, UserId, new MemoryStream([1]), "logo.png", "image/png"));

        _fileStorageMock.Verify(s => s.DeleteIfManagedAsync(newKey, It.IsAny<CancellationToken>()), Times.Once);
        _fileStorageMock.Verify(s => s.DeleteIfManagedAsync("/uploads/branding/old.png", It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UploadLogo_CompensatesNewObject_WhenRepositoryThrows()
    {
        const string newKey = "companies/1/branding/new.png";
        _repoMock.Setup(r => r.GetCompanySettingsAsync(CompanyId))
            .ReturnsAsync(new CompanySettings { Id = CompanyId });
        _fileStorageMock.Setup(s => s.UploadImageAsync(It.IsAny<ImageUploadRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StoredFile(newKey, "https://storage.example/new.png", "image/png", 100, 10, 10));
        _repoMock.Setup(r => r.CompareExchangeCompanyLogoAsync(CompanyId, null, newKey, UserId))
            .ThrowsAsync(new InvalidOperationException("db failed"));
        _fileStorageMock.Setup(s => s.IsManagedReference(newKey)).Returns(true);
        _fileStorageMock.Setup(s => s.DeleteIfManagedAsync(newKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.UploadLogoAsync(
            CompanyId, UserId, new MemoryStream([1]), "logo.png", "image/png"));

        _fileStorageMock.Verify(s => s.DeleteIfManagedAsync(newKey, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RemoveLogo_CasToNull_ThenDeletesOldManagedObject()
    {
        const string oldKey = "companies/1/branding/old.png";
        _repoMock.Setup(r => r.GetCompanySettingsAsync(CompanyId))
            .ReturnsAsync(new CompanySettings { Id = CompanyId, LogoUrl = oldKey });
        _repoMock.Setup(r => r.CompareExchangeCompanyLogoAsync(CompanyId, oldKey, null, UserId))
            .ReturnsAsync(true);
        _fileStorageMock.Setup(s => s.IsManagedReference(oldKey)).Returns(true);
        _fileStorageMock.Setup(s => s.DeleteIfManagedAsync(oldKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await _service.RemoveLogoAsync(CompanyId, UserId);

        _repoMock.Verify(r => r.CompareExchangeCompanyLogoAsync(CompanyId, oldKey, null, UserId), Times.Once);
        _fileStorageMock.Verify(s => s.DeleteIfManagedAsync(oldKey, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RemoveLogo_DoesNotDeleteOldObject_WhenCasFails()
    {
        const string oldKey = "companies/1/branding/old.png";
        _repoMock.Setup(r => r.GetCompanySettingsAsync(CompanyId))
            .ReturnsAsync(new CompanySettings { Id = CompanyId, LogoUrl = oldKey });
        _repoMock.Setup(r => r.CompareExchangeCompanyLogoAsync(CompanyId, oldKey, null, UserId))
            .ReturnsAsync(false);

        await Assert.ThrowsAsync<BusinessException>(() => _service.RemoveLogoAsync(CompanyId, UserId));

        _fileStorageMock.Verify(s => s.DeleteIfManagedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
