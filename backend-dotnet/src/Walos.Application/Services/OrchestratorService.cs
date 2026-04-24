using System.Text.Json;
using Microsoft.Extensions.Logging;
using Walos.Application.DTOs.Ai;
using Walos.Domain.Interfaces;

namespace Walos.Application.Services;

public class OrchestratorService
{
    private readonly IAiSessionRepository _sessions;
    private readonly IAiService _aiService;
    private readonly IInventoryRepository _inventory;
    private readonly IDeliveryRepository _delivery;
    private readonly ISuppliersRepository _suppliers;
    private readonly ILogger<OrchestratorService> _logger;

    private static readonly JsonSerializerOptions _json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public OrchestratorService(
        IAiSessionRepository sessions,
        IAiService aiService,
        IInventoryRepository inventory,
        IDeliveryRepository delivery,
        ISuppliersRepository suppliers,
        ILogger<OrchestratorService> logger)
    {
        _sessions = sessions;
        _aiService = aiService;
        _inventory = inventory;
        _delivery = delivery;
        _suppliers = suppliers;
        _logger = logger;
    }

    public async Task<AiChatResponse> ChatAsync(long companyId, long userId, long branchId, string companyName, string message, long? sessionId)
    {
        var session = sessionId.HasValue
            ? await _sessions.GetSessionAsync(sessionId.Value, companyId) ?? await _sessions.GetOrCreateSessionAsync(companyId, userId)
            : await _sessions.GetOrCreateSessionAsync(companyId, userId);

        await _sessions.AddMessageAsync(session.Id, "user", message);

        var history = await _sessions.GetMessagesAsync(session.Id);
        var context = ParseContext(session.Context);

        // Check if we are in a multi-turn flow waiting for user input
        if (context.TryGetValue("flow", out var flow))
        {
            var flowResult = await HandleFlowAsync(session.Id, companyId, branchId, companyName, message, flow.ToString()!, context, userId);
            await _sessions.AddMessageAsync(session.Id, "assistant", flowResult.Message,
                flowResult.Payload != null ? JsonSerializer.Serialize(flowResult.Payload, _json) : "{}");
            return flowResult;
        }

        // Classify intent via orchestrator
        var intent = await ClassifyIntentAsync(message, history.Select(m => $"{m.Role}: {m.Content}").ToList());
        _logger.LogInformation("Orchestrator intent: {Intent} for session {SessionId}", intent, session.Id);

        AiChatResponse response;
        switch (intent)
        {
            case "inventory":
                response = await HandleInventoryAsync(session.Id, companyId, branchId, companyName, message, context, userId);
                break;
            case "delivery":
                response = await HandleDeliveryAsync(session.Id, companyId, branchId, message, context);
                break;
            default:
                response = await HandleGeneralAsync(session.Id, companyId, companyName, message, history);
                break;
        }

        response.SessionId = session.Id;
        await _sessions.AddMessageAsync(session.Id, "assistant", response.Message,
            response.Payload != null ? JsonSerializer.Serialize(response.Payload, _json) : "{}");

        return response;
    }

    // ─────────────────────────────────────────
    // INTENT CLASSIFICATION
    // ─────────────────────────────────────────
    private async Task<string> ClassifyIntentAsync(string message, List<string> history)
    {
        var historyText = history.Count > 0
            ? string.Join("\n", history.TakeLast(6))
            : "Sin historial previo.";

        var prompt = $@"Eres un clasificador de intenciones para un sistema de gestión de restaurantes/bares.
Analiza el mensaje del usuario y responde ÚNICAMENTE con una de estas palabras exactas:
- inventory  (preguntas o acciones sobre productos, stock, inventario, pedidos a proveedores)
- delivery   (preguntas o acciones sobre domicilios, pedidos de clientes, repartidores, estado de entrega)
- general    (cualquier otra cosa: ventas, finanzas, reportes, preguntas generales)

Historial reciente:
{historyText}

Mensaje actual: {message}

Responde solo con la palabra exacta (inventory, delivery o general):";

        var result = await _aiService.ClassifyAsync(prompt);
        var clean = result.Trim().ToLowerInvariant();
        return clean is "inventory" or "delivery" ? clean : "general";
    }

