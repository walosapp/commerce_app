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

    private static void WriteLine(Stream output, string text)
    {
        output.Write(PrinterEncoding.GetBytes(Sanitize(text)));
        output.WriteByte(0x0A);
    }
}
