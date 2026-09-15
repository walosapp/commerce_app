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

            var systemPrompt = $@"Eres un asistente de inventario de solo lectura para un bar/restaurante.

PRODUCTOS REGISTRADOS EN LA BASE DE DATOS:
{productListStr}

CATEGORÍAS DISPONIBLES: {categoriesStr}
UNIDADES DISPONIBLES: {unitsStr}

REGLAS:
1. Responde únicamente consultas informativas sobre productos, categorías y unidades.
2. Las entradas, creaciones y demás mutaciones de stock NO están disponibles desde IA en V1.
3. Si el usuario pide modificar stock, indícale que debe hacerlo desde el módulo Inventario.
4. No afirmes que creaste, actualizaste o registraste datos.
5. Responde siempre en español y en JSON válido:
{{
  ""action"": ""query"",
  ""confidence"": 0,
  ""response"": ""respuesta en lenguaje natural al usuario"",
  ""data"": {{ ""products"": [], ""total"": 0 }}
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

            _logger.LogInformation("IA procesó consulta de inventario. Action: {Action}, Confidence: {Confidence}, Tokens: {Tokens}",
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

    public async Task<string> ClassifyAsync(string prompt)
    {
        var request = new OpenAiChatRequest
        {
            Model = _model,
            Messages = new List<OpenAiMessage>
            {
                new() { Role = "user", Content = prompt }
            },
            Temperature = 0.0,
            MaxTokens = 10
        };

        var response = await _httpClient.PostAsJsonAsync("https://api.openai.com/v1/chat/completions", request, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        });

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<OpenAiChatResponse>(new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        });

        return result?.Choices?.FirstOrDefault()?.Message?.Content?.Trim() ?? "general";
    }

    public async Task<string> ChatAsync(string systemPrompt, string userMessage, List<AiConversationMessage>? history = null)
    {
        var messages = new List<OpenAiMessage>
        {
            new() { Role = "system", Content = systemPrompt }
        };

        if (history?.Count > 0)
        {
            foreach (var msg in history.TakeLast(10))
                messages.Add(new OpenAiMessage { Role = msg.Role, Content = msg.Content });
        }

        messages.Add(new OpenAiMessage { Role = "user", Content = userMessage });

        var request = new OpenAiChatRequest
        {
            Model = _model,
            Messages = messages,
            Temperature = _temperature,
            MaxTokens = _maxTokens,
            ResponseFormat = new OpenAiResponseFormat { Type = "json_object" }
        };

        var response = await _httpClient.PostAsJsonAsync("https://api.openai.com/v1/chat/completions", request, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        });

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<OpenAiChatResponse>(new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        });

        return result?.Choices?.FirstOrDefault()?.Message?.Content?.Trim() ?? string.Empty;
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