    // ─────────────────────────────────────────
    // INVENTORY AGENT
    // ─────────────────────────────────────────
    private async Task<AiChatResponse> HandleInventoryAsync(
        long sessionId, long companyId, long branchId, string companyName,
        string message, Dictionary<string, object> context, long userId)
    {
        var products = await _inventory.GetAllProductsAsync(companyId);
        var stock = await _inventory.GetStockByBranchAsync(branchId, companyId);
        var categories = await _inventory.GetCategoriesAsync(companyId);
        var units = await _inventory.GetUnitsAsync(companyId);

        var stockByProduct = stock.ToDictionary(s => s.ProductId);
        var lowStockItems = products
            .Where(p => stockByProduct.TryGetValue(p.Id, out var s) && s.Quantity <= p.MinStock)
            .Select(p => new
            {
                p.Id, p.Name, p.Sku,
                CurrentStock = stockByProduct[p.Id].Quantity,
                p.MinStock,
                Unit = units.FirstOrDefault(u => u.Id == p.UnitId)?.Abbreviation ?? ""
            }).ToList();

        var noStockItems = products
            .Where(p => !stockByProduct.ContainsKey(p.Id) || stockByProduct[p.Id].Quantity <= 0)
            .Select(p => new { p.Id, p.Name, p.Sku, CurrentStock = 0m, p.MinStock,
                Unit = units.FirstOrDefault(u => u.Id == p.UnitId)?.Abbreviation ?? "" }).ToList();

        var productListStr = string.Join(", ", products.Select(p => p.Name));
        var lowStockStr = lowStockItems.Count > 0
            ? string.Join("\n", lowStockItems.Select(i => $"- {i.Name} (SKU:{i.Sku}): stock={i.CurrentStock} {i.Unit}, mínimo={i.MinStock}"))
            : "Ninguno";
        var noStockStr = noStockItems.Count > 0
            ? string.Join("\n", noStockItems.Select(i => $"- {i.Name} (SKU:{i.Sku})"))
            : "Ninguno";

        var systemPrompt = $@"Eres el Agente de Inventario para {companyName}.
Tienes acceso en tiempo real al inventario del negocio.

STOCK BAJO:
{lowStockStr}

SIN STOCK:
{noStockStr}

TODOS LOS PRODUCTOS: {productListStr}
CATEGORÍAS: {string.Join(", ", categories.Select(c => c.Name))}
UNIDADES: {string.Join(", ", units.Select(u => $"{u.Name} ({u.Abbreviation})"))}

CAPACIDADES:
- Consultar stock (bajo, sin stock, por producto)
- Registrar entrada de stock (el usuario dice cuánto llegó)
- Iniciar flujo de orden de compra: mostrar checklist de productos con stock bajo, el usuario elige proveedor, generas el pedido y el mensaje de WhatsApp

FLUJOS ESPECIALES - responde con JSON:
Si el usuario quiere ver productos con stock bajo y posiblemente hacer un pedido, responde:
{{ ""action"": ""show_low_stock_checklist"", ""response"": ""[texto natural]"" }}

Si el usuario quiere registrar stock (ej: 'llegaron 10 cervezas'), responde:
{{ ""action"": ""add_stock"", ""response"": ""[texto natural]"", ""data"": {{ ""products"": [...] }} }}

Para todo lo demás, responde:
{{ ""action"": ""query"", ""response"": ""[texto natural]"" }}

Responde SIEMPRE en JSON válido. Idioma: español.";

        var aiRaw = await _aiService.ChatAsync(systemPrompt, message);

        try
        {
            var parsed = JsonSerializer.Deserialize<JsonElement>(aiRaw);
            var action = parsed.TryGetProperty("action", out var a) ? a.GetString() ?? "query" : "query";
            var responseText = parsed.TryGetProperty("response", out var r) ? r.GetString() ?? aiRaw : aiRaw;

            if (action == "show_low_stock_checklist")
            {
                var checklist = lowStockItems.Select(i => new AiChecklistItem
                {
                    Id = i.Id,
                    Sku = i.Sku ?? "",
                    Name = i.Name,
                    CurrentStock = i.CurrentStock,
                    MinStock = i.MinStock,
                    Unit = i.Unit,
                    Checked = true
                }).ToList();

                // Save flow state in session context
                context["flow"] = "awaiting_checklist_confirmation";
                context["checklist_items"] = JsonSerializer.Serialize(checklist, _json);
                await _sessions.UpdateSessionContextAsync(sessionId, JsonSerializer.Serialize(context, _json), "inventory");

                return new AiChatResponse
                {
                    AgentType = "inventory",
                    ResponseType = "checklist",
                    Message = responseText,
                    Payload = new AiChecklistPayload
                    {
                        Items = checklist,
                        NextAction = "select_supplier",
                        Prompt = "Desmarca los productos que NO quieres pedir, luego dime el proveedor."
                    }
                };
            }

            return new AiChatResponse
            {
                AgentType = "inventory",
                ResponseType = "text",
                Message = responseText
            };
        }
        catch
        {
            return new AiChatResponse { AgentType = "inventory", ResponseType = "text", Message = aiRaw };
        }
    }

