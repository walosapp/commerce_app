namespace Walos.Application.DTOs.Platform;

public record ServiceCatalogResponse(
    long Id,
    string Code,
    string Name,
    string? Description,
    decimal BasePrice,
    string BillingUnit,
    bool IsActive,
    int DisplayOrder
);

public record CompanySubscriptionResponse(
    long Id,
    long CompanyId,
    string ServiceCode,
    string ServiceName,
    bool IsActive,
    decimal? CustomPrice,
    decimal BasePrice,
    decimal EffectivePrice,
    string BillingFrequency,
    DateOnly? NextBillingDate,
    DateTime StartedAt,
    DateTime? CancelledAt,
    string? Notes
);

public record AssignServiceRequest(
    string ServiceCode,
    bool IsActive,
    decimal? CustomPrice,
    string BillingFrequency,
    DateOnly? NextBillingDate,
    string? Notes
);

public record BillingInvoiceResponse(
    long Id,
    long CompanyId,
    string? CompanyName,
    string InvoiceNumber,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    decimal Subtotal,
    decimal TaxRate,
    decimal TaxAmount,
    decimal Total,
    string Status,
    DateTime? SentAt,
    DateTime? PaidAt,
    DateOnly? DueDate,
    string? PaymentMethod,
    string? PaymentRef,
    string? Notes,
    DateTime CreatedAt,
    List<BillingInvoiceItemResponse> Items
);

public record BillingInvoiceItemResponse(
    long Id,
    string? ServiceCode,
    string Description,
    decimal Quantity,
    decimal UnitPrice,
    decimal Subtotal
);

public record GenerateInvoiceRequest(
    long CompanyId,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    DateOnly? DueDate,
    string? Notes,
    List<InvoiceItemRequest> Items
);

public record InvoiceItemRequest(
    string? ServiceCode,
    string Description,
    decimal Quantity,
    decimal UnitPrice
);

public record UpdateInvoiceStatusRequest(string Status, string? PaymentRef);

public record PaymentMethodResponse(
    long Id,
    string Type,
    string Provider,
    string? Last4,
    string? BankName,
    string? HolderName,
    bool IsDefault
);

public record RegisterPaymentMethodRequest(
    string Type,
    string ProviderToken,
    string? Last4,
    string? BankName,
    string? HolderName,
    bool IsDefault
);

public record CompanyAiSettingsResponse(
    long CompanyId,
    bool AiKeyManaged,
    string? AiProvider,
    bool HasCustomKey,
    long AiTokensUsed,
    DateTime? AiTokensResetAt,
    decimal AiEstimatedCost
);

public record UpdateAiKeyRequest(
    string Provider,
    string? ApiKey,
    bool Managed
);

public record CompanyPlanResponse(
    long CompanyId,
    string CompanyName,
    string SubscriptionPlan,
    List<CompanySubscriptionResponse> Services,
    List<BillingInvoiceResponse> RecentInvoices,
    CompanyAiSettingsResponse AiSettings
);

public record AdminCompanyListItem(
    long Id,
    string Name,
    string SubscriptionPlan,
    bool IsActive,
    int ActiveServices,
    DateOnly? NextBillingDate,
    string? PendingInvoiceStatus
);
