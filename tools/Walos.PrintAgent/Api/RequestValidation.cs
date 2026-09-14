using System.Globalization;
using System.Text.RegularExpressions;

namespace Walos.PrintAgent.Api;

internal static partial class RequestValidation
{
    public static string? Validate(PairRequest request)
    {
        if (!PairingCodeRegex().IsMatch(request.PairingCode ?? string.Empty))
        {
            return "pairingCode debe contener exactamente 6 dígitos.";
        }

        return ValidateIdentity(request.CompanyId, request.BranchId, request.WorkstationId);
    }

    public static string? Validate(PrinterConfiguration request)
    {
        var identityError = ValidateIdentity(request.CompanyId, request.BranchId, request.WorkstationId);
        if (identityError is not null)
        {
            return identityError;
        }

        if (string.IsNullOrWhiteSpace(request.PrinterName) || request.PrinterName.Length > 260)
        {
            return "printerName es obligatorio y no puede exceder 260 caracteres.";
        }

        if (request.DrawerPin is not (0 or 1))
        {
            return "drawerPin debe ser 0 o 1.";
        }

        if (request.DrawerOnTimeMs is < 10 or > 500 || request.DrawerOffTimeMs is < 10 or > 510)
        {
            return "Los tiempos del cajón están fuera del rango permitido.";
        }

        return null;
    }

    public static string? Validate(JobCommandRequest request)
    {
        return JobIdRegex().IsMatch(request.JobId ?? string.Empty)
            ? null
            : "jobId debe tener entre 1 y 100 caracteres seguros.";
    }

    public static string? Validate(PrintReceiptRequest request)
    {
        if (request.DocumentVersion != 1)
        {
            return "documentVersion no es compatible; se requiere la versión 1.";
        }

        var jobError = Validate(new JobCommandRequest(request.JobId));
        if (jobError is not null)
        {
            return jobError;
        }

        if (request.CompanyId <= 0 || request.BranchId <= 0 || request.OrderId <= 0)
        {
            return "companyId, branchId y orderId deben ser mayores que cero.";
        }

        if (!FingerprintRegex().IsMatch(request.Fingerprint ?? string.Empty))
        {
            return "fingerprint debe ser un SHA-256 hexadecimal en minúsculas.";
        }

        if (request.Receipt is null)
        {
            return "receipt es obligatorio.";
        }

        if (request.Receipt.OrderId != request.OrderId)
        {
            return "orderId debe coincidir con receipt.orderId.";
        }

        return Validate(request.Receipt);
    }

