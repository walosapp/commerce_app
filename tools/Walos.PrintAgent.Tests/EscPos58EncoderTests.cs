using System.Text;
using Walos.PrintAgent.Api;
using Walos.PrintAgent.Printing;

namespace Walos.PrintAgent.Tests;

public sealed class EscPos58EncoderTests
{
    private readonly EscPos58Encoder _encoder = new();
    private readonly PrinterConfiguration _configuration = new(
        1, 2, "POS-CAJA-1", "DIG-58IIA", 0, 50, 200);

    [Fact]
    public void TestTicket_StartsWithEscPosInitialize()
    {
        var bytes = _encoder.EncodeTestTicket(_configuration);

        Assert.Equal([0x1B, 0x40], bytes[..2]);
    }

    [Fact]
    public void Wrap_UsesAtMost32Columns()
    {
        var lines = EscPos58Encoder.Wrap(
            "Este es un texto deliberadamente largo para validar el ancho real del papel térmico de 58 mm");

        Assert.All(lines, line => Assert.InRange(line.Length, 0, EscPos58Encoder.Columns));
        Assert.True(lines.Count > 1);
    }

    [Fact]
    public void TestTicket_EncodesSpanishCharactersWithCp858()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var encoding = Encoding.GetEncoding(858);
        var bytes = _encoder.EncodeTestTicket(_configuration);

        Assert.True(Contains(bytes, encoding.GetBytes("áéíóú")));
        Assert.True(Contains(bytes, encoding.GetBytes("ñ Ñ ü")));
    }

    [Fact]
    public void Amount_HasCurrencyAndTwoDecimals()
    {
        var amount = EscPos58Encoder.FormatAmount(1234.56m);

        Assert.Contains("$", amount);
        Assert.Equal("TOTAL     $ 1.234,56", amount);
    }

    [Fact]
    public void Sanitize_RemovesEscAndOtherControlCharacters()
    {
        var sanitized = EscPos58Encoder.Sanitize("normal\u001B@\0\r\notro");

        Assert.DoesNotContain('\u001B', sanitized);
        Assert.DoesNotContain('\0', sanitized);
        Assert.Equal("normal@  otro", sanitized);
    }

    [Theory]
    [InlineData(0, 50, 200, new byte[] { 0x1B, 0x70, 0x00, 25, 100 })]
    [InlineData(1, 100, 500, new byte[] { 0x1B, 0x70, 0x01, 50, 250 })]
    public void DrawerPulse_HasExactBytes(int pin, int onMs, int offMs, byte[] expected)
    {
        Assert.Equal(expected, _encoder.EncodeDrawerPulse(pin, onMs, offMs));
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