    // ─────────────────────────────────────────
    // MULTI-TURN FLOW HANDLER
    // ─────────────────────────────────────────
    private async Task<AiChatResponse> HandleFlowAsync(
        long sessionId, long companyId, long branchId, string companyName,
        string message, string flow, Dictionary<string, object> context, long userId)
    {
        if (flow == "awaiting_checklist_confirmation")
        {
            // User has selected items and named a supplier
            var suppliersRaw = await _suppliers.GetAllAsync(companyId, null);
            var supplierNames = string.Join(", ", suppliersRaw.Select(s => s.Name));

            var checklistJson = context.TryGetValue("checklist_items", out var ci) ? ci.ToString()! : "[]";
            var checklistItems = JsonSerializer.Deserialize<List<AiChecklistItem>>(checklistJson, _json) ?? new();

            var systemPrompt = $@"Eres el Agente de Inventario para {companyName}.

El usuario estaba revisando estos productos con stock bajo:
{string.Join("\n", checklistItems.Select(i => $"- {i.Name}: stock={i.CurrentStock} {i.Unit}, mínimo={i.MinStock}"))}

Proveedores disponibles: {supplierNames}

El usuario acaba de responder: ""{message}""

Tu tarea:
1. Si el usuario eligió un proveedor (o escribió su nombre), confirma qué productos se van a pedir.
2. Genera un mensaje de WhatsApp para enviarle al proveedor con el listado de productos.
3. Responde en JSON:
{{
  ""action"": ""create_order"",
  ""response"": ""[texto natural confirmando el pedido]"",
  ""supplier_name"": ""[nombre del proveedor]"",
  ""supplier_phone"": ""[teléfono si lo conoces, sino vacío]"",
  ""whatsapp_message"": ""[mensaje formateado para WhatsApp con saltos de línea]"",
  ""items"": [{{ ""name"": ""..."", ""quantity_to_order"": 0 }}]
}}

Si el usuario no ha elegido proveedor aún, responde:
{{ ""action"": ""need_supplier"", ""response"": ""[pregunta cuál proveedor]"" }}

Idioma: español.";

            var aiRaw = await _aiService.ChatAsync(systemPrompt, message);

            try
            {
                var parsed = JsonSerializer.Deserialize<JsonElement>(aiRaw);
                var action = parsed.TryGetProperty("action", out var a) ? a.GetString() ?? "" : "";
                var responseText = parsed.TryGetProperty("response", out var r) ? r.GetString() ?? aiRaw : aiRaw;

                if (action == "need_supplier")
                {
                    return new AiChatResponse { AgentType = "inventory", ResponseType = "text", Message = responseText, SessionId = sessionId };
                }

                if (action == "create_order")
                {
                    var supplierName = parsed.TryGetProperty("supplier_name", out var sn) ? sn.GetString() ?? "" : "";
                    var supplierPhone = parsed.TryGetProperty("supplier_phone", out var sp) ? sp.GetString() ?? "" : "";
                    var waMsg = parsed.TryGetProperty("whatsapp_message", out var wm) ? wm.GetString() ?? "" : "";

                    // Clean flow from context
                    context.Remove("flow");
                    context.Remove("checklist_items");
                    await _sessions.UpdateSessionContextAsync(sessionId, JsonSerializer.Serialize(context, _json), "inventory");

                    var waUrl = !string.IsNullOrWhiteSpace(supplierPhone)
                        ? $"https://wa.me/{supplierPhone.Replace("+", "").Replace(" ", "")}?text={Uri.EscapeDataString(waMsg)}"
                        : $"https://wa.me/?text={Uri.EscapeDataString(waMsg)}";

                    return new AiChatResponse
                    {
                        AgentType = "inventory",
                        ResponseType = "whatsapp",
                        Message = responseText,
                        Payload = new AiWhatsAppPayload
                        {
                            Phone = supplierPhone,
                            Message = waMsg,
                            WhatsAppUrl = waUrl
                        }
                    };
                }
            }
            catch { }

            return new AiChatResponse { AgentType = "inventory", ResponseType = "text", Message = aiRaw, SessionId = sessionId };
        }

        // Unknown flow — reset
        context.Remove("flow");
        await _sessions.UpdateSessionContextAsync(sessionId, JsonSerializer.Serialize(context, _json), "orchestrator");
        return new AiChatResponse { AgentType = "orchestrator", ResponseType = "text", Message = "Entendido, ¿en qué más te puedo ayudar?", SessionId = sessionId };
    }

