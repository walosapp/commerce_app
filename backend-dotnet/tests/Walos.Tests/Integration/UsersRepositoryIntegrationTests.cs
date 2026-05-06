using Walos.Domain.Entities;

namespace Walos.Tests.Integration;

public class UsersRepositoryIntegrationTests : IntegrationTestBase
{
    [SkippableFact]
    public async Task GetAllAsync_Should_Return_Only_Users_From_Requested_Company()
    {
        var companyA = await SeedCompanyAsync("Users Co A");
        var companyB = await SeedCompanyAsync("Users Co B");
        var branchA = await SeedBranchAsync(companyA, "Branch A");
        var branchB = await SeedBranchAsync(companyB, "Branch B");

        await SeedUserAsync(companyA, branchA, "a-user@test.com");
        await SeedUserAsync(companyB, branchB, "b-user@test.com");

        var companyAUsers = (await UsersRepository.GetAllAsync(companyA)).ToList();
        var companyBUsers = (await UsersRepository.GetAllAsync(companyB)).ToList();

        Assert.All(companyAUsers, u => Assert.Equal(companyA, u.CompanyId));
        Assert.All(companyBUsers, u => Assert.Equal(companyB, u.CompanyId));
        Assert.DoesNotContain(companyAUsers, u => u.Email == "b-user@test.com");
        Assert.DoesNotContain(companyBUsers, u => u.Email == "a-user@test.com");
    }

    [SkippableFact]
    public async Task GetByIdAsync_Should_Return_Null_When_User_Belongs_To_Another_Company()
    {
        var companyA = await SeedCompanyAsync("GetById Co A");
        var companyB = await SeedCompanyAsync("GetById Co B");
        var branchA = await SeedBranchAsync(companyA, "Branch A");
        var userId = await SeedUserAsync(companyA, branchA, "cross-company@test.com");

        var wrongCompanyRead = await UsersRepository.GetByIdAsync(userId, companyB);
        var correctCompanyRead = await UsersRepository.GetByIdAsync(userId, companyA);

        Assert.Null(wrongCompanyRead);
        Assert.NotNull(correctCompanyRead);
        Assert.Equal(companyA, correctCompanyRead!.CompanyId);
    }

    [SkippableFact]
    public async Task SetActiveAsync_Should_Not_Update_User_When_Company_Does_Not_Match()
    {
        var companyA = await SeedCompanyAsync("SetActive Co A");
        var companyB = await SeedCompanyAsync("SetActive Co B");
        var branchA = await SeedBranchAsync(companyA, "Branch A");
        var userId = await SeedUserAsync(companyA, branchA, "status-user@test.com");

        var updatedWithWrongCompany = await UsersRepository.SetActiveAsync(userId, companyB, false);
        var updatedWithCorrectCompany = await UsersRepository.SetActiveAsync(userId, companyA, false);
        var user = await UsersRepository.GetByIdAsync(userId, companyA);

        Assert.False(updatedWithWrongCompany);
        Assert.True(updatedWithCorrectCompany);
        Assert.NotNull(user);
        Assert.False(user!.IsActive);
        Assert.Equal(branchA, user.BranchId);
    }
}
