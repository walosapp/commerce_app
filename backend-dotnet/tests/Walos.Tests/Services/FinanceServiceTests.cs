using Microsoft.Extensions.Logging;
using Moq;
using Walos.Application.DTOs.Finance;
using Walos.Application.Services;
using Walos.Domain.Entities;
using Walos.Domain.Exceptions;
using Walos.Domain.Interfaces;

namespace Walos.Tests.Services;

public class FinanceServiceTests
{
    private readonly Mock<IFinanceRepository> _repoMock;
    private readonly Mock<ILogger<FinanceService>> _loggerMock;
    private readonly FinanceService _service;

    private const long CompanyId = 1;
    private const long UserId = 100;
    private const long BranchId = 10;

    public FinanceServiceTests()
    {
        _repoMock = new Mock<IFinanceRepository>();
        _loggerMock = new Mock<ILogger<FinanceService>>();
        _service = new FinanceService(_repoMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task ResolveBranch_Rejects_Body_Override_Of_Authenticated_Branch()
    {
        await Assert.ThrowsAsync<ValidationException>(() =>
            _service.ResolveBranchAsync(CompanyId, BranchId, BranchId + 1));

        _repoMock.Verify(r => r.IsActiveBranchInCompanyAsync(It.IsAny<long>(), It.IsAny<long>()), Times.Never);
    }

    [Fact]
    public async Task ResolveBranch_Rejects_Foreign_Branch_For_CompanyWide_User()
    {
        _repoMock.Setup(r => r.IsActiveBranchInCompanyAsync(99, CompanyId)).ReturnsAsync(false);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.ResolveBranchAsync(CompanyId, null, 99));
    }

    [Fact]
    public async Task ResolveBranch_Allows_Active_Tenant_Branch_For_CompanyWide_User()
    {
        _repoMock.Setup(r => r.IsActiveBranchInCompanyAsync(BranchId, CompanyId)).ReturnsAsync(true);

        var result = await _service.ResolveBranchAsync(CompanyId, null, BranchId);

        Assert.Equal(BranchId, result);
    }

    // ── CreateCategoryAsync ──

    [Fact]
    public async Task CreateCategory_ThrowsValidation_WhenNameEmpty()
    {
        var request = new CreateFinancialCategoryRequest { Name = "", Type = "expense", DefaultAmount = 100, DayOfMonth = 1, Nature = "fixed", Frequency = "monthly" };

        await Assert.ThrowsAsync<ValidationException>(() =>
            _service.CreateCategoryAsync(CompanyId, UserId, BranchId, request));
    }

    [Fact]
    public async Task CreateCategory_ThrowsValidation_WhenInvalidType()
    {
        var request = new CreateFinancialCategoryRequest { Name = "Alquiler", Type = "other", DefaultAmount = 100, DayOfMonth = 1, Nature = "fixed", Frequency = "monthly" };

        await Assert.ThrowsAsync<ValidationException>(() =>
            _service.CreateCategoryAsync(CompanyId, UserId, BranchId, request));
    }

    [Fact]
    public async Task CreateCategory_ThrowsValidation_WhenAmountZero()
    {
        var request = new CreateFinancialCategoryRequest { Name = "Alquiler", Type = "expense", DefaultAmount = 0, DayOfMonth = 1, Nature = "fixed", Frequency = "monthly" };

        await Assert.ThrowsAsync<ValidationException>(() =>
            _service.CreateCategoryAsync(CompanyId, UserId, BranchId, request));
    }

    [Fact]
    public async Task CreateCategory_ThrowsValidation_WhenDayOutOfRange()
    {
        var request = new CreateFinancialCategoryRequest { Name = "Alquiler", Type = "expense", DefaultAmount = 100, DayOfMonth = 32, Nature = "fixed", Frequency = "monthly" };

        await Assert.ThrowsAsync<ValidationException>(() =>
            _service.CreateCategoryAsync(CompanyId, UserId, BranchId, request));
    }

    [Fact]
    public async Task CreateCategory_ThrowsValidation_WhenBiweeklyMissingDays()
    {
        var request = new CreateFinancialCategoryRequest
        {
            Name = "Salario", Type = "expense", DefaultAmount = 500, DayOfMonth = 1,
            Nature = "fixed", Frequency = "biweekly", BiweeklyDay1 = null, BiweeklyDay2 = null
        };

        await Assert.ThrowsAsync<ValidationException>(() =>
            _service.CreateCategoryAsync(CompanyId, UserId, BranchId, request));
    }

    [Fact]
    public async Task CreateCategory_ThrowsValidation_WhenBiweeklyDaysSame()
    {
        var request = new CreateFinancialCategoryRequest
        {
            Name = "Salario", Type = "expense", DefaultAmount = 500, DayOfMonth = 1,
            Nature = "fixed", Frequency = "quincenal", BiweeklyDay1 = 15, BiweeklyDay2 = 15
        };

        await Assert.ThrowsAsync<ValidationException>(() =>
            _service.CreateCategoryAsync(CompanyId, UserId, BranchId, request));
    }

    [Fact]
    public async Task CreateCategory_Success()
    {
        var request = new CreateFinancialCategoryRequest
        {
            Name = "Alquiler", Type = "expense", DefaultAmount = 1500, DayOfMonth = 5,
            Nature = "fixed", Frequency = "monthly"
        };

        _repoMock.Setup(r => r.CreateCategoryAsync(It.IsAny<FinancialCategory>()))
            .ReturnsAsync(new FinancialCategory { Id = 42, CompanyId = CompanyId, Name = "Alquiler" });

        var result = await _service.CreateCategoryAsync(CompanyId, UserId, BranchId, request);

        Assert.Equal(42, result.Id);
        Assert.Equal("Alquiler", result.Name);
        _repoMock.Verify(r => r.CreateCategoryAsync(It.Is<FinancialCategory>(c =>
            c.CompanyId == CompanyId && c.DefaultAmount == 1500 && c.Type == "expense")), Times.Once);
    }

    // ── UpdateCategoryAsync ──

    [Fact]
    public async Task UpdateCategory_ThrowsNotFound_WhenMissing()
    {
        _repoMock.Setup(r => r.GetCategoryByIdAsync(99, CompanyId, BranchId, false)).ReturnsAsync((FinancialCategory?)null);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.UpdateCategoryAsync(CompanyId, BranchId, 99, new UpdateFinancialCategoryRequest { Name = "X", Type = "expense", DefaultAmount = 100, DayOfMonth = 1 }));
    }

