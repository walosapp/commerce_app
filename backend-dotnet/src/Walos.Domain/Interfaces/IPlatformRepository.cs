using Walos.Domain.Entities.Platform;

namespace Walos.Domain.Interfaces;

public interface IPlatformRepository
{
    Task<IEnumerable<ServiceCatalog>> GetServiceCatalogAsync();

    Task<IEnumerable<CompanySubscription>> GetCompanySubscriptionsAsync(long companyId);
    Task<CompanySubscription?> GetSubscriptionAsync(long companyId, string serviceCode);
    Task UpsertSubscriptionAsync(CompanySubscription subscription);
    Task CancelSubscriptionAsync(long companyId, string serviceCode);

    Task<IEnumerable<BillingInvoice>> GetInvoicesAsync(long companyId);
    Task<BillingInvoice?> GetInvoiceByIdAsync(long id);
    Task<BillingInvoice> CreateInvoiceAsync(BillingInvoice invoice, IEnumerable<BillingInvoiceItem> items);
    Task UpdateInvoiceStatusAsync(long id, string status, string? paymentRef = null);

    Task<IEnumerable<PaymentMethod>> GetPaymentMethodsAsync(long companyId);
    Task<PaymentMethod> CreatePaymentMethodAsync(PaymentMethod method);
    Task SetDefaultPaymentMethodAsync(long id, long companyId);
    Task DeletePaymentMethodAsync(long id, long companyId);

    Task<CompanyAiSettings> GetAiSettingsAsync(long companyId);
    Task UpdateAiKeyAsync(long companyId, string? encryptedKey, string provider, bool managed);
    Task IncrementAiTokensAsync(long companyId, long tokens, decimal cost);
    Task ResetAiTokensAsync(long companyId);

    Task<IEnumerable<(long CompanyId, string CompanyName)>> GetAllCompaniesForBillingAsync();
    Task<IEnumerable<CompanySubscription>> GetSubscriptionsDueTodayAsync();
}
