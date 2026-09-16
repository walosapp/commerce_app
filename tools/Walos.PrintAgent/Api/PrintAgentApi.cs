using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Walos.PrintAgent.Commands;
using Walos.PrintAgent.Printing;
using Walos.PrintAgent.Security;
using Walos.PrintAgent.Storage;

namespace Walos.PrintAgent.Api;

public static class PrintAgentApi
{
    private static readonly string[] CanonicalAllowedOrigins =
    [
        "https://commerce-app-red.vercel.app",
        "http://localhost:5173",
        "http://127.0.0.1:5173"
    ];

    public const int Port = 17831;
    public const long DefaultMaxPayloadBytes = 8 * 1024;
    public const long PrintReceiptMaxPayloadBytes = 128 * 1024;
    public const long MaxPayloadBytes = PrintReceiptMaxPayloadBytes;

    public static void ConfigureServices(
        IServiceCollection services,
        IConfiguration configuration,
        string stateDirectory)
    {
        var allowedOrigins = ReadAllowedOrigins(configuration);

        services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;
            options.SerializerOptions.PropertyNameCaseInsensitive = false;
        });

        services.AddCors(options => options.AddPolicy("WalosOnly", policy =>
            policy.WithOrigins(allowedOrigins)
                .WithMethods("GET", "POST", "PUT", "OPTIONS")
                .WithHeaders("Authorization", "Content-Type")));

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    context.Connection.RemoteIpAddress?.ToString() ?? "loopback",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 60,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    }));
            options.AddFixedWindowLimiter("pair", limiter =>
            {
                limiter.PermitLimit = 10;
                limiter.Window = TimeSpan.FromMinutes(1);
                limiter.QueueLimit = 0;
                limiter.AutoReplenishment = true;
            });
        });

        services.AddSingleton(new AgentStateStore(stateDirectory));
        services.AddSingleton<ITokenProtector, DpapiTokenProtector>();
        services.AddSingleton<PairingService>();
        services.AddSingleton<IPrinterCatalog, WindowsPrinterCatalog>();
        services.AddSingleton<IRawPrinter, Win32RawPrinter>();
        services.AddSingleton<EscPos58Encoder>();
        services.AddSingleton<PrintCommandService>();
        services.AddSingleton<IdempotentJobExecutor>();
        services.AddSingleton(new AllowedOriginPolicy(allowedOrigins));
    }

    public static void ConfigurePipeline(WebApplication app)
    {
        app.UseMiddleware<OriginValidationMiddleware>();
        app.UseCors("WalosOnly");
        // CORS must run before local validation errors so an authorized Walos origin
        // can read 413/415 responses. The explicit origin guard still runs first.
        app.UseMiddleware<PayloadLimitMiddleware>();
        app.UseRateLimiter();
        app.UseMiddleware<BearerTokenMiddleware>();

        app.MapGet("/v1/health", (AgentStateStore store) => Results.Ok(new
        {
            status = "ok",
            version = GetSemanticVersion(),
            paired = store.IsPaired
        }));

        app.MapPost("/v1/pair", async (PairRequest request, PairingService pairing, CancellationToken ct) =>
        {
            var error = RequestValidation.Validate(request);
            if (error is not null)
            {
                return Results.BadRequest(new ErrorResponse("invalid_request", error));
            }

            var result = await pairing.PairAsync(request, ct);
            return result is null
                ? Results.Json(new ErrorResponse("invalid_pairing_code", "El código de vinculación es inválido."), statusCode: 401)
                : Results.Ok(result);
        }).RequireRateLimiting("pair");

        app.MapGet("/v1/printers", (IPrinterCatalog printers, AgentStateStore store) => Results.Ok(new PrintersResponse(
            printers.GetInstalledPrinters(),
            store.GetPrinterConfiguration()?.PrinterName)));

        app.MapPut("/v1/config/printer", async (
            PrinterConfiguration request,
            AgentStateStore store,
            IPrinterCatalog printers,
            CancellationToken ct) =>
        {
            var error = RequestValidation.Validate(request);
            if (error is not null)
            {
                return Results.BadRequest(new ErrorResponse("invalid_request", error));
            }

            if (!printers.Exists(request.PrinterName))
            {
                return Results.NotFound(new ErrorResponse("printer_not_found", "La impresora seleccionada no está instalada."));
            }

            if (!store.MatchesPairedIdentity(request.CompanyId, request.BranchId, request.WorkstationId))
            {
                return Results.Conflict(new ErrorResponse(
                    "pairing_context_mismatch",
                    "La configuración no coincide con la empresa, sucursal y estación vinculadas."));
            }

            await store.SetPrinterConfigurationAsync(request, ct);
            return Results.Ok(request);
        });

        app.MapPost("/v1/commands/test-print", async (
            JobCommandRequest request,
            IdempotentJobExecutor jobs,
            PrintCommandService commands,
            CancellationToken ct) => await ExecuteCommandAsync(
                request,
                "test-print",
                jobs,
                commands.PrepareTestTicket,
                commands.SendAsync,
                ct));

        app.MapPost("/v1/commands/open-drawer", async (
            DrawerCommandRequest request,
            AgentStateStore store,
            IdempotentJobExecutor jobs,
            PrintCommandService commands,
            CancellationToken ct) =>
        {
            var error = RequestValidation.Validate(request);
            if (error is not null)
            {
                return Results.BadRequest(new ErrorResponse("invalid_request", error));
            }

            if (!store.MatchesPairedCompanyAndBranch(request.CompanyId, request.BranchId))
            {
                return Results.Conflict(new ErrorResponse(
                    "pairing_context_mismatch",
                    "La apertura no pertenece a la empresa y sucursal vinculadas."));
            }

            return await ExecuteCommandAsync(
                new JobCommandRequest(request.JobId),
                "open-drawer",
                jobs,
                commands.PrepareDrawerPulse,
                commands.SendAsync,
                ct);
        });

        app.MapPost("/v1/commands/print-receipt", async (
            PrintReceiptRequest request,
            AgentStateStore store,
            IdempotentJobExecutor jobs,
            PrintCommandService commands,
            CancellationToken ct) =>
        {
            var error = RequestValidation.Validate(request);
            if (error is not null)
            {
                return Results.BadRequest(new ErrorResponse("invalid_request", error));
            }

            if (!store.MatchesPairedCompanyAndBranch(request.CompanyId, request.BranchId))
            {
                return Results.Conflict(new ErrorResponse(
                    "pairing_context_mismatch",
                    "El recibo no pertenece a la empresa y sucursal vinculadas."));
            }

            var computedFingerprint = ReceiptFingerprint.Compute(request);
            if (!computedFingerprint.Equals(request.Fingerprint, StringComparison.Ordinal))
            {
                return Results.BadRequest(new ErrorResponse(
                    "fingerprint_mismatch",
                    "El fingerprint no coincide con el documento recibido."));
            }

            try
            {
                var result = await jobs.ExecuteAsync(
                    request.JobId,
                    "print-receipt",
                    computedFingerprint,
                    () => commands.PrepareReceipt(request.Receipt),
                    commands.SendAsync,
                    ct);
                return Results.Ok(new JobCommandResponse(request.JobId, result.Status, result.Executed));
            }
            catch (JobConflictException)
            {
                return Results.Conflict(new ErrorResponse(
                    "job_id_conflict",
                    "El jobId ya fue utilizado con otro comando o documento."));
            }
            catch (PrinterNotFoundException exception)
            {
                return Results.NotFound(new ErrorResponse("printer_not_found", exception.Message));
            }
            catch (PrinterNotConfiguredException exception)
            {
                return Results.BadRequest(new ErrorResponse("printer_not_configured", exception.Message));
            }
            catch (PrintSpoolerException exception)
            {
                return Results.Json(
                    new ErrorResponse("spooler_error", exception.Message),
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }
        });

        app.MapPost("/v1/commands/print-cash-close", async (
            PrintCashCloseRequest request,
            AgentStateStore store,
            IdempotentJobExecutor jobs,
            PrintCommandService commands,
            CancellationToken ct) =>
        {
            var error = RequestValidation.Validate(request);
            if (error is not null)
                return Results.BadRequest(new ErrorResponse("invalid_request", error));
            if (!store.MatchesPairedCompanyAndBranch(request.CompanyId, request.BranchId))
                return Results.Conflict(new ErrorResponse(
                    "pairing_context_mismatch",
                    "El cierre no pertenece a la empresa y sucursal vinculadas."));

            var computedFingerprint = ReceiptFingerprint.Compute(request);
            if (!computedFingerprint.Equals(request.Fingerprint, StringComparison.Ordinal))
                return Results.BadRequest(new ErrorResponse(
                    "fingerprint_mismatch",
                    "El fingerprint no coincide con el documento recibido."));

            try
            {
                var result = await jobs.ExecuteAsync(
                    request.JobId,
                    "print-cash-close",
                    computedFingerprint,
                    () => commands.PrepareCashClose(request.CashClose),
                    commands.SendAsync,
                    ct);
                return Results.Ok(new JobCommandResponse(request.JobId, result.Status, result.Executed));
            }
            catch (JobConflictException)
            {
                return Results.Conflict(new ErrorResponse(
                    "job_id_conflict",
                    "El jobId ya fue utilizado con otro comando o documento."));
            }
            catch (PrinterNotFoundException exception)
            {
                return Results.NotFound(new ErrorResponse("printer_not_found", exception.Message));
            }
            catch (PrinterNotConfiguredException exception)
            {
                return Results.BadRequest(new ErrorResponse("printer_not_configured", exception.Message));
            }
            catch (PrintSpoolerException exception)
            {
                return Results.Json(
                    new ErrorResponse("spooler_error", exception.Message),
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }
        });
    }

    private static async Task<IResult> ExecuteCommandAsync(
        JobCommandRequest request,
        string command,
        IdempotentJobExecutor jobs,
        Func<PreparedPrintJob> prepare,
        Func<PreparedPrintJob, CancellationToken, Task> operation,
        CancellationToken ct)
    {
        var error = RequestValidation.Validate(request);
        if (error is not null)
        {
            return Results.BadRequest(new ErrorResponse("invalid_request", error));
        }

        try
        {
            var result = await jobs.ExecuteAsync(request.JobId, command, prepare, operation, ct);
            return Results.Ok(new JobCommandResponse(request.JobId, result.Status, result.Executed));
        }
        catch (JobConflictException)
        {
            return Results.Conflict(new ErrorResponse(
                "job_id_conflict",
                "El jobId ya fue utilizado para otro comando."));
        }
        catch (PrinterNotFoundException exception)
        {
            return Results.NotFound(new ErrorResponse("printer_not_found", exception.Message));
        }
        catch (PrinterNotConfiguredException exception)
        {
            return Results.BadRequest(new ErrorResponse("printer_not_configured", exception.Message));
        }
        catch (PrintSpoolerException exception)
        {
            return Results.Json(
                new ErrorResponse("spooler_error", exception.Message),
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    private static string[] ReadAllowedOrigins(IConfiguration configuration)
    {
        var configured = configuration["WALOS_PRINT_AGENT_ALLOWED_ORIGINS"];
        var additionalOrigins = string.IsNullOrWhiteSpace(configured)
            ? []
            : configured.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (!string.IsNullOrWhiteSpace(configured) && additionalOrigins.Length == 0)
        {
            throw new InvalidOperationException("WALOS_PRINT_AGENT_ALLOWED_ORIGINS contiene un origen inválido.");
        }

        var normalizedAdditionalOrigins = additionalOrigins.Select(NormalizeConfiguredOrigin).ToArray();
        return CanonicalAllowedOrigins
            .Concat(normalizedAdditionalOrigins)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string NormalizeConfiguredOrigin(string origin)
    {
        if (origin == "*" ||
            !Uri.TryCreate(origin, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
            string.IsNullOrEmpty(uri.Host) ||
            !string.IsNullOrEmpty(uri.UserInfo) ||
            uri.AbsolutePath != "/" ||
            !string.IsNullOrEmpty(uri.Query) ||
            !string.IsNullOrEmpty(uri.Fragment))
        {
            throw new InvalidOperationException("WALOS_PRINT_AGENT_ALLOWED_ORIGINS contiene un origen inválido.");
        }

        var normalized = uri.GetLeftPart(UriPartial.Authority);
        if (uri.Scheme == Uri.UriSchemeHttp &&
            !CanonicalAllowedOrigins.Contains(normalized, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("WALOS_PRINT_AGENT_ALLOWED_ORIGINS contiene un origen inválido.");
        }

        if (!origin.Equals(normalized, StringComparison.OrdinalIgnoreCase) &&
            !origin.Equals(normalized + "/", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("WALOS_PRINT_AGENT_ALLOWED_ORIGINS contiene un origen inválido.");
        }

        return normalized;
    }

    private static string GetSemanticVersion()
    {
        var version = typeof(PrintAgentApi).Assembly.GetName().Version;
        return version is null ? "1.0.1" : version.ToString(3);
    }
}
