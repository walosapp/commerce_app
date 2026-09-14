using System.Text;
using System.Globalization;
using Walos.PrintAgent.Api;

namespace Walos.PrintAgent.Printing;

public sealed class EscPos58Encoder
{
    public const int Columns = 32;
    private static readonly Encoding PrinterEncoding;

    static EscPos58Encoder()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        PrinterEncoding = Encoding.GetEncoding(
            858,
            new EncoderReplacementFallback("?"),
            new DecoderReplacementFallback("?"));
    }

    public byte[] EncodeTestTicket(PrinterConfiguration configuration)
    {
        using var output = new MemoryStream();
        output.Write([0x1B, 0x40]); // ESC @: initialize
        output.Write([0x1B, 0x74, 0x13]); // ESC t 19: CP858 on common ESC/POS firmware
        output.Write([0x1B, 0x61, 0x01]); // centered
        WriteLine(output, "WALOS");
        WriteLine(output, "PRUEBA DE IMPRESIÓN");
        output.Write([0x1B, 0x61, 0x00]); // left
        WriteLine(output, new string('-', Columns));
        WriteWrapped(output, $"Empresa: {configuration.CompanyId}");
        WriteWrapped(output, $"Sucursal: {configuration.BranchId}");
        WriteWrapped(output, $"Estación: {configuration.WorkstationId}");
        WriteLine(output, "Español: áéíóú ÁÉÍÓÚ ñ Ñ ü");
        WriteLine(output, FormatAmount(1234.56m));
        WriteWrapped(output, "Texto largo de validación para papel térmico de cincuenta y ocho milímetros.");
        WriteLine(output, new string('-', Columns));
        WriteLine(output, DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        WriteLine(output, string.Empty);
        WriteLine(output, string.Empty);
        WriteLine(output, string.Empty);
        output.Write([0x1D, 0x56, 0x00]); // full cut, ignored by printers without cutter
        return output.ToArray();
    }

    public byte[] EncodeDrawerPulse(int pin, int onTimeMs, int offTimeMs)
    {
        if (pin is not (0 or 1))
        {
            throw new ArgumentOutOfRangeException(nameof(pin));
        }

        return
        [
            0x1B,
            0x70,
            (byte)pin,
            ToPulseUnit(onTimeMs),
            ToPulseUnit(offTimeMs)
        ];
    }

    public byte[] EncodeReceipt(ReceiptDocument receipt)
    {
        using var output = new MemoryStream();
        output.Write([0x1B, 0x40]); // ESC @: initialize
        output.Write([0x1B, 0x74, 0x13]); // ESC t 19: CP858
        output.Write([0x1B, 0x61, 0x01]); // centered
        WriteWrapped(output, receipt.CompanyName);
        if (!string.IsNullOrWhiteSpace(receipt.CompanyLegalName))
        {
            WriteWrapped(output, receipt.CompanyLegalName);
        }
        if (!string.IsNullOrWhiteSpace(receipt.CompanyPhone))
        {
            WriteWrapped(output, $"Tel: {receipt.CompanyPhone}");
        }
        if (!string.IsNullOrWhiteSpace(receipt.CompanyTaxId))
        {
            WriteWrapped(output, $"NIT: {receipt.CompanyTaxId}");
        }
        if (!string.IsNullOrWhiteSpace(receipt.CompanyAddress))
        {
            WriteWrapped(output, receipt.CompanyAddress);
        }

        output.Write([0x1B, 0x61, 0x00]); // left
        WriteSeparator(output);
        WriteWrapped(output, $"Orden: {receipt.OrderNumber}");
        var createdAt = DateTimeOffset.ParseExact(
            receipt.CreatedAt,
            "yyyy-MM-dd'T'HH:mm:ss.fff'Z'",
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
        var localCreatedAt = TimeZoneInfo.ConvertTime(
            createdAt,
            TimeZoneInfo.FindSystemTimeZoneById(receipt.Timezone));
        WriteWrapped(output, $"Fecha: {localCreatedAt:yyyy-MM-dd HH:mm}");
        WriteWrapped(output, $"Cajero: {receipt.CashierName}");
        WriteWrapped(output, $"Mesa: {receipt.TableName}");
        if (receipt.SplitCount > 1)
        {
            WriteWrapped(output, $"Divisiones: {receipt.SplitCount}");
        }

        WriteSeparator(output);
        WriteLine(output, "PRODUCTOS");
        foreach (var item in receipt.Items)
        {
            WriteWrapped(output, item.ProductName);
            WriteWrapped(output, $"{FormatQuantity(item.Quantity)} x {FormatMoney(item.UnitPrice, receipt.Currency)}");
            WriteLeftRight(output, "Importe", FormatMoney(item.Subtotal, receipt.Currency));
        }

        WriteSeparator(output);
        WriteLeftRight(output, "Subtotal", FormatMoney(receipt.Subtotal, receipt.Currency));
        if (receipt.DiscountAmount > 0)
        {
            var discountLabel = string.IsNullOrWhiteSpace(receipt.DiscountType)
                ? "Descuento"
                : $"Descuento {receipt.DiscountType}";
            WriteLeftRight(output, discountLabel, $"-{FormatMoney(receipt.DiscountAmount, receipt.Currency)}");
        }
        var saleTotal = Math.Round(
            receipt.Subtotal - receipt.DiscountAmount,
            2,
            MidpointRounding.AwayFromZero);
        WriteLeftRight(output, "TOTAL VENTA", FormatMoney(saleTotal, receipt.Currency));
        WriteLeftRight(output, "PAGADO VENTA", FormatMoney(receipt.FinalTotalPaid, receipt.Currency));
        if (receipt.TipAmount > 0)
        {
            WriteLeftRight(
                output,
                receipt.TipIncluded ? "Propina incluida" : "Propina no incluida",
                FormatMoney(receipt.TipAmount, receipt.Currency));
        }

        if (receipt.Payments.Count > 0)
        {
            WriteSeparator(output);
            WriteLine(output, "PAGOS");
            foreach (var payment in receipt.Payments)
            {
                WriteLeftRight(output, FormatPaymentMethod(payment.Method), FormatMoney(payment.Amount, receipt.Currency));
                if (!string.IsNullOrWhiteSpace(payment.Reference))
                {
                    WriteWrapped(output, $"Ref: {payment.Reference}");
                }
            }
        }

        if (receipt.HasCredit && receipt.CreditAmount.HasValue &&
            receipt.CreditOriginalTotal.HasValue && receipt.CreditAmountPaid.HasValue)
        {
            WriteSeparator(output);
            WriteLine(output, receipt.CreditStatus switch
            {
                "cancelled" => "CRÉDITO CANCELADO",
                "paid" => "CRÉDITO SALDADO",
                _ => "CRÉDITO VIGENTE"
            });
            if (!string.IsNullOrWhiteSpace(receipt.CreditCustomerName))
            {
                WriteWrapped(output, $"Cliente: {receipt.CreditCustomerName}");
            }
            var originalCredit = receipt.CreditOriginalTotal.Value - receipt.FinalTotalPaid;
            var laterPayments = receipt.CreditAmountPaid.Value - receipt.FinalTotalPaid;
            WriteLeftRight(output, "Crédito original", FormatMoney(originalCredit, receipt.Currency));
            if (laterPayments > 0m)
            {
                WriteLeftRight(output, "Abonos posteriores", FormatMoney(laterPayments, receipt.Currency));
            }
            WriteLeftRight(
                output,
                receipt.CreditStatus == "cancelled" ? "Saldo no vigente" : "Saldo crédito",
                FormatMoney(receipt.CreditAmount.Value, receipt.Currency));
            if (receipt.CreditStatus == "cancelled")
            {
                WriteWrapped(output, "NO VIGENTE / NO EXIGIBLE");
            }
        }

        WriteSeparator(output);
        WriteLine(output, string.Empty);
        WriteLine(output, string.Empty);
        WriteLine(output, string.Empty);
        output.Write([0x1D, 0x56, 0x00]); // full cut, ignored by printers without cutter
        return output.ToArray();
    }

    public static string Sanitize(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length);
        foreach (var character in value.Normalize(NormalizationForm.FormC))
        {
            if (!char.IsControl(character))
            {
                builder.Append(character);
            }
            else if (character is '\r' or '\n' or '\t')
            {
                builder.Append(' ');
            }
        }

        return builder.ToString();
    }

    public static IReadOnlyList<string> Wrap(string? value, int columns = Columns)
    {
        var sanitized = Sanitize(value).Trim();
        if (sanitized.Length == 0)
        {
            return [string.Empty];
        }

        var lines = new List<string>();
        var words = sanitized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var current = new StringBuilder();

        foreach (var originalWord in words)
        {
            var word = originalWord;
            while (word.Length > columns)
            {
                if (current.Length > 0)
                {
                    lines.Add(current.ToString());
                    current.Clear();
                }

                lines.Add(word[..columns]);
                word = word[columns..];
            }

            if (word.Length == 0)
            {
                continue;
            }

            if (current.Length > 0 && current.Length + 1 + word.Length > columns)
            {
                lines.Add(current.ToString());
                current.Clear();
            }

            if (current.Length > 0)
            {
                current.Append(' ');
            }

            current.Append(word);
        }

        if (current.Length > 0)
        {
            lines.Add(current.ToString());
        }

        return lines;
    }

    public static string FormatAmount(decimal amount) =>
        $"TOTAL     $ {amount.ToString("N2", CultureInfo.GetCultureInfo("es-CO"))}";

    public static string FormatMoney(decimal amount) =>
        $"$ {amount.ToString("N2", CultureInfo.GetCultureInfo("es-CO"))}";

    public static string FormatMoney(decimal amount, string currency) =>
        $"{currency} {amount.ToString("N2", CultureInfo.GetCultureInfo("es-CO"))}";

    public static string FormatQuantity(decimal quantity) =>
        quantity.ToString("0.###", CultureInfo.GetCultureInfo("es-CO"));

    private static string FormatPaymentMethod(string method) => method switch
    {
        "cash" => "Efectivo",
        "card" => "Tarjeta",
        "transfer" => "Transferencia",
        "nequi" => "Nequi",
        "other" => "Otro",
        _ => method
    };

    private static byte ToPulseUnit(int milliseconds) => (byte)Math.Clamp(
        (int)Math.Round(milliseconds / 2.0, MidpointRounding.AwayFromZero),
        1,
        255);

    private static void WriteWrapped(Stream output, string text)
    {
        foreach (var line in Wrap(text))
        {
            WriteLine(output, line);
        }
    }

    private static void WriteLeftRight(Stream output, string left, string right)
    {
        left = Sanitize(left).Trim();
        right = Sanitize(right).Trim();
        if (left.Length + right.Length + 1 <= Columns)
        {
            WriteLine(output, left + new string(' ', Columns - left.Length - right.Length) + right);
            return;
        }

        WriteWrapped(output, left);
        foreach (var line in Wrap(right))
        {
            WriteLine(output, line.PadLeft(Columns));
        }
    }

    private static void WriteSeparator(Stream output) => WriteLine(output, new string('-', Columns));

    private static void WriteLine(Stream output, string text)
    {
        output.Write(PrinterEncoding.GetBytes(Sanitize(text)));
        output.WriteByte(0x0A);
    }
}
