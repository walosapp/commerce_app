namespace Walos.Application.DTOs.Sales;

// Request: Abrir caja
public record OpenCashRegisterRequest(
    decimal OpeningAmount,
    string? Notes
);

// Request: Cerrar caja
public record CloseCashRegisterRequest(
    decimal ClosingAmount,
    string? Notes
);

// Request: Movimiento manual (entrada/salida)
public record CashMovementRequest(
    string Type,  // "in" | "out"
    decimal Amount,
    string Reason,
    string? Notes
);

// Response: Caja
public record CashRegisterResponse(
    long Id,
    long BranchId,
    long OpenedBy,
    string? OpenedByName,
    long? ClosedBy,
    string? ClosedByName,
    string Status,
    decimal OpeningAmount,
    decimal? ClosingAmount,
    decimal? ExpectedCash,
    decimal? Difference,
    decimal TotalSales,
    decimal TotalCashSales,
    decimal TotalCardSales,
    decimal TotalTransferSales,
    decimal TotalOtherSales,
    decimal TotalDiscounts,
    decimal TotalCredits,
    decimal TotalTips,
    decimal CashIn,
    decimal CashOut,
    int OrderCount,
    string? Notes,
    DateTime OpenedAt,
    DateTime? ClosedAt
);

// Response: Movimiento de caja
public record CashMovementResponse(
    long Id,
    string Type,
    decimal Amount,
    string Reason,
    string? Notes,
    long CreatedBy,
    string? CreatedByName,
    DateTime CreatedAt
);

// Response: Resumen de caja (Reporte Z)
public record CashRegisterSummaryResponse(
    CashRegisterResponse Register,
    List<CashMovementResponse> Movements,
    List<PaymentMethodSummaryDto> PaymentBreakdown
);

public record PaymentMethodSummaryDto(
    string Method,
    decimal TotalAmount,
    int TransactionCount
);
