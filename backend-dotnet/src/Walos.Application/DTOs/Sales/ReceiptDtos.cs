namespace Walos.Application.DTOs.Sales;

// Recibo de venta — entregado al cliente
public record ReceiptData(
    // Empresa
    string CompanyName,
    string? CompanyLegalName,
    string? CompanyPhone,
    string? CompanyLogoUrl,
    
    // Orden
    long OrderId,
    string OrderNumber,
    string TableName,
    int TableNumber,
    DateTime CreatedAt,
    string CashierName,
    
    // Items
    List<ReceiptItemDto> Items,
    
    // Totales
    decimal Subtotal,
    string? DiscountType,
    decimal DiscountValue,
    decimal DiscountAmount,
    decimal FinalTotalPaid,
    decimal TipAmount,
    bool TipIncluded,
    int SplitCount,
    
    // Pagos
    List<ReceiptPaymentDto> Payments,
    
    // Credito
    bool HasCredit,
    decimal? CreditAmount,
    string? CreditCustomerName
);

public record ReceiptItemDto(
    string ProductName,
    decimal Quantity,
    decimal UnitPrice,
    decimal Subtotal
);

public record ReceiptPaymentDto(
    string Method,
    decimal Amount,
    string? Reference
);

// Comanda de cocina — SIN precios
public record KitchenTicketData(
    string TableName,
    int TableNumber,
    string OrderNumber,
    DateTime CreatedAt,
    string CashierName,
    List<KitchenItemDto> Items
);

public record KitchenItemDto(
    string ProductName,
    decimal Quantity,
    string? Notes
);

// Reporte Z — cierre de caja
public record ZReportData(
    long CashRegisterId,
    DateTime OpenedAt,
    DateTime? ClosedAt,
    string OpenedByName,
    string? ClosedByName,
    decimal OpeningAmount,
    decimal? ClosingAmount,
    decimal? ExpectedCash,
    decimal? Difference,
    
    decimal TotalSales,
    int OrderCount,
    decimal TotalCashSales,
    decimal TotalCardSales,
    decimal TotalTransferSales,
    decimal TotalOtherSales,
    decimal TotalDiscounts,
    decimal TotalCredits,
    decimal TotalTips,
    decimal CashIn,
    decimal CashOut,
    
    List<PaymentMethodSummaryDto> PaymentBreakdown,
    List<CashMovementResponse> Movements,
    
    string CompanyName,
    string? CompanyLegalName
);
