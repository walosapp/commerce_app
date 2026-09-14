using Microsoft.AspNetCore.Http.Features;
using Walos.PrintAgent.Security;

namespace Walos.PrintAgent.Api;

public sealed class AllowedOriginPolicy(string[] origins)
{
    private readonly HashSet<string> _origins = new(origins, StringComparer.OrdinalIgnoreCase);

    public bool IsAllowed(string origin) => _origins.Contains(origin);
}

public sealed class OriginValidationMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, AllowedOriginPolicy policy)
    {
        var isPublicHealthCheck = context.Request.Path.Equals("/v1/health");
        if (!context.Request.Headers.TryGetValue("Origin", out var values))
        {
            if (!isPublicHealthCheck)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new ErrorResponse(
                    "origin_required",
                    "El encabezado Origin es obligatorio."));
                return;
            }
        }
        else
        {
            var origin = values.ToString();
            if (origin.Equals("null", StringComparison.OrdinalIgnoreCase) || !policy.IsAllowed(origin))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new ErrorResponse(
                    "origin_not_allowed",
                    "El origen no está autorizado."));
                return;
            }
        }

        await next(context);
    }
}

public sealed class PayloadLimitMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var maxPayloadBytes = context.Request.Path.Equals("/v1/commands/print-receipt")
            ? PrintAgentApi.PrintReceiptMaxPayloadBytes
            : PrintAgentApi.DefaultMaxPayloadBytes;

        if (context.Request.ContentLength > maxPayloadBytes)
        {
            context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
            await context.Response.WriteAsJsonAsync(new ErrorResponse(
                "payload_too_large",
                $"El payload no puede exceder {maxPayloadBytes} bytes."));
            return;
        }

        var bodySizeFeature = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (bodySizeFeature is { IsReadOnly: false })
        {
            bodySizeFeature.MaxRequestBodySize = maxPayloadBytes;
        }

        if (HttpMethods.IsPost(context.Request.Method) || HttpMethods.IsPut(context.Request.Method))
        {
            if (!context.Request.HasJsonContentType())
            {
                context.Response.StatusCode = StatusCodes.Status415UnsupportedMediaType;
                await context.Response.WriteAsJsonAsync(new ErrorResponse(
                    "json_required",
                    "Solo se acepta application/json."));
                return;
            }

            if (context.Request.ContentLength is null)
            {
                using var limitedBody = new MemoryStream((int)maxPayloadBytes);
                var buffer = new byte[2048];
                long total = 0;
                while (true)
                {
                    var read = await context.Request.Body.ReadAsync(buffer, context.RequestAborted);
                    if (read == 0)
                    {
                        break;
                    }

                    total += read;
                    if (total > maxPayloadBytes)
                    {
                        context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
                        await context.Response.WriteAsJsonAsync(new ErrorResponse(
                            "payload_too_large",
                            $"El payload no puede exceder {maxPayloadBytes} bytes."));
                        return;
                    }

                    await limitedBody.WriteAsync(buffer.AsMemory(0, read), context.RequestAborted);
                }

                context.Request.Body = new MemoryStream(limitedBody.ToArray(), writable: false);
                context.Request.ContentLength = total;
            }
        }

        await next(context);
    }
}

public sealed class BearerTokenMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, PairingService pairing)
    {
        var path = context.Request.Path;
        if (HttpMethods.IsOptions(context.Request.Method) ||
            path.Equals("/v1/health") ||
            path.Equals("/v1/pair"))
        {
            await next(context);
            return;
        }

        var authorization = context.Request.Headers.Authorization.ToString();
        var token = authorization.StartsWith("Bearer ", StringComparison.Ordinal)
            ? authorization["Bearer ".Length..]
            : string.Empty;

        if (!pairing.ValidateToken(token))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new ErrorResponse(
                "invalid_token",
                "El token del agente es inválido."));
            return;
        }

        await next(context);
    }
}
