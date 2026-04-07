using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Walos.Domain.Interfaces;

namespace Walos.Infrastructure.Services;

public class OpenAiService : IAiService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<OpenAiService> _logger;
    private readonly string _model;
    private readonly int _maxTokens;
    private readonly double _temperature;

    public OpenAiService(HttpClient httpClient, IConfiguration configuration, ILogger<OpenAiService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
        _model = configuration["OpenAI:Model"] ?? "gpt-3.5-turbo";
        _maxTokens = int.Parse(configuration["OpenAI:MaxTokens"] ?? "1000");
        _temperature = double.Parse(configuration["OpenAI:Temperature"] ?? "0.7", CultureInfo.InvariantCulture);
    }

    public async Task<AiInventoryResponse> ProcessInventoryInputAsync(string userInput, AiContext context, List<AiConversationMessage>? history = null)
    {
        try
        {
            var productListStr = context.ExistingProductNames.Any()
                ? string.Join(", ", context.ExistingProductNames)
                : "Ninguno registrado";
            var categoriesStr = context.Categories.Any()
                ? string.Join(", ", context.Categories)
                : "Sin categorías";
            var unitsStr = context.Units.Any()
                ? string.Join(", ", context.Units)
                : "Sin unidades";

            var systemPrompt = $@"Eres un asistente de inventario para un bar/restaurante.
Tu trabajo es ayudar al usuario a gestionar su inventario de forma conversacional.

PRODUCTOS REGISTRADOS EN LA BASE DE DATOS (LISTA EXACTA):
{productListStr}

CATEGORÍAS DISPONIBLES: {categoriesStr}
UNIDADES DISPONIBLES: {unitsStr}

REGLAS ESTRICTAS:
1. Compara el producto mencionado por el usuario con la LISTA EXACTA de productos registrados arriba.
2. Si el nombre del producto coincide EXACTAMENTE con uno de la lista (ignorando mayúsculas): usa action=""add_stock"" con is_new=false. Usa el nombre EXACTO de la lista.
3. Si el producto NO está en la lista: usa action=""create_and_stock"" con is_new=true. NO inventes que existe.
4. Si el usuario da un total pero no costo unitario, CALCULA: unit_cost = total / quantity.
5. Para productos EXISTENTES (is_new=false): solo necesitas name, quantity, unit_cost. El sistema recalculará el costo promedio ponderado automáticamente.
6. Para productos NUEVOS (is_new=true): DEBES preguntar el MARGEN DE GANANCIA (%) deseado usando action=""need_info"" si no lo proporciona. Con el margen, sale_price se calcula como: unit_cost * (1 + profit_margin/100). Pregunta también categoría y unidad si faltan.
7. Campos requeridos para producto nuevo: name, quantity, unit_cost, profit_margin (%), category (de las disponibles), unit (de las disponibles), min_stock, description.
8. NUNCA digas que ya registraste algo. Di ""Propongo registrar..."" o ""¿Confirmas que deseas...?"". El usuario debe confirmar.
9. En la respuesta natural, muestra siempre el costo unitario calculado y, para productos nuevos, indica el margen y precio de venta resultante.
10. Responde siempre en español.

Responde SIEMPRE en JSON válido con esta estructura exacta:
{{
  ""action"": ""add_stock"" | ""create_and_stock"" | ""need_info"" | ""query"",
  ""confidence"": 0-100,
  ""response"": ""respuesta en lenguaje natural al usuario"",
  ""data"": {{
    ""products"": [
      {{
        ""name"": ""nombre del producto"",
        ""quantity"": 0,
        ""unit_cost"": 0,
        ""sale_price"": 0,
        ""profit_margin"": 0,
        ""category"": ""categoría"",
        ""unit"": ""unidad"",
        ""min_stock"": 0,
        ""description"": ""descripción"",
        ""is_new"": false
      }}
    ],
    ""total"": 0
  }}
}}

Contexto:
- Empresa: {context.CompanyName ?? "N/A"}
- Sucursal: {context.BranchName ?? "N/A"}";

            var messages = new List<OpenAiMessage>
            {
                new() { Role = "system", Content = systemPrompt }
            };

            if (history?.Count > 0)
            {
                foreach (var msg in history)
                    messages.Add(new OpenAiMessage { Role = msg.Role, Content = msg.Content });
            }

            messages.Add(new OpenAiMessage { Role = "user", Content = userInput });

            var request = new OpenAiChatRequest
            {
                Model = _model,
                Messages = messages,
                Temperature = _temperature,
                MaxTokens = _maxTokens
            };

            var response = await _httpClient.PostAsJsonAsync("https://api.openai.com/v1/chat/completions", request, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                _logger.LogError("OpenAI API error {StatusCode}: {Body}", response.StatusCode, errorBody);
                throw new HttpRequestException($"OpenAI API error {response.StatusCode}: {errorBody}");
            }

            var result = await response.Content.ReadFromJsonAsync<OpenAiChatResponse>(new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
            });

            var content = result?.Choices?.FirstOrDefault()?.Message?.Content ?? "{}";
            var aiData = JsonSerializer.Deserialize<AiInventoryResponseRaw>(content, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
            });

            _logger.LogInformation("IA procesó entrada de inventario. Action: {Action}, Confidence: {Confidence}, Tokens: {Tokens}",
                aiData?.Action, aiData?.Confidence, result?.Usage?.TotalTokens);

            return new AiInventoryResponse
            {
                Action = aiData?.Action ?? "query",
                Confidence = aiData?.Confidence ?? 0,
                Response = aiData?.Response ?? string.Empty,
                Data = aiData?.Data != null ? new AiInventoryData
                {
                    Products = aiData.Data.Products?.Select(p => new AiProductEntry
                    {
                        Name = p.Name,
                        Quantity = p.Quantity,
                        UnitCost = p.UnitCost,
                        SalePrice = p.SalePrice,
                        ProfitMargin = p.ProfitMargin,
                        Category = p.Category,
                        Unit = p.Unit,
                        MinStock = p.MinStock,
                        Description = p.Description,
                        IsNew = p.IsNew
                    }).ToList() ?? new List<AiProductEntry>(),
                    Total = aiData.Data.Total
                } : null,
                Metadata = new AiMetadata
                {
                    Model = result?.Model ?? _model,
                    TokensUsed = result?.Usage?.TotalTokens ?? 0
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en servicio de IA");
            throw new Exception("Error al procesar con IA: " + ex.Message, ex);
        }
    }

    public async Task<string> GenerateAlertSuggestionAsync(AlertData alert)
    {
        try
        {
            var prompt = $@"Tengo una alerta de inventario:
Tipo: {alert.Type}
Producto: {alert.ProductName}
Stock actual: {alert.CurrentStock}
Stock mínimo: {alert.MinStock}

Dame una sugerencia breve y accionable (máximo 100 palabras) sobre qué hacer.";

            var request = new OpenAiChatRequest
            {
                Model = "gpt-3.5-turbo",
                Messages = new List<OpenAiMessage>
                {
                    new() { Role = "user", Content = prompt }
                },
                Temperature = 0.7,
                MaxTokens = 150
            };

            var response = await _httpClient.PostAsJsonAsync("https://api.openai.com/v1/chat/completions", request, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });

            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<OpenAiChatResponse>(new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
            });

            return result?.Choices?.FirstOrDefault()?.Message?.Content?.Trim()
                ?? "Revisar stock y considerar realizar pedido.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generando sugerencia de alerta");
            return "Revisar stock y considerar realizar pedido.";
        }
    }

    public async Task<object> AnalyzeSalesTrendsAsync(object salesData)
    {
        try
        {
            var salesJson = JsonSerializer.Serialize(salesData, new JsonSerializerOptions { WriteIndented = true });

            var prompt = $@"Analiza estos datos de ventas y dame insights:
{salesJson}

Proporciona:
1. Productos más vendidos
2. Tendencias (subiendo/bajando)
3. Recomendaciones de stock
4. Oportunidades de margen

Responde en JSON con estructura clara.";

            var request = new OpenAiChatRequest
            {
                Model = _model,
                Messages = new List<OpenAiMessage>
                {
                    new() { Role = "user", Content = prompt }
                },
                Temperature = 0.5,
                MaxTokens = 800,
            };

            var response = await _httpClient.PostAsJsonAsync("https://api.openai.com/v1/chat/completions", request, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                _logger.LogError("OpenAI API error {StatusCode}: {Body}", response.StatusCode, errorBody);
                throw new HttpRequestException($"OpenAI API error {response.StatusCode}: {errorBody}");
            }

            var result = await response.Content.ReadFromJsonAsync<OpenAiChatResponse>(new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
            });

            var content = result?.Choices?.FirstOrDefault()?.Message?.Content ?? "{}";
            return JsonSerializer.Deserialize<object>(content) ?? new { };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analizando tendencias");
            throw;
        }
    }

    // Internal DTOs for OpenAI API
    private class OpenAiChatRequest
    {
        public string Model { get; set; } = string.Empty;
        public List<OpenAiMessage> Messages { get; set; } = new();
        public double Temperature { get; set; }
        public int MaxTokens { get; set; }
        public OpenAiResponseFormat? ResponseFormat { get; set; }
    }

    private class OpenAiMessage
    {
        public string Role { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
    }

    private class OpenAiResponseFormat
    {
        public string Type { get; set; } = string.Empty;
    }

    private class OpenAiChatResponse
    {
        public string? Model { get; set; }
        public List<OpenAiChoice>? Choices { get; set; }
        public OpenAiUsage? Usage { get; set; }
    }

    private class OpenAiChoice
    {
        public OpenAiMessage? Message { get; set; }
    }

    private class OpenAiUsage
    {
        public int TotalTokens { get; set; }
    }

    private class AiInventoryResponseRaw
    {
        public string? Action { get; set; }
        public int Confidence { get; set; }
        public string? Response { get; set; }
        public AiInventoryDataRaw? Data { get; set; }
    }

    private class AiInventoryDataRaw
    {
        public List<AiProductEntryRaw>? Products { get; set; }
        public decimal? Total { get; set; }
    }

    private class AiProductEntryRaw
    {
        public string Name { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal UnitCost { get; set; }
        public decimal SalePrice { get; set; }
        public decimal ProfitMargin { get; set; }
        public string? Category { get; set; }
        public string? Unit { get; set; }
        public decimal MinStock { get; set; }
        public string? Description { get; set; }
        public bool IsNew { get; set; }
    }
}