    // ─────────────────────────────────────────
    // DELIVERY AGENT
    // ─────────────────────────────────────────
    private async Task<AiChatResponse> HandleDeliveryAsync(
        long sessionId, long companyId, long branchId,
        string message, Dictionary<string, object> context)
    {
        var orders = await _delivery.GetOrdersAsync(companyId, branchId, null, DateTime.UtcNow.Date, DateTime.UtcNow);
        var activeOrders = orders.Take(20).ToList();

        var ordersStr = activeOrders.Count > 0
            ? string.Join("\n", activeOrders.Select(o =>
                $"- #{o.OrderNumber} | Cliente: {o.CustomerName} | Estado: {o.Status} | Dirección: {o.CustomerAddress}"))
            : "No hay pedidos activos hoy.";

        var systemPrompt = $@"Eres el Agente de Domicilios.

PEDIDOS DE HOY:
{ordersStr}

ESTADOS VÁLIDOS: pending, confirmed, preparing, ready, on_the_way, delivered, cancelled

CAPACIDADES:
- Consultar estado de un pedido por número o cliente
- Cambiar estado de un pedido (ej: 'el pedido #12 ya está listo')

Responde en JSON:
{{ ""action"": ""query"" | ""update_status"", ""response"": ""[texto natural]"", ""order_number"": ""..."", ""new_status"": ""..."" }}

Idioma: español.";

        var aiRaw = await _aiService.ChatAsync(systemPrompt, message);

        try
        {
            var parsed = JsonSerializer.Deserialize<JsonElement>(aiRaw);
            var action = parsed.TryGetProperty("action", out var a) ? a.GetString() ?? "query" : "query";
            var responseText = parsed.TryGetProperty("response", out var r) ? r.GetString() ?? aiRaw : aiRaw;

            if (action == "update_status" &&
                parsed.TryGetProperty("order_number", out var on) &&
                parsed.TryGetProperty("new_status", out var ns))
            {
                var orderNumber = on.GetString() ?? "";
                var newStatus = ns.GetString() ?? "";
                var order = activeOrders.FirstOrDefault(o =>
                    o.OrderNumber.Contains(orderNumber, StringComparison.OrdinalIgnoreCase));

                if (order != null)
                {
                    await _delivery.UpdateOrderStatusAsync(order.Id, companyId, newStatus, null, null, new Dictionary<string, DateTime?>());
                    return new AiChatResponse
                    {
                        AgentType = "delivery",
                        ResponseType = "delivery_status",
                        Message = responseText,
                        Payload = new AiDeliveryPayload
                        {
                            OrderId = order.Id,
                            OrderNumber = order.OrderNumber,
                            Status = newStatus,
                            CustomerName = order.CustomerName ?? "",
                            Address = order.CustomerAddress ?? ""
                        }
                    };
                }
            }

            return new AiChatResponse { AgentType = "delivery", ResponseType = "text", Message = responseText };
        }
        catch
        {
            return new AiChatResponse { AgentType = "delivery", ResponseType = "text", Message = aiRaw };
        }
    }

    // ─────────────────────────────────────────
    // GENERAL AGENT
    // ─────────────────────────────────────────
    private async Task<AiChatResponse> HandleGeneralAsync(
        long sessionId, long companyId, string companyName,
        string message, List<Domain.Entities.AiMessage> history)
    {
        var historyText = string.Join("\n", history.TakeLast(10).Select(m => $"{m.Role}: {m.Content}"));

        var systemPrompt = $@"Eres el asistente general de {companyName}, un sistema de gestión para restaurantes y bares.
Puedes ayudar con preguntas sobre ventas, finanzas, operaciones y estrategia de negocio.
Sé conciso, amigable y orientado a la acción. Idioma: español.

Historial reciente:
{historyText}";

        var responseText = await _aiService.ChatAsync(systemPrompt, message);
        return new AiChatResponse { AgentType = "general", ResponseType = "text", Message = responseText };
    }

    private Dictionary<string, object> ParseContext(string contextJson)
    {
        try { return JsonSerializer.Deserialize<Dictionary<string, object>>(contextJson, _json) ?? new(); }
        catch { return new(); }
    }
}
