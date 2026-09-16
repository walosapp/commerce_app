using System.Text.Json;
using Microsoft.Extensions.Logging;
using Walos.Application.DTOs.Ai;
using Walos.Domain.Entities;
using Walos.Domain.Exceptions;
using Walos.Domain.Features;
using Walos.Domain.Interfaces;

namespace Walos.Application.Services;

public class OrchestratorService
{
    private readonly IAiSessionRepository _sessions;
    private readonly IAiService _aiService;
    private readonly IInventoryRepository _inventory;
    private readonly IDeliveryRepository _delivery;
    private readonly ISuppliersRepository _suppliers;
    private readonly IAiCapabilityGuard _capabilityGuard;
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
        IAiCapabilityGuard capabilityGuard,
        ILogger<OrchestratorService> logger)
    {
        _sessions = sessions;
        _aiService = aiService;
        _inventory = inventory;
        _delivery = delivery;
        _suppliers = suppliers;
        _capabilityGuard = capabilityGuard;
        _logger = logger;
    }

    public async Task<AiChatResponse> ChatAsync(
        long companyId,
        long userId,
        long branchId,
        string companyName,
        string message,
        long? sessionId,
        bool trustedDevBypass = false)
    {
        await using var conversationLock = await _sessions.AcquireConversationLockAsync(companyId, userId);
        var capabilities = await _capabilityGuard.GetSnapshotAsync(companyId, trustedDevBypass);
        capabilities.Ensure(WalosFeatures.Ai);

        var session = sessionId.HasValue
            ? await _sessions.GetSessionAsync(sessionId.Value, companyId, userId) ?? await _sessions.GetOrCreateSessionAsync(companyId, userId)
            : await _sessions.GetOrCreateSessionAsync(companyId, userId);

        var context = ParseContext(session.Context);
        if (!context.TryGetValue("capability_fingerprint", out var storedFingerprint)
            || !string.Equals(storedFingerprint?.ToString(), capabilities.Fingerprint, StringComparison.Ordinal)
            || !TryGetContextLong(context, "branch_id", out var storedBranchId)
            || storedBranchId != branchId)
        {
            context = new Dictionary<string, object>
            {
                ["capability_fingerprint"] = capabilities.Fingerprint,
                ["branch_id"] = branchId
            };
            await _sessions.ResetSessionAsync(
                session.Id,
                companyId,
                userId,
                JsonSerializer.Serialize(context, _json));
        }

        var history = (await _sessions.GetMessagesAsync(session.Id, companyId, userId))
            .Where(aiMessage => HasMessageScope(aiMessage.Metadata, capabilities.Fingerprint, branchId))
            .ToList();
        var lastAgent = context.TryGetValue("last_agent", out var la) ? la.ToString() : "orchestrator";

        // Build conversation history for AI
        var conversationHistory = history
            .Where(m => m.Role is "user" or "assistant")
            .TakeLast(12)
            .Select(m => new AiConversationMessage { Role = m.Role, Content = m.Content })
            .ToList();

        // Check if we are in a multi-turn flow waiting for user input
        if (context.TryGetValue("flow", out var flow))
        {
            _logger.LogInformation("Continuing flow {Flow} for session {SessionId}", flow, session.Id);
            var flowResult = await HandleFlowAsync(
                session.Id, companyId, branchId, companyName, message,
                flow.ToString()!, context, userId, capabilities);
            flowResult.SessionId = session.Id;
            await _sessions.AddMessageAsync(
                session.Id, companyId, userId, "user", message, BuildMessageMetadata(capabilities.Fingerprint, branchId));
            await _sessions.AddMessageAsync(session.Id, companyId, userId, "assistant", flowResult.Message,
                BuildMessageMetadata(capabilities.Fingerprint, branchId, flowResult.Payload));
            return flowResult;
        }

        // Classify intent via orchestrator with context awareness
        var intent = await ClassifyIntentAsync(message, history.Select(m => $"{m.Role}: {m.Content}").ToList(), lastAgent!);
        _logger.LogInformation("Orchestrator intent: {Intent} (last_agent: {LastAgent}) for session {SessionId}", intent, lastAgent, session.Id);

        AiChatResponse response;
        switch (intent)
        {
            case "inventory":
                capabilities.Ensure(WalosFeatures.Inventory);
                context["last_agent"] = "inventory";
                await _sessions.UpdateSessionContextAsync(session.Id, companyId, userId, JsonSerializer.Serialize(context, _json), "inventory");
                response = await HandleInventoryAsync(
                    session.Id, companyId, branchId, companyName, message, context,
                    userId, capabilities, WalosFeatures.Inventory, conversationHistory);
                break;
            case "purchases":
                capabilities.Ensure(WalosFeatures.Purchases);
                context["last_agent"] = "inventory";
                await _sessions.UpdateSessionContextAsync(session.Id, companyId, userId, JsonSerializer.Serialize(context, _json), "inventory");
                response = await HandleInventoryAsync(
                    session.Id, companyId, branchId, companyName, message, context,
                    userId, capabilities, WalosFeatures.Purchases, conversationHistory);
                break;
            case "suppliers":
                response = await HandleSuppliersAsync(companyId, companyName, message, capabilities, conversationHistory);
                break;
            case "delivery":
                capabilities.Ensure(WalosFeatures.Delivery);
                context["last_agent"] = "delivery";
                await _sessions.UpdateSessionContextAsync(session.Id, companyId, userId, JsonSerializer.Serialize(context, _json), "delivery");
                response = await HandleDeliveryAsync(session.Id, companyId, branchId, message, context, capabilities, conversationHistory);
                break;
            default:
                response = await HandleGeneralAsync(session.Id, companyId, companyName, message, history);
                break;
        }

        response.SessionId = session.Id;
        await _sessions.AddMessageAsync(
            session.Id, companyId, userId, "user", message, BuildMessageMetadata(capabilities.Fingerprint, branchId));
        await _sessions.AddMessageAsync(session.Id, companyId, userId, "assistant", response.Message,
            BuildMessageMetadata(capabilities.Fingerprint, branchId, response.Payload));

        return response;
    }

    // ─────────────────────────────────────────
    // INTENT CLASSIFICATION
    // ─────────────────────────────────────────
    private async Task<string> ClassifyIntentAsync(string message, List<string> history, string lastAgent)
    {
        var historyText = history.Count > 0
            ? string.Join("\n", history.TakeLast(8))
            : "Sin historial previo.";

        var prompt = $@"Eres un clasificador de intenciones para un sistema POS de restaurantes/bares.

CONTEXTO ACTUAL:
- Agente anterior: {lastAgent}
- Si el usuario está respondiendo a una pregunta del agente anterior, MANTENER el mismo agente
- Solo cambiar de agente si el usuario cambia explícitamente de tema

CATEGORÍAS:
- inventory: productos, stock, inventario, 'llegaron productos', 'agregar stock'
- purchases: crear pedidos u órdenes de compra a proveedores, sugerencias de reposición
- suppliers: consultar proveedores concretos, datos o contacto de proveedor
- delivery: domicilios, pedidos de CLIENTES, repartidores, entregas, 'pedido #123', 'dónde está mi pedido'
- general: ventas, finanzas, reportes, ayuda general

REGLAS IMPORTANTES:
- 'quiero hacer un pedido' + contexto de proveedores/inventario = inventory
- 'quiero hacer un pedido' + contexto de comida/cliente = delivery
- 'llegaron X productos', 'agregar stock', 'recibí mercancia' = inventory (SIEMPRE)
- Si el último agente fue 'inventory' y el usuario menciona productos/stock/proveedores, mantener inventory
- Si el último agente fue 'delivery' y el usuario habla de pedidos/entregas, mantener delivery

Historial reciente:
{historyText}

Mensaje actual: {message}

Responde SOLO con: inventory | purchases | suppliers | delivery | general";

        var result = await _aiService.ClassifyAsync(prompt);
        var clean = result.Trim().ToLowerInvariant();
        _logger.LogDebug("Raw classification result: '{Result}' -> '{Clean}'", result, clean);
        return clean is "inventory" or "purchases" or "suppliers" or "delivery" ? clean : "general";
    }

    // ─────────────────────────────────────────
    // INVENTORY AGENT
    // ─────────────────────────────────────────
    private async Task<AiChatResponse> HandleInventoryAsync(
        long sessionId, long companyId, long branchId, string companyName,
        string message, Dictionary<string, object> context, long userId,
        AiCapabilitySnapshot capabilities, string requiredFeature,
        List<AiConversationMessage>? conversationHistory = null)
    {
        capabilities.Ensure(requiredFeature);
        var products = await _inventory.GetAllProductsAsync(companyId);
        var stock = await _inventory.GetStockByBranchAsync(branchId, companyId);
        var categories = await _inventory.GetCategoriesAsync(companyId);
        var units = await _inventory.GetUnitsAsync(companyId);

        var stockByProduct = stock.ToDictionary(s => s.ProductId);

        // Filtrar solo productos que controlan stock (TrackStock = true)
        var trackedProducts = products.Where(p => p.TrackStock).ToList();

        var lowStockItems = trackedProducts
            .Where(p => stockByProduct.TryGetValue(p.Id, out var s) && s.Quantity <= p.MinStock)
            .Select(p => new
            {
                p.Id, p.Name, p.Sku,
                CurrentStock = stockByProduct[p.Id].Quantity,
                p.MinStock,
                Unit = units.FirstOrDefault(u => u.Id == p.UnitId)?.Abbreviation ?? ""
            }).ToList();

        var noStockItems = trackedProducts
            .Where(p => !stockByProduct.ContainsKey(p.Id) || stockByProduct[p.Id].Quantity <= 0)
            .Select(p => new { p.Id, p.Name, p.Sku, CurrentStock = 0m, p.MinStock,
                Unit = units.FirstOrDefault(u => u.Id == p.UnitId)?.Abbreviation ?? "" }).ToList();

        // Incluir SKU en la lista para match preciso
        var productListStr = string.Join("\n", trackedProducts.Select(p =>
        {
            var qty = stockByProduct.TryGetValue(p.Id, out var s) ? s.Quantity : 0;
            return $"- {p.Name} (SKU: {p.Sku ?? "N/A"}) | stock: {qty}";
        }));
        var lowStockStr = lowStockItems.Count > 0
            ? string.Join("\n", lowStockItems.Select(i => $"- {i.Name} (SKU: {i.Sku}): stock={i.CurrentStock} {i.Unit}, mínimo={i.MinStock}"))
            : "Ninguno";
        var noStockStr = noStockItems.Count > 0
            ? string.Join("\n", noStockItems.Select(i => $"- {i.Name} (SKU: {i.Sku})"))
            : "Ninguno";

        // Contar productos que NO controlan stock para contexto del agente
        var nonTrackedProducts = products.Where(p => !p.TrackStock).ToList();
        var nonTrackedStr = nonTrackedProducts.Count > 0
            ? $"Productos sin control de stock (siempre disponibles): {string.Join(", ", nonTrackedProducts.Select(p => p.Name))}"
            : "";

        var systemPrompt = $@"Eres el Agente de Inventario para {companyName}.
Tienes acceso en tiempo real al inventario del negocio.

STOCK BAJO (solo productos que controlan stock):
{lowStockStr}

SIN STOCK (solo productos que controlan stock):
{noStockStr}

{nonTrackedStr}

INVENTARIO COMPLETO (nombre, SKU, stock actual):
{productListStr}

CATEGORÍAS: {string.Join(", ", categories.Select(c => c.Name))}
UNIDADES: {string.Join(", ", units.Select(u => $"{u.Name} ({u.Abbreviation})"))}

IMPORTANTE:
- Los productos marcados como 'preparación' o 'servicio' NO controlan stock y siempre están disponibles
- Si el usuario pregunta por stock de estos productos, responde que están disponibles (no requieren inventario)
- Cuando el usuario mencione un producto, búscalo por nombre O por SKU en la lista de arriba
- Usa SIEMPRE el nombre EXACTO del producto tal como aparece en el inventario

CAPACIDADES:
- Consultar stock (bajo, sin stock, por producto) → usar action 'query'
- Las entradas de stock NO se registran desde IA en V1. Si el usuario lo pide, indícale que debe usar el módulo Inventario y responde con action 'query'
- Crear pedido a proveedor: SOLO cuando el usuario dice explícitamente que quiere hacer un pedido/orden de compra → usar action 'show_low_stock_checklist'

FLUJOS ESPECIALES - responde con JSON:

SI el usuario dice explícitamente que quiere crear un pedido, orden de compra, o hacer un pedido a proveedor (ej: 'quiero hacer un pedido', 'generar orden de compra', 'pedir productos'), Y hay productos con stock bajo, responde:
{{ ""action"": ""show_low_stock_checklist"", ""response"": ""[texto natural]"" }}

PARA CONSULTAS GENERALES de stock (ej: 'tengo stock bajo?', 'cuál es el stock?', 'hay productos sin stock?'), responde con action 'query' y proporciona la información directamente:
{{ ""action"": ""query"", ""response"": ""[texto natural con la información solicitada]"" }}

REGLA CRÍTICA: Solo usa 'show_low_stock_checklist' cuando el usuario EXPLICITAMENTE indique que quiere crear un pedido/orden de compra. Para consultas informativas sobre stock, usa 'query'.

Responde SIEMPRE en JSON válido. Idioma: español.";

        var aiRaw = await _aiService.ChatAsync(systemPrompt, message, conversationHistory);

        try
        {
            var parsed = JsonSerializer.Deserialize<JsonElement>(aiRaw);
            if (GetDisabledStockMutationRejection(parsed, "inventory") is { } rejection)
                return rejection;

            var action = GetStringProperty(parsed, "action") ?? "query";
            var responseText = parsed.TryGetProperty("response", out var r) && r.ValueKind == JsonValueKind.String
                ? r.GetString() ?? "No pude interpretar la respuesta del asistente."
                : "No pude interpretar la respuesta del asistente.";

            if (action == "show_low_stock_checklist")
            {
                capabilities.Ensure(WalosFeatures.Purchases);
                // Si no hay productos con stock bajo, responder con mensaje informativo
                if (lowStockItems.Count == 0)
                {
                    var noLowStockMessage = nonTrackedProducts.Count > 0
                        ? $"No tienes productos con stock bajo. Tus productos de tipo preparación ({string.Join(", ", nonTrackedProducts.Take(3).Select(p => p.Name))}{(nonTrackedProducts.Count > 3 ? "..." : "")} y {nonTrackedProducts.Count} más) no requieren control de inventario y siempre están disponibles."
                        : "No tienes productos con stock bajo en este momento. Todo tu inventario está bien abastecido.";

                    return new AiChatResponse
                    {
                        AgentType = "inventory",
                        ResponseType = "text",
                        Message = noLowStockMessage
                    };
                }

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
                context["flow_capability"] = WalosFeatures.Purchases;
                context["checklist_items"] = JsonSerializer.Serialize(checklist, _json);
                await _sessions.UpdateSessionContextAsync(sessionId, companyId, userId, JsonSerializer.Serialize(context, _json), "inventory");

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
        catch (FeatureNotEnabledException)
        {
            throw;
        }
        catch (JsonException)
        {
            return new AiChatResponse { AgentType = "inventory", ResponseType = "text", Message = "No pude interpretar la respuesta del asistente." };
        }
        catch (InvalidOperationException)
        {
            return new AiChatResponse { AgentType = "inventory", ResponseType = "text", Message = "No pude interpretar la respuesta del asistente." };
        }
    }

    // ─────────────────────────────────────────
    // MULTI-TURN FLOW HANDLER
    // ─────────────────────────────────────────
    private async Task<AiChatResponse> HandleFlowAsync(
        long sessionId, long companyId, long branchId, string companyName,
        string message, string flow, Dictionary<string, object> context, long userId,
        AiCapabilitySnapshot capabilities)
    {
        if (flow == "awaiting_checklist_confirmation")
        {
            capabilities.Ensure(WalosFeatures.Purchases);
            // Supplier data is a read-only shared dependency of the purchases helper.
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
                if (GetDisabledStockMutationRejection(parsed, "inventory", sessionId) is { } rejection)
                    return rejection;

                var action = parsed.TryGetProperty("action", out var a) ? a.GetString() ?? "" : "";
                var responseText = parsed.TryGetProperty("response", out var r) && r.ValueKind == JsonValueKind.String
                    ? r.GetString() ?? "No pude interpretar la respuesta del asistente."
                    : "No pude interpretar la respuesta del asistente.";

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
                    context.Remove("flow_capability");
                    context.Remove("checklist_items");
                    await _sessions.UpdateSessionContextAsync(sessionId, companyId, userId, JsonSerializer.Serialize(context, _json), "inventory");

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
            catch (JsonException) { }
            catch (InvalidOperationException) { }

            return new AiChatResponse { AgentType = "inventory", ResponseType = "text", Message = "No pude interpretar la respuesta del asistente.", SessionId = sessionId };
        }

        if (flow == "awaiting_stock_confirmation")
        {
            // Revalidate the current tenant capability before handling a legacy pending action.
            capabilities.Ensure(WalosFeatures.Inventory);
            context.Remove("flow");
            context.Remove("flow_capability");
            context.Remove("pending_stock");
            await _sessions.UpdateSessionContextAsync(
                sessionId, companyId, userId, JsonSerializer.Serialize(context, _json), "inventory");

            return new AiChatResponse
            {
                AgentType = "inventory",
                ResponseType = "text",
                Message = "Por seguridad, el ingreso de stock desde IA no esta disponible en V1. Registra la entrada desde Inventario para garantizar una operacion atomica y auditable.",
                SessionId = sessionId
            };
        }

        // Unknown flow — reset
        context.Remove("flow");
        await _sessions.UpdateSessionContextAsync(sessionId, companyId, userId, JsonSerializer.Serialize(context, _json), "orchestrator");
        return new AiChatResponse { AgentType = "orchestrator", ResponseType = "text", Message = "Entendido, ¿en qué más te puedo ayudar?", SessionId = sessionId };
    }

    // ─────────────────────────────────────────
    // DELIVERY AGENT
    // ─────────────────────────────────────────
    private async Task<AiChatResponse> HandleDeliveryAsync(
        long sessionId, long companyId, long branchId,
        string message, Dictionary<string, object> context,
        AiCapabilitySnapshot capabilities,
        List<AiConversationMessage>? conversationHistory = null)
    {
        capabilities.Ensure(WalosFeatures.Delivery);
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

        var aiRaw = await _aiService.ChatAsync(systemPrompt, message, conversationHistory);

        try
        {
            var parsed = JsonSerializer.Deserialize<JsonElement>(aiRaw);
            if (GetDisabledStockMutationRejection(parsed, "delivery") is { } rejection)
                return rejection;

            var action = parsed.TryGetProperty("action", out var a) ? a.GetString() ?? "query" : "query";
            var responseText = parsed.TryGetProperty("response", out var r) && r.ValueKind == JsonValueKind.String
                ? r.GetString() ?? "No pude interpretar la respuesta del asistente."
                : "No pude interpretar la respuesta del asistente.";

            if (action == "update_status")
            {
                return new AiChatResponse
                {
                    AgentType = "delivery",
                    ResponseType = "text",
                    Message = "Por seguridad, los cambios de estado deben realizarse desde el modulo de domicilios. Puedo ayudarte a consultar el pedido."
                };
            }

            return new AiChatResponse { AgentType = "delivery", ResponseType = "text", Message = responseText };
        }
        catch (JsonException)
        {
            return new AiChatResponse { AgentType = "delivery", ResponseType = "text", Message = "No pude interpretar la respuesta del asistente." };
        }
        catch (InvalidOperationException)
        {
            return new AiChatResponse { AgentType = "delivery", ResponseType = "text", Message = "No pude interpretar la respuesta del asistente." };
        }
    }

    private async Task<AiChatResponse> HandleSuppliersAsync(
        long companyId,
        string companyName,
        string message,
        AiCapabilitySnapshot capabilities,
        List<AiConversationMessage>? conversationHistory = null)
    {
        capabilities.Ensure(WalosFeatures.Suppliers);
        var suppliers = (await _suppliers.GetAllAsync(companyId, null)).ToList();
        var supplierNames = suppliers.Count == 0
            ? "No hay proveedores registrados."
            : string.Join(", ", suppliers.Select(supplier => supplier.Name));
        var prompt = $@"Eres el asistente de proveedores de {companyName}.
Proveedores registrados: {supplierNames}
Responde solo consultas informativas. No crees, edites ni elimines proveedores. Idioma: español.";
        var responseText = await _aiService.ChatAsync(prompt, message, conversationHistory);

        return new AiChatResponse
        {
            AgentType = "suppliers",
            ResponseType = "text",
            Message = responseText
        };
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

    private static bool HasMessageScope(string metadataJson, string fingerprint, long branchId)
    {
        try
        {
            var metadata = JsonSerializer.Deserialize<JsonElement>(metadataJson);
            return metadata.TryGetProperty("capability_fingerprint", out var stored)
                && string.Equals(stored.GetString(), fingerprint, StringComparison.Ordinal)
                && metadata.TryGetProperty("branch_id", out var storedBranch)
                && storedBranch.TryGetInt64(out var messageBranchId)
                && messageBranchId == branchId;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryGetContextLong(
        IReadOnlyDictionary<string, object> context,
        string key,
        out long value)
    {
        value = 0;
        if (!context.TryGetValue(key, out var raw) || raw is null)
            return false;

        return raw switch
        {
            long number => SetContextLong(number, out value),
            int number => SetContextLong(number, out value),
            JsonElement { ValueKind: JsonValueKind.Number } element => element.TryGetInt64(out value),
            _ => long.TryParse(raw.ToString(), out value)
        };
    }

    private static bool SetContextLong(long number, out long value)
    {
        value = number;
        return true;
    }

    private static string? GetStringProperty(JsonElement element, params string[] names)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (names.Any(name => string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                && property.Value.ValueKind == JsonValueKind.String)
                return property.Value.GetString();
        }

        return null;
    }

    private static bool IsDisabledStockMutation(string? operation) =>
        operation is not null
        && (operation.Equals("add_stock", StringComparison.OrdinalIgnoreCase)
            || operation.Equals("create_and_stock", StringComparison.OrdinalIgnoreCase));

    private static AiChatResponse? GetDisabledStockMutationRejection(
        JsonElement response,
        string agentType,
        long? sessionId = null)
    {
        var action = GetStringProperty(response, "action");
        var toolName = GetStringProperty(response, "toolName", "tool_name");
        if (!IsDisabledStockMutation(action) && !IsDisabledStockMutation(toolName))
            return null;

        var rejection = new AiChatResponse
        {
            AgentType = agentType,
            ResponseType = "text",
            Message = "Por seguridad, el ingreso de stock desde IA no esta disponible en V1. Registra la entrada desde Inventario para garantizar una operacion atomica y auditable."
        };
        if (sessionId.HasValue)
            rejection.SessionId = sessionId.Value;

        return rejection;
    }

    private static string BuildMessageMetadata(string fingerprint, long branchId, object? payload = null) =>
        JsonSerializer.Serialize(new
        {
            capability_fingerprint = fingerprint,
            branch_id = branchId,
            payload
        }, _json);
}