    private static string? Validate(ReceiptDocument receipt)
    {
        var error = ValidateRequiredText(receipt.CompanyName, "receipt.companyName", 120)
                    ?? ValidateOptionalText(receipt.CompanyLegalName, "receipt.companyLegalName", 160)
                    ?? ValidateOptionalText(receipt.CompanyPhone, "receipt.companyPhone", 50)
                    ?? ValidateOptionalText(receipt.CompanyTaxId, "receipt.companyTaxId", 50)
                    ?? ValidateOptionalText(receipt.CompanyAddress, "receipt.companyAddress", 200)
                    ?? ValidateRequiredText(receipt.Currency, "receipt.currency", 3)
                    ?? ValidateRequiredText(receipt.Timezone, "receipt.timezone", 100)
                    ?? ValidateRequiredText(receipt.OrderNumber, "receipt.orderNumber", 60)
                    ?? ValidateRequiredText(receipt.Status, "receipt.status", 20)
                    ?? ValidateOptionalText(receipt.RefundStatus, "receipt.refundStatus", 30)
                    ?? ValidateRequiredText(receipt.TableName, "receipt.tableName", 100)
                    ?? ValidateRequiredText(receipt.CashierName, "receipt.cashierName", 120)
                    ?? ValidateOptionalText(receipt.DiscountType, "receipt.discountType", 30)
                    ?? ValidateOptionalText(receipt.CreditStatus, "receipt.creditStatus", 20)
                    ?? ValidateOptionalText(receipt.CreditCustomerName, "receipt.creditCustomerName", 160);
        if (error is not null)
        {
            return error;
        }

        if (!CurrencyRegex().IsMatch(receipt.Currency) || !IsKnownTimeZone(receipt.Timezone))
        {
            return "receipt.currency o receipt.timezone no son válidos.";
        }

        if (!receipt.Status.Equals("completed", StringComparison.Ordinal) ||
            !string.IsNullOrWhiteSpace(receipt.RefundStatus))
        {
            return "Solo se pueden imprimir directamente órdenes completadas sin devoluciones.";
        }

        if (!DateTimeOffset.TryParseExact(
                receipt.CreatedAt,
                "yyyy-MM-dd'T'HH:mm:ss.fff'Z'",
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out _))
        {
            return "receipt.createdAt debe usar ISO UTC canónico yyyy-MM-ddTHH:mm:ss.fffZ.";
        }

        if (receipt.TableNumber < 0 || receipt.SplitCount is < 1 or > 100)
        {
            return "receipt.tableNumber o receipt.splitCount está fuera del rango permitido.";
        }

        if (receipt.Items is null || receipt.Items.Count is < 1 or > 100)
        {
            return "receipt.items debe contener entre 1 y 100 elementos.";
        }

        foreach (var item in receipt.Items)
        {
            if (item is null)
            {
                return "receipt.items no puede contener elementos null.";
            }

            error = ValidateRequiredText(item.ProductName, "receipt.items.productName", 200);
            if (error is not null)
            {
                return error;
            }

            if (item.Quantity <= 0 || item.Quantity > 999_999m || DecimalScale(item.Quantity) > 3 ||
                !IsMoney(item.UnitPrice) || !IsMoney(item.Subtotal) ||
                !MoneyEquals(item.Subtotal, item.Quantity * item.UnitPrice))
            {
                return "Un item contiene cantidades o valores fuera del rango permitido.";
            }
        }

        if (!IsMoney(receipt.Subtotal) ||
            !IsMoney(receipt.DiscountValue) ||
            !IsMoney(receipt.DiscountAmount) ||
            !IsMoney(receipt.FinalTotalPaid) ||
            !IsMoney(receipt.TipAmount))
        {
            return "El recibo contiene totales fuera del rango permitido.";
        }

        if (receipt.DiscountAmount > receipt.Subtotal ||
            !MoneyEquals(receipt.Items.Sum(item => item.Subtotal), receipt.Subtotal))
        {
            return "Los subtotales o el descuento del recibo no son consistentes.";
        }

        var saleTotal = RoundMoney(receipt.Subtotal - receipt.DiscountAmount);
        if (receipt.FinalTotalPaid > saleTotal)
        {
            return "El valor pagado no puede superar el total de la venta.";
        }

        if (receipt.Payments is null || receipt.Payments.Count > 20)
        {
            return "receipt.payments no puede exceder 20 elementos.";
        }

        foreach (var payment in receipt.Payments)
        {
            if (payment is null)
            {
                return "receipt.payments no puede contener elementos null.";
            }

            error = ValidateRequiredText(payment.Method, "receipt.payments.method", 50)
                    ?? ValidateOptionalText(payment.Reference, "receipt.payments.reference", 100);
            if (error is not null)
            {
                return error;
            }

            if (!IsMoney(payment.Amount) || payment.Amount <= 0)
            {
                return "Un pago contiene un valor fuera del rango permitido.";
            }
        }

        if (receipt.HasCredit)
        {
            var validCreditStatus = receipt.CreditStatus is "pending" or "partial" or "paid" or "cancelled";
            if (!validCreditStatus ||
                receipt.CreditOriginalTotal is null || !IsMoney(receipt.CreditOriginalTotal.Value) ||
                receipt.CreditAmountPaid is null || !IsMoney(receipt.CreditAmountPaid.Value) ||
                receipt.CreditAmount is null || !IsMoney(receipt.CreditAmount.Value))
            {
                return "Los datos persistidos del crédito están incompletos o su estado no es válido.";
            }

            var creditAmountPaid = RoundMoney(receipt.CreditAmountPaid.Value);
            var creditAmount = RoundMoney(receipt.CreditAmount.Value);
            if (!MoneyEquals(receipt.CreditOriginalTotal.Value, saleTotal) ||
                creditAmountPaid < RoundMoney(receipt.FinalTotalPaid) ||
                creditAmountPaid > saleTotal ||
                !MoneyEquals(creditAmount, saleTotal - creditAmountPaid) ||
                (receipt.CreditStatus == "paid" && creditAmount != 0m) ||
                (receipt.CreditStatus is "pending" or "partial" && creditAmount <= 0m))
            {
                return "Los montos persistidos del crédito no son consistentes con la venta.";
            }
        }
        else if (!string.IsNullOrWhiteSpace(receipt.CreditStatus) ||
                 receipt.CreditOriginalTotal is not null || receipt.CreditAmountPaid is not null ||
                 receipt.CreditAmount is not null || !string.IsNullOrWhiteSpace(receipt.CreditCustomerName) ||
                 !MoneyEquals(receipt.FinalTotalPaid, saleTotal))
        {
            return "Los datos o el saldo de crédito requieren hasCredit=true.";
        }

        var expectedPayments = RoundMoney(
            receipt.FinalTotalPaid + (receipt.TipIncluded ? receipt.TipAmount : 0m));
        if (!MoneyEquals(receipt.Payments.Sum(payment => payment.Amount), expectedPayments))
        {
            return "Los pagos no coinciden con el valor pagado y la propina incluida.";
        }

        return null;
    }

