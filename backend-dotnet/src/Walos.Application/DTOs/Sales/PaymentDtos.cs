namespace Walos.Application.DTOs.Sales;

// Representa un pago individual en una orden
public record PaymentLineDto(
    string Method,     // cash | card | transfer | nequi | other
    decimal Amount,
    string? Reference  // Referencia de transferencia, aprobación TC, etc.
);

// Request actualizado para facturar con pagos
public record InvoiceWithPaymentsRequest(
    string DiscountType,
    decimal DiscountValue,
    decimal DiscountAmount,
    decimal FinalTotalPaid,
    int SplitCount,
    bool OverrideConfirmed,
    
    // Credito
    bool HasCredit,
    decimal CreditAmountPaid,
    string? CreditCustomerName,
    string? Notes,
    
    // Pagos
    List<PaymentLineDto> Payments,  // Requerido
    
    // Propina
    decimal TipAmount,
    bool TipIncluded
);

// Response de pagos
public record OrderPaymentResponse(
    long Id,
    string Method,
    decimal Amount,
    string? Reference,
    DateTime CreatedAt
);