    // ── InitMonthAsync ──

    [Fact]
    public async Task InitMonth_ThrowsValidation_WhenMonthEmpty()
    {
        await Assert.ThrowsAsync<ValidationException>(() =>
            _service.InitMonthAsync(CompanyId, UserId, BranchId, new InitFinanceMonthRequest { Month = "" }));
    }

    [Fact]
    public async Task InitMonth_ThrowsValidation_WhenMonthFormatInvalid()
    {
        await Assert.ThrowsAsync<ValidationException>(() =>
            _service.InitMonthAsync(CompanyId, UserId, BranchId, new InitFinanceMonthRequest { Month = "invalid" }));
    }

    [Fact]
    public async Task InitMonth_Success()
    {
        _repoMock.Setup(r => r.InitMonthFromFinancialItemsAsync(CompanyId, BranchId, It.IsAny<DateTime>(), UserId, null))
            .ReturnsAsync(5);

        var result = await _service.InitMonthAsync(CompanyId, UserId, BranchId, new InitFinanceMonthRequest { Month = "2025-06" });

        Assert.Equal(5, result);
    }

    // ── CreateEntryAsync ──

    [Fact]
    public async Task CreateEntry_ThrowsValidation_WhenInvalidType()
    {
        var request = new CreateFinancialEntryRequest { Type = "other", Amount = 100, CategoryId = 1, Description = "Test" };

        await Assert.ThrowsAsync<ValidationException>(() =>
            _service.CreateEntryAsync(CompanyId, UserId, BranchId, request));
    }

    [Fact]
    public async Task CreateEntry_ThrowsValidation_WhenAmountZero()
    {
        var request = new CreateFinancialEntryRequest { Type = "expense", Amount = 0, CategoryId = 1, Description = "Test" };

        await Assert.ThrowsAsync<ValidationException>(() =>
            _service.CreateEntryAsync(CompanyId, UserId, BranchId, request));
    }