    private static decimal RoundMoney(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private static bool MoneyEquals(decimal left, decimal right) =>
        Math.Abs(RoundMoney(left) - RoundMoney(right)) <= 0.01m;

    private static bool IsKnownTimeZone(string timeZoneId)
    {
        try
        {
            _ = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            return true;
        }
        catch (TimeZoneNotFoundException)
        {
            return false;
        }
        catch (InvalidTimeZoneException)
        {
            return false;
        }
    }

    private static bool IsMoney(decimal value) =>
        value is >= 0 and <= 999_999_999.99m && DecimalScale(value) <= 2;

    private static int DecimalScale(decimal value) => (decimal.GetBits(value)[3] >> 16) & 0x7F;

    private static string? ValidateRequiredText(string? value, string name, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return $"{name} es obligatorio.";
        }

        return ValidateText(value, name, maxLength);
    }

    private static string? ValidateOptionalText(string? value, string name, int maxLength) =>
        value is null ? null : ValidateText(value, name, maxLength);

    private static string? ValidateText(string value, string name, int maxLength)
    {
        if (value.Length > maxLength)
        {
            return $"{name} no puede exceder {maxLength} caracteres.";
        }

        if (value.Any(character => char.IsControl(character) && character is not ('\r' or '\n' or '\t')))
        {
            return $"{name} contiene caracteres de control no permitidos.";
        }

        return HtmlTagRegex().IsMatch(value)
            ? $"{name} no puede contener HTML."
            : null;
    }

    private static string? ValidateIdentity(long companyId, long branchId, string workstationId)
    {
        if (companyId <= 0 || branchId <= 0)
        {
            return "companyId y branchId deben ser mayores que cero.";
        }

        if (!WorkstationIdRegex().IsMatch(workstationId ?? string.Empty))
        {
            return "workstationId debe tener entre 1 y 100 caracteres seguros.";
        }

        return null;
    }

    [GeneratedRegex("^[0-9]{6}$", RegexOptions.CultureInvariant)]
    private static partial Regex PairingCodeRegex();

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9._:-]{0,99}$", RegexOptions.CultureInvariant)]
    private static partial Regex JobIdRegex();

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9._:-]{0,99}$", RegexOptions.CultureInvariant)]
    private static partial Regex WorkstationIdRegex();

    [GeneratedRegex("^[a-f0-9]{64}$", RegexOptions.CultureInvariant)]
    private static partial Regex FingerprintRegex();

    [GeneratedRegex("^[A-Z]{3}$", RegexOptions.CultureInvariant)]
    private static partial Regex CurrencyRegex();

    [GeneratedRegex("<\\s*/?\\s*[A-Za-z][^>]*>", RegexOptions.CultureInvariant)]
    private static partial Regex HtmlTagRegex();
}
