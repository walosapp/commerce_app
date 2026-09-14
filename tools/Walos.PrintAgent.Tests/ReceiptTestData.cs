using Walos.PrintAgent.Api;

namespace Walos.PrintAgent.Tests;

internal static class ReceiptTestData
{
    public static PrintReceiptRequest CreateRequest(string jobId = "receipt-job-1")
    {
        var request = new PrintReceiptRequest(
            DocumentVersion: 1,
            JobId: jobId,
            CompanyId: 10,
            BranchId: 20,
            OrderId: 300,
            Receipt: CreateDocument(),
            Fingerprint: string.Empty);

        return request with { Fingerprint = ReceiptFingerprint.Compute(request) };
    }

    public static ReceiptDocument CreateDocument() => new(
        CompanyName: "Café Español",
        CompanyLegalName: "Café Español S.A.S.",
        CompanyPhone: "+57 300 000 0000",
        CompanyTaxId: "900123456-7",
        CompanyAddress: "Calle 10 # 20-30",
        Currency: "COP",
        Timezone: "America/Bogota",
        OrderId: 300,
        OrderNumber: "ORD-00300",
        Status: "completed",
        RefundStatus: null,
        TableName: "Mesa principal de terraza con nombre largo",
        TableNumber: 7,
        CreatedAt: "2026-09-14T17:05:06.000Z",
        CashierName: "María Muñoz",
        Items:
        [
            new ReceiptItemDocument("Café con leche y porción de torta de chocolate", 2m, 6_250.50m, 12_501m),
            new ReceiptItemDocument("Jugo de maracuyá", 1.5m, 4_000m, 6_000m)
        ],
        Subtotal: 18_501m,
        DiscountType: "Porcentaje",
        DiscountValue: 10m,
        DiscountAmount: 1_850.10m,
        FinalTotalPaid: 14_800.90m,
        TipAmount: 1_850m,
        TipIncluded: true,
        SplitCount: 2,
        Payments:
        [
            new ReceiptPaymentDocument("Efectivo", 10_000m, null),
            new ReceiptPaymentDocument("Tarjeta", 5_000m, "APROB-123"),
            new ReceiptPaymentDocument("Transferencia", 1_650.90m, "TRX-Ñ")
        ],
        HasCredit: true,
        CreditStatus: "pending",
        CreditOriginalTotal: 16_650.90m,
        CreditAmountPaid: 14_800.90m,
        CreditAmount: 1_850m,
        CreditCustomerName: "José Pérez");
}
