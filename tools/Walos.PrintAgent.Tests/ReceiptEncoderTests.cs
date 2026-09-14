using System.Text;
using Walos.PrintAgent.Printing;

namespace Walos.PrintAgent.Tests;

public sealed class ReceiptEncoderTests
{
    private readonly EscPos58Encoder _encoder = new();

    [Fact]
    public void Receipt_Uses58MmLayoutAndContainsPersistedSections()
    {
        var bytes = _encoder.EncodeReceipt(ReceiptTestData.CreateDocument());
        var text = DecodeText(bytes);

        Assert.All(text.Split('\n'), line => Assert.InRange(line.TrimEnd('\r').Length, 0, EscPos58Encoder.Columns));
        Assert.Contains("Café Español", text);
        Assert.Contains("NIT: 900123456-7", text);
        Assert.Contains("Calle 10 # 20-30", text);
        Assert.Contains("María Muñoz", text);
        Assert.Contains("Fecha: 2026-09-14 12:05", text);
        Assert.Contains("TOTAL VENTA", text);
        Assert.Contains("COP 16.650,90", text);
        Assert.Contains("PAGADO VENTA", text);
        Assert.Contains("COP 14.800,90", text);
        Assert.Contains("Descuento Porcentaje", text);
        Assert.Contains("Propina incluida", text);
        Assert.Contains("COP 1.850,00", text);
        Assert.Contains("PAGOS", text);
        Assert.Contains("Efectivo", text);
        Assert.Contains("Tarjeta", text);
        Assert.Contains("Transferencia", text);
        Assert.Contains("CRÉDITO", text);
        Assert.Contains("Saldo crédito", text);
        Assert.Contains("José Pérez", text);
        Assert.DoesNotContain("Gracias por su visita", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Receipt_NeverContainsDrawerPulse()
    {
        var bytes = _encoder.EncodeReceipt(ReceiptTestData.CreateDocument());

        Assert.False(Contains(bytes, [0x1B, 0x70]));
    }

    private static string DecodeText(byte[] bytes)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        using var textBytes = new MemoryStream();
        for (var index = 0; index < bytes.Length;)
        {
            if (bytes[index] == 0x1B && index + 1 < bytes.Length)
            {
                index += bytes[index + 1] == 0x40 ? 2 : 3;
                continue;
            }

            if (bytes[index] == 0x1D && index + 2 < bytes.Length && bytes[index + 1] == 0x56)
            {
                index += 3;
                continue;
            }

            textBytes.WriteByte(bytes[index++]);
        }

        return Encoding.GetEncoding(858).GetString(textBytes.ToArray());
    }

    private static bool Contains(byte[] source, byte[] expected)
    {
        for (var index = 0; index <= source.Length - expected.Length; index++)
        {
            if (source.AsSpan(index, expected.Length).SequenceEqual(expected))
            {
                return true;
            }
        }

        return false;
    }
}
