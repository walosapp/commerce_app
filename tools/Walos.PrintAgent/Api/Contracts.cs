using Walos.PrintAgent.Printing;

namespace Walos.PrintAgent.Api;

public sealed record PairRequest(
    string PairingCode,
    long CompanyId,
    long BranchId,
    string WorkstationId);

public sealed record PairResponse(
    string Token,
    string TokenType,
    Guid AgentId);

public sealed record PrinterConfiguration(
    long CompanyId,
    long BranchId,
    string WorkstationId,
    string PrinterName,
    int DrawerPin,
    int DrawerOnTimeMs,
    int DrawerOffTimeMs);

public sealed record JobCommandRequest(string JobId);

public sealed record DrawerCommandRequest(
    string JobId,
    long CompanyId,
    long BranchId);

public sealed record JobCommandResponse(string JobId, string Status, bool Executed);

public sealed record PrintReceiptRequest(
    int DocumentVersion,
    string JobId,
    long CompanyId,
    long BranchId,
    long OrderId,
    ReceiptDocument Receipt,
    string Fingerprint);

public sealed record ReceiptDocument(
    string CompanyName,
    string? CompanyLegalName,
    string? CompanyPhone,
    string? CompanyTaxId,
    string? CompanyAddress,
    string Currency,
    string Timezone,
    long OrderId,
    string OrderNumber,
    string Status,
    string? RefundStatus,
    string TableName,
    int TableNumber,
    string CreatedAt,
    string CashierName,
    IReadOnlyList<ReceiptItemDocument> Items,
    decimal Subtotal,
    string? DiscountType,
    decimal DiscountValue,
    decimal DiscountAmount,
    decimal FinalTotalPaid,
    decimal TipAmount,
    bool TipIncluded,
    int SplitCount,
    IReadOnlyList<ReceiptPaymentDocument> Payments,
    bool HasCredit,
    string? CreditStatus,
    decimal? CreditOriginalTotal,
    decimal? CreditAmountPaid,
    decimal? CreditAmount,
    string? CreditCustomerName);

public sealed record ReceiptItemDocument(
    string ProductName,
    decimal Quantity,
    decimal UnitPrice,
    decimal Subtotal);

public sealed record ReceiptPaymentDocument(
    string Method,
    decimal Amount,
    string? Reference);

public sealed record PrintCashCloseRequest(
    int DocumentVersion,
    string JobId,
    long CompanyId,
    long BranchId,
    long CashRegisterId,
    CashCloseDocument CashClose,
    string Fingerprint);

public sealed record CashCloseDocument(
    string CompanyName,
    string BranchName,
    string Currency,
    string Timezone,
    long CashRegisterId,
    string OpenedAt,
    string ClosedAt,
    string UserName,
    decimal OpeningAmount,
    int OrderCount,
    decimal TotalSales,
    decimal CashSales,
    decimal CardSales,
    decimal TransferSales,
    decimal OtherSales,
    decimal RefundTotal,
    decimal CashIn,
    decimal CashOut,
    decimal ExpectedCash,
    decimal CountedCash,
    decimal Difference,
    string? Notes);

public sealed record PrintersResponse(
    IReadOnlyList<PrinterDescriptor> Printers,
    string? SelectedPrinter);

public sealed record ErrorResponse(string Code, string Message);
