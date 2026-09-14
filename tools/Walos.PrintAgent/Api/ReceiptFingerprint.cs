using System.Security.Cryptography;
using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Walos.PrintAgent.Api;

public static class ReceiptFingerprint
{
    private static readonly JsonSerializerOptions SourceOptions = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly JsonWriterOptions WriterOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Indented = false
    };

    public static string Compute(PrintReceiptRequest request)
    {
        var source = JsonSerializer.SerializeToElement(new
        {
            request.DocumentVersion,
            request.CompanyId,
            request.BranchId,
            request.OrderId,
            request.Receipt
        }, SourceOptions);

        using var output = new MemoryStream();
        using (var writer = new Utf8JsonWriter(output, WriterOptions))
        {
            WriteCanonical(writer, source);
        }

        return Convert.ToHexStringLower(SHA256.HashData(output.ToArray()));
    }

    private static void WriteCanonical(Utf8JsonWriter writer, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject().OrderBy(
                             property => property.Name,
                             StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    WriteCanonical(writer, property.Value);
                }
                writer.WriteEndObject();
                break;

            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                {
                    WriteCanonical(writer, item);
                }
                writer.WriteEndArray();
                break;

            case JsonValueKind.Number:
                if (!element.TryGetDecimal(out var number))
                {
                    throw new InvalidOperationException("El documento contiene un número no representable.");
                }
                writer.WriteRawValue(
                    number.ToString("0.############################", CultureInfo.InvariantCulture),
                    skipInputValidation: true);
                break;

            case JsonValueKind.String:
                writer.WriteStringValue(element.GetString()?.Normalize(NormalizationForm.FormC));
                break;

            default:
                element.WriteTo(writer);
                break;
        }
    }
}