    [Fact]
    public async Task CreateEntry_ThrowsNotFound_WhenCategoryMissing()
    {
        var request = new CreateFinancialEntryRequest { Type = "expense", Amount = 100, CategoryId = 99, Description = "Test" };
        _repoMock.Setup(r => r.GetCategoryByIdAsync(99, CompanyId, BranchId, true)).ReturnsAsync((FinancialCategory?)null);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.CreateEntryAsync(CompanyId, UserId, BranchId, request));
    }

    [Fact]
    public async Task CreateEntry_ThrowsValidation_WhenTypeMismatch()
    {
        var request = new CreateFinancialEntryRequest { Type = "income", Amount = 100, CategoryId = 1, Description = "Test" };
        _repoMock.Setup(r => r.GetCategoryByIdAsync(1, CompanyId, BranchId, true))
            .ReturnsAsync(new FinancialCategory { Id = 1, Type = "expense", IsActive = true });

        await Assert.ThrowsAsync<ValidationException>(() =>
            _service.CreateEntryAsync(CompanyId, UserId, BranchId, request));
    }

    [Fact]
    public async Task CreateEntry_Treats_Category_From_Another_Branch_As_NotFound()
    {
        var request = new CreateFinancialEntryRequest
        {
            Type = "expense", Amount = 100, CategoryId = 9, Description = "Scoped"
        };
        _repoMock.Setup(r => r.GetCategoryByIdAsync(9, CompanyId, BranchId, true))
            .ReturnsAsync((FinancialCategory?)null);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.CreateEntryAsync(CompanyId, UserId, BranchId, request));

        _repoMock.Verify(r => r.CreateEntryAsync(It.IsAny<FinancialEntry>()), Times.Never);
    }

    [Fact]
    public async Task CreateEntry_Success()
    {
        var request = new CreateFinancialEntryRequest { Type = "expense", Amount = 250, CategoryId = 1, Description = "Compra insumos" };
        _repoMock.Setup(r => r.GetCategoryByIdAsync(1, CompanyId, BranchId, true))
            .ReturnsAsync(new FinancialCategory { Id = 1, Type = "expense", IsActive = true });
        _repoMock.Setup(r => r.CreateEntryAsync(It.IsAny<FinancialEntry>()))
            .ReturnsAsync(new FinancialEntry { Id = 77, Description = "Compra insumos", Amount = 250 });

        var result = await _service.CreateEntryAsync(CompanyId, UserId, BranchId, request);

        Assert.Equal(77, result.Id);
        Assert.Equal(250, result.Amount);
    }

    // ── DeleteEntryAsync ──

    [Fact]
    public async Task DeleteEntry_ThrowsNotFound_WhenMissing()
    {
        _repoMock.Setup(r => r.GetEntryByIdAsync(99, CompanyId, null)).ReturnsAsync((FinancialEntry?)null);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.DeleteEntryAsync(CompanyId, 99));
    }

    [Fact]
    public async Task DeleteEntry_SkipsInsteadOfDelete_WhenNotManual()
    {
        _repoMock.Setup(r => r.GetEntryByIdAsync(1, CompanyId, null))
            .ReturnsAsync(new FinancialEntry { Id = 1, IsManual = false, Status = "pending" });
        _repoMock.Setup(r => r.UpdateEntryAsync(It.IsAny<FinancialEntry>(), null))
            .ReturnsAsync((FinancialEntry e, long? _) => e);

        await _service.DeleteEntryAsync(CompanyId, 1);

        _repoMock.Verify(r => r.UpdateEntryAsync(It.Is<FinancialEntry>(e => e.Status == "skipped"), null), Times.Once);
        _repoMock.Verify(r => r.SoftDeleteEntryAsync(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<long?>()), Times.Never);
    }

    [Fact]
    public async Task DeleteEntry_SoftDeletes_WhenManual()
    {
        _repoMock.Setup(r => r.GetEntryByIdAsync(1, CompanyId, null))
            .ReturnsAsync(new FinancialEntry { Id = 1, IsManual = true });

        await _service.DeleteEntryAsync(CompanyId, 1);

        _repoMock.Verify(r => r.SoftDeleteEntryAsync(1, CompanyId, null), Times.Once);
    }
}
